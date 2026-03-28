using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using System.Collections.Generic;

namespace BirthdayBoard
{
    public class BirthdayMenu : IClickableMenu
    {
        private ClickableTextureComponent tabToday;
        private ClickableTextureComponent tabStats;
        private ClickableTextureComponent tabCalendar;

        private int currentTab = 0;

        private List<string> todayTexts;
        private int currentTodayIndex = 0;
        private string statsText;
        private string tab1Title;
        private string tab2Title;
        private string tab3Title;

        private ClickableTextureComponent forwardButton;
        private ClickableTextureComponent backButton;

        private Billboard calendarMenu;

        public BirthdayMenu(List<string> todayTexts, string statsText, string tab1Title, string tab2Title, string tab3Title) : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            this.todayTexts = todayTexts;
            this.statsText = statsText;
            this.tab1Title = tab1Title;
            this.tab2Title = tab2Title;
            this.tab3Title = tab3Title;

            // FAKT: Kalender laden
            this.calendarMenu = new Billboard(false);

            // FAKT: Wir übernehmen EXAKT die Größe und Position des Vanilla-Kalenders!
            this.xPositionOnScreen = this.calendarMenu.xPositionOnScreen;
            this.yPositionOnScreen = this.calendarMenu.yPositionOnScreen;
            this.width = this.calendarMenu.width;
            this.height = this.calendarMenu.height;

