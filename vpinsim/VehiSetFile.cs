using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace vpinsim
{
    public class VehiSetFile
    {
        public HashSet<int> VehiIndexSet = new HashSet<int>();
        private string vehiSetFileName;
        
        public VehiSetFile(string vsfnm)
        {
            // TODO: Complete member initialization
            this.vehiSetFileName = vsfnm;
            FileStream fs = new FileStream(this.vehiSetFileName, 
                FileMode.Open);
            StreamReader reader = new StreamReader(fs);

            try
            {
                do
                {
                    this.VehiIndexSet.Add(int.Parse(reader.ReadLine()));
                }
                while (reader.Peek() != -1);
            }

            catch
            {
                Console.WriteLine("VehiSetFile " + this.vehiSetFileName + 
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

                foreach (int vehiIdx in this.VehiIndexSet)
                {
                    writer.WriteLine(vehiIdx);
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
