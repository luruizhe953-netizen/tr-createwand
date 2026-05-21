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

		public static bool PreciseTileNeedsMainPass(Tile tile)
		{
			if (tile == null)
				return false;
			if (tile.wall != 0)
				return true;
			return tile.active();
		}

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
