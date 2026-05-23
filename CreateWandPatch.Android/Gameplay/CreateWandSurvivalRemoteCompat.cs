using System;
using Microsoft.Xna.Framework.Graphics;
using CreateWandPatch.Content;

using Terraria;
using Terraria.ID;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 联机、非旅途：临时交换快捷栏持材，并用 msg5 SyncEquipment 同步槽位，满足 TShock OnPlaceObject 校验。
	/// </summary>
	internal static class CreateWandSurvivalRemoteCompat
	{
		private const int MaxInventorySearchSlots = 58;
		private const int RestoreWandDelayFrames = 3;
		private const int RestoreWandIdleFrames = 10;

		private struct PendingRestore
		{
			public bool Active;
			public int DueFrame;
			public int PlayerWhoAmI;
			public int WandSelectedSlot;
			public int MaterialSlot;
			public int ExpectedMaterialItemId;
			public Item SavedWand;
		}

		private static long _nextMissingMatHintUtcTicks = long.MinValue;
		private static int _frame;
		private static int _lastSuccessfulPlaceFrame = int.MinValue;
		private static bool _lastQueueBusy;
		private static PendingRestore _pendingRestore;

		private static bool ShouldAttempt =>
			Main.netMode == 1 &&
			CreateWandSelectionState.MpSurvivalInventoryPlaceFirst &&
			!Main.IsJourneyMode;

		private static int FindFirstInventorySlotWithItem(Player player, int itemTypeId)
		{
			// 优先快捷栏 0–9：切 selectedItem + msg13 后服端能立刻读到对应槽位材料
			for (int i = 0; i < 10 && i < player.inventory.Length; i++)
			{
				Item it = player.inventory[i];
				if (it != null && !it.IsAir && it.type == itemTypeId && it.stack > 0)
					return i;
			}

			for (int i = 10; i < player.inventory.Length && i < MaxInventorySearchSlots; i++)
			{
				Item it = player.inventory[i];
				if (it != null && !it.IsAir && it.type == itemTypeId && it.stack > 0)
					return i;
			}

			return -1;
		}

		private static int FindFirstEmptyInventorySlot(Player player)
		{
			for (int i = 0; i < player.inventory.Length && i < MaxInventorySearchSlots; i++)
			{
				Item it = player.inventory[i];
				if (it == null || it.IsAir)
					return i;
			}

			return -1;
		}

		private static readonly System.Collections.Generic.HashSet<int> AutoGrantedInventorySlots =
			new System.Collections.Generic.HashSet<int>();

		/// <summary>联机生存：背包无该物品时自动补一份（线材/锤/桶等手持链用）。</summary>
		internal static void TryEnsureMaterialForMp(Player player, int itemTypeId)
		{
			if (!ShouldAttempt || player == null || itemTypeId <= 0)
				return;
			if (FindFirstInventorySlotWithItem(player, itemTypeId) >= 0)
				return;
			TryAutoGrantMaterial(player, itemTypeId, out _);
		}

		/// <summary>
		/// 电路/锤/漆/桶：真联机走换持+sync5；仅本地 N 或单机直接执行 <paramref name="action"/>。
		/// </summary>
		internal static bool TryExecuteHandheldTool(Terraria.Player player, int placeX, int placeY, int itemTypeId,
			Func<bool> action)
		{
			if (player == null || !player.active || action == null)
				return false;

			if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
				return TryExecuteWhileHoldingPlaceMaterial(player, placeX, placeY, itemTypeId, 1, action);

			return action();
		}

		/// <summary>背包满时：先清本次自动补料槽，再丢弃堆叠≥999 的铺材以腾格。</summary>
		private static bool TryMakeRoomForAutoGrant(Player player)
		{
			if (player == null)
				return false;

			if (AutoGrantedInventorySlots.Count > 0)
			{
				var slots = new int[AutoGrantedInventorySlots.Count];
				AutoGrantedInventorySlots.CopyTo(slots);
				for (int i = 0; i < slots.Length; i++)
				{
					int slot = slots[i];
					if (slot < 0 || slot >= player.inventory.Length)
						continue;
					player.inventory[slot].TurnToAir(false);
					if (Main.netMode == 1)
						SyncInventorySlotToServer(player, slot);
				}

				AutoGrantedInventorySlots.Clear();
				if (FindFirstEmptyInventorySlot(player) >= 0)
					return true;
			}

			for (int slot = 0; slot < player.inventory.Length && slot < MaxInventorySearchSlots; slot++)
			{
				Item it = player.inventory[slot];
				if (it == null || it.IsAir || it.stack < 999)
					continue;
				if (it.createTile < 0 && it.createWall <= 0 && !it.PaintOrCoating && it.type != ItemID.Wire)
					continue;

				CreateWandMpDebugLog.Write("survival MP discard fullStack item=" + it.type + " stack=" + it.stack +
				                           " slot=" + slot + " reason=inventoryFull");
				it.TurnToAir(false);
				if (Main.netMode == 1)
					SyncInventorySlotToServer(player, slot);
				if (FindFirstEmptyInventorySlot(player) >= 0)
					return true;
			}

			return FindFirstEmptyInventorySlot(player) >= 0;
		}

		/// <summary>涂刷工具 1 个；染料桶（PaintOrCoating）999；铺砖等默认 999。</summary>
		private static int ResolveAutoGrantStack(Item granted)
		{
			int maxStack = granted.maxStack > 0 ? granted.maxStack : 1;
			if (granted.PaintOrCoating)
				return Math.Min(maxStack, 999);
			if (IsSingleStackHandheldTool(granted.type))
				return 1;
			return Math.Min(maxStack, 999);
		}

		private static bool IsSingleStackHandheldTool(int itemTypeId) =>
			itemTypeId == ItemID.Paintbrush || itemTypeId == ItemID.PaintRoller
			|| itemTypeId == ItemID.SpectrePaintbrush || itemTypeId == ItemID.SpectrePaintRoller
			|| itemTypeId == ItemID.IronHammer || itemTypeId == ItemID.Wrench
			|| itemTypeId == ItemID.BlueWrench || itemTypeId == ItemID.GreenWrench
			|| itemTypeId == ItemID.YellowWrench || itemTypeId == ItemID.ActuationRod;

		private static bool TryAutoGrantMaterial(Player player, int itemTypeId, out int grantedSlot)
		{
			grantedSlot = -1;
			if (player == null || itemTypeId <= 0)
				return false;

			int emptySlot = FindFirstEmptyInventorySlot(player);
			if (emptySlot < 0 && !TryMakeRoomForAutoGrant(player))
				return false;
			if (emptySlot < 0)
				emptySlot = FindFirstEmptyInventorySlot(player);
			if (emptySlot < 0)
				return false;

			var granted = new Item();
			granted.SetDefaults(itemTypeId);
			if (granted.IsAir)
				return false;

			granted.stack = ResolveAutoGrantStack(granted);
			player.inventory[emptySlot] = granted;
			grantedSlot = emptySlot;
			AutoGrantedInventorySlots.Add(emptySlot);
			SyncInventorySlotToServer(player, emptySlot);

			CreateWandMpDebugLog.Write("survival MP autoGrant item=" + itemTypeId +
			                           " stack=" + granted.stack + " toInvSlot=" + emptySlot);
			return true;
		}

		/// <summary>蓝图/预设队列正常结束后，清掉本次自动补进背包的铺材（默认开）。</summary>
		internal static void TryClearAutoGrantedMaterials(Player player)
		{
			if (!CreateWandSelectionState.AutoRemoveAutoGrantedMaterialsAfterBlueprint || player == null ||
			    AutoGrantedInventorySlots.Count == 0)
				return;

			foreach (int slot in AutoGrantedInventorySlots)
			{
				if (slot < 0 || slot >= player.inventory.Length)
					continue;
				player.inventory[slot].TurnToAir(false);
				if (Main.netMode == 1)
					SyncInventorySlotToServer(player, slot);
			}

			AutoGrantedInventorySlots.Clear();
			CreateWandMpDebugLog.Write("survival MP cleared autoGranted material slots after blueprint");
		}

		private static void MaybeHintMissingMaterial(Player player, int templateItemId)
		{
			long now = DateTime.UtcNow.Ticks;
			if (now < _nextMissingMatHintUtcTicks)
				return;
			_nextMissingMatHintUtcTicks = now + TimeSpan.TicksPerSecond * 12;

			Item probe = new Item();
			probe.SetDefaults(templateItemId);
			string name = probe.Name ?? ("#" + templateItemId);
			CreateWandMpDebugLog.Write("survival MP skipped: no stack of item " + templateItemId + " (" + name + ") in inventory");
			Terraria.CombatText.NewText(player.getRect(), Color.LightCoral,
				"[魔杖] 非旅途联机·生存发包：背包需有「" + name + "」",
				false, false);
		}

		private static bool TryAutoRefillFromOpenedChest(Player player, int itemTypeId)
		{
			if (!CreateWandSelectionState.MpAutoRefillFromOpenedChest || player == null)
				return false;
			int chestId = player.chest;
			if (chestId < 0 || chestId >= Main.maxChests)
				return false;
			Chest chest = Main.chest[chestId];
			if (chest == null || chest.item == null)
				return false;

			for (int ci = 0; ci < chest.item.Length; ci++)
			{
				ChestItem c = chest.item[ci];
				if (c.IsAir || c.type != itemTypeId || c.stack <= 0)
					continue;

				int target = -1;
				for (int i = 0; i < MaxInventorySearchSlots; i++)
				{
					Item inv = player.inventory[i];
					if (inv != null && !inv.IsAir && inv.type == itemTypeId && inv.stack < inv.maxStack)
					{
						target = i;
						break;
					}
				}
				if (target < 0)
				{
					for (int i = 0; i < MaxInventorySearchSlots; i++)
					{
						Item inv = player.inventory[i];
						if (inv == null || inv.IsAir)
						{
							target = i;
							break;
						}
					}
				}
				if (target < 0)
					return false;

				Item to = player.inventory[target];
				if (to == null || to.IsAir)
				{
					player.inventory[target] = ItemFromChestItem(c);
					chest.item[ci].Clear();
					CreateWandMpDebugLog.Write("survival MP autoRefill from chest movedAll item=" + itemTypeId + " toInvSlot=" + target +
					                           " chest=" + chestId + " chestSlot=" + ci);
					return true;
				}

				int canMove = to.maxStack - to.stack;
				if (canMove <= 0)
					continue;
				int moved = Math.Min(canMove, c.stack);
				to.stack += moved;
				c.stack = (short)(c.stack - moved);
				if (c.stack <= 0)
					chest.item[ci].Clear();
				CreateWandMpDebugLog.Write("survival MP autoRefill from chest moved=" + moved + " item=" + itemTypeId +
				                           " toInvSlot=" + target + " chest=" + chestId + " chestSlot=" + ci);
				return moved > 0;
			}

			return false;
		}

		private static void RestoreSlotsAfterPlace(Player player, int sel, int invSlot, int templateItemId, Item saveWand)
		{
			if (player == null || saveWand == null || sel < 0 || sel >= player.inventory.Length ||
			    invSlot < 0 || invSlot >= player.inventory.Length)
				return;

			Item matAfter = player.inventory[sel]?.Clone();
			player.inventory[sel] = saveWand.Clone();

			if (matAfter != null && !matAfter.IsAir && matAfter.type == templateItemId)
			{
				player.inventory[invSlot] = matAfter.Clone();
				return;
			}

			if (matAfter != null && !matAfter.IsAir)
				CreateWandMpDebugLog.Write("survival MP restore: unexpected selected after PlaceThing type=" +
					matAfter.type + " expected item=" + templateItemId);

			player.inventory[invSlot].TurnToAir(false);
			SyncInventorySlotsAfterSwap(player, sel, invSlot);
		}

		internal static void SyncInventorySlotToServerIfMp(Player player, int inventoryIndex)
		{
			if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
				SyncInventorySlotToServer(player, inventoryIndex);
		}

		private static void SyncInventorySlotToServer(Player player, int inventoryIndex)
		{
			if (Main.netMode != 1 || player == null || player.whoAmI != Main.myPlayer)
				return;
			if (inventoryIndex < 0 || inventoryIndex >= player.inventory.Length)
				return;
			NetMessage.SendData((int)MessageID.SyncEquipment, -1, -1, null, player.whoAmI,
				PlayerItemSlotID.Inventory0 + inventoryIndex);
		}

		private static void SyncInventorySlotsAfterSwap(Player player, int slotA, int slotB)
		{
			SyncInventorySlotToServer(player, slotA);
			SyncInventorySlotToServer(player, slotB);
		}

		private static void QueueRestoreSwap(Player player, int wandSlot, int materialSlot, int templateItemId, Item saveWand)
		{
			_lastSuccessfulPlaceFrame = _frame;
			_pendingRestore = new PendingRestore
			{
				Active = true,
				DueFrame = _frame + RestoreWandDelayFrames,
				PlayerWhoAmI = player.whoAmI,
				WandSelectedSlot = wandSlot,
				MaterialSlot = materialSlot,
				ExpectedMaterialItemId = templateItemId,
				SavedWand = saveWand.Clone()
			};
		}

		public static void ProcessFrame()
		{
			_frame++;
			bool queueBusy = CreateWandStaggeredPlacementQueue.IsBusy;
			bool queueJustBecameIdle = _lastQueueBusy && !queueBusy;
			_lastQueueBusy = queueBusy;

			if (!_pendingRestore.Active || _frame < _pendingRestore.DueFrame)
				return;
			if (queueBusy)
				return;
			if (!queueJustBecameIdle && _frame - _lastSuccessfulPlaceFrame < RestoreWandIdleFrames)
				return;

			Player p = Main.LocalPlayer;
			if (p == null || !p.active || p.whoAmI != _pendingRestore.PlayerWhoAmI)
			{
				_pendingRestore.Active = false;
				return;
			}

			if (p.selectedItem != _pendingRestore.WandSelectedSlot)
			{
				CreateWandMpDebugLog.Write("survival MP restore deferred skipped: selected changed curSel=" + p.selectedItem +
				                           " expectedSel=" + _pendingRestore.WandSelectedSlot);
				_pendingRestore.Active = false;
				return;
			}

			RestoreSlotsAfterPlace(p, _pendingRestore.WandSelectedSlot, _pendingRestore.MaterialSlot,
				_pendingRestore.ExpectedMaterialItemId, _pendingRestore.SavedWand);
			CreateWandMpDebugLog.Write("survival MP restore deferred applied delayFrames=" + RestoreWandDelayFrames +
			                           " idleFrames=" + RestoreWandIdleFrames +
			                           " queueJustBecameIdle=" + queueJustBecameIdle +
			                           " sel=" + _pendingRestore.WandSelectedSlot + " invSlot=" + _pendingRestore.MaterialSlot);
			_pendingRestore.Active = false;
		}

		/// <summary>
		/// 逐格队列可用：当前处于「临时持材等待恢复」窗口，不应因 selectedItem 不是魔杖而中断任务。
		/// </summary>
		public static bool IsInTemporaryHeldMaterialWindow(Player player)
		{
			if (player == null || !_pendingRestore.Active)
				return false;
			if (player.whoAmI != _pendingRestore.PlayerWhoAmI)
				return false;
			if (player.selectedItem != _pendingRestore.WandSelectedSlot)
				return false;
			Item held = player.inventory[player.selectedItem];
			return held != null && !held.IsAir && held.type == _pendingRestore.ExpectedMaterialItemId;
		}

		public static bool TryPlaceTile(Player player, int x, int y, int tileType, int style)
		{
			if (!ShouldAttempt)
				return false;
			if (!CreateWandPlacementService.TryGetTemplateItemIdForPlaceThing(tileType, style, out int templateItemId))
				return false;

			int invSlot = FindFirstInventorySlotWithItem(player, templateItemId);
			if (invSlot < 0)
			{
				if (TryAutoGrantMaterial(player, templateItemId, out int grantedSlot))
					invSlot = grantedSlot;
			}
			if (invSlot < 0)
			{
				if (TryAutoRefillFromOpenedChest(player, templateItemId))
					invSlot = FindFirstInventorySlotWithItem(player, templateItemId);
			}
			if (invSlot < 0)
			{
				MaybeHintMissingMaterial(player, templateItemId);
				return false;
			}

			int sel = player.selectedItem;
			if (invSlot == sel)
			{
				CreateWandPlacementService.SyncHeldItemPlaceStyleForTile(player, templateItemId, tileType, style);
				return CreateWandVanillaItemPlaceSim.TryPlaceTileAtTargetUsingCurrentHeld(player, x, y);
			}

			Item saveWand = player.inventory[sel].Clone();

			player.inventory[sel] = player.inventory[invSlot].Clone();
			player.inventory[invSlot] = saveWand.Clone();
			SyncInventorySlotsAfterSwap(player, sel, invSlot);
			LogHeldMaterialForMpTile(player, x, y, templateItemId, tileType, style, "swapped+sync5");
			CreateWandPlacementService.SyncHeldItemPlaceStyleForTile(player, templateItemId, tileType, style);

			bool ok;
			try
			{
				ok = CreateWandVanillaItemPlaceSim.TryPlaceTileAtTargetUsingCurrentHeld(player, x, y);
			}
			finally
			{
				QueueRestoreSwap(player, sel, invSlot, templateItemId, saveWand);
			}

			if (ok)
			{
				CreateWandMpDebugLog.Write(
					"survival MP PlaceThing tile item=" + templateItemId + " fromInvSlot=" + invSlot + " at " + x + "," + y +
					" tileType=" + tileType + " style=" + style);
				CreateWandTileRollbackDetector.RegisterTileExpectation(x, y, tileType, "SurvivalPlaceThing");
			}
			else
			{
				CreateWandMpDebugLog.Write(
					"survival MP PlaceThing tile failed item=" + templateItemId + " fromInvSlot=" + invSlot + " at " + x + "," + y +
					" msg17Recent=" + CreateWandMpMsg17Probe.DescribeRecentPlaceForCell(x, y, false, 200));
			}

			return ok;
		}

		public static bool TryPlaceWall(Player player, int x, int y, int wallType)
		{
			if (!ShouldAttempt)
				return false;
			if (!CreateWandPlacementService.TryResolveWallItemIdForVanillaPlace(wallType, out int wallItemId))
				return false;

			int invSlot = FindFirstInventorySlotWithItem(player, wallItemId);
			if (invSlot < 0)
			{
				if (TryAutoGrantMaterial(player, wallItemId, out int grantedSlot))
					invSlot = grantedSlot;
			}
			if (invSlot < 0)
			{
				if (TryAutoRefillFromOpenedChest(player, wallItemId))
					invSlot = FindFirstInventorySlotWithItem(player, wallItemId);
			}
			if (invSlot < 0)
			{
				MaybeHintMissingMaterial(player, wallItemId);
				return false;
			}

			int sel = player.selectedItem;
			if (invSlot == sel)
				return CreateWandVanillaItemPlaceSim.TryPlaceWallAtTargetUsingCurrentHeld(player, x, y);

			Item saveWand = player.inventory[sel].Clone();

			player.inventory[sel] = player.inventory[invSlot].Clone();
			player.inventory[invSlot] = saveWand.Clone();
			SyncInventorySlotsAfterSwap(player, sel, invSlot);
			LogHeldMaterialForMpTile(player, x, y, wallItemId, -1, wallType, "swapped+sync5");

			bool ok;
			try
			{
				ok = CreateWandVanillaItemPlaceSim.TryPlaceWallAtTargetUsingCurrentHeld(player, x, y);
			}
			finally
			{
				QueueRestoreSwap(player, sel, invSlot, wallItemId, saveWand);
			}

			if (ok)
			{
				CreateWandMpDebugLog.Write(
					"survival MP PlaceThing wall item=" + wallItemId + " fromInvSlot=" + invSlot + " at " + x + "," + y +
					" wallType=" + wallType);
				CreateWandTileRollbackDetector.RegisterWallExpectation(x, y, wallType, "SurvivalPlaceThingWall");
			}

			return ok;
		}

		/// <summary>
		/// 联机：快捷栏临时换成放置材料，执行 <paramref name="whileHolding"/>（可含 PlaceThing + msg79/msg34），
		/// 再延迟换回魔杖。TShock 按服端 <c>SelectedItem.createTile</c> 校验，须与发包一致。
		/// </summary>
		internal static bool TryExecuteWhileHoldingPlaceMaterial(Terraria.Player player, int placeX, int placeY,
			int itemTypeId, int direction, Func<bool> whileHolding)
		{
			if (Main.netMode != 1 || player == null || !player.active || whileHolding == null)
				return false;

			int saveDir = player.direction;
			player.direction = direction;

			try
			{
				if (ShouldAttempt)
				{
					int invSlot = FindFirstInventorySlotWithItem(player, itemTypeId);
					if (invSlot < 0 && !TryAutoGrantMaterial(player, itemTypeId, out invSlot))
					{
						if (TryAutoRefillFromOpenedChest(player, itemTypeId))
							invSlot = FindFirstInventorySlotWithItem(player, itemTypeId);
					}
					if (invSlot < 0)
					{
						MaybeHintMissingMaterial(player, itemTypeId);
						return false;
					}

					int sel = player.selectedItem;
					if (invSlot == sel)
					{
						LogHeldMaterialForMpFurniture(player, placeX, placeY, itemTypeId, "alreadySelected");
						return whileHolding();
					}

					Item saveWand = player.inventory[sel].Clone();
					player.inventory[sel] = player.inventory[invSlot].Clone();
					player.inventory[invSlot] = saveWand.Clone();
					SyncInventorySlotsAfterSwap(player, sel, invSlot);
					try
					{
						LogHeldMaterialForMpFurniture(player, placeX, placeY, itemTypeId, "swapped+sync5");
						return whileHolding();
					}
					finally
					{
						QueueRestoreSwap(player, sel, invSlot, itemTypeId, saveWand);
					}
				}

				if (!CreateWandSelectionState.MpPreferVanillaHeldItemPlace)
					return false;

				return CreateWandVanillaItemPlaceSim.TryExecuteWhileHoldingItemAt(player, placeX, placeY, itemTypeId,
					whileHolding);
			}
			finally
			{
				player.direction = saveDir;
			}
		}

		private static void LogHeldMaterialForMpFurniture(Terraria.Player player, int placeX, int placeY, int itemTypeId,
			string phase)
		{
			if (player == null)
				return;
			Item held = player.inventory[player.selectedItem];
			CreateWandMpDebugLog.Write(
				"diag heldMaterial furniture phase=" + phase + " sel=" + player.selectedItem + " itemTypeId=" + itemTypeId +
				" held.type=" + (held?.type ?? -1) + " createTile=" + (held?.createTile ?? -1) + " placeStyle=" +
				(held?.placeStyle ?? -1) + " at=" + placeX + "," + placeY);
		}

		private static void LogHeldMaterialForMpTile(Terraria.Player player, int placeX, int placeY, int itemTypeId,
			int tileType, int style, string phase)
		{
			if (player == null)
				return;
			Item held = player.inventory[player.selectedItem];
			CreateWandMpDebugLog.Write(
				"diag heldMaterial tile phase=" + phase + " sel=" + player.selectedItem + " itemTypeId=" + itemTypeId +
				" targetTile=" + tileType + " targetStyle=" + style + " held.type=" + (held?.type ?? -1) +
				" held.createTile=" + (held?.createTile ?? -1) + " held.placeStyle=" + (held?.placeStyle ?? -1) +
				" at=" + placeX + "," + placeY);
		}

		/// <summary>家具/工作台等：必须用 <c>PlaceThing</c> → msg79 PlaceObject 或箱子 msg34。</summary>
		public static bool TryPlaceFurnitureItem(Player player, int x, int y, int itemTypeId, int expectTileType, int style)
		{
			if (Main.netMode != 1 || player == null || !player.active)
				return false;

			int invSlot = -1;
			if (ShouldAttempt)
			{
				invSlot = FindFirstInventorySlotWithItem(player, itemTypeId);
				if (invSlot < 0)
				{
					if (TryAutoGrantMaterial(player, itemTypeId, out int grantedSlot))
						invSlot = grantedSlot;
				}
				if (invSlot < 0)
				{
					if (TryAutoRefillFromOpenedChest(player, itemTypeId))
						invSlot = FindFirstInventorySlotWithItem(player, itemTypeId);
				}
				if (invSlot < 0)
				{
					MaybeHintMissingMaterial(player, itemTypeId);
					return false;
				}
			}

			bool ok;
			if (ShouldAttempt && invSlot >= 0)
			{
				int sel = player.selectedItem;
				if (invSlot == sel)
					ok = CreateWandVanillaItemPlaceSim.TryPlaceTileAtTargetUsingCurrentHeld(player, x, y);
				else
				{
					Item saveWand = player.inventory[sel].Clone();
					player.inventory[sel] = player.inventory[invSlot].Clone();
					player.inventory[invSlot] = saveWand.Clone();
					try
					{
						ok = CreateWandVanillaItemPlaceSim.TryPlaceTileAtTargetUsingCurrentHeld(player, x, y);
					}
					finally
					{
						QueueRestoreSwap(player, sel, invSlot, itemTypeId, saveWand);
					}
				}
			}
			else if (CreateWandSelectionState.MpPreferVanillaHeldItemPlace)
				ok = CreateWandVanillaItemPlaceSim.TryPlaceTileAsIfHoldingBlockItem(player, x, y, itemTypeId);
			else
				return false;

			if (ok)
			{
				CreateWandMpDebugLog.Write(
					"survival MP PlaceThing furniture item=" + itemTypeId + " expectTile=" + expectTileType + " at " + x + "," + y);
				CreateWandTileRollbackDetector.RegisterTileExpectation(x, y, expectTileType, "SurvivalPlaceThingFurniture");
			}
			else
				CreateWandMpDebugLog.Write(
					"survival MP PlaceThing furniture failed item=" + itemTypeId + " at " + x + "," + y +
					" msg17Recent=" + CreateWandMpMsg17Probe.DescribeRecentPlaceForCell(x, y, false, 200));

			return ok || CreateWandPlacementService.IsFurnitureTilePlacedAt(x, y, expectTileType, style);
		}

		// IL2CPP: ChestItem struct → Item conversion helper
		private static Item ItemFromChestItem(ChestItem ci)
		{
			var item = new Item();
			item.SetDefaults(ci.type);
			item.stack = ci.stack;
			item.prefix = ci.prefix;
			return item;
		}
	}
}
