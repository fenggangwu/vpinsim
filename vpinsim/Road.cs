using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vpinsim
{
    class Road
    {
        int roadIndex;
        public HashSet<Vehicle> vehicleSet;

        public Road(int idx)
        {
            this.roadIndex = idx;
            this.vehicleSet = new HashSet<Vehicle>();
        }

        public void AddtoVehicleSet(Vehicle v)
        {
            this.vehicleSet.Add(v);
        }

        public void RemoveFromVehicleSet(Vehicle v)
        {
            this.vehicleSet.Remove(v);
        }
    }
}
