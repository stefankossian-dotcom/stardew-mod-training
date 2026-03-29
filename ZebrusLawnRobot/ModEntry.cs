using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;



namespace ZebrusLawnRobot
{
    /// <summary>Der Haupteinstiegspunkt für die Mod.</summary>
    internal sealed class ModEntry : Mod
    {
        private ModConfig Config;
        private LawnmowerLogic Logic;

        /// <summary>Wird von SMAPI aufgerufen, wenn die Mod geladen wird.</summary>
        public override void Entry(IModHelper helper)
        {
            // 1. Konfiguration laden
            this.Config = this.Helper.ReadConfig<ModConfig>();

            // 2. Logik-Instanz erstellen und den 'helper' von oben übergeben
            // WICHTIG: Hier muss das 'helper' stehen, das in den Klammern von Entry(IModHelper helper) definiert ist.
            this.Logic = new LawnmowerLogic(this.Monitor, this.Config);

            // 3. Events registrieren
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;

            // Registriert den Befehl: lawn_mow
            helper.ConsoleCommands.Add("lawn_mow", "Startet sofort den Mähvorgang aller Roboter-Stationen, ignoriert die Uhrzeit.", this.Command_RunMowing);
        }

        /// <summary>Wird nach dem Start des Spiels ausgelöst. Hier binden wir das Mod-Menü ein.</summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => this.Helper.Translation.Get("config.physical_robot.name"),
                tooltip: () => this.Helper.Translation.Get("config.physical_robot.tooltip"),
                getValue: () => this.Config.UsePhysicalRobot,
                setValue: value => this.Config.UsePhysicalRobot = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                 name: () => this.Helper.Translation.Get("config.remove_grass.name"),
                 tooltip: () => this.Helper.Translation.Get("config.remove_grass.tooltip"),
                 getValue: () => this.Config.RemoveGrassCompletely,
                 setValue: value => this.Config.RemoveGrassCompletely = value
            );

            // Content Patcher API abrufen
            var cpApi = this.Helper.ModRegistry.GetApi<IContentPatcherApi>("Pathoschild.ContentPatcher");

