﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YAVSRG.Interface.Widgets.Gameplay
{
    public class Screencover : GameplayWidget
    {
        bool flip;

        public Screencover(YAVSRG.Gameplay.ScoreTracker st, bool d) : base(st)
        {
            flip = d;
        }

        public override void Draw(Rect bounds)
        {
            base.Draw(bounds);
            bounds = GetBounds(bounds);
            if (flip)
            {
                SpriteBatch.Draw("screencover", new Rect(bounds.Left, bounds.Top + Game.Options.Theme.ColumnWidth, bounds.Right, bounds.Bottom), System.Drawing.Color.White, 0, 1);
                SpriteBatch.Draw("screencover", new Rect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + Game.Options.Theme.ColumnWidth), System.Drawing.Color.White, 0, 0);
            }
            else
            {
                SpriteBatch.Draw("screencover", new Rect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom - Game.Options.Theme.ColumnWidth), System.Drawing.Color.White, 0, 1);
                SpriteBatch.Draw("screencover", new Rect(bounds.Left, bounds.Bottom - Game.Options.Theme.ColumnWidth, bounds.Right, bounds.Bottom), System.Drawing.Color.White, 0, 0, 2);
            }
        }
    }
}
