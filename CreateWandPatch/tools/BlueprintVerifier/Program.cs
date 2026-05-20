using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;

internal static class Program
{
	private static readonly FieldInfo PreciseTilesField =
		typeof(BuildingData).GetField("_preciseTiles", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo TileActiveMethod = ResolveTileMethod("active");
	private static readonly MethodInfo TileSlopeMethod = ResolveTileMethod("slope");
	private static readonly MethodInfo TileHalfBrickMethod = ResolveTileMethod("halfBrick");
	private static readonly MethodInfo TileActuatorMethod = ResolveTileMethod("actuator");
	private static readonly MethodInfo TileWireMethod = ResolveTileMethod("wire");
	private static readonly MethodInfo TileWire2Method = ResolveTileMethod("wire2");
	private static readonly MethodInfo TileWire3Method = ResolveTileMethod("wire3");
	private static readonly MethodInfo TileWire4Method = ResolveTileMethod("wire4");
	private static readonly MethodInfo TileLiquidTypeMethod = ResolveTileMethod("liquidType");
	private static readonly FieldInfo TileTypeField = ResolveTileField("type");
	private static readonly FieldInfo TileWallField = ResolveTileField("wall");
	private static readonly FieldInfo TileLiquidField = ResolveTileField("liquid");
	private static readonly FieldInfo TileFrameXField = ResolveTileField("frameX");
	private static readonly FieldInfo TileFrameYField = ResolveTileField("frameY");

	private static int Main(string[] args)
	{
		InstallAssemblyResolver();

		if (args.Length == 0)
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("  BlueprintVerifier <file1.cwmap|file2.qotstruct|...>");
			return 1;
		}

		int failed = 0;
		foreach (string rawPath in args)
		{
			string path = rawPath.Trim('"');
			if (!Analyze(path))
				failed++;
		}

		return failed == 0 ? 0 : 2;
	}

