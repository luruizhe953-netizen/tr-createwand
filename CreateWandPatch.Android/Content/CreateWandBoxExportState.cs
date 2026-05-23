



namespace CreateWandPatch.Content

{

	public enum CreateWandBoxExportPhase : byte

	{

		Inactive,

		WaitingFirstCorner,

		WaitingSecondCorner

	}



	/// <summary>手持魔杖时：两点左键框选世界区域并导出为 CreateWand 目录下的 PNG 蓝图。</summary>

	public static class CreateWandBoxExportState

	{

		public static CreateWandBoxExportPhase Phase = CreateWandBoxExportPhase.Inactive;

		public static Point FirstCorner;



		public static void Cancel()

		{

			Phase = CreateWandBoxExportPhase.Inactive;

		}



		public static void Begin()

		{

			Phase = CreateWandBoxExportPhase.WaitingFirstCorner;

		}

	}

}


