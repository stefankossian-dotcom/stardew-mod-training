using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ZebrusQuestBoard
{
    public class StatsMenu : IClickableMenu
    {
        private QuestStats stats;
        private ITranslationHelper translation;
        private IClickableMenu previousMenu;

        private ClickableTextureComponent questTab;
        private ClickableTextureComponent statsTab;

        private Texture2D specialOrdersTexture;

        public StatsMenu(int x, int y, int w, int h, IClickableMenu previousMenu, QuestStats playerStats, ITranslationHelper translationHelper)
            : base(x, y, w, h, showUpperRightCloseButton: true)
        {
            this.stats = playerStats;
            this.translation = translationHelper;
            this.previousMenu = previousMenu;

            // Wir laden nur noch diese eine Textur
            this.specialOrdersTexture = Game1.content.Load<Texture2D>("LooseSprites\\SpecialOrdersBoard");

            int tabX = this.xPositionOnScreen - 56;
            // Wir korrigieren die X-Positionen, damit der statsTab nicht mehr so weit herausragt
            // FAKT: questTab X -> -80, statsTab X -> -72
            this.questTab = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen - 80, this.yPositionOnScreen + 64, 64, 64),
                Game1.mouseCursors, new Rectangle(16, 368, 16, 16), 4f);

            this.statsTab = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen - 80, this.yPositionOnScreen + 128, 64, 64),
                Game1.mouseCursors, new Rectangle(16, 368, 16, 16), 4f);
        }

        public override void draw(SpriteBatch b)
        {
            // 1. Hintergrund abdunkeln
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

            // =========================================================
            // DAS KOMPLETTE BRETT ZEICHNEN
            // =========================================================
            // Wir zeichnen einfach das gesamte Bild (Holz + Zettel) auf einen Schlag, genau wie das Original-Spiel.
            // Der Source-Rectangle (0, 0, 320, 240) greift exakt den Bereich der Textur ab, auf dem das fertige Brett liegt.
            b.Draw(this.specialOrdersTexture, new Vector2(this.xPositionOnScreen, this.yPositionOnScreen), new Rectangle(0, 0, 338, 198), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            // =========================================================
            // ÜBERSCHRIFT & TABS
            // =========================================================
            string title = this.translation.Get("menu.stats.title");
            // Die +16 bei der Y-Position zentriert den Text perfekt auf dem Holzschild des Bildes
            SpriteText.drawStringWithScrollCenteredAt(b, title, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 72, title);

            // --- TABS ZEICHNEN ---
            // Wir definieren ein dunkles Braun, das dem Holzrand des Brettes entspricht (101, 71, 49)
            Color darkWoodBrown = new Color(101, 71, 49);
            this.questTab.draw(b, darkWoodBrown, 1f);
            this.statsTab.draw(b, darkWoodBrown, 1f);

            b.Draw(Game1.mouseCursors, new Vector2(this.questTab.bounds.X + 12, this.questTab.bounds.Y + 16), new Rectangle(190, 422, 14, 11), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
            b.Draw(Game1.mouseCursors, new Vector2(this.statsTab.bounds.X + 16, this.statsTab.bounds.Y + 16), new Rectangle(346, 392, 8, 8), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            // =========================================================
            // STATISTIKEN ZEICHNEN
            // =========================================================

            int leftPaperX = this.xPositionOnScreen + 128;
            int rightPaperX = this.xPositionOnScreen + 740;

            // Etwas höher anfangen und Abstand verringern, damit 4 Items auf das linke Papier passen
            int startY = this.yPositionOnScreen + 220;
            int spacing = 95;

            // Linke Spalte (Daily, Town, Qi, Waschbär)
            DrawStatRow(b, this.translation.Get("menu.stats.daily"), Game1.stats.QuestsCompleted.ToString(), leftPaperX, startY);
            DrawStatRow(b, this.translation.Get("menu.stats.town"), this.stats.TownSpecialOrdersCompleted.ToString(), leftPaperX, startY + spacing);
            DrawStatRow(b, this.translation.Get("menu.stats.qi"), this.stats.QiSpecialOrdersCompleted.ToString(), leftPaperX, startY + spacing * 2);
            DrawStatRow(b, this.translation.Get("menu.stats.raccoon"), this.stats.RaccoonQuestsCompleted.ToString(), leftPaperX, startY + spacing * 3);

            // Rechte Spalte (Fischteich, Externe Mods)
            DrawStatRow(b, this.translation.Get("menu.stats.fish"), this.stats.FishPondQuestsCompleted.ToString(), rightPaperX, startY);
            DrawStatRow(b, this.translation.Get("menu.stats.mod"), this.stats.ModSpecialOrdersCompleted.ToString(), rightPaperX, startY + spacing);

            // Hover-Texte
            if (this.questTab.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                IClickableMenu.drawHoverText(b, "Quests", Game1.smallFont);
            if (this.statsTab.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                IClickableMenu.drawHoverText(b, this.translation.Get("menu.stats.hover"), Game1.smallFont);

            base.draw(b);
            this.drawMouse(b);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.questTab.containsPoint(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = this.previousMenu;
                return;
            }

            base.receiveLeftClick(x, y, playSound);
        }

        private void DrawStatRow(SpriteBatch b, string label, string value, int x, int y)
        {
            // Der Text links
            Utility.drawTextWithShadow(b, label, Game1.dialogueFont, new Vector2(x, y), Game1.textColor);

            // FAKT: Wir erhöhen den Abstand für die Zahl um 70 Pixel (380 -> 450)
            Utility.drawTextWithShadow(b, value, Game1.dialogueFont, new Vector2(x + 450, y), Color.Blue);
        }
    }
}