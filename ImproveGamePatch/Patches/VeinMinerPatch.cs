using System;
using System.Collections.Generic;
using HarmonyLib;
using ImproveGamePatch.Content;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Patches
{
    [HarmonyPatch]
    public class VeinMinerPatch
    {
        // Track whether the player is currently swinging a pickaxe.
        // ItemCheck_UseMiningTools_ActuallyUseMiningTool runs on both SP
        // and MP client, so this flag is valid in all modes.
        private static bool _usingMiningTools;

        // State passed from Prefix to Postfix within a single PickTile call.
        private static ushort _lastOreType;
        private static bool _lastTileActive;
        private static bool _isVeinMining; // re-entrancy guard

        private static int _popupTipTimer;

        private static readonly int[] Dx = { 0, 1, 0, -1 };
        private static readonly int[] Dy = { -1, 0, 1, 0 };

        private static bool IsOre(ushort type)
        {
            return TileID.Sets.Ore[type] ||
                   Main.tileOreFinderPriority[type] > 0 ||
                   Main.tileSpelunker[type];
        }

        /// <summary>
        /// BFS search for all connected tiles of the same ore type.
        /// Starts from neighbours of (cx,cy); (cx,cy) itself is not included
        /// since the vanilla PickTile already broke it.
        /// Results packed as (ulong)x << 32 | (uint)y.
        /// </summary>
        private static void SearchOres(int cx, int cy, ushort oreType, HashSet<ulong> ores)
        {
            var queue = new Queue<int>();
            for (int d = 0; d < 4; d++)
                queue.Enqueue((cx + Dx[d]) << 16 | (cy + Dy[d] & 0xFFFF));

            while (queue.Count > 0 && ores.Count < 599)
            {
                int packed = queue.Dequeue();
                int x = packed >> 16;
                int y = (short)(packed & 0xFFFF);

                if (!WorldGen.InWorld(x, y, 2))
                    continue;

                var tile = Main.tile[x, y];
                if (tile == null || !tile.active() || tile.type != oreType)
                    continue;

                ulong key = (ulong)x << 32 | (uint)y;
                if (!ores.Add(key))
                    continue;

                for (int d = 0; d < 4; d++)
                    queue.Enqueue((x + Dx[d]) << 16 | (y + Dy[d] & 0xFFFF));
            }
        }

        // ── SP / host-and-play: break everything locally ──────────────

        private static void DoVeinMiningSp(int x, int y, ushort oreType)
        {
            var ores = new HashSet<ulong>();
            SearchOres(x, y, oreType, ores);
            if (ores.Count == 0) return;

            _isVeinMining = true;
            try
            {
                foreach (var key in ores)
                {
                    int ox = (int)(key >> 32);
                    int oy = (int)(key & 0xFFFFFFFF);
                    WorldGen.KillTile(ox, oy);
                    Tile.SmoothSlope(ox, oy, applyToNeighbors: true, sync: true);
                }
            }
            finally { _isVeinMining = false; }
        }

        // ── MP dedicated-server client: only send msg17 to server ─────

        private static void DoVeinMiningMp(int x, int y, ushort oreType)
        {
            var ores = new HashSet<ulong>();
            SearchOres(x, y, oreType, ores);
            if (ores.Count == 0) return;

            foreach (var key in ores)
            {
                int ox = (int)(key >> 32);
                int oy = (int)(key & 0xFFFFFFFF);

                // Send tile-break request to server (msg17, action 0 = KillTile).
                // Server processes each msg17, breaks the tile, and syncs back.
                // No client-side KillTile – avoids item-dupe / desync risk.
                NetMessage.SendData(17, -1, -1, null, 0, ox, oy);
            }
        }

        // ── Harmony patches ───────────────────────────────────────────

        [HarmonyPatch(typeof(Player), "ItemCheck_UseMiningTools_ActuallyUseMiningTool")]
        [HarmonyPrefix]
        private static bool Prefix_UseMiningTool()
        {
            _usingMiningTools = true;
            return true;
        }

        [HarmonyPatch(typeof(Player), "ItemCheck_UseMiningTools_ActuallyUseMiningTool")]
        [HarmonyPostfix]
        private static void Postfix_UseMiningTool()
        {
            _usingMiningTools = false;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PickTile))]
        [HarmonyPrefix]
        private static bool Prefix_PickTile(Player __instance, int x, int y, int pickPower)
        {
            if (!Config.Get("VeinMiner", true)) return true;
            // Always reset per-call state
            _lastOreType = 0;
            _lastTileActive = false;

            if (_isVeinMining)
                return true;

            // Only trigger for actual mining (not explosive / worldgen damage).
            // _usingMiningTools is set by the mining-tool hook, which runs
            // on both SP and MP client.
            if (!_usingMiningTools)
                return true;

            var tile = Main.tile[x, y];
            if (tile == null || !tile.active())
                return true;

            ushort type = tile.type;
            if (TileID.Sets.IsAContainer[type] || !IsOre(type))
                return true;

            _lastOreType = type;
            _lastTileActive = true;
            return true;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PickTile))]
        [HarmonyPostfix]
        private static void Postfix_PickTile(Player __instance, int x, int y, int pickPower)
        {
            if (_isVeinMining)
                return;

            if (_lastOreType == 0)
                return;

            // Verify the tile actually broke (was active, now inactive)
            var tile = Main.tile[x, y];
            bool broken = _lastTileActive && (tile == null || !tile.active());
            if (!broken)
                return;

            // ── Mode dispatch ──
            if (Main.netMode == 1) // Multiplayer client
            {
                // Client: BFS-search locally, send individual msg17 tile-kill
                // requests to the server for each vein ore. The server does the
                // actual WorldGen.KillTile and syncs results back.
                DoVeinMiningMp(x, y, _lastOreType);
            }
            else // Singleplayer (0) or host-and-play (also 0 internally)
            {
                // Direct local break: instant, no networking overhead.
                DoVeinMiningSp(x, y, _lastOreType);
            }

            // Occasional notification (once per in-game hour)
            if (++_popupTipTimer > 60 * 60 * 60)
            {
                _popupTipTimer = 0;
                Main.NewText("VeinMiner: ore vein mined", 255, 255, 0);
            }
        }
    }
}
