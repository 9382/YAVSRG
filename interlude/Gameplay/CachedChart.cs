﻿using Prelude.Gameplay.DifficultyRating;
using Prelude.Gameplay.Charts.YAVSRG;
using Prelude.Gameplay;

namespace Interlude.Gameplay
{
    public class CachedChart
    {
        public string file;
        public string title;
        public string artist;
        public string creator;
        public string abspath;
        public string pack;
        public string hash;
        public int keymode;
        public float length;
        public int bpm;
        public string diffname;
        public float physical;
        public float technical;

        public static CachedChart FromChart(Chart c)
        {
            RatingReport r = new RatingReport(new ChartWithModifiers(c),1,KeyLayout.Layout.Spread);
            return new CachedChart
            {
                file = c.Data.File,
                title = c.Data.Title,
                artist = c.Data.Artist,
                creator = c.Data.Creator,
                abspath = c.Data.SourcePath,
                pack = c.Data.SourcePack,
                hash = c.GetHash(),
                keymode = c.Keys,
                length = c.GetDuration(),
                bpm = c.GetBPM(),
                diffname = c.Data.DiffName,
                physical = r.Physical,
                technical = r.Technical
            };
        }

        public override bool Equals(object other) //reminder to fix this in future, in an optimised way
        {
            return ReferenceEquals(this, other);
        }
        
        public override int GetHashCode()
        {
            return 0;
        }

        public string GetFileIdentifier()
        {
            return System.IO.Path.Combine(abspath, file);
        }
    }
}

