using System;
using CreateWandPatch.Content;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;

namespace CreateWandPatch.Gameplay
{
	public static class CreateWandSinglePlayerService
	{
		private static long _nextMpReachHintUtcTicks = long.MinValue;
		private static bool _loggedVanillaMpBridgeCapability;
		/// <summary>
		/// 与 <see cref="TryPlaceFromLocalPlayer"/> 相同的权威放置流程（含 <c>netMode == 2</c> 时优先原版 <c>CreateWandServer.TryAuthoritativePlace</c>），
		/// 供专用服上的控制台/聊天/自定义逻辑在已得到打包参数时调用；无鼠标检测。
		/// </summary>
		public static bool TryAuthoritativePlaceFromPacked(Terraria.Player player, byte kind, byte preset, int datamapIndex,
			short reachX, short reachY, short originX, short originY, out bool suppressSuccessSound)
		{
			return TryAuthoritativePlace(player, kind, preset, datamapIndex, reachX, reachY, originX, originY,
				out suppressSuccessSound);
		}

		public static void TryPlaceFromLocalPlayer(Terraria.Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return;
			if (!PlayerInput.Triggers.JustPressed.MouseLeft)
				return;
			if (Main.gameMenu || player.noBuilding)
			{
				Terraria.CombatText.NewText(player.getRect(), Color.OrangeRed, "Cannot build here.", false, false);
				return;
			}

			byte kind;
			byte preset;
			int dmIndex;
			short reachX;
			short reachY;
			short originX;
			short originY;
			if (!TryPackLocalSelection(player, out kind, out preset, out dmIndex, out reachX, out reachY, out originX, out originY))
				return;

			if (TryAuthoritativePlace(player, kind, preset, dmIndex, reachX, reachY, originX, originY,
				    out bool suppressSuccessSound) &&
			    !suppressSuccessSound)
				PlayLocalSuccessSound(kind, reachX, reachY);
		}

		private static bool TryPackLocalSelection(Terraria.Player player, out byte kind, out byte preset, out int dmIndex,
			out short reachX, out short reachY, out short originX, out short originY)
		{
			kind = 0;
			preset = 0;
			dmIndex = 0;
			// 与原版 CreateWandServer.TryPackLocalSelection 一致，供原版放置逻辑（及少数改版 SendCreateWandPlaceRequest）校验距离
			reachX = (short)Player.tileTargetX;
			reachY = (short)Player.tileTargetY;
			originX = reachX;
			originY = reachY;
			Point mouseTile = Main.MouseWorld.ToTileCoordinates();

			if (CreateWandSelectionState.SelectedKind != BlueprintKind.DataMap)
			{
				kind = 0;
				preset = (byte)CreateWandSelectionState.SelectedPreset;
				return true;
			}

			CreateWandPngLibrary.EnsureReload();
			if (CreateWandPngLibrary.Entries.Count == 0 ||
			    CreateWandSelectionState.SelectedDatamapIndex < 0 ||
			    CreateWandSelectionState.SelectedDatamapIndex >= CreateWandPngLibrary.Entries.Count)
				return false;

			CreateWandBlueprintEntry entry = CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex];
			BuildingData bd = entry.Data;
			Point origin = new Point(mouseTile.X - bd.Width / 2, mouseTile.Y - bd.Height / 2);
			kind = 1;
			dmIndex = CreateWandSelectionState.SelectedDatamapIndex;
			originX = (short)origin.X;
			originY = (short)origin.Y;
			return true;
		}

