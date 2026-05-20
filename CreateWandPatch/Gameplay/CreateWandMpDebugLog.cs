using System;
using System.IO;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	internal static class CreateWandMpDebugLog
	{
		private static readonly object Gate = new object();
		private static string _cachedPath;

		/// <summary>每次程序集注入（<see cref="Bootstrap.Init"/>）时调用：删旧联机日志，新会话从空文件写起。</summary>
		internal static void ClearOnInject()
		{
			try
			{
				lock (Gate)
				{
					_cachedPath = null;
					string path = ResolveLogPath();
					if (!string.IsNullOrEmpty(path) && File.Exists(path))
						File.Delete(path);
				}
			}
			catch
			{
			}
		}

		/// <summary>物块/墙「闪回」检测；与 <see cref="CreateWandSelectionState.EnableMpActionTrace"/> 独立，仅受 <c>EnableMpTileRollbackTrace</c> 控制。</summary>
		public static void WriteTileRollback(string message)
		{
			if (!CreateWandSelectionState.EnableMpTileRollbackTrace)
				return;
			try
			{
				AppendLine("TileRollback " + message);
			}
			catch
			{
			}
		}

		public static void Write(string message)
		{
			if (!CreateWandSelectionState.EnableMpActionTrace)
				return;

			try
			{
				string path = ResolveLogPath();
				if (string.IsNullOrEmpty(path))
					return;

				AppendLine(message);
			}
			catch
			{
				// keep gameplay unaffected
			}
		}

		private static void AppendLine(string message)
		{
			string path = ResolveLogPath();
			if (string.IsNullOrEmpty(path))
				return;
			string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine;
			lock (Gate)
			{
				File.AppendAllText(path, line);
			}
		}

		private static string ResolveLogPath()
		{
			if (!string.IsNullOrEmpty(_cachedPath))
				return _cachedPath;

			try
			{
				string path = TryResolveFromEnvironment();
				if (!string.IsNullOrEmpty(path))
				{
					_cachedPath = path;
					return _cachedPath;
				}

				path = TryResolveNextToAssembly();
				if (!string.IsNullOrEmpty(path))
				{
					_cachedPath = path;
					return _cachedPath;
				}

				string baseDir = null;
				if (!string.IsNullOrEmpty(Main.SavePath))
					baseDir = Path.Combine(Main.SavePath, "CreateWand");
				if (string.IsNullOrEmpty(baseDir))
					baseDir = Path.GetTempPath();
				Directory.CreateDirectory(baseDir);
				_cachedPath = Path.Combine(baseDir, "CreateWandPatch-mp.log");
				return _cachedPath;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// <c>CREATEWAND_MP_LOG_PATH</c>：日志文件的完整路径（含文件名）。<br/>
		/// <c>CREATEWAND_MP_LOG_DIR</c>：目录；文件名仍为 CreateWandPatch-mp.log。
		/// </summary>
		private static string TryResolveFromEnvironment()
		{
			try
			{
				string full = Environment.GetEnvironmentVariable("CREATEWAND_MP_LOG_PATH");
				if (!string.IsNullOrWhiteSpace(full))
				{
					full = full.Trim().Trim('"');
					string parent = Path.GetDirectoryName(full);
					if (!string.IsNullOrEmpty(parent))
						Directory.CreateDirectory(parent);
					return full;
				}

				string dir = Environment.GetEnvironmentVariable("CREATEWAND_MP_LOG_DIR");
				if (!string.IsNullOrWhiteSpace(dir))
				{
					dir = dir.Trim().Trim('"');
					Directory.CreateDirectory(dir);
					return Path.Combine(dir, "CreateWandPatch-mp.log");
				}
			}
			catch
			{
			}

			return null;
		}

		/// <summary>与 CreateWandPatch.dll 同目录下 <c>logs\CreateWandPatch-mp.log</c>（便于开发时日志落在生成/注入目录旁，仓库内可直接打开）。</summary>
		private static string TryResolveNextToAssembly()
		{
			try
			{
				string loc = typeof(CreateWandMpDebugLog).Assembly.Location;
				if (string.IsNullOrEmpty(loc))
					return null;
				string dllDir = Path.GetDirectoryName(loc);
				if (string.IsNullOrEmpty(dllDir))
					return null;
				string logsDir = Path.Combine(dllDir, "logs");
				Directory.CreateDirectory(logsDir);
				return Path.Combine(logsDir, "CreateWandPatch-mp.log");
			}
			catch
			{
				return null;
			}
		}
	}
}
