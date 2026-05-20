using System;
using Terraria;
using Terraria.ID;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 记录最近发出的 msg17 TileManipulation（仅客户端），用于把「本地判定成功」与「是否真的发过 PlaceTile/PlaceWall」串联到同一条诊断。
	/// </summary>
	internal static class CreateWandMpMsg17Probe
	{
		private struct RecentMsg17
		{
			public long UtcTicks;
			public int X;
			public int Y;
			public int Action;
			public string Source;
		}

		private static readonly object Gate = new object();
		private static RecentMsg17 _recent;

		public static void RegisterFromOutgoing(int msgType, int number, float number2, float number3, string source)
		{
			if (Main.netMode != 1 || msgType != MessageID.TileManipulation)
				return;
			int action = number;
			if (action != 1 && action != 3)
				return;

			lock (Gate)
			{
				_recent.UtcTicks = DateTime.UtcNow.Ticks;
				_recent.X = (int)number2;
				_recent.Y = (int)number3;
				_recent.Action = action;
				_recent.Source = source ?? "?";
			}
		}

		public static string DescribeRecentPlaceForCell(int x, int y, bool wall, int maxAgeMs)
		{
			if (Main.netMode != 1)
				return "n/a netMode!=1";

			long now = DateTime.UtcNow.Ticks;
			RecentMsg17 snap;
			lock (Gate)
				snap = _recent;

			if (snap.UtcTicks <= 0)
				return "none";
			long ageTicks = now - snap.UtcTicks;
			if (ageTicks < 0)
				ageTicks = 0;
			long maxTicks = (long)maxAgeMs * TimeSpan.TicksPerMillisecond;
			if (ageTicks > maxTicks)
				return "stale ageMs=" + (ageTicks / TimeSpan.TicksPerMillisecond);

			int expectAction = wall ? 3 : 1;
			if (snap.Action != expectAction || snap.X != x || snap.Y != y)
			{
				return "otherRecent action=" + snap.Action + " x=" + snap.X + " y=" + snap.Y +
				       " ageMs=" + (ageTicks / TimeSpan.TicksPerMillisecond) + " via=" + snap.Source;
			}

			return "match action=" + snap.Action + " x=" + snap.X + " y=" + snap.Y +
			       " ageMs=" + (ageTicks / TimeSpan.TicksPerMillisecond) + " via=" + snap.Source;
		}
	}
}
