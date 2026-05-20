using System;
using System.IO;
using CreateWandPatch.Gameplay;
using Terraria;

namespace CreateWandPatch.Content
{
	/// <summary>
	/// 与 PNG 同路径的 <c>.cwmap</c>：文件内保存完整 <see cref="Tile"/> 快照。
	/// 读取时保留完整 <see cref="Terraria.Tile"/> 快照；联机安全降级由语义层统一决策。
	/// </summary>
	public static class CreateWandPreciseTileMap
	{
		private static readonly byte[] Magic = { (byte)'C', (byte)'W', (byte)'M', (byte)'A', (byte)'P', (byte)'1' };
		private const int TileBlobSize = 14;

		public static void Save(string path, int width, int height, Tile[] tiles)
		{
			if (tiles == null || tiles.Length != width * height)
				throw new ArgumentException("tiles");

			using (var fs = File.Create(path))
			using (var bw = new BinaryWriter(fs))
			{
				bw.Write(Magic);
				bw.Write(1);
				bw.Write(width);
				bw.Write(height);
				for (int i = 0; i < tiles.Length; i++)
					WriteTile(bw, tiles[i] ?? new Tile());
			}
		}

		public static bool TryLoad(string path, out BuildingData data)
		{
			data = null;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return false;

			try
			{
				using (var fs = File.OpenRead(path))
				using (var br = new BinaryReader(fs))
				{
					for (int m = 0; m < Magic.Length; m++)
					{
						if (br.BaseStream.Position >= br.BaseStream.Length || br.ReadByte() != Magic[m])
							return false;
					}

					int version = br.ReadInt32();
					if (version != 1)
						return false;
					int width = br.ReadInt32();
					int height = br.ReadInt32();
					if (width <= 0 || height <= 0 || width * height > BuildingData.MaxBlueprintCells)
						return false;
					long expectedLen = Magic.Length + 4 + 4 + 4 + (long)width * height * TileBlobSize;
					if (br.BaseStream.Length < expectedLen)
						return false;

					var tiles = new Tile[width * height];
					for (int i = 0; i < tiles.Length; i++)
						tiles[i] = ReadTile(br);

					data = BuildingData.FromPreciseTileGrid(width, height, tiles);
					return true;
				}
			}
			catch
			{
				data = null;
				return false;
			}
		}

		private static void WriteTile(BinaryWriter bw, Tile t)
		{
			bw.Write(t.type);
			bw.Write(t.wall);
			bw.Write(t.liquid);
			bw.Write(t.sTileHeader);
			bw.Write(t.bTileHeader);
			bw.Write(t.bTileHeader2);
			bw.Write(t.bTileHeader3);
			bw.Write(t.frameX);
			bw.Write(t.frameY);
		}

		private static Tile ReadTile(BinaryReader br)
		{
			var t = new Tile
			{
				type = br.ReadUInt16(),
				wall = br.ReadUInt16(),
				liquid = br.ReadByte(),
				sTileHeader = br.ReadUInt16(),
				bTileHeader = br.ReadByte(),
				bTileHeader2 = br.ReadByte(),
				bTileHeader3 = br.ReadByte(),
				frameX = br.ReadInt16(),
				frameY = br.ReadInt16()
			};
			return t;
		}
	}
}
