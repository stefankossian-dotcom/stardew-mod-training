using System;
using System.Dynamic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.SpecialOrders;

namespace ZebrusQuestBoard
{
    public class ModEntry : Mod
    {
        private ModConfig config;

        // IDs
        private const string NormalItemId = "ZebrusQuestBoardItem";
        private const string GoldItemId = "ZebrusGoldBoardItem";
        private const string IridiumItemId = "ZebrusIridiumBoardItem";
        private const string ModBoardItemId = "ZebrusModBoardItem";
        private const string RaccoonItemId = "ZebrusRaccoonItem";
        private const string FishItemId = "ZebrusFishItem";
        
        // Rezept Namen
        private const string NormalRecipeName = "Zebrus Quest Board";
        private const string GoldRecipeName = "Zebrus Gold Quest Board";
        private const string IridiumRecipeName = "Zebrus Iridium Quest Board";
        private const string ModRecipeName = "Zebrus Mod Quest Board";
        private const string RaccoonRecipeName = "Zebrus Raccoon Statue";
        private const string FishRecipeName = "Zebrus Fish Statue";

        // Mail IDs
        private const string GoldMailId = "ZebrusGoldBoardMail";
        private const string QiMailId = "ZebrusQiMail";

        // Variable für deine Icons
        private Texture2D customIconTexture;
        private Texture2D fishEventIconTexture;

        // --- STATUS CACHE ---
        private bool isFishPondQuestActive = false;
        private bool wasFishPondQuestActive = false; // Um zu merken, ob sich was geändert hat

        private List<string> activeModBoards = new List<string>();
        private int currentBoardIndex = 0;
        private StardewValley.Menus.ClickableTextureComponent rightArrow;
        private StardewValley.Menus.ClickableTextureComponent leftArrow;
        private List<string> activeVanillaBoards = new List<string>();
        private bool isVanillaMode = false; // True = Wir blättern Vanilla, False = Wir blättern Mods


        // --- DEBUG VARIABLE ---
        private bool debugForceQi = false;
        private bool debugForceRaccoon = false; 
        private bool debugForcePond = false;
        private bool debugForceFishingEvent = false;
        private bool debugFishPondItem = false;
        private int lastTimesFedRaccoons = -1;

        // Das ist der Live-Speicher für die Zahlen des Spielers während er spielt
        public QuestStats playerStats;
        private List<string> countedOrdersToday = new List<string>();
        // Der anklickbare Tab an der Seite des Menüs
        private ClickableTextureComponent statsTab;
        private ClickableTextureComponent questTab;
        // Merkt sich, ob der Spieler gezielt das Iridium-Brett angeklickt hat
        private bool isIridiumBoardActive = false;


