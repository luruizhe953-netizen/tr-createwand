using HarmonyLib;
using ImproveGamePatch.Content;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Patches
{
    /// <summary>
    /// Crafting stations, buff tiles, and liquid sources work from inventory.
    /// </summary>
    [HarmonyPatch]
    public class PortableStationPatch
    {
        [HarmonyPatch(typeof(Player), nameof(Player.AdjTiles))]
        [HarmonyPostfix]
        private static void Postfix_AdjTiles(Player __instance)
        {
            if (!Config.Get("PortableStation", true)) return;
            if (Main.myPlayer != __instance.whoAmI) return;

            for (int i = 0; i < 50; i++)
                ApplyItemAsStation(__instance.inventory[i], __instance);

            if (__instance.bank != null)
                for (int i = 0; i < __instance.bank.item.Length; i++)
                    ApplyItemAsStation(__instance.bank.item[i], __instance);
        }

        private static void ApplyItemAsStation(Item item, Player player)
        {
            if (item.IsAir || item.createTile < 0 || item.createTile >= player.adjTile.Length) return;

            int tt = item.createTile;
            player.adjTile[tt] = true;

            switch (tt)
            {
                case TileID.Hellforge: case TileID.GlassKiln: player.adjTile[TileID.Furnaces] = true; break;
                case TileID.AdamantiteForge: player.adjTile[TileID.Furnaces] = true; player.adjTile[TileID.Hellforge] = true; break;
                case TileID.MythrilAnvil: player.adjTile[TileID.Anvils] = true; break;
                case TileID.BewitchingTable: case TileID.Tables2: case TileID.PicnicTable: player.adjTile[TileID.Tables] = true; break;
                case TileID.AlchemyTable: player.adjTile[TileID.Bottles] = true; player.adjTile[TileID.Tables] = true; player.alchemyTable = true; break;
            }

            if (item.type == ItemID.WaterBucket || item.type == ItemID.BottomlessBucket) player.adjWaterSource = true;
            if (item.type == ItemID.LavaBucket || item.type == ItemID.BottomlessLavaBucket) player.adjLava = true;
            if (item.type == ItemID.HoneyBucket) player.adjHoney = true;
        }
    }
}
