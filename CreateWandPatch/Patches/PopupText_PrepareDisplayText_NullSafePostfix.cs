using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Patches
{
	[HarmonyPatch(typeof(PopupText), "PrepareDisplayText")]
	internal static class PopupText_PrepareDisplayText_NullSafePostfix
	{
		[HarmonyPostfix]
		private static void Postfix(PopupText __instance)
		{
			if (__instance.displayText == null)
				__instance.displayText = string.Empty;
		}
	}
}
