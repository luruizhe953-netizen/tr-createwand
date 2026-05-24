using HarmonyLib;
using ImproveGamePatch.Content;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Patches
{
    [HarmonyPatch]
    public class BannerPatch
    {
        /// <summary>
        /// Add banner buffs from the player's inventory to SceneMetrics.
        /// In SP: only processes the local player (myPlayer check).
        /// On dedicated server: processes every player — all inventory banners
        /// get added to the global SceneMetrics each frame. This means on a
        /// multiplayer server all players benefit from everyone's inventory
        /// banners. Slightly generous, but simple and correct for solo servers.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        [HarmonyPostfix]
        private static void Postfix_PlayerUpdate(Player __instance)
        {
            if (!Config.Get("BannerPatch", true)) return;
            // SP / host-and-play: only process the local player
            if (!Main.dedServ && Main.myPlayer != __instance.whoAmI)
                return;

            ScanInventoryForBanners(__instance);
        }

        private static void ScanInventoryForBanners(Player player)
        {
            ScanItems(player.inventory, 50);
            ScanItems(player.bank.item);
        }

        private static void ScanItems(Item[] items, int count = -1)
        {
            if (items == null) return;
            int len = count >= 0 && count < items.Length ? count : items.Length;
            for (int i = 0; i < len; i++)
            {
                var item = items[i];
                if (item.IsAir) continue;

                // Banner items have createTile == TileID.Banners
                if (item.createTile == TileID.Banners)
                {
                    int npcType = item.placeStyle;
                    if (npcType >= 0 && npcType < Main.SceneMetrics.NPCBannerBuff.Length)
                    {
                        Main.SceneMetrics.NPCBannerBuff[npcType] = true;
                        Main.SceneMetrics.hasBanner = true;
                    }
                }
            }
        }
    }
}
