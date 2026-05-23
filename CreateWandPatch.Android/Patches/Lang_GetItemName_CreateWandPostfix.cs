using CreateWandPatch.Content;
using Microsoft.Xna.Framework.Graphics;
using HarmonyLib;
using Terraria;
using Terraria.Localization;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 名称缓存未命中或本地化键缺失时，仍为 6147 提供可绘制字符串，防止弹窗文本为 null。
	/// </summary>
	[HarmonyPatch(typeof(Lang), nameof(Lang.GetItemName), new[] { typeof(int) })]
	internal static class Lang_GetItemName_CreateWandPostfix
	{
		[HarmonyPostfix]
		private static void Postfix(int id, ref LocalizedText __result)
		{
			if (id != CreateWandIds.ItemType)
				return;

			if (!string.IsNullOrEmpty(__result?.Value))
				return;

			__result = Language.GetText("ItemName.CreateWand");
		}
	}
}
