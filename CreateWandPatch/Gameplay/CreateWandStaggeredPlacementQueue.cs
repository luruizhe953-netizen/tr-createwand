using System.Collections.Generic;
using CreateWandPatch.Content;
using Terraria;

namespace CreateWandPatch.Gameplay
{
	/// <summary>
	/// 逐格铺设队列。联机走 <see cref="CreateWandPlacementService.TryPlaceTileViaServer"/>。
	/// <see cref="CreateWandSelectionState.UseStaggeredPlacementEffective"/> 为 true 时在多帧内调用。
	/// </summary>
	public static class CreateWandStaggeredPlacementQueue
	{
		public const int CellsPerUpdate = 1;

		/// <summary>
		/// 联机「手持·失败同逐格」：格与格之间额外空帧，贴近手点节奏（0=每帧一格）。
		/// </summary>
		public const int VanillaHandheldExtraFramesBetweenCells = 3;

		/// <summary>主循环（墙/砖）结束后、家具阶段开始前额外等待帧数，让服端图格稳定。</summary>
		public const int MpFurniturePhaseDelayFrames = 24;

		/// <summary>主阶段结束后、手持电路/锤/漆阶段开始前空帧。</summary>
		public const int MpHandheldExtraPhaseDelayFrames = 12;

		/// <summary>家具阶段结束后、液体阶段开始前空帧（与家具延迟同量级）。</summary>
		public const int MpLiquidPhaseDelayFrames = 8;

		public static bool IsBusy => _job != null;

		private static IStaggerJob _job;
		private static int _slowMpStaggerFramesUntilNext;

		public static bool TryEnqueueBlueprint(Terraria.Player player, BuildingData data, int originX, int originY,
			BlueprintPlacementMode mode)
		{
			if (_job != null)
				return false;
			if (mode == BlueprintPlacementMode.LegacySort)
				_job = new LegacyBlueprintJob(player.whoAmI, data, originX, originY);
			else
				_job = new PreciseBlueprintJob(player.whoAmI, data, originX, originY);
			_slowMpStaggerFramesUntilNext = 0;
			_job.OnStart();
			return true;
		}

		public static bool TryEnqueuePreset(Terraria.Player player, PlacePreset preset, int cx, int cy)
		{
			if (_job != null)
				return false;
			_job = new PresetJob(player.whoAmI, preset, cx, cy);
			_slowMpStaggerFramesUntilNext = 0;
			_job.OnStart();
			return true;
		}

		private static bool TryGetMpSlowStaggerInterval(out int framesBetweenCells)
		{
			framesBetweenCells = 0;
			if (Main.netMode != 1)
				return false;
			if (CreateWandSelectionState.MpLocalOnlyNoNet || CreateWandSelectionState.MpDeleteOnly)
				return false;
			if (CreateWandSelectionState.MpVanillaHandheldThenExplicitMsg17)
			{
				framesBetweenCells = VanillaHandheldExtraFramesBetweenCells;
				return true;
			}

			return false;
		}

		/// <summary>供 UI/热键提示：当前逐格铺设的大致速率描述。</summary>
		public static string GetStaggeredSpeedHintForCombatText()
		{
			if (Main.netMode == 1 && CreateWandSelectionState.MpVanillaHandheldThenExplicitMsg17 &&
			    !CreateWandSelectionState.MpLocalOnlyNoNet && !CreateWandSelectionState.MpDeleteOnly)
				return "手持·失败同逐格：每格间隔 " + VanillaHandheldExtraFramesBetweenCells + " 帧（约 1 格/" +
				       (1 + VanillaHandheldExtraFramesBetweenCells) + " 帧）";
			return "每帧最多 " + CellsPerUpdate + " 格";
		}

		public static void ProcessFrame()
		{
			if (_job == null)
				return;

			var p = Main.LocalPlayer;
			bool holdingWand = p != null && p.active && p.inventory[p.selectedItem].type == CreateWandIds.ItemType;
			bool tempHeldMaterial = p != null && p.active && CreateWandSurvivalRemoteCompat.IsInTemporaryHeldMaterialWindow(p);
			if (p == null || !p.active || Main.gameMenu || p.noBuilding ||
			    (!holdingWand && !tempHeldMaterial) || p.whoAmI != _job.PlayerWhoAmI)
			{
				FinishJob(cancelled: true);
				return;
			}

			// 手持·失败同逐格（联机且非仅本地/非只删）：一次只推进一格，格与格之间再空若干帧
			if (TryGetMpSlowStaggerInterval(out int interval))
			{
				if (_slowMpStaggerFramesUntilNext > 0)
				{
					_slowMpStaggerFramesUntilNext--;
					return;
				}

				if (!_job.Step(p))
				{
					FinishJob(cancelled: false);
					return;
				}

				_slowMpStaggerFramesUntilNext = interval;
				return;
			}

			for (int n = 0; n < CellsPerUpdate; n++)
			{
				if (_job.Step(p))
					continue;
				FinishJob(cancelled: false);
				return;
			}
		}

