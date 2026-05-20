using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CreateWandPatch.Rendering
{
	/// <summary>在 <see cref="Main.DoDraw"/> 开头与自定义叠加绘制前收束未配对的 <see cref="Main.spriteBatch"/>。</summary>
	internal static class SpriteBatchSafety
	{
		internal const int MaxUnwindAttempts = 6;

		internal static void TryUnwindMainSpriteBatch()
		{
			if (Main.dedServ || Main.spriteBatch == null)
				return;

			for (int i = 0; i < MaxUnwindAttempts; i++)
			{
				try
				{
					Main.spriteBatch.End();
				}
				catch (InvalidOperationException)
				{
					break;
				}
			}
		}
	}
}
