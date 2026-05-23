using System.Collections.Generic;
using System.Reflection;
using Terraria.ID;

namespace CreateWandPatch.Gameplay
{
	/// <summary>发包/收包追踪共用的 MessageID 名称与「仅图格」过滤。</summary>
	internal static class CreateWandNetTraceCommon
	{
		private static readonly Dictionary<int, string> MessageIdFieldNames = BuildMessageIdFieldNames();

		private static Dictionary<int, string> BuildMessageIdFieldNames()
		{
			var d = new Dictionary<int, string>();
			foreach (var f in typeof(MessageID).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (f.FieldType != typeof(int))
					continue;
				int v = (int)f.GetValue(null);
				if (d.TryGetValue(v, out string prev))
					d[v] = prev + "|" + f.Name;
				else
					d[v] = f.Name;
			}

			return d;
		}

		public static string GetMessageIdName(int msgType) =>
			MessageIdFieldNames.TryGetValue(msgType, out string name) ? name : "MsgId_" + msgType;

		public static bool IsTileRelatedPacketType(int msgType)
		{
			switch (msgType)
			{
				case MessageID.TileManipulation:
				case MessageID.AreaTileChange:
				case MessageID.ToggleDoorState:
				case MessageID.PlaceObject:
					return true;
				default:
					return false;
			}
		}
	}
}