		private static void FinishJob(bool cancelled)
		{
			if (_job == null)
				return;
			if (!cancelled)
			{
				_job.OnComplete();
				var p = Main.LocalPlayer;
				if (p != null && p.active)
					CreateWandSurvivalRemoteCompat.TryClearAutoGrantedMaterials(p);
			}
			else if (_job is PreciseBlueprintJob)
				CreateWandPlacementService.EndPreciseBlueprintPlacement();

			_job = null;
			_slowMpStaggerFramesUntilNext = 0;
		}

		private interface IStaggerJob
		{
			int PlayerWhoAmI { get; }
			void OnStart();
			/// <returns>false 表示全部完成。</returns>
			bool Step(Terraria.Player player);
			/// <summary>逐格队列正常结束时调用（取消/中断不调用）。</summary>
			void OnComplete();
		}

		/// <summary>精确瓦片：主阶段物块链 → 手持电路/锤/漆 → 家具 → 桶倒液体。</summary>
		private sealed class PreciseBlueprintJob : IStaggerJob
		{
			private readonly int _whoAmI;
			private readonly BuildingData _data;
			private readonly int _ox;
			private readonly int _oy;
			private readonly int _w;
			private readonly int[] _mainCells;
			private readonly int[] _handheldExtraCells;
			private readonly int[] _liquidCells;
			private readonly int[] _clearCells;
			private readonly List<CreateWandPlacementService.LegacyFurnitureJob> _furniture =
				new List<CreateWandPlacementService.LegacyFurnitureJob>();
			private readonly HashSet<long> _furnitureAnchors = new HashSet<long>();
			private bool _useServerAuthoritativeActions;
			private int _nextMain;
			private int _nextHandheld;
			private int _nextFurniture;
			private int _nextLiquid;
			private int _handheldPhaseDelay;
			private int _furniturePhaseDelay;
			private int _liquidPhaseDelay;
			private bool _inHandheldPhase;
			private bool _inFurniturePhase;
			private bool _inLiquidPhase;

			public PreciseBlueprintJob(int whoAmI, BuildingData data, int ox, int oy)
			{
				_whoAmI = whoAmI;
				_data = data;
				_ox = ox;
				_oy = oy;
				_w = data.Width;
				CreateWandBlueprintPlacementOrder.BuildPrecisePassIndices(data, out _mainCells, out _liquidCells);
				CreateWandPreciseHandheldExtras.BuildHandheldExtraIndices(data, out _handheldExtraCells);
				_clearCells = CreateWandBlueprintPlacementOrder.BuildAllCellsBottomUp(_w, data.Height);
			}

			public int PlayerWhoAmI => _whoAmI;

			public void OnStart()
			{
				_useServerAuthoritativeActions = Main.netMode == 1;
				CreateWandPlacementService.BeginPreciseBlueprintPlacement();
				if (CreateWandSelectionState.ClearAreaBeforePlace && _data.Width > 0 && _data.Height > 0)
				{
					if (!_useServerAuthoritativeActions)
						CreateWandPlacementService.ClearTilesAndWallsInRect(_ox, _oy, _data.Width, _data.Height);
				}
			}

			private bool BeginHandheldPhaseIfNeeded()
			{
				if (_handheldExtraCells.Length == 0)
					return false;
				_inHandheldPhase = true;
				_handheldPhaseDelay = MpHandheldExtraPhaseDelayFrames;
				return true;
			}

			private bool BeginFurniturePhaseIfNeeded()
			{
				if (_furniture.Count == 0)
					return false;
				_inFurniturePhase = true;
				_furniturePhaseDelay = MpFurniturePhaseDelayFrames;
				return true;
			}

			private bool BeginLiquidPhaseIfNeeded()
			{
				if (_liquidCells.Length == 0)
					return false;
				_inLiquidPhase = true;
				_liquidPhaseDelay = MpLiquidPhaseDelayFrames;
				return true;
			}

