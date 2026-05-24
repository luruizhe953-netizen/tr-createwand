using HarmonyLib;
using ImproveGamePatch.Content;
using Terraria;
using Terraria.ID;

namespace ImproveGamePatch.Patches
{
    [HarmonyPatch]
    public class FasterExtractinatorPatch
    {
        [HarmonyPatch(typeof(Player), nameof(Player.PlaceThing))]
        [HarmonyPostfix]
        private static void Postfix_PlaceThing(Player __instance)
        {
            if (!Config.Get("FasterExtractinator", true)) return;
            if (Main.myPlayer != __instance.whoAmI)
                return;

            if (__instance.itemAnimation > 5)
            {
                int tx = Player.tileTargetX;
                int ty = Player.tileTargetY;
                if (WorldGen.InWorld(tx, ty))
                {
                    var tile = Main.tile[tx, ty];
                    if (tile != null && (tile.type == TileID.Extractinator || tile.type == TileID.ChlorophyteExtractinator))
                    {
                        __instance.itemAnimation = 1;
                        __instance.itemTime = 1;
                    }
                }
            }
        }
    }
}
