﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace vpinsim
{
    public class Vehicle// : IComparable<Vehicle>
    {
        public Point pos;
        GPSRecord lastRecord;
        int roadIndexOn;
        VpinSim vpinSim;
        public bool inBlock = default(bool); // whether in the observed block
        public bool carryBlockInfo = default(bool);

        public Vehicle(GPSRecord record, VpinSim sim)
        {
            this.lastRecord = record;
            this.vpinSim = sim;
            this.roadIndexOn = Calculator.IndexOfPreferredPolyLine(record,
                this.vpinSim.mf, this.vpinSim.gf, this.vpinSim.af, ref pos);

            #region update vehicle dictionaries and block related info
            if (this.vpinSim.block.ContainsPoint(this.pos))
            {
                this.inBlock = true;
                this.vpinSim.simReporter.vehiInBlkSet.Add(this);
                this.vpinSim.simReporter.vehiAccumPassBlkList.Add(this);
            }
            
            // Setting up for initial information vehicle set. 
            // This code should be in the constructor of VpinSim Class.
            // It is for convenience to implement here.
            if (this.vpinSim.vsf.VehiIndexSet.Contains(this.GetID()) &&
                this.inBlock)
            {
                this.carryBlockInfo = true;
                this.vpinSim.simReporter.vehiCoveredSet.Add(this);
                this.vpinSim.simReporter.vehiAccumSuccessCoveredList.Add(this);
            }
            #endregion

#if DEBUG
            Console.WriteLine("Updating Vehicle " + this.lastRecord.ID);
            Console.WriteLine("using Record " + record.ID);
            Console.WriteLine("-->before" +
                new Point(lastRecord.Longitude, lastRecord.Latitude) + "\n--->after" + pos + "\n");
#endif
        }

        public void UpdateVehileInfo(GPSRecord record)
        {
            this.lastRecord = record;

#if DEBUG
            Console.WriteLine("Updating Vehicle " + this.lastRecord.ID);
            Console.WriteLine("using Record " + record.ID);
            Console.WriteLine("-->before" +
                new Point(lastRecord.Longitude, lastRecord.Latitude) + "\n--->after" + pos + "\n");
#endif
            this.roadIndexOn = Calculator.IndexOfPreferredPolyLine(record,
                this.vpinSim.mf, this.vpinSim.gf, this.vpinSim.af, ref pos);

            #region update vehicle dictionaries and block related info
            if (this.vpinSim.block.ContainsPoint(this.pos))
            {
                // entering the block: now in block but last time not
                if (!this.inBlock)
                {
                   this.inBlock = true;
                   this.vpinSim.simReporter.vehiInBlkSet.Add(this);
                   this.vpinSim.simReporter.vehiAccumPassBlkList.Add(this);
                }
            }
            else
            { 
                // leaving the block: now out of block but last time in
                if (this.inBlock)
                {
                    this.inBlock = false;
                    this.vpinSim.simReporter.vehiInBlkSet.Remove(this);

                    // leaving the block, droping the message
                    if (this.carryBlockInfo)
                    {
                        this.carryBlockInfo = false;
                        this.vpinSim.simReporter.vehiCoveredSet.Remove(this);
                    }
                }
            }
            #endregion
        }

        public int GetID()
        {
            return this.lastRecord.ID;
        }

        public int GetRoadIndexOn()
        {
            return this.roadIndexOn;
        }

        public HashSet<Vehicle> GetNeighbors(double r)
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

            HashSet<Vehicle> nbrSet = new HashSet<Vehicle>();
            #endregion

            #region Search all vehicles in neighboring grid
            // find all the roads in grid blocks in vinicity
            int margin = (int)Math.Ceiling(r/Math.Min(dx, dy));
            //Console.WriteLine("margin = " + margin);
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
                            if (v.GetID() == this.GetID())
                            {
                                continue;
                            }
                            if (Calculator.IsPointInCircle(v.pos, 
                                this.pos, r))
                            {
                                //if (nbrList.Contains(v))
                                //{
                                //    continue;
                                //}
                                nbrSet.Add(v);
#if DEBUG
                                //Console.Write("(" + x + "," + y + "," + i +
                                //    ")" + "v" + v.GetID() + " " + v.pos + " " +
                                //    Calculator.PointToPoint(this.pos, v.pos) * VpinSim.M_PER_DEG +
                                //    "m\n");


                                //Console.Write("v" + v.GetID() + " " + 
                                //    new Point(v.lastRecord.Longitude, v.lastRecord.Latitude) + 
                                //    "->" + v.pos + " " +
                                //    Calculator.PointToPoint(this.pos, v.pos) * VpinSim.M_PER_DEG +
                                //    "m\n");
#endif
                            }
                        }
                    }
                }
            }
            #endregion

#if DEBUG
            //Console.WriteLine("v"+ this.GetID()+", No. Nbr:"+ nbrSet.Count);
#endif

            return nbrSet;
        }

        #region override equity related method. two vehi equal iff. same id
        //public int CompareTo(object obj)
        //{
        //    if (obj == null) return 1;

        //    Vehicle otherVehicle = obj as Vehicle;
        //    if (otherVehicle != null)
        //        return this.GetID().CompareTo(otherVehicle.GetID());
        //    else
        //        throw new ArgumentException("Object is not a Vehicle");
        //}

        public bool Equals(Vehicle otherVehicle)
        {
            if (ReferenceEquals(null, otherVehicle))
            {
                return false;
            }

            if (ReferenceEquals(this, otherVehicle))
            {
                return true;
            }

            return Equals(otherVehicle.GetID(), this.GetID());
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != typeof(Vehicle))
            {
                return false;
            }

            return Equals((Vehicle)obj);
        }

        public override int GetHashCode()
        {
            return this.GetID();
        }

        public static bool operator ==(Vehicle left, Vehicle right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Vehicle left, Vehicle right)
        {
            return !Equals(left, right);
        }
        #endregion
    }
}
