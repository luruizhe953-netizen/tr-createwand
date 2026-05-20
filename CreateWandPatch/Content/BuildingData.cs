using System;

using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Microsoft.Xna.Framework.Graphics;

using Terraria;



namespace CreateWandPatch.Content

{

	/// <summary>PNG datamap（遗留 TileSort 色图）或完整 <see cref="Tile"/> 快照（材料 1:1）。</summary>

	public sealed class BuildingData

	{

		/// <summary>框选导出 / 库加载与 ImproveGame 共用上限。</summary>

		public const int MaxBlueprintCells = 50000;



		public int Width { get; }

		public int Height { get; }

		private readonly TileInfo[] _tileInfos;

		/// <summary>非 null 时放置使用 <see cref="Terraria.Tile.CopyFrom"/>；null 时使用 <see cref="TileSort"/> 映射。</summary>

		private readonly Tile[] _preciseTiles;



		public IReadOnlyList<TileInfo> TileInfos => _tileInfos;



		/// <summary> true = 旧版色图 + 分类放置；false = 按格子写入完整 Tile。</summary>

		public bool UsesLegacyTileSort => _preciseTiles == null;



		private BuildingData(int width, int height, TileInfo[] infos, Tile[] preciseTiles)

		{

			Width = width;

			Height = height;

			_tileInfos = infos;

			_preciseTiles = preciseTiles;

		}



		public static BuildingData FromDataMap(Texture2D dataMap)

		{

			int w = dataMap.Width;

			int h = dataMap.Height;

			var colors = new Color[w * h];

			dataMap.GetData(colors);

			return FromColorGrid(w, h, colors);

		}



		public static BuildingData FromColorGrid(int width, int height, Color[] colors)

		{

			int n = width * height;

			var infos = new TileInfo[n];

			for (int i = 0; i < n; i++)

				infos[i] = TileInfo.FromColor(colors[i]);

			return new BuildingData(width, height, infos, null);

		}



		/// <summary>仅遗留路径：由外部填充 <see cref="TileInfo"/>（色图 / 预设）。</summary>

		public static BuildingData FromTileInfos(int width, int height, TileInfo[] infos)

		{

			if (infos == null || infos.Length != width * height)

				throw new ArgumentException("infos length must equal width * height.");

			return new BuildingData(width, height, infos, null);

		}



		/// <summary>世界采样 / qotstruct / .cwmap：每格一份独立 <see cref="Tile"/> 克隆。</summary>

		public static BuildingData FromPreciseTileGrid(int width, int height, Tile[] tiles)

		{

			if (tiles == null || tiles.Length != width * height)

				throw new ArgumentException("tiles length must equal width * height.");

			var infos = new TileInfo[width * height];

			for (int i = 0; i < tiles.Length; i++)

				infos[i] = TileInfo.FromPreviewTile(tiles[i]);

			return new BuildingData(width, height, infos, tiles);

		}



		internal Tile GetPreciseTileOrNull(int index) =>

			_preciseTiles != null && (uint)index < (uint)_preciseTiles.Length ? _preciseTiles[index] : null;

		internal Tile[] ClonePreciseTilesOrNull()
		{
			if (_preciseTiles == null)
				return null;

			var copy = new Tile[_preciseTiles.Length];
			for (int i = 0; i < _preciseTiles.Length; i++)
			{
				Tile src = _preciseTiles[i];
				if (src != null)
					copy[i] = (Tile)src.Clone();
			}

			return copy;
		}

		/// <summary>精确蓝图是否至少有一格非 null 需要写入；全空画布为 false（避免联机清空+整图发包闪退）。</summary>
		public bool HasAnyPreciseCellToPlace()
		{
			if (_preciseTiles == null)
				return true;
			for (int i = 0; i < _preciseTiles.Length; i++)
			{
				if (_preciseTiles[i] != null)
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

			/// <summary>精确瓦片分类得到的家具 style；色图蓝图为 false 时用模板物品默认 style。</summary>
			public bool HasPlaceStyle { get; set; }

			public int PlaceStyle { get; set; }



			public bool UsePackedPreview { get; set; }

			public ushort PackedTileType { get; set; }

			public ushort PackedWallType { get; set; }



			public static TileInfo FromColor(Color color)

			{

				int index = (color.R + 1) / 64 * 5 + (color.G + 1) / 64;

				if (index > 23 || index < 0)

					index = 0;

				return new TileInfo

				{

					Sort = (TileSort)index,

					HasWall = color.B > 127,

					Flip = color.A < 128,

					UsePackedPreview = false

				};

			}



			public static TileInfo FromPreviewTile(Tile t)

			{

				if (t == null)

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



			public readonly Color ToColor()

			{

				if (UsePackedPreview)

				{

					return new Color(

						PackedTileType & 255,

						(PackedTileType >> 8) & 255,

						PackedWallType & 255,

						(PackedWallType >> 8) & 255);

				}



				int index = (int)Sort;

				return new Color(

					index < 5 ? 0 : index / 5 * 64 - 1,

					index == 0 ? 0 : index % 5 * 64 - 1,

					HasWall ? 255 : 0,

					Flip ? 127 : 255);

			}

		}

	}

}


