using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-10-01, Justin: "I think the colors are messed up? I'm nearly certain I was looking white
    // to everyone with any of what I had selected". His pause doll had the right colours the whole
    // time; the room did not. The game sends pieces and colours as two RPCs and a piece that gets
    // no colours after it is drawn near white on every other screen, and a receiver missing a
    // colour 10 s into a level paints every slot White. These pin what Wardrobe sends the room.
    public class RoomColoursTests
    {
        private const int Palette = 36;
        private const int Slots = 33;
        private const int White = 0;

        private static int[] Unicorn()
        {
            // His save at the time: white horn, Dark Brown 2 skin, the rest of the monkey.
            return new[] { 0, 33, 33, 33, 33, 33, 33, 33, 33, 33, 33, 33, 33, 0, 33, 33, 15, 35, 13, 2, 2, 23, 2, 0, 0, 14, 15, 15, 0, 15, 0, 0, 0 };
        }

        [Fact]
        public void WhatYouWearIsWhatTheRoomGets()
        {
            int[] sent = RoomColours.ForRoom(Unicorn(), new int[Slots], Slots, Palette);

            Assert.Equal(Unicorn(), sent);
        }

        [Fact]
        public void APowerUpsColoursGoOutOverTheSave()
        {
            int[] worn = Unicorn();
            worn[0] = White;
            worn[1] = White;
            worn[2] = White;

            Assert.Equal(worn, RoomColours.ForRoom(worn, Unicorn(), Slots, Palette));
        }

        // A slot your Semibot never got a colour for is -1 on it. Sent as is, the receiver keeps
        // it unpainted and its 10 s fallback turns the whole Semibot White.
        [Fact]
        public void ASlotNeverPaintedGoesOutInTheSavedColour()
        {
            int[] worn = Unicorn();
            worn[5] = -1;
            worn[20] = -1;

            int[] sent = RoomColours.ForRoom(worn, Unicorn(), Slots, Palette);

            Assert.Equal(Unicorn(), sent);
        }

        [Fact]
        public void NothingOutsideThePaletteGoesOut()
        {
            int[] worn = Enumerable.Repeat(99, Slots).ToArray();
            int[] saved = Enumerable.Repeat(-1, Slots).ToArray();

            int[] sent = RoomColours.ForRoom(worn, saved, Slots, Palette);

            Assert.All(sent, c => Assert.InRange(c, 0, Palette - 1));
        }

        [Fact]
        public void AShortListStillCoversEverySlot()
        {
            int[] sent = RoomColours.ForRoom(new[] { 4, 5 }, Unicorn(), Slots, Palette);

            Assert.Equal(Slots, sent.Length);
            Assert.Equal(4, sent[0]);
            Assert.Equal(5, sent[1]);
            Assert.Equal(Unicorn().Skip(2), sent.Skip(2));
        }

        // Every path that puts a look on (the tab, the wheel, Api.Equip, EquipForNow, OFF) ends in
        // PresetEquipper.Dress, which takes the pieces off (each one resets its slot to White), puts
        // the new ones on (their default colour) and then lays the look's colours over that.
        [Fact]
        public void ALooksColoursLandOnEverySlotItNames()
        {
            int[] afterPieces = new int[Slots];
            afterPieces[0] = 17;

            RoomColours.Onto(afterPieces, Unicorn(), Palette);

            Assert.Equal(Unicorn(), afterPieces);
        }

        // A stand-in is a piece in the same slot, so the look's colour for that slot is the one it
        // wears: the colour list is the look's, whatever pieces the matcher picked.
        [Fact]
        public void StandInsKeepTheLooksColours()
        {
            var catalogue = new List<Piece?>
            {
                new Piece { Index = 0, Slot = 0, Name = "Crown", Rarity = 3 },
                new Piece { Index = 1, Slot = 0, Name = "Paper Crown", Rarity = 1 },
            };
            List<Pick> picks = LookMatcher.Resolve(new[] { 0 }, catalogue, new HashSet<int> { 1 });
            Assert.True(picks[0].StoodIn);

            int[] worn = new int[Slots];
            RoomColours.Onto(worn, Unicorn(), Palette);

            Assert.Equal(Unicorn(), RoomColours.ForRoom(worn, null, Slots, Palette));
        }

        // OFF puts back the outfit kept in ownlook.txt. A colour in that file the game does not
        // have (a hand edit, a palette that shrank) must not reach the room as an index the
        // receiver clamps to White.
        [Fact]
        public void OffPutsYourOwnColoursBack()
        {
            OwnOutfit own = OwnOutfit.Read(new[]
            {
                "own=" + string.Join(",", Unicorn()) + "|Horn;Monkey|Unicorn Monkey",
            }, token => token == "Horn" ? 1 : token == "Monkey" ? 2 : -1);
            Outfit target = own.Target(Slots);

            int[] worn = new int[Slots];
            RoomColours.Onto(worn, target.Colours, Palette);

            Assert.Equal(Unicorn(), worn);
        }

        [Fact]
        public void AColourTheGameDoesNotHaveKeepsWhatThePieceGave()
        {
            int[] worn = Enumerable.Repeat(7, Slots).ToArray();
            int[] look = Unicorn();
            look[3] = -1;
            look[4] = Palette;

            RoomColours.Onto(worn, look, Palette);

            Assert.Equal(7, worn[3]);
            Assert.Equal(7, worn[4]);
            Assert.Equal(33, worn[5]);
        }

        // Warden's muted face, a scar, a costume: any mod may send pieces with no colours after
        // them. Those are owed colours at the end of the frame.
        [Fact]
        public void PiecesWithNoColoursAfterThemAreOwedColours()
        {
            var debt = new ColourDebt<string>();

            debt.CosmeticsSent("body");

            Assert.True(debt.Owes);
            Assert.Equal(new[] { "body" }, debt.Collect());
            Assert.False(debt.Owes);
        }

        [Fact]
        public void PiecesThenColoursOweNothing()
        {
            var debt = new ColourDebt<string>();

            debt.CosmeticsSent("body");
            debt.ColoursSent("body");
            debt.CosmeticsSent("head");
            debt.ColoursSent("head");

            Assert.False(debt.Owes);
            Assert.Empty(debt.Collect());
        }

        // Colours that went out before the pieces painted the old pieces, not the new ones.
        [Fact]
        public void ColoursBeforeThePiecesStillOwe()
        {
            var debt = new ColourDebt<string>();

            debt.ColoursSent("body");
            debt.CosmeticsSent("body");

            Assert.Equal(new[] { "body" }, debt.Collect());
        }

        [Fact]
        public void EachSemibotIsOwedOnce()
        {
            var debt = new ColourDebt<string>();

            debt.CosmeticsSent("body");
            debt.CosmeticsSent("body");
            debt.CosmeticsSent("head");

            Assert.Equal(new[] { "body", "head" }, debt.Collect());
        }

        [Fact]
        public void TheResendWaitsOutTheReceiversFallback()
        {
            var resend = new ColourResend();

            resend.Arm(100f);

            Assert.True(ColourResend.Delay > 10f);
            Assert.False(resend.Due(100f + 10f));
            Assert.True(resend.Due(100f + ColourResend.Delay));
            Assert.False(resend.Due(100f + ColourResend.Delay + 1f));
        }

        [Fact]
        public void ANewArmPushesTheResendBack()
        {
            var resend = new ColourResend();

            resend.Arm(0f);
            resend.Arm(8f);

            Assert.False(resend.Due(ColourResend.Delay));
            Assert.True(resend.Due(8f + ColourResend.Delay));
        }

        [Fact]
        public void NothingIsResentUnarmed()
        {
            Assert.False(new ColourResend().Due(1000f));
        }
    }
}
