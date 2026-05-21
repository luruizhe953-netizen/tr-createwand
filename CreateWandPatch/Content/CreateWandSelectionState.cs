using System.Text;
using Terraria;

namespace CreateWandPatch.Content
{
	public enum BlueprintKind : byte
	{
		BuiltinPreset,
		DataMap
	}

	public enum PlacePreset : byte
	{
		Stone3x3,
		WoodPlatform5,
		Dirt2x2
	}

		/// <summary>放置前清空区域模式。</summary>
		public enum ClearAreaMode : byte
		{
			None,
			Staggered,
			Fast
		}

	/// <summary>
	/// 联机客户端放置策略（热键 N 列表，共 3 种）。Kill 一般为原版 <c>MessageID.TileManipulation</c> 拆格。
	/// </summary>
	public enum MpClientPlacementMode : byte
	{
		/// <summary>只改本机 <c>Main.tile</c>。</summary>
		LocalOnlyNoNet = 0,

		/// <summary>
		/// 先 <c>PlaceThing</c>；失败则 <see cref="Terraria.WorldGen.PlaceTile"/>/<c>PlaceWall</c> 联机发包（msg17），无 79/20。
		/// </summary>
		VanillaHandheldThenExplicitMsg17 = 1,

		/// <summary>仅删除（KillTile/KillWall），不尝试放置。用于远距清场。</summary>
		DeleteOnly = 2
	}

	/// <summary>
	/// 当前选中的铺设模式（无 UI 时用热键切换，见 <see cref="Patches.Main_Update_CreateWandHotkeysPatch"/>）。
	/// 放置不消耗背包物块（旅模式式）；若需生存扣料应另作「按格扣物品」逻辑。
	/// </summary>
	public static class CreateWandSelectionState
	{
		public const int MpPlacementModeCount = 3;

		public static BlueprintKind SelectedKind = BlueprintKind.BuiltinPreset;
		public static PlacePreset SelectedPreset = PlacePreset.Stone3x3;
		public static int SelectedDatamapIndex;

		/// <summary>左键放置前是否先清空该矩形内的物块与墙（默认开）。按 ] 或面板内按钮切换。</summary>
		public static ClearAreaMode ClearAreaBeforePlaceMode = ClearAreaMode.Fast;

		/// <summary>向后兼容：None 以外视为清空开启。</summary>
		public static bool ClearAreaBeforePlace => ClearAreaBeforePlaceMode != ClearAreaMode.None;

		/// <summary>清空模式为 Fast 时使用一键全清。</summary>
		public static bool UseFastClearBeforePlace => ClearAreaBeforePlaceMode == ClearAreaMode.Fast;

		/// <summary>] 键循环：None → Staggered → Fast → None。</summary>
		public static void NextClearAreaMode()
		{
			ClearAreaBeforePlaceMode = ClearAreaBeforePlaceMode switch
			{
				ClearAreaMode.None => ClearAreaMode.Staggered,
				ClearAreaMode.Staggered => ClearAreaMode.Fast,
				ClearAreaMode.Fast => ClearAreaMode.None,
				_ => ClearAreaMode.Fast
			};
		}

		public static string GetClearAreaModeLabel() => ClearAreaBeforePlaceMode switch
		{
			ClearAreaMode.None => "关",
			ClearAreaMode.Staggered => "逐格",
			ClearAreaMode.Fast => "一键",
			_ => "?"
		};

		/// <summary>重复放置次数（防服务器回滚）。默认 1。按 O 循环：1→2→3→5→10→1。</summary>
		public static int PlacementRepeatCount = 1;

		/// <summary>R 键循环重复次数。</summary>
		public static void NextPlacementRepeatCount()
		{
			PlacementRepeatCount = PlacementRepeatCount switch
			{
				1 => 2,
				2 => 3,
				3 => 5,
				5 => 10,
				_ => 1
			};
		}

		/// <summary>创造魔杖放置总开关（默认开）。关闭后仅保留预览，不执行实际放置。</summary>
		public static bool PlacementEnabled = true;

		/// <summary><c>false</c> = 快速整帧铺设（无视原版放置距离）；<c>true</c> = 逐格分帧（受原版互动距离限制）。按 [ 切换。
		/// 默认 <c>true</c>：联机首帧即逐格；单机在 <c>ApplyDefaultPlacementPaceByNetMode</c> 中每帧置为 <c>false</c>（快速）。</summary>
		public static bool UseStaggeredPlacement = true;

