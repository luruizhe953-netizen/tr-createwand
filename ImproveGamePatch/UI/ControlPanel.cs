using System;
using ImproveGamePatch.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace ImproveGamePatch.UI
{
    public static class ControlPanel
    {
        private static bool _open, _wasKeyDown;
        private static Keys _hotkey = Keys.OemTilde;
        private static Vector2 _pos = new Vector2(750, 260);
        private static Vector2 _size = new Vector2(900, 600);
        private static bool _dragging;
        private static Vector2 _dragOff;
        private const float RowH = 56f, TitleH = 60f;
        private const float Fn = 1.2f, Fd = 0.85f, Ft = 1.3f;
        private const float TogW = 48f, TogH = 26f;

        private static readonly (string key, string name, string desc)[] Feat =
        {
            ("VeinMiner","VeinMiner","Chain-mine connected ores"),
            ("BannerPatch","BannerPatch","Banner buffs from items in inventory"),
            ("FasterExtractinator","FasterExtractinator","Instant silt / slush processing"),
            ("PortableStation","PortableStation","Craft stations + buffs work from bag"),
            ("InfiniteBuff","InfiniteBuff","Carry 1 potion → permanent buff"),
            ("HomeTeleport","HomeTeleport","[H] teleport home"),
        };
        private static int _fc;
        private static Rectangle[] _togRects;
        private static KeyboardState _sharedKb;
        private static int _kbFrame;

        static ControlPanel()
        {
            _fc = Feat.Length;
            _togRects = new Rectangle[_fc];
        }

        public static KeyboardState Kb
        {
            get { _kbFrame++; if (_kbFrame % 12 == 0) _sharedKb = Keyboard.GetState(); return _sharedKb; }
        }

        public static void Update()
        {
            var ks = Kb;
            bool k = ks.IsKeyDown(_hotkey) && !Main.drawingPlayerChat;
            if (k && !_wasKeyDown) _open = !_open;
            _wasKeyDown = k;
            if (!_open) return;

            int mx = Main.mouseX, my = Main.mouseY;
            var panR = new Rectangle((int)_pos.X, (int)_pos.Y, (int)_size.X, (int)_size.Y);
            if (panR.Contains(mx, my)) Main.LocalPlayer.mouseInterface = true;

            var tR = new Rectangle((int)_pos.X, (int)_pos.Y, (int)_size.X, (int)TitleH);
            if (_dragging) { if (Main.mouseLeft) _pos = new Vector2(mx, my) - _dragOff; else _dragging = false; return; }
            if (tR.Contains(mx, my) && Main.mouseLeft && Main.mouseLeftRelease) { Main.mouseLeftRelease = false; _dragging = true; _dragOff = new Vector2(mx, my) - _pos; return; }
            int cX = (int)(_pos.X + _size.X - 40), cY = (int)_pos.Y + 8;
            if (new Rectangle(cX, cY, 32, 32).Contains(mx, my) && Main.mouseLeft && Main.mouseLeftRelease) { Main.mouseLeftRelease = false; _open = false; return; }
            if (!panR.Contains(mx, my)) return;

            for (int i = 0; i < _fc; i++)
            {
                if (!_togRects[i].Contains(mx, my)) continue;
                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    var (key, _, _) = Feat[i];
                    if (mx - _togRects[i].X < 60) Config.SetAndSave(key, !Config.Get(key, true));
                }
                break;
            }
        }

        public static void Draw(SpriteBatch sb)
        {
            if (!_open) return;
            var px = TextureAssets.MagicPixel.Value;
            var f = FontAssets.MouseText.Value;
            var bgAlpha = 220;

            Fill(sb, px, _pos.X + 2, _pos.Y + 2, _size.X - 4, _size.Y - 4, new Color(14, 14, 38, bgAlpha));
            Border(sb, px, _pos.X, _pos.Y, _size.X, _size.Y, new Color(120, 120, 190, 255));
            Border(sb, px, _pos.X + 2, _pos.Y + 2, _size.X - 4, _size.Y - 4, new Color(60, 60, 100, 180));

            Fill(sb, px, _pos.X + 2, _pos.Y + 2, _size.X - 4, TitleH - 4, new Color(40, 40, 90, 250));
            Chat(sb, f, "ImproveGamePatch  (~ toggle / drag title)", _pos.X + 18, _pos.Y + 14, Color.White, Ft);
            int cX = (int)(_pos.X + _size.X - 40), cY = (int)_pos.Y + 12;
            Chat(sb, f, "X", cX + 11, cY + 4, Color.White, 1.1f);

            float lx = _pos.X + 20, curY = _pos.Y + TitleH + 12;
            for (int i = 0; i < _fc; i++)
            {
                var (key, name, desc) = Feat[i];
                bool on = Config.Get(key, true);
                _togRects[i] = new Rectangle((int)lx, (int)curY, (int)(_size.X - 40), (int)RowH);
                bool hov = _togRects[i].Contains(Main.mouseX, Main.mouseY);
                if (hov) Fill(sb, px, _togRects[i], new Color(50, 50, 80, 90));

                float tx = lx + 4, ty = curY + 12;
                Color bgOn = hov ? new Color(70, 210, 90, 250) : new Color(50, 170, 65, 225);
                Color bgOff = hov ? new Color(110, 65, 65, 230) : new Color(80, 48, 48, 210);
                Fill(sb, px, tx + 1, ty + 1, TogW - 2, TogH - 2, on ? bgOn : bgOff);
                float bx = on ? tx + TogW - 16 : tx + 4;
                Fill(sb, px, bx, ty + 4, 12, TogH - 8, Color.White);

                float nx = lx + 60;
                Chat(sb, f, name, nx, curY + 7, hov ? new Color(220, 230, 255) : (on ? Color.White : Color.Gray * 0.4f), Fn);
                Chat(sb, f, desc, nx, curY + 28, (on ? Color.White : Color.Gray * 0.35f) * 0.6f, Fd);
                curY += RowH;
            }

            // Cursor
            int mx = Main.mouseX, my = Main.mouseY;
            int[] le = {14,12,10,8,6,5,3}, ox = {0,0,0,0,0,1,2}, oy = {0,1,2,3,4,5,6};
            for (int k = 0; k < 7; k++) Fill(sb, px, mx + ox[k] - 1, my + oy[k] - 1, le[k] + 2, 3, Color.Black);
            for (int k = 0; k < 7; k++) Fill(sb, px, mx + ox[k], my + oy[k], le[k], 1, Color.White);
        }

        static void Fill(SpriteBatch sb, Texture2D px, float x, float y, float w, float h, Color c) => sb.Draw(px, new Rectangle((int)x, (int)y, (int)w, (int)h), c);
        static void Fill(SpriteBatch sb, Texture2D px, Rectangle r, Color c) => sb.Draw(px, r, c);
        static void Border(SpriteBatch sb, Texture2D px, float x, float y, float w, float h, Color c) { sb.Draw(px, new Rectangle((int)x, (int)y, (int)w, 1), c); sb.Draw(px, new Rectangle((int)x, (int)(y + h - 1), (int)w, 1), c); sb.Draw(px, new Rectangle((int)x, (int)y, 1, (int)h), c); sb.Draw(px, new Rectangle((int)(x + w - 1), (int)y, 1, (int)h), c); }
        static void Chat(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont f, string t, float x, float y, Color c, float sc) => ChatManager.DrawColorCodedStringWithShadow(sb, f, t, new Vector2(x, y), c, 0f, Vector2.Zero, Vector2.One * sc);
        public static bool IsOpen => _open;
    }
}
