using System;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 与原版创造魔杖逻辑对齐（反射解析类型）：
	/// <list type="bullet">
	/// <item><description>联机客户端 <c>Main.netMode == 1</c>：反射调用原版 <c>CreateWandServer.TryAuthoritativePlace</c>（与打包坐标一致），内部 <c>PlaceBuildingAuthoritative</c> 逐格同步；**原版服只处理常规掘/放**。</description></item>
	/// <item><description>仅当 TerrariaServer 进程也注入本补丁时：<c>Main.netMode == 2</c> 可走 <c>TryAuthoritativePlace</c>（与注入服调试相关；与「只注入客户端连原版服」无关）。</description></item>
	/// </list>
	/// </summary>
	internal static class CreateWandVanillaMpBridge
	{
		private static readonly Assembly TerrariaAsm = typeof(Main).Assembly;
		private static readonly Type TCreateWandServer = TerrariaAsm.GetType("Terraria.GameContent.CreateWandServer");
		private static readonly Type TCreateWandLibrary = TerrariaAsm.GetType("Terraria.GameContent.CreateWandLibrary");

		private static readonly MethodInfo OfficialTryAuthoritativePlace =
			TCreateWandServer?.GetMethod("TryAuthoritativePlace", BindingFlags.NonPublic | BindingFlags.Static, null,
				new[]
				{
					typeof(Player), typeof(byte), typeof(byte), typeof(int), typeof(short), typeof(short), typeof(short),
					typeof(short)
				}, null);

		private static readonly MethodInfo CreateWandLibraryEnsureReload =
			TCreateWandLibrary?.GetMethod("EnsureReload", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes,
				null);

		private static readonly FieldInfo LibraryEntries =
			TCreateWandLibrary?.GetField("Entries", BindingFlags.Public | BindingFlags.Static);

		public static bool CanUseOfficialEntry =>
			OfficialTryAuthoritativePlace != null && CreateWandLibraryEnsureReload != null && LibraryEntries != null;

		public static bool CanUseOfficialDedicatedEntry => CanUseOfficialEntry;

		/// <summary>
		/// 联机客户端：直接调用原版 <c>CreateWandServer.TryAuthoritativePlace</c>（与专用服一致），
		/// 不再经过 <c>TryPlaceFreeFromLocalPlayer</c>（其会再次要求 <c>MouseLeft</c>，在 Harmony 前缀链里常被判定为未按下导致静默失败）。
		/// </summary>
		public static bool TryInvokeOfficialPlace(Player player, byte kind, byte preset, int patchDatamapIndex,
			short reachX, short reachY, short originX, short originY, out bool incompatibleWithUnmodifiedServer)
		{
			incompatibleWithUnmodifiedServer = false;
			if (Main.netMode != 1 || !CanUseOfficialEntry)
				return false;

			return TryRunVanillaAuthoritativePlace(player, kind, preset, patchDatamapIndex, reachX, reachY, originX, originY,
				reportFailureToPlayer: true, out incompatibleWithUnmodifiedServer);
		}

		private static bool TryRunVanillaAuthoritativePlace(Player player, byte kind, byte preset, int patchDatamapIndex,
			short reachX, short reachY, short originX, short originY, bool reportFailureToPlayer,
			out bool incompatibleWithUnmodifiedServer)
		{
			incompatibleWithUnmodifiedServer = false;
			CreateWandLibraryEnsureReload.Invoke(null, null);

			int dm = patchDatamapIndex;
			if (kind == 1)
			{
				CreateWandPngLibrary.EnsureReload();
				if (patchDatamapIndex < 0 || patchDatamapIndex >= CreateWandPngLibrary.Entries.Count)
					return false;

				string wantedName = CreateWandPngLibrary.Entries[patchDatamapIndex].Name;
				BuildingData blueprintData = CreateWandPngLibrary.Entries[patchDatamapIndex].Data;
				if (!TryResolveVanillaDatamapIndexAfterAutoPng(player, wantedName, blueprintData, reportFailureToPlayer,
					    out int vanillaIdx, out incompatibleWithUnmodifiedServer))
					return false;

				dm = vanillaIdx;
			}

			try
			{
				object ret = OfficialTryAuthoritativePlace.Invoke(null,
					new object[] { player, kind, preset, dm, reachX, reachY, originX, originY });
				return ret is bool ok && ok;
			}
			catch
			{
				return false;
			}
		}

		private static int ResolveVanillaDatamapIndexByName(string nameStem)
		{
			object entriesObj = LibraryEntries?.GetValue(null);
			if (!(entriesObj is System.Collections.IList list))
				return -1;

			for (int i = 0; i < list.Count; i++)
			{
				object tuple = list[i];
				if (tuple == null)
					continue;
				Type tt = tuple.GetType();
				FieldInfo item1 = tt.GetField("Item1");
				object nameVal = item1 != null ? item1.GetValue(tuple) : tt.GetProperty("Name")?.GetValue(tuple);
				if (nameVal is string s && string.Equals(s, nameStem, StringComparison.OrdinalIgnoreCase))
					return i;
			}

			return -1;
		}

		private static bool TryResolveVanillaDatamapIndexAfterAutoPng(Player player, string wantedName, BuildingData blueprintData,
			bool reportFailureToPlayer, out int vanillaIdx, out bool incompatibleWithUnmodifiedServer)
		{
			incompatibleWithUnmodifiedServer = false;
			vanillaIdx = ResolveVanillaDatamapIndexByName(wantedName);
			if (vanillaIdx >= 0)
				return true;

			string sanitizedStem = CreateWandVanillaLibrarySync.SanitizeBlueprintFileStem(wantedName);
			vanillaIdx = ResolveVanillaDatamapIndexByName(sanitizedStem);
			if (vanillaIdx >= 0)
				return true;

			string stemWritten;
			string writeErr;
			if (blueprintData == null)
			{
				incompatibleWithUnmodifiedServer = true;
				if (reportFailureToPlayer && player != null && !Main.dedServ)
					ReportVanillaDatamapResolutionFailed(player, "蓝图数据无效");
				return false;
			}

			if (!CreateWandVanillaLibrarySync.TryWriteVanillaPreviewPng(wantedName, blueprintData, out stemWritten, out writeErr))
			{
				incompatibleWithUnmodifiedServer = true;
				if (reportFailureToPlayer && player != null && !Main.dedServ)
					ReportVanillaDatamapResolutionFailed(player, writeErr);
				return false;
			}

			CreateWandLibraryEnsureReload.Invoke(null, null);
			vanillaIdx = ResolveVanillaDatamapIndexByName(stemWritten);
			if (vanillaIdx >= 0)
			{
				if (reportFailureToPlayer && player != null && !Main.dedServ)
					Terraria.CombatText.NewText(player.getRect(), Color.LightGreen,
						"[魔杖] 已为原版库生成预览 PNG（" + stemWritten + ".png），可连原版服铺设。", false, false);
				return true;
			}

			incompatibleWithUnmodifiedServer = true;
			if (reportFailureToPlayer && player != null && !Main.dedServ)
				ReportVanillaDatamapResolutionFailed(player, null);
			return false;
		}

		private static void ReportVanillaDatamapResolutionFailed(Player player, string writeErrDetail)
		{
			string extra = string.IsNullOrEmpty(writeErrDetail) ? "" : " (" + writeErrDetail + ")";
			Terraria.CombatText.NewText(player.getRect(), Color.OrangeRed,
				"[魔杖] 无法进入本机原版 CreateWand 库：已尝试写入预览 PNG 仍失败" + extra +
				"。请检查 Documents\\My Games\\Terraria\\CreateWand\\ 是否可写。**连原版服只需本机有对应蓝图数据；服端无需补丁**。", false, false);
		}

		/// <summary>
		/// 专用服 / 主机服进程内：走原版 <c>TryAuthoritativePlace</c>（内部使用 <c>CreateWandLibrary</c> PNG、
		/// <c>NetMessage.SendTileSquare</c>），与未修改服处理客户端合法放置时一致；不依赖 <c>TryPlaceFreeFromLocalPlayer</c>（其需键鼠与 <c>Main.myPlayer</c>）。
		/// </summary>
		public static bool TryInvokeOfficialDedicatedPlace(Player player, byte kind, byte preset, int datamapIndex,
			short reachX, short reachY, short originX, short originY, out bool incompatibleWithUnmodifiedServer)
		{
			incompatibleWithUnmodifiedServer = false;
			if (Main.netMode != 2 || !CanUseOfficialDedicatedEntry)
				return false;

			if (!player.active || player.dead)
				return false;

			return TryRunVanillaAuthoritativePlace(player, kind, preset, datamapIndex, reachX, reachY, originX, originY,
				reportFailureToPlayer: false, out incompatibleWithUnmodifiedServer);
		}
	}
}
