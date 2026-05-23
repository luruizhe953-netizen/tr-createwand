using System;
using System.Collections.Generic;
using UnityEngine;
using Terraria;

namespace CreateWandPatch.Content
{
    /// <summary>
    /// PNG datamap (legacy TileSort color map) or full Tile snapshot (material 1:1).
    /// Android IL2CPP adaptation: Tile is a struct (value type), uses Color32 instead of XNA Color.
    /// </summary>
    public sealed class BuildingData
    {
        public const int MaxBlueprintCells = 50000;

        public int Width { get; }
        public int Height { get; }
        private readonly TileInfo[] _tileInfos;

        /// <summary>When non-null, placement uses precise tile data. When null, uses TileSort classification.</summary>
        private readonly Tile[] _preciseTiles;
        /// <summary>In Android IL2CPP, Tile is a struct. Tracks which indices have valid precise tiles.</summary>
        private readonly bool[] _preciseTileValid;

        public IReadOnlyList<TileInfo> TileInfos => _tileInfos;

        /// <summary>true = legacy color map + classification placement; false = write complete Tile per cell.</summary>
        public bool UsesLegacyTileSort => _preciseTileValid == null;

        private BuildingData(int width, int height, TileInfo[] infos, Tile[] preciseTiles, bool[] preciseTileValid = null)
        {
            Width = width;
            Height = height;
            _tileInfos = infos;
            _preciseTiles = preciseTiles;
            _preciseTileValid = preciseTileValid;
        }

        /// <summary>
        /// Load from raw RGBA32 pixel data (e.g., from PNG decoded by external loader).
        /// Assumes pixels in row-major order, matching Unity Texture2D layout.
        /// </summary>
        public static BuildingData FromRawPixels(int width, int height, byte[] rgbaPixels)
        {
            int n = width * height;
            var colors = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                int off = i * 4;
                colors[i] = new Color32(rgbaPixels[off], rgbaPixels[off + 1], rgbaPixels[off + 2], rgbaPixels[off + 3]);
            }
            return FromColorGrid(width, height, colors);
        }

        public static BuildingData FromColorGrid(int width, int height, Color32[] colors)
        {
            int n = width * height;
            var infos = new TileInfo[n];
            for (int i = 0; i < n; i++)
                infos[i] = TileInfo.FromColor(colors[i]);
            return new BuildingData(width, height, infos, null, null);
        }

        /// <summary>Legacy path: externally filled TileInfo array (color map / preset).</summary>
        public static BuildingData FromTileInfos(int width, int height, TileInfo[] infos)
        {
            if (infos == null || infos.Length != width * height)
                throw new ArgumentException("infos length must equal width * height.");
            return new BuildingData(width, height, infos, null, null);
        }

        /// <summary>World sample / qotstruct / .cwmap: one independent Tile per cell.</summary>
        public static BuildingData FromPreciseTileGrid(int width, int height, Tile[] tiles)
        {
            if (tiles == null || tiles.Length != width * height)
                throw new ArgumentException("tiles length must equal width * height.");

            var infos = new TileInfo[width * height];
            var valid = new bool[width * height];
            for (int i = 0; i < tiles.Length; i++)
            {
                infos[i] = TileInfo.FromPreviewTile(tiles[i]);
                valid[i] = tiles[i].IsLoaded && (tiles[i].type != 0 || tiles[i].wall != 0);
            }

            return new BuildingData(width, height, infos, tiles, valid);
        }

        /// <summary>
        /// Android: Tile is a struct, cannot return null. Use a sentinel or check _preciseTileValid.
        /// Returns default(Tile) if index is out of range or no precise tile exists.
        /// </summary>
        internal Tile GetPreciseTileOrDefault(int index) =>
            _preciseTileValid != null && (uint)index < (uint)_preciseTileValid.Length && _preciseTileValid[index]
                ? _preciseTiles[index] : default;

        internal bool HasPreciseTile(int index) =>
            _preciseTileValid != null && (uint)index < (uint)_preciseTileValid.Length && _preciseTileValid[index];

        /// <summary>
        /// Clone the precise tiles array. In Android IL2CPP, Tile is a struct (value type),
        /// so array copy creates independent copies.
        /// </summary>
        internal Tile[] ClonePreciseTilesOrNull()
        {
            if (_preciseTiles == null)
                return null;

            var copy = new Tile[_preciseTiles.Length];
            System.Array.Copy(_preciseTiles, copy, _preciseTiles.Length);
            return copy;
        }

        /// <summary>Check if any precise cell needs placing (not all empty).</summary>
        public bool HasAnyPreciseCellToPlace()
        {
            if (_preciseTileValid == null)
                return true;
            for (int i = 0; i < _preciseTileValid.Length; i++)
            {
                if (_preciseTileValid[i])
                    return true;
            }
            return false;
        }

        public enum TileSort : byte
        {
            None,
            Block,
            Platform,
            Workbench,
            Table,
            Chair,
            Door,
            Chest,
            Bed,
            Bookcase,
            Bathtub,
            Candelabra,
            Candle,
            Chandelier,
            Clock,
            Dresser,
            Lamp,
            Lantern,
            Piano,
            Sink,
            Sofa,
            Toilet,
            Torch,
            Campfire
        }

        public struct TileInfo
        {
            public TileSort Sort { get; set; }
            public bool HasWall { get; set; }
            public bool Flip { get; set; }
            /// <summary>When true, PlaceStyle contains the specific furniture style.</summary>
            public bool HasPlaceStyle { get; set; }
            public int PlaceStyle { get; set; }

            public bool UsePackedPreview { get; set; }
            public ushort PackedTileType { get; set; }
            public ushort PackedWallType { get; set; }

            public static TileInfo FromColor(Color32 color)
            {
                int index = (color.r + 1) / 64 * 5 + (color.g + 1) / 64;
                if (index > 23 || index < 0)
                    index = 0;
                return new TileInfo
                {
                    Sort = (TileSort)index,
                    HasWall = color.b > 127,
                    Flip = color.a < 128,
                    UsePackedPreview = false
                };
            }

            public static TileInfo FromPreviewTile(Tile t)
            {
                // In Android IL2CPP, Tile is always a struct so no null check needed.
                // Check if tile is loaded/has content
                if (!t.IsLoaded || (t.type == 0 && t.wall == 0))
                    return default;

                return new TileInfo
                {
                    Sort = TileSort.None,
                    HasWall = t.wall != 0,
                    Flip = false,
                    UsePackedPreview = true,
                    PackedTileType = t.type,
                    PackedWallType = t.wall
                };
            }

            public readonly Color32 ToColor()
            {
                if (UsePackedPreview)
                {
                    return new Color32(
                        (byte)(PackedTileType & 255),
                        (byte)((PackedTileType >> 8) & 255),
                        (byte)(PackedWallType & 255),
                        (byte)((PackedWallType >> 8) & 255));
                }

                int index = (int)Sort;
                return new Color32(
                    (byte)(index < 5 ? 0 : index / 5 * 64 - 1),
                    (byte)(index == 0 ? 0 : index % 5 * 64 - 1),
                    (byte)(HasWall ? 255 : 0),
                    (byte)(Flip ? 127 : 255));
            }
        }
    }
}
