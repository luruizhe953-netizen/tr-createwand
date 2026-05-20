using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using CreateWandPatch.Gameplay;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CreateWandPatch.Content
{
	/// <summary>从 <c>Main.SavePath\CreateWand\</c> 加载 <c>*.png</c>、<c>*.cwmap</c> 与 ImproveGame <c>*.qotstruct</c>。</summary>
	public static class CreateWandPngLibrary
	{
		public static readonly List<CreateWandBlueprintEntry> Entries = new();

		/// <summary>已完成一次加载后为 true；调用 <see cref="Reload"/> 可清除并重扫描。</summary>
		private static bool _loaded;

		public static string GetSaveSubFolder() => Path.Combine(Main.SavePath, "CreateWand");

		/// <summary>
		/// 确保已加载（懒加载，只在首次或 <see cref="Reload"/> 后才真正扫描文件系统）。
		/// 每帧可安全调用，无额外开销。
		/// </summary>
		public static void EnsureReload()
		{
			if (_loaded)
				return;
			Reload();
		}

		/// <summary>强制重新扫描目录并重建 <see cref="Entries"/>。切换 PNG 热键后调用。</summary>
		public static void Reload()
		{
			_loaded = false;
			Entries.Clear();
			try
			{
				string dir = GetSaveSubFolder();
				Directory.CreateDirectory(dir);
				string[] pngFiles = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
				string[] qotFiles = Directory.GetFiles(dir, "*.qotstruct", SearchOption.TopDirectoryOnly);
				string[] cwOnlyCandidates = Directory.GetFiles(dir, "*.cwmap", SearchOption.TopDirectoryOnly);
				var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var pngStems = new HashSet<string>(pngFiles.Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

				IEnumerable<string> pngThenQot = pngFiles.Concat(qotFiles).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
				foreach (string file in pngThenQot)
				{
					try
					{
						BuildingData data = null;
						CreateWandBlueprintSource source;
						BuildingData preciseOptional = null;
						if (file.EndsWith(".qotstruct", StringComparison.OrdinalIgnoreCase))
						{
							if (CreateWandQotStructConverter.TryFromFile(file, out var qLegacy, out var qPrecise))
							{
								data = qLegacy;
								preciseOptional = qPrecise;
							}

							source = CreateWandBlueprintSource.QotStruct;
						}
						else
						{
							source = CreateWandBlueprintSource.PngDataMap;
							if (Main.dedServ || Main.graphics == null || Main.graphics.GraphicsDevice == null)
								data = LoadPngCpu(file);
							else
							{
								using (var fs = File.OpenRead(file))
								using (var tex = Texture2D.FromStream(Main.graphics.GraphicsDevice, fs))
								{
									data = BuildingData.FromDataMap(tex);
								}
							}

							if (data != null)
							{
								string pngStem = Path.GetFileNameWithoutExtension(file);
								string cwSidecar = Path.Combine(dir, pngStem + ".cwmap");
								if (File.Exists(cwSidecar) && CreateWandPreciseTileMap.TryLoad(cwSidecar, out var sidePrecise) &&
								    sidePrecise.Width == data.Width && sidePrecise.Height == data.Height)
									preciseOptional = sidePrecise;
							}
						}

						if (data != null && data.Width * data.Height <= BuildingData.MaxBlueprintCells)
						{
							string stem = Path.GetFileNameWithoutExtension(file);
							string name = MakeUniqueName(stem, source, seenNames);
							Entries.Add(new CreateWandBlueprintEntry(name, data, source, preciseOptional));
							seenNames.Add(name);
						}
					}
					catch
					{
						/* skip bad file */
					}
				}

				foreach (string cwFile in cwOnlyCandidates.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
				{
					string stem = Path.GetFileNameWithoutExtension(cwFile);
					if (pngStems.Contains(stem))
						continue;
					try
					{
						if (!CreateWandPreciseTileMap.TryLoad(cwFile, out var preciseData))
							continue;
						if (preciseData.Width * preciseData.Height > BuildingData.MaxBlueprintCells)
							continue;

						Tile[] preciseTiles = preciseData.ClonePreciseTilesOrNull();
						if (preciseTiles == null || preciseTiles.Length != preciseData.Width * preciseData.Height)
							continue;

						BuildingData legacySafeData =
							CreateWandTileBlueprintClassifier.FromPreciseTilesAsLegacy(preciseData.Width, preciseData.Height, preciseTiles);
						string name = MakeUniqueName(stem, CreateWandBlueprintSource.CwMap, seenNames);
						Entries.Add(new CreateWandBlueprintEntry(name, legacySafeData, CreateWandBlueprintSource.CwMap, preciseData));
						seenNames.Add(name);
					}
					catch
					{
						/* skip */
					}
				}
			}
			catch
			{
				/* no save path yet */
			}

			_loaded = true;
		}

		private static BuildingData LoadPngCpu(string path)
		{
			using (var bitmap = new Bitmap(path))
			{
				int w = bitmap.Width;
				int h = bitmap.Height;
				int n = w * h;
				if (n > BuildingData.MaxBlueprintCells || n <= 0)
					return BuildingData.FromColorGrid(1, 1, new[] { Microsoft.Xna.Framework.Color.White });
				var colors = new Microsoft.Xna.Framework.Color[n];
				int k = 0;
				for (int y = 0; y < h; y++)
				{
					for (int x = 0; x < w; x++)
					{
						System.Drawing.Color c = bitmap.GetPixel(x, y);
						colors[k++] = new Microsoft.Xna.Framework.Color(c.R, c.G, c.B, c.A);
					}
				}

				return BuildingData.FromColorGrid(w, h, colors);
			}
		}

		private static string MakeUniqueName(string stem, CreateWandBlueprintSource source, HashSet<string> seenNames)
		{
			string suffix = source switch
			{
				CreateWandBlueprintSource.PngDataMap => "",
				CreateWandBlueprintSource.QotStruct => " [qot]",
				CreateWandBlueprintSource.CwMap => " [cwmap]",
				_ => ""
			};

			string baseName = stem + suffix;
			string candidate = baseName;
			int idx = 2;
			while (seenNames.Contains(candidate))
			{
				candidate = baseName + " #" + idx;
				idx++;
			}

			return candidate;
		}
	}
}
