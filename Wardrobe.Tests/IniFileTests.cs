using System.IO;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // presets.cfg and names.txt are both this reader, so the parsing rules are worth pinning down.
    public class IniFileTests
    {
        private static IniFile Parse(string text)
        {
            string path = Path.Combine(Path.GetTempPath(), "wardrobe-ini-" + Path.GetRandomFileName());
            File.WriteAllText(path, text);
            try
            {
                return IniFile.Load(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void KeysAreCaseInsensitiveButKeepTheirOrder()
        {
            IniFile ini = Parse("[Look]\nHat = Cone\nbodytop = Tie\n");
            IniSection? section = ini.Find("look");

            Assert.NotNull(section);
            Assert.Equal("Cone", section!.Get("hat"));
            Assert.Equal(new[] { "Hat", "bodytop" }, new[] { section.Entries[0].Key, section.Entries[1].Key });
        }

        [Fact]
        public void CommentsAndBlankLinesAreSkipped()
        {
            IniFile ini = Parse("# a note\n\n[Look]\n; another\nhat = Cone\n");
            Assert.Single(ini.Sections);
            Assert.Single(ini.Find("Look")!.Entries);
        }

        [Fact]
        public void OnlyTheFirstEqualsSplitsAKey()
        {
            // Notes routinely carry an equals sign, and they must survive a save and reload.
            IniFile ini = Parse("[Look]\nnote = width = 10\n");
            Assert.Equal("width = 10", ini.Find("Look")!.Get("note"));
        }

        [Fact]
        public void ALineBeforeAnySectionIsIgnored()
        {
            IniFile ini = Parse("stray = value\n[Look]\nhat = Cone\n");
            Assert.Single(ini.Sections);
            Assert.Equal("Look", ini.Sections[0].Name);
        }

        [Fact]
        public void RewritingAKeyReplacesItInPlace()
        {
            var section = new IniSection("Look");
            section.Set("hat", "Cone");
            section.Set("HAT", "Top Hat");

            Assert.Single(section.Entries);
            Assert.Equal("Top Hat", section.Get("hat"));
        }

        private static IniFile RoundTrip(IniFile ini)
        {
            string path = Path.Combine(Path.GetTempPath(), "wardrobe-ini-" + Path.GetRandomFileName());
            try
            {
                ini.Save(path);
                return IniFile.Load(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // names.txt keys its .library section by look name, and a look can be called anything a
        // section header holds: "#1 Fan" read back as a comment, "A=B" as key A, and the importer
        // then took the look for one of yours and skipped it on every launch.
        [Theory]
        [InlineData("#1 Fan")]
        [InlineData("; wink")]
        [InlineData("[Draft")]
        [InlineData("Size=XL")]
        [InlineData("50%3D off")]
        public void AKeyThatLooksLikeSyntaxSurvivesASave(string name)
        {
            var ini = new IniFile();
            ini.GetOrAdd("save.library").Set(name, "abc123");

            IniSection? back = RoundTrip(ini).Find("save.library");

            Assert.NotNull(back);
            Assert.Single(back!.Entries);
            Assert.Equal(name, back.Entries[0].Key);
            Assert.Equal("abc123", back.Get(name));
        }

        [Fact]
        public void ALineBreakInAValueCannotStartANewKey()
        {
            var ini = new IniFile();
            ini.GetOrAdd("account").Set("persona", "Vippy\ntoken = stolen");

            IniSection? back = RoundTrip(ini).Find("account");

            Assert.NotNull(back);
            Assert.Null(back!.Get("token"));
            Assert.Equal("Vippy token = stolen", back.Get("persona"));
        }

        [Fact]
        public void EmptySectionsAreNotWrittenBackOut()
        {
            var ini = new IniFile();
            ini.GetOrAdd("keep").Set("a", "1");
            ini.GetOrAdd("drop");

            string path = Path.Combine(Path.GetTempPath(), "wardrobe-ini-" + Path.GetRandomFileName());
            try
            {
                ini.Save(path);
                string written = File.ReadAllText(path);
                Assert.Contains("[keep]", written);
                Assert.DoesNotContain("[drop]", written);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
