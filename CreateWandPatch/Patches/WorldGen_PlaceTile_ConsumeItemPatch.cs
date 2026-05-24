using System;
using CreateWandPatch.Content;
using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// When ConsumePlacementItems is on (C key), each PlaceTile / PlaceWall
	/// by a wand-holding player consumes 1 matching item from inventory.
	/// </summary>
	[HarmonyPatch]
	public static class WorldGen_PlaceTile_ConsumeItemPatch
	{
		[HarmonyPatch(typeof(WorldGen), nameof(WorldGen.PlaceTile),
			new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int), typeof(int) })]
		[HarmonyPostfix]
		private static void Postfix_PlaceTile(int i, int j, int type, bool mute, bool forced, int plr, int style, bool __result)
		{
			if (!__result || !CreateWandSelectionState.ConsumePlacementItems) return;
			if (plr < 0 || plr >= Main.player.Length) return;
			var p = Main.player[plr];
			if (p == null || !p.active) return;
			if (p.inventory[p.selectedItem].type != CreateWandIds.ItemType) return;

			for (int s = 0; s < 58; s++)
			{
				var it = p.inventory[s];
				if (it.IsAir || it.stack <= 0 || it.createTile != type) continue;
				it.stack--;
				if (it.stack <= 0) it.TurnToAir();
				return;
			}
		}

		[HarmonyPatch(typeof(WorldGen), nameof(WorldGen.PlaceWall),
			new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool) })]
		[HarmonyPostfix]
		private static void Postfix_PlaceWall(int i, int j, int type, bool mute, bool __result)
		{
			if (!__result || !CreateWandSelectionState.ConsumePlacementItems) return;
			var p = Main.LocalPlayer;
			if (p == null || !p.active) return;
			if (p.inventory[p.selectedItem].type != CreateWandIds.ItemType) return;

			for (int s = 0; s < 58; s++)
			{
				var it = p.inventory[s];
				if (it.IsAir || it.stack <= 0 || it.createWall != type) continue;
				it.stack--;
				if (it.stack <= 0) it.TurnToAir();
				return;
			}
		}
	}
}
