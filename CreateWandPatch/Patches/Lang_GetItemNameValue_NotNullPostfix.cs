using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// ReLogic 的 MeasureString 不接受 null；部分本地化路径会得到 null 字符串。
	/// </summary>
	[HarmonyPatch(typeof(Lang), nameof(Lang.GetItemNameValue), new[] { typeof(int) })]
	internal static class Lang_GetItemNameValue_NotNullPostfix
	{
		[HarmonyPostfix]
		private static void Postfix(ref string __result)
		{
			if (__result == null)
				__result = string.Empty;
		}
	}
}
