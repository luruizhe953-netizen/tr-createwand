using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreateWandPatch.Gameplay;
using Terraria;

namespace CreateWandPatch.Content
{
    /// <summary>
    /// Loads *.png, *.cwmap, and ImproveGame *.qotstruct blueprints from Main.SavePath/CreateWand/.
    /// Android adaptation: uses raw file byte reading instead of XNA Texture2D.FromStream.
    /// </summary>
    public static class CreateWandPngLibrary
    {
        public static readonly List<CreateWandBlueprintEntry> Entries = new();

        private static bool _loaded;

        public static string GetSaveSubFolder() => Path.Combine(Main.SavePath, "CreateWand");

        public static void EnsureReload()
        {
            if (_loaded) return;
            Reload();
        }

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
                            data = LoadPngFromFile(file);

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
                    catch { /* skip bad file */ }
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
                    catch { /* skip */ }
                }
            }
            catch { /* no save path yet */ }

            _loaded = true;
        }

        /// <summary>
        /// Load a PNG file and extract RGBA pixel data using raw byte parsing.
        /// At runtime on Android IL2CPP, Unity Texture2D.LoadImage is available
        /// and will be used through Il2Cpp interop; here we use raw PNG parsing
        /// for compile-time compatibility with dummy DLLs.
        /// </summary>
        private static BuildingData LoadPngFromFile(string path)
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            return LoadPngRaw(fileBytes);
        }

        /// <summary>
        /// Minimal PNG parser: reads IHDR for dimensions, then IDAT for raw pixel data.
        /// Handles only 8-bit RGBA PNGs without interlacing.
        /// </summary>
        private static BuildingData LoadPngRaw(byte[] data)
        {
            // Check PNG signature
            if (data.Length < 33 || data[0] != 0x89 || data[1] != 'P' || data[2] != 'N' || data[3] != 'G')
                return null;

            // Read IHDR at offset 16 (8 byte sig + 4 length + 4 'IHDR')
            int width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            byte bitDepth = data[24];
            byte colorType = data[25];

            if (width <= 0 || height <= 0 || width * height > BuildingData.MaxBlueprintCells)
                return null;

            // Only handle 8-bit RGBA (colorType=6) and RGB (colorType=2)
            if (bitDepth != 8 || (colorType != 6 && colorType != 2))
                return null;

            int bytesPerPixel = colorType == 6 ? 4 : 3;
            var rgbaBytes = new byte[width * height * 4];

            // Simple IDAT extraction
            int pos = 33; // after IHDR
            int byteCount = 0;

            while (pos < data.Length - 8 && byteCount < rgbaBytes.Length)
            {
                int chunkLen = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                string chunkType = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                pos += 8;

                if (chunkType == "IDAT" && colorType == 6 && chunkLen == 1 + (long)width * height * 4 + height)
                {
                    for (int y = 0; y < height && byteCount < rgbaBytes.Length; y++)
                    {
                        pos++; // skip filter byte
                        for (int x = 0; x < width && byteCount < rgbaBytes.Length; x++)
                        {
                            rgbaBytes[byteCount++] = data[pos];     // R
                            rgbaBytes[byteCount++] = data[pos + 1]; // G
                            rgbaBytes[byteCount++] = data[pos + 2]; // B
                            rgbaBytes[byteCount++] = data[pos + 3]; // A
                            pos += 4;
                        }
                    }
                }

                pos += chunkLen + 4; // skip CRC
                if (chunkType == "IEND") break;
            }

            if (byteCount == 0)
                return null;

            return BuildingData.FromRawPixels(width, height, rgbaBytes);
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
