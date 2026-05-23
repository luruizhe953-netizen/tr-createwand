using System;
using System.Collections.Generic;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
    /// <summary>Blueprint placement order: bottom-up; liquid cells deferred.</summary>
    internal static class CreateWandBlueprintPlacementOrder
    {
        // IL2CPP Tile struct bit helpers (Android lacks C# Tile instance methods)
        public static bool Active(Tile t) => (t.bTileHeader & 1) != 0;
        public static bool Wire(Tile t) => (t.bTileHeader & 4) != 0;
        public static bool Wire2(Tile t) => (t.bTileHeader & 8) != 0;
        public static bool Wire3(Tile t) => (t.bTileHeader & 16) != 0;
        public static bool Wire4(Tile t) => (t.bTileHeader3 & 2) != 0;
        public static bool Actuator(Tile t) => (t.bTileHeader & 32) != 0;
        public static bool InActive(Tile t) => (t.bTileHeader3 & 1) != 0;
        public static int Slope(Tile t) => t.sTileHeader & 7;
        public static bool HalfBrick(Tile t) => (t.sTileHeader & 16) != 0;
        public static int PaintColor(Tile t) => t.bTileHeader2 & 31;
        public static int WallPaintColor(Tile t) => (t.bTileHeader2 >> 5) & 3;

        public static bool PreciseTileHasLiquid(Tile tile) => tile.liquid > 0;

        public static bool PreciseTileHasHammerShape(Tile tile) =>
            Slope(tile) != 0 || HalfBrick(tile);

        public static bool PreciseTileNeedsMainPass(Tile tile)
        {
            if (tile.wall != 0) return true;
            if (Active(tile)) return true;
            return PreciseTileHasHammerShape(tile) || PreciseTileHasWiringOrPaint(tile);
        }

        public static bool PreciseTileHasWiringOrPaint(Tile tile)
        {
            return Wire(tile) || Wire2(tile) || Wire3(tile) || Wire4(tile) ||
                   InActive(tile) || Actuator(tile) || PaintColor(tile) > 0 || WallPaintColor(tile) > 0;
        }

        public static bool PreciseTileNeedsHandheldExtras(Tile tile) =>
            PreciseTileHasHammerShape(tile) || PreciseTileHasWiringOrPaint(tile);

        public static void BuildHandheldExtraIndicesBottomUp(BuildingData data, out int[] indices)
        {
            int w = data.Width, h = data.Height;
            var list = new List<int>();
            for (int rowFromBottom = 0; rowFromBottom < h; rowFromBottom++)
            {
                int y = h - 1 - rowFromBottom;
                for (int x = 0; x < w; x++)
                {
                    int i = x + y * w;
                    if (data.HasPreciseTile(i) && PreciseTileNeedsHandheldExtras(data.GetPreciseTileOrDefault(i)))
                        list.Add(i);
                }
            }
            indices = list.ToArray();
        }

        public static bool PreciseTileNeedsVisualRestore(Tile tile) => PreciseTileNeedsHandheldExtras(tile);

        public static bool LegacyTileNeedsMainPass(BuildingData.TileInfo info) =>
            info.HasWall || info.Sort != BuildingData.TileSort.None;

        public static int LinearIndexBottomUp(int step, int width, int height)
        {
            int rowFromBottom = step / width;
            int col = step % width;
            int y = height - 1 - rowFromBottom;
            return col + y * width;
        }

        public static void BuildPrecisePassIndices(BuildingData data, out int[] mainIndices, out int[] liquidIndices)
        {
            int w = data.Width, h = data.Height;
            var main = new List<int>();
            var liquid = new List<int>();
            for (int rowFromBottom = 0; rowFromBottom < h; rowFromBottom++)
            {
                int y = h - 1 - rowFromBottom;
                for (int x = 0; x < w; x++)
                {
                    int i = x + y * w;
                    if (!data.HasPreciseTile(i)) continue;
                    Tile tile = data.GetPreciseTileOrDefault(i);
                    if (PreciseTileHasLiquid(tile)) liquid.Add(i);
                    if (PreciseTileNeedsMainPass(tile)) main.Add(i);
                }
            }
            mainIndices = main.ToArray();
            liquidIndices = liquid.ToArray();
        }

        public static void BuildLegacyPassIndices(BuildingData data, out int[] mainIndices)
        {
            int w = data.Width, h = data.Height;
            var main = new List<int>();
            for (int rowFromBottom = 0; rowFromBottom < h; rowFromBottom++)
            {
                int y = h - 1 - rowFromBottom;
                for (int x = 0; x < w; x++)
                {
                    int i = x + y * w;
                    if (LegacyTileNeedsMainPass(data.TileInfos[i])) main.Add(i);
                }
            }
            mainIndices = main.ToArray();
        }

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