		/// <summary>
		/// 仅 <c>Main.netMode == 1</c> 且 <see cref="UseStaggeredPlacement"/> 时生效：为 <c>true</c> 时「能否本次铺设」按快速模式距离判定（全图），
		/// 仍保持逐格分帧；不改变 <c>]</c> 清区、<see cref="ClearAreaBeforePlace"/> 触发的 Kill 包语义（仍先 Kill 再 Place）。
		/// 默认开：否则常见「光标附近 ≠ reach 参考格」导致整次不放、日志 <c>reach check failed</c>。
		/// </summary>
		public static bool MpStaggeredUnlimitedReach = true;

		/// <summary>
		/// 联机客户端：为 true 时即便超出原版互动距离也允许继续尝试发包（是否生效由服端裁决）。
		/// 仅影响客户端“是否提前跳过”，不修改包结构与服端判定。
		/// </summary>
		public static bool MpAllowOutOfRangePlacementAttempts = true;

		/// <summary>
		/// 联机且<strong>非旅途</strong>：优先用背包里<strong>真实数量</strong>的对应物块/墙物品走 <c>PlaceThing</c>（与原版生存服校验一致）。
		/// 关闭则仍用临时 999 堆叠模拟（旅途/单机 behavior 不变）。
		/// </summary>
		public static bool MpSurvivalInventoryPlaceFirst = true;

		/// <summary>
		/// 生存联机放置：当背包内对应材料耗尽时，若玩家当前打开了箱子，尝试从该箱自动补到背包再继续放置。
		/// </summary>
		public static bool MpAutoRefillFromOpenedChest = true;

		/// <summary>
		/// 联机：在已开启 <see cref="EnableMpTileRollbackTrace"/> 且近期检测到图格「闪回」或「观测期内未稳定为期望」时，
		/// 对同一格的 Kill/Place、WorldGen 铺砖/墙、生存与手持 <c>PlaceThing</c> 使用的<strong>最大重复次数</strong>（含首次），默认 3，clamp 1–10。
		/// 未触发上述检测或未开闪回追踪时每个操作只执行一次，避免常态刷屏发包。
		/// </summary>
		public static int MpTileOperationRepeatCount = 3;

		/// <summary>
		/// 联机：当 <see cref="MpClientPlacementMode.VanillaHandheldThenExplicitMsg17"/> 下生存 <c>PlaceThing</c> 与「临时手持」均失败时，
		/// <strong>不再</strong>回退 <c>WorldGen.PlaceTile</c>。默认 <c>false</c>（仍回退 WG），否则常见整片只有 <c>manual-like skip</c>、砖叠不上；
		/// 若你愿意接受「失败格完全不铺」以强制对齐纯手点，可改为 <c>true</c>。
		/// </summary>
		public static bool MpManualLikeSkipWorldGenFallback = false;

		/// <summary>
		/// 联机：蓝图/预设逐格队列<strong>结束后</strong>是否 <c>SendTileSquare</c> 整区同步。
		/// 默认 <c>false</c>：未改动的 TShock 会拒绝大块/非白名单 msg20（&gt;4×4 或 matches），guest 无 <c>tshock.ignore.sendtilesquare</c> 时易断线。
		/// 仅在你给账号开了该权限后再改为 <c>true</c>。
		/// </summary>
		public static bool MpRegionSyncAfterStaggeredBlueprint = false;

		/// <summary>联机客户端策略，默认「手持·失败同逐格」。按 <c>N</c> 打开列表：<see cref="MpClientPlacementMode"/>。</summary>
		public static MpClientPlacementMode MpPlacementMode = MpClientPlacementMode.VanillaHandheldThenExplicitMsg17;

		private static MpClientPlacementMode ClampMpPlacementMode(MpClientPlacementMode m)
		{
			switch (m)
			{
				case MpClientPlacementMode.LocalOnlyNoNet:
				case MpClientPlacementMode.VanillaHandheldThenExplicitMsg17:
				case MpClientPlacementMode.DeleteOnly:
					return m;
				default:
					return MpClientPlacementMode.LocalOnlyNoNet;
			}
		}

		/// <summary>将旧版 DLL 存下来的无效枚举值纠正为「仅本地」。</summary>
		public static void NormalizeMpPlacementMode()
		{
			MpPlacementMode = ClampMpPlacementMode(MpPlacementMode);
		}

