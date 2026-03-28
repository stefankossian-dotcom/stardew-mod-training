namespace ZebrusQuestBoard
{
    public class QuestStats
    {
        // FAKT: Hier speichern wir die vier Kategorien, gestartet wird immer bei 0
        public int DailyQuestsCompleted { get; set; } = 0;
        public int TownSpecialOrdersCompleted { get; set; } = 0;
        public int QiSpecialOrdersCompleted { get; set; } = 0;
        public int ModSpecialOrdersCompleted { get; set; } = 0;
        public int RaccoonQuestsCompleted { get; set; } = 0;
        public int FishPondQuestsCompleted { get; set; } = 0;
    }
}