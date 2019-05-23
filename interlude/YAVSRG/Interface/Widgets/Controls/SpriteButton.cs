﻿using System;
using Interlude.Graphics;

namespace Interlude.Interface.Widgets
{
    public class SpriteButton : Widget
    {
        protected string icon;
        protected string text;
        protected Action action;
        protected Animations.AnimationColorMixer color;

        public SpriteButton(string sprite, string label, Action onClick) : base()
        {
            icon = sprite;
            text = label;
            action = onClick;
            Animation.Add(color = new Animations.AnimationColorMixer(Game.Screens.BaseColor));
        }

        public override void Draw(Rect bounds)
        {
            base.Draw(bounds);
            bounds = GetBounds(bounds);
            SpriteBatch.Draw(icon, bounds, color);
            //SpriteBatch.Font1.DrawCentredText(text, 30f, (left + right) / 2, (top + bottom) / 2 - 20, Game.Options.Theme.MenuFont);
        }

        public override void Update(Rect bounds)
        {
            base.Update(bounds);
            bounds = GetBounds(bounds);
            if (ScreenUtils.MouseOver(bounds))
            {
                color.Target(Game.Screens.HighlightColor);
                Game.Screens.Toolbar.SetTooltip(text);
                if (IO.Input.MouseClick(OpenTK.Input.MouseButton.Left))
                {
                    Game.Audio.PlaySFX("click");
                    action();
                }
            }
            else
            {
                color.Target(Game.Screens.BaseColor);
            }
        }
    }
}
