using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CreateWandPatch.Infrastructure.TmlTagIo
{
	/// <summary>
	/// 读取 ImproveGame / tModLoader <c>TagIO.ToFile</c> 生成的 <c>.qotstruct</c>（默认 GZip + 大端 Tag）。
	/// 仅实现 CreateWand 结构所需类型子集；与 tML 1.3.x–1.4.x 常见 <see cref="Terraria.ModLoader.IO.TagIO"/> 布局一致。
	/// </summary>
	internal static class TmlTagReader
	{
		private const byte IdByte = 1;
		private const byte IdShort = 2;
		private const byte IdInt = 3;
		private const byte IdLong = 4;
		private const byte IdFloat = 5;
		private const byte IdDouble = 6;
		private const byte IdByteArray = 7;
		private const byte IdString = 8;
		private const byte IdList = 9;
		private const byte IdCompound = 10;
		private const byte IdIntArray = 11;

		internal static TmlCompound ReadRootFromFile(string path, bool gzip = true)
		{
			using var fs = File.OpenRead(path);
			Stream stream = fs;
			if (gzip)
				stream = new GZipStream(fs, CompressionMode.Decompress);

			using (stream)
			using (var br = new BigEndianReader(stream))
			{
				object root = ReadTag(br, out _);
				if (root is not TmlCompound c)
					throw new InvalidDataException("Root is not a compound.");
				return c;
			}
		}

		private static object ReadTag(BigEndianReader br, out string name)
		{
			byte id = br.ReadByte();
			if (id == 0)
			{
				name = null;
				return null;
			}

			name = ReadStringPayload(br);
			return ReadPayload(id, br);
		}

		private static string ReadStringPayload(BigEndianReader br)
		{
			int len = br.ReadUInt16();
			if (len <= 0)
				return string.Empty;
			return Encoding.UTF8.GetString(br.ReadBytes(len));
		}

		private static object ReadPayload(byte id, BigEndianReader br)
		{
			switch (id)
			{
				case IdByte:
					return br.ReadByte();
				case IdShort:
					return br.ReadInt16();
				case IdInt:
					return br.ReadInt32();
				case IdLong:
					return br.ReadInt64();
				case IdFloat:
					return br.ReadSingle();
				case IdDouble:
					return br.ReadDouble();
				case IdByteArray:
				{
					int n = br.ReadInt32();
					return n <= 0 ? Array.Empty<byte>() : br.ReadBytes(n);
				}
				case IdString:
					return ReadStringPayload(br);
				case IdList:
					return ReadList(br);
				case IdCompound:
					return ReadCompound(br);
				case IdIntArray:
				{
					int n = br.ReadInt32();
					var arr = new int[n];
					for (int i = 0; i < n; i++)
						arr[i] = br.ReadInt32();
					return arr;
				}
				default:
					throw new InvalidDataException("Unsupported TagIO payload id: " + id);
			}
		}

		private static List<object> ReadList(BigEndianReader br)
		{
			byte elemId = br.ReadByte();
			int count = br.ReadInt32();
			var list = new List<object>(Math.Max(0, count));
			for (int i = 0; i < count; i++)
				list.Add(ReadPayload(elemId, br));
			return list;
		}

		private static TmlCompound ReadCompound(BigEndianReader br)
		{
			var c = new TmlCompound();
			while (true)
			{
				object tag = ReadTag(br, out string n);
				if (tag == null)
					break;
				c[n] = tag;
			}

			return c;
		}
	}

	internal sealed class TmlCompound : Dictionary<string, object>
	{
		internal int GetInt(string key, int defaultValue = 0)
		{
			if (!TryGetValue(key, out object o))
				return defaultValue;
			return o switch
			{
				byte b => b,
				short s => s,
				ushort us => us,
				int i => i,
				long l => (int)l,
				_ => defaultValue
			};
		}

		internal short GetShort(string key, short defaultValue = 0)
		{
			if (!TryGetValue(key, out object o))
				return defaultValue;
			return o switch
			{
				short s => s,
				int i => (short)i,
				byte b => b,
				long l => (short)l,
				_ => defaultValue
			};
		}

		internal byte GetByte(string key, byte defaultValue = 0)
		{
			if (!TryGetValue(key, out object o))
				return defaultValue;
			return o switch
			{
				byte b => b,
				short s => (byte)s,
				int i => (byte)i,
				_ => defaultValue
			};
		}

		internal bool TryGetCompound(string key, out TmlCompound c)
		{
			c = null;
			if (!TryGetValue(key, out object o) || o is not TmlCompound comp)
				return false;
			c = comp;
			return true;
		}

		internal List<object> GetList(string key) =>
			TryGetValue(key, out object o) && o is List<object> list ? list : new List<object>();

		internal bool TryGetList(string key, out List<object> list)
		{
			list = null;
			if (!TryGetValue(key, out object o) || o is not List<object> l)
				return false;
			list = l;
			return true;
		}

		internal static TmlCompound AsCompound(object o) => o as TmlCompound;
	}
}
