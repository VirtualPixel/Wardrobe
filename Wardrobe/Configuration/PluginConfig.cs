using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Wardrobe.Services;

namespace Wardrobe.Configuration
{
    public enum VerbosityLevel
    {
        Off = 0,
        Debug = 1,
        Verbose = 2
    }

    // Four sections: General for what most people touch, Wheel and Keys for the buttons, and
    // Advanced for the rest. Settings from the old layout are moved over on first load (ConfigMoves).
    internal static class PluginConfig
    {
        public static ConfigEntry<bool> ImportLibrary = null!;
        public static ConfigEntry<bool> ShowPresetNames = null!;
        public static ConfigEntry<bool> ShowColorNames = null!;
        public static ConfigEntry<bool> CombineCoins = null!;

        public static ConfigEntry<KeyCode> WheelModifierKey = null!;
        public static ConfigEntry<KeyCode> WheelFolderPrevKey = null!;
        public static ConfigEntry<KeyCode> WheelFolderNextKey = null!;
        public static ConfigEntry<KeyCode> WheelLookPrevKey = null!;
        public static ConfigEntry<KeyCode> WheelLookNextKey = null!;
        public static ConfigEntry<KeyCode> WheelCopyKey = null!;
        public static ConfigEntry<KeyCode> WheelConfirmKey = null!;
        public static ConfigEntry<float> WheelSoundVolume = null!;

        public static ConfigEntry<KeyCode> RenameKey = null!;
        public static ConfigEntry<KeyCode> ExportKey = null!;
        public static ConfigEntry<KeyCode> ImportKey = null!;
        public static ConfigEntry<KeyCode> SaveCodeKey = null!;

        public static ConfigEntry<int> PresetSlots = null!;
        public static ConfigEntry<int> TrashDays = null!;
        public static ConfigEntry<bool> ShowHint = null!;
        public static ConfigEntry<bool> MarkNewAsSeen = null!;
        public static ConfigEntry<int> TokenStackCap = null!;
        public static ConfigEntry<int> CombineRate = null!;
        public static ConfigEntry<int> BackupsToKeep = null!;
        public static ConfigEntry<float> WheelSettleSeconds = null!;
        public static ConfigEntry<VerbosityLevel> LoggingLevel = null!;

