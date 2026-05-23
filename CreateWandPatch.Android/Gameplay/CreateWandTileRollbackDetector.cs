using System;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 联机客户端：登记魔杖成功写入的物块/墙，随后在窗口期内检测「曾符合期望 → 随后不符」或「始终未符合」，
	/// 用于解释「闪一下就没」（多为服端未采纳或下行区块覆盖）。需 <see cref="CreateWandSelectionState.EnableMpTileRollbackTrace"/>。
	/// </summary>
	internal static class CreateWandTileRollbackDetector
	{
		private const int SettleFrames = 2;
		private const int WatchWindowFrames = 180;
		private const int MaxWatches = 400;

		private enum WatchKind : byte
		{
			Tile,
			Wall
		}

		private struct Watch
		{
			public int X;
			public int Y;
			public WatchKind Kind;
			public ushort ExpectedTileType;
			public ushort ExpectedWallType;
			public int StartFrame;
			public int ExpireFrame;
			public string Source;
			public bool SawMatchOnce;
			public bool Finished;
		}

		private static readonly List<Watch> Watches = new List<Watch>(64);
		private static int _frame;

		/// <summary>检测到闪回或「从未稳定」后，若干帧内允许魔杖对 Kill/Place 使用多次重试（与 <see cref="CreateWandSelectionState.MpTileOperationRepeatCount"/> 配合）。</summary>
		private const int MpRepeatBoostWindowFrames = 180;

		private static int _repeatBoostUntilExclusiveFrame;

		/// <summary>
		/// 联机、已开闪回追踪、且近期日志中出现过 TileRollback（FLASHBACK / NO_STABILIZE）时，为 <c>true</c>。
		/// 供 <see cref="CreateWandPlacementService"/> 决定单次操作是否按 <see cref="CreateWandSelectionState.MpTileOperationRepeatCount"/> 重复。
		/// </summary>
		internal static bool ShouldBoostMpTileOperationRepeats =>
			Main.netMode == 1 &&
			CreateWandSelectionState.EnableMpTileRollbackTrace &&
			_frame < _repeatBoostUntilExclusiveFrame;

		/// <summary>每帧在 <see cref="Patches.Main_Update_CreateWandHotkeysPatch"/> 中、铺设队列之后调用。</summary>
		public static void ProcessFrame()
		{
			if (Main.netMode != 1 || !CreateWandSelectionState.EnableMpTileRollbackTrace)
				return;

			_frame++;
			int now = _frame;

			for (int i = Watches.Count - 1; i >= 0; i--)
			{
				Watch w = Watches[i];
				if (w.Finished)
				{
					Watches.RemoveAt(i);
					continue;
				}

				int age = now - w.StartFrame;
				if (age < SettleFrames)
					continue;

				Tile t = new Tile(w.Y * Main.maxTilesX + w.X);
				if (t == null)
				{
					if (now >= w.ExpireFrame)
						FinishNeverMatched(ref w, now, "tile null");
					else if (w.SawMatchOnce)
						ReportRollback(ref w, now, "tile became null");
					Watches[i] = w;
					continue;
				}

				bool match = w.Kind == WatchKind.Tile
					? t.active() && t.type == w.ExpectedTileType
					: t.wall == w.ExpectedWallType;

				if (match)
				{
					w.SawMatchOnce = true;
					Watches[i] = w;
					continue;
				}

				if (w.SawMatchOnce)
				{
					ReportRollback(ref w, now,
						w.Kind == WatchKind.Tile
							? DescribeTileMismatch(t, w.ExpectedTileType)
							: DescribeWallMismatch(t, w.ExpectedWallType));
				}
				else if (now >= w.ExpireFrame)
				{
					FinishNeverMatched(ref w, now,
						w.Kind == WatchKind.Tile
							? DescribeTileMismatch(t, w.ExpectedTileType)
							: DescribeWallMismatch(t, w.ExpectedWallType));
				}

				Watches[i] = w;
			}
		}

		private static string DescribeTileMismatch(Tile t, ushort expectedType)
		{
			if (!t.active())
				return "inactive wasActive=false";
			return "typeNow=" + t.type + " expectedTileType=" + expectedType;
		}

		private static string DescribeWallMismatch(Tile t, ushort expectedWall)
		{
			return "wallNow=" + t.wall + " expectedWall=" + expectedWall;
		}

		private static void ReportRollback(ref Watch w, int now, string detail)
		{
			int span = now - w.StartFrame;
			Player p = Main.LocalPlayer;
			CreateWandMpDebugLog.WriteTileRollback(
				"FLASHBACK kind=" + (w.Kind == WatchKind.Tile ? "tile" : "wall") +
				" x=" + w.X + " y=" + w.Y + " spanFrames=" + span + " source=" + w.Source +
				" detail=" + detail +
				" cellDiag={" + CreateWandMpPlacementDiagnostics.DescribeCellContext(p, w.X, w.Y) + "}" +
				" hint=曾符合期望后丢失，多见于服端未采纳放置或下行同步覆盖；对照 F9 网络日志");
			BoostMpRepeatsForAWhile();
			TryCombatTextFlashback(w.X, w.Y);
			w.Finished = true;
		}

		private static void FinishNeverMatched(ref Watch w, int now, string detail)
		{
			int span = now - w.StartFrame;
			Player p = Main.LocalPlayer;
			CreateWandMpDebugLog.WriteTileRollback(
				"NO_STABILIZE kind=" + (w.Kind == WatchKind.Tile ? "tile" : "wall") +
				" x=" + w.X + " y=" + w.Y + " spanFrames=" + span + " source=" + w.Source +
				" detail=" + detail +
				" cellDiag={" + CreateWandMpPlacementDiagnostics.DescribeCellContext(p, w.X, w.Y) + "}" +
				" hint=观测期内从未稳定为期望图格（可能未铺成功或瞬间被覆盖）");
			BoostMpRepeatsForAWhile();
			w.Finished = true;
		}

		private static void BoostMpRepeatsForAWhile()
		{
			_repeatBoostUntilExclusiveFrame = _frame + MpRepeatBoostWindowFrames;
		}

		private static long _nextCombatTextUtcTicks;

		private static void TryCombatTextFlashback(int tileX, int tileY)
		{
			long now = DateTime.UtcNow.Ticks;
			if (now < _nextCombatTextUtcTicks)
				return;
			_nextCombatTextUtcTicks = now + TimeSpan.TicksPerSecond * 2;

			Player p = Main.LocalPlayer;
			if (p == null || !p.active)
				return;

			int px = (int)(p.Center.X / 16f);
			int py = (int)(p.Center.Y / 16f);
			if (Math.Abs(px - tileX) > 48 || Math.Abs(py - tileY) > 48)
				return;

			CombatText.NewText(p.getRect(), Color.LightCoral,
				"[魔杖] 图格闪回（见 mp.log TileRollback）", false, false);
		}

		public static void RegisterTileExpectation(int x, int y, int tileType, string source)
		{
			if (Main.netMode != 1 || CreateWandSelectionState.MpLocalOnlyNoNet ||
			    !CreateWandSelectionState.EnableMpTileRollbackTrace)
				return;
			if (tileType < 0 || tileType > ushort.MaxValue)
				return;

			TrimOrDedup(x, y, WatchKind.Tile);
			Watches.Add(new Watch
			{
				X = x,
				Y = y,
				Kind = WatchKind.Tile,
				ExpectedTileType = (ushort)tileType,
				StartFrame = _frame,
				ExpireFrame = _frame + WatchWindowFrames,
				Source = source ?? "?"
			});
		}

		public static void RegisterWallExpectation(int x, int y, int wallType, string source)
		{
			if (Main.netMode != 1 || CreateWandSelectionState.MpLocalOnlyNoNet ||
			    !CreateWandSelectionState.EnableMpTileRollbackTrace)
				return;
			if (wallType <= 0 || wallType > ushort.MaxValue)
				return;

			TrimOrDedup(x, y, WatchKind.Wall);
			Watches.Add(new Watch
			{
				X = x,
				Y = y,
				Kind = WatchKind.Wall,
				ExpectedWallType = (ushort)wallType,
				StartFrame = _frame,
				ExpireFrame = _frame + WatchWindowFrames,
				Source = source ?? "?"
			});
		}

		private static void TrimOrDedup(int x, int y, WatchKind kind)
		{
			for (int i = Watches.Count - 1; i >= 0; i--)
			{
				Watch w = Watches[i];
				if (w.X == x && w.Y == y && w.Kind == kind)
					Watches.RemoveAt(i);
			}

			while (Watches.Count >= MaxWatches)
				Watches.RemoveAt(0);
		}
	}
}
