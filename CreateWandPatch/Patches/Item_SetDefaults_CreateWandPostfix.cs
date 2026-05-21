using System.Linq;
using System.Reflection;
using CreateWandPatch.Content;
using HarmonyLib;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.UI;

namespace CreateWandPatch.Patches
{
	[HarmonyPatch]
	public static class Item_SetDefaults_CreateWandPostfix
	{
		[HarmonyTargetMethod]
		public static MethodBase TargetMethod()
		{
			// 1.4.5.x：SetDefaults(int Type, ItemVariant variant = null)；必须与 Postfix 参数一致，否则 Harmony 报 Unexpected null
			return typeof(Item).GetMethods()
				.FirstOrDefault(m =>
					m.Name == "SetDefaults" &&
					m.GetParameters().Length == 2 &&
					m.GetParameters()[0].ParameterType == typeof(int) &&
					m.GetParameters()[1].ParameterType == typeof(ItemVariant));
		}

		[HarmonyPostfix]
		public static void Postfix(Item __instance, int Type, ItemVariant variant)
		{
			if (Type != CreateWandIds.ItemType)
				return;

			// 若 Vanila 在部分版本/路径下把 6147 清成空气，再套模板并改回 type（与 Type 形参判断无关，看 __instance.type）
			if (__instance.type != CreateWandIds.ItemType)
			{
				__instance.SetDefaults(CreateWandIds.FallbackTemplateItem, null);
				__instance.type = CreateWandIds.ItemType;
			}

			__instance.SetNameOverride("创造魔杖");
			__instance.ToolTip = ItemTooltip.FromHardcodedText(
				"左键：在光标处放置 | 右键：打开模式面板（预设 / 蓝图 / 清空选项）",
				"1/2/3：快捷预设  [：快速⇄逐格  - / +：上一张 / 下一张蓝图  ]：开/关放置前清空区域；铺完自动删补料",
				"P：发魔杖  N：联机发包模式列表  蓝图：文档\\My Games\\Terraria\\CreateWand\\*.png");

			if (__instance.stack < 1 && __instance.maxStack > 0)
				__instance.stack = 1;
		}
	}
}