			public bool Step(Terraria.Player player)
			{
				if (!_inHandheldPhase && !_inFurniturePhase && !_inLiquidPhase)
				{
					int[] order = CreateWandSelectionState.MpDeleteOnly ? _clearCells : _mainCells;
					if (_nextMain >= order.Length)
					{
						if (CreateWandSelectionState.MpDeleteOnly)
							return false;
						if (BeginHandheldPhaseIfNeeded())
							return true;
						if (BeginFurniturePhaseIfNeeded())
							return true;
						if (BeginLiquidPhaseIfNeeded())
							return true;
						return false;
					}

					int i = order[_nextMain++];
					int tx = _ox + i % _w;
					int ty = _oy + i / _w;
					if (CreateWandPlacementService.IsInWorldTile(tx, ty))
					{
						if (CreateWandSelectionState.MpDeleteOnly)
							CreateWandPlacementService.TryClearCellViaServer(player, tx, ty);
						else if (_useServerAuthoritativeActions)
						{
							if (CreateWandPlacementService.TryGetPreciseServerCellOverrides(_data, _ox, _oy, _w, i,
								    out tx, out ty, out BuildingData.TileInfo info, out int? wallOverride,
								    out int? tileOverride, out int? styleOverride))
							{
								CreateWandPlacementService.TryPlaceLegacyServerCell(player, tx, ty, info,
									CreateWandPlacementService.GetDefaultWallType(), wallOverride, tileOverride,
									styleOverride, _furniture, _furnitureAnchors);
							}
						}
						else
							CreateWandPlacementService.ApplyPreciseCellStructure(player, _data, _ox, _oy, _w, i);
					}

					return true;
				}

				if (_inHandheldPhase && !_inFurniturePhase && !_inLiquidPhase)
				{
					if (_handheldPhaseDelay > 0)
					{
						_handheldPhaseDelay--;
						return true;
					}

					if (_nextHandheld < _handheldExtraCells.Length)
					{
						int hi = _handheldExtraCells[_nextHandheld++];
						CreateWandPreciseHandheldExtras.TryApplyHandheldExtras(player, _data, _ox, _oy, _w, hi);
						return true;
					}

					_inHandheldPhase = false;
					if (BeginFurniturePhaseIfNeeded())
						return true;
					if (BeginLiquidPhaseIfNeeded())
						return true;
					return false;
				}

				if (_inFurniturePhase && !_inLiquidPhase)
				{
					if (_furniturePhaseDelay > 0)
					{
						_furniturePhaseDelay--;
						return true;
					}

					if (_nextFurniture < _furniture.Count)
					{
						if (_useServerAuthoritativeActions)
						{
							if (CreateWandSelectionState.MpRegionSyncAfterStaggeredBlueprint)
								CreateWandPlacementService.ApplyLegacyFurnitureCell(player, _furniture[_nextFurniture],
									localForMpRegionSync: true);
							else
								CreateWandPlacementService.TryPlaceFurnitureViaServer(player, _furniture[_nextFurniture],
									clearBeforePlace: CreateWandSelectionState.ClearAreaBeforePlace);
						}
						else
							CreateWandPlacementService.ApplyLegacyFurnitureCell(player, _furniture[_nextFurniture]);

						_nextFurniture++;
						return true;
					}

					_inFurniturePhase = false;
					if (_liquidCells.Length > 0)
					{
						_inLiquidPhase = true;
						_liquidPhaseDelay = MpLiquidPhaseDelayFrames;
						return true;
					}

					return false;
				}

				if (_liquidPhaseDelay > 0)
				{
					_liquidPhaseDelay--;
					return true;
				}

				if (_nextLiquid >= _liquidCells.Length)
					return false;

				int li = _liquidCells[_nextLiquid++];
				CreateWandPlacementService.ApplyPreciseCellLiquid(player, _data, _ox, _oy, _w, li);
				return true;
			}

			public void OnComplete()
			{
				if (_data.Width > 0 && _data.Height > 0 && !CreateWandSelectionState.MpDeleteOnly)
				{
					CreateWandPlacementService.SyncPreciseBlueprintToServerIfEnabled(_ox, _oy, _data.Width, _data.Height);
					if (_useServerAuthoritativeActions &&
					    CreateWandSelectionState.MpRegionSyncAfterStaggeredBlueprint)
						CreateWandPlacementService.SyncTileRegionToServerIfMpClient(_ox, _oy, _data.Width, _data.Height);
				}

				CreateWandPlacementService.EndPreciseBlueprintPlacement();
			}
		}

