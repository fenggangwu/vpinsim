using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace vpinsim
{
    public class Vehicle
    {
        Point pos;
        GPSRecord lastRecord;
        int roadIndexOn;
        VpinSim vpinSim;
        public bool carryBlockInfo = default(bool);

        public Vehicle(GPSRecord record, VpinSim sim)
        {
            this.lastRecord = record;
            this.vpinSim = sim;
            this.roadIndexOn = Calculator.IndexOfPreferredPolyLine(record,
                this.vpinSim.mf, this.vpinSim.gf, this.vpinSim.af, ref pos);

#if DEBUG
            Console.WriteLine("Initializing Vehicle " + this.lastRecord.ID);
            Console.WriteLine("using Record " + record.ID);
            Console.WriteLine("-->before" +
                new Point(lastRecord.Longitude, lastRecord.Latitude) + "\n--->after" + pos + "\n");
#endif
        }

        public void UpdateVehileInfo(GPSRecord record)
        {
            this.lastRecord = record;

            this.roadIndexOn = Calculator.IndexOfPreferredPolyLine(record,
                this.vpinSim.mf, this.vpinSim.gf, this.vpinSim.af, ref pos);

#if DEBUG
            Console.WriteLine("Updating Vehicle " + this.lastRecord.ID);
            Console.WriteLine("using Record " + record.ID);
            Console.WriteLine("-->before" +
                new Point(lastRecord.Longitude, lastRecord.Latitude) + "\n--->after" + pos + "\n");
#endif
        }

        public int GetID()
        {
            return this.lastRecord.ID;
        }

        public int GetRoadIndexOn()
        {
            return this.roadIndexOn;
        }

        public List<Vehicle> GetNeighbors(double r)
        {
            #region Initialization
            double Xmin = this.vpinSim.mf.header.Xmin;
            double Xmax = this.vpinSim.mf.header.Xmax;
            double Ymin = this.vpinSim.mf.header.Ymin;
            double Ymax = this.vpinSim.mf.header.Ymax;
            double dx = (Xmax - Xmin) / this.vpinSim.gf.XGridNum;
            double dy = (Ymax - Ymin) / this.vpinSim.gf.YGridNum;
            int x0 = (int)((this.pos.X - Xmin) / dx);
            int y0 = (int)((this.pos.Y - Ymin) / dy);

            List<Vehicle> nbrList = new List<Vehicle>();
            #endregion

            #region Search all vehicles in neighboring grid
            // find all the roads in neighboring grid
            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < this.vpinSim.gf.Grids[x, y].Length; i++)
                    {
                        int roadIdx = this.vpinSim.gf.Grids[x, y][i];
                        Road aRoad = this.vpinSim.roadDict[roadIdx];

                        // for all the vehicles on this road
                        foreach (Vehicle v in aRoad.vehicleSet)
                        {
                            if (Calculator.IsPointInCircle(v.pos, this.pos, r))
                            {
                                nbrList.Add(v);
                            }
                        }
                    }
                }
            }
            #endregion

#if DEBUG
            Console.WriteLine("v"+ this.GetID()+", No. Nbr:"+ nbrList.Count);
            throw new Exception();
#endif

            return nbrList;
        }
    }
}