		public static bool MpLocalOnlyNoNet =>
			ClampMpPlacementMode(MpPlacementMode) == MpClientPlacementMode.LocalOnlyNoNet;

		/// <summary><see cref="MpClientPlacementMode.VanillaHandheldThenExplicitMsg17"/>：手持链失败时显式 msg17 / 延迟 WorldGen 补发。</summary>
		public static bool MpVanillaHandheldThenExplicitMsg17 =>
			ClampMpPlacementMode(MpPlacementMode) == MpClientPlacementMode.VanillaHandheldThenExplicitMsg17;

		/// <summary>仅删除，不放置。</summary>
		public static bool MpDeleteOnly =>
			ClampMpPlacementMode(MpPlacementMode) == MpClientPlacementMode.DeleteOnly;

		/// <summary>
		/// 联机且非仅本地、非只删除：走生存/临时手持 <c>PlaceThing</c> 优先链。
		/// </summary>
		public static bool MpPreferVanillaHeldItemPlace =>
			Main.netMode == 1 && !MpLocalOnlyNoNet && !MpDeleteOnly;

		public static bool HasManualPaceChoice;

		/// <summary>
		/// 为 true 时魔杖联机放置动作写入文本日志（魔杖代码路径内显式调用的发包），便于对照 <c>MessageID</c>。
		/// 默认开。路径优先：环境变量 <c>CREATEWAND_MP_LOG_PATH</c> 或 <c>CREATEWAND_MP_LOG_DIR</c>；否则
		/// <c>My Games\Terraria\CreateWand\CreateWandPatch-mp.log</c>；或 DLL 旁 <c>logs\CreateWandPatch-mp.log</c>。单文件上限 20KB（保留最新尾部）。
		/// 关为 false 可省 IO。
		/// </summary>
		public static bool EnableMpActionTrace = true;

		/// <summary>
		/// 联机客户端为 <c>true</c> 时，经 Harmony 记录：<c>NetMessage.SendData</c>（上行）与 <c>MessageBuffer.GetData</c>（下行）；
		/// 普通手持铺砖亦走 SendData，与魔杖无关。
		/// 默认关。日志：<c>CreateWandPatch-outgoing-net.log</c>、<c>CreateWandPatch-incoming-net.log</c>（各 20KB 上限）
		/// （或 <c>CREATEWAND_OUTGOING_*</c> / <c>CREATEWAND_INCOMING_*</c> 环境变量）。
		/// 按 **F9** 切换（不需持有魔杖）。
		/// </summary>
		public static bool EnableClientOutgoingNetTrace;

		/// <summary>配合 <see cref="EnableClientOutgoingNetTrace"/>：<c>true</c> 仅记录图格相关类型（17 / 19 / 20 / 79）；<c>false</c> 记录全部 MessageID。默认 <c>false</c> 便于排查；联机 <c>Shift+F9</c> 可切回仅图格（减体积）。</summary>
		public static bool OutgoingNetTraceTileRelatedOnly;

		/// <summary>
		/// 联机：监测魔杖成功写入的坐标是否在短期内被撤销（「闪回」）。与 <see cref="EnableMpActionTrace"/> 无关；
		/// 日志前缀 <c>TileRollback</c> 写入同一 <c>CreateWandPatch-mp.log</c>。默认关；联机 <b>Ctrl+F9</b> 切换。
		/// 开启后才会根据闪回/未稳定信号提升 <see cref="MpTileOperationRepeatCount"/> 的实际使用（见该字段说明）。
		/// </summary>
		public static bool EnableMpTileRollbackTrace;

		/// <summary>开启后：凡条目含 <c>PreciseData</c>（cwmap、qotstruct 解析、或与 PNG 同 stem 同尺寸的 .cwmap）走 <c>PreciseCopy</c>。默认开；<c>;</c> 或面板可关。</summary>
		public static bool EnablePreciseCwmapPlacement = true;

		/// <summary>蓝图/预设铺设队列正常结束后，移除本次 <c>TryAutoGrantMaterial</c> 补进背包的铺材堆叠。</summary>
		public static bool AutoRemoveAutoGrantedMaterialsAfterBlueprint = true;

