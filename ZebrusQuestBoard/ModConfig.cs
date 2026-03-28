using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebrusQuestBoard
{
    public class ModConfig
    {
        // Standardmäßig ist das Gold Board AN (true)
        public bool EnableGoldBoard { get; set; } = true;
        // Standardmäßig ist das Iridium Board AN (true)
        public bool EnableIridiumBoard { get; set; } = true;
        // Standardmäßig ist die Waschbär-Statue AN (true)
        public bool EnableRaccoon { get; set; } = true;
        // Standardmäßig ist die Fisch-Statue AN (true)
        public bool EnableFish { get; set; } = true;
        // Standardmäßig ist das Mod Board AN (true)
        public bool EnableModBoard { get; set; } = true;
        // Standardmäßig AUS (false), als Schutzmaßnahme für den Spielstand
        public bool EnableGhostQuestCleanup { get; set; } = false;

        public bool UseNotbelovelyNormalBoard { get; set; } = false;
        public bool UseNotbelovelyGoldBoard { get; set; } = false;
        public bool UseNotbelovelyIridiumBoard { get; set; } = false;
        public bool UseNotbelovelyModBoard { get; set; } = false;
        public bool UseNotbelovelyRaccoon { get; set; } = false;
        public bool UseNotbelovelyFish { get; set; } = false;
        public bool UseNotbelovelyIcons { get; set; } = false;
        public bool UseNotbelovelyFishIcon { get; set; } = false;
    }
}
