using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using ZebrusBirthdayBoard;

namespace BirthdayBoard
{
    public class ModEntry : Mod
    {
        private NPC currentBirthdayNPC;        
        private ModConfig config;
        private string leftItemToDraw = null;
        private string rightItemToDraw = null;
        private List<NPC> birthdayNPCs = new List<NPC>();
        private BirthdaySaveData saveData;
        private NPC potentialGiftTarget = null;
        private string potentialGiftItemName = null;
        private bool wasGiftAlreadyGiven = false;
        private NPC secondBirthdayNPC = null; // FAKT: Speichert den aktuell 2. Kopf
        private Texture2D customCheckmarkTex; // FAKT: Speichert deine eigene Bilddatei im Arbeitsspeicher

        public override void Entry(IModHelper helper)
        {
            this.config = helper.ReadConfig<ModConfig>();

            // FAKT: Wir weisen SMAPI an, die eigene PNG-Datei zu laden. 
            // Der try-catch-Block verhindert einen Spielabsturz, falls du den Dateinamen mal änderst oder die Datei fehlt.
            try
            {
                this.customCheckmarkTex = helper.ModContent.Load<Texture2D>("[CP] Zebrus Birthday Board/assets/checkmark.png");
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Fakt: Die Datei 'assets/checkmark.png' wurde nicht gefunden. Der Haken wird nicht gezeichnet.", LogLevel.Warn);
            }

            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Player.InventoryChanged += OnInventoryChanged;
            helper.Events.Player.Warped += OnPlayerWarped;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedHud += OnRenderedHud;

            // FAKT: Registrierung der Konsolenbefehle für das Debugging
            helper.ConsoleCommands.Add("bb_forget_recipe", "Entfernt das Rezept für das Zebrus Birthday Board.", this.CommandForgetRecipe);
            helper.ConsoleCommands.Add("bb_force_mail", "Legt den Brief mit dem Rezept in den Briefkasten für den nächsten Morgen.", this.CommandForceMail);
            helper.ConsoleCommands.Add("bb_gift_given", "Setzt den Geschenk-Status des heutigen Geburtstagskindes auf 'geschenkt' (1).", this.CommandGiftGiven);
            helper.ConsoleCommands.Add("bb_gift_reset", "Setzt den Geschenk-Status des heutigen Geburtstagskindes auf 'nicht geschenkt' (0).", this.CommandGiftReset);
            helper.ConsoleCommands.Add("bb_add_gift", "Erhöht den versteckten Zähler für Geburtstagsgeschenke um 1.", this.CommandAddGift);
            helper.ConsoleCommands.Add("bb_reset_gift_counter", "Setzt den versteckten Zähler für Geburtstagsgeschenke auf 0 zurück.", this.CommandResetGiftCounter);
            helper.ConsoleCommands.Add("bb_test_double", "Simuliert zwei Geburtstagskinder (Lewis & Robin) für das Board.", this.CommandTestDouble);
            helper.ConsoleCommands.Add("reset_birthday_stats", "Setzt alle gesammelten Geburtstags-Geschenke und Quoten auf 0 zurück.", this.CommandResetStats);
            helper.ConsoleCommands.Add("bb_test_anniversary", "Simuliert einen Hochzeitstag mit einem NPC. Nutzung: bb_test_anniversary [NPC-Name]", this.CommandTestAnniversary);

            // NEU: Das Event für das abendliche Speichern (für die Brief-Logik)
            helper.Events.GameLoop.DayEnding += OnDayEnding;

            // NEU: GMCM Menü beim Start des Spiels laden
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            // FAKT: Wir zeichnen das Icon nur, wenn die Welt geladen ist, 
            // das Standard-HUD sichtbar ist, es noch ein Geburtstagskind gibt 
            // und die Option in der Config aktiviert ist.
            if (!Context.IsWorldReady || !Game1.displayHUD || this.currentBirthdayNPC == null || !this.config.ShowHudIcon) return;

            // FAKT: Wir lesen die aktuellen Live-Koordinaten der In-Game-Uhr aus.
            Vector2 clockPos = Game1.dayTimeMoneyBox.position;

            // FAKT: Wir laden den Pink Cake als standardisiertes UI-Icon
            var giftIcon = ItemRegistry.GetData("(O)221");

            if (giftIcon != null)
            {
                // Positionierung links neben der Uhr
                float iconX = clockPos.X - 48;
                float iconY = clockPos.Y + 16;

                // Die pulsierende Animation
                float time = (float)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
                float scale = 2f + (float)Math.Sin(time / 200.0) * 0.2f;

                // 1. Icon zeichnen
                e.SpriteBatch.Draw(
                    texture: giftIcon.GetTexture(),
                    position: new Vector2(iconX, iconY),
                    sourceRectangle: giftIcon.GetSourceRect(),
                    color: Color.White,
                    rotation: 0f,
                    origin: new Vector2(8, 8), // Zentrum des 16x16 Icons
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 1f
                );

                // 2. Hover-Logik für den Tooltip
                int mouseX = Game1.getMouseX();
                int mouseY = Game1.getMouseY();

                // FAKT: Die Breite und Höhe des Icons basierend auf der aktuellen Skalierung berechnen (16 Pixel * Scale)
                int currentWidth = (int)(16 * scale);
                int currentHeight = (int)(16 * scale);

                // FAKT: Da der Ursprung in der Mitte liegt (8, 8), müssen wir die obere linke Ecke für das Rechteck berechnen
                Rectangle iconBounds = new Rectangle(
                    (int)(iconX - (currentWidth / 2f)),
                    (int)(iconY - (currentHeight / 2f)),
                    currentWidth,
                    currentHeight
                );

                // FAKT: Prüfen, ob die Maus den Kuchen berührt
                if (iconBounds.Contains(mouseX, mouseY))
                {
                    // FAKT: Wir holen uns alle Namen der heutigen Geburtstagskinder und verbinden sie mit einem Komma
                    List<string> npcNames = this.birthdayNPCs.Select(n => n.displayName).ToList();
                    string hoverText = string.Join(", ", npcNames);

                    // FAKT: Die offizielle Stardew Valley Methode für Tooltips aufrufen
                    IClickableMenu.drawHoverText(e.SpriteBatch, hoverText, Game1.smallFont);
                }
            }
        }

        private void OnInventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            // FAKT: Wird nur ausgelöst, wenn sich die Anzahl der Items im Rucksack ändert
            if (!Context.IsWorldReady || this.currentBirthdayNPC == null) return;
            this.GetFavoriteItemText(this.currentBirthdayNPC);
        }

        private void OnPlayerWarped(object sender, WarpedEventArgs e)
        {
            // FAKT: Wird nur ausgelöst, wenn der Spieler durch eine Tür geht (Ladebildschirm)
            if (!Context.IsWorldReady || this.currentBirthdayNPC == null) return;
            this.GetFavoriteItemText(this.currentBirthdayNPC);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Hole die API von GMCM
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            // Wenn GMCM nicht installiert ist, breche hier ab (Mod läuft normal weiter)
            if (configMenu is null)
                return;

            // Registriere deine Mod beim GMCM
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () =>
                {
                    // 1. Speichert die Änderungen physisch in der config.json
                    this.Helper.WriteConfig(this.config);

                    // FAKT: 2. Erzwingt die sofortige Aktualisierung der Board im laufenden Spiel,
                    // falls die Welt bereits geladen ist und es ein Geburtstagskind gibt.
                    if (Context.IsWorldReady && this.currentBirthdayNPC != null)
                    {
                        this.GetFavoriteItemText(this.currentBirthdayNPC);
                    }
                }
            );

