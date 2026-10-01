using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // An outfit code is read on a stranger's machine, off a Discord paste, on a build that may not
    // be the one it was written on. These are the rules it is held to before any of that matters.
    public class OutfitCodeTests
    {
        private static OutfitCode Sample()
        {
            var code = new OutfitCode { Name = "Eleventh Doctor", GameVersion = "v0.4.4.3" };
            code.Cosmetics.AddRange(new[] { 3, 17, 400, 1041 });
            code.Colors[0] = 12;
            code.Colors[7] = 3;
            code.Colors[31] = 35;
            return code;
        }

        private static OutfitCode Decode(string text)
        {
            Assert.True(OutfitCodec.TryDecode(text, out OutfitCode? read, out string problem), problem);
            return read!;
        }

        private static void Same(OutfitCode expected, OutfitCode actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.GameVersion, actual.GameVersion);
            Assert.Equal(expected.Cosmetics, actual.Cosmetics);
            Assert.Equal(new List<KeyValuePair<int, int>>(expected.Colors), new List<KeyValuePair<int, int>>(actual.Colors));
        }

        [Fact]
        public void ALookSurvivesTheRoundTrip()
        {
            OutfitCode look = Sample();

            Same(look, Decode(OutfitCodec.Encode(look)));
        }

        [Fact]
        public void ACodeStartsWithThePrefixAndIsGroupedInFours()
        {
            string code = OutfitCodec.Encode(Sample());

            Assert.StartsWith("WD1-", code);
            foreach (string group in code.Substring(4).Split('-'))
            {
                Assert.InRange(group.Length, 1, 4);
            }
        }

        [Fact]
        public void TheSameLookAlwaysComesOutAsTheSameCode()
        {
            var shuffled = new OutfitCode { Name = "Eleventh Doctor", GameVersion = "v0.4.4.3" };
            shuffled.Cosmetics.AddRange(new[] { 1041, 3, 400, 17, 3 });
            shuffled.Colors[31] = 35;
            shuffled.Colors[0] = 12;
            shuffled.Colors[7] = 3;

            Assert.Equal(OutfitCodec.Encode(Sample()), OutfitCodec.Encode(shuffled));
        }

        [Fact]
        public void ALookWithNothingOnItStillMakesACode()
        {
            var bare = new OutfitCode { Name = "", GameVersion = "" };

            Same(bare, Decode(OutfitCodec.Encode(bare)));
        }

        [Fact]
        public void DashesAndCapitalsAreThrownAwayOnTheWayIn()
        {
            string code = OutfitCodec.Encode(Sample());

            Same(Decode(code), Decode(code.Replace("-", "").ToLowerInvariant()));
            Same(Decode(code), Decode("  " + code.Replace("-", "-  -") + "  "));
        }

        [Fact]
        public void TheLettersCrockfordLeavesOutAreReadAsTheDigitsTheyLookLike()
        {
            string code = OutfitCodec.Encode(Sample());

            // I, L and O are never written, so seeing one means somebody typed the code out by
            // hand. They mean the 1, 1 and 0 they were mistaken for.
            string body = code.Substring(4);
            Assert.DoesNotContain("I", body);
            Assert.DoesNotContain("L", body);
            Assert.DoesNotContain("O", body);
            Same(Decode(code), Decode("WD1-" + body.Replace("1", "L").Replace("0", "O")));
        }

        [Fact]
        public void UIsNotACodeCharacterAtAll()
        {
            Assert.False(OutfitCodec.TryDecode("WD1-8U8U", out _, out string problem));
            Assert.Equal("'U' is not part of an outfit code", problem);
        }

        [Fact]
        public void SomethingThatIsNotACodeIsSaidSo()
        {
            Assert.False(OutfitCodec.TryDecode("dude sick outfit", out _, out string problem));
            Assert.Contains("not an outfit code", problem);
        }

        [Fact]
        public void ACharacterLostInChatIsCaughtByTheChecksum()
        {
            string code = OutfitCodec.Encode(Sample());

            Assert.False(OutfitCodec.TryDecode(code.Substring(0, code.Length - 1), out _, out string problem));
            Assert.Contains("did not come through in one piece", problem);
        }

        [Fact]
        public void ACharacterChangedInTheMiddleIsCaughtToo()
        {
            string code = OutfitCodec.Encode(Sample());
            const int at = 6;
            string bent = code.Substring(0, at) + (code[at] == '7' ? '8' : '7') + code.Substring(at + 1);

            Assert.False(OutfitCodec.TryDecode(bent, out _, out string problem));
            Assert.Contains("did not come through in one piece", problem);
        }

        [Fact]
        public void ACodeFromALaterFormatIsRefusedRatherThanGuessedAt()
        {
            // The format byte is the first byte of the payload, so bending the first character of
            // the body is enough to make it a format nobody here knows.
            var bytes = new List<byte> { 2, 0, 0, 0, 0 };
            Assert.False(OutfitCodec.TryDecode(WithChecksum(bytes), out _, out string problem));
            Assert.Contains("newer Wardrobe", problem);
        }

        [Fact]
        public void ANameLongerThanTheCodeHoldsIsCutShort()
        {
            var wordy = new OutfitCode { Name = "Doctor Who Eleventh Doctor With The Fez", GameVersion = "v0.4.4.3" };
            wordy.Cosmetics.Add(4);

            OutfitCode read = Decode(OutfitCodec.Encode(wordy));

            Assert.Equal("Doctor Who Eleventh Doct", read.Name);
            Assert.StartsWith(read.Name, wordy.Name);
        }

        [Fact]
        public void ANameIsNeverCutThroughTheMiddleOfACharacter()
        {
            // A letter and eight three-byte characters is twenty-five bytes, so the eighth of them
            // straddles the cut and is dropped whole instead of sliced down the middle.
            var wide = new OutfitCode { Name = "A" + new string('\u4e2d', 8) };

            OutfitCode read = Decode(OutfitCodec.Encode(wide));

            Assert.Equal("A" + new string('\u4e2d', 7), read.Name);
        }

        [Fact]
        public void TheGameVersionRidesAlongAndSaysWhenItDoesNotMatch()
        {
            OutfitCode read = Decode(OutfitCodec.Encode(Sample()));

            Assert.Equal("v0.4.4.3", read.GameVersion);
            Assert.True(read.SameGame("v0.4.4.3"));
            Assert.True(read.SameGame("V0.4.4.3"));
            Assert.False(read.SameGame("v0.5.0.9"));
        }

        [Fact]
        public void ACodeFromABuildThatDidNotSayIsNotTreatedAsAMismatch()
        {
            OutfitCode read = Decode(OutfitCodec.Encode(new OutfitCode { Name = "Nameless build" }));

            Assert.True(read.SameGame("v0.4.4.3"));
            Assert.True(read.SameGame(""));
        }

        [Fact]
        public void AColourOnEveryOneOfTheGamesSlotsFitsInTheMask()
        {
            var full = new OutfitCode { Name = "Everything", GameVersion = "v0.4.4.3" };
            for (int slot = 0; slot < 33; slot++)
            {
                full.Colors[slot] = slot % 36;
                full.Cosmetics.Add(slot * 7);
            }

            Same(full, Decode(OutfitCodec.Encode(full)));
        }

        [Fact]
        public void ACodeCutInHalfIsRefusedRatherThanReadAsHalfALook()
        {
            var bytes = new List<byte> { 1, 4, 3 };

            Assert.False(OutfitCodec.TryDecode(WithChecksum(bytes), out _, out string problem));
            Assert.Equal("the code is cut short", problem);
        }

        [Fact]
        public void AnEmptyCodeIsTooShortToBeOne()
        {
            Assert.False(OutfitCodec.TryDecode("WD1-", out _, out string problem));
            Assert.Contains("too short", problem);
        }

        // Payloads built by hand, so a test can be about the bytes rather than about a look. The
        // checksum has to be right or the reader never gets as far as reading them.
        private static string WithChecksum(List<byte> payload)
        {
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var bytes = new List<byte>(payload) { Crc(payload) };
            var sb = new System.Text.StringBuilder("WD1-");
            int acc = 0;
            int bits = 0;
            foreach (byte b in bytes)
            {
                acc = acc << 8 | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    sb.Append(alphabet[acc >> bits & 0x1F]);
                }
            }
            if (bits > 0)
            {
                sb.Append(alphabet[acc << 5 - bits & 0x1F]);
            }
            return sb.ToString();
        }

        private static byte Crc(List<byte> bytes)
        {
            int crc = 0;
            foreach (byte b in bytes)
            {
                crc ^= b;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x80) != 0 ? crc << 1 ^ 0x07 : crc << 1;
                    crc &= 0xFF;
                }
            }
            return (byte)crc;
        }
    }
}
