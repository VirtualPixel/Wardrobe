using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // A shared look is a presets.cfg section and nothing else. These are the rules a pasted code is
    // held to before it is written anywhere near the library.
    public class LookSectionTests
    {
        private const string Good = "[Waluigi]\ncategory = Games\nnote = purple\nhat = Cone\ncolor.hat = Dark Purple\n";

        private static IniSection Parse(string text)
        {
            Assert.True(LookSection.TryParse(text, out IniSection? section, out string problem), problem);
            return section!;
        }

        [Fact]
        public void AGoodSectionKeepsItsNameFolderNoteAndPieces()
        {
            IniSection section = Parse(Good);

            Assert.Equal("Waluigi", LookSection.Name(section));
            Assert.Equal("Games", LookSection.Category(section));
            Assert.Equal("purple", LookSection.Note(section));
            Assert.Equal(new[] { "hat", "color.hat" }, Pieces(section));
        }

        [Fact]
        public void CarriageReturnsFromAWindowsMachineDoNotBreakIt()
        {
            IniSection section = Parse("[Waluigi]\r\nhat = Cone\r\n");

            Assert.Equal("Waluigi", LookSection.Name(section));
            Assert.Equal("Cone", section.Get("hat"));
        }

        [Fact]
        public void TwoLooksInOneUploadAreRefused()
        {
            Assert.False(LookSection.TryParse(Good + "\n[Second]\nhat = Cone\n", out _, out string problem));
            Assert.Equal("more than one look in there", problem);
        }

        [Fact]
        public void PlainTextWithNoSectionLineIsRefused()
        {
            Assert.False(LookSection.TryParse("hat = Cone\n", out _, out string problem));
            Assert.Equal("no [name] line", problem);
        }

        [Fact]
        public void ASectionWearingNothingIsRefused()
        {
            Assert.False(LookSection.TryParse("[Waluigi]\ncategory = Games\nnote = nothing on\n", out _, out string problem));
            Assert.Equal("the look has no items", problem);
        }

        [Fact]
        public void AnEmptyUploadIsRefused()
        {
            Assert.False(LookSection.TryParse("", out _, out string problem));
            Assert.Equal("the look is empty", problem);
        }

        [Fact]
        public void SomethingLongEnoughToBeAFileIsRefused()
        {
            Assert.False(LookSection.TryParse("[x]\nhat = " + new string('a', 9000) + "\n", out _, out string problem));
            Assert.Equal("the look is too long", problem);
        }

        [Fact]
        public void ControlCharactersAndBracketsNeverReachTheFile()
        {
            Assert.Equal("Waluigi", LookSection.Clean("[Wal\u0007uigi]\n", LookSection.MaxNameLength));
        }

        [Fact]
        public void ANameLongerThanTheCapIsCutNotRefused()
        {
            Assert.Equal(10, LookSection.Clean(new string('a', 400), 10).Length);
        }

        [Fact]
        public void WritingASectionAndReadingItBackGivesTheSamePieces()
        {
            var pieces = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("hat", "Cone"),
                new KeyValuePair<string, string>("color.hat", "Dark Purple")
            };

            IniSection section = Parse(LookSection.Text("Waluigi", "Games", "purple", pieces));

            Assert.Equal("Cone", section.Get("hat"));
            Assert.Equal("Dark Purple", section.Get("color.hat"));
            Assert.Equal("Games", LookSection.Category(section));
        }

        [Fact]
        public void ALoneCarriageReturnCannotSmuggleASecondKeyIn()
        {
            // "hat = Cone\rbodytop = Tie" on one line would otherwise land in presets.cfg as one
            // value with a key hidden inside it.
            IniSection section = Parse("[Waluigi]\rhat = Cone\rbodytop = Tie\r");

            Assert.Equal("Cone", section.Get("hat"));
            Assert.Equal("Tie", section.Get("bodytop"));
        }

        [Theory]
        [InlineData("hat<script>")]
        [InlineData("hat key")]
        [InlineData("hat/../../etc")]
        [InlineData("hat\tkey")]
        [InlineData("hat%20key")]
        [InlineData("[hat]")]
        public void AKeyThatIsNotOneIsRefusedBeforeAnythingIsWritten(string key)
        {
            Assert.False(LookSection.TryParse("[Waluigi]\n" + key + " = Cone\n", out _, out string problem));
            Assert.StartsWith("a key this does not know", problem);
        }

        [Fact]
        public void AKeyLongerThanFortyIsRefused()
        {
            Assert.False(LookSection.TryParse("[Waluigi]\n" + new string('a', 41) + " = Cone\n", out _, out _));
            Assert.True(LookSection.IsKey(new string('a', 40)));
        }

        [Fact]
        public void TheKeysTheLibraryActuallyUsesAreAllFine()
        {
            Assert.True(LookSection.IsKey("hat"));
            Assert.True(LookSection.IsKey("color.bodytop"));
            Assert.True(LookSection.IsKey("legright"));
            Assert.True(LookSection.IsKey("category"));
        }

        // An arrival that lands on an existing section name is folded into that section on the
        // next read, so the free name has to be free as it is written, after Clean has cut it.
        [Fact]
        public void AClashingNameAtFullLengthStillLandsOnANameOfItsOwn()
        {
            string longest = new string('a', LookSection.MaxNameLength);
            IniFile library = IniFile.Parse(new[] { "[" + longest + "]", "hat = Cone" });

            string free = LookInbox.FreeName(library, longest);

            Assert.Null(library.Find(LookSection.Clean(free, LookSection.MaxNameLength)));
        }

        [Fact]
        public void AClashIsCheckedOnTheNameAsItWillBeWritten()
        {
            IniFile library = IniFile.Parse(new[] { "[Mario]", "hat = Cone" });

            string free = LookInbox.FreeName(library, "Mar[io");

            Assert.Null(library.Find(LookSection.Clean(free, LookSection.MaxNameLength)));
        }

        // A look you made in the game has its name in names.txt and not in presets.cfg. A pasted code
        // taking that name was skipped by the importer as a clash while the popup said it had landed.
        [Fact]
        public void ANameOnOneOfYourOwnLooksIsTakenToo()
        {
            IniFile library = IniFile.Parse(new[] { "[Luigi]", "hat = Cone" });

            string free = LookInbox.FreeName(library, "Mario", name => name == "Mario");

            Assert.Equal("Mario 2", free);
        }

        private static string[] Pieces(IniSection section)
        {
            var keys = new List<string>();
            foreach (var piece in LookSection.Pieces(section))
            {
                keys.Add(piece.Key);
            }
            return keys.ToArray();
        }
    }
}
