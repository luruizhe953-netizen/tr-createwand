using System;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Infrastructure
{
	/// <summary>复现 PlaceThing 前半段（油漆桶等），以便在 Prefix  return false 时不丢失原版行为。</summary>
	internal static class PlaceThingPrelude
	{
		private static readonly Action<Player> Paintbrush = CreateVoid("PlaceThing_Paintbrush");
		private static readonly Action<Player> PaintRoller = CreateVoid("PlaceThing_PaintRoller");
		private static readonly Action<Player> PaintScrapper = CreateVoid("PlaceThing_PaintScrapper");
		private static readonly Action<Player> CannonBall = CreateVoid("PlaceThing_CannonBall");
		private static readonly Action<Player> XMasTreeTops = CreateVoid("PlaceThing_XMasTreeTops");
		private static readonly Action<Player> LockChest = CreateVoid("PlaceThing_LockChest");

		private delegate void ExtractinatorDelegate(Player player, ref Terraria.Player.ItemCheckContext context);

		private static readonly ExtractinatorDelegate Extractinator =
			(ExtractinatorDelegate)Delegate.CreateDelegate(typeof(ExtractinatorDelegate),
				AccessTools.Method(typeof(Player), "PlaceThing_ItemInExtractinator"));

		private static Action<Player> CreateVoid(string name)
		{
			var m = AccessTools.Method(typeof(Player), name);
			return (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), m);
		}

		public static void Run(Player player, ref Terraria.Player.ItemCheckContext context)
		{
			Paintbrush(player);
			PaintRoller(player);
			PaintScrapper(player);
			CannonBall(player);
			XMasTreeTops(player);
			Extractinator(player, ref context);
			LockChest(player);
		}
	}
}
