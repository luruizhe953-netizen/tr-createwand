using System;
using System.Drawing;
using System.IO;
using CreateWandPatch.Content;

namespace CreateWandPatch.Gameplay
{
	internal static class CreateWandVanillaLibrarySync
	{
		public static string SanitizeBlueprintFileStem(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return "createwand_patch";
			char[] invalid = Path.GetInvalidFileNameChars();
			var chars = name.Trim().ToCharArray();
			for (int i = 0; i < chars.Length; i++)
			{
				for (int j = 0; j < invalid.Length; j++)
				{
					if (chars[i] != invalid[j])
						continue;
					chars[i] = '_';
					break;
				}
			}

			string stem = new string(chars).Trim();
			return string.IsNullOrEmpty(stem) ? "createwand_patch" : stem;
		}

		public static bool TryWriteVanillaPreviewPng(string wantedName, BuildingData data, out string stemWritten, out string error)
		{
			stemWritten = SanitizeBlueprintFileStem(wantedName);
			error = null;
			if (data == null || data.Width <= 0 || data.Height <= 0)
			{
				error = "invalid building data";
				return false;
			}

			try
			{
				string dir = CreateWandPngLibrary.GetSaveSubFolder();
				Directory.CreateDirectory(dir);
				string path = Path.Combine(dir, stemWritten + ".png");
				using (var bmp = new Bitmap(data.Width, data.Height))
				{
					for (int y = 0; y < data.Height; y++)
					{
						for (int x = 0; x < data.Width; x++)
						{
							int idx = y * data.Width + x;
							BuildingData.TileInfo info = data.TileInfos[idx];
							Microsoft.Xna.Framework.Color c = info.ToColor();
							bmp.SetPixel(x, y, Color.FromArgb(c.A, c.R, c.G, c.B));
						}
					}

					bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
				}

				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}
	}
}
