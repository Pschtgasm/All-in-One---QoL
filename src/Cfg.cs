using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Alle Einstellungen. Datei: BepInEx\config\pscht.allinoneqol.cfg (auch im Config-Editor des Mod Managers).
    /// Abschnitte sind zweistellig nummeriert (01 … 20), damit sie in der richtigen Reihenfolge stehen.
    /// Jede Beschreibung endet mit "Vanilla: …" als Vergleich zum Standardwert des Mods.
    /// Innerhalb eines Abschnitts sortiert der Editor alphabetisch – deshalb beginnen zusammengehörige
    /// Einträge mit demselben Wort (z.B. "Schmelzofen: …").
    ///
    /// Einträge vom Typ Fixed&lt;T&gt; stehen NICHT mehr in der Datei (fester Wert),
    /// Stored&lt;T&gt; wird im Spiel eingestellt und in pscht.allinoneqol.local.txt gespeichert.
    /// </summary>
    internal static class Cfg
    {
        // ---------- 01 Zähmen ----------
        public static ConfigEntry<bool> TamingEnabled;
        public static ConfigEntry<float> TamingTime;
        public static readonly Fixed<int> LovePoints = new Fixed<int>(2);
        public static readonly Fixed<float> PregnancyDuration = new Fixed<float>(30f);
        public static ConfigEntry<float> GrowTime;
        public static ConfigEntry<int> MaxCreatures;
        public static ConfigEntry<int> OffspringStarChance;
        public static ConfigEntry<int> OffspringMaxLevel;
        public static ConfigEntry<bool> AllTamesCommandable;
        public static ConfigEntry<KeyboardShortcut> FollowAllKey;
        public static ConfigEntry<KeyboardShortcut> StayAllKey;
        public static ConfigEntry<KeyboardShortcut> TeleportTamesKey;
        public static readonly Fixed<float> CommandRange = new Fixed<float>(30f);
        public static readonly Fixed<bool> AutoTeleportLostTames = new Fixed<bool>(true);
        public static readonly Fixed<float> LostTameDistance = new Fixed<float>(40f);
        public static ConfigEntry<bool> TamesThroughPortals;
        public static ConfigEntry<bool> ProtectTames;
        public static ConfigEntry<float> ProtectHealPercent;
        public static ConfigEntry<bool> TamesEatFromChests;
        public static ConfigEntry<bool> WildEatFromChests;
        public static ConfigEntry<float> FeedChestRange;

        // ---------- 02 Pflanzen ----------
        public static ConfigEntry<bool> PlantsEnabled;
        public static ConfigEntry<bool> PlantAnyBiome;
        public static readonly Fixed<bool> CropsRequireCultivation = new Fixed<bool>(true);
        public static ConfigEntry<float> PlantGrowMinutes;
        public static ConfigEntry<bool> BushesEnabled;
        public static ConfigEntry<int> BushCost;
        public static ConfigEntry<float> BushRegrowMinutes;
        public static ConfigEntry<string> BushItems;
        internal const string BushDefaultOld = "Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomSmokePuff";
        internal const string BushDefault = BushDefaultOld + ",Thistle";
        public static ConfigEntry<bool> GridEnabled;
        public static ConfigEntry<bool> GridLines;
        public static ConfigEntry<KeyCode> GridWheelWidthKey;
        public static ConfigEntry<KeyCode> GridWheelLengthKey;
        public static ConfigEntry<bool> GridSeedInfo;
        public static readonly Stored<int> GridWidth = new Stored<int>("Raster.Breite", 3);
        public static readonly Stored<int> GridLength = new Stored<int>("Raster.Laenge", 3);
        public static readonly Fixed<float> GridSpacing = new Fixed<float>(0f);
        public static ConfigEntry<KeyboardShortcut> GridWiderKey;
        public static ConfigEntry<KeyboardShortcut> GridNarrowerKey;
        public static ConfigEntry<KeyboardShortcut> GridLongerKey;
        public static ConfigEntry<KeyboardShortcut> GridShorterKey;

        // ---------- 03 Material aus Kisten ----------
        public static ConfigEntry<bool> CfcEnabled;
        public static ConfigEntry<float> CfcRange;
        public static ConfigEntry<bool> CfcIgnoreVehicles;

        // ---------- 04 Spieler ----------
        public static ConfigEntry<bool> PlayerEnabled;
        public static ConfigEntry<float> CarryWeight;
        public static ConfigEntry<float> BuildDistance;
        public static ConfigEntry<float> InteractDistance;
        public static ConfigEntry<float> AutoPickupRange;

        // ---------- 05 Items ----------
        public static ConfigEntry<bool> ItemsEnabled;
        public static ConfigEntry<float> StackMultiplier;
        public static ConfigEntry<float> WeightMultiplier;
        public static ConfigEntry<bool> NoMovementPenalty;
        public static ConfigEntry<bool> FloatingItems;
        public static ConfigEntry<string> NoFloatItems;

        // ---------- 06 Verarbeitung ----------
        public static ConfigEntry<bool> ProcessingEnabled;
        public static readonly Fixed<bool> FillEnabled = new Fixed<bool>(true);
        public static ConfigEntry<bool> FillFromChests;
        public static ConfigEntry<float> FillChestRange;
        public static ConfigEntry<bool> FillOnlyWood;
        public static ConfigEntry<KeyCode> FillMaxKey;
        public static ConfigEntry<bool> FillOtherWood;
        public static ConfigEntry<bool> SpinningWheelNoRoof;
        public static ConfigEntry<float> EitrDamagePercent;
        public static ConfigEntry<float> SecSmelter;
        public static ConfigEntry<float> CapSmelter;
        public static ConfigEntry<float> SecBlastFurnace;
        public static ConfigEntry<float> CapBlastFurnace;
        public static ConfigEntry<float> SecKiln;
        public static ConfigEntry<float> CapKiln;
        public static ConfigEntry<float> SecSpinningWheel;
        public static ConfigEntry<float> CapSpinningWheel;
        public static ConfigEntry<float> SecWindmill;
        public static ConfigEntry<float> CapWindmill;
        public static ConfigEntry<bool> WindmillIgnoreWind;
        public static ConfigEntry<float> SecEitrRefinery;
        public static ConfigEntry<float> CapEitrRefinery;
        public static ConfigEntry<float> FermenterSeconds;
        public static ConfigEntry<float> BeehiveSeconds;
        public static ConfigEntry<float> SmelterSpeed;      // andere Stationen
        public static ConfigEntry<float> SmelterCapacity;   // andere Stationen

        // ---------- 07 Feuer ----------
        // Feuerstelle, Fackeln, Badewanne und Steinofen laufen über "Andere Feuer ohne Brennstoff".
        public static ConfigEntry<bool> FireCampfire;
        public static ConfigEntry<bool> FireBraziers;
        public static ConfigEntry<bool> FireOther;
        public static ConfigEntry<bool> NoSmoke;

        // ---------- 08 Bauen ----------
        public static ConfigEntry<bool> BuildingEnabled;
        public static ConfigEntry<bool> NoWeatherDamage;
        public static ConfigEntry<bool> NoWaterDamage;
        public static ConfigEntry<float> WorkbenchRange;
        public static ConfigEntry<bool> NoRoofRequired;
        public static ConfigEntry<bool> AutoRepairItems;
        public static ConfigEntry<bool> AreaRepair;
        public static ConfigEntry<float> AreaRepairRadius;
        public static ConfigEntry<bool> BuildInDungeons;
        public static ConfigEntry<bool> PortalTerrainProtection;
        public static readonly Fixed<float> PortalTerrainRadius = new Fixed<float>(4f);
        public static ConfigEntry<bool> DeconstructVehicles;
        public static readonly Fixed<float> WreckSeconds = new Fixed<float>(10f);

        // ---------- 09 Skills ----------
        public static ConfigEntry<bool> SkillsEnabled;
        public static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> SkillXp = new Dictionary<Skills.SkillType, ConfigEntry<float>>();

        /// <summary>XP-Faktor für einen Skill (1 = normal).</summary>
        internal static float SkillFactor(Skills.SkillType t) => SkillXp.TryGetValue(t, out var e) ? e.Value : 1f;

        // ---------- 10 Komfort ----------
        public static ConfigEntry<bool> HoverTimers;
        public static ConfigEntry<bool> NoMist;
        public static ConfigEntry<bool> CookGroups;
        public static ConfigEntry<bool> CookSortButtons;
        public static ConfigEntry<bool> CookAtCauldron;
        public static ConfigEntry<bool> CookAtPrepTable;
        public static ConfigEntry<bool> CookAtMeadCauldron;
        public static ConfigEntry<bool> CookAtOther;
        public static ConfigEntry<bool> CraftAmountField;
        public static ConfigEntry<bool> CraftShowCounts;
        public static ConfigEntry<bool> PauseWhenAllInMenu;
        public static ConfigEntry<bool> GearGroups;
        public static ConfigEntry<bool> GearSortButtons;
        public static ConfigEntry<bool> GearAtWorkbench;
        public static ConfigEntry<bool> GearAtForge;
        public static ConfigEntry<bool> GearAtBlackForge;
        public static ConfigEntry<bool> GearAtMageTable;
        public static ConfigEntry<bool> GearAtArtisan;
        public static ConfigEntry<bool> GearAtOther;
        public static ConfigEntry<bool> AutoCloseDoors;
        public static readonly Fixed<float> AutoCloseDelay = new Fixed<float>(2f);
        public static readonly Fixed<float> AutoCloseDistance = new Fixed<float>(5f);
        public static ConfigEntry<bool> BallistaIgnorePlayers;
        public static ConfigEntry<bool> BallistaIgnoreTames;
        public static ConfigEntry<bool> KeepEquipmentSwimming;

        // ---------- 11 Massenernte ----------
        public static ConfigEntry<bool> HarvestEnabled;
        public static ConfigEntry<float> HarvestRadius;
        public static ConfigEntry<int> HarvestMax;
        public static ConfigEntry<bool> HarvestSameType;
        public static readonly Fixed<bool> HarvestBeehives = new Fixed<bool>(true);

        // ---------- 12 Kisten ----------
        public static ConfigEntry<bool> BiggerChests;
        /// <summary>Prefab der Kiste → Eintrag "Größe: …" (BreitexHöhe).</summary>
        public static readonly Dictionary<string, ConfigEntry<string>> ChestSizeEntries = new Dictionary<string, ConfigEntry<string>>();
        public static readonly Fixed<bool> ChestTagEnabled = new Fixed<bool>(true);
        public static ConfigEntry<KeyboardShortcut> ChestTagKey;
        public static readonly Fixed<bool> ChestSignEnabled = new Fixed<bool>(true);
        public static ConfigEntry<KeyboardShortcut> ChestSideKey;
        public static ConfigEntry<KeyboardShortcut> ChestIconKey;
        public static ConfigEntry<string> ChestTextColor;
        public static readonly Fixed<string> ChestOutlineColor = new Fixed<string>("Dunkelbraun");
        public static ConfigEntry<bool> QuickStackEnabled;
        public static ConfigEntry<KeyboardShortcut> QuickStackKey;
        public static ConfigEntry<float> QuickStackRange;
        public static ConfigEntry<bool> IgnoreHotbar;
        public static ConfigEntry<KeyboardShortcut> SortKey;
        public static readonly Fixed<bool> DeleteEnabled = new Fixed<bool>(true);
        public static ConfigEntry<KeyboardShortcut> DeleteKey;
        public static readonly Fixed<bool> DeleteProtectEquipped = new Fixed<bool>(true);

        // ---------- 13 Werkbank-Erweiterungen ----------
        public static ConfigEntry<bool> ExtensionsEnabled;
        public static ConfigEntry<float> ExtensionDistance;
        public static ConfigEntry<float> ExtensionSpacing;

        // ---------- 14 Portale ----------
        public static ConfigEntry<bool> PortalsEnabled;
        public static readonly Fixed<int> PortalNameLength = new Fixed<int>(30);
        public static readonly Fixed<float> PortalMenuCloseDistance = new Fixed<float>(15f);
        public static readonly Fixed<bool> PortalValheimStyle = new Fixed<bool>(true);

        // ---------- 15 Smarte Kisten ----------
        public static ConfigEntry<bool> SmartEnabled;
        public static ConfigEntry<float> SmartRange;
        public static ConfigEntry<bool> SmartGroups;
        public static ConfigEntry<bool> SmartAutoGroups;
        public static ConfigEntry<string> SmartGroupList;
        public static ConfigEntry<KeyboardShortcut> SmartBypassKey;
        public static readonly Fixed<bool> SmartHighlight = new Fixed<bool>(true);
        public static readonly Fixed<string> SmartHighlightColor = new Fixed<string>("Rot");
        public static readonly Fixed<float> SmartHighlightSeconds = new Fixed<float>(2f);

        // ---------- 16 Timer ----------
        public static ConfigEntry<bool> TimerPlants;
        public static ConfigEntry<bool> TimerFermenter;
        public static ConfigEntry<bool> TimerBeehive;
        public static ConfigEntry<bool> TimerTames;

        // ---------- 17 Tag, Nacht und Uhr ----------
        public static readonly Fixed<bool> DayNightEnabled = new Fixed<bool>(true);
        public static ConfigEntry<float> DayMinutes;
        public static ConfigEntry<float> NightMinutes;
        public static ConfigEntry<bool> ClockEnabled;
        public static ConfigEntry<bool> ClockShowGameTime;
        public static ConfigEntry<bool> ClockShowDay;
        public static ConfigEntry<bool> ClockShowRealTime;
        public static ConfigEntry<bool> ClockRealTime24h;
        public static readonly Fixed<bool> ClockHideInInventory = new Fixed<bool>(true);
        public static readonly Fixed<int> ClockFontSize = new Fixed<int>(22);
        public static readonly Fixed<string> ClockPosition = new Fixed<string>("Oben Mitte");
        public static readonly Fixed<KeyboardShortcut> ClockToggleKey = new Fixed<KeyboardShortcut>(KeyboardShortcut.Empty);

        // ---------- 18 Extra-Slots ----------
        public static ConfigEntry<bool> ArmorSlotsEnabled;
        public static ConfigEntry<bool> UtilitySlotsEnabled;
        public static ConfigEntry<bool> BeltSlotEnabled;
        public static ConfigEntry<bool> FoodSlotsEnabled;
        public static ConfigEntry<bool> ShieldSlotEnabled;
        public static ConfigEntry<bool> KeySlotEnabled;
        public static ConfigEntry<KeyboardShortcut> FoodKey1;
        public static ConfigEntry<KeyboardShortcut> FoodKey2;
        public static ConfigEntry<KeyboardShortcut> FoodKey3;
        public static ConfigEntry<bool> ExtraSlotsLabels;
        public static readonly Fixed<int> ExtraSlotsOffsetX = new Fixed<int>(80);
        public static readonly Fixed<int> ExtraSlotsOffsetY = new Fixed<int>(0);
        public static ConfigEntry<bool> Hotbar2Enabled;
        public static ConfigEntry<float> Hotbar2Opacity;
        public static ConfigEntry<bool> SlotsAfterDeath;
        public static ConfigEntry<int> InvExtraRows;
        public static ConfigEntry<KeyCode> Hotbar2Modifier;

        // ---------- 19 Radar ----------
        public static ConfigEntry<bool> RadarEnabled;
        public static ConfigEntry<KeyboardShortcut> RadarKey;
        public static ConfigEntry<bool> RadarDungeons;
        public static ConfigEntry<bool> RadarShowLabels;
        // im Radar-Fenster (F7) einstellbar, gespeichert in pscht.allinoneqol.local.txt
        public static readonly Stored<string> RadarItems = new Stored<string>("Radar.Items", "");
        public static readonly Stored<bool> RadarOtherLocations = new Stored<bool>("Radar.AndereOrte", false);
        public static readonly Stored<bool> RadarOnlyDiscovered = new Stored<bool>("Radar.NurEntdeckte", true);
        public static readonly Stored<bool> RadarShowBuried = new Stored<bool>("Radar.Vergrabene", false);
        public static readonly Stored<bool> RadarOnlyExplored = new Stored<bool>("Radar.NurErkundet", false);
        public static readonly Stored<bool> RadarLargeIcons = new Stored<bool>("Radar.GrosseSymbole", false);
        public static readonly Fixed<float> RadarMaxDistance = new Fixed<float>(0f);      // 0 = so weit, wie die Welt geladen ist
        public static readonly Fixed<float> RadarRefreshSeconds = new Fixed<float>(2f);

        // ---------- 20 Server-Sync ----------
        public static ConfigEntry<bool> SyncSend;
        public static ConfigEntry<bool> SyncReceive;

        // ---------- 21 Inventar-Hilfen ----------
        public static ConfigEntry<bool> FavoritesEnabled;
        public static ConfigEntry<KeyCode> FavoriteKey;
        public static ConfigEntry<float> FavoriteRange;
        public static ConfigEntry<bool> KeepWeaponsAtStation;
        public static ConfigEntry<bool> TooltipCompare;
        public static ConfigEntry<bool> DurabilityColors;

        // ---------- 22 Erkunden ----------
        public static ConfigEntry<bool> ExploreEnabled;
        public static ConfigEntry<float> ExploreRadiusMin;
        public static ConfigEntry<float> ExploreRadiusMax;
        public static ConfigEntry<float> ExploreXp;
        public static ConfigEntry<bool> SailEnabled;
        public static ConfigEntry<float> SailBonus;
        public static ConfigEntry<float> SailXp;
        public static ConfigEntry<bool> TameSkillEnabled;
        public static ConfigEntry<float> TameProtect;
        public static ConfigEntry<float> TameDamage;
        public static ConfigEntry<float> TameXp;

        internal const string ExploreSection = "10 - Eigene Skills";
        internal const string ExtraSlotsSection = "19 - Extra-Slots";
        internal const string RadarSection = "20 - Radar";
        internal const string SyncSection = "21 - Server-Sync";
        internal const string HelpersSection = "22 - Inventar-Hilfen";
        internal const string TrophySection = "23 - Trophäen";
        internal const string DeathSection = "24 - Tod";
        internal const string SerpentSection = "25 - Seeschlangen";
        internal const string AchievementSection = "26 - Steam-Erfolge";

        // ---------- 23 Trophäen ----------
        public static ConfigEntry<bool> TrophyEnabled;
        public static ConfigEntry<string> TrophyFactor;
        // ---------- 24 Tod ----------
        public static ConfigEntry<bool> DeathEnabled;
        public static ConfigEntry<float> DeathSkillLoss;
        public static ConfigEntry<bool> DeathKeepEquipped;
        public static ConfigEntry<bool> DeathKeepHotbar;
        public static ConfigEntry<bool> DeathKeepFood;
        // ---------- 25 Seeschlangen ----------
        public static ConfigEntry<bool> SerpentEnabled;
        public static ConfigEntry<float> SerpentChance;
        public static ConfigEntry<int> SerpentMax;
        public static ConfigEntry<bool> SerpentDay;
        public static ConfigEntry<bool> SerpentAnyWeather;
        // ---------- 26 Steam-Erfolge ----------
        public static ConfigEntry<bool> AchievementsEnabled;
        public static ConfigEntry<bool> AchievementsSync;
        public static ConfigEntry<bool> AchievementsClearItems;
        // ---------- 27 Schnellzugriff ----------
        internal const string QuickSection = "27 - Schnellzugriff Server";
        public static ConfigEntry<bool> QuickEnabled;
        public static ConfigEntry<string> QuickAddress;
        public static ConfigEntry<string> QuickName;
        public static ConfigEntry<string> QuickPassword;

        // ---------- Hilfen ----------

        /// <summary>Beschreibung mit "Vanilla: …" in der letzten Zeile. Auf Englisch über CfgEnglish (Sprache beim Start).</summary>
        private static ConfigDescription D(string text, string vanilla, AcceptableValueBase range = null)
        {
            if (!L.De) { text = CfgEnglish.Tr(text); vanilla = CfgEnglish.Tr(vanilla); }
            string full = string.IsNullOrEmpty(vanilla) ? text : text + "\nVanilla: " + vanilla;
            return new ConfigDescription(full, range);
        }

        // ---------- 00 Sprache ----------
        public static ConfigEntry<string> Language;
        internal const string LanguageSection = "00 - Sprache / Language";

        private static AcceptableValueRange<float> F(float min, float max) => new AcceptableValueRange<float>(min, max);
        private static AcceptableValueRange<int> I(int min, int max) => new AcceptableValueRange<int>(min, max);

        /// <summary>Deutsche Namen der Skills (Reihenfolge im Editor: alphabetisch).</summary>
        private static readonly Dictionary<Skills.SkillType, string> SkillNames = new Dictionary<Skills.SkillType, string>
        {
            { Skills.SkillType.Swords, "Schwerter" }, { Skills.SkillType.Knives, "Messer" },
            { Skills.SkillType.Clubs, "Keulen" }, { Skills.SkillType.Polearms, "Stangenwaffen" },
            { Skills.SkillType.Spears, "Speere" }, { Skills.SkillType.Blocking, "Blocken" },
            { Skills.SkillType.Axes, "Äxte" }, { Skills.SkillType.Bows, "Bögen" },
            { Skills.SkillType.ElementalMagic, "Elementarmagie" }, { Skills.SkillType.BloodMagic, "Blutmagie" },
            { Skills.SkillType.Unarmed, "Faustkampf" }, { Skills.SkillType.Pickaxes, "Spitzhacken" },
            { Skills.SkillType.WoodCutting, "Holzfällen" }, { Skills.SkillType.Crossbows, "Armbrüste" },
            { Skills.SkillType.Jump, "Springen" }, { Skills.SkillType.Sneak, "Schleichen" },
            { Skills.SkillType.Run, "Laufen" }, { Skills.SkillType.Swim, "Schwimmen" },
            { Skills.SkillType.Fishing, "Angeln" }, { Skills.SkillType.Cooking, "Kochen" },
            { Skills.SkillType.Farming, "Landwirtschaft" }, { Skills.SkillType.Crafting, "Handwerk" },
            { Skills.SkillType.Dodge, "Ausweichen" }, { Skills.SkillType.Ride, "Reiten" },
        };

        /// <summary>Kisten mit eigener Größen-Einstellung: Prefab, Name, Standard, Vanilla.</summary>
        private static readonly string[][] Chests =
        {
            new[] { "piece_chest_wood", "Holzkiste", "5x2", "5x2" },
            new[] { "piece_chest", "Eisenkiste", "6x4", "6x4" },
            new[] { "piece_chest_private", "Private Kiste", "3x2", "3x2" },
            new[] { "piece_chest_blackmetal", "Schwarzmetallkiste", "8x6", "8x4" },
            new[] { "piece_chest_barrel", "Fass", "3x3", "3x2" },
        };

        public static void Bind(ConfigFile c)
        {
            // Sprache zuerst, damit die Beschreibungen gleich in der richtigen Sprache geschrieben werden
            Language = c.Bind(LanguageSection, "Sprache / Language", L.Auto,
                new ConfigDescription("Sprache der Mod-Texte im Spiel und der Beschreibungen hier. Auto = wie Valheim eingestellt ist (Deutsch, sonst Englisch). Beschreibungen ändern sich nach einem Neustart.\n" +
                                      "Language of the mod's texts in game and of these descriptions. Auto = same as Valheim's language (German, otherwise English). Descriptions change after a restart.\nVanilla: –",
                                      new AcceptableValueList<string>(L.Options)));

            // =====================================================================
            string s = "01 - Zähmen";
            TamingEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus.", "aus"));
            TamingTime = c.Bind(s, "Zähmdauer (Sekunden)", 5f,
                D("Wie lange das Zähmen eines gefütterten Tieres dauert.", "1800 s = 30 Min (Wolf, Eber)", F(1f, 10000f)));
            GrowTime = c.Bind(s, "Jungtier: Wachstumszeit (Sekunden)", 900f,
                D("Wie lange ein Jungtier zum Erwachsenwerden braucht. 900 = 15 Min.", "3000 s = 50 Min", F(10f, 20000f)));
            OffspringStarChance = c.Bind(s, "Jungtier: Chance auf +1 Stern (%)", 100,
                D("Wahrscheinlichkeit, dass ein Jungtier einen Stern mehr hat als die Mutter.", "0 % (gleicher Stern wie die Mutter)", I(0, 100)));
            OffspringMaxLevel = c.Bind(s, "Jungtier: Max. Level", 6,
                D("Höchstes Level für Nachwuchs. 1 = kein Stern, 2 = 1 Stern, 3 = 2 Sterne …", "wie die Mutter", I(1, 20)));
            MaxCreatures = c.Bind(s, "Max. Tiere pro Gehege", 18,
                D("Ab so vielen Tieren in der Nähe gibt es keinen Nachwuchs mehr. (Fest: 2× füttern für Nachwuchs, 30 s Trächtigkeit.)", "4", I(1, 100)));
            AllTamesCommandable = c.Bind(s, "Befehle: Alle Tiere befehlbar", true,
                D("Auch Eber, Loxe usw. können folgen und bleiben.", "nur Wolf und Asksvin"));
            FollowAllKey = c.Bind(s, "Befehle: Taste Alle folgen", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                D("Alle gezähmten Tiere im Umkreis von 30 m folgen dir.", "–"));
            StayAllKey = c.Bind(s, "Befehle: Taste Alle bleiben", new KeyboardShortcut(KeyCode.G, KeyCode.LeftAlt),
                D("Alle Tiere, die dir folgen, bleiben stehen.", "–"));
            TeleportTamesKey = c.Bind(s, "Befehle: Taste Tiere herholen", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                D("Alle Tiere, die dir folgen, werden zu dir teleportiert. Ab 40 m Abstand passiert das auch von selbst.", "–"));
            TamesThroughPortals = c.Bind(s, "Begleiter: Folgen durch Portale", true,
                D("Tiere, die dir folgen, reisen mit durchs Portal.", "aus"));
            ProtectTames = c.Bind(s, "Schutz: Tiere vor dem Tod schützen", true,
                D("Gezähmte Tiere sterben nicht durch Monster oder Umwelt, sondern werden gerettet. Durch Spieler (z.B. Schlachtmesser) sterben sie weiterhin.", "aus"));
            ProtectHealPercent = c.Bind(s, "Schutz: Gerettet mit Leben (%)", 25f,
                D("", "–", F(1f, 100f)));
            TamesEatFromChests = c.Bind(s, "Futter: Zahme Tiere fressen aus Kisten", true,
                D("Hungrige gezähmte Tiere nehmen ihr Futter aus Kisten in der Nähe.", "aus"));
            WildEatFromChests = c.Bind(s, "Futter: Wilde Tiere fressen aus Kisten", true,
                D("Auch beim Zähmen: wilde Tiere fressen aus Kisten in der Nähe.", "aus"));
            FeedChestRange = c.Bind(s, "Futter: Kisten-Reichweite (Meter)", 10f,
                D("", "–", F(1f, 50f)));

            // =====================================================================
            s = "02 - Pflanzen";
            PlantsEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus.", "aus"));
            PlantAnyBiome = c.Bind(s, "In jedem Biom pflanzen", true,
                D("Alle Pflanzen, Setzlinge und Bäume wachsen in jedem Biom.", "nur im passenden Biom"));
            PlantGrowMinutes = c.Bind(s, "Wachstumszeit (Minuten)", 67f,
                D("So viele Minuten braucht jede Pflanze und jeder Setzling bis zur Ernte (für alle gleich). Normal sind etwa 67 Minuten (im Spiel je nach Pflanze 50–83). Es zählt die Zeit, in der du spielst. Beispiel: 10 = 10 Minuten.", "je nach Pflanze ca. 50–83 Minuten", F(0f, 10000f)));
            BushesEnabled = c.Bind(s, "Büsche/Pilze: Mit Kultivator pflanzen", true,
                D("Himbeer-, Blaubeer-, Moltebeerbüsche, Pilze und Disteln erscheinen im Kultivator (ohne Raster, ganz nah aneinander). Sie wachsen nach dem Ernten wieder nach. Änderung erst nach Neustart.", "nicht pflanzbar"));
            BushCost = c.Bind(s, "Büsche/Pilze: Kosten (Stück)", 5,
                D("So viele Beeren/Pilze kostet ein Busch bzw. Pilz. Änderung erst nach Neustart.", "–", I(1, 100)));
            BushRegrowMinutes = c.Bind(s, "Büsche/Pilze: Nachwachsen (Minuten)", 300f,
                D("So viele Minuten dauert es, bis Büsche, Pilze und Disteln aus der Liste nach dem Ernten wieder erntbar sind. Normal sind 300 Minuten (5 Stunden, wie Himbeer- und Blaubeerbüsche im Spiel). Es zählt die Zeit, in der du spielst (Schlafen spult vor). Beispiel: 60 = eine Stunde. Wirkt nach Neu-Einloggen.", "Büsche 300, Pilze/Disteln wachsen gar nicht nach", F(0f, 10000f)));
            BushItems = c.Bind(s, "Büsche/Pilze: Liste", BushDefault,
                D("Interne Item-Namen, durch Komma getrennt. Zu jedem wird der passende Busch/Pilz gesucht. Was der Kultivator schon kann (Jotunnpuffs, Magierkappe), wird automatisch übersprungen. Welche gefunden wurden, steht im LogOutput.log.", "–"));
            // 1.2.0: Disteln dazu – alte Standardliste automatisch erweitern
            if (BushItems.Value == BushDefaultOld) BushItems.Value = BushDefault;
            GridEnabled = c.Bind(s, "Raster: Aktiv", true,
                D("Der Kultivator setzt Pflanzen und Setzlinge im Raster (Vorschau zeigt alle Plätze). Größe im Spiel mit Strg + Pfeiltasten ändern – sie wird gemerkt. Abstand automatisch so eng, wie die Pflanze wachsen kann.", "aus (eine Pflanze pro Klick)"));
            GridLines = c.Bind(s, "Raster: Linien anzeigen", true,
                D("Zeigt das Raster als Linien auf dem Boden. Grün = wird gepflanzt, rot = nicht genug Samen, grau = Platz belegt oder Boden nicht bearbeitet.", "keine Linien"));
            GridSeedInfo = c.Bind(s, "Raster: Samen-Anzeige", true,
                D("Zeigt unten im Bild, wie viele Samen du hast und wie viele das Raster braucht. Pflanzen, für die es nicht reicht, leuchten rot.", "keine Anzeige"));
            GridWheelWidthKey = c.Bind(s, "Raster: Mausrad Breite mit", KeyCode.LeftControl,
                D("Diese Taste halten und am Mausrad drehen = Raster breiter/schmaler (statt die Pflanze zu drehen). None = aus.", "–"));
            GridWheelLengthKey = c.Bind(s, "Raster: Mausrad Länge mit", KeyCode.LeftAlt,
                D("Diese Taste halten und am Mausrad drehen = Raster länger/kürzer (statt die Pflanze zu drehen). None = aus.", "–"));
            GridWiderKey = c.Bind(s, "Raster: Taste breiter", new KeyboardShortcut(KeyCode.RightArrow, KeyCode.LeftControl), D("", "–"));
            GridNarrowerKey = c.Bind(s, "Raster: Taste schmaler", new KeyboardShortcut(KeyCode.LeftArrow, KeyCode.LeftControl), D("", "–"));
            GridLongerKey = c.Bind(s, "Raster: Taste länger", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl), D("", "–"));
            GridShorterKey = c.Bind(s, "Raster: Taste kürzer", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl), D("", "–"));

            // =====================================================================
            s = "03 - Material aus Kisten";
            CfcEnabled = c.Bind(s, "Aktiv", true,
                D("Beim Craften, Bauen und Kaufen wird Material aus Kisten in der Nähe genommen.", "aus"));
            CfcRange = c.Bind(s, "Reichweite (Meter)", 50f, D("Aus Kisten in diesem Umkreis wird Material genommen.", "–", F(1f, 300f)));
            CfcIgnoreVehicles = c.Bind(s, "Schiffe und Karren ignorieren", false,
                D("true = Kisten auf Schiffen und Karren werden nicht benutzt.", "–"));

            // =====================================================================
            s = "04 - Spieler";
            PlayerEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus (wirkt nach Neu-Einloggen).", "aus"));
            CarryWeight = c.Bind(s, "Tragkraft", 1800f,
                D("Tragkraft ohne Gürtel. Der Megingjord-Bonus kommt obendrauf.", "300", F(50f, 100000f)));
            InvExtraRows = c.Bind(s, "Inventar: Zusätzliche Reihen", 0,
                D("So viele Reihen bekommt das Inventar zusätzlich (je 8 Plätze). Beim Händler gekaufte Reihen kommen obendrauf, höchstens 9 Reihen insgesamt. Wirkt sofort. Achtung: Stellst du den Wert wieder kleiner, fallen Items aus den weggefallenen Reihen auf den Boden.", "0 (4 Reihen)", I(0, 5)));
            BuildDistance = c.Bind(s, "Baudistanz (Meter)", 50f, D("Wie weit entfernt du bauen kannst.", "5 m", F(1f, 200f)));
            InteractDistance = c.Bind(s, "Interaktionsdistanz (Meter)", 10f, D("Wie weit entfernt du Dinge benutzen kannst (E).", "5 m", F(1f, 200f)));
            AutoPickupRange = c.Bind(s, "Auto-Aufsammeln (Meter)", 30f, D("Reichweite zum automatischen Aufsammeln.", "2 m", F(0f, 100f)));

            // =====================================================================
            s = "05 - Items";
            ItemsEnabled = c.Bind(s, "Aktiv", true, D("Stapel und Gewicht ein/aus.", "aus"));
            StackMultiplier = c.Bind(s, "Stapelgrößen-Faktor", 20f,
                D("Stapelbare Items: maximale Stapelgröße mal diesen Faktor (Holz 50 → 1000).", "1", F(1f, 1000f)));
            WeightMultiplier = c.Bind(s, "Gewichts-Faktor", 0.5f,
                D("Gewicht aller Items mal diesen Faktor. 0.5 = halbes Gewicht, 0 = schwerelos. Kommazahlen mit PUNKT schreiben (0.5, nicht 0,5).", "1", F(0f, 10f)));
            NoMovementPenalty = c.Bind(s, "Keine Bewegungsstrafe", true,
                D("Rüstung, Schilde und Waffen machen dich nicht langsamer (wirkt nach Neu-Einloggen).", "schwere Ausrüstung bremst"));
            FloatingItems = c.Bind(s, "Items schwimmen", true,
                D("Fallengelassene Items schwimmen auf dem Wasser (außer denen in der Liste).", "viele Items gehen unter"));
            NoFloatItems = c.Bind(s, "Items, die NICHT schwimmen",
                "Obsidian,BlackMetalScrap,IronScrap,CopperOre,TinOre,SilverOre,FlametalOre,BlackMetal,Iron,Copper,Tin,Silver,Flametal,Crystal,Chitin,SurtlingCore,Eitr,Softtissue,Tar,StoneBlock,SharpeningStone,DragonEgg",
                D("Prefab-Namen, mit Komma getrennt.", "–"));

            // =====================================================================
            s = "06 - Verarbeitung";
            ProcessingEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus.", "aus"));
            FillFromChests = c.Bind(s, "Befüllen: Auch aus Kisten", true,
                D("Material und Brennstoff werden auch aus Kisten in der Nähe genommen (erst Inventar, dann Kisten).", "nur Inventar"));
            FillChestRange = c.Bind(s, "Befüllen: Kisten-Reichweite (Meter)", 20f,
                D("", "–", F(1f, 100f)));
            FillOnlyWood = c.Bind(s, "Befüllen: Nur normales Holz", true,
                D("Stationen, die Holz nehmen (z.B. Meiler), bekommen nur normales Holz – kein Feinholz, Kernholz usw. Ist es leer, kommt die Meldung \"Normales Holz leer\".", "nimmt jedes passende Holz"));
            FillOtherWood = c.Bind(s, "Befüllen: Anderes Holz wenn normales leer", false,
                D("true = ist das normale Holz leer, wird mit Feinholz, Kernholz usw. weiter aufgefüllt. Nur wirksam, wenn \"Nur normales Holz\" an ist.", "nimmt jedes passende Holz"));
            FillMaxKey = c.Bind(s, "Befüllen: Taste halten für Max. Füllung", KeyCode.LeftShift,
                D("Diese Taste halten und E drücken = bis zur Max. Füllmenge auffüllen (Material oder Brennstoff, je nachdem wo du hinschaust). LeftShift = Shift links (über Strg links).", "eins pro Druck"));
            SpinningWheelNoRoof = c.Bind(s, "Spinnrad: Ohne Dach", true,
                D("Das Spinnrad arbeitet auch ohne Dach.", "braucht ein Dach"));
            EitrDamagePercent = c.Bind(s, "Eitr-Raffinerie: Umgebungsschaden (%)", 0f,
                D("Schaden, den die Eitr-Raffinerie in ihrer Umgebung macht. 0 = kein Schaden, 100 = normal.", "100", F(0f, 100f)));

            SecSmelter = c.Bind(s, "Schmelzofen: Sekunden pro Barren", 5f, D("Zeit für einen Barren.", "30 s", F(1f, 600f)));
            CapSmelter = c.Bind(s, "Schmelzofen: Kapazitäts-Faktor", 10f, D("So viel passt auf einmal rein: Vanilla-Menge mal diesen Faktor (Erz und Kohle).", "1 (10 Erz, 20 Kohle)", F(1f, 50f)));

            SecBlastFurnace = c.Bind(s, "Hochofen: Sekunden pro Barren", 5f, D("Zeit für einen Barren.", "30 s", F(1f, 600f)));
            CapBlastFurnace = c.Bind(s, "Hochofen: Kapazitäts-Faktor", 10f, D("So viel passt auf einmal rein: Vanilla-Menge mal diesen Faktor (Erz und Kohle).", "1 (10 Erz, 20 Kohle)", F(1f, 50f)));

            SecKiln = c.Bind(s, "Köhlerei: Sekunden pro Kohle", 3f, D("Zeit für eine Kohle.", "15 s", F(1f, 600f)));
            CapKiln = c.Bind(s, "Köhlerei: Kapazitäts-Faktor", 8f, D("So viel passt auf einmal rein: Vanilla-Menge mal diesen Faktor (Holz).", "1 (25 Holz)", F(1f, 50f)));

            SecSpinningWheel = c.Bind(s, "Spinnrad: Sekunden pro Garn", 5f, D("Zeit für ein Leinengarn.", "30 s", F(1f, 600f)));
            CapSpinningWheel = c.Bind(s, "Spinnrad: Kapazitäts-Faktor", 4f, D("So viel passt auf einmal rein: Vanilla-Menge mal diesen Faktor (Flachs).", "1 (40 Flachs)", F(1f, 50f)));

            SecWindmill = c.Bind(s, "Windmühle: Sekunden pro Mehl", 5f, D("Zeit für ein Gerstenmehl bei vollem Wind.", "10 s", F(1f, 600f)));
            CapWindmill = c.Bind(s, "Windmühle: Kapazitäts-Faktor", 4f, D("So viel passt auf einmal rein: Vanilla-Menge mal diesen Faktor (Gerste).", "1 (50 Gerste)", F(1f, 50f)));
            WindmillIgnoreWind = c.Bind(s, "Windmühle: Ohne Wind", true, D("Läuft immer mit voller Leistung.", "braucht Wind"));

            SecEitrRefinery = c.Bind(s, "Eitr-Raffinerie: Sekunden pro Eitr", 5f, D("Zeit für ein veredeltes Eitr.", "40 s", F(1f, 600f)));
            CapEitrRefinery = c.Bind(s, "Eitr-Raffinerie: Kapazitäts-Faktor", 4f, D("So viel passt auf einmal rein: Vanilla-Menge mal diesen Faktor (Weichgewebe und Saft).", "1 (20 Weichgewebe, 20 Saft)", F(1f, 50f)));

            FermenterSeconds = c.Bind(s, "Gärbottich: Sekunden pro Durchgang", 600f, D("Gärdauer, bis Met fertig ist.", "2400 s = 40 Min", F(1f, 10000f)));
            BeehiveSeconds = c.Bind(s, "Bienenstock: Sekunden pro Honig", 300f, D("Zeit für einen Honig.", "1200 s = 20 Min", F(1f, 10000f)));

            SmelterSpeed = c.Bind(s, "Andere Stationen: Tempo-Faktor", 6f,
                D("Für Stationen ohne eigenen Regler (z.B. aus anderen Mods). 6 = sechsmal so schnell.", "1", F(0.1f, 100f)));
            SmelterCapacity = c.Bind(s, "Andere Stationen: Kapazitäts-Faktor", 4f,
                D("Für Stationen ohne eigenen Kapazitäts-Faktor (z.B. aus anderen Mods): Vanilla-Menge mal diesen Faktor.", "1", F(1f, 50f)));

            // =====================================================================
            s = "07 - Feuer";
            FireCampfire = c.Bind(s, "Lagerfeuer ohne Brennstoff", true, D("Lagerfeuer, eisernes Lagerfeuer und Freudenfeuer brennen ewig.", "brauchen Holz"));
            FireBraziers = c.Bind(s, "Kohlebecken ohne Brennstoff", true, D("Stand- und Deckenkohlebecken brennen ewig.", "brauchen Kohle"));
            FireOther = c.Bind(s, "Andere Feuer ohne Brennstoff", true,
                D("Feuerstelle (Herd), alle Fackeln, Badezuber, Steinofen und alle übrigen Feuer (auch aus anderen Mods) brauchen keinen Brennstoff.", "brauchen Brennstoff"));
            NoSmoke = c.Bind(s, "Kein Rauch", true, D("Feuer erzeugen keinen Rauch.", "Rauch"));

            // =====================================================================
            s = "08 - Bauen";
            BuildingEnabled = c.Bind(s, "Aktiv", true, D("Verfall, Werkbank-Reichweite und Dach ein/aus.", "aus"));
            NoWeatherDamage = c.Bind(s, "Kein Wetterschaden", true, D("Holz verrottet nicht im Regen.", "Regen beschädigt Holz"));
            NoWaterDamage = c.Bind(s, "Kein Wasserschaden", true, D("Bauteile im Wasser nehmen keinen Schaden.", "Wasser beschädigt"));
            WorkbenchRange = c.Bind(s, "Werkbank-Reichweite (Meter)", 100f, D("Bauradius von Werkbank, Schmiede usw.", "20 m", F(5f, 300f)));
            NoRoofRequired = c.Bind(s, "Werkbank braucht kein Dach", true, D("Craften auch ohne Dach.", "Dach nötig"));
            AutoRepairItems = c.Bind(s, "Reparatur: Ausrüstung automatisch", true,
                D("Beim Benutzen einer Werkbank/Schmiede wird deine ganze Ausrüstung repariert.", "einzeln reparieren"));
            AreaRepair = c.Bind(s, "Reparatur: Gebäude automatisch", true,
                D("Beim Benutzen einer Werkbank/Schmiede werden beschädigte Bauteile im Umkreis repariert.", "einzeln mit dem Hammer"));
            AreaRepairRadius = c.Bind(s, "Reparatur: Gebäude-Radius (Meter)", 100f, D("", "–", F(5f, 300f)));
            BuildInDungeons = c.Bind(s, "Bauen in Dungeons", true, D("Auch in Dungeons, Krypten und an Orten mit Bauverbot bauen.", "verboten"));
            PortalTerrainProtection = c.Bind(s, "Portal: Boden schützen", true, D("Hacke, Spitzhacke usw. verändern den Boden um Portale nicht.", "aus"));
            DeconstructVehicles = c.Bind(s, "Karren und Schiffe mit Hammer abbauen", true,
                D("Vorher leer räumen! Das Wrack verschwindet nach 10 Sekunden.", "nicht möglich"));

            // =====================================================================
            s = "09 - Skills";
            SkillsEnabled = c.Bind(s, "Aktiv", true, D("Skill-Einstellungen ein/aus.", "aus"));
            foreach (Skills.SkillType t in Enum.GetValues(typeof(Skills.SkillType)))
            {
                if (t == Skills.SkillType.None || t == Skills.SkillType.All || SkillXp.ContainsKey(t)) continue;
                string name = SkillNames.TryGetValue(t, out var n) ? n : t.ToString();
                SkillXp[t] = c.Bind(s, "XP-Faktor: " + name, 2f,
                    D("So viel schneller steigt dieser Skill. 2 = doppelt, 1 = normal. Kommazahlen mit PUNKT.", "1", F(0.1f, 100f)));
            }

            // =====================================================================
            s = "11 - Komfort";
            HoverTimers = c.Bind(s, "Timer beim Drüberschauen", true, D("Hauptschalter für alle Timer. Einzelne Timer im Abschnitt 16 - Timer.", "keine Timer"));
            NoMist = c.Bind(s, "Kein Nebel in den Nebellanden", true, D("Schaltet den dichten Nebel in den Nebellanden ab.", "Nebel"));
            CookGroups = c.Bind(s, "Anzeige: Kochrezepte in Gruppen", true,
                D("An Stationen mit Essen (Kessel, Zubereitungstisch, Metkessel, auch aus anderen Mods) stehen die Rezepte in aufklappbaren Gruppen: Gesundheit, Ausdauer, Eitr, Tränke und Met, Zutaten und Sonstiges. Überschrift anklicken = auf/zu.", "eine lange Liste"));
            CookSortButtons = c.Bind(s, "Anzeige: Kochrezepte Sortier-Knöpfe", true,
                D("Drei Tränke oben rechts im Herstellmenü sortieren das Essen nach Gesundheit, Ausdauer oder Eitr (höchster Wert zuerst). Nochmal klicken = normale Reihenfolge.", "keine Sortierung"));
            CookAtCauldron = c.Bind(s, "Anzeige: Kochrezepte am Kessel", true,
                D("Gruppen und Sortier-Knöpfe am Kessel.", "aus"));
            CookAtPrepTable = c.Bind(s, "Anzeige: Kochrezepte am Zubereitungstisch", true,
                D("Gruppen und Sortier-Knöpfe am Zubereitungstisch (Nebellande).", "aus"));
            CookAtMeadCauldron = c.Bind(s, "Anzeige: Kochrezepte am Metkessel", true,
                D("Gruppen und Sortier-Knöpfe am Metkessel (Moorhexe).", "aus"));
            CookAtOther = c.Bind(s, "Anzeige: Kochrezepte an anderen Stationen", true,
                D("Gruppen und Sortier-Knöpfe an allen übrigen Stationen, die Essen oder Getränke herstellen (z.B. aus anderen Mods).", "aus"));
            CraftAmountField = c.Bind(s, "Anzeige: Herstellen Anzahl-Feld", true,
                D("An Herstellstationen über dem Herstellen-Knopf: [−] [Anzahl] [+] [Max]. So viele Stück werden auf einmal hergestellt (Material wird für alle zusammen geprüft). Shift + Klick auf −/+ = 10er-Schritte, Klick auf die Zahl = zurück auf 1. Nicht beim Aufwerten.", "1 Stück, mit Shift 5"));
            CraftShowCounts = c.Bind(s, "Anzeige: Herstellen wie oft möglich", true,
                D("Hinter jedem Rezept steht, wie oft du es mit deinem Material herstellen kannst (×5), auch mit Material aus Kisten in der Nähe.", "keine Anzeige"));
            PauseWhenAllInMenu = c.Bind(s, "Pause: Wenn alle im Menü sind", true,
                D("Mehrspieler: Haben alle Spieler das Esc-Menü offen, hält das Spiel an (auch Tageszeit, Gegner, Öfen). Sobald einer das Menü schließt oder jemand neu dazukommt, geht es weiter. Der Mod muss dafür auch auf dem Server sein.", "nur im Einzelspieler Pause"));
            GearGroups = c.Bind(s, "Anzeige: Ausrüstung in Gruppen", true,
                D("An Werkbank, Schmiede usw. stehen die Rezepte in aufklappbaren Gruppen: Schwerter, Äxte, Keulen, Messer, Speere, Stangenwaffen, Faustwaffen, Bögen und Armbrüste, Stäbe, Schilde, Turmschilde, Rüstung, Werkzeuge, Munition, Sonstiges. Überschrift anklicken = auf/zu.", "eine lange Liste"));
            GearSortButtons = c.Bind(s, "Anzeige: Ausrüstung Sortier-Knöpfe", true,
                D("Schwert, Brustpanzer und Schild oben rechts im Herstellmenü sortieren nach Schaden (alle Kampf-Schadensarten zusammen, ohne Holzfällen und Abbau), Rüstung oder Blocken – höchster Wert zuerst, jeweils für die Stufe, die du herstellst. Nochmal klicken = normale Reihenfolge.", "keine Sortierung"));
            GearAtWorkbench = c.Bind(s, "Anzeige: Ausrüstung an der Werkbank", true, D("Gruppen und Sortier-Knöpfe an der Werkbank.", "aus"));
            GearAtForge = c.Bind(s, "Anzeige: Ausrüstung an der Schmiede", true, D("Gruppen und Sortier-Knöpfe an der Schmiede.", "aus"));
            GearAtBlackForge = c.Bind(s, "Anzeige: Ausrüstung an der Schwarzmetall-Schmiede", true, D("Gruppen und Sortier-Knöpfe an der Schwarzmetall-Schmiede.", "aus"));
            GearAtMageTable = c.Bind(s, "Anzeige: Ausrüstung am Galdrtisch", true, D("Gruppen und Sortier-Knöpfe am Galdrtisch.", "aus"));
            GearAtArtisan = c.Bind(s, "Anzeige: Ausrüstung am Handwerkstisch", true, D("Gruppen und Sortier-Knöpfe am Handwerkstisch.", "aus"));
            GearAtOther = c.Bind(s, "Anzeige: Ausrüstung an anderen Stationen", true,
                D("Gruppen und Sortier-Knöpfe an allen übrigen Stationen, die Waffen, Rüstung oder Werkzeuge herstellen (z.B. aus anderen Mods).", "aus"));
            AutoCloseDoors = c.Bind(s, "Türen: Schließen automatisch", true,
                D("Türen gehen 2 Sekunden, nachdem niemand mehr in der Nähe (5 m) ist, von selbst zu.", "bleiben offen"));
            BallistaIgnorePlayers = c.Bind(s, "Ballisten ignorieren Spieler", true, D("", "können Spieler treffen"));
            BallistaIgnoreTames = c.Bind(s, "Ballisten ignorieren gezähmte Tiere", true, D("", "schießen auf zahme Tiere"));
            KeepEquipmentSwimming = c.Bind(s, "Ausrüstung beim Schwimmen behalten", true, D("Waffe und Schild werden beim Schwimmen nicht weggesteckt.", "werden weggesteckt"));

            // =====================================================================
            s = "12 - Massenernte";
            HarvestEnabled = c.Bind(s, "Aktiv", true, D("Beim Ernten werden gleiche Pflanzen in der Nähe mitgeerntet, Bienenstöcke ebenso.", "aus"));
            HarvestRadius = c.Bind(s, "Radius (Meter)", 6f, D("", "–", F(1f, 30f)));
            HarvestMax = c.Bind(s, "Max. Pflanzen auf einmal", 100, D("", "–", I(1, 500)));
            HarvestSameType = c.Bind(s, "Nur gleiche Sorte", true, D("true = nur dieselbe Pflanze (z.B. nur Karotten).", "–"));

            // =====================================================================
            s = "13 - Kisten";
            BiggerChests = c.Bind(s, "Größe: Aktiv", true, D("Kistengrößen unten verwenden.", "aus"));
            foreach (var ch in Chests)
                ChestSizeEntries[ch[0]] = c.Bind(s, "Größe: " + ch[1], ch[2],
                    D("Breite x Höhe, z.B. 5x2. NICHT kleiner machen als bisher, sonst können Items verloren gehen!", ch[3]));
            ChestTagKey = c.Bind(s, "Schild: Taste Beschriften", new KeyboardShortcut(KeyCode.K),
                D("Auf eine Kiste schauen und drücken, um einen Namen einzugeben. Der Name steht als Schild auf der Kiste.", "–"));
            ChestSideKey = c.Bind(s, "Schild: Taste Seite wechseln", new KeyboardShortcut(KeyCode.L),
                D("Auf die Kiste schauen und drücken: Das Schild wandert zur nächsten Seite (vorne, rechts, hinten, links).", "–"));
            ChestIconKey = c.Bind(s, "Schild: Taste Bild setzen", new KeyboardShortcut(KeyCode.G),
                D("In der offenen Kiste auf ein Item zeigen und drücken: Sein Bild erscheint auf dem Schild. Nochmal drücken entfernt es.", "–"));
            ChestTextColor = c.Bind(s, "Schild: Textfarbe", "Hellgelb",
                D("Farbe der Schrift auf der Kiste (Auswahlliste).", "–", new AcceptableValueList<string>(ChestSign.ColorNames)));
            QuickStackEnabled = c.Bind(s, "Stapeln: Quick Stack und Sortieren", true, D("Schnell verstauen und sortieren per Taste.", "aus"));
            QuickStackKey = c.Bind(s, "Stapeln: Taste Quick Stack", new KeyboardShortcut(KeyCode.P),
                D("Legt alle Items in Kisten in der Nähe, in denen schon dasselbe Item liegt.", "–"));
            QuickStackRange = c.Bind(s, "Stapeln: Reichweite (Meter)", 10f, D("", "–", F(1f, 100f)));
            IgnoreHotbar = c.Bind(s, "Stapeln: Hotbar nicht anfassen", true, D("Die oberste Inventarreihe (1–8) wird beim Verstauen und Sortieren ignoriert.", "–"));
            SortKey = c.Bind(s, "Stapeln: Taste Sortieren", new KeyboardShortcut(KeyCode.O),
                D("Sortiert dein Inventar und die geöffnete Kiste (Stapel werden zusammengelegt).", "–"));
            DeleteKey = c.Bind(s, "Löschen: Taste", new KeyboardShortcut(KeyCode.Delete),
                D("Im Inventar: Item mit der Maus festhalten und Taste drücken = gelöscht. Ausgerüstetes wird nie gelöscht.", "–"));

            // =====================================================================
            s = "14 - Werkbank-Erweiterungen";
            ExtensionsEnabled = c.Bind(s, "Aktiv", true, D("Kompaktere Werkbank-Erweiterungen (wirkt nach Neu-Einloggen).", "aus"));
            ExtensionDistance = c.Bind(s, "Max. Abstand zur Werkbank (Meter)", 5f,
                D("Wie weit eine Erweiterung (Hackklotz, Gerbgestell usw.) entfernt stehen darf.", "5 m", F(1f, 50f)));
            ExtensionSpacing = c.Bind(s, "Min. Abstand zwischen Erweiterungen (Meter)", 0f,
                D("0 = Erweiterungen dürfen direkt nebeneinander stehen.", "ca. 1 m", F(0f, 10f)));

            // =====================================================================
            s = "15 - Portale";
            PortalsEnabled = c.Bind(s, "Aktiv", true,
                D("E am Portal öffnet ein Fenster: Name (bis 30 Zeichen), Ziel (alle Portale der Welt) und Standard-Portal. Der Mod muss dafür auch auf dem Server installiert sein.", "nur Name"));

            // =====================================================================
            s = "16 - Smarte Kisten";
            SmartEnabled = c.Bind(s, "Aktiv", true,
                D("Legst du ein Item in eine Kiste, in der es noch nicht liegt, wandert es in eine Kiste in der Nähe, die es schon hat (oder die gleiche Gruppe, z.B. Holz zu Holz). Die Ziel-Kiste leuchtet kurz rot auf.", "aus"));
            SmartRange = c.Bind(s, "Reichweite (Meter)", 30f, D("", "–", F(1f, 100f)));
            SmartGroups = c.Bind(s, "Gruppen benutzen", true, D("Holz zu Holz, Metall zu Metall usw. (Liste unten).", "–"));
            SmartAutoGroups = c.Bind(s, "Gruppen automatisch", true,
                D("Automatisch gruppiert: fertige Gerichte, Tränke/Met, Trophäen, Munition, Fische, Rüstung und Waffen (auch Schilde, Bögen, Fackeln), Schmuck, Hilfsmittel und Werkzeuge (Gürtel, Wünschelrute, Irrlicht, Schmuckstücke, Hammer, Hacke, Kultivator, Spitzhacken, Angel). Dazu kommen alle pflanzbaren Samen zu \"Samen\" und ungekochte Zwischenprodukte (Teig, ungebackene Pasteten …) zu \"Zutaten\", auch wenn sie nicht in der Liste stehen.", "–"));
            SmartGroupList = c.Bind(s, "Gruppen-Liste",
                "Holz:Wood,FineWood,RoundLog,ElderBark,YggdrasilWood,Blackwood;" +
                "Metall:CopperOre,TinOre,IronScrap,IronOre,SilverOre,BlackMetalScrap,FlametalOre,FlametalOreNew,CopperScrap,BronzeScrap," +
                "Copper,Tin,Bronze,Iron,Silver,BlackMetal,Flametal,FlametalNew,BronzeNails,IronNails;" +
                "Steine:Stone,Flint,Obsidian,Grausten,BlackMarble,Coal;" +
                "Leder:LeatherScraps,DeerHide,TrollHide,WolfPelt,LoxPelt,ScaleHide,AskHide,BjornHide,CelestialFeather;" +
                "Fleisch:RawMeat,DeerMeat,NeckTail,WolfMeat,LoxMeat,SerpentMeat,HareMeat,ChickenMeat,BugMeat,AsksvinMeat,VoltureMeat,BoneMawSerpentMeat,BjornMeat;" +
                ItemGroups.DefaultSeeds + ItemGroups.DefaultIngredients +
                "Monsterteile:BoneFragments,Resin,Feathers,Guck,Bloodbag,Ooze,Entrails,Chitin,WolfFang,Crystal,FreezeGland,Needle,SurtlingCore,GreydwarfEye",
                D("Format: Gruppenname:Prefab1,Prefab2;Gruppenname2:… (Prefab-Namen wie im Spawn-Befehl)", "–"));
            SmartBypassKey = c.Bind(s, "Taste halten zum normalen Ablegen", new KeyboardShortcut(KeyCode.LeftAlt),
                D("Hältst du diese Taste beim Einlagern, bleibt das Item in der offenen Kiste.", "–"));

            // =====================================================================
            s = "17 - Timer";
            TimerPlants = c.Bind(s, "Pflanzen", true, D("Restzeit bei Pflanzen und Setzlingen.", "keine Anzeige"));
            TimerFermenter = c.Bind(s, "Gärbottich", true, D("Restzeit beim Gären.", "keine Anzeige"));
            TimerBeehive = c.Bind(s, "Bienenstock", true, D("Zeit bis zum nächsten Honig.", "keine Anzeige"));
            TimerTames = c.Bind(s, "Tiere", true, D("Zähmen, Liebe, Trächtigkeit, Erwachsenwerden.", "nur Zähm-Prozent"));

            // =====================================================================
            s = "18 - Tag, Nacht und Uhr";
            // "Anzeige" fängt mit A an, damit es im Editor ganz oben steht
            ClockEnabled = c.Bind(s, "Anzeige: Uhr", true,
                D("Zeigt oben in der Mitte eine Uhr (ausgeblendet, solange Inventar, Kiste oder Karte offen ist).", "keine Uhr"));
            DayMinutes = c.Bind(s, "Tag/Nacht: Tag (Minuten)", 42f,
                D("Echte Minuten vom Morgen bis zum Abend. Muss auf dem Server gleich sein (Server-Sync erledigt das).", "ca. 21 Min", F(1f, 600f)));
            NightMinutes = c.Bind(s, "Tag/Nacht: Nacht (Minuten)", 9f,
                D("Echte Minuten vom Abend bis zum Morgen.", "ca. 9 Min", F(1f, 600f)));
            ClockShowDay = c.Bind(s, "Uhr: Tag-Nummer", true, D("Zeigt den aktuellen Tag (z.B. Tag 42).", "–"));
            ClockShowGameTime = c.Bind(s, "Uhr: Spielzeit", true, D("Valheim-Uhrzeit (z.B. 14:30).", "–"));
            ClockShowRealTime = c.Bind(s, "Uhr: Echte Uhrzeit", true, D("Uhrzeit deines PCs.", "–"));
            ClockRealTime24h = c.Bind(s, "Uhr: 24-Stunden-Format", true, D("false = 12-Stunden-Format (AM/PM).", "–"));

            // =====================================================================
            s = ExtraSlotsSection;
            ArmorSlotsEnabled = c.Bind(s, "Rüstungsslots: Aktiv", true,
                D("4 Slots neben dem Inventar: Helm, Brust, Beine, Umhang. Angelegte Rüstung belegt dann keinen Inventarplatz mehr. Wirkt nach Neu-Einloggen.", "keine Rüstungsslots"));
            FoodSlotsEnabled = c.Bind(s, "Essensslots: Aktiv", true,
                D("3 Essensslots neben dem Inventar, Essen per Taste. Wirkt nach Neu-Einloggen.", "keine Essensslots"));
            ShieldSlotEnabled = c.Bind(s, "Schild-Slot: Aktiv", true,
                D("Ein Slot unter dem Essen für einen Schild. Nimmst du eine Einhandwaffe, wird dieser Schild automatisch angelegt (wenn die linke Hand frei ist). Bei Zweihandwaffen und Bögen legt Valheim ihn ab, er bleibt im Slot. Legst du einen anderen Schild an, kommt der in den Slot. Wirkt nach Neu-Einloggen.", "kein Schild-Slot, Schild von Hand anlegen"));
            UtilitySlotsEnabled = c.Bind(s, "Hilfsmittel- und Schmuckslot: Aktiv", true,
                D("2 Slots in der mittleren Spalte: Wünschelrute ODER Irrlicht (normales Hilfsmittel) und Schmuck (Trinket). Angelegtes belegt keinen Inventarplatz. Wirkt nach Neu-Einloggen.", "keine Slots"));
            BeltSlotEnabled = c.Bind(s, "Gürtel-Slot: Aktiv", true,
                D("Eigener Slot für den Gürtel (Megingiard). Er wirkt dort ZUSÄTZLICH zu Wünschelrute oder Irrlicht – im normalen Spiel geht nur eins davon. Wirkt nach Neu-Einloggen.", "Gürtel teilt sich den Platz mit Wünschelrute/Irrlicht"));
            KeySlotEnabled = c.Bind(s, "Schlüssel-Slot: Aktiv", true,
                D("Ein Slot unter dem Schmuck für Schlüssel (z.B. Krypta-Schlüssel aus dem Sumpf). Türen öffnen sich wie gewohnt, der Schlüssel belegt aber keinen Inventarplatz. Aufgehobene Schlüssel wandern von selbst hinein. Erkennt alle Schlüssel, die eine Tür braucht – auch aus anderen Mods. Wirkt nach Neu-Einloggen.", "kein Schlüssel-Slot"));
            FoodKey1 = c.Bind(s, "Essensslots: Taste 1", new KeyboardShortcut(KeyCode.Y, KeyCode.LeftControl), D("Isst das Essen aus dem ersten Essensslot.", "–"));
            FoodKey2 = c.Bind(s, "Essensslots: Taste 2", new KeyboardShortcut(KeyCode.X, KeyCode.LeftControl), D("Isst das Essen aus dem zweiten Essensslot.", "–"));
            FoodKey3 = c.Bind(s, "Essensslots: Taste 3", new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl), D("Isst das Essen aus dem dritten Essensslot.", "–"));
            ExtraSlotsLabels = c.Bind(s, "Beschriftung anzeigen", true, D("Tastenkürzel über den Essensslots, Namen in leeren Rüstungsslots.", "–"));
            Hotbar2Enabled = c.Bind(s, "Zweite Schnellleiste: Aktiv", true,
                D("Zweite Schnellleiste mit 8 eigenen Slots: im Inventarfenster unter den Extra-Slots, auf dem Bildschirm unter der normalen Leiste. Benutzen mit Alt+1 bis Alt+8. Wirkt nach Neu-Einloggen.", "nur eine Schnellleiste"));
            Hotbar2Opacity = c.Bind(s, "Zweite Schnellleiste: Deckkraft", 1f,
                D("Wie durchsichtig die zweite Leiste auf dem Bildschirm ist. 1 = so wie die normale Schnellleiste, kleiner = durchsichtiger.", "–", F(0.1f, 1f)));
            SlotsAfterDeath = c.Bind(s, "Nach dem Tod: zurück in die Slots", true,
                D("Holst du deinen Grabstein ab, kommen Essen, zweite Schnellleiste, Rüstung, Schild, Schmuck und Schlüssel wieder in ihre Slots statt ins normale Inventar. Rüstung wird dabei wieder angelegt.", "alles landet im normalen Inventar"));
            Hotbar2Modifier = c.Bind(s, "Zweite Schnellleiste: Taste", KeyCode.LeftAlt,
                D("Diese Taste halten und 1-8 drücken = Item aus der zweiten Schnellleiste benutzen.", "–"));

            // =====================================================================
            s = RadarSection;
            RadarEnabled = c.Bind(s, "Aktiv", true,
                D("Fundstellen in Sichtweite erscheinen als Pins auf Minikarte und großer Karte (werden nicht gespeichert). Alles Weitere (Items, andere Orte, vergrabene Erze, große Symbole …) im Radar-Fenster einstellen.", "aus"));
            RadarKey = c.Bind(s, "Taste Fenster", new KeyboardShortcut(KeyCode.F7),
                D("Öffnet das Radar-Fenster: dort auswählen, welche Items/Orte gepinnt werden.", "–"));
            RadarDungeons = c.Bind(s, "Dungeons", true, D("Dungeon-Eingänge pinnen (Grabkammern, Trollhöhlen, Krypten, Frosthöhlen …).", "–"));
            RadarShowLabels = c.Bind(s, "Namen anzeigen", true, D("Namen neben den Pins.", "–"));

            // =====================================================================
            s = SyncSection;
            SyncSend = c.Bind(s, "Server: Konfig an Spieler senden", true,
                D("Gilt auf dem Server bzw. beim Host: Alle Spieler bekommen beim Einloggen diese Werte. Tasten, Uhr, Extra-Slots, Radar und Inventar-Hilfen werden nicht übertragen.", "–"));
            SyncReceive = c.Bind(s, "Spieler: Werte vom Server übernehmen", true,
                D("Gilt beim Spieler: Werte vom Server ersetzen während der Sitzung die eigenen. Deine Datei wird nicht verändert.", "–"));

            // =====================================================================
            s = HelpersSection;
            FavoritesEnabled = c.Bind(s, "Favoriten: Aktiv", true,
                D("Im Inventar mit Mausrad-Klick auf ein Item: Es wird Favorit mit der Menge, die gerade im Stapel liegt (gelber Rahmen). Verbrauchst du davon etwas (Essen, Bauen, Herstellen, Pfeile …), wird aus Kisten in der Nähe wieder auf diese Menge aufgefüllt. Nochmal Mausrad-Klick = kein Favorit mehr. Aufgefüllt wird, sobald das Inventar zu ist.", "keine Favoriten"));
            FavoriteKey = c.Bind(s, "Favoriten: Taste", KeyCode.Mouse2,
                D("Taste bzw. Maustaste zum Markieren (Mouse2 = Mausrad-Klick). Maus über das Item im Inventar halten und drücken.", "–"));
            FavoriteRange = c.Bind(s, "Favoriten: Reichweite (Meter)", 50f,
                D("Aus Kisten in diesem Umkreis wird aufgefüllt (keine privaten Kisten anderer, keine Grabsteine, keine gerade geöffneten Kisten).", "–", F(5f, 300f)));
            TooltipCompare = c.Bind(s, "Vergleich: Aktiv", true,
                D("Beim Draufzeigen auf Waffe, Schild oder Rüstung (auch an der Werkbank) steht unten der Vergleich zu dem, was du gerade anhast: Schaden, Blocken oder Rüstung, grün = besser, rot = schlechter.", "kein Vergleich"));
            KeepWeaponsAtStation = c.Bind(s, "Stationen: Waffen in der Hand behalten", true,
                D("An Werkbank, Schmiede, Kessel usw. steckt Valheim Waffe und Schild weg. Mit true bleiben sie in der Hand.", "Waffen werden weggesteckt"));
            DurabilityColors = c.Bind(s, "Haltbarkeit: Farbige Leiste", true,
                D("Die Haltbarkeits-Leiste unter Waffen, Werkzeugen und Rüstung (Inventar und Schnellleiste) färbt sich von Grün (neu) über Gelb bis Rot (fast kaputt).", "Leiste immer gleiche Farbe"));

            // =====================================================================
            s = ExploreSection;
            // Eigene Skills (gibt es in Valheim nicht)
            ExploreEnabled = c.Bind(s, "Erkunden: Aktiv", true,
                D("Eigener Skill „Erkunden“: Je mehr Karte du neu aufdeckst, desto höher steigt er – und desto weiter um dich herum wird die Karte aufgedeckt.", "fester Radius 100 m, kein Skill"));
            ExploreRadiusMin = c.Bind(s, "Erkunden: Radius bei Skill 0 (Meter)", 100f,
                D("So weit wird die Karte am Anfang aufgedeckt.", "100", F(50f, 500f)));
            ExploreRadiusMax = c.Bind(s, "Erkunden: Radius bei Skill 100 (Meter)", 300f,
                D("So weit wird die Karte bei Skill 100 aufgedeckt. Dazwischen wächst der Radius gleichmäßig mit dem Skill.", "100", F(50f, 1000f)));
            ExploreXp = c.Bind(s, "Erkunden: XP-Faktor", 1f,
                D("Wie schnell der Skill steigt. 1 = etwa 20 Stunden neues Land erkunden bis Skill 100, 2 = doppelt so schnell.", "–", F(0.1f, 20f)));
            SailEnabled = c.Bind(s, "Segeln: Aktiv", true,
                D("Eigener Skill „Segeln“: steigt, solange du am Steuer stehst und das Schiff fährt. Mit dem Skill wird das Schiff schneller (Segel und Rudern).", "kein Skill"));
            SailBonus = c.Bind(s, "Segeln: Tempo-Bonus bei Skill 100 (%)", 50f,
                D("So viel schneller ist das Schiff bei Skill 100. Dazwischen gleichmäßig.", "0", F(0f, 200f)));
            SailXp = c.Bind(s, "Segeln: XP-Faktor", 1f,
                D("Wie schnell der Skill steigt. 1 = etwa 20 Stunden Fahrt bis Skill 100.", "–", F(0.1f, 20f)));
            TameSkillEnabled = c.Bind(s, "Zähmen: Aktiv", true,
                D("Eigener Skill „Zähmen“: steigt beim Zähmen, Füttern und Verlieben deiner Tiere (in deiner Nähe). Zahme Tiere in deiner Nähe (50 m) nehmen weniger Schaden und machen mehr Schaden.", "kein Skill"));
            TameProtect = c.Bind(s, "Zähmen: Weniger Schaden für Tiere bei Skill 100 (%)", 30f,
                D("So viel weniger Schaden nehmen zahme Tiere bei Skill 100.", "0", F(0f, 90f)));
            TameDamage = c.Bind(s, "Zähmen: Mehr Schaden durch Tiere bei Skill 100 (%)", 30f,
                D("So viel mehr Schaden machen zahme Tiere bei Skill 100.", "0", F(0f, 200f)));
            TameXp = c.Bind(s, "Zähmen: XP-Faktor", 1f,
                D("Wie schnell der Skill steigt.", "–", F(0.1f, 20f)));

            // ---------- 23 Trophäen ----------
            s = TrophySection;
            TrophyEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus.", "aus"));
            TrophyFactor = c.Bind(s, "Trophäen: Drop-Chance (%)", "Vanilla",
                D("Wahrscheinlichkeit in Prozent, dass ein Monster oder Tier seine Trophäe fallen lässt. Vanilla oder eine Zahl von 0 bis 100.", "Vanilla (je nach Monster, z.B. Greyling 10 %, Troll 50 %)"));

            // ---------- 24 Tod ----------
            s = DeathSection;
            DeathEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus.", "aus"));
            DeathSkillLoss = c.Bind(s, "Skill-Verlust: Faktor", 1f,
                D("Wie viel Skill du beim Tod verlierst, mal diesen Faktor. 0 = kein Verlust, 0.5 = halb so viel, 1 = normal. Kommazahlen mit PUNKT.", "1", F(0f, 5f)));
            DeathKeepEquipped = c.Bind(s, "Behalten: Angelegte Ausrüstung", true,
                D("Angelegte Waffen, Rüstung, Umhang, Gürtel usw. bleiben beim Tod an dir. Der Rest kommt wie gewohnt in den Grabstein.", "alles in den Grabstein"));
            DeathKeepHotbar = c.Bind(s, "Behalten: Schnellleiste", false,
                D("Die oberste Inventarreihe (Tasten 1–8) und die 2. Schnellleiste bleiben beim Tod in deinem Inventar.", "kommt in den Grabstein"));
            DeathKeepFood = c.Bind(s, "Behalten: Essen", false,
                D("Gegessenes Essen wirkt nach dem Tod weiter (Lebenspunkte und Ausdauer bleiben), und das Essen in den Essensslots bleibt bei dir.", "Essen weg"));

            // ---------- 25 Seeschlangen ----------
            s = SerpentSection;
            SerpentEnabled = c.Bind(s, "Aktiv", true, D("Alle Einstellungen dieses Abschnitts ein/aus.", "aus"));
            SerpentChance = c.Bind(s, "Seeschlangen: Chance (%)", 5f,
                D("So wahrscheinlich erscheint im Ozean eine Seeschlange, wenn das Spiel es versucht (nachts oder bei Sturm). 0 = nie, 5 = normal, 100 = jedes Mal. Gilt nach dem nächsten Einloggen.", "5", F(0f, 100f)));
            SerpentMax = c.Bind(s, "Seeschlangen: Max. Anzahl", 0,
                D("So viele Seeschlangen dürfen gleichzeitig in deiner Nähe sein. 0 = nicht ändern. Gilt nach dem nächsten Einloggen.", "1", I(0, 10)));
            SerpentDay = c.Bind(s, "Seeschlangen: Tagsüber", false,
                D("Seeschlangen erscheinen auch tagsüber, nicht nur nachts. Gilt nach dem nächsten Einloggen.", "nur nachts"));
            SerpentAnyWeather = c.Bind(s, "Seeschlangen: Bei jedem Wetter", false,
                D("Seeschlangen erscheinen bei jedem Wetter, nicht nur bei Sturm. Zusammen mit „Tagsüber“ also immer. Gilt nach dem nächsten Einloggen.", "nachts oder bei Sturm"));

            // ---------- 26 Steam-Erfolge ----------
            s = AchievementSection;
            AchievementsEnabled = c.Bind(s, "Aktiv", true,
                D("Steam-Erfolge gibt es auch mit Mods und nach Cheats (Konsole, devcommands, Weltmodifikatoren). Valheim sperrt sie sonst, sobald ein Mod geladen ist.", "mit Mods oder Cheats keine Erfolge"));
            AchievementsSync = c.Bind(s, "Erfolge: Fortschritt nachholen", true,
                D("Beim Einloggen werden alle Erfolge freigeschaltet, deren Bedingungen du schon erfüllt hast (z.B. Bosse besiegt, Items gefunden), auch wenn sie damals gesperrt waren.", "aus"));
            AchievementsClearItems = c.Bind(s, "Erfolge: Cheat-Markierung entfernen", true,
                D("Beim Einloggen wird bei allen Items im Inventar die Markierung „mit Cheats erhalten“ entfernt.", "Markierung bleibt"));

            // ---------- 27 Schnellzugriff Server ----------
            s = QuickSection;
            QuickEnabled = c.Bind(s, "Aktiv", true,
                D("Im Hauptmenü erscheint unter „Spiel starten“ ein Knopf „Server: …“. Klick → Charakter wählen → Start verbindet direkt mit dem Server. Ein Passwort fragt Valheim selbst ab. Gilt nach Neustart des Spiels.", "Server suchen oder IP eintippen"));
            QuickAddress = c.Bind(s, "Server: Adresse", "",
                D("Adresse mit Port, z.B. 123.45.67.89:2456 oder meinserver.de:2456. Leer = kein Knopf.", "–"));
            QuickName = c.Bind(s, "Server: Name", "",
                D("Name auf dem Knopf, z.B. Team Pscht. Leer = die Adresse wird angezeigt.", "–"));
            QuickPassword = c.Bind(s, "Server: Passwort", "",
                D("Freiwillig: Passwort des Servers. Dann wird es beim Verbinden über den Knopf automatisch eingegeben. Leer = Valheim fragt wie gewohnt. Achtung: steht als lesbarer Text in dieser Datei, also die Datei nicht weitergeben.", "Passwort jedes Mal eintippen"));

        }

        // ===================== Alte Einträge übernehmen =====================

        /// <summary>Alte Abschnitts-Namen (vor 1.22) → neue.</summary>
        private static readonly Dictionary<string, string> RenamedSections = new Dictionary<string, string>
        {
            // 1.29.3: eigene Skills als 10, alles ab Komfort eins nach unten
            { "10 - Komfort", "11 - Komfort" },
            { "11 - Massenernte", "12 - Massenernte" },
            { "12 - Kisten", "13 - Kisten" },
            { "13 - Werkbank-Erweiterungen", "14 - Werkbank-Erweiterungen" },
            { "14 - Portale", "15 - Portale" },
            { "15 - Smarte Kisten", "16 - Smarte Kisten" },
            { "16 - Timer", "17 - Timer" },
            { "17 - Tag, Nacht und Uhr", "18 - Tag, Nacht und Uhr" },
            { "18 - Extra-Slots", ExtraSlotsSection },
            { "19 - Radar", RadarSection },
            { "20 - Server-Sync", SyncSection },
            { "21 - Inventar-Hilfen", HelpersSection },
            { "22 - Skills", ExploreSection },
            { "22 - Eigene Skills", ExploreSection },
            { "16 - Server-Sync", SyncSection },
        };

        /// <summary>
        /// Übernimmt Werte aus Einträgen, die es so nicht mehr gibt (umbenannt, zusammengelegt oder ins Spiel
        /// verlegt), bevor Plugin die verwaisten Einträge löscht. Gibt die Zahl der übernommenen Werte zurück.
        /// </summary>
        internal static int Migrate(ConfigFile c, IDictionary orphans)
        {
            if (orphans == null || orphans.Count == 0) return 0;
            int n = 0;
            string Old(string section, string key)
            {
                var def = new ConfigDefinition(section, key);
                return orphans.Contains(def) ? orphans[def] as string : null;
            }
            void Take(ConfigEntryBase e, string value)
            {
                if (e == null || value == null) return;
                try { e.SetSerializedValue(value); n++; } catch { }
            }

            // 1) Abschnitt umbenannt, Schlüssel gleich
            foreach (var kv in RenamedSections)
                foreach (var def in c.Keys)
                    if (def.Section == kv.Value) Take(c[def], Old(kv.Key, def.Key));

            // 2) Einzelne Einträge
            Take(ClockEnabled, Old("18 - Tag, Nacht und Uhr", "Uhr: Anzeigen"));
            Take(ExploreEnabled, Old("22 - Erkunden", "Aktiv"));
            Take(ExploreRadiusMin, Old("22 - Erkunden", "Radius bei Skill 0 (Meter)"));
            Take(ExploreRadiusMax, Old("22 - Erkunden", "Radius bei Skill 100 (Meter)"));
            Take(ExploreXp, Old("22 - Erkunden", "XP-Faktor"));

            string ships = Old("03 - Material aus Kisten", "Schiffe ignorieren"), carts = Old("03 - Material aus Kisten", "Karren ignorieren");
            if (ships != null || carts != null)
                Take(CfcIgnoreVehicles, (ships == "true" || carts == "true") ? "true" : "false");

            string xp = Old("09 - Skills", "Skill-XP-Faktor");
            if (xp != null) foreach (var e in SkillXp.Values) Take(e, xp);

            // Max.-Mengen (feste Zahl) → Kapazitäts-Faktor (Menge geteilt durch Vanilla-Menge)
            void Cap(ConfigEntry<float> e, string key, float vanilla)
            {
                string v = Old("06 - Verarbeitung", key);
                if (v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) && amount > 0f)
                    Take(e, Mathf.Clamp((float)Math.Round(amount / vanilla, 1), 1f, 50f).ToString(CultureInfo.InvariantCulture));
            }
            Cap(CapSmelter, "Schmelzofen: Max. Erz", 10f);
            Cap(CapBlastFurnace, "Hochofen: Max. Erz", 10f);
            Cap(CapKiln, "Köhlerei: Max. Holz", 25f);
            Cap(CapSpinningWheel, "Spinnrad: Max. Flachs", 40f);
            Cap(CapWindmill, "Windmühle: Max. Gerste", 50f);
            Cap(CapEitrRefinery, "Eitr-Raffinerie: Max. Weichgewebe", 20f);

            // Sekunden -> Minuten (Wachstumszeit war früher in Sekunden; Testversion hatte kurz Sekunden)
            void FromSeconds(ConfigEntry<float> e, string section, string key)
            {
                string v = Old(section, key);
                if (v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float sec) && sec > 0f)
                    Take(e, Math.Round(sec / 60f, 1).ToString(CultureInfo.InvariantCulture));
            }
            FromSeconds(PlantGrowMinutes, "02 - Pflanzen", "Wachstumszeit (Sekunden)");
            FromSeconds(BushRegrowMinutes, "02 - Pflanzen", "Büsche/Pilze: Nachwachsen (Sekunden)");
            FromSeconds(DayMinutes, "18 - Tag, Nacht und Uhr", "Tag/Nacht: Tag (Sekunden)");
            FromSeconds(NightMinutes, "18 - Tag, Nacht und Uhr", "Tag/Nacht: Nacht (Sekunden)");
            // Trophäen: alte Mindest-Chance übernehmen; Seeschlangen: alter Faktor / alte Prozent vom Normalwert (5 %) -> echte Prozent
            string tmin = Old(TrophySection, "Trophäen: Mindest-Chance (%)");
            if (tmin != null && float.TryParse(tmin, NumberStyles.Float, CultureInfo.InvariantCulture, out float tm) && tm > 0f)
                Take(TrophyFactor, tm.ToString(CultureInfo.InvariantCulture));
            void Serpent(string key, float div)
            {
                string v = Old(SerpentSection, key);
                if (v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && f >= 0f)
                    Take(SerpentChance, Math.Round(Math.Min(100f, 5f * f / div), 1).ToString(CultureInfo.InvariantCulture));
            }
            Serpent("Seeschlangen: Spawn-Chance-Faktor", 1f);
            Serpent("Seeschlangen: Spawn-Chance (%)", 100f);

            string sizes = Old("12 - Kisten", "Größe: Kistengrößen");
            if (sizes != null)
                foreach (var part in sizes.Split(','))
                {
                    var p = part.Split('=');
                    if (p.Length == 2 && ChestSizeEntries.TryGetValue(p[0].Trim(), out var e)) Take(e, p[1].Trim());
                }

            // 3) Ins Spiel verlegt (Radar-Fenster F7, Kultivator-Raster) → lokale Datei
            GridWidth.MigrateFrom(Old("02 - Pflanzen", "Raster: Breite"));
            GridLength.MigrateFrom(Old("02 - Pflanzen", "Raster: Länge"));
            RadarItems.MigrateFrom(Old("20 - Radar", "Items"));
            RadarOtherLocations.MigrateFrom(Old("20 - Radar", "Andere Orte"));
            RadarOnlyDiscovered.MigrateFrom(Old("20 - Radar", "Nur entdeckte Items"));
            RadarShowBuried.MigrateFrom(Old("20 - Radar", "Vergrabene Erze"));
            RadarOnlyExplored.MigrateFrom(Old("20 - Radar", "Nur erkundete Karte"));
            RadarLargeIcons.MigrateFrom(Old("20 - Radar", "Große Symbole"));
            return n;
        }
    }
}