        public override void Entry(IModHelper helper)
        {
            this.config = helper.ReadConfig<ModConfig>();

            // FAKT: Sicheres Laden der Texturen beim Start / Getrennte Präfixe für die beiden Icon-Dateien beim Spielstart
            string iconPrefix = this.config.UseNotbelovelyIcons ? "notbelovely_" : "";
            this.customIconTexture = LoadTextureSafely("custom_icons.png", iconPrefix);

            string fishIconPrefix = this.config.UseNotbelovelyFishIcon ? "notbelovely_" : "";
            this.fishEventIconTexture = LoadTextureSafely("fishicon.png", fishIconPrefix);

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked; // // Registriere den Timer (1x pro Sekunde)
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            
            // --- DEBUG 1: GOLD RESET ---
            helper.ConsoleCommands.Add("reset_board", "Löscht Gold-Rezept und Robin-Brief.", (command, args) =>
            {
                if (Game1.player.craftingRecipes.ContainsKey(GoldRecipeName))
                {
                    Game1.player.craftingRecipes.Remove(GoldRecipeName);
                    this.Monitor.Log($"Rezept '{GoldRecipeName}' gelöscht.", LogLevel.Info);
                }

                if (Game1.player.mailReceived.Contains(GoldMailId)) Game1.player.mailReceived.Remove(GoldMailId);
                if (Game1.player.mailbox.Contains(GoldMailId)) Game1.player.mailbox.Remove(GoldMailId);

                this.Monitor.Log("Gold-Reset komplett!", LogLevel.Alert);
            });

            // --- DEBUG 2: IRIDIUM/QI RESET ---
            helper.ConsoleCommands.Add("reset_boardqi", "Löscht Iridium-Rezept und Qi-Brief.", (command, args) =>
            {
                if (Game1.player.craftingRecipes.ContainsKey(IridiumRecipeName))
                {
                    Game1.player.craftingRecipes.Remove(IridiumRecipeName);
                    this.Monitor.Log($"Rezept '{IridiumRecipeName}' gelöscht.", LogLevel.Info);
                }

                if (Game1.player.mailReceived.Contains(QiMailId)) Game1.player.mailReceived.Remove(QiMailId);
                if (Game1.player.mailbox.Contains(QiMailId)) Game1.player.mailbox.Remove(QiMailId);

                this.Monitor.Log("Qi-Reset komplett!", LogLevel.Alert);
            });

            // --- DEBUG 3: FORCE UNLOCK QI ---
            helper.ConsoleCommands.Add("unlock_boardqi", "Erzwingt den Brief von Mr. Qi (auch ohne 100 Nüsse).", (command, args) =>
            {
                if (!Game1.player.hasOrWillReceiveMail(QiMailId))
                {
                    Game1.player.mailbox.Add(QiMailId);
                    this.Monitor.Log($"Brief '{QiMailId}' wurde erzwungen! Geh schlafen.", LogLevel.Alert);
                }
                else
                {
                    this.Monitor.Log($"Du hast den Brief '{QiMailId}' bereits (oder er kommt morgen).", LogLevel.Warn);
                }
            });

            // --- DEBUG 4: SIMULIERE QI QUEST ---
            helper.ConsoleCommands.Add("toggle_qi", "Simuliert, dass eine Qi-Quest aktiv ist (Blau !).", (command, args) =>
            {
                this.debugForceQi = !this.debugForceQi;
                this.Monitor.Log($"Qi Quest Simulation ist jetzt: {(this.debugForceQi ? "AN" : "AUS")}", LogLevel.Alert);
            });

            // --- DEBUG 5: SIMULIERE WASCHBÄR QUEST ---            
            helper.ConsoleCommands.Add("toggle_raccoon", "Simuliert, dass der Waschbär hungrig ist (Grünes Blatt/Waschbär Icon).", (command, args) =>
            {
                this.debugForceRaccoon = !this.debugForceRaccoon;
                this.Monitor.Log($"Waschbär Simulation ist jetzt: {(this.debugForceRaccoon ? "AN" : "AUS")}", LogLevel.Alert);
            });

            // --- DEBUG 6: SIMULIERE FISCHTEICH QUEST ---              
            helper.ConsoleCommands.Add("toggle_pond", "Simuliert, dass ein Fischteich ein Item braucht (Orangenes Icon).", (command, args) =>
            {
                this.debugForcePond = !this.debugForcePond;
                this.Monitor.Log($"Fischteich Simulation ist jetzt: {(this.debugForcePond ? "AN" : "AUS")}", LogLevel.Alert);
            });

            // --- DEBUG 7: BRIEF TESTEN (WASCHBÄR) ---
            helper.ConsoleCommands.Add("get_mail_raccoon", "Legt den Waschbär-Brief sofort in den Briefkasten.", (command, args) =>
            {
                // 1. Falls wir den Brief schon mal gelesen haben: Vergessen wir das!
                // Damit kannst du den Befehl immer wieder benutzen.
                if (Game1.player.mailReceived.Contains("ZebrusRaccoonMail"))
                {
                    Game1.player.mailReceived.Remove("ZebrusRaccoonMail");                    
                }

                // Rezept verlernen
                Game1.player.craftingRecipes.Remove(RaccoonRecipeName);

                // 2. Falls er noch nicht im Kasten liegt: Reinlegen!
                if (!Game1.player.mailbox.Contains("ZebrusRaccoonMail"))
                {
                    Game1.player.mailbox.Add("ZebrusRaccoonMail");
                    this.Monitor.Log("Erfolg: Der Waschbär-Brief liegt jetzt im Briefkasten!", LogLevel.Alert);
                }
                else
                {
                    this.Monitor.Log("Info: Der Brief liegt schon im Kasten.", LogLevel.Info);
                }
            });

            // --- DEBUG 8: BRIEF TESTEN (FISCHTEICH) ---
            helper.ConsoleCommands.Add("get_mail_fish", "Legt den Fischteich-Brief sofort in den Briefkasten.", (command, args) =>
            {
                // 1. "Gelesen"-Status entfernen
                if (Game1.player.mailReceived.Contains("ZebrusFishPondMail"))
                {
                    Game1.player.mailReceived.Remove("ZebrusFishPondMail");                    
                }

                // Rezept verlernen
                Game1.player.craftingRecipes.Remove(FishRecipeName);

                // 2. In den Kasten legen
                if (!Game1.player.mailbox.Contains("ZebrusFishPondMail"))
                {
                    Game1.player.mailbox.Add("ZebrusFishPondMail");
                    this.Monitor.Log("Erfolg: Willys Fisch-Brief liegt jetzt im Briefkasten!", LogLevel.Alert);
                }
                else
                {
                    this.Monitor.Log("Info: Der Brief liegt schon im Kasten.", LogLevel.Info);
                }
            });

            // --- DEBUG 9: ANGEL-EVENT SIMULIEREN ---
            helper.ConsoleCommands.Add("toggle_fishing_event", "Simuliert, dass heute ein Angel-Turnier stattfindet.", (command, args) =>
            {
                this.debugForceFishingEvent = !this.debugForceFishingEvent;
                this.Monitor.Log($"Angel-Event Simulation ist jetzt: {(this.debugForceFishingEvent ? "AN" : "AUS")}", LogLevel.Alert);
            });
            // --- DEBUG 10: GEISTER-QUESTS LÖSCHEN ---
            helper.ConsoleCommands.Add("clear_ghost_quests", "Löscht fehlerhafte Mod-Quests (z.B. Lumisteria) vom Brett.", (command, args) =>
            {
                if (!Context.IsWorldReady) return;

                int removedCount = 0;

                // FAKT: Wir müssen die Liste rückwärts durchlaufen, da wir Elemente löschen!
                for (int i = Game1.player.team.availableSpecialOrders.Count - 1; i >= 0; i--)
                {
                    var order = Game1.player.team.availableSpecialOrders[i];

                    // Wir prüfen den internen Namen der Quest
                    string questKey = order.questKey.Value;

                    // Wenn der Key das Wort "Lumisteria" enthält oder keinen gültigen Namen mehr hat
                    if (!string.IsNullOrEmpty(questKey) && questKey.Contains("Lumisteria"))
                    {
                        Game1.player.team.availableSpecialOrders.RemoveAt(i);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    this.Monitor.Log($"Erfolg: Es wurden {removedCount} fehlerhafte Geister-Quests vom Brett entfernt!", LogLevel.Alert);
                }
                else
                {
                    this.Monitor.Log("Keine Geister-Quests mit diesem Namen auf dem Brett gefunden.", LogLevel.Info);
                }
            });

            // Debug-Befehle für die SMAPI-Konsole registrieren
            helper.ConsoleCommands.Add("zq_setstat", "Setzt einen Quest-Zähler auf einen bestimmten Wert. Nutzung: zq_setstat <kategorie> <zahl> (Kategorien: town, qi, mod, daily)", this.CommandSetStat);
            helper.ConsoleCommands.Add("zq_completequests", "Setzt alle aktuell aktiven Spezialaufträge sofort auf den Status 'Abgeschlossen', um die Hintergrund-Zähllogik zu testen.", this.CommandCompleteQuests);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // 1. Holen wir uns die Content Patcher API
            var cpApi = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            if (cpApi != null)
            {
                // 2. Token für Waschbär registrieren
                // Der Token heißt dann: "Zebru.QuestBoard/EnableRaccoon" (Dein Mod-ID + Name)
                cpApi.RegisterToken(this.ModManifest, "EnableRaccoon", () =>
                {
                    return new[] { this.config.EnableRaccoon.ToString() };
                });

                // 3. Token für Fisch registrieren
                cpApi.RegisterToken(this.ModManifest, "EnableFish", () =>
                {
                    return new[] { this.config.EnableFish.ToString() };
                });

                // 4. Toekn für Mod Board registrieren
                cpApi.RegisterToken(this.ModManifest, "EnableModBoard", () =>
                {
                    return new[] { this.config.EnableModBoard.ToString() };
                });                
            }

            // --- ÜBERSETZUNGEN FÜR CONTENT PATCHER BEREITSTELLEN ---

            // Boards
            cpApi.RegisterToken(this.ModManifest, "NormalBoardName", () => new[] { this.Helper.Translation.Get("item.normal.name").ToString() });
            cpApi.RegisterToken(this.ModManifest, "NormalBoardDesc", () => new[] { this.Helper.Translation.Get("item.normal.description").ToString() });

            cpApi.RegisterToken(this.ModManifest, "GoldBoardName", () => new[] { this.Helper.Translation.Get("item.gold.name").ToString() });
            cpApi.RegisterToken(this.ModManifest, "GoldBoardDesc", () => new[] { this.Helper.Translation.Get("item.gold.description").ToString() });

            cpApi.RegisterToken(this.ModManifest, "IridiumBoardName", () => new[] { this.Helper.Translation.Get("item.iridium.name").ToString() });
            cpApi.RegisterToken(this.ModManifest, "IridiumBoardDesc", () => new[] { this.Helper.Translation.Get("item.iridium.description").ToString() });

            cpApi.RegisterToken(this.ModManifest, "ModBoardName", () => new[] { this.Helper.Translation.Get("item.modboard.name").ToString() });
            cpApi.RegisterToken(this.ModManifest, "ModBoardDesc", () => new[] { this.Helper.Translation.Get("item.modboard.description").ToString() });

            // Statuen
            cpApi.RegisterToken(this.ModManifest, "RaccoonName", () => new[] { this.Helper.Translation.Get("item.raccoon.name").ToString() });
            cpApi.RegisterToken(this.ModManifest, "RaccoonDesc", () => new[] { this.Helper.Translation.Get("item.raccoon.description").ToString() });

            cpApi.RegisterToken(this.ModManifest, "FishName", () => new[] { this.Helper.Translation.Get("item.fishstatue.name").ToString() });
            cpApi.RegisterToken(this.ModManifest, "FishDesc", () => new[] { this.Helper.Translation.Get("item.fishstatue.description").ToString() });

            // Briefe
            cpApi.RegisterToken(this.ModManifest, "MailRobin", () => new[] { this.Helper.Translation.Get("mail.robin.text").ToString() });
            cpApi.RegisterToken(this.ModManifest, "MailQi", () => new[] { this.Helper.Translation.Get("mail.qi.text").ToString() });
            cpApi.RegisterToken(this.ModManifest, "MailRaccoon", () => new[] { this.Helper.Translation.Get("mail.raccoon.text").ToString() });
            cpApi.RegisterToken(this.ModManifest, "MailFish", () => new[] { this.Helper.Translation.Get("mail.fish.text").ToString() });

            // notbelovely Grafiken
            cpApi.RegisterToken(this.ModManifest, "PrefixNormal", () => new[] { this.config.UseNotbelovelyNormalBoard ? "notbelovely_" : "" });
            cpApi.RegisterToken(this.ModManifest, "PrefixGold", () => new[] { this.config.UseNotbelovelyGoldBoard ? "notbelovely_" : "" });
            cpApi.RegisterToken(this.ModManifest, "PrefixIridium", () => new[] { this.config.UseNotbelovelyIridiumBoard ? "notbelovely_" : "" });
            cpApi.RegisterToken(this.ModManifest, "PrefixModBoard", () => new[] { this.config.UseNotbelovelyModBoard ? "notbelovely_" : "" });
            cpApi.RegisterToken(this.ModManifest, "PrefixRaccoon", () => new[] { this.config.UseNotbelovelyRaccoon ? "notbelovely_" : "" });
            cpApi.RegisterToken(this.ModManifest, "PrefixFish", () => new[] { this.config.UseNotbelovelyFish ? "notbelovely_" : "" });

            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () =>
                {
                    this.Helper.WriteConfig(this.config);

                    // FAKT: Sicheres Neuladen, wenn der Spieler im Menü speichert
                    string p1 = this.config.UseNotbelovelyIcons ? "notbelovely_" : "";
                    this.customIconTexture = LoadTextureSafely("custom_icons.png", p1);

                    string p2 = this.config.UseNotbelovelyFishIcon ? "notbelovely_" : "";
                    this.fishEventIconTexture = LoadTextureSafely("fishicon.png", p2);
                }
            );

            // Option 1: Gold
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.gold.name"),
                tooltip: () => this.Helper.Translation.Get("config.gold.tooltip"),
                getValue: () => this.config.EnableGoldBoard,
                setValue: value => this.config.EnableGoldBoard = value
            );