		private sealed class LegacyBlueprintJob : IStaggerJob
		{
			private readonly int _whoAmI;
			private readonly BuildingData _data;
			private readonly int _ox;
			private readonly int _oy;
			private readonly int[] _mainCells;
			private readonly int[] _clearCells;
			private readonly Item _wallTemplate = CreateWandPlacementService.CreateWallTemplateItem();
			private readonly List<CreateWandPlacementService.LegacyFurnitureJob> _furniture = new List<CreateWandPlacementService.LegacyFurnitureJob>();
			private readonly HashSet<long> _furnitureAnchors = new HashSet<long>();
			private readonly int _defaultWallType;
			private bool _useServerAuthoritativeActions;
			private int _nextMain;
			private int _nextFurniture;
			private int _furniturePhaseDelay;
			private bool _inFurniturePhase;

			public LegacyBlueprintJob(int whoAmI, BuildingData data, int ox, int oy)
			{
				_whoAmI = whoAmI;
				_data = data;
				_ox = ox;
				_oy = oy;
				CreateWandBlueprintPlacementOrder.BuildLegacyPassIndices(data, out _mainCells);
				_clearCells = CreateWandBlueprintPlacementOrder.BuildAllCellsBottomUp(data.Width, data.Height);
				_defaultWallType = _wallTemplate.createWall;
			}

			public int PlayerWhoAmI => _whoAmI;

			public void OnStart()
			{
				_useServerAuthoritativeActions = Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet;
				if (CreateWandSelectionState.ClearAreaBeforePlace && _data.Width > 0 && _data.Height > 0)
				{
					if (!_useServerAuthoritativeActions)
						CreateWandPlacementService.ClearTilesAndWallsInRect(_ox, _oy, _data.Width, _data.Height);
				}
			}

			public bool Step(Terraria.Player player)
			{
				if (!_inFurniturePhase)
				{
					int[] order = CreateWandSelectionState.MpDeleteOnly ? _clearCells : _mainCells;
					if (_nextMain >= order.Length)
					{
						if (_furniture.Count > 0)
						{
							_inFurniturePhase = true;
							_furniturePhaseDelay = MpFurniturePhaseDelayFrames;
							return true;
						}

						return false;
					}

					int i = order[_nextMain++];
					int tx = _ox + i % _data.Width;
					int ty = _oy + i / _data.Width;
					if (CreateWandPlacementService.IsInWorldTile(tx, ty))
					{
						if (CreateWandSelectionState.MpDeleteOnly)
							CreateWandPlacementService.TryClearCellViaServer(player, tx, ty);
						else if (_useServerAuthoritativeActions)
							CreateWandPlacementService.TryPlaceLegacyServerCell(player, tx, ty, _data.TileInfos[i],
								_defaultWallType, null, null, null, _furniture, _furnitureAnchors);
						else
							CreateWandPlacementService.ApplyLegacyMainLoopCell(player, _data, _ox, _oy, i, _wallTemplate,
								_furniture, _furnitureAnchors);
					}

					return true;
				}

				if (_nextFurniture >= _furniture.Count)
					return false;

				if (_furniturePhaseDelay > 0)
				{
					_furniturePhaseDelay--;
					return true;
				}

				if (_useServerAuthoritativeActions)
				{
					if (CreateWandSelectionState.MpRegionSyncAfterStaggeredBlueprint)
						CreateWandPlacementService.ApplyLegacyFurnitureCell(player, _furniture[_nextFurniture], localForMpRegionSync: true);
					else
						CreateWandPlacementService.TryPlaceFurnitureViaServer(player, _furniture[_nextFurniture],
							clearBeforePlace: CreateWandSelectionState.ClearAreaBeforePlace);
				}
				else
					CreateWandPlacementService.ApplyLegacyFurnitureCell(player, _furniture[_nextFurniture]);
				_nextFurniture++;
				return _nextFurniture < _furniture.Count;
			}

			public void OnComplete()
			{
				if (!_useServerAuthoritativeActions || CreateWandSelectionState.MpDeleteOnly)
					return;
				if (_data.Width > 0 && _data.Height > 0)
					CreateWandPlacementService.SyncTileRegionToServerIfMpClient(_ox, _oy, _data.Width, _data.Height);
			}
		}

		private sealed class PresetJob : IStaggerJob
		{
			private readonly int _whoAmI;
			private readonly PlacePreset _preset;
			private readonly int _cx;
			private readonly int _cy;
			private bool _useServerAuthoritativeActions;
			private int _step;
			private static readonly (int dx, int dy)[] Stone3x3Order = new[]
			{
				(0, 0),
				(0, -1), (0, 1), (-1, 0), (1, 0),
				(-1, -1), (1, -1), (-1, 1), (1, 1)
			};
			private static readonly int[] Platform5Order = { 0, -1, 1, -2, 2 };
			private static readonly (int dx, int dy)[] Dirt2x2Order = new[]
			{
				(0, 0), (1, 0), (0, 1), (1, 1)
			};

