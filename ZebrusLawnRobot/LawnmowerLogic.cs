using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace ZebrusLawnRobot
{
    public class LawnmowerLogic
    {
        private readonly IMonitor Monitor;
        private readonly ModConfig Config;

        // Liste der aktuell herumfahrenden Roboter (max. 1 pro Station!)
        public List<LawnmowerBot> ActiveBots { get; } = new List<LawnmowerBot>();

        public LawnmowerLogic(IMonitor monitor, ModConfig config)
        {
            this.Monitor = monitor;
            this.Config = config;
        }

        public void RunHourlyMowing()
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return;

            foreach (var kvp in farm.objects.Pairs)
            {
                Vector2 tileLocation = kvp.Key;
                StardewValley.Object placedObject = kvp.Value;

                int radius = GetMowRadius(placedObject.Name);
                if (radius > 0)
                {
                    if (!IsBotAlreadyMowingForStation(tileLocation))
                    {
                        this.HarvestGrassInRadius(farm, tileLocation, radius);
                    }
                }
            }
        }

        private bool IsBotAlreadyMowingForStation(Vector2 stationTile)
        {
            Vector2 stationPixel = (stationTile * 64f) + new Vector2(16, 16);
            foreach (var bot in this.ActiveBots)
            {
                if (bot.GetStationPosition() == stationPixel && !bot.IsFinished)
                    return true;
            }
            return false;
        }

        private void HarvestGrassInRadius(Farm farm, Vector2 stationTile, int radius)
        {
            // 1. Zuerst finden wir ALLE existierenden Grasfelder im Raster vor/unter der Station
            List<Vector2> grassTilesOnFarm = new List<Vector2>();

            // X geht nach links und rechts (Mitte - Radius bis Mitte + Radius)
            for (int x = (int)stationTile.X - radius; x <= (int)stationTile.X + radius; x++)
            {
                // Y startet GENAU auf der Höhe der Station und geht nach unten (insgesamt radius * 2 Schritte)
                for (int y = (int)stationTile.Y; y <= (int)stationTile.Y + (radius * 2); y++)
                {
                    Vector2 targetTile = new Vector2(x, y);

                    // Gras check (Zur Sicherheit schließen wir die Kachel der Station selbst aus)
                    if (targetTile != stationTile && farm.terrainFeatures.TryGetValue(targetTile, out TerrainFeature feature) && feature is Grass)
                    {
                        grassTilesOnFarm.Add(targetTile);
                    }
                }
            }

            if (grassTilesOnFarm.Count > 0)
            {
                // 2. Wir erstellen das Schlangen-Muster (Boustephedon)
                // Gruppieren nach Zeilen (Y)
                var groupedByRow = grassTilesOnFarm
                    .GroupBy(t => t.Y)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Sortieren der Zeilen von oben nach unten (kleinste Y-Koordinate zuerst)
                var sortedRows = groupedByRow.Keys.ToList();
                sortedRows.Sort();

                List<ValueTuple<Vector2, bool>> sortedGrasPath = new List<ValueTuple<Vector2, bool>>();
                bool leftToRight = true;

                // Durch jede Zeile gehen
                foreach (float rowY in sortedRows)
                {
                    var tilesInRow = groupedByRow[rowY];

                    // X-Positionen sortieren (gerade Zeilen L-R, ungerade Zeilen R-L)
                    if (leftToRight)
                    {
                        tilesInRow.Sort((a, b) => a.X.CompareTo(b.X));
                    }
                    else
                    {
                        tilesInRow.Sort((a, b) => b.X.CompareTo(a.X));
                    }

                    // Zur finalen Pfad-Liste hinzufügen (true = hier wird gemäht)
                    foreach (Vector2 tile in tilesInRow)
                    {
                        sortedGrasPath.Add(new ValueTuple<Vector2, bool>(tile, true));
                    }

                    // Richtung für die nächste Zeile umkehren
                    leftToRight = !leftToRight;
                }

                // 3. Expliziter Rückweg (Vor die Garage fahren und von VORNE einparken)
                Vector2 stationPixel = (stationTile * 64f) + new Vector2(16, 16);
                Vector2 lastMownTile = sortedGrasPath.Last().Item1;

                List<ValueTuple<Vector2, bool>> finalTargets = new List<ValueTuple<Vector2, bool>>();

                // Das eigentliche Mäh-Muster zuerst hinzufügen
                foreach (var mownTarget in sortedGrasPath)
                {
                    finalTargets.Add(mownTarget);
                }

                // Wegpunkte für den Rückweg berechnen
                // Wir definieren die Kachel genau UNTER der Station (die Vorderseite / Einfahrt)
                float frontOfGarageY = stationTile.Y + 1f;

                // 1. Fahre vom letzten Grasfeld auf die Y-Höhe "vor der Garage" (false = kein Gras mähen!)
                finalTargets.Add(new ValueTuple<Vector2, bool>(new Vector2(lastMownTile.X, frontOfGarageY), false));

                // 2. Fahre auf dieser Höhe nach links/rechts, bis er exakt VOR der Garage steht
                finalTargets.Add(new ValueTuple<Vector2, bool>(new Vector2(stationTile.X, frontOfGarageY), false));

                // Wir wandeln die Liste in die fertige Schlange um
                Queue<ValueTuple<Vector2, bool>> grasTargetsQueue = new Queue<ValueTuple<Vector2, bool>>();
                Vector2 currentTile = stationTile; // Der Roboter startet an der Station

                foreach (var target in finalTargets)
                {
                    Vector2 destinationTile = target.Item1;
                    bool isMowable = target.Item2;

                    // Finde den sicheren, kachelweisen Weg vom aktuellen Punkt zum nächsten Ziel
                    List<Vector2> safePath = FindPath(farm, currentTile, destinationTile);

                    // Füge alle Zwischenschritte des Ausweichmanövers als "Nicht mähen" hinzu
                    for (int i = 0; i < safePath.Count - 1; i++)
                    {
                        grasTargetsQueue.Enqueue(new ValueTuple<Vector2, bool>(safePath[i], false));
                    }

                    // Füge den finalen Schritt als eigentliches Ziel hinzu
                    if (safePath.Count > 0)
                    {
                        grasTargetsQueue.Enqueue(new ValueTuple<Vector2, bool>(safePath.Last(), isMowable));
                        currentTile = safePath.Last(); // Das ist der Startpunkt für die nächste Wegsuche
                    }
                }

                // 4. Bot spawnen oder sofort mähen
                if (this.Config.UsePhysicalRobot)
                {
                    LawnmowerBot bot = new LawnmowerBot(stationPixel, grasTargetsQueue, (targetTile) =>
                    {
                        // Dieser Code wird nur für echte Gras-Ziele aufgerufen
                        this.ExecuteMowAction(farm, targetTile);
                    });

                    this.ActiveBots.Add(bot);
                }
                else
                {
                    // Wenn unsichtbar: Sofort alle echten Grasfelder mähen
                    foreach (var target in grasTargetsQueue)
                    {
                        if (target.Item2) // Nur echte Gras-Ziele
                        {
                            this.ExecuteMowAction(farm, target.Item1);
                        }
                    }
                }
            }
        }

        private void ExecuteMowAction(Farm farm, Vector2 targetTile)
        {
            if (farm.terrainFeatures.TryGetValue(targetTile, out TerrainFeature feature) && feature is Grass grass)
            {
                if (farm.tryToAddHay(1) == 0)
                {
                    // Wir nutzen die radikale Lösung (entfernen), es sei denn, der Spieler hat es anders konfiguriert
                    if (this.Config.RemoveGrassCompletely)
                    {
                        farm.terrainFeatures.Remove(targetTile);
                    }
                    else
                    {
                        if (grass.numberOfWeeds.Value > 1)
                            grass.numberOfWeeds.Value--;
                        else
                            farm.terrainFeatures.Remove(targetTile);
                    }

                    Game1.playSound("cut");

                    TemporaryAnimatedSprite grassEffect = new TemporaryAnimatedSprite(28, targetTile * 64f, Color.White);
                    grassEffect.animationLength = 4;
                    grassEffect.interval = 50f;
                    grassEffect.totalNumberOfLoops = 1;
                    farm.TemporarySprites.Add(grassEffect);
                }
            }
        }

        public void UpdateBots(GameTime time)
        {
            for (int i = this.ActiveBots.Count - 1; i >= 0; i--)
            {
                var bot = this.ActiveBots[i];
                bot.Update(time);

                if (bot.IsFinished)
                {
                    this.ActiveBots.RemoveAt(i);
                }
            }
        }

        private int GetMowRadius(string objectName)
        {
            // Der Radius bestimmt, wie viele Kacheln er in JEDE Richtung fährt.
            // Radius 3 bedeutet z.B. ein 7x7 großes Quadrat um die Station.

            if (objectName.Contains("ZebrusLawnRobot_Normal")) return 2;  // Ergibt 5x5 
            if (objectName.Contains("ZebrusLawnRobot_Gold")) return 4;    // Ergibt 9x9
            if (objectName.Contains("ZebrusLawnRobot_Iridium")) return 9; // Ergibt 19x19

            return 0;
        }

        private bool IsTilePassable(Farm farm, Vector2 tile)
        {
            // 1. Gebäude im Weg? (Verhindert das Durchfahren von Häusern/Silos)
            if (farm.getBuildingAt(tile) != null) return false;

            // 2. Objekte im Weg? (Verhindert das Durchfahren von Sprinklern, Zäunen, Vogelscheuchen)
            if (farm.objects.ContainsKey(tile)) return false;

            // 3. Bäume im Weg? (Gras darf durchfahren werden, Bäume nicht)
            if (farm.terrainFeatures.TryGetValue(tile, out TerrainFeature tf))
            {
                if (tf is StardewValley.TerrainFeatures.Tree || tf is StardewValley.TerrainFeatures.FruitTree)
                    return false;
            }

            return true;
        }

        private List<Vector2> FindPath(Farm farm, Vector2 start, Vector2 end)
        {
            if (start == end) return new List<Vector2> { end };

            Queue<List<Vector2>> queue = new Queue<List<Vector2>>();
            HashSet<Vector2> visited = new HashSet<Vector2>();

            queue.Enqueue(new List<Vector2> { start });
            visited.Add(start);

            // Die vier Bewegungsrichtungen (Oben, Unten, Links, Rechts)
            Vector2[] directions = new Vector2[]
            {
        new Vector2(0, -1),
        new Vector2(0, 1),
        new Vector2(-1, 0),
        new Vector2(1, 0)
            };

            while (queue.Count > 0)
            {
                List<Vector2> path = queue.Dequeue();
                Vector2 current = path.Last();

                if (current == end)
                {
                    path.RemoveAt(0); // Den Startpunkt entfernen, da wir da schon sind
                    return path;
                }

                // Sicherheitsabbruch, falls er komplett eingesperrt ist (max 30 Schritte Umweg)
                if (path.Count > 30) continue;

                foreach (var dir in directions)
                {
                    Vector2 next = current + dir;

                    // Wenn die Kachel frei ist oder es unser Ziel ist
                    if (!visited.Contains(next) && (IsTilePassable(farm, next) || next == end))
                    {
                        visited.Add(next);
                        List<Vector2> newPath = new List<Vector2>(path);
                        newPath.Add(next);
                        queue.Enqueue(newPath);
                    }
                }
            }

            // Wenn kein Weg gefunden wird (z.B. komplett von Zäunen umzingelt),
            // teleportiert/ignoriert er es notgedrungen und fährt Luftlinie
            return new List<Vector2> { end };
        }
    }
}