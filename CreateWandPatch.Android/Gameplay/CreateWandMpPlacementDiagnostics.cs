using System;
using CreateWandPatch.Content;
using Terraria;
using Terraria.DataStructures;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 联机排查：在 <see cref="CreateWandSinglePlayerService.TryAuthoritativePlace"/> 通过魔杖距离门控后，
	/// 记录玩家与铺设足迹、世界出生点的切比雪夫距离，以及每格是否仍在原版 <see cref="Player.IsInTileInteractionRange"/> 内，
	/// 便于先排除「魔杖入口挡距离」与「离出生点过近」两类假设（出生保护仍以服为准）。
	/// </summary>
	internal static class CreateWandMpPlacementDiagnostics
	{
		/// <summary>
		/// 针对单格坐标输出统一诊断口径：玩家-目标格距离、目标格-出生点距离、是否在原版交互范围内。
		/// </summary>
		public static string DescribeCellContext(Player player, int x, int y)
		{
			if (player == null)
				return "cell=(" + x + "," + y + ") player=null";

			int ptx = (int)(player.Center.X / 16f);
			int pty = (int)(player.Center.Y / 16f);
			int chebPlayerToCell = Cheb(ptx, pty, x, y);
			int sx = Main.spawnTileX;
			int sy = Main.spawnTileY;
			int chebCellToSpawn = Cheb(x, y, sx, sy);
			Item held = player.inventory[player.selectedItem];
			int tileBoost = held != null ? held.tileBoost + player.blockRange : player.blockRange;
			bool inVanillaInteractionRange = player.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, tileBoost);
			return "cell=(" + x + "," + y + ") playerTile=(" + ptx + "," + pty + ") spawnWorld=(" + sx + "," + sy + ")" +
			       " chebPlayerToCell=" + chebPlayerToCell + " chebCellToSpawn=" + chebCellToSpawn +
			       " inVanillaInteractionRange=" + inVanillaInteractionRange;
		}

		public static void LogAfterWandReachGate(Terraria.Player player, short originX, short originY, short reachX,
			short reachY, byte kind, byte preset, int datamapIndex)
		{
			if (Main.netMode != 1 || !CreateWandSelectionState.EnableMpActionTrace || player == null)
				return;

			int ptx = (int)(player.Center.X / 16f);
			int pty = (int)(player.Center.Y / 16f);
			int chebPlayerToOrigin = Cheb(ptx, pty, originX, originY);

			int maxChebPlayerToAnyCell;
			bool allVanillaReach;
			string blueprintReachProbe = "";
			Item wand = player.inventory[player.selectedItem];
			int tileBoost = wand != null ? wand.tileBoost + player.blockRange : player.blockRange;

			if (kind == 0)
			{
				maxChebPlayerToAnyCell = MaxChebPlayerToPresetFootprint(ptx, pty, preset, originX, originY);
				allVanillaReach = PresetAllCellsInVanillaReach(player, preset, originX, originY, tileBoost);
			}
			else if (kind == 1 && TryGetBlueprintBounds(datamapIndex, originX, originY, out int bx0, out int by0, out int bw,
				         out int bh))
			{
				maxChebPlayerToAnyCell = MaxChebPlayerToRectCorners(ptx, pty, bx0, by0, bw, bh);
				allVanillaReach = BlueprintAllCellsInVanillaReach(player, bx0, by0, bw, bh, tileBoost);
				blueprintReachProbe = bw * bh > 400 ? "cornersApprox(>400cells)" : "fullGrid";
			}
			else
			{
				maxChebPlayerToAnyCell = chebPlayerToOrigin;
				allVanillaReach = player.IsInTileInteractionRange(originX, originY, TileReachCheckSettings.Simple, tileBoost);
			}

			int sx = Main.spawnTileX;
			int sy = Main.spawnTileY;
			int chebOriginToSpawn = Cheb(originX, originY, sx, sy);

			bool unlimitedGate = !CreateWandSelectionState.UseStaggeredPlacement ||
			                     (CreateWandSelectionState.UseStaggeredPlacement &&
			                      CreateWandSelectionState.MpStaggeredUnlimitedReach);

			CreateWandMpDebugLog.Write(
				"diag excludeDistanceSpawn: playerTile=(" + ptx + "," + pty + ") origin=(" + originX + "," + originY + ") reach=(" +
				reachX + "," + reachY + ") chebPlayerToOrigin=" + chebPlayerToOrigin + " maxChebPlayerToAnyCell=" +
				maxChebPlayerToAnyCell + " spawnWorld=(" + sx + "," + sy + ") chebOriginToSpawn=" + chebOriginToSpawn +
				" wandUnlimitedReachGate=" + unlimitedGate + " allPresetCellsVanillaInteractionRange=" + allVanillaReach +
				(kind == 1 && !string.IsNullOrEmpty(blueprintReachProbe) ? " blueprintVanillaReachProbe=" + blueprintReachProbe : "") +
				" noBuilding=" + player.noBuilding +
				" note=maxCheb 小→不太可能只因「太远」被拒；chebOriginToSpawn 大→不太像经典出生点附近保护带（仍以服为准）");
		}

		private static bool TryGetBlueprintBounds(int datamapIndex, short originX, short originY, out int bx0, out int by0,
			out int bw, out int bh)
		{
			bx0 = by0 = bw = bh = 0;
			CreateWandPngLibrary.EnsureReload();
			if (datamapIndex < 0 || datamapIndex >= CreateWandPngLibrary.Entries.Count)
				return false;
			var entry = CreateWandPngLibrary.Entries[datamapIndex];
			var data = entry.Data;
			if (data == null || data.Width <= 0 || data.Height <= 0)
				return false;
			bx0 = originX;
			by0 = originY;
			bw = data.Width;
			bh = data.Height;
			return true;
		}

		private static int Cheb(int ax, int ay, int bx, int by) =>
			Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

		private static int MaxChebPlayerToPresetFootprint(int ptx, int pty, byte preset, int cx, int cy)
		{
			int max = 0;
			switch ((PlacePreset)preset)
			{
				case PlacePreset.Stone3x3:
					for (int i = -1; i <= 1; i++)
					for (int j = -1; j <= 1; j++)
						max = Math.Max(max, Cheb(ptx, pty, cx + i, cy + j));
					break;
				case PlacePreset.WoodPlatform5:
					for (int k = -2; k <= 2; k++)
						max = Math.Max(max, Cheb(ptx, pty, cx + k, cy));
					break;
				case PlacePreset.Dirt2x2:
					for (int l = 0; l < 2; l++)
					for (int m = 0; m < 2; m++)
						max = Math.Max(max, Cheb(ptx, pty, cx + l, cy + m));
					break;
				default:
					max = Cheb(ptx, pty, cx, cy);
					break;
			}

			return max;
		}

		private static int MaxChebPlayerToRectCorners(int ptx, int pty, int ox, int oy, int w, int h)
		{
			int x1 = ox + w - 1;
			int y1 = oy + h - 1;
			int max = 0;
			max = Math.Max(max, Cheb(ptx, pty, ox, oy));
			max = Math.Max(max, Cheb(ptx, pty, x1, oy));
			max = Math.Max(max, Cheb(ptx, pty, ox, y1));
			max = Math.Max(max, Cheb(ptx, pty, x1, y1));
			return max;
		}

		private static bool PresetAllCellsInVanillaReach(Player player, byte preset, int cx, int cy, int tileBoost)
		{
			switch ((PlacePreset)preset)
			{
				case PlacePreset.Stone3x3:
					for (int i = -1; i <= 1; i++)
					for (int j = -1; j <= 1; j++)
					{
						if (!player.IsInTileInteractionRange(cx + i, cy + j, TileReachCheckSettings.Simple, tileBoost))
							return false;
					}

					return true;
				case PlacePreset.WoodPlatform5:
					for (int k = -2; k <= 2; k++)
					{
						if (!player.IsInTileInteractionRange(cx + k, cy, TileReachCheckSettings.Simple, tileBoost))
							return false;
					}

					return true;
				case PlacePreset.Dirt2x2:
					for (int l = 0; l < 2; l++)
					for (int m = 0; m < 2; m++)
					{
						if (!player.IsInTileInteractionRange(cx + l, cy + m, TileReachCheckSettings.Simple, tileBoost))
							return false;
					}

					return true;
				default:
					return player.IsInTileInteractionRange(cx, cy, TileReachCheckSettings.Simple, tileBoost);
			}
		}

		private static bool BlueprintAllCellsInVanillaReach(Player player, int ox, int oy, int w, int h, int tileBoost)
		{
			const int maxFullProbe = 400;
			if (w * h > maxFullProbe)
			{
				int x1 = ox + w - 1;
				int y1 = oy + h - 1;
				if (!player.IsInTileInteractionRange(ox, oy, TileReachCheckSettings.Simple, tileBoost))
					return false;
				if (!player.IsInTileInteractionRange(x1, oy, TileReachCheckSettings.Simple, tileBoost))
					return false;
				if (!player.IsInTileInteractionRange(ox, y1, TileReachCheckSettings.Simple, tileBoost))
					return false;
				if (!player.IsInTileInteractionRange(x1, y1, TileReachCheckSettings.Simple, tileBoost))
					return false;
				int mx = ox + w / 2;
				int my = oy + h / 2;
				return player.IsInTileInteractionRange(mx, my, TileReachCheckSettings.Simple, tileBoost);
			}

			for (int j = 0; j < h; j++)
			for (int i = 0; i < w; i++)
			{
				if (!player.IsInTileInteractionRange(ox + i, oy + j, TileReachCheckSettings.Simple, tileBoost))
					return false;
			}

			return true;
		}
	}
}
