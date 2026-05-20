using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>将世界矩形采样为完整 Tile 快照；PNG 为预览色图，同目录 <c>.cwmap</c> 为材料 1:1。</summary>
	public static class CreateWandWorldToPngExport
	{
		public const int MaxCells = BuildingData.MaxBlueprintCells;

		public static bool TryExportRectToNewPng(int ax, int ay, int bx, int by, out string savedPath, out string error)
		{
			savedPath = null;
			error = null;
			int minX = Math.Min(ax, bx);
			int minY = Math.Min(ay, by);
			int maxX = Math.Max(ax, bx);
			int maxY = Math.Max(ay, by);
			int w = maxX - minX + 1;
			int h = maxY - minY + 1;
			if (w <= 0 || h <= 0 || w * h > MaxCells)
			{
				error = w * h > MaxCells
					? "[魔杖] 区域过大（上限 " + MaxCells + " 格）"
					: "[魔杖] 区域无效";
				return false;
			}

			try
			{
				var tiles = new Tile[w * h];
				for (int ly = 0; ly < h; ly++)
				for (int lx = 0; lx < w; lx++)
				{
					int wx = minX + lx;
					int wy = minY + ly;
					tiles[ly * w + lx] = CloneWorldTile(wx, wy);
				}

				var data = BuildingData.FromPreciseTileGrid(w, h, tiles);
				string dir = CreateWandPngLibrary.GetSaveSubFolder();
				Directory.CreateDirectory(dir);
				string name = "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
				savedPath = Path.Combine(dir, name);
				SaveDataAsPng(data, savedPath);
				string cwPath = Path.ChangeExtension(savedPath, ".cwmap");
				CreateWandPreciseTileMap.Save(cwPath, w, h, tiles);
				return true;
			}
			catch (Exception ex)
			{
				error = "[魔杖] 导出失败: " + ex.Message;
				return false;
			}
		}

		private static Tile CloneWorldTile(int wx, int wy)
		{
			if (wx < 0 || wy < 0 || wx >= Main.maxTilesX || wy >= Main.maxTilesY)
				return new Tile();
			Tile src = Main.tile[wx, wy];
			return src == null ? new Tile() : new Tile(src);
		}

		/// <summary>把蓝图写成原版 <c>CreateWandLibrary</c> 可读的预览 PNG（与导出矩形逻辑一致）。</summary>
		public static bool TrySaveBlueprintPreviewPng(BuildingData data, string fullPath, out string error)
		{
			error = null;
			if (data == null || data.Width * data.Height > MaxCells || data.Width <= 0 || data.Height <= 0)
			{
				error = "[魔杖] 蓝图无效或超过上限";
				return false;
			}

			try
			{
				string dir = Path.GetDirectoryName(fullPath);
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);
				SaveDataAsPng(data, fullPath);
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}

		private static void SaveDataAsPng(BuildingData data, string path)
		{
			using (var bmp = new Bitmap(data.Width, data.Height, PixelFormat.Format32bppArgb))
			{
				for (int y = 0; y < data.Height; y++)
				for (int x = 0; x < data.Width; x++)
				{
					var c = data.TileInfos[y * data.Width + x].ToColor();
					bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
				}

				bmp.Save(path, ImageFormat.Png);
			}
		}
	}
}