		private static bool TryAuthoritativePlace(Terraria.Player player, byte kind, byte preset, int datamapIndex,
			short reachX, short reachY, short originX, short originY, out bool suppressSuccessSound)
		{
			CreateWandMpDebugLog.Write("TryAuthoritativePlace enter kind=" + kind + " preset=" + preset + " dm=" + datamapIndex +
			                          " staggered=" + CreateWandSelectionState.UseStaggeredPlacement + " netMode=" + Main.netMode +
			                          " mpMode=" + CreateWandSelectionState.MpPlacementMode +
			                          " reach=(" + reachX + "," + reachY + ") origin=(" + originX + "," + originY + ")");
			if (Main.netMode == 1 && !_loggedVanillaMpBridgeCapability)
			{
				_loggedVanillaMpBridgeCapability = true;
				CreateWandMpDebugLog.Write(
					"VanillaMpBridge CanUseOfficialEntry=" + CreateWandVanillaMpBridge.CanUseOfficialEntry +
					" (反射 CreateWandServer；主路径为逐格队列+msg20，未接 CreateWandMpPlacementOrchestrator)");
			}

			suppressSuccessSound = false;
			Item item = player.inventory[player.selectedItem];
			if (item.type != CreateWandIds.ItemType)
			{
				CreateWandMpDebugLog.Write("Exit: selected item is not create wand");
				return false;
			}
			if (Main.gameMenu || player.noBuilding)
			{
				CreateWandMpDebugLog.Write("Exit: gameMenu or noBuilding");
				return false;
			}
			if (!CreateWandSelectionState.PlacementEnabled)
			{
				CreateWandMpDebugLog.Write("Exit: placement disabled by user toggle");
				return false;
			}

			if (!WorldGen.InWorld((int)reachX, (int)reachY, 10))
			{
				CreateWandMpDebugLog.Write("Exit: reach out-of-world");
				return false;
			}

			if (Main.netMode == 2 && (!Netplay.Clients[player.whoAmI].TileSections[Netplay.GetSectionX((int)reachX), Netplay.GetSectionY((int)reachY)] ||
			                          Netplay.GetSectionX((int)reachX) < 0 || Netplay.GetSectionY((int)reachY) < 0))
				return false;

			// 逐格：默认原版互动距离；快速或 MpStaggeredUnlimitedReach：全图可铺（仅判定「是否执行放置链」，不单独改 Kill 逻辑）
			if (!IsWandPlacementReachAllowed(player, item, (int)reachX, (int)reachY))
			{
				CreateWandMpDebugLog.Write("Exit: reach check failed");
				MaybeHintMpReachTooFar(player);
				return false;
			}

			if (kind == 1)
			{
				CreateWandPngLibrary.EnsureReload();
				if (datamapIndex < 0 || datamapIndex >= CreateWandPngLibrary.Entries.Count)
					return false;
				CreateWandBlueprintEntry entry = CreateWandPngLibrary.Entries[datamapIndex];
				BuildingData data = entry.Data;
				if (data.Width * data.Height > BuildingData.MaxBlueprintCells)
					return false;

				if (!FitsWorld(originX, originY, data.Width, data.Height))
					return false;

				int cx = (int)originX + data.Width / 2;
				int cy = (int)originY + data.Height / 2;
				if (!IsWandPlacementReachAllowed(player, item, cx, cy))
				{
					CreateWandMpDebugLog.Write("Exit: blueprint center reach failed cx=" + cx + " cy=" + cy);
					MaybeHintMpReachTooFar(player);
					return false;
				}
			}

			CreateWandMpPlacementDiagnostics.LogAfterWandReachGate(player, originX, originY, reachX, reachY, kind, preset,
				datamapIndex);

			// 专用服：与原版 HandlePlaceRequestFromClient → TryAuthoritativePlace 一致（原版 PNG 索引 + SendTileSquare）
			if (Main.netMode == 2)
			{
				if (CreateWandVanillaMpBridge.TryInvokeOfficialDedicatedPlace(player, kind, preset, datamapIndex, reachX,
					    reachY, originX, originY, out bool dedicatedIncompatible))
				{
					suppressSuccessSound = true;
					return true;
				}

				if (dedicatedIncompatible)
				{
					if (!Main.dedServ)
						Terraria.CombatText.NewText(player.getRect(), Color.OrangeRed,
							"[魔杖] 原版库无此蓝图名。连 **原版服** 时：在你本机 CreateWand 放同名 PNG 即可，**远程服不必有蓝图**；仅 **注入专用服** 时需在服机同路径准备 PNG。", false, false);
					return false;
				}
			}

			if (kind == 1)
			{
				CreateWandBlueprintEntry entry = CreateWandPngLibrary.Entries[datamapIndex];
				BlueprintPlacementPlan plan = CreateWandBlueprintSemantics.ResolvePlacementPlan(entry);
				BuildingData data = plan.Mode == BlueprintPlacementMode.PreciseCopy && entry.PreciseData != null
					? entry.PreciseData
					: entry.Data;

				if (plan.Mode == BlueprintPlacementMode.PreciseCopy && !data.HasAnyPreciseCellToPlace())
				{
					Terraria.CombatText.NewText(player.getRect(), Color.Gray, "[魔杖] 精确蓝图无有效格子（全空）", false, false);
					return false;
				}

				if (ShouldUseQueuedBlueprintPlacement())
				{
					if (CreateWandStaggeredPlacementQueue.IsBusy)
					{
						Terraria.CombatText.NewText(player.getRect(), Color.LightCoral, "[魔杖] 上次蓝图尚在铺设中…", false, false);
						return false;
					}

					return CreateWandStaggeredPlacementQueue.TryEnqueueBlueprint(player, data, (int)originX, (int)originY, plan.Mode);
				}

				if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
					return CreateWandPlacementService.PlaceBuildingViaServerActions(player, data, (int)originX, (int)originY, plan.Mode,
						out _, out _, out _, out _);

				return CreateWandPlacementService.PlaceBuildingAuthoritative(player, data, (int)originX, (int)originY, plan.Mode,
					out _, out _, out _, out _);
			}

			if (preset > 2)
				return false;

			if (ShouldUseQueuedBlueprintPlacement())
			{
				if (CreateWandStaggeredPlacementQueue.IsBusy)
				{
					Terraria.CombatText.NewText(player.getRect(), Color.LightCoral, "[魔杖] 上次蓝图尚在铺设中…", false, false);
					return false;
				}

				return CreateWandStaggeredPlacementQueue.TryEnqueuePreset(player, (PlacePreset)preset, (int)reachX, (int)reachY);
			}

			if (Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet)
				return CreateWandPlacementService.TryPlacePresetViaServerActions(player, (PlacePreset)preset, (int)reachX, (int)reachY,
					out _, out _, out _, out _);

			return TryPlacePresetAuthoritative(player, (PlacePreset)preset, (int)reachX, (int)reachY, out _, out _, out _, out _);
		}