            // Option 2: Iridium
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.iridium.name"),
                tooltip: () => this.Helper.Translation.Get("config.iridium.tooltip"),
                getValue: () => this.config.EnableIridiumBoard,
                setValue: value => this.config.EnableIridiumBoard = value
            );

            // Option 3: Mod Board
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.modboard.name"),
                tooltip: () => this.Helper.Translation.Get("config.modboard.tooltip"),
                getValue: () => this.config.EnableModBoard,
                setValue: value => this.config.EnableModBoard = value
            );

            // Option 4: Waschbär-Statue 
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.raccoon.name"),
                tooltip: () => this.Helper.Translation.Get("config.raccoon.tooltip"),
                getValue: () => this.config.EnableRaccoon,
                setValue: value => this.config.EnableRaccoon = value                
            );

            // Option 5: Fischteich-Statue
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.fish.name"),
                tooltip: () => this.Helper.Translation.Get("config.fish.tooltip"),
                getValue: () => this.config.EnableFish,
                setValue: value => this.config.EnableFish = value                
            );
            // Option 6: Geister-Quest Bereinigung
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.ghostcleanup.name"),
                tooltip: () => this.Helper.Translation.Get("config.ghostcleanup.tooltip"),
                getValue: () => this.config.EnableGhostQuestCleanup,
                setValue: value => this.config.EnableGhostQuestCleanup = value
            );

            // --- ÜBERSCHRIFT FÜR ALTERNATIVE GRAFIKEN ---
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => this.Helper.Translation.Get("config.section_graphics.name")
            );

            // notbelovely Grafiken
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_normal.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_normal.tooltip"),
                getValue: () => this.config.UseNotbelovelyNormalBoard,
                setValue: value => this.config.UseNotbelovelyNormalBoard = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_gold.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_gold.tooltip"),
                getValue: () => this.config.UseNotbelovelyGoldBoard,
                setValue: value => this.config.UseNotbelovelyGoldBoard = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_iridium.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_iridium.tooltip"),
                getValue: () => this.config.UseNotbelovelyIridiumBoard,
                setValue: value => this.config.UseNotbelovelyIridiumBoard = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_mod.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_mod.tooltip"),
                getValue: () => this.config.UseNotbelovelyModBoard,
                setValue: value => this.config.UseNotbelovelyModBoard = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_raccoon.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_raccoon.tooltip"),
                getValue: () => this.config.UseNotbelovelyRaccoon,
                setValue: value => this.config.UseNotbelovelyRaccoon = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_fish.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_fish.tooltip"),
                getValue: () => this.config.UseNotbelovelyFish,
                setValue: value => this.config.UseNotbelovelyFish = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_icons.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_icons.tooltip"),
                getValue: () => this.config.UseNotbelovelyIcons,
                setValue: value => this.config.UseNotbelovelyIcons = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.nl_fishicon.name"),
                tooltip: () => this.Helper.Translation.Get("config.nl_fishicon.tooltip"),
                getValue: () => this.config.UseNotbelovelyFishIcon,
                setValue: value => this.config.UseNotbelovelyFishIcon = value
            );
        }

        private void OnSaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            // FAKT: Dein exakter Multiplayer- und Savegame-sicherer Schlüssel
            string globalKey = $"{this.ModManifest.UniqueID}_{StardewModdingAPI.Constants.SaveFolderName}_{Game1.player.UniqueMultiplayerID}";

            // Versuche, bestehende Daten zu laden
            this.playerStats = this.Helper.Data.ReadGlobalData<QuestStats>(globalKey);

            // FAKT: Wenn der Spieler die Mod neu hat, gibt es noch keine Datei. Dann erstellen wir eine leere Statistik mit 0.
            if (this.playerStats == null)
            {
                this.playerStats = new QuestStats();
            }

            this.countedOrdersToday.Clear();
        }

        private void OnSaving(object sender, StardewModdingAPI.Events.SavingEventArgs e)
        {
            if (this.playerStats != null)
            {
                // FAKT: Beim Speichern (Schlafen gehen) schreiben wir die aktuellen Zahlen zurück in die Datei
                string globalKey = $"{this.ModManifest.UniqueID}_{StardewModdingAPI.Constants.SaveFolderName}_{Game1.player.UniqueMultiplayerID}";
                this.Helper.Data.WriteGlobalData(globalKey, this.playerStats);
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // 1. Normales Rezept
            if (!Game1.player.craftingRecipes.ContainsKey(NormalRecipeName))
            {
                Game1.player.craftingRecipes.Add(NormalRecipeName, 0);
                this.Monitor.Log("Rezept für Zebrus Quest Board wurde dem Spieler hinzugefügt.", LogLevel.Info);
            }

            // 2. Gold Brief   // Game1.year >= 2 &&
            if (this.config.EnableGoldBoard &&
                SpecialOrder.IsSpecialOrdersBoardUnlocked() &&
                !Game1.player.hasOrWillReceiveMail(GoldMailId))
            {
                Game1.player.mailbox.Add(GoldMailId);
                this.Monitor.Log("Brief von Robin wurde verschickt!", LogLevel.Debug);
            }

            // 3. Qi Brief
            if (this.config.EnableIridiumBoard &&
                Game1.netWorldState.Value.GoldenWalnutsFound >= 100 &&
                !Game1.player.hasOrWillReceiveMail(QiMailId))
            {
                Game1.player.mailbox.Add(QiMailId);
                
                this.Monitor.Log("Brief von Qi wurde verschickt!", LogLevel.Debug);
            }

            // 4. MOD BOARD REZEPT VERGEBEN ---            
            if (this.config.EnableModBoard && !Game1.player.craftingRecipes.ContainsKey("Zebrus Mod Quest Board"))
            {
                // Rezept direkt beibringen (Die 0 bedeutet, es wurde bisher 0 mal hergestellt)
                Game1.player.craftingRecipes.Add("Zebrus Mod Quest Board", 0);
                this.Monitor.Log("Rezept für Zebrus Mod Quest Board wurde dem Spieler hinzugefügt.", LogLevel.Info);
            }

            // --- 1. WASCHBÄR BRIEF ---
            // Bedingung: 
            // - Config ist an
            // - Hat dem Waschbären schon mindestens 1x geholfen (TimesFedRaccoons > 0)
            // - Brief noch nicht erhalten oder im Kasten
            if (this.config.EnableRaccoon &&
                Game1.netWorldState.Value.TimesFedRaccoons > 0 &&
                !Game1.player.mailReceived.Contains("ZebrusRaccoonMail") &&
                !Game1.player.mailbox.Contains("ZebrusRaccoonMail"))
            {
                Game1.player.mailbox.Add("ZebrusRaccoonMail");
                this.Monitor.Log("Post vom Waschbär wurde zugestellt!", LogLevel.Info);
            }

            // --- 2. FISCHTEICH BRIEF ---
            // Bedingungen:
            // - Config ist an
            // - Hat einen Fischteich gebaut
            // - Brief noch nicht erhalten            
            if (this.config.EnableFish &&
                !Game1.player.mailReceived.Contains("ZebrusFishPondMail") &&
                !Game1.player.mailbox.Contains("ZebrusFishPondMail"))
            {
                bool hasPond = false;

                // NEU: Durchsuche ALLE Orte im Spiel (Farm, Ingwer-Insel, Mods)
                foreach (var location in Game1.locations)
                {
                    foreach (var building in location.buildings)
                    {
                        if (building is StardewValley.Buildings.FishPond)
                        {
                            hasPond = true;
                            break; // Bricht die Gebäude-Schleife ab
                        }
                    }
                    if (hasPond) break; // Bricht die Orte-Schleife ab, sobald einer gefunden wurde
                }

                if (hasPond)
                {
                    Game1.player.mailbox.Add("ZebrusFishPondMail");
                    this.Monitor.Log("Post von Willy (Fischteich) wurde zugestellt!", LogLevel.Info);
                }
            }

            // Reinigt das Auftragsbrett jeden Morgen automatisch von gelöschten Mods
            if (this.config.EnableGhostQuestCleanup)
            {
                CleanUpGhostQuests();
            }
        }

        private void OnButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            // Welt muss geladen sein, sonst brechen wir direkt ab
            if (!Context.IsWorldReady) return;

            // =================================================================
            // UI-KLICKS (Wenn eines unserer unterstützten Menüs offen ist)
            // =================================================================
            if (Game1.activeClickableMenu is StardewValley.Menus.SpecialOrdersBoard || Game1.activeClickableMenu is StardewValley.Menus.Billboard)
            {
                // Wir prüfen dynamisch, welche Liste wir zum Blättern nutzen müssen
                var activeList = this.isVanillaMode ? this.activeVanillaBoards : this.activeModBoards;

                if (e.Button == StardewModdingAPI.SButton.MouseLeft || e.Button == StardewModdingAPI.SButton.ControllerA)
                {
                    int mouseX = (int)e.Cursor.ScreenPixels.X;
                    int mouseY = (int)e.Cursor.ScreenPixels.Y;

                    // =========================================================
                    // KLICK AUF DEN STATISTIK-TAB
                    // =========================================================
                    // Erlaubt den Klick auf den Tab jetzt auch, wenn das normale Tagesquest-Billboard offen ist
                    if (this.statsTab != null && this.statsTab.containsPoint(mouseX, mouseY) && this.isVanillaMode && this.isIridiumBoardActive && (Game1.activeClickableMenu is StardewValley.Menus.SpecialOrdersBoard || Game1.activeClickableMenu is StardewValley.Menus.Billboard))
                    {
                        bool canOpenStats = false;

                        // Prüfen, ob die Liste Einträge hat, bevor wir darauf zugreifen
                        if (activeList != null && activeList.Count > 0)
                        {
                            string currentStringRaw = activeList[this.currentBoardIndex];
                            // Nur auslösen, wenn wir am Qi- oder Stadt-Board sind
                            if (currentStringRaw == "SpecialOrder_Qi" || currentStringRaw == "SpecialOrder_Town")
                            {
                                canOpenStats = true;
                            }
                        }
                        else
                        {
                            // Wenn die Liste leer ist (Fallback auf das leere Brett), 
                            // erlauben wir den Klick trotzdem, da wir am Iridium-Board sind.
                            canOpenStats = true;
                        }

                        if (canOpenStats)
                        {
                            Game1.playSound("smallSelect");
                            // this.Monitor.Log("Der Statistik-Tab wurde angeklickt!", StardewModdingAPI.LogLevel.Info);

                            // Öffnet unsere neue Menü-Klasse und übergibt die aktuellen Statistiken des Spielers
                            Game1.activeClickableMenu = new StatsMenu(
                                Game1.activeClickableMenu.xPositionOnScreen,
                                Game1.activeClickableMenu.yPositionOnScreen,
                                Game1.activeClickableMenu.width,
                                Game1.activeClickableMenu.height,
                                Game1.activeClickableMenu,
                                this.playerStats,
                                this.Helper.Translation);
                            this.Helper.Input.Suppress(e.Button);
                            return;
                        }
                    }

                    // =========================================================
                    // KLICK AUF PFEILE
                    // =========================================================
                    if (activeList != null && activeList.Count > 1)
                    {
                        // Klick auf den linken Pfeil
                        if (this.leftArrow != null && this.leftArrow.containsPoint(mouseX, mouseY))
                        {
                            this.currentBoardIndex--;
                            if (this.currentBoardIndex < 0) this.currentBoardIndex = activeList.Count - 1;

                            // Wenn wir im Vanilla-Modus sind, müssen wir die Hilfsmethode nutzen, 
                            // da sich die Menü-Klasse ändern kann.
                            if (this.isVanillaMode)
                            {
                                OpenVanillaBoardAndCreateArrows(activeList[this.currentBoardIndex]);
                            }
                            else
                            {
                                Game1.playSound("shwip");
                                Game1.activeClickableMenu = new StardewValley.Menus.SpecialOrdersBoard(activeList[this.currentBoardIndex]);
                            }
                            this.Helper.Input.Suppress(e.Button);
                            return;
                        }
                        // Klick auf den rechten Pfeil
                        else if (this.rightArrow != null && this.rightArrow.containsPoint(mouseX, mouseY))
                        {
                            this.currentBoardIndex++;
                            if (this.currentBoardIndex >= activeList.Count) this.currentBoardIndex = 0;

                            if (this.isVanillaMode)
                            {
                                OpenVanillaBoardAndCreateArrows(activeList[this.currentBoardIndex]);
                            }
                            else
                            {
                                Game1.playSound("shwip");
                                Game1.activeClickableMenu = new StardewValley.Menus.SpecialOrdersBoard(activeList[this.currentBoardIndex]);
                            }
                            this.Helper.Input.Suppress(e.Button);
                            return;
                        }
                    }
                }
            }

            // =================================================================
            // WELT-KLICKS (Wenn kein Menü offen ist)
            // =================================================================

            // Wenn der Spieler blockiert ist (z.B. in einer Zwischensequenz oder einem normalen Menü), brechen wir hier ab
            if (!Context.CanPlayerMove) return;

            // Wir erlauben Action-Button (Rechtsklick/PC) ODER Use-Tool-Button (Linksklick/Touch/Mobile)
            if (e.Button.IsActionButton() || e.Button.IsUseToolButton())
            {
                // Bei Controllern und Touch-Screens ist GrabTile oft die bessere Wahl
                Microsoft.Xna.Framework.Vector2 tile = e.Cursor.GrabTile;

                // Wir versuchen das Objekt auf dem GrabTile zu finden
                if (!Game1.currentLocation.Objects.TryGetValue(tile, out StardewValley.Object obj))
                {
                    // Falls da nichts ist, probieren wir das exakte Maus-Tile (Sicherheit für PC-Spieler)
                    tile = e.Cursor.Tile;
                    if (!Game1.currentLocation.Objects.TryGetValue(tile, out obj))
                    {
                        return; // Weder auf GrabTile noch auf Tile ein Objekt gefunden -> abbrechen
                    }
                }

                // WERKZEUG-CHECK (Damit man die Statue abreißen kann)
                if (e.Button.IsUseToolButton() && Game1.player.CurrentTool != null)
                {
                    // Wenn der Spieler eine Spitzhacke oder Axt in der Hand hat...
                    if (Game1.player.CurrentTool is StardewValley.Tools.Pickaxe ||
                        Game1.player.CurrentTool is StardewValley.Tools.Axe)
                    {
                        return; // ...brechen wir unsere Mod-Logik hier ab. 
                                // Das Spiel übernimmt und zerstört die Statue ganz normal!
                    }
                }

                // --- NORMAL ---
                if (obj.ItemId == NormalItemId)
                {
                    this.isIridiumBoardActive = false; // Schalter aus
                    this.activeVanillaBoards.Clear();
                    this.activeModBoards.Clear();
                    OpenDailyQuestBoard(e.Button);
                }

                // --- GOLD & IRIDIUM BOARD (Das All-in-One Vanilla Brett) ---
                else if (obj.ItemId == GoldItemId || obj.ItemId == IridiumItemId)
                {
                    // Schalter an, wenn es das Iridium-Brett ist. Bei Gold bleibt er aus.
                    this.isIridiumBoardActive = (obj.ItemId == IridiumItemId);

                    this.activeVanillaBoards.Clear();

                    // Die Reihenfolge der Add-Befehle bestimmt die Anzeige-Priorität!

                    // 1. Prio: Mr. Qi (Nur wenn es das Iridium-Board ist und Quests da sind)
                    if (obj.ItemId == IridiumItemId && this.config.EnableIridiumBoard && HasQiOrder())
                    {
                        this.activeVanillaBoards.Add("SpecialOrder_Qi");
                    }

                    // 2. Prio: Stadt-Aufträge (Lewis' Spezial-Brett)
                    if (HasSpecialOrder())
                    {
                        this.activeVanillaBoards.Add("SpecialOrder_Town");
                    }

                    // 3. Prio: Tägliche Hilfe (Pierres kleines Brett)
                    if (HasDailyQuest())
                    {
                        this.activeVanillaBoards.Add("DailyQuest");
                    }

                    // Wenn es mindestens eine Quest gibt...
                    if (this.activeVanillaBoards.Count > 0)
                    {
                        this.isVanillaMode = true; // Wir sind im Vanilla-Modus
                        this.currentBoardIndex = 0; // Wir starten beim allerersten Eintrag in der Liste (höchste Prio)

                        OpenVanillaBoardAndCreateArrows(this.activeVanillaBoards[this.currentBoardIndex]);
                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                    else
                    {
                        // Fallback, falls absolut gar nichts da ist (öffnet das leere Daily-Brett)
                        this.isVanillaMode = true;
                        OpenDailyQuestBoard(e.Button);
                    }
                }

                // --- MOD BOARD ---
                else if (obj.ItemId == ModBoardItemId)
                {
                    this.activeModBoards = GetAllAvailableModBoards();

                    if (this.activeModBoards.Count > 0)
                    {
                        this.isVanillaMode = false;
                        this.currentBoardIndex = 0;
                        Game1.playSound("bigSelect");
                        Game1.activeClickableMenu = new StardewValley.Menus.SpecialOrdersBoard(this.activeModBoards[this.currentBoardIndex]);

                        // Pfeile nur generieren, wenn es mehr als 1 Mod mit Quests gibt
                        if (this.activeModBoards.Count > 1)
                        {
                            int x = Game1.activeClickableMenu.xPositionOnScreen;
                            int y = Game1.activeClickableMenu.yPositionOnScreen;
                            int height = Game1.activeClickableMenu.height;
                            int width = Game1.activeClickableMenu.width;

                            // Die Standard-Pfeile aus dem Spiel nutzen (Maus-Cursor Datei)
                            this.leftArrow = new StardewValley.Menus.ClickableTextureComponent(
                                new Microsoft.Xna.Framework.Rectangle(x - 64, y + height - 128, 48, 44),
                                Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(352, 495, 12, 11), 4f);

                            this.rightArrow = new StardewValley.Menus.ClickableTextureComponent(
                                new Microsoft.Xna.Framework.Rectangle(x + width + 16, y + height - 128, 48, 44),
                                Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(365, 495, 12, 11), 4f);
                        }

                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                }
                // --- Waschbär --- 
                else if (obj.ItemId == RaccoonItemId)
                {
                    if (HasRaccoonQuest())
                    {
                        Game1.playSound("dustMeep");

                        string text = this.Helper.Translation.Get("hud.raccoon");
                        Game1.addHUDMessage(new HUDMessage(text, 2));
                        this.Helper.Input.Suppress(e.Button);
                    }
                }
                // --- Fischteiche und Event --- 
                else if (obj.ItemId == FishItemId)
                {
                    // Wir nutzen deine HasFishPondQuest Methode
                    if (this.isFishPondQuestActive)
                    {
                        Game1.playSound("waterSlosh");
                        string text = this.Helper.Translation.Get("hud.pond");
                        Game1.addHUDMessage(new HUDMessage(text, 2));
                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                    // Klick während eines Events
                    else if (IsFishingEvent())
                    {
                        Game1.playSound("bubbles");
                        string text = this.Helper.Translation.Get("hud.fishing");
                        Game1.addHUDMessage(new HUDMessage(text, 2));
                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                }
            }
        }

        private void OpenDailyQuestBoard(SButton button)
        {
            Game1.playSound("bigSelect");
            this.Helper.Input.Suppress(button);
            Game1.activeClickableMenu = new Billboard(true);
        }

        private void OpenSpecialOrders(SButton button, string boardType)
        {
            Game1.playSound("bigSelect");
            this.Helper.Input.Suppress(button);
            Game1.activeClickableMenu = new SpecialOrdersBoard(boardType == "Qi" ? "Qi" : "");
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            foreach (var pair in Game1.currentLocation.Objects.Pairs)
            {
                string id = pair.Value.ItemId;
                Vector2 tileLocation = pair.Key;

                // --- 1. IRIDIUM BOARD ---
                if (id == IridiumItemId && this.config.EnableIridiumBoard)
                {
                    if (HasQiOrder())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 2); // Index 2: Qi
                    else if (HasSpecialOrder())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 1); // Index 1: Special
                    else if (HasDailyQuest())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 0); // Index 0: Daily
                }

                // --- 2. GOLD BOARD ---
                else if (id == GoldItemId && this.config.EnableGoldBoard)
                {
                    if (HasSpecialOrder())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 1); // Special
                    else if (HasDailyQuest())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 0); // Daily
                }

                // --- 3. NORMAL BOARD ---
                else if (id == NormalItemId)
                {
                    if (HasDailyQuest())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 0); // Daily
                }
                // --- 4. MOD BOARD ---
                else if (id == ModBoardItemId)
                {
                    // Prüft, ob es eine Mod-Quest (z.B. RSV) gibt
                    if (GetAllAvailableModBoards().Count > 0)
                    {
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 5); // Dein weißes Ausrufezeichen
                    }
                }

                // --- Waschbär --- 
                else if (id == RaccoonItemId)
                {
                    // Index 3 war dein Waschbär-Icon im Sprite-Strip
                    if (HasRaccoonQuest())
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 3);
                }

                // --- Fischteich --- 
                else if (id == FishItemId)
                {
                    // Index 5 war das Fischteich-Icon (Orangener Tropfen) in deinem Sprite-Strip
                    if (this.isFishPondQuestActive)
                    {
                        DrawQuestIcon(e.SpriteBatch, tileLocation, 4);
                    }
                    // Priorität 2: Es ist ein Angel-Event (Dein neues, einzelnes Icon)
                    else if (IsFishingEvent())
                    {
                        DrawSingleIcon(e.SpriteBatch, tileLocation, this.fishEventIconTexture);
                    }
                }
            }
        }

        private bool HasDailyQuest()
        {
            if (Game1.questOfTheDay == null || Game1.questOfTheDay.currentObjective == null || Game1.questOfTheDay.currentObjective.Length == 0) return false;
            return !Game1.questOfTheDay.accepted.Value;
        }

        private bool HasSpecialOrder()
        {
            if (!SpecialOrder.IsSpecialOrdersBoardUnlocked()) return false;
            return !Game1.player.team.acceptedSpecialOrderTypes.Contains("");
        }

        private bool HasQiOrder()
        {
            // 1. Wenn Debug an ist, IMMER true zurückgeben (blaues Icon!)
            if (this.debugForceQi) return true;

            if (Game1.netWorldState.Value.GoldenWalnutsFound < 100) return false;
            return !Game1.player.team.acceptedSpecialOrderTypes.Contains("Qi");
        }

        private string GetExternModBoardType()
        {
            foreach (var order in Game1.player.team.availableSpecialOrders)
            {
                // Prüfen, ob eine Spezial-Quest aktiv ist
                if (order.orderType.Value != null)
                {
                    string type = order.orderType.Value;

                    if (type == "RSVNinjaSO" || type == "RSVTownSO")
                    {
                        // NEU: Wir prüfen, ob der Spieler für dieses Brett (Town oder Ninja) diese Woche schon was angenommen hat!
                        if (!Game1.player.team.acceptedSpecialOrderTypes.Contains(type))
                        {
                            return type; // Gibt "RSVTownSO" oder "RSVNinjaSO" zurück
                        }
                    }
                    else
                    {
                        // Prüfen auf alle Mods die auch normale Special Orders verwenden nich Standard oder RSV sind
                        if (type != "" && type != "Qi" && type != "RSVNinjaSO" && type != "RSVTownSO")
                        {
                            if (!Game1.player.team.acceptedSpecialOrderTypes.Contains(type))
                            {
                                //this.Monitor.Log("Anderer Quest Type gefunden (für die Liste): " + type, LogLevel.Alert);
                                // List: Esca.EMP/MtVapiusBoard
                                return type;
                            }
                        }
                    }
                }
            }
            return null; // Keine Ridgeside-Quest oder unbekannte Mods gefunden (oder alle schon angenommen!)
        }

        private List<string> GetAllAvailableModBoards()
        {
            List<string> foundBoards = new List<string>();

            foreach (var order in Game1.player.team.availableSpecialOrders)
            {
                string type = order.orderType.Value;

                // FAKT: Vanilla-Lewis-Brett ist "", Qi-Brett ist "Qi". Alles andere kommt von Mods!
                if (!string.IsNullOrEmpty(type) && type != "Qi")
                {
                    // Prüfen, ob der Spieler dieses Brett diese Woche schon bedient hat
                    if (!Game1.player.team.acceptedSpecialOrderTypes.Contains(type))
                    {
                        if (!foundBoards.Contains(type))
                        {
                            foundBoards.Add(type); // Internen Namen (z.B. "RSVTownSO") speichern
                        }
                    }
                }
            }
            return foundBoards;
        }

        private string GetModBoardDisplayName(string internalName)
        {
            switch (internalName)
            {
                // --- 100% FAKTEN ---
                case "RSVTownSO": return "Ridgeside Village";
                case "RSVNinjaSO": return "Ridgeside Ninja Board";

                // --- UNVERIFIZIERTE WERTE ---
                case "VapiusInnBoard": return "Visit Mount Vapius";
                case "MountVapius": return "Visit Mount Vapius";

                case "RedPandaBazaar": return "Red Panda Bazaar";
                case "Sunberry": return "Sunberry Village";
                
                case "EastScarp": return "East Scarp";
                case "EastScarpBoard": return "East Scarp";

                case "ZuzuBoard": return "Downtown Zuzu";
                case "DowntownZuzu": return "Downtown Zuzu";

                // Wenn der interne Name nicht in der Liste oben steht, wird er einfach direkt als Text ausgegeben!
                default: return internalName;
            }
        }

        //private string GetExternalModBoardType()
        //{
        //    List<string> supportedModBoards = new List<string>
        //    {
        //        "RSVTownSO",            // Ridgeside Village (RSVTownSO)
        //        "RSVNinjaSO",          // Ridgeside Village (RSVNinjaSO)
        //        "MountVapius",    // Mount Vapius
        //        "RedPandaBazaar", // Red Panda Bazaar
        //        "Sunberry"        // Sunberry Village
        //    };

        //    // Wir durchsuchen alle aktiven Quests auf den Brettern
        //    foreach (var order in Game1.player.team.availableSpecialOrders)
        //    {
        //        string orderType = order.orderType.Value;

        //        // Wenn die Quest zu einer unserer unterstützten Mods gehört...
        //        if (orderType != null && supportedModBoards.Contains(orderType))
        //        {
        //            // NEU: ...prüfen wir, ob der Spieler für dieses Brett (diese Woche) schon eine Quest angenommen hat!
        //            if (!Game1.player.team.acceptedSpecialOrderTypes.Contains(orderType))
        //            {
        //                // Nur wenn er noch KEINE angenommen hat, geben wir den Namen zurück und das Icon leuchtet.
        //                return orderType;
        //            }
        //        }
        //    }
        //    return null; // Keine annehmbaren Quests von diesen Mods gefunden
        //}

        // index 0 = Daily, 1 = Special, 2 = Qi, 3 = Raccoon, 4 = Fishing, 5 = Pond
        private void DrawQuestIcon(SpriteBatch b, Vector2 tileLocation, int index)
        {
            // --- EINSTELLUNGEN FÜR DEINE ICONS ---
            const int IconWidth = 7;   // DEINE NEUE BREITE
            const int IconHeight = 16; // Standard Höhe in Stardew (meistens 16)
            // -------------------------------------


            // 1. Animation berechnen (Schweben)
            float bob = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 200.0) * 5f;

            // 2. Position in der Welt berechnen
            // Wir nehmen die Mitte des Tiles (X * 64), gehen etwas hoch (-50 statt -64) und addieren das Schweben (bob).
            Vector2 pos = Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f, tileLocation.Y * 64f - 50f + bob));


            // 3. Welches Stück aus dem Bild schneiden wir aus?
            // X-Startpunkt = Index * Breite (z.B. 0*6=0, 1*6=6, 2*6=12...)
            // Breite ist jetzt 6.
            Rectangle sourceRect = new Rectangle(index * IconWidth, 0, IconWidth, IconHeight);

            // 4. Wo ist der Ankerpunkt (Pivot) des Icons?
            // Wir wollen die untere Mitte, damit es schön auf dem Brett "sitzt".
            // Mitte von 6 ist 3. Unten ist 16.
            Vector2 origin = new Vector2(IconWidth / 2f, IconHeight);


            // 5. Zeichnen!
            // WICHTIG: Wir nutzen scale 4f. Das ist der Standard in Stardew Valley.
            // Ein 6px breites Icon wird dadurch auf dem Bildschirm 24px breit (6 * 4).
            // Es sieht also schmaler aus als die normalen Ausrufezeichen (die 16 * 4 = 64px breit sind).
            // Das ist meistens gewünscht für schmalere Pixel-Art.
            // Die +32f bei der Position schiebt es in die Mitte des 64px breiten Tiles.
            b.Draw(this.customIconTexture, pos + new Vector2(32f, 0f), sourceRect, Color.White, 0f, origin, 4f, SpriteEffects.None, 1f);
        }

        // Zeichnet ein einzelnes Bild (ohne Ausschneiden) schwebend über dem Tile
        private void DrawSingleIcon(SpriteBatch b, Vector2 tileLocation, Texture2D texture)
        {
            if (texture == null) return;

            // 1. Animation berechnen (Schweben)
            float bob = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 200.0) * 5f;

            // 2. Position
            Vector2 pos = Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f, tileLocation.Y * 64f - 50f + bob));

            // 3. Wir nehmen das ganze Bild
            Rectangle sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);

            // 4. Ankerpunkt (Untere Mitte)
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height);

            // 5. Zeichnen (Skalierung 4x wie üblich in Stardew)
            b.Draw(texture, pos + new Vector2(32f, 0f), sourceRect, Color.White, 0f, origin, 4f, SpriteEffects.None, 1f);
        }


        private bool HasRaccoonQuest()
        {
            // 1. CONFIG CHECK (Wenn ausgeschaltet, sofort abbrechen)
            if (!this.config.EnableRaccoon) return false;

            // 2. DEBUG CHECK
            if (this.debugForceRaccoon) return true;

            // 3. ECHTE LOGIK
            // Wenn der Spieler dem Waschbären noch nie geholfen hat, gibt es noch keine Anzeige
            if (Game1.netWorldState.Value.TimesFedRaccoons == 0) return false;

            // Wenn er schon geholfen hat, prüfen wir, ob die 7 Tage Wartezeit um sind => statt +7 mach ich hier +8 weil es im spiel ein Tag zu früh war
            int lastBundleDay = Game1.netWorldState.Value.DaysPlayedWhenLastRaccoonBundleWasFinished;
            if (Game1.stats.DaysPlayed >= lastBundleDay + 8)
            {
                return true;
            }

            return false;
        }

        private bool IsFishingEvent()
        {
            // 1. DEBUG-CHECK (Zuerst!)
            if (this.debugForceFishingEvent) return true;

            // 2. CONFIG CHECK (falls deaktiviert, passiert nichts)
            if (!this.config.EnableFish) return false;

            // Forellen-Derby (Trout Derby): Sommer 20 & 21
            if (Game1.currentSeason == "summer" && (Game1.dayOfMonth == 20 || Game1.dayOfMonth == 21))
                return true;

            // Tintenfischfest (SquidFest): Winter 12 & 13
            if (Game1.currentSeason == "winter" && (Game1.dayOfMonth == 12 || Game1.dayOfMonth == 13))
                return true;

            return false;
        }

        private bool HasFishPondQuest()
        {
            // 1. CONFIG CHECK
            if (!this.config.EnableFish) return false;

            // 2. DEBUG CHECK
            // (Achte darauf, dass du hier deine richtige Debug-Variable nutzt)
            if (this.debugForcePond) return true;

            // 3. ECHTE LOGIK
            // NEU: Durchsuche ALLE Orte im Spiel
            foreach (var location in Game1.locations)
            {
                foreach (var building in location.buildings)
                {
                    // Prüfen, ob das Gebäude ein Fischteich ist
                    if (building is StardewValley.Buildings.FishPond pond)
                    {
                        // FAKT: Wir prüfen, ob ein Item verlangt wird UND die Quest noch NICHT erledigt ist!
                        if (pond.neededItem.Value != null && !pond.hasCompletedRequest.Value)
                        {
                            if (this.debugFishPondItem == false)
                            {
                                this.debugFishPondItem = true;
                            }
                            return true; // Ja! Die Statue soll leuchten!
                        }
                        else
                        {
                            this.debugFishPondItem = false;
                        }
                    }
                }
            }

            return false; // Kein Teich an irgendeinem Ort braucht etwas
        }

        private void OnOneSecondUpdateTicked(object sender, StardewModdingAPI.Events.OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // =========================================================
            // TEIL 1: FISCHTEICH-LOGIK
            // =========================================================
            this.wasFishPondQuestActive = this.isFishPondQuestActive;
            this.isFishPondQuestActive = HasFishPondQuest();

            if (this.wasFishPondQuestActive == true && this.isFishPondQuestActive == false)
            {
                this.Monitor.Log("Eine Fisch-Quest wurde beendet und alle Teiche sind wunschlos glücklich!", LogLevel.Info);
                if (this.playerStats != null)
                {
                    this.playerStats.FishPondQuestsCompleted++;
                }
            }

            // =========================================================
            // TEIL 2: QUEST-STATISTIKEN ZÄHLEN
            // =========================================================
            if (this.playerStats != null)
            {
                // --- WASCHBÄR ZÄHLER ---
                int currentRaccoonFeds = Game1.netWorldState.Value.TimesFedRaccoons;
                if (this.lastTimesFedRaccoons >= 0 && currentRaccoonFeds > this.lastTimesFedRaccoons)
                {
                    int difference = currentRaccoonFeds - this.lastTimesFedRaccoons;
                    this.playerStats.RaccoonQuestsCompleted += difference;
                    this.Monitor.Log($"Waschbär-Quest abgeschlossen! Neuer Zähler: {this.playerStats.RaccoonQuestsCompleted}", LogLevel.Info);
                }
                this.lastTimesFedRaccoons = currentRaccoonFeds; // Aktuellen Stand für die nächste Sekunde merken

                // --- SPEZIALAUFTRÄGE ZÄHLER (Dein bestehender Code) ---
                foreach (var order in Game1.player.team.specialOrders)
                {
                    if (order.questState.Value == SpecialOrderStatus.Complete && !this.countedOrdersToday.Contains(order.questKey.Value))
                    {
                        this.countedOrdersToday.Add(order.questKey.Value);

                        if (order.orderType.Value == "Qi")
                        {
                            this.playerStats.QiSpecialOrdersCompleted++;
                        }
                        else if (string.IsNullOrEmpty(order.orderType.Value))
                        {
                            this.playerStats.TownSpecialOrdersCompleted++;
                        }
                        else
                        {
                            this.playerStats.ModSpecialOrdersCompleted++;
                        }
                    }
                }
            }
        }

        private void OnRenderedActiveMenu(object sender, StardewModdingAPI.Events.RenderedActiveMenuEventArgs e)
        {
            // Greift ein, wenn entweder ein Spezial-Brett oder ein Tagesquest-Brett offen ist
            if (Game1.activeClickableMenu is StardewValley.Menus.SpecialOrdersBoard || Game1.activeClickableMenu is StardewValley.Menus.Billboard)
            {
                var activeMenu = Game1.activeClickableMenu;
                var activeList = this.isVanillaMode ? this.activeVanillaBoards : this.activeModBoards;

                // 1. Text und Pfeile zeichnen (nur wenn Quests vorhanden sind)
                if (activeList != null && activeList.Count > 0)
                {
                    // Pfeile zeichnen (falls mehrere Boards vorhanden)
                    if (activeList.Count > 1)
                    {
                        this.leftArrow?.draw(e.SpriteBatch);
                        this.rightArrow?.draw(e.SpriteBatch);
                    }

                    // --- TEXT ERMITTELN ---
                    string currentStringRaw = activeList[this.currentBoardIndex];
                    string displayString = "";

                    if (this.isVanillaMode)
                    {
                        if (currentStringRaw == "DailyQuest")
                            displayString = this.Helper.Translation.Get("board.daily");
                        else if (currentStringRaw == "SpecialOrder_Town")
                            displayString = this.Helper.Translation.Get("board.town");
                        else if (currentStringRaw == "SpecialOrder_Qi")
                            displayString = this.Helper.Translation.Get("board.qi");
                    }
                    else
                    {
                        // Übersetzung für die Mod-Bretter
                        displayString = GetModBoardDisplayName(currentStringRaw);
                    }

                    // --- TEXT ZEICHNEN ---
                    int textX = activeMenu.xPositionOnScreen + (activeMenu.width / 2);
                    int textY = activeMenu.yPositionOnScreen + activeMenu.height + 16;
                    StardewValley.BellsAndWhistles.SpriteText.drawStringWithScrollCenteredAt(e.SpriteBatch, displayString, textX, textY);
                }

                // =========================================================
                // TABS ZEICHNEN (Nur am Iridium-Board)
                // =========================================================
                // Wir prüfen direkt auf den Menü-Typ, egal ob es voll oder leer ist
                if (this.isVanillaMode && this.isIridiumBoardActive && (Game1.activeClickableMenu is StardewValley.Menus.SpecialOrdersBoard || Game1.activeClickableMenu is StardewValley.Menus.Billboard))
                {
                    // --- TABS ERSTELLEN ODER POSITION AKTUALISIEREN ---
                    if (this.questTab == null)
                    {
                        this.questTab = new StardewValley.Menus.ClickableTextureComponent(new Microsoft.Xna.Framework.Rectangle(activeMenu.xPositionOnScreen - 80, activeMenu.yPositionOnScreen + 64, 64, 64), Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(16, 368, 16, 16), 4f);
                    }
                    else
                    {
                        this.questTab.bounds.X = activeMenu.xPositionOnScreen - 80;
                        this.questTab.bounds.Y = activeMenu.yPositionOnScreen + 64;
                    }

                    if (this.statsTab == null)
                    {
                        this.statsTab = new StardewValley.Menus.ClickableTextureComponent(new Microsoft.Xna.Framework.Rectangle(activeMenu.xPositionOnScreen - 80, activeMenu.yPositionOnScreen + 128, 64, 64), Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(16, 368, 16, 16), 4f);
                    }
                    else
                    {
                        this.statsTab.bounds.X = activeMenu.xPositionOnScreen - 80;
                        this.statsTab.bounds.Y = activeMenu.yPositionOnScreen + 128;
                    }

                    // --- TABS ZEICHNEN ---
                    Microsoft.Xna.Framework.Color darkWoodBrown = new Microsoft.Xna.Framework.Color(101, 71, 49);
                    this.questTab.draw(e.SpriteBatch, darkWoodBrown, 1f);
                    this.statsTab.draw(e.SpriteBatch, darkWoodBrown, 1f);

                    e.SpriteBatch.Draw(Game1.mouseCursors, new Microsoft.Xna.Framework.Vector2(this.questTab.bounds.X + 12, this.questTab.bounds.Y + 16), new Microsoft.Xna.Framework.Rectangle(190, 422, 14, 11), Microsoft.Xna.Framework.Color.White, 0f, Microsoft.Xna.Framework.Vector2.Zero, 3f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 1f);
                    e.SpriteBatch.Draw(Game1.mouseCursors, new Microsoft.Xna.Framework.Vector2(this.statsTab.bounds.X + 16, this.statsTab.bounds.Y + 16), new Microsoft.Xna.Framework.Rectangle(346, 392, 8, 8), Microsoft.Xna.Framework.Color.White, 0f, Microsoft.Xna.Framework.Vector2.Zero, 4f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 1f);

                    if (this.questTab.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                        StardewValley.Menus.IClickableMenu.drawHoverText(e.SpriteBatch, "Quests", Game1.smallFont);
                    if (this.statsTab.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                        StardewValley.Menus.IClickableMenu.drawHoverText(e.SpriteBatch, this.Helper.Translation.Get("menu.stats.hover"), Game1.smallFont);
                }

                // Eigener Mauszeiger, damit er über dem Text sichtbar bleibt
                activeMenu.drawMouse(e.SpriteBatch);
            }
        }

        private void OpenVanillaBoardAndCreateArrows(string boardType)
        {
            Game1.playSound("bigSelect");

            // FAKT: Das richtige Menü basierend auf dem String öffnen
            if (boardType == "DailyQuest")
            {
                Game1.activeClickableMenu = new StardewValley.Menus.Billboard(true);
            }
            else if (boardType == "SpecialOrder_Town")
            {
                Game1.activeClickableMenu = new StardewValley.Menus.SpecialOrdersBoard("");
            }
            else if (boardType == "SpecialOrder_Qi")
            {
                Game1.activeClickableMenu = new StardewValley.Menus.SpecialOrdersBoard("Qi");
            }

            // Pfeile passend zur Größe des frisch geöffneten Menüs erstellen
            if (this.activeVanillaBoards.Count > 1)
            {
                int x = Game1.activeClickableMenu.xPositionOnScreen;
                int y = Game1.activeClickableMenu.yPositionOnScreen;
                int height = Game1.activeClickableMenu.height;
                int width = Game1.activeClickableMenu.width;

                this.leftArrow = new StardewValley.Menus.ClickableTextureComponent(
                    new Microsoft.Xna.Framework.Rectangle(x - 64, y + height - 128, 48, 44),
                    Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(352, 495, 12, 11), 4f);

                this.rightArrow = new StardewValley.Menus.ClickableTextureComponent(
                    new Microsoft.Xna.Framework.Rectangle(x + width + 16, y + height - 128, 48, 44),
                    Game1.mouseCursors, new Microsoft.Xna.Framework.Rectangle(365, 495, 12, 11), 4f);
            }
        }

        private void CleanUpGhostQuests()
        {
            if (!Context.IsWorldReady) return;

            // FAKT: Wir laden die offizielle Spieldatenbank aller AKTUELL installierten Spezialaufträge
            var validSpecialOrders = Game1.content.Load<Dictionary<string, StardewValley.GameData.SpecialOrders.SpecialOrderData>>("Data\\SpecialOrders");

            int removedCount = 0;

            // Wir durchlaufen die aktiven Quests auf dem Brett rückwärts
            for (int i = Game1.player.team.availableSpecialOrders.Count - 1; i >= 0; i--)
            {
                var order = Game1.player.team.availableSpecialOrders[i];
                string questKey = order.questKey.Value;

                // FAKT: Wenn die Quest einen Schlüssel hat, dieser Schlüssel aber NICHT mehr 
                // in der Spieldatenbank existiert, MUSS die Mod dazu gelöscht worden sein!
                if (!string.IsNullOrEmpty(questKey) && !validSpecialOrders.ContainsKey(questKey))
                {
                    this.Monitor.Log($"Geister-Quest ohne installierte Mod-Daten erkannt und gelöscht: {questKey}", LogLevel.Info);
                    Game1.player.team.availableSpecialOrders.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                this.Monitor.Log($"Automatischer Clean-Up: {removedCount} Geister-Quests wurden vom Brett entfernt!", LogLevel.Info);
            }
        }

        // FAKT: Diese Methode fängt Abstürze ab und lädt im Notfall das Standard-Bild
        private Texture2D LoadTextureSafely(string fileName, string prefix)
        {
            string pathWithPrefix = $"[CP] ZebrusQuestBoard/assets/{prefix}{fileName}";
            string pathFallback = $"[CP] ZebrusQuestBoard/assets/{fileName}";

            try
            {
                // 1. Versuch: Lade das Bild (mit oder ohne Präfix)
                return this.Helper.ModContent.Load<Texture2D>(pathWithPrefix);
            }
            catch (Exception)
            {
                // Wenn das fehlschlägt, prüfen wir, ob ein Präfix aktiv war
                if (!string.IsNullOrEmpty(prefix))
                {
                    this.Monitor.Log($"Alternative Grafik '{pathWithPrefix}' nicht gefunden! Lade stattdessen die Standard-Grafik.", LogLevel.Warn);
                    try
                    {
                        // 2. Versuch: Fallback auf das normale Bild ohne Präfix
                        return this.Helper.ModContent.Load<Texture2D>(pathFallback);
                    }
                    catch (Exception)
                    {
                        this.Monitor.Log($"Auch Standard-Grafik '{pathFallback}' fehlt! Das Icon wird vorerst unsichtbar sein.", LogLevel.Error);
                        return null;
                    }
                }
                else
                {
                    this.Monitor.Log($"Standard-Grafik '{pathWithPrefix}' fehlt! Das Icon wird vorerst unsichtbar sein.", LogLevel.Error);
                    return null;
                }
            }
        }

        private void CommandSetStat(string command, string[] args)
        {
            // Abbrechen, wenn das Spiel noch nicht geladen ist
            if (!Context.IsWorldReady || this.playerStats == null)
            {
                this.Monitor.Log("Du musst erst einen Spielstand laden, bevor du diesen Befehl nutzen kannst.", LogLevel.Warn);
                return;
            }

            // Prüfen, ob der Spieler auch Kategorie und Zahl eingegeben hat
            if (args.Length < 2)
            {
                this.Monitor.Log("Bitte benutze das Format: zq_setstat <kategorie> <zahl> (z.B. zq_setstat qi 10)", LogLevel.Error);
                return;
            }

            string category = args[0].ToLower();
            if (!int.TryParse(args[1], out int amount))
            {
                this.Monitor.Log("Die Zahl konnte nicht gelesen werden. Bitte gib eine gültige Ziffer ein.", LogLevel.Error);
                return;
            }

            // Den entsprechenden Zähler anpassen
            switch (category)
            {
                case "town":
                    this.playerStats.TownSpecialOrdersCompleted = amount;
                    this.Monitor.Log($"Stadt-Aufträge wurden auf {amount} gesetzt.", LogLevel.Info);
                    break;
                case "qi":
                    this.playerStats.QiSpecialOrdersCompleted = amount;
                    this.Monitor.Log($"Qi-Aufträge wurden auf {amount} gesetzt.", LogLevel.Info);
                    break;
                case "mod":
                    this.playerStats.ModSpecialOrdersCompleted = amount;
                    this.Monitor.Log($"Mod-Aufträge wurden auf {amount} gesetzt.", LogLevel.Info);
                    break;
                case "daily":
                    Game1.stats.QuestsCompleted = (uint)amount;
                    this.Monitor.Log($"Tagesquests wurden auf {amount} gesetzt.", LogLevel.Info);
                    break;
                default:
                    this.Monitor.Log("Unbekannte Kategorie. Erlaubt sind: town, qi, mod, daily", LogLevel.Error);
                    break;
            }
        }

        private void CommandCompleteQuests(string command, string[] args)
        {
            if (!Context.IsWorldReady) return;

            // Standardmäßig auf "all" setzen, falls der Spieler kein Argument eingibt
            string targetType = "all";
            if (args.Length > 0)
            {
                targetType = args[0].ToLower();
            }

            int count = 0;

            foreach (var order in Game1.player.team.specialOrders)
            {
                if (order.questState.Value != StardewValley.SpecialOrders.SpecialOrderStatus.Complete)
                {
                    bool shouldComplete = false;

                    // Prüfen, welche Quest-Art abgeschlossen werden soll
                    if (targetType == "town" && string.IsNullOrEmpty(order.orderType.Value))
                    {
                        shouldComplete = true;
                    }
                    else if (targetType == "qi" && order.orderType.Value == "Qi")
                    {
                        shouldComplete = true;
                    }
                    else if (targetType == "mod" && !string.IsNullOrEmpty(order.orderType.Value) && order.orderType.Value != "Qi")
                    {
                        shouldComplete = true;
                    }
                    else if (targetType == "all")
                    {
                        shouldComplete = true;
                    }

                    if (shouldComplete)
                    {
                        order.questState.Value = StardewValley.SpecialOrders.SpecialOrderStatus.Complete;
                        count++;

                        this.Monitor.Log($"Debug: Auftrag '{order.questKey.Value}' (Typ: '{order.orderType.Value}') wurde künstlich abgeschlossen.", LogLevel.Alert);
                    }
                }
            }

            if (count > 0)
            {
                this.Monitor.Log($"Erfolg! {count} Aufträge für den Filter '{targetType}' wurden auf 'Abgeschlossen' gesetzt.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Es gibt aktuell keine aktiven Spezialaufträge für den Filter '{targetType}'.", LogLevel.Warn);
                this.Monitor.Log("Nutze: zq_completequests [town | qi | mod | all]", LogLevel.Info);
            }
        }
    }

    public interface IContentPatcherAPI
    {
        void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);
    }
}