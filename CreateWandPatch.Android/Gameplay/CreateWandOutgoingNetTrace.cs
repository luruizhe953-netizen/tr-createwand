using System;
using System.IO;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 联机客户端钩住 <see cref="Terraria.NetMessage.SendData"/> 时的文本日志（与魔杖无关）。
	/// 下行见 <see cref="CreateWandIncomingNetTrace"/>；开关共用。
	/// </summary>
	internal static class CreateWandOutgoingNetTrace
	{
		private static readonly object Gate = new object();
		private static string _cachedPath;

		internal static void ClearCachedPath()
		{
			lock (Gate)
				_cachedPath = null;
		}

		public static void Write(string line)
		{
			if (!CreateWandSelectionState.EnableClientOutgoingNetTrace)
				return;

			try
			{
				string path = ResolveLogPath();
				if (string.IsNullOrEmpty(path))
					return;

				string full = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine;
				lock (Gate)
				{
					CreateWandLogFileWriter.AppendUtf8(path, full);
				}
			}
			catch
			{
			}
		}

		private static string ResolveLogPath()
		{
			if (!string.IsNullOrEmpty(_cachedPath))
				return _cachedPath;

			try
			{
				string envFull = Environment.GetEnvironmentVariable("CREATEWAND_OUTGOING_LOG_PATH");
				if (!string.IsNullOrWhiteSpace(envFull))
				{
					envFull = envFull.Trim().Trim('"');
					string parent = Path.GetDirectoryName(envFull);
					if (!string.IsNullOrEmpty(parent))
						Directory.CreateDirectory(parent);
					_cachedPath = envFull;
					return _cachedPath;
				}

				string dirVar = Environment.GetEnvironmentVariable("CREATEWAND_OUTGOING_LOG_DIR");
				if (!string.IsNullOrWhiteSpace(dirVar))
				{
					dirVar = dirVar.Trim().Trim('"');
					Directory.CreateDirectory(dirVar);
					_cachedPath = Path.Combine(dirVar, "CreateWandPatch-outgoing-net.log");
					return _cachedPath;
				}

				string baseDir = null;
				if (!string.IsNullOrEmpty(Main.SavePath))
					baseDir = Path.Combine(Main.SavePath, "CreateWand");
				if (string.IsNullOrEmpty(baseDir))
					baseDir = Path.GetTempPath();
				Directory.CreateDirectory(baseDir);
				_cachedPath = Path.Combine(baseDir, "CreateWandPatch-outgoing-net.log");
				return _cachedPath;
			}
			catch
			{
				return null;
			}
		}
	}
}