	private static bool Analyze(string path)
	{
		Console.WriteLine();
		Console.WriteLine("==== " + path + " ====");
		if (!File.Exists(path))
		{
			Console.WriteLine("File not found.");
			return false;
		}

		string ext = Path.GetExtension(path);
		BuildingData data;
		if (ext.Equals(".cwmap", StringComparison.OrdinalIgnoreCase))
		{
			if (!CreateWandPreciseTileMap.TryLoad(path, out data))
			{
				Console.WriteLine("Failed to parse cwmap.");
				return false;
			}
		}
		else if (ext.Equals(".qotstruct", StringComparison.OrdinalIgnoreCase))
		{
			if (!CreateWandQotStructConverter.TryFromFile(path, out data, out _))
			{
				Console.WriteLine("Failed to parse qotstruct: " + (CreateWandQotStructConverter.LastError ?? "unknown"));
				return false;
			}
		}
		else if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
		{
			return AnalyzePngDatamap(path);
		}
		else
		{
			Console.WriteLine("Unsupported extension: " + ext);
			return false;
		}

		Console.WriteLine("Size: " + data.Width + " x " + data.Height + " (cells " + (data.Width * data.Height) + ")");
		Console.WriteLine("UsesLegacyTileSort: " + data.UsesLegacyTileSort);
		if (data.UsesLegacyTileSort)
		{
			AnalyzeLegacyMap(data);
			return true;
		}

		Array preciseTiles = PreciseTilesField?.GetValue(data) as Array;
		if (preciseTiles == null)
		{
			Console.WriteLine("Cannot access precise tile payload.");
			return false;
		}

		int tileCountLimit = ResolveIdCount("Terraria.ID.TileID", "Count");
		int wallCountLimit = ResolveIdCount("Terraria.ID.WallID", "Count");

		int active = 0;
		int withWall = 0;
		int withLiquid = 0;
		int invalidTileId = 0;
		int invalidWallId = 0;
		int invalidLiquidType = 0;
		int inactiveWithSlopeOrHalf = 0;
		int inactiveWithActuator = 0;
		int activeWithFrame = 0;
		int withActuator = 0;
		int withWire = 0;
		var tileCounts = new Dictionary<int, int>();
		var wallCounts = new Dictionary<int, int>();

		foreach (object t in preciseTiles)
		{
			if (t == null)
				continue;

			bool isActive = InvokeBool(TileActiveMethod, t);
			if (isActive)
			{
				active++;
				int tid = GetIntField(TileTypeField, t);
				Increment(tileCounts, tid);
				if (tileCountLimit > 0 && (tid < 0 || tid >= tileCountLimit))
					invalidTileId++;
			}

			int wall = GetIntField(TileWallField, t);
			if (wall != 0)
			{
				withWall++;
				Increment(wallCounts, wall);
				if (wallCountLimit > 0 && (wall < 0 || wall >= wallCountLimit))
					invalidWallId++;
			}

			int liquid = GetIntField(TileLiquidField, t);
			if (liquid > 0)
			{
				withLiquid++;
				byte lt = (byte)GetIntFromMethod(TileLiquidTypeMethod, t);
				if (lt > 2)
					invalidLiquidType++;
			}

			if (InvokeBool(TileActuatorMethod, t))
				withActuator++;
			if (InvokeBool(TileWireMethod, t) || InvokeBool(TileWire2Method, t) || InvokeBool(TileWire3Method, t) ||
			    InvokeBool(TileWire4Method, t))
				withWire++;
			if (isActive && (GetIntField(TileFrameXField, t) != 0 || GetIntField(TileFrameYField, t) != 0))
				activeWithFrame++;

			if (!isActive && (GetIntFromMethod(TileSlopeMethod, t) != 0 || InvokeBool(TileHalfBrickMethod, t)))
				inactiveWithSlopeOrHalf++;
			if (!isActive && InvokeBool(TileActuatorMethod, t))
				inactiveWithActuator++;
		}

		Console.WriteLine("Active tiles: " + active);
		Console.WriteLine("Tiles with wall: " + withWall);
		Console.WriteLine("Tiles with liquid: " + withLiquid);
		Console.WriteLine("Invalid tile ids: " + invalidTileId);
		Console.WriteLine("Invalid wall ids: " + invalidWallId);
		Console.WriteLine("Invalid liquid types: " + invalidLiquidType);
		Console.WriteLine("Inactive+Slope/HalfBrick: " + inactiveWithSlopeOrHalf);
		Console.WriteLine("Inactive+Actuator: " + inactiveWithActuator);
		Console.WriteLine("Active with non-zero frame: " + activeWithFrame);
		Console.WriteLine("Tiles with actuator flag: " + withActuator);
		Console.WriteLine("Tiles with any wire flag: " + withWire);

		Console.WriteLine("Top tile ids:");
		foreach (var kv in tileCounts.OrderByDescending(p => p.Value).Take(12))
			Console.WriteLine("  " + kv.Key + " " + kv.Value);

		Console.WriteLine("Top wall ids:");
		foreach (var kv in wallCounts.OrderByDescending(p => p.Value).Take(12))
			Console.WriteLine("  " + kv.Key + " " + kv.Value);

		bool suspicious = invalidTileId > 0 || invalidWallId > 0 || invalidLiquidType > 0 ||
		                  inactiveWithSlopeOrHalf > 0 || inactiveWithActuator > 0;
		Console.WriteLine("Suspicious payload: " + (suspicious ? "YES" : "NO"));
		int multiplayerDegradeRisk = activeWithFrame + withLiquid + withActuator + withWire;
		Console.WriteLine("MP downgrade risk cells (frame/liquid/actuator/wire): " + multiplayerDegradeRisk);
		return true;
	}

	private static bool AnalyzePngDatamap(string path)
	{
		try
		{
			using (var bitmap = new Bitmap(path))
			{
				int w = bitmap.Width;
				int h = bitmap.Height;
				int n = w * h;
				if (n <= 0 || n > BuildingData.MaxBlueprintCells)
					return false;

				int withWall = 0;
				int blockLike = 0;
				int furnitureLike = 0;
				var sortCounts = new Dictionary<int, int>();
				for (int y = 0; y < h; y++)
				{
					for (int x = 0; x < w; x++)
					{
						Color c = bitmap.GetPixel(x, y);
						int index = ((c.R + 1) / 64) * 5 + ((c.G + 1) / 64);
						if (index < 0 || index > 23)
							index = 0;
						bool hasWall = c.B > 127;
						if (hasWall)
							withWall++;

						if (index is 1 or 2)
							blockLike++;
						else if (index != 0)
							furnitureLike++;

						if (sortCounts.TryGetValue(index, out int cur))
							sortCounts[index] = cur + 1;
						else
							sortCounts[index] = 1;
					}
				}

				Console.WriteLine("Size: " + w + " x " + h + " (cells " + n + ")");
				Console.WriteLine("UsesLegacyTileSort: True (png datamap)");
				Console.WriteLine("Legacy map classification summary:");
				Console.WriteLine("  Cells with wall flag: " + withWall);
				Console.WriteLine("  Block/platform cells: " + blockLike);
				Console.WriteLine("  Furniture sort cells: " + furnitureLike);
				Console.WriteLine("  MP-safe by design: YES (classification placement)");
				Console.WriteLine("Top sorts:");
				foreach (var kv in sortCounts.OrderByDescending(p => p.Value).Take(12))
					Console.WriteLine("  " + ((BuildingData.TileSort)kv.Key) + " " + kv.Value);
				return true;
			}
		}
		catch
		{
			return false;
		}
	}

