using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 绘制浮动文本前最后一次兜底（含 AdvancedPopupRequest.Text 为 null 等情况）。
	/// </summary>
	[HarmonyPatch(typeof(PopupText), nameof(PopupText.DrawItemTextPopups))]
	internal static class PopupText_DrawItemTextPopups_NullSafePrefix
	{
		[HarmonyPrefix]
		private static void Prefix()
		{
			for (int i = 0; i < PopupText.popupText.Length; i++)
			{
				PopupText p = PopupText.popupText[i];
				if (!p.active)
					continue;
				if (p.displayText == null)
					p.displayText = string.Empty;
			}
		}
	}
}
