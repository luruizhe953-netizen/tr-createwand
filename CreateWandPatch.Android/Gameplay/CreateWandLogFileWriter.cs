using System;
using System.IO;
using System.Text;

namespace CreateWandPatch.Gameplay
{
	/// <summary>所有补丁文本日志共用：单文件最大 <see cref="DefaultMaxBytes"/>，超出则保留尾部。</summary>
	internal static class CreateWandLogFileWriter
	{
		public const int DefaultMaxBytes = 20 * 1024;

		private static readonly object Gate = new object();

		public static void AppendUtf8(string path, string text, int maxBytes = DefaultMaxBytes)
		{
			if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(text))
				return;

			lock (Gate)
			{
				try
				{
					string dir = Path.GetDirectoryName(path);
					if (!string.IsNullOrEmpty(dir))
						Directory.CreateDirectory(dir);

					byte[] add = Encoding.UTF8.GetBytes(text);
					byte[] existing = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
					int total = existing.Length + add.Length;
					if (total <= maxBytes)
					{
						using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
							fs.Write(add, 0, add.Length);
						return;
					}

					byte[] merged = new byte[total];
					Buffer.BlockCopy(existing, 0, merged, 0, existing.Length);
					Buffer.BlockCopy(add, 0, merged, existing.Length, add.Length);
					int start = AlignUtf8Start(merged, total - maxBytes);
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
