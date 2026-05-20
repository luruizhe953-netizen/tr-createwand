using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	public enum BlueprintPlacementMode
	{
		LegacySort,
		PreciseCopy
	}

	internal sealed class BlueprintPlacementPlan
	{
		public BlueprintPlacementMode Mode { get; }
		public bool IsDegraded { get; }
		public string DegradeReason { get; }

		public BlueprintPlacementPlan(BlueprintPlacementMode mode, bool isDegraded, string degradeReason)
		{
			Mode = mode;
			IsDegraded = isDegraded;
			DegradeReason = degradeReason;
		}
	}

	/// <summary>蓝图语义层：定义来源能力矩阵与降级规则。</summary>
	internal static class CreateWandBlueprintSemantics
	{
		public static BlueprintPlacementPlan ResolvePlacementPlan(CreateWandBlueprintEntry entry)
		{
			if (entry == null)
				return new BlueprintPlacementPlan(BlueprintPlacementMode.LegacySort, true, "蓝图条目不存在");

			switch (entry.Source)
			{
				case CreateWandBlueprintSource.PngDataMap:
				{
					if (CreateWandSelectionState.EnablePreciseCwmapPlacement && entry.HasPreciseVariant)
						return new BlueprintPlacementPlan(BlueprintPlacementMode.PreciseCopy, false, null);
					return new BlueprintPlacementPlan(BlueprintPlacementMode.LegacySort, true,
						"PNG 为色图分类；同名同尺寸的 .cwmap 侧写可 1:1（须开精确门）");
				}
				case CreateWandBlueprintSource.QotStruct:
				{
					if (CreateWandSelectionState.EnablePreciseCwmapPlacement && entry.HasPreciseVariant)
						return new BlueprintPlacementPlan(BlueprintPlacementMode.PreciseCopy, false, null);
					return new BlueprintPlacementPlan(BlueprintPlacementMode.LegacySort, true,
						"未开精确门：qotstruct 走分类；开精确门后按图格 1:1");
				}
				case CreateWandBlueprintSource.CwMap:
				{
					if (CreateWandSelectionState.EnablePreciseCwmapPlacement && entry.HasPreciseVariant)
						return new BlueprintPlacementPlan(BlueprintPlacementMode.PreciseCopy, false, null);
					return new BlueprintPlacementPlan(BlueprintPlacementMode.LegacySort, true,
						"cwmap 未开精确门或无 PreciseData，走分类模式");
				}
				default:
					return new BlueprintPlacementPlan(BlueprintPlacementMode.LegacySort, true, "未知蓝图类型");
			}
		}

		public static string GetMultiplayerCapabilitySummary(CreateWandBlueprintEntry entry)
		{
			if (entry == null)
				return "不可用";

			switch (entry.Source)
			{
				case CreateWandBlueprintSource.PngDataMap:
					return "PNG 色图=分类；同名同尺寸 .cwmap 侧写 + 精确门 → 1:1";
				case CreateWandBlueprintSource.QotStruct:
					return "qotstruct：精确门 → 本地 1:1；联机 → 瓦片 1:1 手持发包";
				case CreateWandBlueprintSource.CwMap:
					return "cwmap：精确门 → 本地 1:1 复制；联机 → 瓦片 1:1 手持发包+家具阶段";
				default:
					return "不可用";
			}
		}
	}
}
