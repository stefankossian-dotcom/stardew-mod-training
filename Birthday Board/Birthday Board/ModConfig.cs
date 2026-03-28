namespace BirthdayBoard
{
    public class ModConfig
    {
        public bool FilterRareItems { get; set; } = false;
        public string[] BlacklistedItemIds { get; set; } = new string[0];
        public bool ShowLovedItems { get; set; } = true;

        // NEU: Das Dropdown (Erlaubte interne Werte: "Inventory", "Radius", "Location", "Farm", "World")
        public string SearchScope { get; set; } = "Farm";

        // FAKT: Der Radius geht nun wieder logisch bei 1 los.
        public int ChestSearchRadius { get; set; } = 8;

        // FAKT: 10 individuelle Schalter für die wertvollen Items
        public bool FilterWine { get; set; } = false;
        public bool FilterTruffleOil { get; set; } = false;
        public bool FilterTruffle { get; set; } = false;
        public bool FilterPaleAle { get; set; } = false;
        public bool FilterStarfruit { get; set; } = false;
        public bool FilterAncientFruit { get; set; } = false;
        public bool FilterCaviar { get; set; } = false;
        public bool FilterIridiumBar { get; set; } = false;
        public bool FilterDiamond { get; set; } = false;
        public bool FilterFairyRose { get; set; } = false;
        // NEU: Benötigte Anzahl an Geschenken für die Rezept-Briefe
        public int RequiredGiftsForBoard { get; set; } = 5;
        public int RequiredGiftsForPhone { get; set; } = 10;
        public bool EnablePhone { get; set; } = true;
        public bool PhoneInventoryOnlyOutsideFarm { get; set; } = false; // FAKT: Standardmäßig deaktiviert, um bestehendes Verhalten nicht zu brechen
        public bool ShowHudIcon { get; set; } = true;
    }
}