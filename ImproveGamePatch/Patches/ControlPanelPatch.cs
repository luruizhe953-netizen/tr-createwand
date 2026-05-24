using HarmonyLib;
using ImproveGamePatch.UI;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ImproveGamePatch.Patches
{
    [HarmonyPatch]
    public class ControlPanelPatch
    {
        [HarmonyPatch(typeof(Main), "Update")]
        [HarmonyPostfix]
        private static void Postfix_MainUpdate()
        {
            ControlPanel.Update();
        }

        [HarmonyPatch(typeof(Main), "DrawInterface")]
        [HarmonyPostfix]
        private static void Postfix_DrawInterface()
        {
            if (!ControlPanel.IsOpen) return;
            var sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            ControlPanel.Draw(sb);
            sb.End();
        }
    }
}
