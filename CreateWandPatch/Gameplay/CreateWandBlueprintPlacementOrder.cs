using System;
using System.Collections.Generic;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>大蓝图铺设顺序：自下而上；含液体的格延后到液体阶段。</summary>
	internal static class CreateWandBlueprintPlacementOrder
	{
		public static bool PreciseTileHasLiquid(Tile tile) => tile != null && tile.liquid > 0;

		/// <summary>锤子斜面/半砖（sTileHeader 内 slope、halfBrick）。</summary>
		public static bool PreciseTileHasHammerShape(Tile tile) =>
			tile != null && (tile.slope() != 0 || tile.halfBrick());

		public static bool PreciseTileNeedsMainPass(Tile tile)
		{
			if (tile == null)
				return false;
			if (tile.wall != 0)
				return true;
			if (tile.active())
				return true;
			return PreciseTileHasHammerShape(tile) || PreciseTileHasWiringOrPaint(tile);
		}

		/// <summary>无墙无 active 物块时仍可能有电路/染色/制动器（须单独写入）。</summary>
		public static bool PreciseTileHasWiringOrPaint(Tile tile)
		{
			if (tile == null)
				return false;
			return tile.wire() || tile.wire2() || tile.wire3() || tile.wire4() || tile.inActive() || tile.actuator()
			       || tile.color() > 0 || tile.wallColor() > 0;
		}

		/// <summary>须走手持链（电路/锤子/喷漆），在主阶段砖墙铺完后处理。</summary>
		public static bool PreciseTileNeedsHandheldExtras(Tile tile) =>
			tile != null && (PreciseTileHasHammerShape(tile) || PreciseTileHasWiringOrPaint(tile));

		/// <summary>手持 extras 与主阶段相同：自下而上，避免敲斜面时相邻格未就绪。</summary>
		public static void BuildHandheldExtraIndicesBottomUp(BuildingData data, out int[] indices)
		{
			int w = data.Width;
			int h = data.Height;
			var list = new List<int>();
			for (int rowFromBottom = 0; rowFromBottom < h; rowFromBottom++)
			{
				int y = h - 1 - rowFromBottom;
				for (int x = 0; x < w; x++)
				{
					int i = x + y * w;
					Tile src = data.GetPreciseTileOrNull(i);
					if (src != null && PreciseTileNeedsHandheldExtras(src))
						list.Add(i);
				}
			}

			indices = list.ToArray();
		}

		/// <summary>铺砖后须从蓝图还原、且不宜调用 SquareTileFrame(true) 以免冲掉锤子/染色。</summary>
		public static bool PreciseTileNeedsVisualRestore(Tile tile) => PreciseTileNeedsHandheldExtras(tile);

		public static bool LegacyTileNeedsMainPass(BuildingData.TileInfo info) =>
			info.HasWall || info.Sort != BuildingData.TileSort.None;

		/// <summary>行优先、自最底行向顶行（Terraria Y 向下增大）。</summary>
		public static int LinearIndexBottomUp(int step, int width, int height)
		{
			int rowFromBottom = step / width;
			int col = step % width;
			int y = height - 1 - rowFromBottom;
			return col + y * width;
		}

		public static void BuildPrecisePassIndices(BuildingData data, out int[] mainIndices, out int[] liquidIndices)
		{
			int w = data.Width;
			int h = data.Height;
			var main = new List<int>();
			var liquid = new List<int>();
			for (int rowFromBottom = 0; rowFromBottom < h; rowFromBottom++)
			{
				int y = h - 1 - rowFromBottom;
				for (int x = 0; x < w; x++)
				{
					int i = x + y * w;
					Tile tile = data.GetPreciseTileOrNull(i);
					if (tile == null)
						continue;
					if (PreciseTileHasLiquid(tile))
						liquid.Add(i);
					if (PreciseTileNeedsMainPass(tile))
						main.Add(i);
				}
			}

			mainIndices = main.ToArray();
			liquidIndices = liquid.ToArray();
		}

		public static void BuildLegacyPassIndices(BuildingData data, out int[] mainIndices)
		{
			int w = data.Width;
			int h = data.Height;
			var main = new List<int>();
			for (int rowFromBottom = 0; rowFromBottom < h; rowFromBottom++)
			{
				int y = h - 1 - rowFromBottom;
				for (int x = 0; x < w; x++)
				{
					int i = x + y * w;
					if (LegacyTileNeedsMainPass(data.TileInfos[i]))
						main.Add(i);
				}
			}

			mainIndices = main.ToArray();
		}

		/// <summary>矩形内全部格子，自下而上（用于联机逐格清空）。</summary>
		public static int[] BuildAllCellsBottomUp(int width, int height)
		{
			int n = width * height;
			var all = new int[n];
			int k = 0;
			for (int rowFromBottom = 0; rowFromBottom < height; rowFromBottom++)
			{
				int y = height - 1 - rowFromBottom;
				for (int x = 0; x < width; x++)
					all[k++] = x + y * width;
			}

			return all;
		}
	}
}
