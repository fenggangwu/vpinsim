using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace vpinsim
{
    /// <summary>
    /// Representing a .rds file. And the data read from it.
    /// It will be a road set.
    /// Here we use it to store all that <b>hot</b> roads, 
    /// where vehicles holding information will boradcast it
    /// to neighbors.
    /// </summary>
    class RoadSetFile
    {
        public HashSet<int> RoadIndexSet = new HashSet<int>();
        private string roadSetFileName;
        
        public RoadSetFile(string rsfnm)
        {
            // TODO: Complete member initialization
            this.roadSetFileName = rsfnm;
            FileStream fs = new FileStream(this.roadSetFileName, 
                FileMode.Open);
            StreamReader reader = new StreamReader(fs);

            try
            {
                do
                {
                    this.RoadIndexSet.Add(int.Parse(reader.ReadLine()));
                }
                while (reader.Peek() != -1);
            }

            catch
            {
                Console.WriteLine("RoadSetFile " + this.roadSetFileName + 
                    " is empty!!");
            }

            finally
            {
                reader.Close();
            }

            fs.Close();
        }


        public void DumptoFile(string dumpFileName)
        {
            try
            {
                FileStream fs = new FileStream(dumpFileName, 
                    FileMode.Create);
                StreamWriter writer = new StreamWriter(fs);

                foreach (int roadIdx in this.RoadIndexSet)
                {
                    writer.WriteLine(roadIdx);
                }

                writer.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error! " + ex.Message);
                throw;
            }
            
        }
    }


}
