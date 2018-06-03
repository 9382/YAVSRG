﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YAVSRG.Charts.YAVSRG;

namespace YAVSRG.Gameplay
{
    public class ScoreTracker //handles scoring while you play through a chart, keeping track of hits and acc and stuff
    {
        public class HitData : OffsetItem
        {
            public float[] delta;
            public byte[] hit;

            public HitData(GameplaySnap s, int keycount)
            {
                Offset = s.Offset;
                hit = new byte[keycount];
                foreach (int k in s.Combine().GetColumns())
                {
                    hit[k] = 1;
                }
                delta = new float[keycount];
            }

            public int Count
            {
                get
                {
                    int x = 0;
                    for (int i = 0; i < hit.Length; i++)
                    {
                        if (hit[i] > 0)
                        {
                            x++;
                        }
                    }
                    return x;
                }
            }
        }

        public event Action<int, int, float> OnHit;

        public ChartWithModifiers c;
        public ScoreSystem Scoring;
        public HitData[] hitdata;
        public int maxcombo; //max possible combo

        public ScoreTracker(ChartWithModifiers c)
        {
            this.c = c;
            //some temp hack until i move this inside ScoreSystem
            maxcombo = 0;
            foreach (Snap s in c.Notes.Points)
            {
                maxcombo += s.Count; //merge this down into other for loop?
            }
            Scoring = ScoreSystem.GetScoreSystem(Game.Options.Profile.ScoreSystem);

            Scoring.OnHit = (k, j, d) => { OnHit(k, j, d); };

            int count = c.Notes.Count;
            hitdata = new HitData[count];
            for (int i = 0; i < count; i++)
            {
                hitdata[i] = new HitData(c.Notes.Points[i], c.Keys);
            }

            Game.Gameplay.ApplyModsToHitData(c, ref hitdata);
        }

        public int Combo()
        {
            return Scoring.Combo;
        }

        public float Accuracy()
        {
            return Scoring.Accuracy();
        }

        public void Update(float time)
        {
            Scoring.Update(time,hitdata);
        }

        public void RegisterHit(int i, int k, float delta)
        {
            if (hitdata[i].hit[k] != 1) { return; } //ignore if the note is already hit or doesn't need to be hit. prevents mashing exploits and such.
            hitdata[i].hit[k] = 2; //mark that note was not only supposed to be hit, but was also hit (marks it as not a miss)
            hitdata[i].delta[k] = delta;
            Scoring.HandleHit(k, i, hitdata);
        }

        public bool EndOfChart() //is end of chart?
        {
            return Scoring.EndOfChart(hitdata.Length);
        }

        public static string HitDataToString(HitData[] data)
        {
            int k = data[0].hit.Length;
            byte[] result = new byte[data.Length * 5 * k];
            for (int i = 0; i < data.Length; i++)
            {
                Array.Copy(data[i].hit, 0, result, k * (i * 5), k);
                Buffer.BlockCopy(data[i].delta, 0, result, k * (i * 5 + 1), k * 4);
            }
            return Convert.ToBase64String(result);
        }

        public static HitData[] StringToHitData(string s, int k)
        {
            byte[] raw = Convert.FromBase64String(s);
            HitData[] result = new HitData[raw.Length / (5 * k)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new HitData(new GameplaySnap(0, 0, 0, 0, 0, 0), k);
                Array.Copy(raw, k * (i * 5), result[i].hit, 0, k);
                Buffer.BlockCopy(raw, k * (i * 5 + 1), result[i].delta, 0, k * 4);
            }
            return result;
        }
    }
}
