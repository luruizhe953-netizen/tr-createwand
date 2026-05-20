using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using EasyHook;

namespace TerrariaLoader
{
	internal static class Program
	{
		private static bool IsRetriableInjectFailure(Exception ex)
		{
			if (ex is TimeoutException)
				return true;
			var msg = ex.Message ?? string.Empty;
			return msg.IndexOf("Unable to wait for injection", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("STATUS_INTERNAL_ERROR", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("Code: 1", StringComparison.Ordinal) >= 0;
		}

		/// <summary>
		/// 仍存活的 Terraria：优先有主窗口的实例，其次最晚启动。
		/// 若 HasExited 因权限抛错，仍保留该进程作候选（否则会出现「明明有泰拉却找不到」）。
		/// </summary>
		private static Process FindLiveTerrariaProcess()
		{
			var list = Process.GetProcessesByName("Terraria").ToList();
			if (list.Count == 0)
				return null;

			int WindowScore(Process p)
			{
				try
				{
					return p.MainWindowHandle != IntPtr.Zero ? 1 : 0;
				}
				catch
				{
					return 0;
				}
			}

			var ordered = list
				.OrderByDescending(WindowScore)
				.ThenByDescending(ProcessStartTime);

			foreach (var p in ordered)
			{
				try
				{
					p.Refresh();
					if (!p.HasExited)
						return p;
				}
				catch
				{
					return p;
				}
			}

			return null;
		}

		/// <summary>
		/// 若跟踪的 PID 已退出（常见于单实例：后台仍有泰拉时新开的进程会立刻结束），改为注入仍存活的进程。
		/// </summary>
		private static void RebindIfTargetExited(ref Process proc, bool afterLaunch)
		{
			try
			{
				proc.Refresh();
				if (!proc.HasExited)
					return;
			}
			catch (InvalidOperationException)
			{
				// 进程已消失
			}
			catch (ArgumentException)
			{
				// 进程已消失
			}

			Console.WriteLine("PID " + proc.Id + " 已结束。" +
				(afterLaunch
					? " 泰拉多为单实例：已有进程（如僵尸 PID）时，新开的短暂 PID 会立刻退出，界面仍可能正常。"
					: string.Empty));
			Console.WriteLine("正在改为注入当前仍存活的 Terraria 进程…");

			var live = FindLiveTerrariaProcess();
			if (live == null)
				throw new InvalidOperationException("没有仍在运行的 Terraria.exe，无法注入。请先启动游戏或结束冲突进程。");

			Console.WriteLine("改用 PID=" + live.Id + "。");
			proc = live;
		}

		/// <summary>
		/// 新起的泰拉进程立刻注入时，EasyHook 常出现 Timeout / Code:1。等到主窗口出现并再缓冲几秒后再注入。
		/// </summary>
		private static void WaitForInjectionReadiness(ref Process proc, bool startedNewInstance, bool launchedAnotherInstance)
		{
			if (!startedNewInstance)
				return;

			Console.WriteLine("等待游戏窗口与 CLR 就绪后再注入（减轻 EasyHook 超时）…");
			const int minDelayMs = 6000;
			const int postWindowBufferMs = 3500;
			var sw = Stopwatch.StartNew();

			while (sw.ElapsedMilliseconds < 120000)
			{
				try
				{
					RebindIfTargetExited(ref proc, launchedAnotherInstance);

					proc.Refresh();
					if (proc.MainWindowHandle != IntPtr.Zero)
					{
						var pad = minDelayMs - (int)sw.ElapsedMilliseconds;
						if (pad > 0)
							Thread.Sleep(pad);
						Thread.Sleep(postWindowBufferMs);
						Console.WriteLine("已就绪（约 " + (sw.ElapsedMilliseconds / 1000.0).ToString("F1") + " s）。");
						return;
					}
				}
				catch (InvalidOperationException)
				{
					throw;
				}
				catch
				{
					// ignored
				}

				Thread.Sleep(400);
			}

			Thread.Sleep(minDelayMs);
			Console.WriteLine("未在时限内检测到主窗口，已使用固定延迟。");
		}

		private static void InjectWithRetries(int targetPid, string injectDll)
		{
			const int maxAttempts = 4;
			for (var attempt = 1; attempt <= maxAttempts; attempt++)
			{
				try
				{
					RemoteHooking.Inject(
						targetPid,
						InjectionOptions.Default,
						injectDll,
						injectDll);
					return;
				}
				catch (Exception ex)
				{
					if (attempt >= maxAttempts || !IsRetriableInjectFailure(ex))
						throw;

					Console.WriteLine("注入失败（第 " + attempt + "/" + maxAttempts + " 次），5 秒后重试…");
					Console.WriteLine(ex.Message);
					Thread.Sleep(5000);
					try
					{
						using (var check = Process.GetProcessById(targetPid))
						{
							if (check.HasExited)
								throw new InvalidOperationException("注入重试前目标进程已退出（PID " + targetPid + "）。");
						}
					}
					catch (ArgumentException)
					{
						throw new InvalidOperationException("注入重试前找不到进程 PID " + targetPid + "。");
					}
				}
			}
		}

		private static bool ArgIsLaunchFlag(string a) =>
			string.Equals(a, "--launch", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(a, "-launch", StringComparison.OrdinalIgnoreCase);

		private static string ResolveTerrariaExe(string[] args)
		{
			foreach (var a in args)
			{
				if (string.IsNullOrWhiteSpace(a) || (a.Length > 0 && (a[0] == '-' || a[0] == '/')))
					continue;
				try
				{
					if (File.Exists(a) && a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
						return Path.GetFullPath(a);
				}
				catch
				{
					// ignored
				}
			}

			return @"D:\SteamLibrary\steamapps\common\Terraria\Terraria.exe";
		}

		private static DateTime ProcessStartTime(Process p)
		{
			try
			{
				return p.StartTime;
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		private static int Main(string[] args)
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;

			string injectDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TerrariaPatchInjector.dll");
			if (!File.Exists(injectDll))
			{
				Console.Error.WriteLine("缺少 TerrariaPatchInjector.dll，请与 TerrariaLoader.exe 同目录。");
				return 1;
			}

			bool forceLaunch = args.Any(ArgIsLaunchFlag);
			string terrariaExe = ResolveTerrariaExe(args);

			var beforeIds = new HashSet<int>(Process.GetProcessesByName("Terraria").Select(p => p.Id));
			Process proc = null;
			bool startedByLoader = false;

			if (forceLaunch)
			{
				if (!File.Exists(terrariaExe))
				{
					Console.Error.WriteLine("未找到 Terraria.exe：");
					Console.Error.WriteLine(terrariaExe);
					Console.Error.WriteLine("用法: TerrariaLoader.exe [--launch] [Terraria.exe完整路径]");
					return 1;
				}

				Console.WriteLine("--launch：正在启动泰拉瑞亚（将优先注入「新出现」的进程，可绕过卡死的旧 PID）…");
				Process.Start(new ProcessStartInfo(terrariaExe) { UseShellExecute = true });

				for (int i = 0; i < 240; i++)
				{
					Thread.Sleep(250);
					var now = Process.GetProcessesByName("Terraria");
					var fresh = now.Where(p => !beforeIds.Contains(p.Id)).ToList();
					if (fresh.Count > 0)
					{
						proc = fresh.OrderByDescending(ProcessStartTime).First();
						startedByLoader = true;
						break;
					}
				}

				if (proc == null)
				{
					var all = Process.GetProcessesByName("Terraria").OrderByDescending(ProcessStartTime).ToList();
					proc = all.FirstOrDefault();
					if (proc == null)
					{
						Console.Error.WriteLine("启动后仍未发现 Terraria 进程（可能单实例限制未拉起新进程）。请结束僵尸 Terraria 后再试。");
						return 1;
					}

					Console.WriteLine("警告：未发现新的 PID（游戏可能禁止多开）。将注入当前最晚启动的 PID=" + proc.Id + "。");
				}
			}
			else
			{
				var existing = Process.GetProcessesByName("Terraria").OrderByDescending(ProcessStartTime).ToList();
				proc = existing.FirstOrDefault();

				if (proc == null)
				{
					if (!File.Exists(terrariaExe))
					{
						Console.Error.WriteLine("未找到运行中的 Terraria，且默认路径不存在：");
						Console.Error.WriteLine(terrariaExe);
						Console.Error.WriteLine("用法: TerrariaLoader.exe [--launch] [Terraria.exe完整路径]");
						return 1;
					}

					Console.WriteLine("正在启动泰拉瑞亚…");
					Process.Start(new ProcessStartInfo(terrariaExe) { UseShellExecute = true });
					for (int i = 0; i < 60; i++)
					{
						Thread.Sleep(500);
						proc = Process.GetProcessesByName("Terraria").OrderByDescending(ProcessStartTime).FirstOrDefault();
						if (proc != null)
							break;
					}

					if (proc == null)
					{
						Console.Error.WriteLine("等待 Terraria 进程超时。");
						return 1;
					}

					startedByLoader = true;
				}

				if (existing.Count > 1)
					Console.WriteLine("提示：当前有 " + existing.Count + " 个 Terraria 进程，已选最晚启动的 PID=" + proc.Id + "。");

				Console.WriteLine(startedByLoader
					? "已由本程序启动游戏，准备注入 PID=" + proc.Id + "。"
					: "检测到泰拉已在运行（未执行启动）。将向现有进程注入 PID=" + proc.Id + "。若要无视旧进程并新开泰拉请使用: TerrariaLoader.exe --launch [Terraria.exe路径]");
			}

			if (forceLaunch && startedByLoader)
				Console.WriteLine("已定位新进程，准备注入 PID=" + proc.Id + "。");

			var startedNewForWait = startedByLoader;
			try
			{
				WaitForInjectionReadiness(ref proc, startedNewForWait, forceLaunch);

				RebindIfTargetExited(ref proc, forceLaunch);

				try
				{
					Console.WriteLine("进程路径: " + proc.MainModule.FileName);
				}
				catch (Exception)
				{
					Console.WriteLine("（无法读取进程路径，可尝试以管理员身份运行 TerrariaLoader。）");
				}

				InjectWithRetries(proc.Id, injectDll);

				Console.WriteLine("已注入 " + proc.Id + "，请在桌面查看 TerrariaPatchInjector-ok.txt 或 CreateWandPatch / Harmony 日志。");
				return 0;
			}
			catch (OutOfMemoryException oom) when (oom.Message.Contains("Code: 5"))
			{
				Console.Error.WriteLine("注入失败（Code 5 / ACCESS_DENIED）。");
				Console.Error.WriteLine("完整信息: " + oom.Message);
				Console.Error.WriteLine("常见原因：1) 该进程已注入过 → 彻底退出泰拉再试；2) 后台仍有 Terraria.exe → 任务管理器结束全部；");
				Console.Error.WriteLine("3) 权限不一致 → 用「以管理员身份运行」启动 TerrariaLoader，或与游戏同一权限；4) 杀毒/防护拦截注入。");
				return 1;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				return 1;
			}
		}
	}
}