            if (cpApi != null)
            {
                // Namen (Zahlen entfernt oder ausgeschrieben)
                cpApi.RegisterToken(this.ModManifest, "LawnRobotNormalName", () => new[] { this.Helper.Translation.Get("item.normal.name").ToString() });
                cpApi.RegisterToken(this.ModManifest, "LawnRobotGoldName", () => new[] { this.Helper.Translation.Get("item.gold.name").ToString() });
                cpApi.RegisterToken(this.ModManifest, "LawnRobotIridiumName", () => new[] { this.Helper.Translation.Get("item.iridium.name").ToString() });

                // Beschreibungen
                cpApi.RegisterToken(this.ModManifest, "LawnRobotNormalDesc", () => new[] { this.Helper.Translation.Get("item.normal.desc").ToString() });
                cpApi.RegisterToken(this.ModManifest, "LawnRobotGoldDesc", () => new[] { this.Helper.Translation.Get("item.gold.desc").ToString() });
                cpApi.RegisterToken(this.ModManifest, "LawnRobotIridiumDesc", () => new[] { this.Helper.Translation.Get("item.iridium.desc").ToString() });

                // Briefe (Zahlen durch Wörter ersetzt)
                cpApi.RegisterToken(this.ModManifest, "MisuMailOneText", () => new[] { this.Helper.Translation.Get("mail.misu1.text").ToString() });
                cpApi.RegisterToken(this.ModManifest, "MisuMailTwoText", () => new[] { this.Helper.Translation.Get("mail.misu2.text").ToString() });
                cpApi.RegisterToken(this.ModManifest, "MisuMailThreeText", () => new[] { this.Helper.Translation.Get("mail.misu3.text").ToString() });
            }
        }

        /// <summary>Wird jeden Morgen im Spiel ausgelöst, wenn der Spieler aufwacht.</summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // 1. Defensive Prüfung
            if (!Context.IsWorldReady || Game1.player == null || Game1.getFarm() == null)
                return;

            // 2. Anzahl der fertiggestellten Silos zählen
            int completedSilos = Game1.getFarm().buildings.Count(building =>
                building != null &&
                building.buildingType.Value == "Silo" &&
                building.daysOfConstructionLeft.Value <= 0
            );

            // 3. Prüfe jeden Brief einzeln über unsere Hilfsmethode
            // Brief 1: Normaler Roboter (ab 1 Silo)
            this.TrySendMisuMail("Zebrus.ZebrusLawnRobot_Mail1", completedSilos >= 1);

            // Brief 2: Gold Roboter (ab 2 Silos)
            this.TrySendMisuMail("Zebrus.ZebrusLawnRobot_Mail2", completedSilos >= 2);

            // Brief 3: Iridium Roboter (ab 3 Silos)
            this.TrySendMisuMail("Zebrus.ZebrusLawnRobot_Mail3", completedSilos >= 3);
        }

        /// <summary>Hilfsmethode, um den Briefkasten nicht zu überfluten und Bedingungen sauber zu prüfen.</summary>
        private void TrySendMisuMail(string mailId, bool conditionMet)
        {
            // Wenn die Bedingung nicht erfüllt ist ODER der Spieler den Brief schon hat/bekommt -> nichts tun
            if (!conditionMet || Game1.player.hasOrWillReceiveMail(mailId))
                return;

            // Brief für den nächsten Tag einplanen
            Game1.addMailForTomorrow(mailId);
            this.Monitor.Log($"Bedingung für {mailId} erfüllt (Silo-Anzahl erreicht). Brief wird zugestellt.", LogLevel.Info);
        }

        /// <summary>Wird ausgelöst, wenn sich die Uhrzeit im Spiel ändert (alle 10 Ingame-Minuten).</summary>
        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            // Wir filtern auf den Stundenwechsel (z.B. 0800, 0900, 1000)
            // Das Modulo 100 prüft, ob die Minuten auf 00 stehen.
            if (e.NewTime % 100 != 0)
                return;

            // Logik ausführen
            this.Logic.RunHourlyMowing();
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Die Logik muss die Bots bewegen
            this.Logic.UpdateBots(Game1.currentGameTime);
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            // 1. Nur zeichnen, wenn die Welt geladen ist und Bots existieren
            if (!Context.IsWorldReady || this.Logic.ActiveBots.Count == 0)
                return;

            // 2. Deine Textur laden (die wir per CP in den Content-Manager geschleust haben)
            // "Mods/Zebrus.ZebrusLawnRobot/RobotSpritesheet" muss exakt wie in der content.json heißen!
            Texture2D robotTexture = this.Helper.GameContent.Load<Texture2D>("Mods/Zebrus.ZebrusLawnRobot/RobotSpritesheet");

            foreach (var bot in this.Logic.ActiveBots)
            {
                // 3. Welches 16x16 Bild aus deinem 64x16 Streifen?
                Rectangle sourceRect = new Rectangle(bot.SpriteIndex * 16, 0, 16, 16);

                // 4. In der Spielwelt zeichnen
                e.SpriteBatch.Draw(
                    robotTexture,
                    Game1.GlobalToLocal(Game1.viewport, bot.position),
                    sourceRect,
                    Color.White,
                    0f,
                    new Vector2(8, 8), // Mitte des Roboters als Drehpunkt
                    4f,                // Maßstab (Stardew Standard 4.0f)
                    SpriteEffects.None,
                    (bot.position.Y + 64f) / 10000f // Layer-Depth für korrekte Überlappung
                );
            }
        }

        private void Command_RunMowing(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Du musst einen Spielstand geladen haben, um diesen Befehl zu nutzen.", LogLevel.Error);
                return;
            }

            this.Monitor.Log("Erzwinge Mähvorgang...", LogLevel.Info);

            // Wir rufen direkt unsere Logik auf
            this.Logic.RunHourlyMowing();
        }
    }    
}