		private static bool IsWandPlacementReachAllowed(Terraria.Player player, Item wandItem, int tileX, int tileY) => true;

		private static void MaybeHintMpReachTooFar(Terraria.Player player)
		{
			if (Main.netMode != 1 || !CreateWandSelectionState.UseStaggeredPlacement ||
			    CreateWandSelectionState.MpStaggeredUnlimitedReach)
				return;
			long now = DateTime.UtcNow.Ticks;
			if (now < _nextMpReachHintUtcTicks)
				return;
			_nextMpReachHintUtcTicks = now + TimeSpan.TicksPerSecond * 5;
			Terraria.CombatText.NewText(player.getRect(), Color.LightGreen,
				"[魔杖] 联机逐格需在互动距离内；走近或按 [ 切「快速」不限距离。", false, false);
		}

		/// <summary>
		/// 联机真服始终逐格；<see cref="CreateWandSelectionState.MpLocalOnlyNoNet"/> 时尊重 [ 快速/逐格 切换。
		/// </summary>
		private static bool ShouldUseQueuedBlueprintPlacement() =>
			Main.netMode == 1
				? !CreateWandSelectionState.MpLocalOnlyNoNet || CreateWandSelectionState.UseStaggeredPlacement
				: CreateWandSelectionState.UseStaggeredPlacement;

