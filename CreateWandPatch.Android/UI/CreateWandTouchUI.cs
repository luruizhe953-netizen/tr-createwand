using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using CreateWandPatch.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CreateWandPatch.UI
{
    /// <summary>
    /// Visible touch zone overlay for CreateWandPatch on Android.
    /// 4 colored rectangle zones at screen bottom when Create Wand is held.
    /// Zone 1: Menu (blue) | Zone 2: MP Mode (yellow) | Zone 3: Place Toggle (green) | Zone 4: Speed -/+ (purple)
    /// </summary>
    public static class CreateWandTouchUI
    {
        private static Texture2D _whiteTex;
        private static bool _wasTouching;
        private static bool _active;
        private static Rectangle[] _rects = new Rectangle[4];
        private static Color[] _colors;
        private static System.Action[] _actions;

        public static bool IsActive => _active;

        /// <summary>Call each frame from HotkeysPatch Postfix.</summary>
        public static void ProcessTouchInput()
        {
            var player = Main.LocalPlayer;
            _active = player != null && player.active && !player.dead && !Main.gameMenu &&
                      player.inventory[player.selectedItem].type == CreateWandIds.ItemType;

            if (!_active) return;

            EnsureWhiteTex();
            UpdateLayout();
            DrawZoneRects();

            // Tap detection
            bool touching = Main.mouseLeft;
            bool tapped = touching && !_wasTouching;

            if (tapped)
            {
                int mx = Main.mouseX;
                int my = Main.mouseY;

                // Zone 3 (speed) is split left/right
                if (_rects.Length > 3 && _rects[3].Contains(mx, my))
                {
                    if (mx < _rects[3].X + _rects[3].Width / 2)
                    {
                        int spd = CreateWandStaggeredPlacementQueue.AdjustHandheldSpeed(+1);
                        Feedback("Slower: " + spd + "f");
                    }
                    else
                    {
                        int spd = CreateWandStaggeredPlacementQueue.AdjustHandheldSpeed(-1);
                        Feedback("Faster: " + spd + "f");
                    }
                }
                else
                {
                    for (int i = 0; i < _rects.Length - 1; i++)
                    {
                        if (_rects[i].Contains(mx, my))
                        {
                            _actions[i]?.Invoke();
                            break;
                        }
                    }
                }
            }

            _wasTouching = touching;
        }

        static void UpdateLayout()
        {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            int bw = 110, bh = 48, gap = 6;
            int startX = (sw - (bw * 4 + gap * 3)) / 2;
            int y = sh - bh - 16;

            _rects[0] = new Rectangle(startX, y, bw, bh);
            startX += bw + gap;
            _rects[1] = new Rectangle(startX, y, bw, bh);
            startX += bw + gap;
            _rects[2] = new Rectangle(startX, y, bw, bh);
            startX += bw + gap;
            _rects[3] = new Rectangle(startX, y, bw, bh);

            _colors = new[]
            {
                new Color(40, 60, 140, 150),   // blue: menu/mode
                GetMpColor(),                    // yellow/red: MP mode
                GetPlaceColor(),                 // green/gray: place toggle
                new Color(80, 40, 120, 150),   // purple: speed
            };

            _actions = new System.Action[]
            {
                OpenMenu,
                CycleMpMode,
                TogglePlacement,
                null, // speed handled separately
            };
        }

        static void DrawZoneRects()
        {
            var sb = Main.spriteBatch;
            if (sb == null) return;

            for (int i = 0; i < _rects.Length; i++)
            {
                var r = _rects[i];
                var c = _colors[i];

                // Fill
                sb.Draw(_whiteTex, r, c);

                // Border (2px white at 50% alpha)
                var border = new Color(255, 255, 255, 80);
                sb.Draw(_whiteTex, new Rectangle(r.X, r.Y, r.Width, 2), border);
                sb.Draw(_whiteTex, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), border);
                sb.Draw(_whiteTex, new Rectangle(r.X, r.Y, 2, r.Height), border);
                sb.Draw(_whiteTex, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), border);

                // Speed zone: vertical split line
                if (i == 3)
                {
                    int mid = r.X + r.Width / 2;
                    sb.Draw(_whiteTex, new Rectangle(mid - 1, r.Y + 6, 2, r.Height - 12), Color.Gray);
                }
            }
        }

        static void EnsureWhiteTex()
        {
            if (_whiteTex != null) return;
            try
            {
                var gd = Main.instance?.GraphicsDevice;
                if (gd == null) return;
                _whiteTex = new Texture2D(gd, 1, 1);
                _whiteTex.SetData(new[] { Color.White }, 0, 1);
            }
            catch { /* Not available yet */ }
        }

        // ---- Zone actions ----

        static void OpenMenu()
        {
            // Right-click equivalent for single player: toggle between place on/off
            // In MP: open mode panel
            if (Main.netMode == 1)
            {
                // Cycle MP placement modes via the mode panel
                Terraria.UI.IngameFancyUI.OpenUIState(new CreateWandModePanel());
            }
            Feedback("Menu opened");
        }

        static void CycleMpMode()
        {
            // N key: cycle MP placement modes via MpPlacementMode
            if (Main.netMode != 1)
            {
                CreateWandSelectionState.PlacementEnabled = !CreateWandSelectionState.PlacementEnabled;
                Feedback(CreateWandSelectionState.PlacementEnabled ? "Place ON" : "Place OFF");
            }
            else
            {
                // Cycle: LocalOnly → Handheld → DeleteOnly → LocalOnly
                switch (CreateWandSelectionState.MpPlacementMode)
                {
                    case MpClientPlacementMode.LocalOnlyNoNet:
                        CreateWandSelectionState.MpPlacementMode = MpClientPlacementMode.VanillaHandheldThenExplicitMsg17;
                        Feedback("Handheld");
                        break;
                    case MpClientPlacementMode.VanillaHandheldThenExplicitMsg17:
                        CreateWandSelectionState.MpPlacementMode = MpClientPlacementMode.DeleteOnly;
                        Feedback("Delete Only");
                        break;
                    case MpClientPlacementMode.DeleteOnly:
                        CreateWandSelectionState.MpPlacementMode = MpClientPlacementMode.LocalOnlyNoNet;
                        Feedback("Local Only");
                        break;
                }
                CreateWandSelectionState.NormalizeMpPlacementMode();
            }
            CreateWandWorldPreview.InvalidateCache();
        }

        static void TogglePlacement()
        {
            // Backslash: toggle placement on/off
            CreateWandSelectionState.PlacementEnabled = !CreateWandSelectionState.PlacementEnabled;
            if (CreateWandSelectionState.MpDeleteOnly && CreateWandSelectionState.PlacementEnabled)
                CreateWandSelectionState.MpPlacementMode = MpClientPlacementMode.VanillaHandheldThenExplicitMsg17;
            Feedback(CreateWandSelectionState.PlacementEnabled ? "Place ON" : "Place OFF");
            CreateWandWorldPreview.InvalidateCache();
        }

        // ---- Visual feedback ----

        static void Feedback(string msg)
        {
            var p = Main.LocalPlayer;
            if (p != null)
                CombatText.NewText(p.getRect(), Color.White, msg, false, true);
        }

        // ---- Color helpers ----

        static Color GetMpColor()
        {
            if (Main.netMode != 1) return new Color(60, 60, 100, 150); // dim = local
            if (CreateWandSelectionState.MpDeleteOnly) return new Color(200, 60, 40, 160); // red = delete
            if (CreateWandSelectionState.PlacementEnabled) return new Color(200, 180, 40, 160); // yellow = handheld
            return new Color(80, 80, 120, 150); // dim = local only
        }

        static Color GetPlaceColor()
        {
            return CreateWandSelectionState.PlacementEnabled
                ? new Color(40, 160, 60, 160)  // green = ON
                : new Color(80, 80, 80, 130);   // gray = OFF
        }
    }
}
