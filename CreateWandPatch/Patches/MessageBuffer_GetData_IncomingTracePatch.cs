using System.Reflection;
using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using HarmonyLib;
using Terraria;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 联机客户端记录<strong>从服务器收到并解析完成</strong>的封包类型（经 <see cref="Terraria.MessageBuffer.GetData"/>）。
	/// 与 <see cref="NetMessage_SendData_OutgoingTracePatch"/> 共用 F9 开关与 Shift+F9 过滤。
	/// </summary>
	[HarmonyPatch]
	public static class MessageBuffer_GetData_IncomingTracePatch
	{
		private static MethodBase TargetMethod() =>
			AccessTools.Method(typeof(MessageBuffer), nameof(MessageBuffer.GetData), new[]
			{
				typeof(int),
				typeof(int),
				typeof(int).MakeByRefType()
			});

		public static void Postfix(MessageBuffer __instance, int start, int length, int messageType)
		{
			if (Main.netMode != 1 || !CreateWandSelectionState.EnableClientOutgoingNetTrace)
				return;

			if (CreateWandSelectionState.OutgoingNetTraceTileRelatedOnly &&
			    !CreateWandNetTraceCommon.IsTileRelatedPacketType(messageType))
				return;

			string name = CreateWandNetTraceCommon.GetMessageIdName(messageType);
			CreateWandIncomingNetTrace.Write(
				"GetData bufferWhoAmI=" + __instance.whoAmI + " msgType=" + messageType + " (" + name + ") start=" + start +
				" length=" + length + " totalData=" + __instance.totalData + " messageLength=" + __instance.messageLength);
		}
	}
}
