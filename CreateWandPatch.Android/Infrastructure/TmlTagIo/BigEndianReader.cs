using System;
using System.IO;

namespace CreateWandPatch.Infrastructure.TmlTagIo
{
	/// <summary>tModLoader <c>TagIO</c> 使用大端序二进制（与 <c>BinaryReader</c> 默认小端相反）。</summary>
	internal sealed class BigEndianReader : IDisposable
	{
		private readonly BinaryReader _r;

		internal BigEndianReader(Stream stream) => _r = new BinaryReader(stream);

		public void Dispose() => _r.Dispose();

		public byte ReadByte() => _r.ReadByte();

		public byte[] ReadBytes(int count) => _r.ReadBytes(count);

		public short ReadInt16()
		{
			ushort hi = _r.ReadByte();
			ushort lo = _r.ReadByte();
			return (short)((hi << 8) | lo);
		}

		public ushort ReadUInt16()
		{
			ushort hi = _r.ReadByte();
			ushort lo = _r.ReadByte();
			return (ushort)((hi << 8) | lo);
		}

		public int ReadInt32()
		{
			uint b0 = _r.ReadByte();
			uint b1 = _r.ReadByte();
			uint b2 = _r.ReadByte();
			uint b3 = _r.ReadByte();
			return (int)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
		}

		public long ReadInt64()
		{
			ulong v = 0;
			for (int i = 0; i < 8; i++)
				v = (v << 8) | _r.ReadByte();
			return (long)v;
		}

		public float ReadSingle()
		{
			byte[] b = _r.ReadBytes(4);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(b);
			return BitConverter.ToSingle(b, 0);
		}

		public double ReadDouble()
		{
			byte[] b = _r.ReadBytes(8);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(b);
			return BitConverter.ToDouble(b, 0);
		}
	}
}
