using System;
using System.Collections.Generic;
using System.IO;
using CreateWandPatch.Content;
using CreateWandPatch.Infrastructure.TmlTagIo;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 将 ImproveGame <c>.qotstruct</c>（tModLoader TagIO）转为补丁用 <see cref="BuildingData"/>；
	/// <see cref="BuildingData"/> 遗留分类一份 + <see cref="BuildingData.FromPreciseTileGrid"/> 一份供 1:1（与 cwmap 同款精确门）。
	/// </summary>
	public static class CreateWandQotStructConverter
	{
		public static string LastError { get; private set; }

		public static bool TryFromFile(string path, out BuildingData legacyData, out BuildingData preciseData)
		{
			legacyData = null;
			preciseData = null;
			LastError = null;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				LastError = "file missing";
				return false;
			}

			try
			{
				TmlCompound root = TryReadTagRoot(path);
				bool ok = TryFromRoot(root, out legacyData, out preciseData);
				if (!ok)
					LastError = "no compatible structure payload";
				return ok;
			}
			catch (Exception ex)
			{
				LastError = ex.ToString();
				legacyData = null;
				preciseData = null;
				return false;
			}
		}

		private static TmlCompound TryReadTagRoot(string path)
		{
			try
			{
				return TmlTagReader.ReadRootFromFile(path, gzip: true);
			}
			catch (Exception gzipEx)
			{
				try
				{
					return TmlTagReader.ReadRootFromFile(path, gzip: false);
				}
				catch (Exception rawEx)
				{
					throw new InvalidDataException("gzip/raw parse failed: gzip=" + gzipEx.Message + "; raw=" + rawEx.Message);
				}
			}
		}

		private static bool TryFromRoot(TmlCompound root, out BuildingData legacyData, out BuildingData preciseData)
		{
			legacyData = null;
			preciseData = null;
			if (root == null)
				return false;

			if (!TryResolveStructure(root, out int w, out int h, out List<object> list))
				return false;

			var tiles = new Tile[w * h];
			for (int n = 0; n < list.Count; n++)
			{
				int dst = ReorderIndex(n, w, h);
				if (TmlCompound.AsCompound(list[n]) is not TmlCompound def)
				{
					tiles[dst] = new Tile();
					continue;
				}

				byte extra = GetByte(def, "ExtraDatas", "ExtraData", "Extra");
				bool vanillaTile = (extra & 1) == 0;
				bool vanillaWall = (extra & 2) == 0;

				int wall = vanillaWall ? GetInt(def, -1, "WallIndex", "WallType", "Wall") : -1;
				int tile = -1;
				if (vanillaTile)
					tile = GetInt(def, -1, "TileIndex", "TileType", "Tile");

				short fx = (short)GetInt(def, 0, "TileFrameX", "FrameX");
				short fy = (short)GetInt(def, 0, "TileFrameY", "FrameY");

				var t = new Tile();
				if (wall >= 0)
					t.wall = (ushort)wall;

				if (tile >= 0)
				{
					t.active(true);
					t.type = (ushort)tile;
					t.frameX = fx;
					t.frameY = fy;
				}

				tiles[dst] = t;
			}

			legacyData = CreateWandTileBlueprintClassifier.FromPreciseTilesAsLegacy(w, h, tiles);
			preciseData = BuildingData.FromPreciseTileGrid(w, h, tiles);
			return true;
		}

		private static bool TryResolveStructure(TmlCompound root, out int width, out int height, out List<object> list)
		{
			width = 0;
			height = 0;
			list = null;

			if (TryResolveStructureFromCompound(root, out width, out height, out list))
				return true;

			var queue = new Queue<TmlCompound>();
			queue.Enqueue(root);
			while (queue.Count > 0)
			{
				TmlCompound cur = queue.Dequeue();
				foreach (object value in cur.Values)
				{
					if (value is TmlCompound child)
					{
						if (TryResolveStructureFromCompound(child, out width, out height, out list))
							return true;
						queue.Enqueue(child);
					}
					else if (value is List<object> l)
					{
						for (int i = 0; i < l.Count; i++)
						{
							if (l[i] is TmlCompound lc)
								queue.Enqueue(lc);
						}
					}
				}
			}

			return false;
		}

		private static bool TryResolveStructureFromCompound(TmlCompound comp, out int width, out int height, out List<object> list)
		{
			width = 0;
			height = 0;
			list = null;
			if (comp == null)
				return false;

			foreach (var key in new[] { "StructureData", "structureData", "Tiles", "TileData", "Data" })
			{
				if (!comp.TryGetList(key, out List<object> candidate) || candidate == null || candidate.Count == 0)
					continue;
				if (!TryResolveDimensions(comp, candidate.Count, out width, out height))
					continue;
				list = candidate;
				return true;
			}

			return false;
		}

		private static bool TryResolveDimensions(TmlCompound comp, int listCount, out int width, out int height)
		{
			width = 0;
			height = 0;
			int rw = GetInt(comp, int.MinValue, "Width", "width", "W");
			int rh = GetInt(comp, int.MinValue, "Height", "height", "H");
			if (rw == int.MinValue || rh == int.MinValue)
				return false;

			if (TryAssignDimensions(rw, rh, listCount, out width, out height))
				return true;
			if (TryAssignDimensions(rw + 1, rh + 1, listCount, out width, out height))
				return true;

			return false;
		}

		private static bool TryAssignDimensions(int w, int h, int listCount, out int width, out int height)
		{
			width = 0;
			height = 0;
			if (w <= 0 || h <= 0)
				return false;
			if (w * h != listCount)
				return false;
			if (w * h > CreateWandWorldToPngExport.MaxCells)
				return false;
			width = w;
			height = h;
			return true;
		}

		private static int GetInt(TmlCompound comp, int defaultValue, params string[] keys)
		{
			if (comp == null || keys == null)
				return defaultValue;
			for (int i = 0; i < keys.Length; i++)
			{
				int v = comp.GetInt(keys[i], int.MinValue);
				if (v != int.MinValue)
					return v;
			}

			return defaultValue;
		}

		private static byte GetByte(TmlCompound comp, params string[] keys)
		{
			int v = GetInt(comp, 0, keys);
			return v < 0 ? (byte)0 : (byte)v;
		}

		/// <summary>与 ImproveGame <c>infos[n % height * width + n / height]</c> 一致。</summary>
		private static int ReorderIndex(int n, int width, int height) => n % height * width + n / height;
	}
}
