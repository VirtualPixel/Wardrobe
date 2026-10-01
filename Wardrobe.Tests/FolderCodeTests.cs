using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // A folder code is a whole folder of looks on one line. It goes through the same Discord paste
    // an outfit code does, only longer, so it is held to the same rules and a few more.
    public class FolderCodeTests
    {
        private static OutfitCode Look(string name, params int[] cosmetics)
        {
            var look = new OutfitCode { Name = name };
            look.Cosmetics.AddRange(cosmetics);
            return look;
        }

        private static FolderCode Cartoons()
        {
            var folder = new FolderCode { Name = "Cartoons", GameVersion = "v0.4.4.3" };
            OutfitCode goofy = Look("Goofy", 12, 113, 284, 1041);
            goofy.Colors[0] = 1;
            goofy.Colors[5] = 20;
            folder.Looks.Add(goofy);
            folder.Looks.Add(Look("Perry the Platypus", 3, 17));
            folder.Looks.Add(Look("SpongeBob", 400));
            return folder;
        }

        private static FolderCode Decode(string text)
        {
            Assert.True(FolderCodec.TryDecode(text, out FolderCode? read, out string problem), problem);
            return read!;
        }

        private static void Same(FolderCode expected, FolderCode actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.GameVersion, actual.GameVersion);
            Assert.Equal(expected.Looks.Count, actual.Looks.Count);
            for (int i = 0; i < expected.Looks.Count; i++)
            {
                Assert.Equal(expected.Looks[i].Name, actual.Looks[i].Name);
                Assert.Equal(expected.Looks[i].Cosmetics, actual.Looks[i].Cosmetics);
                Assert.Equal(new List<KeyValuePair<int, int>>(expected.Looks[i].Colors), new List<KeyValuePair<int, int>>(actual.Looks[i].Colors));
            }
        }

        [Fact]
        public void AFolderSurvivesTheRoundTrip()
        {
            FolderCode folder = Cartoons();

            Same(folder, Decode(FolderCodec.Encode(folder)));
        }

        [Fact]
        public void AFolderCodeIsTaggedApartFromALookCode()
        {
            string code = FolderCodec.Encode(Cartoons());

            Assert.StartsWith("WD2-", code);
            foreach (string group in code.Substring(4).Split('-'))
            {
                Assert.InRange(group.Length, 1, 4);
            }
        }

        [Fact]
        public void ABigFolderStaysPlainTextAllTheWay()
        {
            var big = new FolderCode { Name = "Everything I own", GameVersion = "v0.5.0.9" };
            for (int i = 0; i < 60; i++)
            {
                OutfitCode look = Look("Look number " + i, i, i + 200, i * 17 + 1000);
                look.Colors[i % 33] = i % 36;
                big.Looks.Add(look);
            }

            string code = FolderCodec.Encode(big);

            Same(big, Decode(code));
            foreach (char c in code)
            {
                Assert.True(c == '-' || (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'), "'" + c + "' in the code");
            }
        }

        [Fact]
        public void AnEmptyFolderStillMakesACode()
        {
            var empty = new FolderCode { Name = "Nothing yet" };

            Same(empty, Decode(FolderCodec.Encode(empty)));
        }

        [Fact]
        public void AFolderNameLongerThanAFolderCanBeIsCutShort()
        {
            var wordy = new FolderCode { Name = new string('a', 60) };

            Assert.Equal(new string('a', 48), Decode(FolderCodec.Encode(wordy)).Name);
        }

        [Fact]
        public void HalfAFolderCodeIsRefusedWithAReason()
        {
            string code = FolderCodec.Encode(Cartoons());

            Assert.False(FolderCodec.TryDecode(code.Substring(0, code.Length / 2), out _, out string problem));
            Assert.Contains("did not come through in one piece", problem);
        }

        [Fact]
        public void OneCharacterChangedAnywhereIsCaught()
        {
            string code = FolderCodec.Encode(Cartoons());
            for (int at = 4; at < code.Length; at++)
            {
                if (code[at] == '-')
                {
                    continue;
                }
                string bent = code.Substring(0, at) + (code[at] == '7' ? '8' : '7') + code.Substring(at + 1);
                Assert.False(FolderCodec.TryDecode(bent, out _, out _), "changed at " + at);
            }
        }

        [Fact]
        public void ALookCodeIsNotAFolderCode()
        {
            string look = OutfitCodec.Encode(Look("Goofy", 12));

            Assert.False(FolderCodec.TryDecode(look, out _, out string problem));
            Assert.Contains("not a folder code", problem);
        }

        // ---- One door for every kind of code ----

        [Fact]
        public void AnOldLookCodeStillReadsThroughTheSameDoor()
        {
            // Posted before folder codes existed. It has to keep working as it did.
            const string old = "WD1-042G-8FAR-CT60-2171-ZW88-030N-2M20-858N-0G20-R58N-1G1G-YHBC-CNV6-AVKM-D0G4-8VV3-EHQQ-423P-60Q3-8BHM-5RSN-6";

            Assert.Equal(ShareCodes.Kind.Look, ShareCodes.Read(old, out OutfitCode? look, out FolderCode? folder, out string problem));
            Assert.Equal("", problem);
            Assert.Null(folder);
            Assert.Equal("Eleventh Doctor", look!.Name);
        }

        [Fact]
        public void AFolderCodeReadsAsAFolder()
        {
            string code = FolderCodec.Encode(Cartoons());

            Assert.Equal(ShareCodes.Kind.Folder, ShareCodes.Read(code, out OutfitCode? look, out FolderCode? folder, out _));
            Assert.Null(look);
            Same(Cartoons(), folder!);
        }

        [Fact]
        public void TextThatIsNoCodeAtAllIsSaidSo()
        {
            Assert.Equal(ShareCodes.Kind.None, ShareCodes.Read("dude sick outfit", out _, out _, out string problem));
            Assert.Equal("that is not a Wardrobe code, they start with WD1- or WD2-", problem);
        }

        [Fact]
        public void ACodeFromALaterWardrobeIsRefusedRatherThanGuessedAt()
        {
            Assert.Equal(ShareCodes.Kind.None, ShareCodes.Read("WD3-0000-0000", out _, out _, out string problem));
            Assert.Contains("newer Wardrobe", problem);
        }

        [Fact]
        public void ABrokenCodeKeepsTheReasonItsOwnReaderGave()
        {
            string code = FolderCodec.Encode(Cartoons());

            Assert.Equal(ShareCodes.Kind.None, ShareCodes.Read(code.Substring(0, code.Length - 3), out _, out _, out string problem));
            Assert.Contains("did not come through in one piece", problem);
        }

        [Fact]
        public void TheCodeIsPickedOutOfWhateverWasCopiedAroundIt()
        {
            string code = FolderCodec.Encode(Cartoons());

            Assert.Equal(code, ShareCodes.Find("here, all my cartoons:\n" + code + "\nhave fun"));
            Assert.Equal(code, ShareCodes.Find("  " + code.ToLowerInvariant() + " ").ToUpperInvariant());
            Assert.Equal("", ShareCodes.Find("nothing in here"));
        }

        [Theory]
        [InlineData("/outfit", "Post")]
        [InlineData("  /OUTFIT ", "Post")]
        [InlineData("/outfit paste", "Paste")]
        [InlineData("/Outfit   Paste", "Paste")]
        [InlineData("/outfits", "None")]
        [InlineData("/outfit something", "None")]
        [InlineData("nice outfit", "None")]
        public void TheChatCommandIsReadTheSameWhateverTheCase(string typed, string expected)
        {
            Assert.Equal(expected, ShareCodes.ReadCommand(typed).ToString());
        }
    }
}