		/// <summary>
		/// 精确蓝图收尾 <see cref="Gameplay.CreateWandPlacementService.FinalizePreciseBlueprintRegion"/> 后，
		/// 分块 msg20 把斜砖/染色/线路同步到服端（重进游戏才不丢）。需 <c>tshock.ignore.sendtilesquare</c> 或管理员；默认关。
		/// </summary>
		public static bool MpSyncPreciseTilesAfterBlueprint;

		public static bool UseStaggeredPlacementEffective => UseStaggeredPlacement;

		public static void ToggleStaggeredPlacement()
		{
			UseStaggeredPlacement = !UseStaggeredPlacement;
			HasManualPaceChoice = true;
		}

		public static void CycleMpPlacementMode()
		{
			NormalizeMpPlacementMode();
			MpPlacementMode = (MpClientPlacementMode)((((byte)MpPlacementMode) + 1) % MpPlacementModeCount);
		}

		public static string GetMpPlacementModeShortLabel() => GetMpPlacementModeShortLabel(MpPlacementMode);

		public static string GetMpPlacementModeShortLabel(MpClientPlacementMode m) =>
			ClampMpPlacementMode(m) switch
			{
				MpClientPlacementMode.LocalOnlyNoNet => "仅本地",
				MpClientPlacementMode.VanillaHandheldThenExplicitMsg17 => "手持·失败同逐格",
				MpClientPlacementMode.DeleteOnly => "只删除",
				_ => "?"
			};

		public static string GetMpPlacementModeHintLong(MpClientPlacementMode m) =>
			ClampMpPlacementMode(m) switch
			{
				MpClientPlacementMode.LocalOnlyNoNet =>
					"[魔杖] 仅本机改图",
				MpClientPlacementMode.VanillaHandheldThenExplicitMsg17 =>
					"[魔杖] 先手持 PlaceThing；失败仍回 WorldGen（可调 MpManualLikeSkipWorldGenFallback）；格间帧见队列常量",
				MpClientPlacementMode.DeleteOnly =>
					"[魔杖] 仅删除（KillTile/KillWall），不放置；适合远距清场",
				_ => ""
			};

		/// <summary>N 面板内「与 <c>CreateWandPatch-outgoing-net.log</c> 同写法」的标题。</summary>
		public static string GetMpPlacementOutgoingReferenceTitle() => "SendData 写法（与 outgoing-net.log 一致）";

		/// <summary>热键 N 打开的面板中展示的 MessageID 对照全文。</summary>
		public static string GetMpPlacementOutgoingReferenceBody()
		{
			var sb = new StringBuilder();
			sb.AppendLine("原版手持铺一格：核心 SendData msgType=17 (TileManipulation) number=1 PlaceTile，n2/n3 坐标，n4 物块类型；");
			sb.AppendLine("同帧其它 msg（如 5/13/16…）多为背包/周期同步，非放置本体。");
			sb.AppendLine("number：0=KillTile，1=PlaceTile，2=KillWall，3=PlaceWall");
			sb.AppendLine("魔杖模式→主要涉及：");
			for (int i = 0; i < MpPlacementModeCount; i++)
			{
				var m = (MpClientPlacementMode)i;
				sb.Append(GetMpPlacementModeShortLabel(m)).Append(" → ").AppendLine(GetMpPlacementModeOutgoingOneLine(m));
			}

			sb.AppendLine("F9：开关上行 SendData + 下行 GetData 日志（outgoing-net / incoming-net；默认记全部 msgType）");
			sb.AppendLine("Shift+F9：在「全部」与「仅 17/19/20/79」之间切换（开日志时减刷屏）");
			sb.AppendLine("Ctrl+F9：开关「物块/墙闪回」检测（TileRollback → CreateWandPatch-mp.log）；开启后近期闪回才会多次重试发包");
			return sb.ToString();
		}

		private static string GetMpPlacementModeOutgoingOneLine(MpClientPlacementMode m) =>
			ClampMpPlacementMode(m) switch
			{
				MpClientPlacementMode.LocalOnlyNoNet =>
					"放置不发 SendData；清区静默（无 msg17）",
				MpClientPlacementMode.VanillaHandheldThenExplicitMsg17 =>
					"成：PlaceThing；败：WorldGen（除非 MpManualLikeSkipWorldGenFallback）；格间帧见队列常量",
				MpClientPlacementMode.DeleteOnly =>
					"仅发 msg17 KillTile/KillWall，不发 PlaceTile/PlaceWall",
				_ => ""
			};
	}
}
