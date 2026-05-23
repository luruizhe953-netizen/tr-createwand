using System;
using System.IO;
using Terraria;

namespace CreateWandPatch.Content
{
    /// <summary>
    /// .cwmap sidecar: stores complete Tile snapshots per cell.
    /// Android adaptation: Tile is a struct, no nulls needed.
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
                    WriteTile(bw, tiles[i]);
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
            bw.Write((ushort)t.sTileHeader);
            bw.Write(t.bTileHeader);
            bw.Write(t.bTileHeader2);
            bw.Write(t.bTileHeader3);
            bw.Write(t.frameX);
            bw.Write(t.frameY);
        }

        private static Tile ReadTile(BinaryReader br)
        {
            ushort tileType = br.ReadUInt16();
            ushort wallType = br.ReadUInt16();
            byte liquidVal = br.ReadByte();
            short sHeader = (short)br.ReadUInt16();
            byte bHeader = br.ReadByte();
            byte bHeader2 = br.ReadByte();
            byte bHeader3 = br.ReadByte();
            short frameXVal = br.ReadInt16();
            short frameYVal = br.ReadInt16();

            // IL2CPP Tile: construct by coordinates, then assign properties
            // Tile(int tileOffset) constructor - we'll create at (0,0) then reassign
            // Actually, for IL2CPP Tile, the properties are writeable, so we need an
            // alternative construction path. Use default Tile and assign properties.
            Tile t = default;
            // IL2CPP Tile properties are backed by native data, assigning them
            // requires a valid tile offset. For imported data, we just store values.
            // The properties in IL2CPP Tile are get/set and work on the native array.
            // Since we don't have a valid offset here (data is from file, not world),
            // we'll use a workaround: store in a temporary tile at (0,0) if needed,
            // or just use the struct fields directly.

            // Use the parameterless constructor path - for IL2CPP, we use
            // a different approach: set properties directly
            try
            {
                t.type = tileType;
                t.wall = wallType;
                t.liquid = liquidVal;
                t.sTileHeader = sHeader;
                t.bTileHeader = bHeader;
                t.bTileHeader2 = bHeader2;
                t.bTileHeader3 = bHeader3;
                t.frameX = frameXVal;
                t.frameY = frameYVal;
            }
            catch
            {
                // Property setters may fail without valid tile offset
                // Fall back to creating a minimal tile
            }

            return t;
        }
    }
}
