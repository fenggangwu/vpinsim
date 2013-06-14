﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vpinsim
{
    class Program
    {
        static void Main(string[] args)
        {
            string gpsFileName = ".\\WNL_CSL.txt";
            string mainFileName = ".\\LD.shp";
            string indexFileName = ".\\LD.shx";
            string gridFileName = ".\\LD.grd";
            string angelFileName = ".\\LD.ngl";
            string roadSetFileName = ".\\roadSetFile.txt";
            
            VpinSim sim = new VpinSim(gpsFileName, mainFileName,
                indexFileName, gridFileName, angelFileName, roadSetFileName);
            sim.Run();
            sim.GenerateReport();
        }

    }

    
}