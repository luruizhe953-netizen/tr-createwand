using System;
using System.IO;
using System.Text;

namespace TerrariaPatchInjector
{
	/// <summary>注入器桌面日志：单文件最大 20KB，超出保留尾部（与 CreateWandPatch 一致）。</summary>
	internal static class CappedLogFile
	{
		public const int MaxBytes = 20 * 1024;
		private static readonly object Gate = new object();

		public static void AppendUtf8(string path, string text)
		{
			if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(text))
				return;

			lock (Gate)
			{
				try
				{
					byte[] add = Encoding.UTF8.GetBytes(text);
					byte[] existing = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
					int total = existing.Length + add.Length;
					if (total <= MaxBytes)
					{
						using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
							fs.Write(add, 0, add.Length);
						return;
					}

					byte[] merged = new byte[total];
					Buffer.BlockCopy(existing, 0, merged, 0, existing.Length);
					Buffer.BlockCopy(add, 0, merged, existing.Length, add.Length);
					int start = AlignUtf8Start(merged, total - MaxBytes);
					int len = total - start;
					var trimmed = new byte[len];
					Buffer.BlockCopy(merged, start, trimmed, 0, len);
					File.WriteAllBytes(path, trimmed);
				}
				catch
				{
				}
			}
		}

		public static void WriteUtf8Capped(string path, string text)
		{
			if (string.IsNullOrEmpty(path))
				return;
			lock (Gate)
			{
				try
				{
					byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
					if (bytes.Length <= MaxBytes)
					{
						File.WriteAllBytes(path, bytes);
						return;
					}

					int start = AlignUtf8Start(bytes, bytes.Length - MaxBytes);
					int len = bytes.Length - start;
					var trimmed = new byte[len];
					Buffer.BlockCopy(bytes, start, trimmed, 0, len);
					File.WriteAllBytes(path, trimmed);
				}
				catch
				{
				}
			}
		}

		private static int AlignUtf8Start(byte[] buf, int index)
		{
			if (index <= 0)
				return 0;
			while (index < buf.Length && (buf[index] & 0xC0) == 0x80)
				index++;
			return index;
		}
	}
}
