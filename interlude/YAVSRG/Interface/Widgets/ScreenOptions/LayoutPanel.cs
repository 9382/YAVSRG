﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Input;
using Prelude.Utilities;
using Prelude.Gameplay.DifficultyRating;
using Interlude.Graphics;
using Interlude.IO;


namespace Interlude.Interface.Widgets
{
    class LayoutPanel : Widget
    {
        private Widget selectKeyMode, selectLayout;
        private KeyBinder[] binds = new KeyBinder[10];
        private ColorPicker[] colors = new ColorPicker[10];
        private int keyMode = (int)Game.Options.Profile.DefaultKeymode + 3;
        private float width;

        protected class ColorPicker : Widget
        {
            string label;
            Action<int> select;
            Func<int> get;
            int max;

            public ColorPicker(string label, Action<int> select, Func<int> get, int max)
            {
                Change(label, select, get, max);
            }

            public void Change(string label, Action<int> select, Func<int> get, int max)
            {
                this.label = label;
                this.select = select;
                this.get = get;
                this.max = max;
            }

            public override void Draw(Rect bounds)
            {
                base.Draw(bounds);
                bounds = GetBounds(bounds);
                SpriteBatch.Draw(new RenderTarget(Game.Options.Themes.GetNoteSkinTexture("note"), bounds, System.Drawing.Color.White, 1, get()));
            }

            public override void Update(Rect bounds)
            {
                base.Update(bounds);
                bounds = GetBounds(bounds);
                if (ScreenUtils.MouseOver(bounds))
                {
                    if (Input.MouseClick(MouseButton.Left))
                    {
                        select(Utils.Modulus(get() + 1, max));
                    }
                    else if (Input.MouseClick(MouseButton.Right))
                    {
                        select(Utils.Modulus(get() - 1, max));
                    }
                    Game.Screens.Toolbar.SetTooltip(label, "");
                }
            }
        }

        public LayoutPanel()
        {
            width = ScreenUtils.ScreenWidth * 2 - 600;
            selectKeyMode = new TextPicker("Keys", new string[] { "3K", "4K", "5K", "6K", "7K", "8K", "9K", "10K" }, (int)Game.Options.Profile.DefaultKeymode, (i) => { ChangeKeyMode(i + 3, width); })
                .TL_DeprecateMe(-50, 100, AnchorType.CENTER, AnchorType.MIN).BR_DeprecateMe(50, 150, AnchorType.CENTER, AnchorType.MIN);
            for (int i = 0; i < 10; i++)
            {
                var j = i;
                void set(Bind bind) { Game.Options.Profile.KeyBinds[keyMode - 3][j] = (KeyBind)bind; }
                Bind get() => Game.Options.Profile.KeyBinds[keyMode - 3][j];
                binds[i] = new KeyBinder("Column " + (i + 1).ToString(), new SetterGetter<Bind>(set, get)) { AllowAltBinds = false };
                AddChild(binds[i]);
                colors[i] = new ColorPicker("", null, null, 1);
                AddChild(colors[i]);
            }
            AddChild(selectKeyMode);
            AddChild(new BoolPicker("Different colors per keymode", !Game.Options.Profile.ColorStyle.UseForAllKeyModes, (i) => { Game.Options.Profile.ColorStyle.UseForAllKeyModes = !i; Refresh(); })
                .TL_DeprecateMe(-500, 525, AnchorType.CENTER, AnchorType.MIN).BR_DeprecateMe(-200, 575, AnchorType.CENTER, AnchorType.MIN));
            AddChild(new SimpleButton("Change Theme", () => { Game.Screens.AddDialog(new Dialogs.ThemeSelectDialog((s) => { })); }, () => false, null).Reposition(200, 0.5f, 525, 0, 500, 0.5f, 575, 0));
            var arr = Game.Options.Themes.NoteSkins.Keys.ToArray();
            AddChild(new TextPicker("Noteskin", arr, Math.Max(0, Array.IndexOf(arr, Game.Options.Profile.NoteSkin)), (i) =>
            {
                Game.Options.Profile.NoteSkin = arr[i];
                Game.Options.Themes.Unload(); Game.Options.Themes.Load();
                ChangeKeyMode(keyMode, width);
            }).Reposition(200, 0.5f, 600, 0, 500, 0.5f, 650, 0));
            Refresh();
        }

        public void Refresh()
        {
            if (Game.Options.Profile.KeymodePreference)
            {
                keyMode = (int)Game.Options.Profile.PreferredKeymode + 3;
                selectKeyMode.SetState(WidgetState.DISABLED);
            }
            else
            {
                selectKeyMode.SetState(WidgetState.NORMAL);
            }
            ChangeKeyMode(keyMode, width);
        }

        private Action<int> ColorSetter(int i, int k)
        {
            return (s) => { Game.Options.Profile.ColorStyle.SetColorIndex(i, k, s); };
        }

        private Func<int> ColorGetter(int i, int k)
        {
            return () => { return Game.Options.Profile.ColorStyle.GetColorIndex(i, k); };
        }

        private void ChangeKeyMode(int k, float Width)
        {
            for (int i = 0; i < 10; i++)
            {
                binds[i].SetState(WidgetState.DISABLED);
                colors[i].SetState(WidgetState.DISABLED);
            }
            keyMode = k;
            int c = k * Game.Options.Theme.ColumnWidth > Width ? (int)(Width / k) : Game.Options.Theme.ColumnWidth;
            int start = -k * c / 2;
            for (int i = 0; i < k; i++)
            {
                binds[i].SetState(WidgetState.NORMAL);
                binds[i].TL_DeprecateMe(start + i * c, 200, AnchorType.CENTER, AnchorType.MIN).BR_DeprecateMe(start + c + i * c, 250, AnchorType.CENTER, AnchorType.MIN);
            }

            int colorCount = Game.Options.Profile.ColorStyle.GetColorCount(k);
            int availableColors = Game.Options.Theme.NoteColorCount();
            c = colorCount * Game.Options.Theme.ColumnWidth > Width ? (int)(Width / colorCount) : Game.Options.Theme.ColumnWidth;
            start = -colorCount * c / 2;
            int keymodeIndex = Game.Options.Profile.ColorStyle.UseForAllKeyModes ? 0 : k;
            for (int i = 0; i < colorCount; i++)
            {
                colors[i].Change(Game.Options.Profile.ColorStyle.GetDescription(i), ColorSetter(i, keymodeIndex), ColorGetter(i, keymodeIndex), availableColors);
                colors[i].SetState(WidgetState.NORMAL);
                colors[i].TL_DeprecateMe(start + i * c, 300, AnchorType.CENTER, AnchorType.MIN).BR_DeprecateMe(start + c + i * c, 300 + Game.Options.Theme.ColumnWidth, AnchorType.CENTER, AnchorType.MIN);
            }
            if (selectLayout != null)
            {
                Children.Remove(selectLayout);
            }
            List<KeyLayout.Layout> layouts = KeyLayout.GetPossibleLayouts(k);
            string[] layoutNames = layouts.Select((x) => KeyLayout.GetLayoutName(x, k)).ToArray();
            Children.Add(selectLayout = new TextPicker("Keyboard layout", layoutNames, Math.Max(0, layouts.IndexOf(Game.Options.Profile.KeymodeLayouts[k])), (i) => { Game.Options.Profile.KeymodeLayouts[k] = layouts[i]; })
                .TL_DeprecateMe(-150, 600, AnchorType.CENTER, AnchorType.MIN).BR_DeprecateMe(150, 650, AnchorType.CENTER, AnchorType.MIN));
        }
    }
}