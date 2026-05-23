namespace CreateWandPatch.Content
{
	public enum CreateWandBlueprintSource
	{
		PngDataMap,
		QotStruct,
		CwMap
	}

	/// <summary>蓝图库条目：包含来源语义与默认联机安全数据。</summary>
	public sealed class CreateWandBlueprintEntry
	{
		public string Name { get; }
		public BuildingData Data { get; }
		public CreateWandBlueprintSource Source { get; }
		public BuildingData PreciseData { get; }

		public bool HasPreciseVariant => PreciseData != null;

		public CreateWandBlueprintEntry(string name, BuildingData data, CreateWandBlueprintSource source, BuildingData preciseData = null)
		{
			Name = name;
			Data = data;
			Source = source;
			PreciseData = preciseData;
		}
	}
}
