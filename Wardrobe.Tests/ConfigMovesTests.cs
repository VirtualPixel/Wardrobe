using System.Collections.Generic;
using Wardrobe.Configuration;
using Xunit;

namespace Wardrobe.Tests
{
    // The config got fewer sections. Whatever a player set under an old name must come through
    // under the new one; nothing they chose may be lost.
    public class ConfigMovesTests
    {
        private static Dictionary<(string Section, string Key), string> Old(params (string section, string key, string value)[] rows)
        {
            var values = new Dictionary<(string Section, string Key), string>();
            foreach (var row in rows)
            {
                values[(row.section, row.key)] = row.value;
            }
            return values;
        }

        [Fact]
        public void EveryOldKeyHasAHome()
        {
            var old = new[]
            {
                ("Presets", "PresetSlots"), ("Presets", "ImportLibrary"), ("Presets", "TrashDays"),
                ("Menu", "ShowPresetNames"), ("Menu", "ShowColorNames"), ("Menu", "ShowHint"), ("Menu", "MarkNewAsSeen"), ("Menu", "TokenStackCap"),
                ("Keys", "RenameKey"), ("Keys", "ExportKey"), ("Keys", "ImportKey"), ("Keys", "SaveCodeKey"),
                ("Coins", "CombineWhenNothingLeft"), ("Coins", "CombineRate"),
                ("Backups", "BackupsToKeep"),
                ("Wheel", "WheelModifier"), ("Wheel", "WheelFolderPrev"), ("Wheel", "WheelFolderNext"), ("Wheel", "WheelLookPrev"), ("Wheel", "WheelLookNext"), ("Wheel", "WheelSettleSeconds"),
                ("Logging", "LogLevel")
            };
            foreach (var (section, key) in old)
            {
                Assert.Contains(ConfigMoves.All, m => m.FromSection == section && m.FromKey == key);
            }
        }

        [Fact]
        public void AValueSetUnderAnOldNameComesThroughUnderTheNewOne()
        {
            var values = Old(("Presets", "PresetSlots", "140"), ("Coins", "CombineWhenNothingLeft", "false"), ("Keys", "RenameKey", "F9"));

            ConfigMoves.Apply(values);

            Assert.Equal("140", values[("Advanced", "PresetSlots")]);
            Assert.Equal("false", values[("General", "CombineCoins")]);
            Assert.Equal("F9", values[("Keys", "Rename")]);
            Assert.False(values.ContainsKey(("Presets", "PresetSlots")));
        }

        [Fact]
        public void TheOldWheelModifierDefaultGivesWayToBareArrows()
        {
            var untouched = Old(("Wheel", "WheelModifier", "LeftAlt"));
            var chosen = Old(("Wheel", "WheelModifier", "RightControl"));

            ConfigMoves.Apply(untouched);
            ConfigMoves.Apply(chosen);

            Assert.False(untouched.ContainsKey(("Wheel", "Modifier")));
            Assert.Equal("RightControl", chosen[("Wheel", "Modifier")]);
        }

        // The exchange is gone. Its address goes with it, whichever layout the file was written in,
        // and nothing turns up under another name in its place.
        [Fact]
        public void TheOldExchangeAddressIsDroppedQuietly()
        {
            var current = Old(("Advanced", "ExchangeUrl", "https://example.invalid/wardrobe"), ("Advanced", "TrashDays", "7"));
            var older = Old(("Exchange", "BaseUrl", "https://example.invalid/wardrobe"));

            ConfigMoves.Apply(current);
            ConfigMoves.Apply(older);

            Assert.Equal(new[] { ("Advanced", "TrashDays") }, current.Keys);
            Assert.Empty(older);
        }

        [Fact]
        public void AValueAlreadyUnderTheNewNameWins()
        {
            var values = Old(("Keys", "RenameKey", "F9"), ("Keys", "Rename", "F10"));

            ConfigMoves.Apply(values);

            Assert.Equal("F10", values[("Keys", "Rename")]);
        }

        [Fact]
        public void NewKeysAreUnique()
        {
            var seen = new HashSet<(string, string)>();
            foreach (ConfigMoves.Move move in ConfigMoves.All)
            {
                Assert.True(seen.Add((move.ToSection, move.ToKey)), move.ToSection + "/" + move.ToKey);
            }
        }
    }
}
