using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using HarmonyLib;
using Terraria;
using Terraria.Localization;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 1.4.x 部分图格操作走 <see cref="NetMessage.TrySendData"/> 而非 <see cref="NetMessage.SendData"/>；与
	/// <see cref="NetMessage_SendData_OutgoingTracePatch"/> 同内容，避免 outgoing 里缺 msg17 PlaceTile。
	/// </summary>
	[HarmonyPatch(typeof(NetMessage), nameof(NetMessage.TrySendData), new[]
	{
		typeof(int), typeof(int), typeof(int), typeof(NetworkText), typeof(int), typeof(float), typeof(float),
		typeof(float), typeof(int), typeof(int), typeof(int)
	})]
	public static class NetMessage_TrySendData_OutgoingTracePatch
	{
		public static void Postfix(bool __result, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number,
			float number2, float number3, float number4, int number5, int number6, int number7)
		{
			if (__result)
				CreateWandMpMsg17Probe.RegisterFromOutgoing(msgType, number, number2, number3, "TrySendData");

			if (Main.netMode != 1 || !CreateWandSelectionState.EnableClientOutgoingNetTrace)
				return;

			if (CreateWandSelectionState.OutgoingNetTraceTileRelatedOnly &&
			    !CreateWandNetTraceCommon.IsTileRelatedPacketType(msgType))
				return;

			string name = CreateWandNetTraceCommon.GetMessageIdName(msgType);
			CreateWandOutgoingNetTrace.Write(
				"TrySendData ok=" + __result + " msgType=" + msgType + " (" + name + ") remoteClient=" + remoteClient +
				" ignoreClient=" + ignoreClient +
				" number=" + number + " n2=" + number2.ToString("R") + " n3=" + number3.ToString("R") + " n4=" + number4.ToString("R") +
				" n5=" + number5 + " n6=" + number6 + " n7=" + number7 +
				(text != null && text != NetworkText.Empty ? " text=" + text : ""));
		}
	}
}
