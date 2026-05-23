using System;
using CreateWandPatch.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ObjectData;

namespace CreateWandPatch.Gameplay
{
	/// <summary>由完整 Tile 快照提取联机安全的 TileSort 分类（qotstruct/cwmap 降级路径）。</summary>
	public static class CreateWandTileBlueprintClassifier
	{
		public static BuildingData FromPreciseTilesAsLegacy(int width, int height, Tile[] tiles)
		{
			var infos = new BuildingData.TileInfo[width * height];
			int total = Math.Min(infos.Length, tiles?.Length ?? 0);
			for (int i = 0; i < total; i++)
			{
				Tile t = tiles[i];
				if (t == null)
				{
					infos[i] = default;
					continue;
				}

				infos[i] = Classify(t.active() ? t.type : -1, t.frameX, t.frameY, t.wall != 0);
			}

			return BuildingData.FromTileInfos(width, height, infos);
		}

		public static BuildingData.TileInfo Classify(int tileType, short frameX, short frameY, bool hasWall)
		{
			var info = new BuildingData.TileInfo { HasWall = hasWall };
			if (tileType < 0 || tileType >= TileID.Count)
				return info;

			if (IsTileBlock(tileType))
			{
				info.Sort = BuildingData.TileSort.Block;
				return info;
			}

			if (IsTilePlatform(tileType))
			{
				info.Sort = BuildingData.TileSort.Platform;
				return info;
			}

			if (IsTileTorch(tileType))
			{
				info.Sort = BuildingData.TileSort.Torch;
				return info;
			}

			TileObjectData objData = GetTileDataSafe(tileType);
			if (objData == null)
				return info;

			int coordX = frameX % objData.CoordinateFullWidth / (objData.CoordinateWidth + objData.CoordinatePadding);
			int coordY = -1;
			int[] heights = objData.CoordinateHeights;
			int frY = frameY % objData.CoordinateFullHeight;
			for (int k = 0; k < heights.Length; k++)
			{
				if (frY == 0)
				{
					coordY = k;
					break;
				}

				frY -= heights[k] + objData.CoordinatePadding;
			}

			if (coordY < 0)
				return info;

			Point16 origin = objData.Origin;
			Point16 coord = new Point16(coordX, coordY);
			switch (tileType)
			{
				case TileID.Candelabras:
				case TileID.Sinks:
					origin = new Point16(origin.X + 1, origin.Y);
					break;
				case TileID.ClosedDoor:
					origin = new Point16(origin.X, origin.Y + 2);
					break;
			}

			if (coord != origin)
				return info;

			BuildingData.TileSort tempsort = BuildingData.TileSort.None;
			for (int k = 2; k < 24; k++)
			{
				Func<int, bool> checker = CheckersForTile[k];
				if (checker != null && checker.Invoke(tileType))
					tempsort = (BuildingData.TileSort)(k + 1);
			}

			info.Sort = tempsort;
			int styleX = frameX / objData.CoordinateFullWidth;
			int styleY = frameY / objData.CoordinateFullHeight;
			info.HasPlaceStyle = tempsort != BuildingData.TileSort.None;
			info.PlaceStyle = styleY;
			info.Flip = styleX % 2 == 1;
			if (tempsort is BuildingData.TileSort.None or BuildingData.TileSort.Bed or BuildingData.TileSort.Bathtub)
			{
				if (IsTileChair(tileType, styleY))
					info.Sort = BuildingData.TileSort.Chair;
				else if (IsTileToilet(tileType, styleY))
					info.Sort = BuildingData.TileSort.Toilet;
			}

			return info;
		}

		private static readonly Func<int, bool>[] CheckersForTile =
		{
			IsTileBlock,
			IsTilePlatform,
			IsTileWorkbench,
			IsTileTable,
			null,
			IsTileDoor,
			IsTileChest,
			IsTileBed,
			IsTileBookcase,
			IsTileBathtub,
			IsTileCandelabra,
			IsTileCandle,
			IsTileChandelier,
			IsTileClock,
			IsTileDresser,
			IsTileLamp,
			IsTileLantern,
			IsTilePiano,
			IsTileSink,
			IsTileBench,
			null,
			IsTileTorch,
			IsTileCampfire,
			null
		};

		private static bool IsTilePlatform(int tileType) =>
			tileType >= TileID.Dirt && TileID.Sets.Platforms != null && tileType < TileID.Sets.Platforms.Length &&
			TileID.Sets.Platforms[tileType];

		private static bool IsTileBlock(int tileType) =>
			tileType >= TileID.Dirt
			&& GetTileDataSafe(tileType) == null
			&& IsSolidTileWithoutSolidTop(tileType);

		private static bool IsTileTorch(int tileType) =>
			tileType >= TileID.Dirt && TileID.Sets.Torches != null && tileType < TileID.Sets.Torches.Length &&
			TileID.Sets.Torches[tileType];

		private static bool IsTileWorkbench(int tileType) => tileType == TileID.WorkBenches;

		private static bool IsTileChair(int tileType, int placeStyle) =>
			tileType == TileID.Chairs && placeStyle is not 1 and not 20;

		private static bool IsTileTable(int tileType) =>
			tileType is TileID.Tables or TileID.Tables2;

		private static bool IsTileDoor(int tileType) => tileType == TileID.ClosedDoor;

		private static bool IsTileBed(int tileType) => tileType == TileID.Beds;

		private static bool IsTileChest(int tileType) =>
			tileType is TileID.Containers or TileID.Containers2;

		private static bool IsTileBookcase(int tileType) => tileType == TileID.Bookcases;

		private static bool IsTileBathtub(int tileType) => tileType == TileID.Bathtubs;

		private static bool IsTileCandelabra(int tileType) => tileType == TileID.Candelabras;

		private static bool IsTileCandle(int tileType) => tileType == TileID.Candles;

		private static bool IsTileChandelier(int tileType) => tileType == TileID.Chandeliers;

		private static bool IsTileClock(int tileType) => tileType == TileID.GrandfatherClocks;

		private static bool IsTileDresser(int tileType) => tileType == TileID.Dressers;

		private static bool IsTileLamp(int tileType) => tileType == TileID.Lamps;

		private static bool IsTileLantern(int tileType) => tileType == TileID.HangingLanterns;

		private static bool IsTilePiano(int tileType) => tileType == TileID.Pianos;

		private static bool IsTileSink(int tileType) => tileType == TileID.Sinks;

		private static bool IsTileBench(int tileType) => tileType == TileID.Benches;

		private static bool IsTileToilet(int tileType, int placeStyle) =>
			tileType == TileID.Toilets || (tileType == TileID.Chairs && placeStyle is 1 or 20);

		private static bool IsTileCampfire(int tileType) => tileType == TileID.Campfire;

		private static TileObjectData GetTileDataSafe(int tileType)
		{
			try
			{
				return TileObjectData.GetTileData(tileType, 0);
			}
			catch
			{
				return null;
			}
		}

		private static bool IsSolidTileWithoutSolidTop(int tileType)
		{
			try
			{
				bool[] solids = Main.tileSolid;
				bool[] solidTop = Main.tileSolidTop;
				if (solids == null || solidTop == null)
					return false;
				if (tileType < 0 || tileType >= solids.Length || tileType >= solidTop.Length)
					return false;
				return solids[tileType] && !solidTop[tileType];
			}
			catch
			{
				return false;
			}
		}
	}
}
