namespace CreateWandPatch.Content
{
	/// <summary>与反编译/ImproveGame 对齐的创造魔杖物品 ID（需在 SetDefaults 中覆盖为可用物品）。</summary>
	public static class CreateWandIds
	{
		/// <summary>与原版 ItemID.CreateWand 一致；属性与贴图由游戏内 SetDefaults / TextureAssets 提供。</summary>
		public const int ItemType = 6147;

		/// <summary>当 6147 在某环境下被置为空气时，用原版物品做数值与贴图模板再改回 type。</summary>
		public const int FallbackTemplateItem = 213;
	}
}
