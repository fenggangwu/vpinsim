using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace vpinsim
{
    public class Reporter
    {
        string reportFileName = default(string);
        private string reportMsg = "";
        private VpinSim sim = default(VpinSim);

        #region statistical dictionaries
        /// <summary>
        /// Set of vehicle indices that is maintaining the information in
        /// the observed block
        /// </summary>
        public HashSet<Vehicle> vehiCoveredSet = new HashSet<Vehicle>();

        /// <summary>
        /// Set of vehicle indices that is currently in the observed block
        /// </summary>
        public HashSet<Vehicle> vehiInBlkSet = new HashSet<Vehicle>();

        /// <summary>
        /// Accumulated list of vehicles that is successfully covered
        /// by our protocol, i.e., vehicles that received the information
        /// before leaving the block.
        /// The vehicle will have two instance if it has enterend the 
        /// block several times and been successfully informed twice.
        /// </summary>
        public List<Vehicle> vehiAccumSuccessCoveredList = new List<Vehicle>();

        /// <summary>
        /// Accumulated list of vehicle that has entered the observed
        /// block. The vehicle will have two instance in the list
        /// if it has entered the block twice.
        /// </summary>
        public List<Vehicle> vehiAccumPassBlkList = new List<Vehicle>();
        #endregion

        public Reporter(string nm, VpinSim sim)
        {
            this.reportFileName = nm;
            this.sim = sim;
        }

        internal void InsertReportTuple()
        {
            this.reportMsg += 
                vehiCoveredSet.Count.ToString() + "," +
                vehiInBlkSet.Count.ToString() + "," +
                vehiAccumSuccessCoveredList.Count.ToString() + "," +
                vehiAccumPassBlkList.Count.ToString() + "," + 
                this.sim.vehiDict.Count.ToString() + "\n";
        }

        public void GenerateReport()
        {
            try
            {
                FileStream fs = new FileStream(this.reportFileName,
                    FileMode.Create);
                StreamWriter writer = new StreamWriter(fs);

                writer.Write(this.reportMsg);

                writer.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error! " + ex.Message);
                throw;
            }
            finally
            {
                Console.WriteLine("Report written to " + 
                    this.reportFileName);
            }

        }
    }
}
