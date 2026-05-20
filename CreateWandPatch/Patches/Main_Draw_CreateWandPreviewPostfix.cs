using HarmonyLib;
using Microsoft.Xna.Framework;
using CreateWandPatch.Rendering;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 在整帧 <see cref="Main.Draw"/>（含 <see cref="Main.DoDraw"/>）结束后绘制蓝图预览。
	/// 配合 <see cref="Main_DoDraw_SpriteBatchSafetyPrefix"/> 在帧首收束 <see cref="Main.spriteBatch"/>；
	/// 预览使用独立 <see cref="SpriteBatch"/>，且在 Begin 前再次收束主批，减轻旅途模式 MapHeadRenderer 路径崩溃。
	/// </summary>
	[HarmonyPatch(typeof(Main), "Draw", new[] { typeof(GameTime) })]
	public static class Main_Draw_CreateWandPreviewPostfix
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		public static void Postfix()
		{
			if (Main.dedServ || Main.gameMenu || Main.showSplash)
				return;

			CreateWandWorldPreview.DrawAfterFrameWithOwnSpriteBatch();
		}
	}
}