			public PresetJob(int whoAmI, PlacePreset preset, int cx, int cy)
			{
				_whoAmI = whoAmI;
				_preset = preset;
				_cx = cx;
				_cy = cy;
			}

			public int PlayerWhoAmI => _whoAmI;

			public void OnStart()
			{
				_useServerAuthoritativeActions = Main.netMode == 1 && !CreateWandSelectionState.MpLocalOnlyNoNet;
				switch (_preset)
				{
					case PlacePreset.Stone3x3 when CreateWandSelectionState.ClearAreaBeforePlace:
						BeginClear(_cx - 1, _cy - 1, 3, 3);
						break;
					case PlacePreset.WoodPlatform5 when CreateWandSelectionState.ClearAreaBeforePlace:
						BeginClear(_cx - 2, _cy, 5, 1);
						break;
					case PlacePreset.Dirt2x2 when CreateWandSelectionState.ClearAreaBeforePlace:
						BeginClear(_cx, _cy, 2, 2);
						break;
				}
			}

			public bool Step(Terraria.Player player)
			{
				switch (_preset)
				{
					case PlacePreset.Stone3x3:
					{
						if (_step >= 9)
							return false;
						var o = Stone3x3Order[_step];
						int i = o.dx;
						int j = o.dy;
						if (_useServerAuthoritativeActions)
						{
							if (CreateWandSelectionState.MpDeleteOnly)
								CreateWandPlacementService.TryClearCellViaServer(player, _cx + i, _cy + j);
							else
								CreateWandPlacementService.TryPlaceTileViaServer(player, _cx + i, _cy + j, 1, 0,
									clearBeforePlace: CreateWandSelectionState.ClearAreaBeforePlace);
						}
						else
							CreateWandPlacementService.TryPlaceTileVanilla(player, _cx + i, _cy + j, 1, 0);
						_step++;
						return _step < 9;
					}
					case PlacePreset.WoodPlatform5:
					{
						if (_step >= 5)
							return false;
						int k = Platform5Order[_step];
						if (_useServerAuthoritativeActions)
						{
							if (CreateWandSelectionState.MpDeleteOnly)
								CreateWandPlacementService.TryClearCellViaServer(player, _cx + k, _cy);
							else
								CreateWandPlacementService.TryPlaceTileViaServer(player, _cx + k, _cy, 19, 0,
									clearBeforePlace: CreateWandSelectionState.ClearAreaBeforePlace);
						}
						else
							CreateWandPlacementService.TryPlaceTileVanilla(player, _cx + k, _cy, 19, 0);
						_step++;
						return _step < 5;
					}
					case PlacePreset.Dirt2x2:
					{
						if (_step >= 4)
							return false;
						var o = Dirt2x2Order[_step];
						int l = o.dx;
						int m = o.dy;
						if (_useServerAuthoritativeActions)
						{
							if (CreateWandSelectionState.MpDeleteOnly)
								CreateWandPlacementService.TryClearCellViaServer(player, _cx + l, _cy + m);
							else
								CreateWandPlacementService.TryPlaceTileViaServer(player, _cx + l, _cy + m, 0, 0,
									clearBeforePlace: CreateWandSelectionState.ClearAreaBeforePlace);
						}
						else
							CreateWandPlacementService.TryPlaceTileVanilla(player, _cx + l, _cy + m, 0, 0);
						_step++;
						return _step < 4;
					}
					default:
						return false;
				}
			}

			public void OnComplete()
			{
				if (!_useServerAuthoritativeActions || CreateWandSelectionState.MpDeleteOnly)
					return;
				int ox;
				int oy;
				int w;
				int h;
				switch (_preset)
				{
					case PlacePreset.Stone3x3:
						ox = _cx - 1;
						oy = _cy - 1;
						w = 3;
						h = 3;
						break;
					case PlacePreset.WoodPlatform5:
						ox = _cx - 2;
						oy = _cy;
						w = 5;
						h = 1;
						break;
					case PlacePreset.Dirt2x2:
						ox = _cx;
						oy = _cy;
						w = 2;
						h = 2;
						break;
					default:
						return;
				}

				CreateWandPlacementService.SyncTileRegionToServerIfMpClient(ox, oy, w, h);
			}

			private void BeginClear(int x, int y, int w, int h)
			{
				if (!_useServerAuthoritativeActions)
					CreateWandPlacementService.ClearTilesAndWallsInRect(x, y, w, h);
			}
		}
	}
}
