using Microsoft.Xna.Framework;
using StardewValley;
using System;
using System.Collections.Generic;

namespace ZebrusLawnRobot
{
    public class LawnmowerBot
    {
        // Positionen in Pixeln (für flüssige Bewegung)
        public Vector2 position;
        private Vector2 stationPixel;
        private Vector2 currentTargetPixel;

        // Einstellungen
        private float speed = 0.4f; // Kriechen (auf 0.8f oder 1.0f erhöhen, falls der Rückweg zu lange dauert)
        private bool isReturning = false;
        public bool IsFinished { get; private set; } = false;

        // 🟢 NEU: Eine Warteschlange, die (Tile-Position, IstEsGras) speichert
        private Queue<ValueTuple<Vector2, bool>> targets;
        private Action<Vector2> onReachedTarget;

        // SpriteIndex (0=Front, 1=Back, 2=Left, 3=Right)
        public int SpriteIndex { get; private set; } = 0;

        // 🟢 NEU: Speichert, ob das aktuelle Ziel gemäht werden soll
        private bool isCurrentTargetMowable = false;

        // 🟢 NEU: Der Konstruktor nimmt jetzt die neue Queue an
        public LawnmowerBot(Vector2 stationPixel, Queue<ValueTuple<Vector2, bool>> targets, Action<Vector2> onReachedTarget)
        {
            this.position = stationPixel;
            this.stationPixel = stationPixel;
            this.targets = targets;
            this.onReachedTarget = onReachedTarget;

            this.SetNextTarget();
        }

        public void Update(GameTime time)
        {
            if (this.IsFinished) return;

            float distance = Vector2.Distance(this.position, this.currentTargetPixel);

            if (distance > 2f)
            {
                // Bewegung zum aktuellen Ziel
                Vector2 direction = this.currentTargetPixel - this.position;
                direction.Normalize();
                this.position += direction * this.speed;
            }
            else
            {
                // Ziel erreicht!
                if (!this.isReturning)
                {
                    // 🟢 NEU: Wir rufen das Mähen nur auf, wenn es ein echtes Gras-Ziel war
                    if (this.isCurrentTargetMowable)
                    {
                        // Wir rechnen die Pixel-Position wieder in die Kachel-Koordinate um
                        Vector2 targetTile = (this.currentTargetPixel - new Vector2(16, 16)) / 64f;
                        this.onReachedTarget?.Invoke(targetTile);
                    }

                    // Nächstes Ziel auswählen
                    this.SetNextTarget();
                }
                else
                {
                    this.IsFinished = true;
                }
            }
        }

        private void SetNextTarget()
        {
            // 🟢 NEU: Wir prüfen, ob noch Ziele in der Schlange sind
            if (!this.isReturning && this.targets.Count > 0)
            {
                // Hole das nächste Ziel aus der Schlange
                ValueTuple<Vector2, bool> nextTarget = this.targets.Dequeue();
                Vector2 nextTile = nextTarget.Item1;
                this.isCurrentTargetMowable = nextTarget.Item2; // Speichern, ob es gemäht werden soll

                this.currentTargetPixel = (nextTile * 64f) + new Vector2(16, 16);

                // Richtung nur jetzt berechnen
                Vector2 direction = this.currentTargetPixel - this.position;
                direction.Normalize();
                this.SpriteIndex = GetSpriteIndexFromDirection(direction);
            }
            else
            {
                // Zurück zur Station (impliziter Rückweg)
                this.currentTargetPixel = this.stationPixel;
                this.isReturning = true;
                this.isCurrentTargetMowable = false; // Zur Sicherheit

                // Richtung nur jetzt berechnen
                Vector2 direction = this.currentTargetPixel - this.position;
                direction.Normalize();
                this.SpriteIndex = GetSpriteIndexFromDirection(direction);
            }
        }

        private int GetSpriteIndexFromDirection(Vector2 direction)
        {
            if (Math.Abs(direction.X) > Math.Abs(direction.Y))
                return direction.X > 0 ? 3 : 2; // Rechts (3), Links (2)
            else
                return direction.Y > 0 ? 0 : 1; // Front (0), Back (1)
        }

        public Vector2 GetStationPosition()
        {
            return this.stationPixel;
        }
    }
}