using CreateWandPatch.Content;
using CreateWandPatch.Rendering;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace CreateWandPatch.UI
{
	/// <summary>联机客户端发包模式列表（与 <see cref="CreateWandModePanel"/> 内预设行同款）；热键 N 打开。</summary>
	public sealed class CreateWandMpPlacementModeListPanel : UIState
	{
		private const string TTitle = "联机发包（客户端）";
		private const string THint = "服是否接受因插件而异；可多试几种。";
		private const string TBack = "关闭";
		private const float RowH = 34f;

		public override void OnInitialize()
		{
			CreateWandSelectionState.NormalizeMpPlacementMode();
			RemoveAllChildren();

			float panelH = 48f + CreateWandSelectionState.MpPlacementModeCount * RowH + 2 * RowH + 240f + 52f + 44f;
			var root = new UIElement
			{
				Width = { Pixels = 520f },
				Height = { Pixels = panelH },
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

			float row0 = 44f;
			for (int mi = 0; mi < CreateWandSelectionState.MpPlacementModeCount; mi++)
			{
				MpClientPlacementMode mode = (MpClientPlacementMode)mi;
				bool cur = CreateWandSelectionState.MpPlacementMode == mode;
				string label = (cur ? "▶ " : "　 ") + CreateWandSelectionState.GetMpPlacementModeShortLabel(mode);

				var rowBtn = new UITextPanel<string>(label, 0.76f, true)
				{
					Width = { Precent = 1f, Pixels = -36f },
					Height = { Pixels = 32f },
					HAlign = 0.5f,
					Top = { Pixels = row0 + mi * RowH }
				};
				rowBtn.OnLeftClick += delegate
				{
					CreateWandSelectionState.MpPlacementMode = mode;
					CreateWandWorldPreview.InvalidateCache();
					SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
					OnInitialize();
				};
				panel.Append(rowBtn);
			}

			float togglesTop = row0 + CreateWandSelectionState.MpPlacementModeCount * RowH + 6f;
			var outOfRangeToggle = new UITextPanel<string>(
				"越距放置尝试：" + (CreateWandSelectionState.MpAllowOutOfRangePlacementAttempts ? "开" : "关"),
				0.68f, true)
			{
				Width = { Precent = 1f, Pixels = -36f },
				Height = { Pixels = 30f },
				HAlign = 0.5f,
				Top = { Pixels = togglesTop }
			};
			outOfRangeToggle.OnLeftClick += delegate
			{
				CreateWandSelectionState.MpAllowOutOfRangePlacementAttempts =
					!CreateWandSelectionState.MpAllowOutOfRangePlacementAttempts;
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
				OnInitialize();
			};
			panel.Append(outOfRangeToggle);

			var autoRefillToggle = new UITextPanel<string>(
				"自动补给(开箱)：" + (CreateWandSelectionState.MpAutoRefillFromOpenedChest ? "开" : "关"),
				0.68f, true)
			{
				Width = { Precent = 1f, Pixels = -36f },
				Height = { Pixels = 30f },
				HAlign = 0.5f,
				Top = { Pixels = togglesTop + RowH }
			};
			autoRefillToggle.OnLeftClick += delegate
			{
				CreateWandSelectionState.MpAutoRefillFromOpenedChest =
					!CreateWandSelectionState.MpAutoRefillFromOpenedChest;
				SoundEngine.PlaySound(12, -1, -1, 1, 1f, 0f);
				OnInitialize();
			};
			panel.Append(autoRefillToggle);

			float refTop = togglesTop + 2 * RowH + 8f;
			var refTitle = new UIText(CreateWandSelectionState.GetMpPlacementOutgoingReferenceTitle(), 0.68f, false)
			{
				HAlign = 0.5f,
				Top = { Pixels = refTop },
				PaddingLeft = 8f
			};
			panel.Append(refTitle);

			var refBody = new UIText(CreateWandSelectionState.GetMpPlacementOutgoingReferenceBody(), 0.54f, true)
			{
				HAlign = 0.5f,
				Top = { Pixels = refTop + 22f },
				Width = { Precent = 1f, Pixels = -28f },
				WrappedTextBottomPadding = 6f
			};
			panel.Append(refBody);

			var hint = new UIText(THint, 0.65f, true)
			{
				HAlign = 0.5f,
				Top = { Pixels = refTop + 208f },
				Width = { Precent = 1f, Pixels = -32f },
				WrappedTextBottomPadding = 4f
			};
			panel.Append(hint);

			var back = new UITextPanel<string>(TBack, 0.85f, true)
			{
				Width = { Pixels = 160f },
				Height = { Pixels = 40f },
				HAlign = 0.5f,
				Top = { Pixels = refTop + 242f }
			};
			back.OnLeftClick += delegate
			{
				SoundEngine.PlaySound(11, -1, -1, 1, 1f, 0f);
				IngameFancyUI.Close(false);
			};
			panel.Append(back);
		}
	}
}
