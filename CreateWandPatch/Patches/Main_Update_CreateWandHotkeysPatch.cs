using System;
using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using CreateWandPatch.Rendering;
using CreateWandPatch.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace CreateWandPatch.Patches
{
	/// <summary>
	/// 无面板时的快捷键：B 开始/取消框选导出 PNG；1/2/3 预设；[ 切换 快速(单机式)/逐格(联机式)；- / + 上一张 / 下一张蓝图；] 切换放置前清空；
	/// ; 切换蓝图精确放置（cwmap / qotstruct / PNG+侧写 cwmap 走图格 1:1）；N 联机打开发包模式列表。
	/// F9（不需魔杖）联机切换：上行 SendData + 下行 GetData 双日志；Shift+F9 切换「仅图格相关」过滤；Ctrl+F9 开关「闪回」检测（TileRollback → mp.log）。
	/// 持有魔杖时：除 F9 / P 给予魔杖外下列热键生效。
	/// </summary>
	/// <summary>Main.Update 为 protected，此处用方法名字符串以便 Harmony 绑定。</summary>
	[HarmonyPatch(typeof(Main), "Update", new[] { typeof(GameTime) })]
	public static class Main_Update_CreateWandHotkeysPatch
	{
		private static KeyboardState _lastKb;
		private static int _giveCooldown;

		[HarmonyPostfix]
		public static void Postfix(GameTime gameTime)
		{
			var kb = Keyboard.GetState();
			var prevKb = _lastKb;

			if (_giveCooldown > 0)
				_giveCooldown--;

			try
			{
				var p = Main.LocalPlayer;
				if (p == null || !p.active || Main.gameMenu)
					return;

				ApplyDefaultPlacementPaceByNetMode();

				CreateWandWorldPreview.SyncPreviewTexture();
				CreateWandStaggeredPlacementQueue.ProcessFrame();
				CreateWandDeferredWgFallbackQueue.ProcessFrame();
				CreateWandSurvivalRemoteCompat.ProcessFrame();
				CreateWandTileRollbackDetector.ProcessFrame();

				// P 键：给予创造魔杖（使用原版 GetItem，避免直接写格子被下一帧逻辑清空）
				if (_giveCooldown == 0 && KeyPressed(kb, prevKb, Keys.P))
					GiveCreateWand(p);

				if (Main.netMode == 1)
				{
					bool ctrlDown = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
					if (ctrlDown && KeyPressed(kb, prevKb, Keys.F9))
					{
						CreateWandSelectionState.EnableMpTileRollbackTrace = !CreateWandSelectionState.EnableMpTileRollbackTrace;
						Terraria.CombatText.NewText(p.getRect(),
							CreateWandSelectionState.EnableMpTileRollbackTrace
								? new Microsoft.Xna.Framework.Color(255, 200, 160)
								: Microsoft.Xna.Framework.Color.Gray,
							CreateWandSelectionState.EnableMpTileRollbackTrace
								? "[CreateWand] 闪回检测：开 → mp.log 前缀 TileRollback（Ctrl+F9 关）"
								: "[CreateWand] 闪回检测：关（Ctrl+F9 开）",
							false, false);
						SoundEngine.PlaySound(12, -1, -1, 1, 0.75f, 0f);
					}
					else if (KeyPressed(kb, prevKb, Keys.F9))
					{
						bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
						if (shift)
						{
							CreateWandSelectionState.OutgoingNetTraceTileRelatedOnly =
								!CreateWandSelectionState.OutgoingNetTraceTileRelatedOnly;
							Terraria.CombatText.NewText(p.getRect(), new Microsoft.Xna.Framework.Color(200, 230, 255),
								CreateWandSelectionState.OutgoingNetTraceTileRelatedOnly
									? "[CreateWand] 网络日志过滤：仅 17/19/20/79 上下行（Shift+F9 切回全部）"
									: "[CreateWand] 网络日志过滤：全部 msgType 上下行（Shift+F9 切仅图格）",
								false, false);
						}
						else
						{
							CreateWandSelectionState.EnableClientOutgoingNetTrace = !CreateWandSelectionState.EnableClientOutgoingNetTrace;
							Terraria.CombatText.NewText(p.getRect(),
								CreateWandSelectionState.EnableClientOutgoingNetTrace
									? new Microsoft.Xna.Framework.Color(160, 220, 255)
									: Microsoft.Xna.Framework.Color.Gray,
								CreateWandSelectionState.EnableClientOutgoingNetTrace
									? "[CreateWand] 网络日志：开 → outgoing-net + incoming-net（F9 关；Shift+F9 筛图格）"
									: "[CreateWand] 网络日志：关（F9 开）",
								false, false);
						}

						SoundEngine.PlaySound(12, -1, -1, 1, 0.75f, 0f);
					}
				}

				if (p.inventory[p.selectedItem].type != CreateWandIds.ItemType)
					return;

				// 逐格速度热键（< 和 >，不需 FancyUI 也可调）
				if (KeyPressed(kb, prevKb, Keys.OemComma))
				{
					int newSpeed = CreateWandStaggeredPlacementQueue.AdjustHandheldSpeed(+1);
					Terraria.CombatText.NewText(p.getRect(), new Microsoft.Xna.Framework.Color(200, 220, 255),
						"[魔杖] 逐格减速 → " + CreateWandStaggeredPlacementQueue.GetSpeedBarForCombatText(), false, false);
				}
				else if (KeyPressed(kb, prevKb, Keys.OemPeriod))
				{
					int newSpeed = CreateWandStaggeredPlacementQueue.AdjustHandheldSpeed(-1);
					Terraria.CombatText.NewText(p.getRect(), new Microsoft.Xna.Framework.Color(200, 255, 200),
						"[魔杖] 逐格加速 → " + CreateWandStaggeredPlacementQueue.GetSpeedBarForCombatText(), false, false);
				}
				else if (KeyPressed(kb, prevKb, Keys.K))
				{
					if (!CreateWandSelectionState.MpDeleteOnly)
					{
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.OrangeRed,
							"[魔杖] 一键全清仅在「仅删除」模式可用。请先按 N 切换到仅删除", false, false);
					}
					else if (TryPackFastClear(p, out BuildingData data, out int ox, out int oy))
					{
						CreateWandPlacementService.ClearEntireBlueprintFast(p, data, ox, oy);
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.Orange,
							"[魔杖] 一键全清完成（" + data.Width + "x" + data.Height + "，含线路）", false, false);
						SoundEngine.PlaySound(12, -1, -1, 1, 0.8f, 0f);
					}
				}

				// C — toggle material consumption (works regardless of FancyUI)
				if (KeyPressed(kb, prevKb, Keys.C))
				{
					CreateWandSelectionState.ConsumePlacementItems = !CreateWandSelectionState.ConsumePlacementItems;
					Terraria.CombatText.NewText(p.getRect(),
						CreateWandSelectionState.ConsumePlacementItems ? new Microsoft.Xna.Framework.Color(255, 200, 100) : Microsoft.Xna.Framework.Color.Gray,
						CreateWandSelectionState.ConsumePlacementItems ? "[魔杖] 材料消耗：开（每格扣1个）" : "[魔杖] 材料消耗：关", false, false);
				}

				// Fancy UI 打开时避免与界面/其它 IngameFancy 状态抢键（创造魔杖原版面板亦会走此分支）
				if (Main.inFancyUI)
					return;

				if (KeyPressed(kb, prevKb, Keys.B))
				{
					if (CreateWandBoxExportState.Phase != CreateWandBoxExportPhase.Inactive)
					{
						CreateWandBoxExportState.Cancel();
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.Gray,
							"[魔杖] 已取消框选导出", false, false);
					}
					else
					{
						CreateWandBoxExportState.Begin();
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.LightYellow,
							"[魔杖] 左键点矩形两角导出 PNG（再按 B 取消）", false, false);
					}

					SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
				}
				else if (KeyPressed(kb, prevKb, Keys.OemCloseBrackets))
				{
					CreateWandSelectionState.NextClearAreaMode();
					string clearLabel = CreateWandSelectionState.GetClearAreaModeLabel();
					Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.Orange,
						"[魔杖] 放置前清空：" + clearLabel + "（] 切换：关/逐格/一键）", false, false);
					CreateWandWorldPreview.InvalidateCache();
				}
				else if (KeyPressed(kb, prevKb, Keys.O))
				{
					CreateWandSelectionState.NextPlacementRepeatCount();
					Terraria.CombatText.NewText(p.getRect(), new Microsoft.Xna.Framework.Color(255, 200, 100),
						"[魔杖] 重复放置 " + CreateWandSelectionState.PlacementRepeatCount + " 次（防回滚 · 按 O 切换）", false, false);
				}
				else if (KeyPressed(kb, prevKb, Keys.OemSemicolon))
				{
					CreateWandSelectionState.EnablePreciseCwmapPlacement = !CreateWandSelectionState.EnablePreciseCwmapPlacement;
					Terraria.CombatText.NewText(p.getRect(), new Microsoft.Xna.Framework.Color(200, 230, 180),
						CreateWandSelectionState.EnablePreciseCwmapPlacement
							? "[魔杖] 蓝图精确放置：开（有 PreciseData 时图格 1:1）"
							: "[魔杖] 蓝图精确放置：关（走分类铺设）",
						false, false);
					CreateWandWorldPreview.InvalidateCache();
				}
				else if (KeyPressed(kb, prevKb, Keys.OemBackslash) || KeyPressed(kb, prevKb, Keys.OemPipe))
				{
					CreateWandSelectionState.PlacementEnabled = !CreateWandSelectionState.PlacementEnabled;
					Terraria.CombatText.NewText(p.getRect(),
						CreateWandSelectionState.PlacementEnabled ? Microsoft.Xna.Framework.Color.LightGreen : Microsoft.Xna.Framework.Color.Gray,
						CreateWandSelectionState.PlacementEnabled
							? "[魔杖] 放置功能：开（\\ 可切换）"
							: "[魔杖] 放置功能：关（仅预览，\\ 可切换）",
						false, false);
					SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
				}
				else if (KeyPressed(kb, prevKb, Keys.N))
				{
					if (Main.netMode != 1)
					{
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.Gray,
							"[魔杖] 发包模式列表仅在联机客户端有意义", false, false);
					}
					else
					{
						IngameFancyUI.OpenUIState(new CreateWandMpPlacementModeListPanel());
					}

					SoundEngine.PlaySound(12, -1, -1, 1, 0.85f, 0f);
				}

				if (KeyPressed(kb, prevKb, Keys.OemMinus) || KeyPressed(kb, prevKb, Keys.Subtract))
				{
					CreateWandPngLibrary.Reload();
					CreateWandSelectionState.SelectedKind = BlueprintKind.DataMap;
					if (CreateWandPngLibrary.Entries.Count > 0)
					{
						CreateWandSelectionState.SelectedDatamapIndex--;
						if (CreateWandSelectionState.SelectedDatamapIndex < 0)
							CreateWandSelectionState.SelectedDatamapIndex = CreateWandPngLibrary.Entries.Count - 1;
					}

					if (CreateWandPngLibrary.Entries.Count > 0)
					{
						var e = CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex];
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.LightGreen, "[魔杖] 蓝图：" + e.Name, false, false);
					}

					CreateWandWorldPreview.InvalidateCache();
				}
				else if (KeyPressed(kb, prevKb, Keys.OemPlus) || KeyPressed(kb, prevKb, Keys.Add))
				{
					CreateWandPngLibrary.Reload();
					CreateWandSelectionState.SelectedKind = BlueprintKind.DataMap;
					if (CreateWandPngLibrary.Entries.Count > 0)
					{
						CreateWandSelectionState.SelectedDatamapIndex =
							(CreateWandSelectionState.SelectedDatamapIndex + 1) % CreateWandPngLibrary.Entries.Count;
					}

					if (CreateWandPngLibrary.Entries.Count > 0)
					{
						var e = CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex];
						Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.LightGreen, "[魔杖] 蓝图：" + e.Name, false, false);
					}

					CreateWandWorldPreview.InvalidateCache();
				}
			}
			finally
			{
				// 每帧必须更新，否则在 gameMenu 返回时 prevKb 冻结，会出现一次按键多次「边沿」、刷屏给予
				_lastKb = kb;
			}
		}

		private static bool KeyPressed(KeyboardState cur, KeyboardState last, Keys k) =>
			cur.IsKeyDown(k) && !last.IsKeyDown(k);

		private static void ApplyDefaultPlacementPaceByNetMode()
		{
			if (CreateWandSelectionState.HasManualPaceChoice)
				return;

			// 默认策略：联机优先逐格（稳），单机默认快速（爽）；用户手动切换后不再自动覆盖。
			CreateWandSelectionState.UseStaggeredPlacement = Main.netMode == 1;
		}

		private static void GiveCreateWand(Terraria.Player player)
		{
			try
			{
				for (int s = 0; s < 50; s++)
				{
					if (player.inventory[s].type == CreateWandIds.ItemType)
					{
						Main.NewText("[创造魔杖] 已在背包（快捷栏格 " + (s + 1).ToString() + "）", 255, 220, 80);
						_giveCooldown = 12;
						return;
					}
				}

				var gift = new Item();
				// 与 Item.SetDefaults(int, ItemVariant) 一致，避免重载/初始化顺序下仍是空气
				gift.SetDefaults(CreateWandIds.ItemType, null);
				if (gift.IsAir || gift.type != CreateWandIds.ItemType)
				{
					Main.NewText("[创造魔杖] SetDefaults 失败（仍为空气），请查桌面 CreateWandPatch-harmony.txt", 255, 80, 80);
					_giveCooldown = 30;
					return;
				}

				// LoadItem(i) 会访问 TextureAssets.Item[i]；部分版本数组长度刚好不含 6147，会越界崩。
				TryLoadItemTextureForGift(CreateWandIds.ItemType);

				var leftover = player.GetItem(gift, GetItemSettings.PickupItemFromWorld);
				if (!leftover.IsAir)
				{
					Main.NewText("[创造魔杖] 背包满，物品落在地上", 255, 80, 80);
					_giveCooldown = 20;
					return;
				}

				int slot = -1;
				for (int s = 0; s < 50; s++)
				{
					if (player.inventory[s].type == CreateWandIds.ItemType)
					{
						slot = s;
						break;
					}
				}

				Main.NewText(slot >= 0
					? "[创造魔杖] 已获得（背包格 " + (slot + 1).ToString() + "）"
					: "[创造魔杖] 已获得", 100, 220, 255);
				_giveCooldown = 15;
			}
			catch (Exception ex)
			{
				try
				{
					Main.NewText("[创造魔杖] 给予出错，见桌面 CreateWandPatch-harmony.txt", 255, 80, 80);
				}
				catch
				{
					// ignored
				}

				try
				{
					CreateWandPatch.Gameplay.CreateWandLogFileWriter.AppendUtf8(
						System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
							"CreateWandPatch-harmony.txt"),
						System.DateTime.Now + " GiveCreateWand ERR: " + ex + System.Environment.NewLine);
				}
				catch { }
			}
		}

		private static void TryLoadItemTextureForGift(int itemType)
		{
			try
			{
				var assets = TextureAssets.Item;
				if (assets != null && itemType >= 0 && itemType < assets.Length)
				{
					Main.instance.LoadItem(itemType);
					return;
				}

				Main.instance.LoadItem(CreateWandIds.FallbackTemplateItem);
			}
			catch
			{
				try
				{
					Main.instance.LoadItem(CreateWandIds.FallbackTemplateItem);
				}
				catch
				{
					// 无贴图预载亦可能仍可拾取
				}
			}
		}

			/// <summary>为 K 键一键全清打包当前蓝图选区，返回数据和原点。</summary>
			private static bool TryPackFastClear(Terraria.Player p, out BuildingData data, out int ox, out int oy)
			{
				data = null;
				ox = 0;
				oy = 0;

				if (p == null || !p.active)
					return false;

				if (CreateWandSelectionState.SelectedKind != BlueprintKind.DataMap)
					return false;

				CreateWandPngLibrary.EnsureReload();
				if (CreateWandPngLibrary.Entries.Count == 0 ||
				    CreateWandSelectionState.SelectedDatamapIndex < 0 ||
				    CreateWandSelectionState.SelectedDatamapIndex >= CreateWandPngLibrary.Entries.Count)
					return false;

				CreateWandBlueprintEntry entry = CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex];
				BuildingData bd = entry.Data;
				Point mouseTile = Main.MouseWorld.ToTileCoordinates();
				ox = mouseTile.X - bd.Width / 2;
				oy = mouseTile.Y - bd.Height / 2;

				if (ox < 0 || oy < 0 || ox + bd.Width > Main.maxTilesX || oy + bd.Height > Main.maxTilesY)
				{
					Terraria.CombatText.NewText(p.getRect(), Microsoft.Xna.Framework.Color.OrangeRed,
						"[魔杖] 蓝图超出世界边界", false, false);
					return false;
				}

				data = bd;
				return true;
			}

		}
	}
