//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vpinsim
{
    public class VpinSim
    {
        #region attributes and fields
        #region constants
        public const double COMM_RANGE = 300; //meter
        public const double M_PER_DEG = 110665.1;//meter
        public const double BLOCK_SIZE = 500;//meter
        //KM_PER_LAT =  2*pi*6371/360 = 111.1949;
        //KM_PER_LONG = 2*pi*6371*cos((31.84316 + 30.712357)/2)/360 = 110.1352;
        //KM_PER_DEG=(KM_PER_LAT + KM_PER_LONG)/2 = 110.6651;
        #endregion

        #region file readers
        public GPSFile gpsf = default(GPSFile);
        public MainFile mf = default(MainFile);
        public IndexFile idxf = default(IndexFile);
        public GridFile gf = default(GridFile);
        public AngleFile af = default(AngleFile);
        public RoadSetFile rsf = default(RoadSetFile);
        public VehiSetFile vsf = default(VehiSetFile);
        #endregion

        #region data stuctures related to roads and vehicles

        public Block block = default(Block);

        public Dictionary<int, Vehicle> vehiDict = new Dictionary<int, Vehicle>();
        public Dictionary<int, Road> roadDict = new Dictionary<int, Road>();


        #endregion

        public Reporter simReporter = default(Reporter);
        #endregion

        #region Constructor
        public VpinSim(string gpsFileName, string mainFileName, 
            string indexFileName, string gridFileName, 
            string angleFileName, string roadSetFileName,
            string vehiInitSetFileName)
        {
            #region Read data files
            Console.WriteLine("Creator of VpinSim Called");

            Console.WriteLine("Reading GPSFile " + gpsFileName);
            this.gpsf = new GPSFile(gpsFileName);

            Console.WriteLine("Reading MainFile " + mainFileName);
            this.mf = new MainFile(mainFileName, this);

            Console.WriteLine("Reading IndexFile " + indexFileName);
            this.idxf = new IndexFile(indexFileName);

            Console.WriteLine("Reading gridFileName " + gridFileName);
            this.gf = new GridFile(gridFileName);

            Console.WriteLine("Reading angleFileName " + angleFileName);
            this.af = new AngleFile(angleFileName);

            Console.WriteLine("Reading RoadSetFile " + roadSetFileName);
            this.rsf = new RoadSetFile(roadSetFileName);

            Console.WriteLine("Reading VehiSetFile " + vehiInitSetFileName);
            this.vsf = new VehiSetFile(vehiInitSetFileName);
            #endregion

            #region init observed block
            double Xmin = this.mf.header.Xmin;
            double Xmax = this.mf.header.Xmax;
            double Ymin = this.mf.header.Ymin;
            double Ymax = this.mf.header.Ymax;
            double dx = (Xmax - Xmin) / this.gf.XGridNum;
            double dy = (Ymax - Ymin) / this.gf.YGridNum;

            //Point center = new Point((Xmin + Xmax)/2, (Ymin + Ymax)/2);
            Point center = new Point(121.423485652362, 31.2342309399148);
            this.block = new Block(center, BLOCK_SIZE / M_PER_DEG);
#if DEBUG
            Console.WriteLine("Xmin, Xmax, Ymin, Ymax=" + Xmin + "," +
                Xmax + "," + Ymin + "," + Ymax);
            Console.WriteLine("XGridNum, YGridNum=" +
                this.gf.XGridNum + "," + this.gf.YGridNum);
            Console.WriteLine("(dx,dy) = " + dx + "," + dy);
            Console.WriteLine("(dx,dy) in meter = " + dx * M_PER_DEG + "," +
                dy * M_PER_DEG);
#endif


            #endregion

            this.simReporter = new Reporter(
                this.block.Xmin.ToString() + "," +
                this.block.Ymin.ToString() + ".csv", this);
        }
        #endregion

        #region run simulator
        public bool Run()
        {
            DateTime lastts = default(DateTime);
            foreach (GPSRecord record in gpsf.records)
            {
                if (lastts == default(DateTime))
                {
                    lastts = record.TimeStamp;
                }
                if (record.TimeStamp > lastts)
                {
                    Console.WriteLine(lastts + "->" + record.TimeStamp);
                    this.triggerInformationBoradcast();
                    this.simReporter.InsertReportTuple();
                    lastts = record.TimeStamp;
                }

                this.updateDataStructures(record);
            }
            return false;
        }

        #region tool functions
        /// <summary>
        /// At each time slot, vehicles will try to communicate with each
        /// other. Once all the position is updated by the record within
        /// the same time slot, the communication will be triggered.
        /// </summary>
        private void triggerInformationBoradcast()
        {
            HashSet<Vehicle> tempReceiverSet = new HashSet<Vehicle>();

            #region calculate message receiver vehicles
            // only perform broadcast on the "hot" roads
            foreach (int roadIdx in this.rsf.RoadIndexSet)
            {
                Road hotRoad = this.roadDict[roadIdx];

                foreach (Vehicle v in hotRoad.vehicleSet)
                {
                    if (v.carryBlockInfo)
                    {
                        //broadcast the information
                        foreach (Vehicle nbr in v.GetNeighbors(
                            COMM_RANGE / M_PER_DEG))
                        {
                            // for those who newly get the message
                            if (!nbr.carryBlockInfo)
                            {
                                tempReceiverSet.Add(nbr);
                            }
                        }
                    }
                }
            }
            #endregion

            #region update vehicle carrying status and the dictionaries
            foreach (Vehicle v in tempReceiverSet)
            {
                if (v.inBlock)
                {
                    v.carryBlockInfo = true;
                    this.simReporter.vehiCoveredSet.Add(v);
                    this.simReporter.vehiAccumSuccessCoveredList.Add(v);
                }

            }
            #endregion
        }

        /// <summary>
        /// update corresponding data structure according to 
        /// the record
        /// </summary>
        /// <param name="record">the GPS record</param>
        private void updateDataStructures(GPSRecord record)
        {

            #region update vehicle info
            Vehicle vehicle = default(Vehicle);
            int lastRoadIdx = default(int);
            try
            {
                vehicle = this.vehiDict[record.ID];
                lastRoadIdx = vehicle.GetRoadIndexOn();
                vehicle.UpdateVehileInfo(record);
            }
            catch (KeyNotFoundException)
            {
                vehicle = new Vehicle(record, this);
                this.vehiDict.Add(record.ID, vehicle);
            }
            #endregion

            #region update road info
            if (lastRoadIdx != default(int))
            {
                try
                {
                    Road lastRoad = this.roadDict[lastRoadIdx];
                    lastRoad.RemoveFromVehicleSet(vehicle);
                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine("Last Road Not Found!");
                    throw;
                }
            }

            int currRoadIdx = vehicle.GetRoadIndexOn();
            Road currRoad = default(Road);

            try 
	        {
                currRoad = this.roadDict[currRoadIdx];
	        }
	        catch (Exception)
	        {
                currRoad = new Road(currRoadIdx);
                this.roadDict.Add(currRoadIdx, currRoad);
		    }

            currRoad.AddtoVehicleSet(vehicle);
            #endregion

        }
        #endregion

        #endregion

        #region write to reporting files
        internal void GenerateReport()
        {
            Console.WriteLine("Generating Report...");
            this.simReporter.GenerateReport();
        }
        #endregion

        
    }
}
