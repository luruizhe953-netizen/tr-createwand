using Terraria;

namespace CreateWandPatch.Gameplay
{
	internal enum MpPlacementFailureReason
	{
		None,
		IncompatibleWithServerLibrary,
		NoSupportedRequestRoute
	}

	internal readonly struct MpPlacementDispatchResult
	{
		public bool Handled { get; }
		public bool Success { get; }
		public bool SuppressSuccessSound { get; }
		public MpPlacementFailureReason FailureReason { get; }

		public MpPlacementDispatchResult(bool handled, bool success, bool suppressSuccessSound, MpPlacementFailureReason failureReason)
		{
			Handled = handled;
			Success = success;
			SuppressSuccessSound = suppressSuccessSound;
			FailureReason = failureReason;
		}
	}

	/// <summary>可选调度入口：先尝试反射 <see cref="CreateWandVanillaMpBridge"/>（原版 CreateWandServer）；补丁主路径不再依赖显式 17 铺砖 / 20 整区。</summary>
	internal static class CreateWandMpPlacementOrchestrator
	{
		public static MpPlacementDispatchResult TryDispatch(Player player, byte kind, byte preset, int datamapIndex,
			short reachX, short reachY, short originX, short originY)
		{
			if (Main.netMode != 1)
				return new MpPlacementDispatchResult(false, false, false, MpPlacementFailureReason.None);

			if (CreateWandVanillaMpBridge.TryInvokeOfficialPlace(player, kind, preset, datamapIndex, reachX, reachY, originX, originY,
				    out bool incompatibleWithUnmodifiedServer))
				return new MpPlacementDispatchResult(true, true, true, MpPlacementFailureReason.None);

			if (incompatibleWithUnmodifiedServer)
				return new MpPlacementDispatchResult(false, false, false, MpPlacementFailureReason.IncompatibleWithServerLibrary);

			return new MpPlacementDispatchResult(false, false, false, MpPlacementFailureReason.NoSupportedRequestRoute);
		}
	}
}
