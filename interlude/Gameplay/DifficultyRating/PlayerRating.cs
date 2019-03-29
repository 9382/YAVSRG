﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interlude.Gameplay.DifficultyRating
{
    public class PlayerRating
    {
        //uncommented due to being subject to change
        //how it works may be explained in design document
        public static float GetRating(RatingReport r, ScoreTracker.HitData[] hitdata) //calculates rating of a play - i.e how capable you showed you were when you played this chart
        {
            if (hitdata.Length == 0) return 0;
            double[] v = new double[hitdata[0].hit.Length];
            double[] physical = new double[hitdata.Length];
            for (int i = 0; i < hitdata.Length; i++)
            {
                List<double> findMean = new List<double>();
                for (byte k = 0; k < hitdata[i].hit.Length; k++)
                {
                    if (hitdata[i].hit[k] > 0)
                    {
                        CalcUtils.UpdateStrain(ref v[k], r.PhysicalComposite[i, k] * CalcUtils.ConfidenceValue(hitdata[i].delta[k]), r.Delta[i, k]);
                        findMean.Add(v[k]);
                    }
                }
                if (findMean.Count > 0)
                {
                    physical[i] = CalcUtils.RootMeanPower(findMean, 1);
                }
                /*
                        v *= Math.Exp(0.02 * w * Math.Max(0,Math.Log(w * r.OverallPhysical[i] / v)));
                }*/
            }
            return CalcUtils.GetOverallDifficulty(physical);
        }
    }
}
