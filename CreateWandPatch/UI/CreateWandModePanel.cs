using System;
using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using CreateWandPatch.Rendering;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace CreateWandPatch.UI
{
	/// <summary>创造魔杖模式面板：内置预设、PNG 蓝图、放置前清空。通过 <see cref="IngameFancyUI.OpenUIState"/> 打开，与 ImproveGame 思路一致，不 new UserInterface。</summary>
	public class CreateWandModePanel : UIState
	{
		private UIText _selectionLabel;
		private UITextPanel<string> _clearToggle;
		private UITextPanel<string> _paceToggle;
		private UITextPanel<string> _preciseToggle;

		private const string TTitle = "创造魔杖 — 模式";
		private const string THint = "左键在光标处放置。蓝图目录：文档\\My Games\\Terraria\\CreateWand\\（*.png / *.qotstruct / *.cwmap）。热键 ; 与下方按钮：蓝图精确放置。";
		private const string TPresetSection = "内置预设";
		private const string TDatamapSection = "蓝图 PNG";
		private const string TReload = "刷新列表";
		private const string TNoDatamaps = "未发现 PNG，可将色图放入上述文件夹。";
		private const string TMoreFiles = "…另有 {0} 个未列出";
		private const string TBack = "关闭";
		private const string TCurrent = "当前：";
		private const string TPresetStone = "石块 3×3";
		private const string TPresetPlatform = "木平台 ×5";
		private const string TPresetDirt = "泥土 2×2";
		private const string TBoxExport = "框选区域 → 导出 PNG";
		private const string THintExport = "热键 B：开始/取消两点框选；导出至 CreateWand 文件夹";
		private const string TPaceFast = "铺设速度：快速（无限距离·整图一帧）";
		private const string TPaceStagger = "铺设速度：逐格（原版距离·每帧多格）";
		private const string TMpInlineHint = "联机发包：按 N 打开列表";
		private const string TPreciseOn = "蓝图精确放置：开（cwmap / qot / PNG+同名 cwmap → 图格 1:1）";
		private const string TPreciseOff = "蓝图精确放置：关（色图·分类铺设）";

		public override void OnInitialize()
		{
			RemoveAllChildren();
			CreateWandPngLibrary.EnsureReload();
			int datamapCount = CreateWandPngLibrary.Entries.Count;
			float panelHeight = 580f + Math.Min(datamapCount, 12) * 34f;
			if (Main.netMode == 1)
				panelHeight += 52f;
			if (panelHeight > 720f)
				panelHeight = 720f;

			var root = new UIElement
			{
				Width = { Pixels = 560f },
				Height = { Pixels = panelHeight },
				HAlign = 0.5f,
				VAlign = 0.5f
			};
			Append(root);

			var panel = new UIPanel
			{
				Width = { Precent = 1f },
				Height = { Precent = 1f },
				BackgroundColor = new Color(33, 43, 79) * 0.85f
			};
			root.Append(panel);

			var title = new UIText(TTitle, 1f, false) { HAlign = 0.5f, Top = { Pixels = 10f } };
			panel.Append(title);

			var hint = new UIText(THint, 0.78f, true)
			{
				HAlign = 0.5f,
				Top = { Pixels = 38f },
				Width = { Precent = 1f, Pixels = -36f },
				WrappedTextBottomPadding = 8f
			};
			panel.Append(hint);

			var presetHeader = new UIText(TPresetSection, 0.95f, false)
			{
				Top = { Pixels = 100f },
				PaddingLeft = 8f
			};
			panel.Append(presetHeader);

			float pt = 128f;
			const float gap = 44f;
			panel.Append(MakePresetButton(pt, PlacePreset.Stone3x3, TPresetStone));
			panel.Append(MakePresetButton(pt + gap, PlacePreset.WoodPlatform5, TPresetPlatform));
			panel.Append(MakePresetButton(pt + gap * 2f, PlacePreset.Dirt2x2, TPresetDirt));

			float clearTop = pt + gap * 3f + 8f;
			_clearToggle = new UITextPanel<string>(ClearToggleText(), 0.78f, true)
			{
				Width = { Precent = 1f, Pixels = -40f },
				Height = { Pixels = 34f },
				HAlign = 0.5f,
				Top = { Pixels = clearTop }
			};
			_clearToggle.OnLeftClick += delegate
			{
				CreateWandSelectionState.NextClearAreaMode();
				_clearToggle.SetText(ClearToggleText(), 0.78f, true);
				CreateWandWorldPreview.InvalidateCache();
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
			};
			panel.Append(_clearToggle);

			float paceTop = clearTop + 40f;
			_paceToggle = new UITextPanel<string>(PaceToggleText(), 0.78f, true)
			{
				Width = { Precent = 1f, Pixels = -40f },
				Height = { Pixels = 34f },
				HAlign = 0.5f,
				Top = { Pixels = paceTop }
			};
			_paceToggle.OnLeftClick += delegate
			{
				CreateWandSelectionState.ToggleStaggeredPlacement();
				_paceToggle.SetText(PaceToggleText(), 0.78f, true);
				CreateWandWorldPreview.InvalidateCache();
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
			};
			panel.Append(_paceToggle);

			float preciseTop = paceTop + 40f;
			_preciseToggle = new UITextPanel<string>(PreciseToggleText(), 0.78f, true)
			{
				Width = { Precent = 1f, Pixels = -40f },
				Height = { Pixels = 34f },
				HAlign = 0.5f,
				Top = { Pixels = preciseTop }
			};
			_preciseToggle.OnLeftClick += delegate
			{
				CreateWandSelectionState.EnablePreciseCwmapPlacement = !CreateWandSelectionState.EnablePreciseCwmapPlacement;
				_preciseToggle.SetText(PreciseToggleText(), 0.78f, true);
				CreateWandWorldPreview.InvalidateCache();
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
			};
			panel.Append(_preciseToggle);

			float exportTop = preciseTop + 40f;
			if (Main.netMode == 1)
			{
				var mpLine = new UIText(TMpInlineHint, 0.68f, true)
				{
					HAlign = 0.5f,
					Top = { Pixels = preciseTop + 42f },
					Width = { Precent = 1f, Pixels = -36f },
					WrappedTextBottomPadding = 4f
				};
				panel.Append(mpLine);
				exportTop = preciseTop + 88f;
			}

			var exportBtn = new UITextPanel<string>(TBoxExport, 0.78f, true)
			{
				Width = { Precent = 1f, Pixels = -40f },
				Height = { Pixels = 34f },
				HAlign = 0.5f,
				Top = { Pixels = exportTop }
			};
			exportBtn.OnLeftClick += delegate
			{
				CreateWandBoxExportState.Begin();
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
				IngameFancyUI.Close(false);
				Main.NewText(THintExport, 200, 220, 160);
			};
			panel.Append(exportBtn);

			float dmTop = exportTop + 44f;
			var dmHeader = new UIText(TDatamapSection, 0.95f, false)
			{
				Top = { Pixels = dmTop },
				PaddingLeft = 8f
			};
			panel.Append(dmHeader);

			var reloadBtn = new UITextPanel<string>(TReload, 0.75f, true)
			{
				Width = { Pixels = 200f },
				Height = { Pixels = 32f },
				HAlign = 1f,
				Top = { Pixels = dmTop + 26f },
				PaddingRight = 12f
			};
			reloadBtn.OnLeftClick += delegate
			{
				CreateWandPngLibrary.Reload();
				CreateWandWorldPreview.InvalidateCache();
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
				OnInitialize();
			};
			panel.Append(reloadBtn);

			float row = dmTop + 72f;
			if (datamapCount == 0)
			{
				var empty = new UIText(TNoDatamaps, 0.8f, true)
				{
					Top = { Pixels = row },
					Width = { Precent = 1f, Pixels = -28f },
					PaddingLeft = 10f
				};
				panel.Append(empty);
				row += 56f;
			}
			else
			{
				int shown = 0;
				for (int i = 0; i < CreateWandPngLibrary.Entries.Count && shown < 16; i++)
				{
					int index = i;
					var entry = CreateWandPngLibrary.Entries[i];
					string label = entry.Name;
					if (label.Length > 28)
						label = label.Substring(0, 25) + "...";

					var rowBtn = new UITextPanel<string>(label, 0.78f, true)
					{
						Width = { Precent = 1f, Pixels = -24f },
						Height = { Pixels = 30f },
						HAlign = 0.5f,
						Top = { Pixels = row }
					};
					rowBtn.OnLeftClick += delegate
					{
						CreateWandSelectionState.SelectedKind = BlueprintKind.DataMap;
						CreateWandSelectionState.SelectedDatamapIndex = index;
						CreateWandWorldPreview.InvalidateCache();
						RefreshSelectionLabel();
						SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
					};
					panel.Append(rowBtn);
					row += 34f;
					shown++;
				}

				if (datamapCount > 16)
				{
					var more = new UIText(string.Format(TMoreFiles, datamapCount - 16), 0.75f, false)
					{
						Top = { Pixels = row },
						PaddingLeft = 12f
					};
					panel.Append(more);
					row += 22f;
				}
			}

			row += 16f;
			_selectionLabel = new UIText("", 0.85f, false)
			{
				HAlign = 0.5f,
				Width = { Precent = 1f, Pixels = -32f },
				Top = { Pixels = row }
			};
			panel.Append(_selectionLabel);
			RefreshSelectionLabel();
			row += 40f;

			var back = new UITextPanel<string>(TBack, 0.85f, true)
			{
				Width = { Pixels = 160f },
				Height = { Pixels = 40f },
				HAlign = 0.5f,
				Top = { Pixels = row }
			};
			back.OnLeftClick += delegate
			{
				SoundEngine.PlaySound(11, -1, -1, 1, 1f, 0f);
				IngameFancyUI.Close(false);
			};
			panel.Append(back);

			float footerBottom = row + 48f;
			float finalHeight = Math.Min(720f, Math.Max(panelHeight, footerBottom + 24f));
			root.Height.Set(finalHeight, 0f);
		}

		private static string ClearToggleText() =>
			"放置前清空：" + CreateWandSelectionState.GetClearAreaModeLabel() + "（] 切换）";

		private static string PaceToggleText()
		{
			if (!CreateWandSelectionState.UseStaggeredPlacement)
				return TPaceFast + "（点击切逐格）";
			if (Main.netMode == 1)
				return "铺设速度：逐格（" + CreateWandStaggeredPlacementQueue.GetStaggeredSpeedHintForCombatText() + "）（点击切快速）";
			return TPaceStagger + "（点击切快速）";
		}

		private static string PreciseToggleText() =>
			CreateWandSelectionState.EnablePreciseCwmapPlacement
				? TPreciseOn + "（点击关闭）"
				: TPreciseOff + "（点击开启）";

		private UITextPanel<string> MakePresetButton(float top, PlacePreset preset, string label)
		{
			var btn = new UITextPanel<string>(label, 0.8f, true)
			{
				Width = { Precent = 1f, Pixels = -40f },
				Height = { Pixels = 34f },
				HAlign = 0.5f,
				Top = { Pixels = top }
			};
			btn.OnLeftClick += delegate
			{
				CreateWandSelectionState.SelectedKind = BlueprintKind.BuiltinPreset;
				CreateWandSelectionState.SelectedPreset = preset;
				CreateWandWorldPreview.InvalidateCache();
				RefreshSelectionLabel();
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
			};
			return btn;
		}

		private void RefreshSelectionLabel()
		{
			if (_selectionLabel == null)
				return;

			if (CreateWandSelectionState.SelectedKind == BlueprintKind.DataMap &&
			    CreateWandPngLibrary.Entries.Count > 0 &&
			    CreateWandSelectionState.SelectedDatamapIndex >= 0 &&
			    CreateWandSelectionState.SelectedDatamapIndex < CreateWandPngLibrary.Entries.Count)
			{
				var entry = CreateWandPngLibrary.Entries[CreateWandSelectionState.SelectedDatamapIndex];
				string name = entry.Name;
				_selectionLabel.SetText(TCurrent + name + "（蓝图，" + SourceDisplay(entry.Source) + "）");
				return;
			}

			string presetLabel = TPresetStone;
			switch (CreateWandSelectionState.SelectedPreset)
			{
				case PlacePreset.Stone3x3:
					presetLabel = TPresetStone;
					break;
				case PlacePreset.WoodPlatform5:
					presetLabel = TPresetPlatform;
					break;
				case PlacePreset.Dirt2x2:
					presetLabel = TPresetDirt;
					break;
			}

			_selectionLabel.SetText(TCurrent + presetLabel);
		}

		private static string SourceDisplay(CreateWandBlueprintSource source)
		{
			switch (source)
			{
				case CreateWandBlueprintSource.PngDataMap: return "PNG（+侧写 cwmap 可 1:1）";
				case CreateWandBlueprintSource.QotStruct: return "qotstruct（精确门 1:1）";
				case CreateWandBlueprintSource.CwMap: return "cwmap";
				default: return "未知";
			}
		}
	}
}
