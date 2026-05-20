using System;
using System.IO;
using System.Reflection;
using EasyHook;

namespace TerrariaPatchInjector
{
	/// <summary>
	/// EasyHook 把本 DLL 注入后会在一个独立 AppDomain（非 Terraria 主域）内调用 Run()。
	/// 核心策略：通过 AppDomain.nGetDefaultDomain() 拿到 Terraria 的主域，
	/// 把 0Harmony + CreateWandPatch 字节直接 Load 进主域，
	/// 再用 DoCallBack 在主域内调用 Bootstrap.Init()。
	/// 这样 typeof(Main) 等解析到的是真正在运行的类型，Harmony 补丁才能生效。
	/// </summary>
	public class InjectionEntry : IEntryPoint
	{
		public InjectionEntry(RemoteHooking.IContext context) { }

		public void Run(RemoteHooking.IContext context)
		{
			var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			var logPath = Path.Combine(desktop, "TerrariaPatchInjector-ok.txt");
			var errPath = Path.Combine(desktop, "TerrariaPatchInjector-error.txt");

			try
			{
				var injectorDir = Path.GetDirectoryName(typeof(InjectionEntry).Assembly.Location) ?? "";

				// 获取 Terraria 主域：必须用 GetDefaultDomain()。nGetDefaultDomain 是 InternalCall，
				// GetMethod("nGetDefaultDomain") 常为 null，或 Invoke 不受支持；GetDefaultDomain 是托管包装，可反射调用。
				var defaultDomain = GetDefaultAppDomainForBootstrap();

				// 把需要的 DLL 字节预先 Load 进主域，顺序：先依赖项后主包
				string[] preload = { "0Harmony.dll", "TerrariaPatchInjector.dll" };
				foreach (var dll in preload)
				{
					var path = Path.Combine(injectorDir, dll);
					if (File.Exists(path))
						defaultDomain.Load(File.ReadAllBytes(path));
				}

				// 把注入目录写入主域的 AppDomain 数据槽，供回调读取（byte[] 跨域可序列化）
				defaultDomain.SetData("CWP_INJECTOR_DIR", injectorDir);

				// 在主域内执行初始化（DoCallBack 的静态方法需在主域能找到的程序集里）
				defaultDomain.DoCallBack(new CrossAppDomainDelegate(RunInDefaultDomain));

				File.AppendAllText(logPath, DateTime.Now + " Bootstrap.Init() finished.\r\n");
			}
			catch (Exception ex)
			{
				try { File.WriteAllText(errPath, ex.ToString()); } catch { }
				throw;
			}
		}

		static AppDomain GetDefaultAppDomainForBootstrap()
		{
			const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Static;
			var t = typeof(AppDomain);
			MethodInfo m = t.GetMethod("GetDefaultDomain", bf);
			if (m != null)
				return (AppDomain)m.Invoke(null, null);
			// 个别 CLR 版本若仅有 nGetDefaultDomain，再试一次（可能 Invoke 失败）
			m = t.GetMethod("nGetDefaultDomain", bf);
			if (m != null)
				return (AppDomain)m.Invoke(null, null);
			throw new InvalidOperationException(
				"反射无法取得默认 AppDomain：GetDefaultDomain / nGetDefaultDomain 均不可用。");
		}

		/// <summary>
		/// 在 Terraria 主域内运行：注册 AssemblyResolve，加载 CreateWandPatch.dll，调用 Bootstrap.Init()。
		/// </summary>
		public static void RunInDefaultDomain()
		{
			var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			try
			{
				var dir = (string)AppDomain.CurrentDomain.GetData("CWP_INJECTOR_DIR") ?? "";

				// 补充 AssemblyResolve：找不到的 dll 从注入器目录补充
				AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
				{
					try
					{
						var name = new AssemblyName(args.Name).Name;
						foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
							if (a.GetName().Name == name) return a;
						var p = Path.Combine(dir, name + ".dll");
						if (File.Exists(p)) return Assembly.LoadFrom(p);
					}
					catch { }
					return null;
				};

				// 用 Load(bytes) 加载 CreateWandPatch.dll 进主域（使其依赖解析到真实 Terraria 类型）
				var patchBytes = File.ReadAllBytes(Path.Combine(dir, "CreateWandPatch.dll"));
				var patchAsm = Assembly.Load(patchBytes);

				// 通过反射调用 Bootstrap.Init()
				var bootstrapType = patchAsm.GetType("CreateWandPatch.Bootstrap")
					?? throw new InvalidOperationException("找不到 CreateWandPatch.Bootstrap");
				var initMethod = bootstrapType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
					?? throw new InvalidOperationException("找不到 Bootstrap.Init");
				initMethod.Invoke(null, null);
			}
			catch (Exception ex)
			{
				try { File.AppendAllText(Path.Combine(desktop, "CreateWandPatch-harmony.txt"),
					DateTime.Now + " RunInDefaultDomain ERR: " + ex + "\r\n"); } catch { }
				throw;
			}
		}
	}
}
