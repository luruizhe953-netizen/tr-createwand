using System;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.GameContent;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 短时把快捷栏换成「真实物块」并调用 <see cref="Player.PlaceThing"/>，走与手持泥土/石块相同的放置链（内部仍会 <c>SendData(17,...)</c>）。
	/// 仍受 <see cref="Player.IsInTileInteractionRange"/> 等同原版限制；解析不出物品 ID 时由调用方回退 <see cref="CreateWandPlacementService"/> 的 TileManipulation。
	/// </summary>
	internal static class CreateWandVanillaItemPlaceSim
	{
		// Raise local placement reach during simulated PlaceThing calls.
		// This is restored immediately after each attempt.
		private const int ExperimentalPlaceThingBlockRange = 320;

		private static readonly FieldInfo SmartCursorLockedContinuity =
			AccessTools.Field(typeof(SmartCursorHelper), "_lockedContinuityCoords");
		private static readonly FieldInfo SmartCursorLockedDirection =
			AccessTools.Field(typeof(SmartCursorHelper), "_lockedDesiredDirection");

		private static int ApplyExperimentalPlaceThingReachBoost(Player player)
		{
			if (player == null)
				return 0;
			int saveBlockRange = player.blockRange;
			if (player.blockRange < ExperimentalPlaceThingBlockRange)
				player.blockRange = ExperimentalPlaceThingBlockRange;
			return saveBlockRange;
		}

		/// <summary>与 <see cref="CreateWandPlacementService.TryGetTemplateItemIdForPlaceThing"/> 同语义的墙：默认木墙模板等。</summary>
		public static bool TryPlaceWallAsIfHoldingWallItem(Terraria.Player player, int x, int y, int wallItemTypeId)
		{
			if (Main.netMode != 1 || player == null || !player.active || player.whoAmI != Main.myPlayer)
				return false;

			int saveTx = Player.tileTargetX;
			int saveTy = Player.tileTargetY;
			int saveSmartX = Main.SmartCursorX;
			int saveSmartY = Main.SmartCursorY;
			bool saveSmartShowing = Main.SmartCursorShowing;
			object saveContinuityLock = SmartCursorLockedContinuity?.GetValue(null);
			object saveDirectionLock = SmartCursorLockedDirection?.GetValue(null);
			int slot = player.selectedItem;
			Item saveItem = player.inventory[slot].Clone();
			int saveAnim = player.itemAnimation;
			int saveTime = player.itemTime;
			bool saveUse = player.controlUseItem;
			int saveBlockRange = ApplyExperimentalPlaceThingReachBoost(player);

			var held = new Item();
			held.SetDefaults(wallItemTypeId, null);
			if (held.createWall <= 0)
				return false;
			int expectWall = held.createWall;

			try
			{
				Player.tileTargetX = x;
				Player.tileTargetY = y;
				Main.SmartCursorX = x;
				Main.SmartCursorY = y;
				Main.SmartCursorShowing = true;
				SmartCursorLockedContinuity?.SetValue(null, null);
				SmartCursorLockedDirection?.SetValue(null, null);
				held.stack = Math.Max(held.stack, 999);
				player.inventory[slot] = held;
				player.itemTime = 0;
				player.itemAnimation = Math.Max(1, held.useAnimation > 0 ? held.useAnimation : 15);
				player.controlUseItem = true;

				var ctx = default(Player.ItemCheckContext);
				player.PlaceThing(true, ref ctx);

				Tile t = new Tile(y * Main.maxTilesX + x);
				return t != null && t.wall == expectWall;
			}
			catch
			{
				return false;
			}
			finally
			{
				Player.tileTargetX = saveTx;
				Player.tileTargetY = saveTy;
				Main.SmartCursorX = saveSmartX;
				Main.SmartCursorY = saveSmartY;
				Main.SmartCursorShowing = saveSmartShowing;
				SmartCursorLockedContinuity?.SetValue(null, saveContinuityLock);
				SmartCursorLockedDirection?.SetValue(null, saveDirectionLock);
				player.inventory[slot] = saveItem;
				player.itemAnimation = saveAnim;
				player.itemTime = saveTime;
				player.controlUseItem = saveUse;
				player.blockRange = saveBlockRange;
			}
		}

		/// <returns>若本地目标格已变为期望物块类型则 true（表示 <c>PlaceThing_Tiles</c> 已走通；联机 17 由原版发出）。</returns>
		public static bool TryPlaceTileAsIfHoldingBlockItem(Terraria.Player player, int x, int y, int itemTypeToHold)
		{
			if (Main.netMode != 1 || player == null || !player.active || player.whoAmI != Main.myPlayer)
				return false;

			int saveTx = Player.tileTargetX;
			int saveTy = Player.tileTargetY;
			int saveSmartX = Main.SmartCursorX;
			int saveSmartY = Main.SmartCursorY;
			bool saveSmartShowing = Main.SmartCursorShowing;
			object saveContinuityLock = SmartCursorLockedContinuity?.GetValue(null);
			object saveDirectionLock = SmartCursorLockedDirection?.GetValue(null);
			int slot = player.selectedItem;
			Item saveItem = player.inventory[slot].Clone();
			int saveAnim = player.itemAnimation;
			int saveTime = player.itemTime;
			bool saveUse = player.controlUseItem;
			int saveBlockRange = ApplyExperimentalPlaceThingReachBoost(player);

			try
			{
				var held = new Item();
				held.SetDefaults(itemTypeToHold, null);
				if (held.createTile < 0)
					return false;

				int expectTile = held.createTile;
				Player.tileTargetX = x;
				Player.tileTargetY = y;
				// PlaceThing_Tiles 要求 SmartCursorHelper.TileTargetDesired()：若「连续放置」锁住了坐标，
				// 须让 tileTarget 与 Main.SmartCursorX/Y 一致且 SmartCursorShowing，否则放置链不执行。
				Main.SmartCursorX = x;
				Main.SmartCursorY = y;
				Main.SmartCursorShowing = true;
				// 沿线填充等会写入 _lockedContinuityCoords；锁在未对齐格子上时仅对齐 X/Y 仍可能不满足内部连续性判定。
				// SmartCursorLookup 在 (!controlUseItem || !SmartCursorIsUsed) 时会清空两锁；此处模拟同等「干净」状态。
				SmartCursorLockedContinuity?.SetValue(null, null);
				SmartCursorLockedDirection?.SetValue(null, null);
				held.stack = Math.Max(held.stack, 999);
				player.inventory[slot] = held;
				player.itemTime = 0;
				player.itemAnimation = Math.Max(1, held.useAnimation > 0 ? held.useAnimation : 15);
				player.controlUseItem = true;

				var ctx = default(Player.ItemCheckContext);
				player.PlaceThing(true, ref ctx);

				Tile t = new Tile(y * Main.maxTilesX + x);
				return t != null && t.active() && t.type == expectTile;
			}
			catch
			{
				return false;
			}
			finally
			{
				Player.tileTargetX = saveTx;
				Player.tileTargetY = saveTy;
				Main.SmartCursorX = saveSmartX;
				Main.SmartCursorY = saveSmartY;
				Main.SmartCursorShowing = saveSmartShowing;
				SmartCursorLockedContinuity?.SetValue(null, saveContinuityLock);
				SmartCursorLockedDirection?.SetValue(null, saveDirectionLock);
				player.inventory[slot] = saveItem;
				player.itemAnimation = saveAnim;
				player.itemTime = saveTime;
				player.controlUseItem = saveUse;
				player.blockRange = saveBlockRange;
			}
		}

		/// <summary>旅途/模拟：临时手持材料执行回调（含额外发包），再还原快捷栏。</summary>
		internal static bool TryExecuteWhileHoldingItemAt(Terraria.Player player, int x, int y, int itemTypeToHold,
			Func<bool> whileHolding)
		{
			if (Main.netMode != 1 || player == null || !player.active || player.whoAmI != Main.myPlayer || whileHolding == null)
				return false;

			int saveTx = Player.tileTargetX;
			int saveTy = Player.tileTargetY;
			int saveSmartX = Main.SmartCursorX;
			int saveSmartY = Main.SmartCursorY;
			bool saveSmartShowing = Main.SmartCursorShowing;
			object saveContinuityLock = SmartCursorLockedContinuity?.GetValue(null);
			object saveDirectionLock = SmartCursorLockedDirection?.GetValue(null);
			int slot = player.selectedItem;
			Item saveItem = player.inventory[slot].Clone();
			int saveAnim = player.itemAnimation;
			int saveTime = player.itemTime;
			bool saveUse = player.controlUseItem;
			int saveBlockRange = ApplyExperimentalPlaceThingReachBoost(player);

			try
			{
				var held = new Item();
				held.SetDefaults(itemTypeToHold, null);
				if (held.createTile < 0)
					return false;

				PreparePlaceThingTarget(player, x, y, held);
				return whileHolding();
			}
			catch
			{
				return false;
			}
			finally
			{
				Player.tileTargetX = saveTx;
				Player.tileTargetY = saveTy;
				Main.SmartCursorX = saveSmartX;
				Main.SmartCursorY = saveSmartY;
				Main.SmartCursorShowing = saveSmartShowing;
				SmartCursorLockedContinuity?.SetValue(null, saveContinuityLock);
				SmartCursorLockedDirection?.SetValue(null, saveDirectionLock);
				player.inventory[slot] = saveItem;
				player.itemAnimation = saveAnim;
				player.itemTime = saveTime;
				player.controlUseItem = saveUse;
				player.blockRange = saveBlockRange;
			}
		}

		internal static void InvokePlaceThingAt(Terraria.Player player, int x, int y)
		{
			Player.tileTargetX = x;
			Player.tileTargetY = y;
			Main.SmartCursorX = x;
			Main.SmartCursorY = y;
			Main.SmartCursorShowing = true;
			SmartCursorLockedContinuity?.SetValue(null, null);
			SmartCursorLockedDirection?.SetValue(null, null);
			player.itemTime = 0;
			Item held = player.inventory[player.selectedItem];
			player.itemAnimation = Math.Max(1, held != null && held.useAnimation > 0 ? held.useAnimation : 15);
			player.controlUseItem = true;
			var ctx = default(Player.ItemCheckContext);
			player.PlaceThing(true, ref ctx);
		}

		private static void PreparePlaceThingTarget(Terraria.Player player, int x, int y, Item held)
		{
			held.stack = Math.Max(held.stack, 999);
			player.inventory[player.selectedItem] = held;
			player.itemTime = 0;
			player.itemAnimation = Math.Max(1, held.useAnimation > 0 ? held.useAnimation : 15);
			player.controlUseItem = true;
			Player.tileTargetX = x;
			Player.tileTargetY = y;
			Main.SmartCursorX = x;
			Main.SmartCursorY = y;
			Main.SmartCursorShowing = true;
			SmartCursorLockedContinuity?.SetValue(null, null);
			SmartCursorLockedDirection?.SetValue(null, null);
		}

		/// <summary>
		/// 不替换快捷栏：假定 <see cref="Player.selectedItem"/> 上已是能铺目标格的物品（如从背包换入的真实堆叠），
		/// 只设光标并 <c>PlaceThing</c>。用于非旅途联机「用真材料」路径。
		/// </summary>
		internal static bool TryPlaceTileAtTargetUsingCurrentHeld(Terraria.Player player, int x, int y)
		{
			if (Main.netMode != 1 || player == null || !player.active || player.whoAmI != Main.myPlayer)
				return false;

			int slot = player.selectedItem;
			Item held = player.inventory[slot];
			if (held == null || held.IsAir || held.createTile < 0)
				return false;

			int expectTile = held.createTile;

			int saveTx = Player.tileTargetX;
			int saveTy = Player.tileTargetY;
			int saveSmartX = Main.SmartCursorX;
			int saveSmartY = Main.SmartCursorY;
			bool saveSmartShowing = Main.SmartCursorShowing;
			object saveContinuityLock = SmartCursorLockedContinuity?.GetValue(null);
			object saveDirectionLock = SmartCursorLockedDirection?.GetValue(null);
			int saveAnim = player.itemAnimation;
			int saveTime = player.itemTime;
			bool saveUse = player.controlUseItem;
			int saveBlockRange = ApplyExperimentalPlaceThingReachBoost(player);

			try
			{
				PreparePlaceThingTarget(player, x, y, held);
				InvokePlaceThingAt(player, x, y);
				Tile t = new Tile(y * Main.maxTilesX + x);
				return t != null && t.active() && t.type == expectTile;
			}
			catch
			{
				return false;
			}
			finally
			{
				Player.tileTargetX = saveTx;
				Player.tileTargetY = saveTy;
				Main.SmartCursorX = saveSmartX;
				Main.SmartCursorY = saveSmartY;
				Main.SmartCursorShowing = saveSmartShowing;
				SmartCursorLockedContinuity?.SetValue(null, saveContinuityLock);
				SmartCursorLockedDirection?.SetValue(null, saveDirectionLock);
				player.itemAnimation = saveAnim;
				player.itemTime = saveTime;
				player.controlUseItem = saveUse;
				player.blockRange = saveBlockRange;
			}
		}

		internal static bool TryPlaceWallAtTargetUsingCurrentHeld(Terraria.Player player, int x, int y)
		{
			if (Main.netMode != 1 || player == null || !player.active || player.whoAmI != Main.myPlayer)
				return false;

			int slot = player.selectedItem;
			Item held = player.inventory[slot];
			if (held == null || held.IsAir || held.createWall <= 0)
				return false;

			int expectWall = held.createWall;

			int saveTx = Player.tileTargetX;
			int saveTy = Player.tileTargetY;
			int saveSmartX = Main.SmartCursorX;
			int saveSmartY = Main.SmartCursorY;
			bool saveSmartShowing = Main.SmartCursorShowing;
			object saveContinuityLock = SmartCursorLockedContinuity?.GetValue(null);
			object saveDirectionLock = SmartCursorLockedDirection?.GetValue(null);
			int saveAnim = player.itemAnimation;
			int saveTime = player.itemTime;
			bool saveUse = player.controlUseItem;
			int saveBlockRange = ApplyExperimentalPlaceThingReachBoost(player);

			try
			{
				Player.tileTargetX = x;
				Player.tileTargetY = y;
				Main.SmartCursorX = x;
				Main.SmartCursorY = y;
				Main.SmartCursorShowing = true;
				SmartCursorLockedContinuity?.SetValue(null, null);
				SmartCursorLockedDirection?.SetValue(null, null);
				player.itemTime = 0;
				player.itemAnimation = Math.Max(1, held.useAnimation > 0 ? held.useAnimation : 15);
				player.controlUseItem = true;

				var ctx = default(Player.ItemCheckContext);
				player.PlaceThing(true, ref ctx);

				Tile t = new Tile(y * Main.maxTilesX + x);
				return t != null && t.wall == expectWall;
			}
			catch
			{
				return false;
			}
			finally
			{
				Player.tileTargetX = saveTx;
				Player.tileTargetY = saveTy;
				Main.SmartCursorX = saveSmartX;
				Main.SmartCursorY = saveSmartY;
				Main.SmartCursorShowing = saveSmartShowing;
				SmartCursorLockedContinuity?.SetValue(null, saveContinuityLock);
				SmartCursorLockedDirection?.SetValue(null, saveDirectionLock);
				player.itemAnimation = saveAnim;
				player.itemTime = saveTime;
				player.controlUseItem = saveUse;
				player.blockRange = saveBlockRange;
			}
		}
	}
}
