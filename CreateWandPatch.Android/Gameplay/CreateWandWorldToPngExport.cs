using System;
using System.IO;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
    /// <summary>
    /// Exports world rectangle as Tile snapshot + PNG preview.
    /// Android adaptation: PNG encoded as raw RGBA byte stream (no System.Drawing).
    /// </summary>
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
                    ? "[Wand] Area too large (max " + MaxCells + " cells)"
                    : "[Wand] Invalid area";
                return false;
            }

            try
            {
                // IL2CPP: Tile is struct constructed with tile offset
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

                // Write PNG preview as raw RGBA bytes (simplified - full PNG writer would need zlib)
                WriteSimplePng(data, savedPath);

                string cwPath = Path.ChangeExtension(savedPath, ".cwmap");
                CreateWandPreciseTileMap.Save(cwPath, w, h, tiles);
                return true;
            }
            catch (Exception ex)
            {
                error = "[Wand] Export failed: " + ex.Message;
                return false;
            }
        }

        private static Tile CloneWorldTile(int wx, int wy)
        {
            if (wx < 0 || wy < 0 || wx >= Main.maxTilesX || wy >= Main.maxTilesY)
                return default;

            // IL2CPP: Main.tile is TileData, not Tile[,]. Construct Tile from offset.
            int offset = wy * Main.maxTilesX + wx;
            return new Tile(offset);
        }

        public static bool TrySaveBlueprintPreviewPng(BuildingData data, string fullPath, out string error)
        {
            error = null;
            if (data == null || data.Width * data.Height > MaxCells || data.Width <= 0 || data.Height <= 0)
            {
                error = "[Wand] Invalid blueprint";
                return false;
            }

            try
            {
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                WriteSimplePng(data, fullPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Write a minimal uncompressed RGBA PNG file.
        /// For production, use Unity's ImageConversion.EncodeToPNG at runtime.
        /// </summary>
        private static void WriteSimplePng(BuildingData data, string path)
        {
            int w = data.Width;
            int h = data.Height;
            var infos = data.TileInfos;

            // Build raw filtered scanlines (filter byte 0 = None per row)
            int rawLen = h * (1 + w * 4); // 1 filter byte per row + 4 bytes per pixel
            var raw = new byte[rawLen];
            int p = 0;
            for (int y = 0; y < h; y++)
            {
                raw[p++] = 0; // filter: None
                for (int x = 0; x < w; x++)
                {
                    var c = infos[y * w + x].ToColor();
                    raw[p++] = c.r;
                    raw[p++] = c.g;
                    raw[p++] = c.b;
                    raw[p++] = c.a;
                }
            }

            // Write minimal PNG with uncompressed IDAT (zlib stored, no compression)
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                // PNG signature
                bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

                // IHDR
                WritePngChunk(bw, "IHDR", bw2 => {
                    bw2.Write(ToBigEndian(w));
                    bw2.Write(ToBigEndian(h));
                    bw2.Write((byte)8);  // bit depth
                    bw2.Write((byte)6);  // color type: RGBA
                    bw2.Write((byte)0);  // compression
                    bw2.Write((byte)0);  // filter
                    bw2.Write((byte)0);  // interlace
                });

                // IDAT (stored, uncompressed)
                int dataLen = rawLen;
                // zlib header: 0x78 0x01 (no compression)
                int zlibLen = 2 + dataLen + 4; // header + data + adler32
                var zlib = new byte[zlibLen];
                zlib[0] = 0x78;
                zlib[1] = 0x01;
                Array.Copy(raw, 0, zlib, 2, dataLen);
                // Simple adler32=1 for empty/stored (will be incorrect but many decoders don't check)
                uint adler = Adler32(raw, 0, dataLen);
                zlib[zlibLen - 4] = (byte)((adler >> 24) & 0xFF);
                zlib[zlibLen - 3] = (byte)((adler >> 16) & 0xFF);
                zlib[zlibLen - 2] = (byte)((adler >> 8) & 0xFF);
                zlib[zlibLen - 1] = (byte)(adler & 0xFF);

                WritePngChunkRaw(bw, "IDAT", zlib);

                // IEND
                WritePngChunk(bw, "IEND", _ => { });
            }
        }

        private static void WritePngChunk(BinaryWriter bw, string type, Action<BinaryWriter> writeData)
        {
            var ms = new MemoryStream();
            var bw2 = new BinaryWriter(ms);
            writeData(bw2);
            bw2.Flush();
            byte[] data = ms.ToArray();
            WritePngChunkRaw(bw, type, data);
        }

        private static void WritePngChunkRaw(BinaryWriter bw, string type, byte[] data)
        {
            bw.Write(ToBigEndian(data.Length));
            bw.Write(System.Text.Encoding.ASCII.GetBytes(type));
            bw.Write(data);
            uint crc = Crc32(type, data);
            bw.Write(ToBigEndian((int)crc));
        }

        private static int ToBigEndian(int v) =>
            ((v & 0xFF) << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | ((v >> 24) & 0xFF);

        private static uint Crc32(string type, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (char c in type)
                crc = Crc32Update(crc, (byte)c);
            foreach (byte b in data)
                crc = Crc32Update(crc, b);
            return crc ^ 0xFFFFFFFF;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        private static uint Crc32Update(uint crc, byte b) =>
            CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

        private static uint Adler32(byte[] data, int offset, int length)
        {
            uint a = 1, b = 0;
            for (int i = offset; i < offset + length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }
}
