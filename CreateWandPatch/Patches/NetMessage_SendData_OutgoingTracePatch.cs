using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using HarmonyLib;
using Terraria;
using Terraria.Localization;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 联机客户端记录发往服务器的 <see cref="Terraria.NetMessage.SendData"/>。
	/// 部分路径（含部分 WorldGen 铺砖）走 <see cref="Terraria.NetMessage.TrySendData"/>，见 <see cref="NetMessage_TrySendData_OutgoingTracePatch"/>。
	/// </summary>
	[HarmonyPatch(typeof(NetMessage), nameof(NetMessage.SendData), new[]
	{
		typeof(int), typeof(int), typeof(int), typeof(NetworkText), typeof(int), typeof(float), typeof(float),
		typeof(float), typeof(int), typeof(int), typeof(int)
	})]
	public static class NetMessage_SendData_OutgoingTracePatch
	{
		public static void Postfix(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number,
			float number2, float number3, float number4, int number5, int number6, int number7)
		{
			CreateWandMpMsg17Probe.RegisterFromOutgoing(msgType, number, number2, number3, "SendData");

			if (Main.netMode != 1 || !CreateWandSelectionState.EnableClientOutgoingNetTrace)
				return;

			if (CreateWandSelectionState.OutgoingNetTraceTileRelatedOnly &&
			    !CreateWandNetTraceCommon.IsTileRelatedPacketType(msgType))
				return;

			string name = CreateWandNetTraceCommon.GetMessageIdName(msgType);
			CreateWandOutgoingNetTrace.Write(
				"SendData msgType=" + msgType + " (" + name + ") remoteClient=" + remoteClient + " ignoreClient=" + ignoreClient +
				" number=" + number + " n2=" + number2.ToString("R") + " n3=" + number3.ToString("R") + " n4=" + number4.ToString("R") +
				" n5=" + number5 + " n6=" + number6 + " n7=" + number7 +
				(text != null && text != NetworkText.Empty ? " text=" + text : ""));
		}
	}
}
