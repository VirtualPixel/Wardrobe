using System.Collections.Generic;

namespace Wardrobe.Configuration
{
    // Where every setting of the old config lives now.
    internal static class ConfigMoves
    {
        internal readonly struct Move
        {
            public readonly string FromSection;
            public readonly string FromKey;
            public readonly string ToSection;
            public readonly string ToKey;

            // An old value equal to this was never chosen by anybody, it is the old default. It is
            // dropped so the new default applies. Null moves every value.
            public readonly string? OldDefault;

            public Move(string fromSection, string fromKey, string toSection, string toKey, string? oldDefault = null)
            {
                FromSection = fromSection;
                FromKey = fromKey;
                ToSection = toSection;
                ToKey = toKey;
                OldDefault = oldDefault;
            }
        }

        public const string General = "General";
        public const string Wheel = "Wheel";
        public const string Keys = "Keys";
        public const string Advanced = "Advanced";

        public static readonly Move[] All =
        {
            new Move("Presets", "ImportLibrary", General, "ImportPresets"),
            new Move("Menu", "ShowPresetNames", General, "ShowPresetNames"),
            new Move("Menu", "ShowColorNames", General, "ShowColorNames"),
            new Move("Coins", "CombineWhenNothingLeft", General, "CombineCoins"),

            new Move("Wheel", "WheelModifier", Wheel, "Modifier", oldDefault: "LeftAlt"),
            new Move("Wheel", "WheelFolderPrev", Wheel, "PreviousFolder"),
            new Move("Wheel", "WheelFolderNext", Wheel, "NextFolder"),
            new Move("Wheel", "WheelLookPrev", Wheel, "PreviousLook"),
            new Move("Wheel", "WheelLookNext", Wheel, "NextLook"),

            new Move("Keys", "RenameKey", Keys, "Rename"),
            new Move("Keys", "ExportKey", Keys, "Export"),
            new Move("Keys", "ImportKey", Keys, "Reimport"),
            new Move("Keys", "SaveCodeKey", Keys, "KeepOutfitCode"),

            new Move("Presets", "PresetSlots", Advanced, "PresetSlots"),
            new Move("Presets", "TrashDays", Advanced, "TrashDays"),
            new Move("Menu", "ShowHint", Advanced, "ShowHint"),
            new Move("Menu", "MarkNewAsSeen", Advanced, "MarkNewAsSeen"),
            new Move("Menu", "TokenStackCap", Advanced, "TokenStackCap"),
            new Move("Coins", "CombineRate", Advanced, "CombineRate"),
            new Move("Backups", "BackupsToKeep", Advanced, "BackupsToKeep"),
            new Move("Wheel", "WheelSettleSeconds", Advanced, "WheelSettleSeconds"),
            new Move("Logging", "LogLevel", Advanced, "LogLevel")
        };

        // Settings for things the mod no longer has. Their lines are taken out and go nowhere.
        public static readonly (string Section, string Key)[] Dropped =
        {
            ("Exchange", "BaseUrl"),
            ("Advanced", "ExchangeUrl")
        };

        // Rewrites the (section, key) -> raw value table the config file was read into: each old
        // entry is taken out and its value handed to the new name, unless the new name already has
        // one (then the file was migrated before and the new value is the live one).
        public static void Apply(IDictionary<(string Section, string Key), string> values)
        {
            foreach (var gone in Dropped)
            {
                values.Remove(gone);
            }
            foreach (Move move in All)
            {
                if (!values.TryGetValue((move.FromSection, move.FromKey), out string value))
                {
                    continue;
                }
                values.Remove((move.FromSection, move.FromKey));
                if (move.OldDefault != null && string.Equals(value.Trim(), move.OldDefault, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!values.ContainsKey((move.ToSection, move.ToKey)))
                {
                    values[(move.ToSection, move.ToKey)] = value;
                }
            }
        }
    }
}
