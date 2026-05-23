using System.Collections.Generic;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 联机手持链失败后的 WorldGen 回退延迟队列：避免同一帧混用 PlaceThing 与 WG 导致服端更易回滚。
	/// </summary>
	internal static class CreateWandDeferredWgFallbackQueue
	{
		public const int TileFallbackDelayFrames = 4;

		private struct TileFallbackJob
		{
			public int DueFrame;
			public int PlayerWhoAmI;
			public int X;
			public int Y;
			public int TileType;
			public int Style;
		}

		private static readonly List<TileFallbackJob> Jobs = new List<TileFallbackJob>(64);
		private static int _frame;

		public static void EnqueueTileFallbackNextFrame(Player player, int x, int y, int tileType, int style)
		{
			if (Main.netMode != 1 || player == null || !player.active)
				return;

			// 同格去重：保留最新一次请求，避免短时间重复回退叠包。
			for (int i = Jobs.Count - 1; i >= 0; i--)
			{
				TileFallbackJob old = Jobs[i];
				if (old.X == x && old.Y == y)
					Jobs.RemoveAt(i);
			}

			Jobs.Add(new TileFallbackJob
			{
				DueFrame = _frame + TileFallbackDelayFrames,
				PlayerWhoAmI = player.whoAmI,
				X = x,
				Y = y,
				TileType = tileType,
				Style = style
			});
		}

		public static void ProcessFrame()
		{
			_frame++;
			if (Jobs.Count == 0 || Main.netMode != 1)
				return;

			for (int i = Jobs.Count - 1; i >= 0; i--)
			{
				TileFallbackJob j = Jobs[i];
				if (_frame < j.DueFrame)
					continue;

				Jobs.RemoveAt(i);
				if (j.PlayerWhoAmI < 0 || j.PlayerWhoAmI >= Main.player.Length)
					continue;
				Player p = Main.player[j.PlayerWhoAmI];
				if (p == null || !p.active)
					continue;

				CreateWandMpDebugLog.Write(
					"diag placeChain tile stage=FallbackWorldGenDelayedExecute x=" + j.X + " y=" + j.Y +
					" tileType=" + j.TileType + " style=" + j.Style + " delayFrames=" + TileFallbackDelayFrames);
				CreateWandPlacementService.TryPlaceTileVanilla(p, j.X, j.Y, j.TileType, j.Style);
			}
		}
	}
}
