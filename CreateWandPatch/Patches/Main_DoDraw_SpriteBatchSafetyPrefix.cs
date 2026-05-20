using CreateWandPatch.Rendering;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// <see cref="Main.DoDraw"/> 开头会对 <see cref="Main.ContentThatNeedsRenderTargets"/> 调用
	/// <c>PrepareRenderTarget(device, Main.spriteBatch)</c>，内部会 <c>Begin</c>。
	/// 若上一帧结束时 <see cref="Main.spriteBatch"/> 仍处在未 <c>End</c> 的状态（旅途模式等路径更容易出现），
	/// 此处会抛「Begin cannot be called again until End…」。在帧初尝试 <c>End</c> 以收束残留批处理。
	/// </summary>
	[HarmonyPatch(typeof(Main), "DoDraw", new[] { typeof(GameTime) })]
	public static class Main_DoDraw_SpriteBatchSafetyPrefix
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		public static void Prefix()
		{
			SpriteBatchSafety.TryUnwindMainSpriteBatch();
		}
	}
}