        public static void Init(ConfigFile config)
        {
            Migrate(config);

            const string general = ConfigMoves.General;
            ImportLibrary = config.Bind(general, "ImportPresets", true,
                "Put the looks from Wardrobe/presets.cfg (and any preset pack another mod brings) into your preset slots on launch.");
            ShowPresetNames = config.Bind(general, "ShowPresetNames", true,
                "Write each look's name on its slot, with how many of its pieces are still locked.");
            ShowColorNames = config.Bind(general, "ShowColorNames", true,
                "Show a colour's name and hex code when you hover a swatch.");
            CombineCoins = config.Bind(general, "CombineCoins", true,
                "Combine your cosmetic coins into the biggest coin they can make. A coin you can spend is never turned into one you cannot. Off only sorts them.");

            const string wheel = ConfigMoves.Wheel;
            WheelModifierKey = config.Bind(wheel, "Modifier", KeyCode.None,
                "None: the arrow keys open the wheel on their own. Set a key (LeftAlt was the old default) and the arrows only work while you hold it, for when another mod wants the bare arrows.");
            WheelFolderPrevKey = config.Bind(wheel, "PreviousFolder", KeyCode.LeftArrow, "Turn the ring one folder to the left.");
            WheelFolderNextKey = config.Bind(wheel, "NextFolder", KeyCode.RightArrow, "Turn the ring one folder to the right.");
            WheelLookPrevKey = config.Bind(wheel, "PreviousLook", KeyCode.UpArrow, "Up one look in the folder you are on.");
            WheelLookNextKey = config.Bind(wheel, "NextLook", KeyCode.DownArrow, "Down one look in the folder you are on.");
            WheelCopyKey = config.Bind(wheel, "CopyCode", KeyCode.C, "While the wheel is open: copy the code for the look you are on, to paste in Discord.");
            WheelConfirmKey = config.Bind(wheel, "Confirm", KeyCode.Return, "While the wheel is open: put the look you are on on now instead of waiting for the ring to fill.");
            WheelSoundVolume = config.Bind(wheel, "SoundVolume", 1f,
                new ConfigDescription("How loud the wheel's clicks are. 1 is as loud as the game's own menu, 0 is silent.", new AcceptableValueRange<float>(0f, 2f)));

            const string keys = ConfigMoves.Keys;
            RenameKey = config.Bind(keys, "Rename", KeyCode.F2, "While hovering a look: name it and pick its folder.");
            ExportKey = config.Bind(keys, "Export", KeyCode.F3, "In the cosmetics menu: write what you have on to Wardrobe/exported.cfg.");
            ImportKey = config.Bind(keys, "Reimport", KeyCode.F4, "In the cosmetics menu: read presets.cfg again right now.");
            SaveCodeKey = config.Bind(keys, "KeepOutfitCode", KeyCode.F7, "After somebody posts an outfit code in chat: keep it in your Shared folder.");

            const string advanced = ConfigMoves.Advanced;
            PresetSlots = config.Bind(advanced, "PresetSlots", 70,
                new ConfigDescription(
                    "Preset slots the cosmetics menu offers (the game has 28). Wardrobe adds more on its own as looks come in, up to " + SlotGrower.MaxSlots + ". Slots past 28 are dropped if the mod is removed.",
                    new AcceptableValueRange<int>(28, SlotGrower.MaxSlots)));
            TrashDays = config.Bind(advanced, "TrashDays", 30,
                new ConfigDescription("Days a look stays in Trash before it is removed for good. 0 keeps it until you empty the Trash.", new AcceptableValueRange<int>(0, 3650)));
            ShowHint = config.Bind(advanced, "ShowHint", true, "Show the short how-to the first time the Presets tab opens each session.");
            MarkNewAsSeen = config.Bind(advanced, "MarkNewAsSeen", true, "Clear the NEW badges at launch instead of making you hover every item.");
            TokenStackCap = config.Bind(advanced, "TokenStackCap", 12,
                new ConfigDescription("Coins drawn in the pile before it prints a count instead. 0 draws up to 65.", new AcceptableValueRange<int>(0, 65)));
            CombineRate = config.Bind(advanced, "CombineRate", 3,
                new ConfigDescription("Coins of one rarity it takes to make one of the rarity above.", new AcceptableValueRange<int>(CoinPile.MinCombineRate, 10)));
            BackupsToKeep = config.Bind(advanced, "BackupsToKeep", 30,
                new ConfigDescription("Recent save backups kept under Wardrobe/backups, on top of one a day for two weeks and the last good one.", new AcceptableValueRange<int>(1, 500)));
            WheelSettleSeconds = config.Bind(advanced, "WheelSettleSeconds", 1f,
                new ConfigDescription("Stay on a look on the wheel this long and it goes on.", new AcceptableValueRange<float>(0.2f, 10f)));
            LoggingLevel = config.Bind(advanced, "LogLevel", VerbosityLevel.Off,
                "Off keeps the console quiet. Debug logs imports and renames. Verbose logs menu rebuilds too.");
        }

        // BepInEx keeps lines it has no binding for as orphans and hands one to Bind when a key of
        // that name comes along. Renaming the orphans first is all a move takes: the player's value
        // lands on the new key, and the old line is gone the next time the file is written.
        private static void Migrate(ConfigFile config)
        {
            if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) is not Dictionary<ConfigDefinition, string> orphans)
            {
                return;
            }
            var values = new Dictionary<(string Section, string Key), string>();
            foreach (var pair in orphans)
            {
                values[(pair.Key.Section, pair.Key.Key)] = pair.Value;
            }
            ConfigMoves.Apply(values);
            orphans.Clear();
            foreach (var pair in values)
            {
                orphans[new ConfigDefinition(pair.Key.Section, pair.Key.Key)] = pair.Value;
            }
        }
    }
}
