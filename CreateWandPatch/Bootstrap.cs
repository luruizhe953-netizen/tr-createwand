using System;
using System.IO;
using System.Text;
using CreateWandPatch.Gameplay;
using CreateWandPatch.Infrastructure;
using HarmonyLib;

namespace CreateWandPatch
{
	public static class Bootstrap
	{
		private static bool _initialized;
		private static readonly string LogPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
			"CreateWandPatch-harmony.txt");

		public static void Init()
		{
			if (_initialized)
				return;
			_initialized = true;

			CreateWandMpDebugLog.ClearOnInject();
			CreateWandOutgoingNetTrace.ClearCachedPath();
			CreateWandIncomingNetTrace.ClearCachedPath();

			var sb = new StringBuilder();
			sb.AppendLine(DateTime.Now + " Bootstrap.Init() start");

			try
			{
				var harmony = new Harmony("com.local.terraria.createwand");

				// 逐个补丁类单独应用，记录每一个的结果
				foreach (var type in typeof(Bootstrap).Assembly.GetTypes())
				{
					if (type.Namespace == null || !type.Namespace.StartsWith("CreateWandPatch.Patches"))
						continue;
					try
					{
						var proc = new PatchClassProcessor(harmony, type);
						var result = proc.Patch();
						sb.AppendLine("  OK   " + type.Name + " -> " + (result == null ? "null" : result.Count + " patch(es)"));
					}
					catch (Exception ex)
					{
						sb.AppendLine("  FAIL " + type.Name + ": " + ex.ToString());
					}
				}

				ItemIdCompatBootstrap.Apply(sb);

				sb.AppendLine(DateTime.Now + " Bootstrap.Init() done");
			}
			catch (Exception ex)
			{
				sb.AppendLine(DateTime.Now + " FATAL: " + ex);
			}
			finally
			{
				try { CreateWandPatch.Gameplay.CreateWandLogFileWriter.AppendUtf8(LogPath, sb.ToString()); } catch { }
			}
		}
	}
}