            this.tabToday = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen - 64, this.yPositionOnScreen + 64, 64, 64),
                Game1.mouseCursors,
                new Rectangle(16, 368, 16, 16),
                4f)
            {
                // FAKT: Wir weisen dem ersten Tab die ID 100 zu und sagen, der Nachbar unten ist 101
                myID = 100,
                downNeighborID = 101,
                rightNeighborID = 103 // Erlaubt den Sprung ins Menü (zum Vorwärts-Pfeil)
            };

            this.tabStats = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen - 64, this.yPositionOnScreen + 128, 64, 64),
                Game1.mouseCursors,
                new Rectangle(16, 368, 16, 16),
                4f)
            {
                // FAKT: Der zweite Tab ist ID 101. Oben ist 100, unten ist 102.
                myID = 101,
                upNeighborID = 100,
                downNeighborID = 102
            };

            this.tabCalendar = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen - 64, this.yPositionOnScreen + 192, 64, 64),
                Game1.mouseCursors,
                new Rectangle(16, 368, 16, 16),
                4f)
            {
                // FAKT: Der dritte Tab ist ID 102. Oben ist 101.
                myID = 102,
                upNeighborID = 101
            };

            this.forwardButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 64, this.yPositionOnScreen + this.height - 64, 48, 44),
                Game1.mouseCursors,
                new Rectangle(365, 495, 12, 11),
                4f)
            {
                // FAKT: ID 103 für Vorwärts. Nachbar links ist Zurück (104).
                myID = 103,
                leftNeighborID = 104
            };

            this.backButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 16, this.yPositionOnScreen + this.height - 64, 48, 44),
                Game1.mouseCursors,
                new Rectangle(352, 495, 12, 11),
                4f)
            {
                // FAKT: ID 104 für Zurück. Nachbar rechts ist Vorwärts (103).
                myID = 104,
                rightNeighborID = 103,
                leftNeighborID = 100 // Erlaubt den Rücksprung zu den Tabs auf der linken Seite
            };
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.upperRightCloseButton != null && this.upperRightCloseButton.containsPoint(x, y))
            {
                this.exitThisMenu();
                Game1.playSound("bigDeSelect");
                return;
            }

            if (this.tabToday.containsPoint(x, y) && this.currentTab != 0)
            {
                this.currentTab = 0;
                Game1.playSound("smallSelect");
                return;
            }
            else if (this.tabStats.containsPoint(x, y) && this.currentTab != 1)
            {
                this.currentTab = 1;
                Game1.playSound("smallSelect");
                return;
            }
            else if (this.tabCalendar.containsPoint(x, y) && this.currentTab != 2)
            {
                this.currentTab = 2;
                Game1.playSound("smallSelect");
                return;
            }

            if (this.currentTab == 2)
            {
                if (this.calendarMenu.upperRightCloseButton != null && this.calendarMenu.upperRightCloseButton.containsPoint(x, y))
                {
                    this.exitThisMenu();
                    Game1.playSound("bigDeSelect");
                    return;
                }

                this.calendarMenu.receiveLeftClick(x, y, playSound);
                return;
            }

            base.receiveLeftClick(x, y, playSound);

            if (this.currentTab == 0 && this.todayTexts.Count > 1)
            {
                if (this.forwardButton.containsPoint(x, y))
                {
                    this.currentTodayIndex = (this.currentTodayIndex + 1) % this.todayTexts.Count;
                    Game1.playSound("shwip");
                }
                else if (this.backButton.containsPoint(x, y))
                {
                    this.currentTodayIndex = (this.currentTodayIndex - 1 + this.todayTexts.Count) % this.todayTexts.Count;
                    Game1.playSound("shwip");
                }
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (this.currentTab == 2)
            {
                this.calendarMenu.receiveRightClick(x, y, playSound);
                return;
            }
            base.receiveRightClick(x, y, playSound);
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);

            if (this.currentTab == 2)
            {
                this.calendarMenu.performHoverAction(x, y);
            }
        }

        public override void update(GameTime time)
        {
            base.update(time);

            if (this.currentTab == 2)
            {
                this.calendarMenu.update(time);
            }
        }

        // FAKT: Teilt der Engine mit, dass dieses Menü Gamepad-Snapping unterstützt
        public override void snapToDefaultClickableComponent()
        {
            this.currentlySnappedComponent = this.getComponentWithID(100); // 100 ist unser "Heute"-Tab
            this.snapCursorToCurrentSnappedComponent();
        }

        // FAKT: Sammelt alle klickbaren Elemente für den Controller in eine vom Spiel lesbare Liste
        public override void populateClickableComponentList()
        {
            base.populateClickableComponentList(); // Lädt Standard-Elemente (wie das rote X oben rechts)

            // Fügt unsere eigenen Tabs zur Controller-Steuerung hinzu
            this.allClickableComponents.Add(this.tabToday);
            this.allClickableComponents.Add(this.tabStats);
            if (this.tabCalendar != null) this.allClickableComponents.Add(this.tabCalendar);

            // FAKT: Die Blätter-Pfeile müssen ebenfalls rein!
            if (this.forwardButton != null) this.allClickableComponents.Add(this.forwardButton);
            if (this.backButton != null) this.allClickableComponents.Add(this.backButton);
        }

        public override void draw(SpriteBatch b)
        {
            if (this.currentTab == 2)
            {
                this.calendarMenu.draw(b);
                SpriteText.drawStringWithScrollCenteredAt(b, this.tab3Title, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 24);
            }
            else
            {
                b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

                // FAKT: Wir nutzen wieder die absolut sichere und lesbare Standard-Box des Spiels
                Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);

                string titleText = this.currentTab == 0 ? this.tab1Title : this.tab2Title;
                SpriteText.drawStringWithScrollCenteredAt(b, titleText, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen + 16);

                int maxWidth = this.width - 128;

                if (this.currentTab == 0)
                {
                    string wrappedText = Game1.parseText(this.todayTexts[this.currentTodayIndex], Game1.dialogueFont, maxWidth);
                    Utility.drawTextWithShadow(b, wrappedText, Game1.dialogueFont, new Vector2(this.xPositionOnScreen + 64, this.yPositionOnScreen + 120), Game1.textColor);

                    if (this.todayTexts.Count > 1)
                    {
                        this.forwardButton.draw(b);
                        this.backButton.draw(b);
                        string pageText = $"{this.currentTodayIndex + 1}/{this.todayTexts.Count}";
                        Utility.drawTextWithShadow(b, pageText, Game1.dialogueFont, new Vector2(this.xPositionOnScreen + this.width / 2 - Game1.dialogueFont.MeasureString(pageText).X / 2, this.yPositionOnScreen + this.height - 64), Game1.textColor);
                    }
                }
                else if (this.currentTab == 1)
                {
                    string wrappedStats = Game1.parseText(this.statsText, Game1.dialogueFont, maxWidth);
                    Utility.drawTextWithShadow(b, wrappedStats, Game1.dialogueFont, new Vector2(this.xPositionOnScreen + 64, this.yPositionOnScreen + 120), Game1.textColor);
                }
            }

            this.tabToday.draw(b);
            this.tabStats.draw(b);
            this.tabCalendar.draw(b);

            var tab1Icon = ItemRegistry.GetData("(O)221");
            var tab2Icon = ItemRegistry.GetData("(O)79");
            var tab3Icon = ItemRegistry.GetData("(O)102");

            if (tab1Icon != null) b.Draw(tab1Icon.GetTexture(), new Rectangle(this.tabToday.bounds.X + 16, this.tabToday.bounds.Y + 16, 32, 32), tab1Icon.GetSourceRect(), Color.White);
            if (tab2Icon != null) b.Draw(tab2Icon.GetTexture(), new Rectangle(this.tabStats.bounds.X + 16, this.tabStats.bounds.Y + 16, 32, 32), tab2Icon.GetSourceRect(), Color.White);
            if (tab3Icon != null) b.Draw(tab3Icon.GetTexture(), new Rectangle(this.tabCalendar.bounds.X + 16, this.tabCalendar.bounds.Y + 16, 32, 32), tab3Icon.GetSourceRect(), Color.White);

            this.drawMouse(b);

            if (this.currentTab != 2)
            {
                base.draw(b);
            }
        }
    }
}