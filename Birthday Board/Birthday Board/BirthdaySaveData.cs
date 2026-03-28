using System.Collections.Generic;

namespace ZebrusBirthdayBoard // Dein Namespace
{
    public class BirthdaySaveData
    {
        // FAKT: Zähler für die Quoten (Diese Jahreszeit, Dieses Jahr, Gesamt)
        public int GiftsGivenThisSeason { get; set; } = 0;
        public int GiftsGivenThisYear { get; set; } = 0;
        public int TotalGiftsGiven { get; set; } = 0;

        // FAKT: Das Spiel muss wissen, wann ein neues Jahr/eine neue Jahreszeit 
        // begonnen hat, um die oberen Zähler automatisch wieder auf 0 zu setzen.
        public int LastRecordedYear { get; set; } = 1;
        public string LastRecordedSeason { get; set; } = "spring";

        // FAKT: Wörterbücher (Dictionaries), die sich merken, 
        // WER beschenkt wurde und WAS genau verschenkt wurde.
        public Dictionary<string, int> NpcGiftCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ItemGiftCounts { get; set; } = new Dictionary<string, int>();

        // FAKT: Live-Zähler für die verstrichenen Geburtstage seit Mod-Installation
        public int PossibleGiftsThisSeason { get; set; } = 0;
        public int PossibleGiftsThisYear { get; set; } = 0;
        public int PossibleGiftsTotal { get; set; } = 0;
    }
}