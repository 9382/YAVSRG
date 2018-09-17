﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

namespace YAVSRG.Utilities
{
    public class Splashes
    {
        static readonly string[] menu = LoadSplashes("YAVSRG.Resources.MenuSplashes.txt");
        static readonly string[] loading = LoadSplashes("YAVSRG.Resources.LoadingSplashes.txt");

        static Random random = new Random();

        static string[] LoadSplashes(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd().Split('\n');
            }
        }

        static string RandomSplash(string[] splash)
        {
            return splash[random.Next(0, splash.Length)];
        }

        public static string MenuSplash()
        {
            return RandomSplash(menu);
        }

        public static string LoadingSplash()
        {
            return RandomSplash(loading);
        }
    }
}
