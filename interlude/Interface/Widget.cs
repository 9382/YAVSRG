﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace YAVSRG.Interface
{
    public class Widget
    {
        public class AnchorPoint
        {
            private SlidingEffect _X;
            private SlidingEffect _Y;
            private AnchorType XRel;
            private AnchorType YRel;

            public AnchorPoint(float x, float y, AnchorType xa, AnchorType ya)
            {
                _X = new SlidingEffect(x);
                _Y = new SlidingEffect(y);
                XRel = xa;
                YRel = ya;
            }

            public void Update()
            {
                _X.Update();
                _Y.Update();
            }

            public void Move(float x, float y)
            {
                _X.Target += x;
                _Y.Target += y;
            }

            public void Target(float x, float y)
            {
                _X.Target = x;
                _Y.Target = y;
            }

            public float X(float min, float max)
            {
                switch (XRel)
                {
                    case (AnchorType.CENTER):
                        return _X;
                    case (AnchorType.MAX):
                        return max - _X;
                    case (AnchorType.MIN):
                        return min + _X;
                }
                return _X;
            }

            public float Y(float min, float max)
            {
                switch (YRel)
                {
                    case (AnchorType.CENTER):
                        return _Y;
                    case (AnchorType.MAX):
                        return max - _Y;
                    case (AnchorType.MIN):
                        return min + _Y;
                }
                return _Y;
            }

            public float AbsX
            {
                get
                {
                    return _X;
                }
            }

            public float AbsY
            {
                get
                {
                    return _Y;
                }
            }

            public float TargetX
            {
                get
                {
                    return _X.Target;
                }
            }

            public float TargetY
            {
                get
                {
                    return _Y.Target;
                }
            }
        }

        public AnchorPoint A;
        public AnchorPoint B;
        public int State = 1; //0 is hidden, 2 is focussed
        public Animations.AnimationGroup Animation;
        protected Widget parent;
        protected List<Widget> Widgets;


        public Widget()
        {
            A = new AnchorPoint(0, 0, AnchorType.MIN, AnchorType.MIN);
            B = new AnchorPoint(0, 0, AnchorType.MAX, AnchorType.MAX);
            Animation = new Animations.AnimationGroup(true);
            Widgets = new List<Widget>();
        }

        public virtual void AddToContainer(Widget parent)
        {
            this.parent = parent;
        }

        public virtual void AddChild(Widget child)
        {
            Widgets.Add(child);
            child.AddToContainer(this);
        }

        public float Left(float l, float r)
        {
            return A.X(l, r);
        }

        public float Top(float t, float b)
        {
            return A.Y(t, b);
        }

        public float Right(float l, float r)
        {
            return B.X(l, r);
        }

        public float Bottom(float t, float b)
        {
            return B.Y(t, b);
        }

        protected void ConvertCoordinates(ref float l, ref float t, ref float r, ref float b)
        {
            float ol = l;//otherwise it will fuck up
            float ot = t;
            l = Left(l, r); t = Top(t, b); r = Right(ol, r); b = Bottom(ot, b);
        }

        public Widget PositionTopLeft(float x, float y, AnchorType ax, AnchorType ay)
        {
            A = new AnchorPoint(x, y, ax, ay);
            return this; //allows for method chaining
        }

        public Widget PositionBottomRight(float x, float y, AnchorType ax, AnchorType ay)
        {
            B = new AnchorPoint(x, y, ax, ay);
            return this;
        }

        public virtual void Draw(float left, float top, float right, float bottom)
        {
            ConvertCoordinates(ref left, ref top, ref right, ref bottom);
            foreach (Widget w in Widgets)
            {
                if (w.State > 0)
                {
                    w.Draw(left, top, right, bottom);
                }
            }
        }

        public virtual void Update(float left, float top, float right, float bottom)
        {
            ConvertCoordinates(ref left, ref top, ref right, ref bottom);
            foreach (Widget w in Widgets)
            {
                if (w.State > 0)
                {
                    w.Update(left, top, right, bottom);
                }
            }
            A.Update();
            B.Update();
            Animation.Update();
        }
    }
}