            // FAKT: WIEDERHERGESTELLT - Schalter zum kompletten Deaktivieren der Item-Anzeige
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.showitems.name"),
                tooltip: () => this.Helper.Translation.Get("config.showitems.tooltip"),
                getValue: () => this.config.ShowLovedItems,
                setValue: value => this.config.ShowLovedItems = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.show_hud_icon.name"),
                tooltip: () => this.Helper.Translation.Get("config.show_hud_icon.tooltip"),
                getValue: () => this.config.ShowHudIcon,
                setValue: value => this.config.ShowHudIcon = value
            );

            // FAKT: Schalter zum kompletten Deaktivieren des Telefons
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.enable_phone.name"),
                tooltip: () => this.Helper.Translation.Get("config.enable_phone.tooltip"),
                getValue: () => this.config.EnablePhone,
                setValue: value => this.config.EnablePhone = value
            );

            // FAKT: Neuer Schalter für die Inventar-Limitierung des Telefons
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.phone_inventory_only.name"),
                tooltip: () => this.Helper.Translation.Get("config.phone_inventory_only.tooltip"),
                getValue: () => this.config.PhoneInventoryOnlyOutsideFarm,
                setValue: value => this.config.PhoneInventoryOnlyOutsideFarm = value
            );

            // Regler: Geschenke für die Board
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.gifts_board.name"),
                tooltip: () => this.Helper.Translation.Get("config.gifts_board.tooltip"),
                getValue: () => this.config.RequiredGiftsForBoard,
                setValue: value => this.config.RequiredGiftsForBoard = value,
                min: 1,
                max: 100,
                interval: 1
            );

            // Regler: Geschenke für das Telefon
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.gifts_phone.name"),
                tooltip: () => this.Helper.Translation.Get("config.gifts_phone.tooltip"),
                getValue: () => this.config.RequiredGiftsForPhone,
                setValue: value => this.config.RequiredGiftsForPhone = value,
                min: 1,
                max: 100,
                interval: 1
            );

            // 1. Das neue Dropdown-Menü
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.scope.name"),
                tooltip: () => this.Helper.Translation.Get("config.scope.tooltip"),
                getValue: () => this.config.SearchScope,
                setValue: value => this.config.SearchScope = value,
                allowedValues: new string[] { "Inventory", "Radius", "Location", "Farm", "World" },
                formatAllowedValue: value => this.Helper.Translation.Get("scope." + value.ToLower())
            );

            // 2. Der Radius-Regler (nur für den Modus "Radius")
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.radius.name"),
                tooltip: () => this.Helper.Translation.Get("config.radius.tooltip"),
                getValue: () => this.config.ChestSearchRadius,
                setValue: value => this.config.ChestSearchRadius = value,
                min: 1, // Startet wieder logisch bei 1
                max: 50,
                interval: 1
            );

            // 3. Schalter: Seltene Items filtern (sofern du die Blacklist-Funktion weiterhin nutzt)
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.filter.name"),
                tooltip: () => this.Helper.Translation.Get("config.filter.tooltip"),
                getValue: () => this.config.FilterRareItems,
                setValue: value => this.config.FilterRareItems = value
            );

            // FAKT: Fügt eine dynamisch übersetzte Überschrift in das Menü ein
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section_filter.name")
            );

            // FAKT: Array mit den Referenzen zu den Konfigurationswerten und den passenden i18n Schlüsseln
            var toggleItems = new (Func<bool> get, Action<bool> set, string key)[]
            {
                (() => this.config.FilterWine, val => this.config.FilterWine = val, "wine"),
                (() => this.config.FilterTruffleOil, val => this.config.FilterTruffleOil = val, "truffleoil"),
                (() => this.config.FilterTruffle, val => this.config.FilterTruffle = val, "truffle"),
                (() => this.config.FilterPaleAle, val => this.config.FilterPaleAle = val, "paleale"),
                (() => this.config.FilterStarfruit, val => this.config.FilterStarfruit = val, "starfruit"),
                (() => this.config.FilterAncientFruit, val => this.config.FilterAncientFruit = val, "ancientfruit"),
                (() => this.config.FilterCaviar, val => this.config.FilterCaviar = val, "caviar"),
                (() => this.config.FilterIridiumBar, val => this.config.FilterIridiumBar = val, "iridiumbar"),
                (() => this.config.FilterDiamond, val => this.config.FilterDiamond = val, "diamond"),
                (() => this.config.FilterFairyRose, val => this.config.FilterFairyRose = val, "fairyrose")
            };

            foreach (var item in toggleItems)
            {
                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    name: () => this.Helper.Translation.Get($"config.filter_{item.key}.name"),
                    tooltip: () => this.Helper.Translation.Get("config.filter_item.tooltip"),
                    getValue: item.get,
                    setValue: item.set
                );
            }

            // Hole die API von Content Patcher
            var cpApi = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            if (cpApi != null)
            {
                // FAKT: Wir lesen die Texte aus deiner einzigen i18n-Datei und registrieren sie als CP-Tokens.
                // Der Name des Tokens im Spiel ist dann immer "DeineModID/TokenName"
                cpApi.RegisterToken(this.ModManifest, "BoardName", () => new[] { this.Helper.Translation.Get("board.name").ToString() });
                cpApi.RegisterToken(this.ModManifest, "MailText", () => new[] { this.Helper.Translation.Get("mail.text").ToString() });
                cpApi.RegisterToken(this.ModManifest, "PhoneName", () => new[] { this.Helper.Translation.Get("phone.name").ToString() });
                cpApi.RegisterToken(this.ModManifest, "PhoneDesc", () => new[] { this.Helper.Translation.Get("phone.description").ToString() });
                cpApi.RegisterToken(this.ModManifest, "PhoneMail", () => new[] { this.Helper.Translation.Get("phone.mail").ToString() });
                cpApi.RegisterToken(this.ModManifest, "WallBoardName", () => new[] { this.Helper.Translation.Get("wallboard.name").ToString() });
                cpApi.RegisterToken(this.ModManifest, "MailTextWall", () => new[] { this.Helper.Translation.Get("mail.text_wall").ToString() });
            }
        }

        private void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            this.birthdayNPCs.Clear();
            this.currentBirthdayNPC = null; // Wir behalten das für die Grafik bei (zeigt den 1. NPC an)

            // FAKT: Wir durchsuchen alle Charaktere nur ein einziges Mal
            foreach (NPC npc in Utility.getAllCharacters())
            {
                // FAKT: Wir prüfen zusätzlich, ob dieser NPC-Name nicht schon in der Liste steht!
                if (npc.isBirthday() && npc.CanReceiveGifts() && !this.birthdayNPCs.Exists(n => n.Name == npc.Name))
                {
                    this.birthdayNPCs.Add(npc);
                }
            }

            // FAKT: Initialisiert die Köpfe am Morgen sauber über die neue Logik
            this.UpdateBoardGraphicsState();

            if (this.saveData != null)
            {
                // 1. Jahreszeit- und Jahreswechsel prüfen (Reset der Zähler)
                int currentYear = Game1.year;
                string currentSeason = Game1.currentSeason;

                if (this.saveData.LastRecordedYear != currentYear)
                {
                    this.saveData.GiftsGivenThisYear = 0;
                    this.saveData.PossibleGiftsThisYear = 0;
                    this.saveData.GiftsGivenThisSeason = 0;
                    this.saveData.PossibleGiftsThisSeason = 0;
                    this.saveData.LastRecordedYear = currentYear;
                    this.saveData.LastRecordedSeason = currentSeason;
                }
                else if (this.saveData.LastRecordedSeason != currentSeason)
                {
                    this.saveData.GiftsGivenThisSeason = 0;
                    this.saveData.PossibleGiftsThisSeason = 0;
                    this.saveData.LastRecordedSeason = currentSeason;
                }

                // 2. Heutige Geburtstage zählen und zu den Max-Werten addieren
                // FAKT: Wir nutzen einfach die Anzahl der oben gefundenen NPCs!
                int todaysBirthdays = this.birthdayNPCs.Count;
                if (todaysBirthdays > 0)
                {
                    this.saveData.PossibleGiftsThisSeason += todaysBirthdays;
                    this.saveData.PossibleGiftsThisYear += todaysBirthdays;
                    this.saveData.PossibleGiftsTotal += todaysBirthdays;
                }
            }
        }

        private void CommandResetStats(string command, string[] args)
        {
            // FAKT: Überschreibt die aktuellen Daten im Spielstand mit einer frischen, leeren Liste
            this.saveData = new BirthdaySaveData();
            this.Monitor.Log("Die Geburtstags-Statistiken wurden erfolgreich auf 0 zurückgesetzt!", LogLevel.Info);
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            this.Monitor.Log("DayEnding Event wurde ausgelöst. Prüfe Geburtstage...", LogLevel.Debug);

            // Wenn es heute kein Geburtstagskind gab, direkt abbrechen
            if (this.currentBirthdayNPC == null)
            {
                this.Monitor.Log("Heute gab es kein Geburtstagskind. Zähler-Logik wird beendet.", LogLevel.Debug);
                return;
            }

            // Prüfen, ob das Geschenk heute erfolgreich übergeben wurde
            if (this.HasGivenBirthdayGift(this.currentBirthdayNPC))
            {
                this.Monitor.Log($"Geschenk an {this.currentBirthdayNPC.Name} wurde bestätigt. Lade Zähler...", LogLevel.Debug);

                // 1. Zähler aus dem Savegame (modData) auslesen
                string key = "Zebru.BirthdayBoard.TotalBirthdayGifts";
                int currentGifts = 0;

                if (Game1.player.modData.TryGetValue(key, out string savedValue))
                {
                    int.TryParse(savedValue, out currentGifts);
                }

                // 2. Zähler um 1 erhöhen und wieder speichern
                currentGifts++;
                Game1.player.modData[key] = currentGifts.ToString();

                // FAKT: Dynamische Log-Nachricht anhand der aktuellen Einstellungen
                string logProgress;
                if (currentGifts <= this.config.RequiredGiftsForBoard)
                {
                    // Ziel 1: Board
                    logProgress = $"{currentGifts} / {this.config.RequiredGiftsForBoard} (Board)";
                }
                else if (currentGifts <= this.config.RequiredGiftsForPhone)
                {
                    // Ziel 2: Telefon
                    logProgress = $"{currentGifts} / {this.config.RequiredGiftsForPhone} (Telefon)";
                }
                else
                {
                    // Alle Ziele erreicht, Endlos-Zähler
                    logProgress = $"{currentGifts} (Alle Ziele erreicht)";
                }

                this.Monitor.Log($"Zähler erfolgreich erhöht. Neuer Stand: {logProgress}", LogLevel.Info);

                // 3. Meilensteine dynamisch anhand der Config prüfen
                if (currentGifts == this.config.RequiredGiftsForBoard)
                {
                    this.Monitor.Log($"Fakt: Meilenstein von {this.config.RequiredGiftsForBoard} Geschenken erreicht! Board-Brief verschickt.", LogLevel.Info);
                    Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_RecipeMail");
                    this.Monitor.Log($"Fakt: Meilenstein von {this.config.RequiredGiftsForBoard} Geschenken erreicht! Poster-Brief verschickt.", LogLevel.Info);
                    Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_WallRecipeMail");
                }
                // FAKT: Das Telefon wird nur verschickt, wenn die Einstellung aktiviert ist
                else if (this.config.EnablePhone && currentGifts == this.config.RequiredGiftsForPhone)
                {
                    this.Monitor.Log($"Fakt: Meilenstein von {this.config.RequiredGiftsForPhone} Geschenken erreicht! Telefon-Brief verschickt.", LogLevel.Info);
                    Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_PhoneMail");
                }
            }
            else
            {
                this.Monitor.Log($"Das heutige Geburtstagskind ({this.currentBirthdayNPC.Name}) hat KEIN Geschenk erhalten. Zähler bleibt unverändert.", LogLevel.Debug);
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // FAKT: Kombiniert Mod-Name, Speicherstand-Name (Farm) und die exakte Spieler-ID
            string globalKey = $"{this.ModManifest.UniqueID}_{StardewModdingAPI.Constants.SaveFolderName}_{Game1.player.UniqueMultiplayerID}";

            // FAKT: ReadGlobalData liest die Datei sicher aus dem SMAPI-Kernordner
            this.saveData = this.Helper.Data.ReadGlobalData<BirthdaySaveData>(globalKey) ?? new BirthdaySaveData();

            // FAKT: Überprüft und vergibt das Rezept für das Stand-Board
            if (!Game1.player.craftingRecipes.ContainsKey("ZebrusBirthdayBoard"))
            {
                Game1.player.craftingRecipes.Add("ZebrusBirthdayBoard", 0);
                this.Monitor.Log($"Rezept Zebrus Birthday Board hinzugefügt.", LogLevel.Info);
            }

            // FAKT: Überprüft und vergibt das Rezept für das Wandposter
            if (!Game1.player.craftingRecipes.ContainsKey("ZebrusBirthdayWallBoard"))
            {
                Game1.player.craftingRecipes.Add("ZebrusBirthdayWallBoard", 0);
                this.Monitor.Log($"Rezept Zebrus Birthday Wall Board hinzugefügt.", LogLevel.Info);
            }
        }

        private void OnSaving(object sender, StardewModdingAPI.Events.SavingEventArgs e)
        {
            if (this.saveData == null) return;

            // FAKT: Kombiniert Mod-Name, Speicherstand-Name (Farm) und die exakte Spieler-ID
            string globalKey = $"{this.ModManifest.UniqueID}_{StardewModdingAPI.Constants.SaveFolderName}_{Game1.player.UniqueMultiplayerID}";

            // FAKT: WriteGlobalData schreibt die Daten sicher an den globalen Ort
            this.Helper.Data.WriteGlobalData(globalKey, this.saveData);
        }

        private void OnUpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            // FAKT: Die Blockade (potentialGiftTarget == null) wurde hier entfernt, 
            // damit das Board sich auch dann aktualisiert, wenn wir keinen NPC anklicken!
            if (!Context.IsWorldReady || this.saveData == null) return;

            if (e.IsMultipleOf(15))
            {
                // FAKT: Prüft alle 15 Frames automatisch, ob jemand beschenkt wurde und rückt die Köpfe ggf. auf
                this.UpdateBoardGraphicsState();

                // Die alte Geschenk-Erfassungslogik
                if (this.potentialGiftTarget != null)
                {
                    bool isGiftGivenNow = this.HasGivenBirthdayGift(this.potentialGiftTarget);

                    if (!this.wasGiftAlreadyGiven && isGiftGivenNow)
                    {
                        this.RecordGift(this.potentialGiftTarget.displayName, this.potentialGiftItemName);
                        this.potentialGiftTarget = null;
                    }
                    else if (Vector2.Distance(Game1.player.Tile, this.potentialGiftTarget.Tile) > 3f)
                    {
                        this.potentialGiftTarget = null;
                    }
                }
            }
        }

        private void RecordGift(string npcName, string itemName)
        {
            // 1. Allgemeine Zähler erhöhen
            this.saveData.TotalGiftsGiven++;
            this.saveData.GiftsGivenThisYear++;
            this.saveData.GiftsGivenThisSeason++;

            // 2. NPC-Statistik aktualisieren
            if (this.saveData.NpcGiftCounts.ContainsKey(npcName))
                this.saveData.NpcGiftCounts[npcName]++;
            else
                this.saveData.NpcGiftCounts[npcName] = 1;

            // 3. Item-Statistik aktualisieren
            if (this.saveData.ItemGiftCounts.ContainsKey(itemName))
                this.saveData.ItemGiftCounts[itemName]++;
            else
                this.saveData.ItemGiftCounts[itemName] = 1;

            this.Monitor.Log($"Erfolgreich lokal gespeichert: 1x {itemName} an {npcName} übergeben.", LogLevel.Info);
        }

        private string GenerateFullStatsText()
        {
            // 1. Wir rufen direkt unsere live mitgezählten Max-Werte ab
            int birthdaysThisSeason = this.saveData.PossibleGiftsThisSeason;
            int birthdaysThisYear = this.saveData.PossibleGiftsThisYear;
            int totalPossibleGifts = this.saveData.PossibleGiftsTotal;

            // 2. Prozente berechnen (Verhindert Division durch Null)
            float seasonPercent = birthdaysThisSeason > 0 ? (this.saveData.GiftsGivenThisSeason / (float)birthdaysThisSeason) * 100f : 0f;
            float yearPercent = birthdaysThisYear > 0 ? (this.saveData.GiftsGivenThisYear / (float)birthdaysThisYear) * 100f : 0f;
            float totalPercent = totalPossibleGifts > 0 ? (this.saveData.TotalGiftsGiven / (float)totalPossibleGifts) * 100f : 0f;

            // 3. Spitzenreiter-NPC ermitteln (Am häufigsten beschenkt)
            string mostGiftedNpc = "Niemand";
            int mostGiftedNpcCount = 0;
            foreach (var kvp in this.saveData.NpcGiftCounts)
            {
                if (kvp.Value > mostGiftedNpcCount)
                {
                    mostGiftedNpcCount = kvp.Value;
                    mostGiftedNpc = kvp.Key;
                }
            }

            // 4. Meistverschenktes Item ermitteln (mit Anzahl)
            string mostGiftedItem = "Nichts";
            int mostGiftedItemCount = 0;
            foreach (var kvp in this.saveData.ItemGiftCounts)
            {
                if (kvp.Value > mostGiftedItemCount)
                {
                    mostGiftedItemCount = kvp.Value;
                    mostGiftedItem = kvp.Key;
                }
            }

            // FAKT: 5. Neue Logik für vergessene NPCs (Zufallswahl bei Gleichstand)
            int minGifts = int.MaxValue;
            List<string> potentialForgottenNpcs = new List<string>();

            foreach (NPC npc in Utility.getAllCharacters())
            {
                if (npc.CanReceiveGifts() && npc.Birthday_Day > 0)
                {
                    // Standardmäßig 0 Geschenke, wenn der NPC nicht im Dictionary steht
                    int giftCount = 0;
                    if (this.saveData.NpcGiftCounts.ContainsKey(npc.displayName))
                    {
                        giftCount = this.saveData.NpcGiftCounts[npc.displayName];
                    }

                    // Wenn wir eine neue Mindestzahl finden, Liste leeren und von vorne beginnen
                    if (giftCount < minGifts)
                    {
                        minGifts = giftCount;
                        potentialForgottenNpcs.Clear();
                        potentialForgottenNpcs.Add(npc.displayName);
                    }
                    // Wenn die Anzahl der Geschenke der aktuellen Mindestzahl entspricht, zur Liste hinzufügen
                    else if (giftCount == minGifts)
                    {
                        potentialForgottenNpcs.Add(npc.displayName);
                    }
                }
            }

            // Wähle zufällig einen NPC aus der Liste der potenziellen "Vergessenen" aus
            string forgottenNpc = "Niemand";
            if (potentialForgottenNpcs.Count > 0)
            {
                // FAKT: Game1.random ist der offizielle, unbestechliche Zufallsgenerator des Spiels
                forgottenNpc = potentialForgottenNpcs[Game1.random.Next(potentialForgottenNpcs.Count)];
            }


            // FAKT: 6. Den finalen Text in der NEUEN Reihenfolge zusammenbauen
            List<string> lines = new List<string>();

            lines.Add(string.Format(this.Helper.Translation.Get("statistics.next_birthdays"), this.GetNextBirthdaysText()));
            lines.Add(""); // Leerzeile vor Quoten

            // Quoten
            lines.Add(string.Format(this.Helper.Translation.Get("statistics.season"), this.saveData.GiftsGivenThisSeason, birthdaysThisSeason, seasonPercent.ToString("0.0")));
            lines.Add(string.Format(this.Helper.Translation.Get("statistics.year"), this.saveData.GiftsGivenThisYear, birthdaysThisYear, yearPercent.ToString("0.0")));
            lines.Add(string.Format(this.Helper.Translation.Get("statistics.total"), this.saveData.TotalGiftsGiven, totalPossibleGifts, totalPercent.ToString("0.0")));

            lines.Add(""); // Leerzeile vor Details

            // Details
            lines.Add(this.Helper.Translation.Get("statistics.details_title"));

            // Punkt 1: Spitzenreiter-NPC
            if (mostGiftedNpcCount > 0)
                lines.Add(string.Format(this.Helper.Translation.Get("statistics.most_gifted_npc"), mostGiftedNpc, mostGiftedNpcCount));

            // Punkt 2: Meistverschenktes Item mit Anzahl (Neu hinzugefügt)
            if (mostGiftedItemCount > 0)
                lines.Add(string.Format(this.Helper.Translation.Get("statistics.most_gifted_item_with_count"), mostGiftedItem, mostGiftedItemCount));

            // Punkt 3: Vergessene Zeile GANZ UNTEN (mit Anzahl)
            if (minGifts != int.MaxValue)
                lines.Add(string.Format(this.Helper.Translation.Get("statistics.forgotten_npc_with_count"), forgottenNpc, minGifts));

            return string.Join("\n", lines);
        }

        private string GetFavoriteItemText(NPC npc)
        {
            // FAKT: Lokale Variable für das Ergebnis dieser spezifischen Abfrage
            string resultText = "Leider wurden keine Items gefunden.";

            var giftTastes = Game1.NPCGiftTastes;

            if (npc == null || !giftTastes.TryGetValue(npc.Name, out string rawData))
                return resultText;

            string[] sections = rawData.Split('/');
            if (sections.Length < 2) return resultText;

            string[] loveIds = sections[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            List<Item> lovedItems = new List<Item>();

            foreach (string itemId in loveIds)
            {
                if (itemId.StartsWith("-")) continue;

                string qualifiedId = itemId.Contains("(") ? itemId : "(O)" + itemId;

                // Filter-Logik (Blacklist & Config)
                if (this.config != null && this.config.FilterRareItems &&
                    this.config.BlacklistedItemIds != null &&
                    this.config.BlacklistedItemIds.Contains(qualifiedId))
                {
                    continue;
                }

                if (this.config != null)
                {
                    if (this.config.FilterWine && qualifiedId == "(O)348") continue;
                    if (this.config.FilterTruffleOil && qualifiedId == "(O)432") continue;
                    if (this.config.FilterTruffle && qualifiedId == "(O)430") continue;
                    if (this.config.FilterPaleAle && qualifiedId == "(O)346") continue;
                    if (this.config.FilterStarfruit && qualifiedId == "(O)268") continue;
                    if (this.config.FilterAncientFruit && qualifiedId == "(O)114") continue;
                    if (this.config.FilterCaviar && qualifiedId == "(O)445") continue;
                    if (this.config.FilterIridiumBar && qualifiedId == "(O)337") continue;
                    if (this.config.FilterDiamond && qualifiedId == "(O)72") continue;
                    if (this.config.FilterFairyRose && qualifiedId == "(O)295") continue;
                }

                try
                {
                    Item item = ItemRegistry.Create(qualifiedId);
                    if (item != null) lovedItems.Add(item);
                }
                catch { /* Ignorieren */ }
            }

            // Sortierung nach Inventar-Bestand
            lovedItems.Sort((a, b) =>
            {
                int countA = this.CountInInventory(a);
                int countB = this.CountInInventory(b);
                if (countA != countB) return countB.CompareTo(countA);
                return a.DisplayName.CompareTo(b.DisplayName);
            });

            // FAKT: Grafik-Updates für das Board nur durchführen, wenn wir gerade das aktuelle HAUPT-Geburtstagskind abfragen
            if (npc == this.currentBirthdayNPC)
            {
                this.leftItemToDraw = lovedItems.Count > 0 ? lovedItems[0].QualifiedItemId : null;
                this.rightItemToDraw = lovedItems.Count > 1 ? lovedItems[1].QualifiedItemId : null;
            }

            // Text-Generierung
            if (lovedItems.Count > 0)
            {
                List<string> topItemNames = new List<string>();
                int maxItemsToShow = Math.Min(5, lovedItems.Count);

                for (int i = 0; i < maxItemsToShow; i++)
                {
                    Item currentItem = lovedItems[i];
                    if (currentItem != null)
                    {
                        int countInInv = this.CountInInventory(currentItem);
                        string displayName = currentItem.DisplayName;

                        // FAKT: Fix für SVE "Objektfehler" / FlashShifter IDs / Error Items
                        if (string.IsNullOrWhiteSpace(displayName) || displayName.Contains("Objektfehler") || displayName.Contains("Error Item") || displayName.Contains("FlashShifter") || displayName.Contains("_") || displayName.Length > 25)
                        {
                            continue; // FAKT: Statt den Namen zu ändern, ignorieren wir das kaputte Item komplett und rutschen zum nächsten!
                        }

                        // Längen-Check für das UI Layout
                        if (displayName.Contains("Objektfehler") || displayName.Length > 25)
                        {
                            displayName = "Unbekanntes Item";
                        }

                        topItemNames.Add($"{displayName} ({countInInv})");
                    }
                }
                resultText = string.Join(", ", topItemNames);
            }

            return resultText;
        }

        private void UpdateBoardGraphicsState()
        {
            if (this.birthdayNPCs.Count == 0)
            {
                this.currentBirthdayNPC = null;
                this.secondBirthdayNPC = null;
                return;
            }

            // FAKT: Alle NPCs filtern, die HEUTE noch KEIN Geschenk erhalten haben
            List<NPC> ungiftedNPCs = this.birthdayNPCs.Where(n => !this.HasGivenBirthdayGift(n)).ToList();

            if (ungiftedNPCs.Count == 0)
            {
                this.currentBirthdayNPC = null;
                this.secondBirthdayNPC = null;
                return;
            }

            // FAKT: Der erste in der verbleibenden Liste wird zum Hauptkopf
            NPC newMainNpc = ungiftedNPCs[0];

            // FAKT: Gibt es noch einen weiteren unbeschenkten NPC, wird er zum zweiten Kopf im Hintergrund
            NPC newSecondNpc = ungiftedNPCs.Count > 1 ? ungiftedNPCs[1] : null;

            // FAKT: Items nur neu aus dem Spiel laden, wenn ein neuer Hauptkopf nachgerückt ist
            if (this.currentBirthdayNPC != newMainNpc)
            {
                this.currentBirthdayNPC = newMainNpc;
                this.GetFavoriteItemText(this.currentBirthdayNPC);
            }

            this.secondBirthdayNPC = newSecondNpc;
        }

        private string GetNextBirthdaysText()
        {
            int found = 0;
            // Wir starten die Suche beim morgigen Tag
            StardewModdingAPI.Utilities.SDate checkDate = StardewModdingAPI.Utilities.SDate.Now().AddDays(1);
            List<string> lines = new List<string>();

            // FAKT: Wir suchen maximal 1 Jahr (112 Tage) in die Zukunft, um Endlosschleifen zu verhindern.
            for (int i = 0; i < 112 && found < 3; i++)
            {
                foreach (NPC npc in Utility.getAllCharacters())
                {
                    // FAKT: Wir wandeln den Season-Enum in einen Text um und vergleichen ihn
                    if (npc.CanReceiveGifts() && npc.Birthday_Day > 0 && npc.Birthday_Season.ToString().ToLower() == checkDate.Season.ToString().ToLower() && npc.Birthday_Day == checkDate.Day)
                    {
                        // Die Jahreszeit übersetzen lassen (z.B. "spring" -> "Frühling")
                        string seasonName = this.Helper.Translation.Get("season." + checkDate.Season);

                        // Text zusammenbauen: "- Abigail (13. Herbst)"
                        string line = string.Format(this.Helper.Translation.Get("statistics.entry"), npc.displayName, checkDate.Day, seasonName);
                        lines.Add(line);
                        found++;

                        if (found >= 3) break;
                    }
                }
                // Einen Tag weitergehen
                checkDate = checkDate.AddDays(1);
            }

            // Verbindet die 3 gefundenen Zeilen mit einer kompletten Leerzeile dazwischen
            return string.Join("\n", lines);
        }

        // Lokale Zähl-Funktion
        private int CountInInventory(Item itemToCheck)
        {
            // 1. Zuerst immer das Spieler-Inventar zählen
            int totalCount = Game1.player.Items
                .Where(i => i != null && i.QualifiedItemId == itemToCheck.QualifiedItemId)
                .Sum(i => i.Stack);

            if (this.config == null) return totalCount;

            // FAKT: Wir laden den Scope aus der Config in eine lokale Variable
            string effectiveScope = this.config.SearchScope;

            // FAKT: Wenn die neue Einstellung aktiv ist, prüfen wir den exakten Aufenthaltsort des Spielers
            if (this.config.PhoneInventoryOnlyOutsideFarm)
            {
                bool isOnFarm = false;
                if (Game1.currentLocation != null)
                {
                    // Ist er draußen auf dem Hof oder im Farmhaus?
                    if (Game1.currentLocation is Farm || Game1.currentLocation is StardewValley.Locations.FarmHouse)
                    {
                        isOnFarm = true;
                    }
                    else
                    {
                        // Ist er in einem selbst gebauten Gebäude auf dem Hof (Stall, Schuppen etc.)?
                        var farm = Game1.getFarm();
                        if (farm != null)
                        {
                            foreach (var building in farm.buildings)
                            {
                                if (building.indoors.Value != null && Game1.currentLocation == building.indoors.Value)
                                {
                                    isOnFarm = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // FAKT: Wenn er sich nicht auf dem Hof/in einem Hofgebäude befindet, erzwingen wir "Inventory"!
                if (!isOnFarm)
                {
                    effectiveScope = "Inventory";
                }
            }

            // FAKT: Wenn der (jetzt eventuell überschriebene) Scope "Inventory" ist, brechen wir hier sofort ab.
            if (effectiveScope != "Inventory")
            {
                List<GameLocation> locationsToSearch = new List<GameLocation>();

                // FAKT: Such-Scope anhand des effektiven Scopes ermitteln und die Maps laden
                if (effectiveScope == "World")
                {
                    Utility.ForEachLocation(delegate (GameLocation loc)
                    {
                        locationsToSearch.Add(loc);
                        return true;
                    });
                }
                else if (effectiveScope == "Farm")
                {
                    var farm = Game1.getFarm();
                    locationsToSearch.Add(farm);
                    foreach (var building in farm.buildings)
                    {
                        if (building.indoors.Value != null) locationsToSearch.Add(building.indoors.Value);
                    }
                    locationsToSearch.Add(Utility.getHomeOfFarmer(Game1.player));
                }
                else if (Game1.currentLocation != null) // Zieht bei "Location" und "Radius"
                {
                    locationsToSearch.Add(Game1.currentLocation);
                }

                // FAKT: Alle gesammelten Maps durchsuchen
                foreach (var loc in locationsToSearch)
                {
                    // Normale Truhen prüfen
                    foreach (StardewValley.Object obj in loc.Objects.Values)
                    {
                        if (obj is StardewValley.Objects.Chest chest)
                        {
                            // FAKT: Hier wird nun geprüft, ob im Dropdown explizit "Radius" ausgewählt ist
                            if (effectiveScope == "Radius" && loc == Game1.currentLocation)
                            {
                                Vector2 playerTile = Game1.player.Tile;
                                if (Math.Abs(chest.TileLocation.X - playerTile.X) > this.config.ChestSearchRadius ||
                                    Math.Abs(chest.TileLocation.Y - playerTile.Y) > this.config.ChestSearchRadius)
                                {
                                    continue; // Truhe ist zu weit weg
                                }
                            }

                            totalCount += chest.Items.Where(i => i != null && i.QualifiedItemId == itemToCheck.QualifiedItemId).Sum(i => i.Stack);
                        }
                    }

                    // FAKT: Kühlschränke werden bei World und Farm automatisch mitgezählt
                    if ((effectiveScope == "World" || effectiveScope == "Farm") && loc is StardewValley.Locations.FarmHouse farmHouse && farmHouse.fridge.Value != null)
                    {
                        totalCount += farmHouse.fridge.Value.Items.Where(i => i != null && i.QualifiedItemId == itemToCheck.QualifiedItemId).Sum(i => i.Stack);
                    }
                }
            }
            return totalCount;
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            // FAKT: Wir prüfen hier nur noch, ob die Welt geladen ist und der Raum existiert.
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;

            foreach (var furniture in Game1.currentLocation.furniture)
            {
                if (furniture.QualifiedItemId == "(F)Zebru.BirthdayBoard.CP" || furniture.QualifiedItemId == "(F)Zebru.BirthdayWallBoard.CP")
                {
                    Vector2 boardPos = furniture.TileLocation;
                    bool isWallBoard = furniture.QualifiedItemId == "(F)Zebru.BirthdayWallBoard.CP";

                    float headBaseY = isWallBoard ? (boardPos.Y * 64) - 40 : (boardPos.Y * 64) - 144;
                    float head2BaseY = isWallBoard ? (boardPos.Y * 64) - 46 : (boardPos.Y * 64) - 150;

                    // FAKT: Die Basis-Höhe für Items. Hier soll auch der Haken hin!
                    float itemsBaseY = isWallBoard ? (boardPos.Y * 64) + 70 : (boardPos.Y * 64) - 40;

                    float xOffsetItemsLeft = isWallBoard ? 24 : 20;
                    float xOffsetItemsRight = isWallBoard ? 72 : 76;

                    int headWidth = isWallBoard ? 48 : 64;
                    int headHeight = isWallBoard ? 72 : 96;

                    float time = (float)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
                    float yOffset = (float)Math.Sin(time / 200.0) * 12f;

                    // 1. Der springende NPC-Hauptkopf
                    Vector2 headPos = Game1.GlobalToLocal(Game1.viewport, new Vector2((boardPos.X * 64) + 32, headBaseY + yOffset));

                    // FAKT: Wir prüfen auf unsere neue, dynamische Variable für den zweiten Kopf!
                    if (this.secondBirthdayNPC != null)
                    {
                        int head2Width = isWallBoard ? 40 : 56;
                        int head2Height = isWallBoard ? 60 : 84;
                        float xOffset2 = isWallBoard ? 60 : 80;

                        float yOffset2 = (float)Math.Sin((time + 500) / 200.0) * 12f;
                        Vector2 headPos2 = Game1.GlobalToLocal(Game1.viewport, new Vector2((boardPos.X * 64) + xOffset2, head2BaseY + yOffset2));

                        // FAKT: Die Textur wird dynamisch vom zweiten unbeschenkten NPC geladen
                        Texture2D npcSprite2 = this.secondBirthdayNPC.Sprite.Texture;

                        e.SpriteBatch.Draw(npcSprite2, new Rectangle((int)headPos2.X, (int)headPos2.Y, head2Width, head2Height), new Rectangle(0, 0, 16, 24), Color.LightGray);
                    }

                    // FAKT: Wir prüfen zwingend, ob es aktuell noch einen unbeschenkten Haupt-NPC gibt!
                    if (this.currentBirthdayNPC != null)
                    {
                        // FAKT: Die Textur des aktuellen Haupt-NPCs
                        Texture2D npcSprite = this.currentBirthdayNPC.Sprite.Texture;
                        e.SpriteBatch.Draw(npcSprite, new Rectangle((int)headPos.X, (int)headPos.Y, headWidth, headHeight), new Rectangle(0, 0, 16, 24), Color.White);

                        // 2. Die Items zeichnen
                        if (this.config.ShowLovedItems)
                        {
                            if (this.leftItemToDraw != null)
                            {
                                var itemData = ItemRegistry.GetData(this.leftItemToDraw);
                                if (itemData != null)
                                {
                                    Vector2 leftPos = Game1.GlobalToLocal(Game1.viewport, new Vector2((boardPos.X * 64) + xOffsetItemsLeft, itemsBaseY));
                                    e.SpriteBatch.Draw(itemData.GetTexture(), new Rectangle((int)leftPos.X, (int)leftPos.Y, 32, 32), itemData.GetSourceRect(), Color.White);
                                }
                            }

                            if (this.rightItemToDraw != null)
                            {
                                var itemData = ItemRegistry.GetData(this.rightItemToDraw);
                                if (itemData != null)
                                {
                                    Vector2 rightPos = Game1.GlobalToLocal(Game1.viewport, new Vector2((boardPos.X * 64) + xOffsetItemsRight, itemsBaseY));
                                    e.SpriteBatch.Draw(itemData.GetTexture(), new Rectangle((int)rightPos.X, (int)rightPos.Y, 32, 32), itemData.GetSourceRect(), Color.White);
                                }
                            }
                        }
                    }
                    else
                    {
                        // FAKT: Alles erledigt! Wir zeichnen dein eigenes PNG-Bild STATISCH mittig auf das Pergament.

                        if (this.customCheckmarkTex != null)
                        {
                            // FAKT: Das Source-Rectangle ist das gesamte PNG-Bild (dynamische Erkennung der Originalgröße)
                            Rectangle checkmarkSource = new Rectangle(0, 0, this.customCheckmarkTex.Width, this.customCheckmarkTex.Height);

                            // FAKT: Wir skalieren dein Bild für das Board auf 32x32 Pixel (exakt die Größe eines Items).
                            int checkSize = 32;

                            // FAKT: Mittig auf dem Pergament positionieren
                            float checkX = (boardPos.X * 64) + 64 - (checkSize / 2f);
                            float checkY = itemsBaseY;

                            Vector2 checkPos = Game1.GlobalToLocal(Game1.viewport, new Vector2(checkX, checkY));

                            e.SpriteBatch.Draw(this.customCheckmarkTex, new Rectangle((int)checkPos.X, (int)checkPos.Y, checkSize, checkSize), checkmarkSource, Color.White);
                        }
                    }
                }
            }
        }

        private Vector2? FindBoardPosition()
        {
            if (Game1.currentLocation == null) return null;

            // Möbel werden in einer separaten Liste gespeichert
            foreach (var furniture in Game1.currentLocation.furniture)
            {
                // FAKT: Möbel haben das Präfix (F) in der QualifiedItemId
                if (furniture.QualifiedItemId == "(F)Zebru.BirthdayBoard.CP" || furniture.QualifiedItemId == "(F)Zebru.BirthdayWallBoard.CP")
                {
                    // Gibt die Basis-Koordinate (oben links) des 2-Kacheln-breiten Boards zurück
                    return furniture.TileLocation;
                }
            }
            return null;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.Button.IsActionButton()) return;
            if (Game1.activeClickableMenu != null) return;

            // FAKT: Wir prüfen, ob der Spieler einen NPC anklickt und ein Geschenk in der Hand hält
            if (e.Button.IsActionButton() && Game1.currentLocation != null)
            {
                NPC clickedNPC = Game1.currentLocation.isCharacterAtTile(e.Cursor.GrabTile);

                // Wenn es ein Geburtstagskind ist und der Spieler ein gültiges Geschenk hält
                if (clickedNPC != null && clickedNPC.isBirthday() && Game1.player.CurrentItem != null && Game1.player.CurrentItem.canBeGivenAsGift())
                {
                    this.potentialGiftTarget = clickedNPC;
                    this.potentialGiftItemName = Game1.player.CurrentItem.DisplayName; // Wir speichern den übersetzten Namen für die Anzeige
                    this.wasGiftAlreadyGiven = this.HasGivenBirthdayGift(clickedNPC);
                }
            }

            bool isClickingBoard = false;

            // FAKT: Wir durchsuchen den Raum und nutzen nun pixelgenaue Kollision statt fehlerhaftem "GrabTile"
            if (Game1.currentLocation != null)
            {
                foreach (var furniture in Game1.currentLocation.furniture)
                {
                    if (furniture.QualifiedItemId == "(F)Zebru.BirthdayBoard.CP" || furniture.QualifiedItemId == "(F)Zebru.BirthdayWallBoard.CP")
                    {
                        Vector2 boardPos = furniture.TileLocation;
                        bool isWallBoard = furniture.QualifiedItemId == "(F)Zebru.BirthdayWallBoard.CP";

                        // Wir holen uns die absoluten, exakten Pixel-Koordinaten der Maus in der Welt
                        Vector2 mousePixels = e.Cursor.AbsolutePixels;

                        // FAKT: Wir holen uns die Kachel (Tile), auf die der Controller des Spielers zielt
                        Vector2 grabTile = e.Cursor.GrabTile;

                        // FAKT: Wir definieren den exakten, klickbaren Bereich in Pixeln (Breite x Höhe)
                        Rectangle clickArea;
                        if (isWallBoard)
                        {
                            // Wandposter: Kopf schwebt bei Y-1, das Poster belegt Y und Y+1. 
                            clickArea = new Rectangle((int)boardPos.X * 64, (int)(boardPos.Y - 1) * 64, 128, 192);
                        }
                        else
                        {
                            // Stand-Board: Kopf schwebt hoch bei Y-2, Board belegt optisch Y-1 und Y.
                            clickArea = new Rectangle((int)boardPos.X * 64, (int)(boardPos.Y - 2) * 64, 128, 192);
                        }

                        // FAKT: 1. Prüfung -> Maus-Klick (Pixel-Kollision + Reichweiten-Check)
                        bool clickedWithMouse = clickArea.Contains((int)mousePixels.X, (int)mousePixels.Y) &&
                                                Vector2.Distance(Game1.player.Tile, boardPos) <= 4f;

                        // FAKT: 2. Prüfung -> Controller-Klick (Kachel-Kollision)
                        // Das Board belegt auf der X-Achse 2 Kacheln (boardPos.X und boardPos.X + 1).
                        // Auf der Y-Achse belegt es die Kacheln von Y-2 bis Y.
                        bool clickedWithController = grabTile.X >= boardPos.X && grabTile.X <= boardPos.X + 1 &&
                                                     grabTile.Y >= boardPos.Y - 2 && grabTile.Y <= boardPos.Y;

                        // FAKT: Wenn entweder die Maus ODER der Controller das Board trifft, markieren wir es als angeklickt
                        if (clickedWithMouse || clickedWithController)
                        {
                            isClickingBoard = true;
                            break; // Treffer! Wir müssen die restlichen Möbel nicht mehr prüfen.
                        }
                    }
                }
            }

            bool isUsingPhone = this.config.EnablePhone &&
                                Game1.player.CurrentItem != null &&
                                Game1.player.CurrentItem.QualifiedItemId == "(O)Zebru.BirthdayBoard_Phone";

            if (!isClickingBoard && !isUsingPhone) return;

            this.Helper.Input.Suppress(e.Button);
            Game1.playSound(isUsingPhone ? "phone" : "bigSelect");

            // --- 1. Liste für den ersten Tab (Heute) generieren ---
            List<string> todayTextPages = new List<string>();

            // FAKT: SCHRITT 1 - Zuerst alle Geburtstagskinder eintragen (Seite 1 bis X)
            foreach (var npc in this.birthdayNPCs)
            {
                string itemText = this.GetFavoriteItemText(npc).Replace(", ", "\n- ");
                string npcName = npc.displayName;
                string finalMessage;

                if (this.HasGivenBirthdayGift(npc))
                    finalMessage = string.Format(this.Helper.Translation.Get("dialogue.thanks"), npcName);
                else if (!this.config.ShowLovedItems)
                    finalMessage = string.Format(this.Helper.Translation.Get("dialogue.simple_birthday"), npcName);
                else
                    finalMessage = string.Format(this.Helper.Translation.Get("dialogue.wishlist"), npcName, itemText);

                todayTextPages.Add(finalMessage);
            }

            // FAKT: SCHRITT 2 - Danach den eigenen Hochzeitstag prüfen und ans Ende heften
            if (Game1.player.friendshipData != null)
            {
                foreach (string npcName in Game1.player.friendshipData.Keys)
                {
                    var friendship = Game1.player.friendshipData[npcName];
                    if (friendship.IsMarried() && friendship.WeddingDate != null)
                    {
                        // FAKT: Prüft, ob Jahreszeit und Tag exakt mit dem heutigen Datum übereinstimmen
                        if (friendship.WeddingDate.Season.ToString().ToLower() == Game1.currentSeason.ToLower() &&
                            friendship.WeddingDate.DayOfMonth == Game1.dayOfMonth)
                        {
                            int yearsMarried = Game1.year - friendship.WeddingDate.Year;

                            if (yearsMarried > 0)
                            {
                                string spouseDisplayName = Game1.getCharacterFromName(npcName)?.displayName ?? npcName;
                                string anniversaryMessage = string.Format(this.Helper.Translation.Get("dialogue.anniversary"), spouseDisplayName, yearsMarried);
                                todayTextPages.Add(anniversaryMessage);
                            }
                        }
                    }
                }
            }

            // FAKT: SCHRITT 3 - Wenn die Liste leer ist (weder Geburtstag noch Hochzeitstag)
            if (todayTextPages.Count == 0)
            {
                todayTextPages.Add(this.Helper.Translation.Get("dialogue.no_birthday"));
            }

            // --- 2. Text für den zweiten Tab (Statistik) generieren ---
            string statsText = this.GenerateFullStatsText();

            // --- 3. Das eigene Menü öffnen und alle DREI Titel mitgeben! ---
            string tab1Title = this.Helper.Translation.Get("menu.tab_today");
            string tab2Title = this.Helper.Translation.Get("menu.tab_stats");
            string tab3Title = this.Helper.Translation.Get("menu.tab_calendar");

            Game1.activeClickableMenu = new BirthdayMenu(todayTextPages, statsText, tab1Title, tab2Title, tab3Title);
        }



        private bool HasGivenBirthdayGift(NPC npc)
        {
            if (npc == null || Game1.player == null || Game1.player.friendshipData == null)
                return false;

            // FAKT: Wir prüfen, ob der Spieler den NPC überhaupt schon kennt
            if (Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship))
            {
                // FAKT: Wenn der Zähler für heute bei 1 (oder größer) liegt, wurde das Geschenk gemacht.
                return friendship.GiftsToday > 0;
            }

            return false;
        }

        // 1. Rezept vergessen
        private void CommandForgetRecipe(string command, string[] args)
        {
            bool removedBoard = Game1.player.craftingRecipes.Remove("ZebrusBirthdayBoard");
            bool removedWallBoard = Game1.player.craftingRecipes.Remove("ZebrusBirthdayWallBoard");

            if (removedBoard || removedWallBoard)
            {
                this.Monitor.Log("Die Rezepte für das Birthday Board und das Wallboard wurden erfolgreich vergessen.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log("Der Spieler kannte diese Rezepte noch gar nicht.", LogLevel.Info);
            }
        }

        // 2. Rezept lernen (Mail erzwingen)
        private void CommandForceMail(string command, string[] args)
        {
            // FAKT: Fügt die Mail-ID der Warteschlange für den nächsten Morgen hinzu
            Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_RecipeMail");
            Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_WallRecipeMail");
            this.Monitor.Log("Fakt: Der Brief liegt bereit. Gehe im Spiel schlafen, um ihn am nächsten Morgen im Briefkasten zu finden.", LogLevel.Info);
        }

        // 3. Geschenk gegeben
        private void CommandGiftGiven(string command, string[] args)
        {
            if (this.currentBirthdayNPC != null && Game1.player.friendshipData.TryGetValue(this.currentBirthdayNPC.Name, out var friendship))
            {
                friendship.GiftsToday = 1;
                this.Monitor.Log($"Fakt: Der Geschenk-Status für {this.currentBirthdayNPC.Name} wurde auf 1 (geschenkt) gesetzt. Das Board updatet sich im nächsten Frame.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log("Fakt: Heute hat niemand Geburtstag oder der NPC ist dem Spieler noch völlig unbekannt.", LogLevel.Warn);
            }
        }

        // 4. Geschenk noch nicht gegeben
        private void CommandGiftReset(string command, string[] args)
        {
            if (this.currentBirthdayNPC != null && Game1.player.friendshipData.TryGetValue(this.currentBirthdayNPC.Name, out var friendship))
            {
                friendship.GiftsToday = 0;
                this.Monitor.Log($"Fakt: Der Geschenk-Status für {this.currentBirthdayNPC.Name} wurde auf 0 (nicht geschenkt) zurückgesetzt.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log("Fakt: Heute hat niemand Geburtstag oder der NPC ist dem Spieler noch völlig unbekannt.", LogLevel.Warn);
            }
        }

        private void CommandAddGift(string command, string[] args)
        {
            string key = "Zebru.BirthdayBoard.TotalBirthdayGifts";
            int currentGifts = 0;

            // FAKT: Alten Wert auslesen
            if (Game1.player.modData.TryGetValue(key, out string savedValue))
            {
                int.TryParse(savedValue, out currentGifts);
            }

            // FAKT: Um 1 erhöhen und speichern
            currentGifts++;
            Game1.player.modData[key] = currentGifts.ToString();

            // FAKT: Die dynamische Log-Nachricht nun auch hier im manuellen Command
            string logProgress;
            if (currentGifts <= this.config.RequiredGiftsForBoard)
            {
                logProgress = $"{currentGifts} / {this.config.RequiredGiftsForBoard} (Board)";
            }
            else if (currentGifts <= this.config.RequiredGiftsForPhone)
            {
                logProgress = $"{currentGifts} / {this.config.RequiredGiftsForPhone} (Telefon)";
            }
            else
            {
                logProgress = $"{currentGifts} (Alle Ziele erreicht)";
            }

            this.Monitor.Log($"Fakt: Der Geschenk-Zähler wurde manuell um 1 erhöht. Neuer Stand: {logProgress}", LogLevel.Info);

            // FAKT: Brief-Trigger dynamisch anhand der Config prüfen (inkl. EnablePhone-Check!)
            if (currentGifts == this.config.RequiredGiftsForBoard)
            {
                this.Monitor.Log($"Fakt: Meilenstein von {this.config.RequiredGiftsForBoard} Geschenken erreicht! Board-Brief verschickt.", LogLevel.Info);
                Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_RecipeMail");
                this.Monitor.Log($"Fakt: Meilenstein von {this.config.RequiredGiftsForBoard} Geschenken erreicht! Poster-Brief verschickt.", LogLevel.Info);
                Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_WallRecipeMail");
            }
            else if (this.config.EnablePhone && currentGifts == this.config.RequiredGiftsForPhone)
            {
                this.Monitor.Log($"Fakt: Meilenstein von {this.config.RequiredGiftsForPhone} Geschenken erreicht! Telefon-Brief verschickt.", LogLevel.Info);
                Game1.player.mailForTomorrow.Add("Zebru.BirthdayBoard_PhoneMail");
            }
        }

        private void CommandResetGiftCounter(string command, string[] args)
        {
            string key = "Zebru.BirthdayBoard.TotalBirthdayGifts";

            // FAKT: Überschreibt den aktuellen Wert im modData-Dictionary des Spielers hart mit "0".
            Game1.player.modData[key] = "0";

            // FAKT: Gibt eine saubere Bestätigung in der SMAPI-Konsole aus.
            this.Monitor.Log("Fakt: Der Geschenk-Zähler wurde manuell auf 0 zurückgesetzt.", LogLevel.Info);
        }

        private void CommandTestDouble(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Fakt: Die Welt muss geladen sein, um NPCs zu finden.", LogLevel.Warn);
                return;
            }

            // FAKT: Wir leeren die Liste und füllen sie manuell für den Test
            this.birthdayNPCs.Clear();

            NPC lewis = Game1.getCharacterFromName("Lewis");
            NPC robin = Game1.getCharacterFromName("Robin");

            if (lewis != null) this.birthdayNPCs.Add(lewis);
            if (robin != null) this.birthdayNPCs.Add(robin);

            // FAKT: Wir lassen die neue Aufrück-Logik die Köpfe und Items vollautomatisch sortieren!
            this.UpdateBoardGraphicsState();

            this.Monitor.Log($"Fakt: Test-Modus aktiv. Es werden jetzt {this.birthdayNPCs.Count} Geburtstagskinder simuliert (Lewis & Robin).", LogLevel.Info);
        }

        private void CommandTestAnniversary(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Fakt: Die Welt muss geladen sein.", LogLevel.Warn);
                return;
            }

            // FAKT: Standardmäßig nehmen wir Abigail, falls du keinen Namen eingibst
            string spouseName = args.Length > 0 ? args[0] : "Abigail";

            NPC spouse = Game1.getCharacterFromName(spouseName);
            if (spouse == null)
            {
                this.Monitor.Log($"Fakt: Der NPC '{spouseName}' existiert nicht. Bitte auf die genaue englische Schreibweise achten (z.B. Haley, Harvey).", LogLevel.Warn);
                return;
            }

            // FAKT: Falls der Spieler den NPC noch gar nicht kennt, legen wir ein leeres Profil an
            if (!Game1.player.friendshipData.ContainsKey(spouseName))
            {
                Game1.player.friendshipData.Add(spouseName, new StardewValley.Friendship());
            }

            var friendship = Game1.player.friendshipData[spouseName];

            // FAKT: Wir verheiraten den Spieler hart per Code
            friendship.Status = StardewValley.FriendshipStatus.Married;

            // FAKT: Wir setzen das Hochzeitsdatum auf EXAKT heute, aber im vorherigen Jahr (Game1.year - 1)
            int testYear = Math.Max(1, Game1.year - 1); // Verhindert Jahr 0, falls du im ersten Jahr testest
            friendship.WeddingDate = new StardewValley.WorldDate(testYear, Game1.currentSeason, Game1.dayOfMonth);

            this.Monitor.Log($"Fakt: Test-Modus aktiv. Du bist jetzt mit {spouseName} verheiratet und heute ist euer Hochzeitstag! Klicke auf das Board.", LogLevel.Info);
        }
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatValue = null, string fieldId = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
    }
}