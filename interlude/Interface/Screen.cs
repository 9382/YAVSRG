﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static YAVSRG.Interface.ScreenUtils;
using System.Drawing;

namespace YAVSRG.Interface
{
    public class Screen
    {
        protected List<Widget> Widgets = new List<Widget>();

        static readonly Color bgdim = Color.FromArgb(80, 80, 80);
        public static int parallax = 15;
        private static float fade = 0;
        private static bool fadingIn = false;
        private static List<Screen> stack = new List<Screen> { new Screens.ScreenMenu() };
        private static Screen Pending;
        private static List<Dialog> dialogs = new List<Dialog>();

        public static Screen Current
        {
            get
            {
                if (stack.Count == 0) { return null; }
                return stack[stack.Count -1];
            }
        }

        public static void AddDialog(Dialog d)
        {
            dialogs.Insert(0,d);
        }

        public static void Push(Screen s)
        {
            if (fadingIn) { return; }
            Pending = s;
            fadingIn = true;
        }

        private static void DoPush()
        {
            Current?.OnPassthrough(Pending);
            Pending.OnEnter(Current);

            stack.Add(Pending);
            Pending = null;
        }

        public static void Pop()
        {
            if (fadingIn) { return; }
            fadingIn = true;
        }

        protected static void DoPop()
        {
            Screen old = Current;
            stack.RemoveAt(stack.Count - 1);

            old.OnExit(Current);
            Current?.OnEnter(old);
        }

        public static void UpdateScreens()
        {
            if (fadingIn || fade > 0)
            {
                if (fadingIn)
                {
                    fade += 0.04f;
                    if (fade >= 0.96f)
                    {
                        fadingIn = false;
                        if (Pending == null)
                        {
                            DoPop();
                        }
                        else
                        {
                            DoPush();
                        }
                    }
                }
                else
                {
                    fade -= 0.03f;
                }
            }
            if (dialogs.Count > 0)
            {
                dialogs[0].Update(-Width, -Height, Width, Height);
                if (dialogs[0].Closed)
                {
                    dialogs.RemoveAt(0);
                }
            }
            else
            {
                Current?.Update();
            }
        }

        public static void DrawScreens()
        {
            int parallaxX = Input.MouseX * parallax / Width;
            int parallaxY = Input.MouseY * parallax / Height;
            DrawChartBackground(-Width - parallaxX - parallax, -Height - parallaxY - parallax, Width - parallaxX + parallax, Height - parallaxY + parallax, bgdim);
            Current?.Draw();
            if (dialogs.Count > 0)
            {
                dialogs[0].Draw(-Width, -Height, Width, Height);
            }
                if (fade > 0)
            {
                SpriteBatch.DrawRect(-Width, -Height, Width, Height, Color.FromArgb((int)(fade*250), 0, 0, 0));
            }
        }

        public virtual void Update()
        {
            foreach (Widget w in Widgets)
            {
                w.Update(-Width,-Height,Width,Height);
            }
        }

        public virtual void Draw()
        {
            foreach (Widget w in Widgets)
            {
                w.Draw(-Width, -Height, Width, Height);
            }
        }

        public virtual void OnEnter(Screen prev) { }

        public virtual void OnExit(Screen next) { }

        public virtual void OnPassthrough(Screen next) { }
    }
}
