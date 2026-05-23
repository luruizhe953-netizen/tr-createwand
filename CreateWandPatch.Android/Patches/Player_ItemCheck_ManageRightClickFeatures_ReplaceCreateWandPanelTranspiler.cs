using System.Collections.Generic;
using CreateWandPatch.Rendering;
using CreateWandPatch.UI;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 原版在 <see cref="Player.ItemCheck_ManageRightClickFeatures"/> 内先于 <see cref="Player.PlaceThing"/> 打开创造魔杖 Fancy UI，
	/// 会先置 <see cref="Main.inFancyUI"/>，导致 PlaceThing 前缀里的右键面板永远不执行。
	/// 将无参 <c>new UICreateWandPanel()</c>（类型可能对补丁工程不可见，故按命名空间+类名匹配）替换为 <see cref="CreateWandModePanel"/>。
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.ItemCheck_ManageRightClickFeatures))]
	public static class Player_ItemCheck_ManageRightClickFeatures_ReplaceCreateWandPanelTranspiler
	{
		private const string VanillaPanelNamespace = "Terraria.GameContent.UI.States";
		private const string VanillaPanelName = "UICreateWandPanel";

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			ConstructorInfo ours = typeof(CreateWandModePanel).GetConstructor(System.Type.EmptyTypes);
			foreach (var ci in instructions)
			{
				if (ci.opcode == OpCodes.Newobj &&
				    ci.operand is ConstructorInfo ctor &&
				    ctor.DeclaringType != null &&
				    ctor.GetParameters().Length == 0 &&
				    ctor.DeclaringType.Namespace == VanillaPanelNamespace &&
				    ctor.DeclaringType.Name == VanillaPanelName &&
				    ours != null)
				{
					ci.operand = ours;
				}

				yield return ci;
			}
		}
	}
}