		private static bool IsWandUnlimitedPlacementReach() =>
			!CreateWandSelectionState.UseStaggeredPlacement;

		private static bool IsWandTargetInPlayerReach(Terraria.Player player, Item wandItem, int tileX, int tileY)
		{
			int tileBoost = wandItem.tileBoost + player.blockRange;
			return player.IsInTileInteractionRange(tileX, tileY, TileReachCheckSettings.Simple, tileBoost);
		}

		private static bool FitsWorld(short ox, short oy, int width, int height)
		{
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					int x = (int)ox + j;
					int y = (int)oy + i;
					if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
						return false;
				}
			}

			return true;
		}

		private static bool TryPlacePresetAuthoritative(Terraria.Player player, PlacePreset preset, int cx, int cy,
			out int minX, out int minY, out int w, out int h)
		{
			ClearPresetFootprintIfNeeded(preset, cx, cy);

			minX = cx;
			minY = cy;
			int maxX = cx;
			int maxY = cy;
			switch (preset)
			{
				case PlacePreset.Stone3x3:
					for (int i = -1; i <= 1; i++)
					for (int j = -1; j <= 1; j++)
					{
						int x = cx + i;
						int y = cy + j;
						TryPlaceTile(player, x, y, 1, 0);
						minX = Math.Min(minX, x);
						minY = Math.Min(minY, y);
						maxX = Math.Max(maxX, x);
						maxY = Math.Max(maxY, y);
					}

					break;
				case PlacePreset.WoodPlatform5:
					for (int k = -2; k <= 2; k++)
					{
						int x = cx + k;
						TryPlaceTile(player, x, cy, 19, 0);
						minX = Math.Min(minX, x);
						minY = Math.Min(minY, cy);
						maxX = Math.Max(maxX, x);
						maxY = Math.Max(maxY, cy);
					}

					break;
				case PlacePreset.Dirt2x2:
					for (int l = 0; l < 2; l++)
					for (int m = 0; m < 2; m++)
					{
						int x = cx + l;
						int y = cy + m;
						TryPlaceTile(player, x, y, 0, 0);
						minX = Math.Min(minX, x);
						minY = Math.Min(minY, y);
						maxX = Math.Max(maxX, x);
						maxY = Math.Max(maxY, y);
					}

					break;
				default:
					w = 0;
					h = 0;
					return false;
			}

			w = maxX - minX + 1;
			h = maxY - minY + 1;
			return true;
		}

		private static void ClearPresetFootprintIfNeeded(PlacePreset preset, int cx, int cy)
		{
			if (!CreateWandSelectionState.ClearAreaBeforePlace)
				return;

			switch (preset)
			{
				case PlacePreset.Stone3x3:
					CreateWandPlacementService.ClearTilesAndWallsInRect(cx - 1, cy - 1, 3, 3);
					break;
				case PlacePreset.WoodPlatform5:
					CreateWandPlacementService.ClearTilesAndWallsInRect(cx - 2, cy, 5, 1);
					break;
				case PlacePreset.Dirt2x2:
					CreateWandPlacementService.ClearTilesAndWallsInRect(cx, cy, 2, 2);
					break;
			}
		}

		private static void TryPlaceTile(Terraria.Player player, int x, int y, int tileType, int style)
		{
			CreateWandPlacementService.TryPlaceTileVanilla(player, x, y, tileType, style);
		}

		private static void PlayLocalSuccessSound(byte kind, short reachX, short reachY)
		{
			if (kind == 1)
				SoundEngine.PlaySound(SoundID.Item14, Main.MouseWorld, 0f, 1f);
			else
				SoundEngine.PlaySound(SoundID.Item1, new Vector2(reachX * 16f, reachY * 16f), 0f, 1f);
		}
	}
}
