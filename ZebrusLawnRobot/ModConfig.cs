using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebrusLawnRobot
{
    public class ModConfig
    {
        // Wenn true: Option B (Roboter fährt physisch rum)
        // Wenn false: Option A (Gras wird smart im Radius gelöscht)
        public bool UsePhysicalRobot { get; set; } = true;
        public bool RemoveGrassCompletely { get; set; } = false;
    }
}