	private static void AnalyzeLegacyMap(BuildingData data)
	{
		int blockLike = 0;
		int furnitureLike = 0;
		int withWall = 0;
		var sortCounts = new Dictionary<BuildingData.TileSort, int>();
		foreach (var info in data.TileInfos)
		{
			if (info.HasWall)
				withWall++;
			if (info.Sort != BuildingData.TileSort.None)
			{
				if (info.Sort is BuildingData.TileSort.Block or BuildingData.TileSort.Platform)
					blockLike++;
				else
					furnitureLike++;
			}

			if (sortCounts.TryGetValue(info.Sort, out int cur))
				sortCounts[info.Sort] = cur + 1;
			else
				sortCounts[info.Sort] = 1;
		}

		Console.WriteLine("Legacy map classification summary:");
		Console.WriteLine("  Cells with wall flag: " + withWall);
		Console.WriteLine("  Block/platform cells: " + blockLike);
		Console.WriteLine("  Furniture sort cells: " + furnitureLike);
		Console.WriteLine("  MP-safe by design: YES (classification placement)");
		Console.WriteLine("Top sorts:");
		foreach (var kv in sortCounts.OrderByDescending(p => p.Value).Take(12))
			Console.WriteLine("  " + kv.Key + " " + kv.Value);
	}

	private static void Increment(Dictionary<int, int> dict, int key)
	{
		if (dict.TryGetValue(key, out int cur))
			dict[key] = cur + 1;
		else
			dict[key] = 1;
	}

	private static void InstallAssemblyResolver()
	{
		AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
		{
			var name = new AssemblyName(e.Name).Name;
			string[] probeRoots =
			{
				AppContext.BaseDirectory,
				@"D:\SteamLibrary\steamapps\common\Terraria",
				@"d:\improvegame\CreateWandPatch\lib"
			};

			foreach (string root in probeRoots)
			{
				if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
					continue;

				string dll = Path.Combine(root, name + ".dll");
				if (File.Exists(dll))
					return Assembly.LoadFrom(dll);

				if (name == "Terraria")
				{
					string exe = Path.Combine(root, "Terraria.exe");
					if (File.Exists(exe))
						return Assembly.LoadFrom(exe);
				}
			}

			return null;
		};
	}

	private static MethodInfo ResolveTileMethod(string name)
	{
		Type t = Type.GetType("Terraria.Tile, Terraria");
		return t?.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
	}

	private static FieldInfo ResolveTileField(string name)
	{
		Type t = Type.GetType("Terraria.Tile, Terraria");
		return t?.GetField(name, BindingFlags.Public | BindingFlags.Instance);
	}

	private static int ResolveIdCount(string typeName, string fieldName)
	{
		Type t = Type.GetType(typeName + ", Terraria");
		if (t == null)
			return -1;
		FieldInfo f = t.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
		if (f == null)
			return -1;
		object val = f.GetValue(null);
		return val is int i ? i : -1;
	}

	private static bool InvokeBool(MethodInfo method, object instance) =>
		method != null && instance != null && method.Invoke(instance, null) is bool b && b;

	private static int GetIntFromMethod(MethodInfo method, object instance)
	{
		if (method == null || instance == null)
			return 0;
		object val = method.Invoke(instance, null);
		return val is byte b ? b : val is int i ? i : 0;
	}

	private static int GetIntField(FieldInfo field, object instance)
	{
		if (field == null || instance == null)
			return 0;
		object val = field.GetValue(instance);
		return val is ushort us ? us :
			val is short s ? s :
			val is byte b ? b :
			val is int i ? i : 0;
	}
}
