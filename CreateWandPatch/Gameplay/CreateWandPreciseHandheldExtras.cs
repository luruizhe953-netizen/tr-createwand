using System;
using CreateWandPatch.Content;
using Terraria;
using Terraria.ID;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 精确蓝图：电路/斜砖/染色/液体走与物块相同的「背包取物 → 换持 → 原版放置/敲击」链，不用整区 CopyFrom/msg20。
	/// </summary>
	internal static class CreateWandPreciseHandheldExtras
	{
		/// <summary>简体中文「漆刷」：给物块/家具涂色（<see cref="ItemID.Paintbrush"/>）。</summary>
		private const int TilePaintToolItemId = ItemID.Paintbrush;

		/// <summary>简体中文「涂漆滚刷」：给墙体涂色（<see cref="ItemID.PaintRoller"/>）。</summary>
		private const int WallPaintToolItemId = ItemID.PaintRoller;

		internal static void BuildHandheldExtraIndices(BuildingData data, out int[] indices) =>
			CreateWandBlueprintPlacementOrder.BuildHandheldExtraIndicesBottomUp(data, out indices);

		/// <summary>主阶段（墙/砖）完成后调用：手持铺电路、敲斜砖/半砖、喷漆。</summary>
		internal static void TryApplyHandheldExtras(Terraria.Player player, BuildingData data, int originX, int originY,
			int w, int i)
		{
			Tile src = data.GetPreciseTileOrNull(i);
			if (src == null)
				return;
			int tx = originX + i % w;
			int ty = originY + i / w;
			if (!CreateWandPlacementService.IsInWorldTile(tx, ty))
				return;

			TryApplyHammerShape(player, tx, ty, src);
			TryApplyWiring(player, tx, ty, src);
			TryApplyPaint(player, tx, ty, src);
		}

		/// <summary>液体阶段：手持对应桶倒出（发 sendWater / 本地 Liquid）。</summary>
		internal static void TryApplyLiquidViaBucket(Terraria.Player player, int tx, int ty, Tile src)
		{
			if (src == null || src.liquid == 0 || !CreateWandPlacementService.IsInWorldTile(tx, ty))
				return;

			int bucketId = GetBucketItemIdForLiquid(src);
			if (bucketId <= 0)
				return;

			CreateWandSurvivalRemoteCompat.TryEnsureMaterialForMp(player, bucketId);
			CreateWandSurvivalRemoteCompat.TryExecuteHandheldTool(player, tx, ty, bucketId,
				() => PourLiquidAt(tx, ty, src));
		}

		private static bool TryApplyHammerShape(Terraria.Player player, int tx, int ty, Tile src)
		{
			byte targetSlope = src.slope();
			bool wantHalf = src.halfBrick();
			if (targetSlope == 0 && !wantHalf)
				return true;

			Tile dest = Main.tile[tx, ty];
			if (dest == null || !dest.active())
				return false;

			CreateWandSurvivalRemoteCompat.TryEnsureMaterialForMp(player, ItemID.IronHammer);
			return CreateWandSurvivalRemoteCompat.TryExecuteHandheldTool(player, tx, ty, ItemID.IronHammer,
				() => ApplyHammerShapeAt(tx, ty, targetSlope, wantHalf, dest, src));
		}

		private static bool ApplyHammerShapeAt(int tx, int ty, byte targetSlope, bool wantHalf, Tile dest, Tile src)
		{
			if (wantHalf && !dest.halfBrick())
			{
				if (!WorldGen.PoundTile(tx, ty))
					dest.halfBrick(true);
				if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
					NetMessage.SendData((int)MessageID.TileManipulation, -1, -1, null, 7, tx, ty, 1f);
			}
			else if (!wantHalf && dest.halfBrick())
			{
				WorldGen.PoundTile(tx, ty);
				if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
					NetMessage.SendData((int)MessageID.TileManipulation, -1, -1, null, 7, tx, ty, 0f);
			}

			// 勿用 SlopeTile(noEffects:false)：会 KillTile，左斜等易与相邻帧重算冲突。
			if (dest.slope() != targetSlope)
			{
				dest.slope(targetSlope);
				if (!CreateWandPlacementService.DeferSquareTileFrameDuringPreciseBlueprint)
					WorldGen.SquareTileFrame(tx, ty, true);
				else
					WorldGen.TileFrame(tx, ty, true, false);

				if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
					NetMessage.SendData((int)MessageID.TileManipulation, -1, -1, null, 14, tx, ty, targetSlope);
			}

			dest = Main.tile[tx, ty];
			return dest != null && dest.slope() == targetSlope && dest.halfBrick() == wantHalf;
		}

		private static bool TryApplyWiring(Terraria.Player player, int tx, int ty, Tile src)
		{
			bool any = src.wire() || src.wire2() || src.wire3() || src.wire4();
			if (!any && !src.actuator())
				return true;

			bool ok = true;
			if (src.wire())
				ok &= TryPlaceWireChannel(player, tx, ty, ItemID.Wrench, ItemID.Wire,
					() => WorldGen.PlaceWire(tx, ty), 5);
			if (src.wire2())
				ok &= TryPlaceWireChannel(player, tx, ty, ItemID.BlueWrench, ItemID.Wire,
					() => WorldGen.PlaceWire2(tx, ty), 10);
			if (src.wire3())
				ok &= TryPlaceWireChannel(player, tx, ty, ItemID.GreenWrench, ItemID.Wire,
					() => WorldGen.PlaceWire3(tx, ty), 12);
			if (src.wire4())
				ok &= TryPlaceWireChannel(player, tx, ty, ItemID.YellowWrench, ItemID.Wire,
					() => WorldGen.PlaceWire4(tx, ty), 11);

			if (src.actuator() && !Main.tile[tx, ty].actuator())
				ok &= TryActuateAt(player, tx, ty);

			if (src.inActive() != Main.tile[tx, ty].inActive())
				ok &= TrySetInactiveAt(player, tx, ty, src.inActive());

			return ok;
		}

		private static bool TryPlaceWireChannel(Terraria.Player player, int x, int y, int wrenchId, int wireItemId,
			Func<bool> place, int msg17Action)
		{
			Tile t = Main.tile[x, y];
			if (t == null)
				return false;
			if ((msg17Action == 5 && t.wire()) || (msg17Action == 10 && t.wire2()) || (msg17Action == 12 && t.wire3()) ||
			    (msg17Action == 11 && t.wire4()))
				return true;

			CreateWandSurvivalRemoteCompat.TryEnsureMaterialForMp(player, wireItemId);

			return CreateWandSurvivalRemoteCompat.TryExecuteHandheldTool(player, x, y, wrenchId, () =>
			{
				if (!place())
					return false;
				if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
					NetMessage.SendData((int)MessageID.TileManipulation, -1, -1, null, msg17Action, x, y);
				CreateWandMpDebugLog.Write("diag handheld wire action=" + msg17Action + " at " + x + "," + y +
				                           " wrench=" + wrenchId + " wireItem=" + wireItemId);
				return true;
			});
		}

		private static bool TryActuateAt(Terraria.Player player, int x, int y)
		{
			return CreateWandSurvivalRemoteCompat.TryExecuteHandheldTool(player, x, y, ItemID.ActuationRod, () =>
			{
				if (!Wiring.Actuate(x, y))
					return false;
				if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
					NetMessage.SendData((int)MessageID.TileManipulation, -1, -1, null, 19, x, y);
				return Main.tile[x, y].actuator();
			});
		}

		private static bool TrySetInactiveAt(Terraria.Player player, int x, int y, bool inactive)
		{
			Tile t = Main.tile[x, y];
			if (t == null || t.inActive() == inactive)
				return true;
			t.inActive(inactive);
			if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
				NetMessage.SendData((int)MessageID.TileManipulation, -1, -1, null, 18, x, y, inactive ? 1f : 0f);
			return true;
		}

		private static bool TryApplyPaint(Terraria.Player player, int tx, int ty, Tile src)
		{
			byte tileColor = src.color();
			byte wallColor = src.wallColor();
			if (tileColor == 0 && wallColor == 0)
				return true;

			bool ok = true;
			if (tileColor > 0)
				ok &= TryPaintTileHeld(player, tx, ty, tileColor);
			if (wallColor > 0)
				ok &= TryPaintWallHeld(player, tx, ty, wallColor);
			return ok;
		}

		private static bool TryPaintTileHeld(Terraria.Player player, int x, int y, byte color)
		{
			Tile t = Main.tile[x, y];
			if (t == null || !t.active() || t.color() == color)
				return true;

			if (!TryEnsurePaintKitInInventory(player, color, forWall: false))
				return false;

			return CreateWandSurvivalRemoteCompat.TryExecuteHandheldTool(player, x, y, TilePaintToolItemId, () =>
			{
				bool broadcast = Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet;
				if (!WorldGen.paintTile(x, y, color, broadcast, false))
					return false;
				TryConsumePaintByColor(player, color);
				return true;
			});
		}

		private static bool TryPaintWallHeld(Terraria.Player player, int x, int y, byte color)
		{
			Tile t = Main.tile[x, y];
			if (t == null || t.wall == 0 || t.wallColor() == color)
				return true;

			if (!TryEnsurePaintKitInInventory(player, color, forWall: true))
				return false;

			return CreateWandSurvivalRemoteCompat.TryExecuteHandheldTool(player, x, y, WallPaintToolItemId, () =>
			{
				bool broadcast = Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet;
				if (!WorldGen.paintWall(x, y, color, broadcast, false))
					return false;
				TryConsumePaintByColor(player, color);
				return true;
			});
		}

		/// <summary>涂刷前：物块用漆刷、墙用涂漆滚刷（各 1）+ 对应色染料桶（消耗物，自动补 999）。</summary>
		private static bool TryEnsurePaintKitInInventory(Terraria.Player player, byte color, bool forWall)
		{
			if (player == null || color == 0)
				return false;

			int toolId = forWall ? WallPaintToolItemId : TilePaintToolItemId;
			CreateWandSurvivalRemoteCompat.TryEnsureMaterialForMp(player, toolId);

			if (FindInventorySlotWithPaintColor(player, color) >= 0)
				return FindFirstInventorySlotWithItemType(player, toolId) >= 0;

			if (!TryResolvePaintItemId(color, out int paintItemId))
				return false;

			CreateWandSurvivalRemoteCompat.TryEnsureMaterialForMp(player, paintItemId);
			return FindInventorySlotWithPaintColor(player, color) >= 0
			       && FindFirstInventorySlotWithItemType(player, toolId) >= 0;
		}

		private static int FindFirstInventorySlotWithItemType(Terraria.Player player, int itemTypeId)
		{
			for (int i = 0; i < player.inventory.Length && i < 58; i++)
			{
				Item it = player.inventory[i];
				if (it != null && !it.IsAir && it.type == itemTypeId && it.stack > 0)
					return i;
			}

			return -1;
		}

		private static int FindInventorySlotWithPaintColor(Terraria.Player player, byte color)
		{
			for (int i = 0; i < player.inventory.Length && i < 58; i++)
			{
				Item it = player.inventory[i];
				if (it != null && !it.IsAir && it.stack > 0 && it.paint == color)
					return i;
			}

			return -1;
		}

		private static void TryConsumePaintByColor(Terraria.Player player, byte color)
		{
			int slot = FindInventorySlotWithPaintColor(player, color);
			if (slot < 0)
				return;
			Item it = player.inventory[slot];
			it.stack--;
			if (it.stack <= 0)
				it.TurnToAir(false);
			CreateWandSurvivalRemoteCompat.SyncInventorySlotToServerIfMp(player, slot);
		}

		/// <summary>瓦片 <see cref="Tile.color"/> → 对应染料桶 ItemID（与 Item.SetDefaults 中 paint 字段一致）。</summary>
		private static bool TryResolvePaintItemId(byte color, out int itemId)
		{
			itemId = 0;
			if (color == 0)
				return false;

			if (color >= PaintID.RedPaint && color <= PaintID.GrayPaint)
			{
				itemId = ItemID.RedPaint + (color - PaintID.RedPaint);
				return true;
			}

			switch (color)
			{
				case PaintID.BrownPaint:
					itemId = ItemID.BrownPaint;
					return true;
				case PaintID.ShadowPaint:
					itemId = ItemID.ShadowPaint;
					return true;
				case PaintID.NegativePaint:
					itemId = ItemID.NegativePaint;
					return true;
				case PaintID.IlluminantPaint:
					itemId = ItemID.GlowPaint;
					return true;
				default:
					return false;
			}
		}

		private static bool PourLiquidAt(int x, int y, Tile src)
		{
			Tile dest = Main.tile[x, y];
			if (dest == null)
				return false;

			dest.liquidType(src.liquidType());
			dest.liquid = src.liquid;
			dest.lava(src.lava());
			dest.honey(src.honey());
			dest.shimmer(src.shimmer());
			WorldGen.SquareTileFrame(x, y, true);
			if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
				NetMessage.sendWater(x, y);
			else
				Liquid.AddWater(x, y);

			CreateWandMpDebugLog.Write("diag handheld liquid at " + x + "," + y + " amount=" + src.liquid +
			                           " type=" + src.liquidType());
			return dest.liquid > 0;
		}

		private static int GetBucketItemIdForLiquid(Tile src)
		{
			if (src.lava())
				return ItemID.LavaBucket;
			if (src.honey())
				return ItemID.HoneyBucket;
			if (src.shimmer())
				return ItemID.BottomlessShimmerBucket;
			if (src.liquidType() == 1)
				return ItemID.LavaBucket;
			if (src.liquidType() == 2)
				return ItemID.HoneyBucket;
			if (src.liquidType() == 3)
				return ItemID.BottomlessShimmerBucket;
			return ItemID.WaterBucket;
		}
	}
}
