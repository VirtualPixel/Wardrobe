using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // The coin pile is spent from the top and a coin whose rarity is finished buys nothing. These
    // are the rules that decide what the pile looks like, with no game in the way.
    public class CoinPileTests
    {
        private const int Common = 0;
        private const int Uncommon = 1;
        private const int Rare = 2;
        private const int UltraRare = 3;

        private static List<int> Pile(params int[] tokens) => new List<int>(tokens);

        // A pile is worth what it costs to build: one coin of a rarity is worth rate of the one
        // below. Reordering keeps this the same; combining keeps it the same; nothing may raise it.
        private static int Worth(IReadOnlyList<int> tokens, int rate)
        {
            int total = 0;
            foreach (int token in tokens)
            {
                int value = 1;
                for (int i = 0; i < token; i++)
                {
                    value *= rate;
                }
                total += value;
            }
            return total;
        }

        [Fact]
        public void CheapestUsableCoinEndsUpOnTop()
        {
            CoinPlan plan = CoinPile.Plan(Pile(UltraRare, Common, Rare, Uncommon), new[] { 5, 5, 5, 5 }, 3, true);

            Assert.Equal(new[] { UltraRare, Rare, Uncommon, Common }, plan.Tokens);
            Assert.True(plan.Changed);
            Assert.False(plan.Combined);
        }

        [Fact]
        public void CoinsOfAFinishedRaritySinkToTheBottom()
        {
            // Nothing common is left to unlock, and two commons is under the rate, so they stay
            // commons. They just stop sitting where the machine would read them.
            CoinPlan plan = CoinPile.Plan(Pile(Common, Rare, Common, Uncommon), new[] { 0, 4, 4, 0 }, 3, true);

            Assert.Equal(new[] { Common, Common, Rare, Uncommon }, plan.Tokens);
            Assert.False(plan.Combined);
        }

        [Fact]
        public void APileThatIsAlreadyRightIsLeftAlone()
        {
            CoinPlan plan = CoinPile.Plan(Pile(Rare, Uncommon, Common), new[] { 1, 1, 1, 1 }, 3, true);

            Assert.False(plan.Changed);
            Assert.False(plan.Combined);
            Assert.Empty(plan.Lines);
        }

        [Fact]
        public void ThreeDeadCommonsBecomeOneUncommon()
        {
            CoinPlan plan = CoinPile.Plan(Pile(Common, Common, Common), new[] { 0, 4, 0, 0 }, 3, true);

            Assert.Equal(new[] { Uncommon }, plan.Tokens);
            Assert.True(plan.Combined);
            Assert.Equal("3 common coins became 1 uncommon", Assert.Single(plan.Lines));
        }

        [Fact]
        public void CoinsClimbTheWholeLadderInOnePass()
        {
            // Nine dead commons make three uncommons, and uncommon is dead too, so those three
            // make the one rare the player can actually spend.
            CoinPlan plan = CoinPile.Plan(
                Pile(Common, Common, Common, Common, Common, Common, Common, Common, Common),
                new[] { 0, 0, 2, 0 },
                3,
                true);

            Assert.Equal(new[] { Rare }, plan.Tokens);
            Assert.Equal(2, plan.Lines.Count);
            Assert.Equal("9 common coins became 3 uncommon", plan.Lines[0]);
            Assert.Equal("3 uncommon coins became 1 rare", plan.Lines[1]);
        }

        [Fact]
        public void CheapestCoinsAreSpentFirst()
        {
            // Common and uncommon are both finished. Because the commons are merged first, the
            // two uncommons they make join the four already there and the pile reaches six, which
            // is two rares. Merging the uncommons first would have made one rare and stranded four
            // commons.
            CoinPlan plan = CoinPile.Plan(
                Pile(Common, Common, Common, Common, Common, Common, Uncommon, Uncommon, Uncommon, Uncommon),
                new[] { 0, 0, 1, 0 },
                3,
                true);

            Assert.Equal(new[] { Rare, Rare }, plan.Tokens);
            Assert.Equal("6 common coins became 2 uncommon", plan.Lines[0]);
            Assert.Equal("6 uncommon coins became 2 rare", plan.Lines[1]);
        }

        [Fact]
        public void LeftoversUnderTheRateStayAsTheyAre()
        {
            CoinPlan plan = CoinPile.Plan(
                Pile(Common, Common, Common, Common, Common),
                new[] { 0, 3, 0, 0 },
                3,
                true);

            Assert.Equal(new[] { Common, Common, Uncommon }, plan.Tokens);
            Assert.Equal("3 common coins became 1 uncommon", Assert.Single(plan.Lines));
        }

        [Fact]
        public void TheRateIsWhatTheConfigSays()
        {
            CoinPlan two = CoinPile.Plan(Pile(Common, Common, Common, Common), new[] { 0, 9, 0, 0 }, 2, true);
            CoinPlan five = CoinPile.Plan(Pile(Common, Common, Common, Common), new[] { 0, 9, 0, 0 }, 5, true);

            Assert.Equal(new[] { Uncommon, Uncommon }, two.Tokens);
            Assert.False(five.Combined);
            Assert.Equal(4, five.Tokens.Count);
        }

        [Fact]
        public void ARateUnderTwoWouldMakeValueFromNothingSoItDoesNotRun()
        {
            CoinPlan plan = CoinPile.Plan(Pile(Common, Common), new[] { 0, 9, 0, 0 }, 1, true);

            Assert.False(plan.Combined);
            Assert.Equal(new[] { Common, Common }, plan.Tokens);
        }

        [Fact]
        public void TurningCombiningOffOnlyReorders()
        {
            CoinPlan plan = CoinPile.Plan(Pile(Common, Common, Common, Rare), new[] { 0, 2, 2, 0 }, 3, false);

            Assert.False(plan.Combined);
            Assert.Equal(new[] { Common, Common, Common, Rare }, plan.Tokens);
        }

        [Fact]
        public void DeadCoinsWithNothingLockedAboveThemStayCoinsOfTheirOwnRarity()
        {
            // Rare and ultra rare are both finished, so three rares have nowhere to climb to.
            // They sink under the uncommon, which is the one coin that still buys something.
            CoinPlan plan = CoinPile.Plan(Pile(Rare, Rare, Rare, Uncommon), new[] { 0, 3, 0, 0 }, 3, true);

            Assert.False(plan.Combined);
            Assert.Equal(new[] { Rare, Rare, Rare, Uncommon }, plan.Tokens);
        }

        [Fact]
        public void DeadCoinsClimbPastADeadRarityToReachALiveOne()
        {
            // Uncommon and rare are finished but ultra rare is not, so commons climb the ladder
            // one rung at a time until they land somewhere that buys something.
            var nine = new List<int>();
            for (int i = 0; i < 27; i++)
            {
                nine.Add(Common);
            }

            CoinPlan plan = CoinPile.Plan(nine, new[] { 0, 0, 0, 2 }, 3, true);

            Assert.Equal(new[] { UltraRare }, plan.Tokens);
            Assert.Equal(3, plan.Lines.Count);
        }

        [Fact]
        public void WhenEverythingIsUnlockedThePileIsLeftAlone()
        {
            List<int> before = Pile(Common, UltraRare, Common, Rare);

            CoinPlan plan = CoinPile.Plan(before, new[] { 0, 0, 0, 0 }, 3, true);

            Assert.True(plan.NothingLocked);
            Assert.False(plan.Changed);
            Assert.False(plan.Combined);
            Assert.Empty(plan.Lines);
            Assert.Equal(before, plan.Tokens);
        }

        [Fact]
        public void AnEmptyPileStaysEmpty()
        {
            CoinPlan plan = CoinPile.Plan(Pile(), new[] { 3, 3, 3, 3 }, 3, true);

            Assert.Empty(plan.Tokens);
            Assert.False(plan.Changed);
        }

        [Fact]
        public void APileIsNeverWorthMoreThanItWas()
        {
            var piles = new[]
            {
                Pile(Common, Common, Common, Common, Common, Uncommon, Rare),
                Pile(UltraRare, UltraRare, Common),
                Pile(Uncommon, Uncommon, Uncommon, Uncommon, Common, Common, Common),
                Pile(Common, Uncommon, Rare, UltraRare),
                Pile()
            };
            var sets = new[]
            {
                new[] { 0, 0, 0, 1 },
                new[] { 0, 2, 0, 0 },
                new[] { 4, 4, 4, 4 },
                new[] { 0, 0, 5, 0 },
                new[] { 0, 0, 0, 0 }
            };

            foreach (List<int> pile in piles)
            {
                foreach (int[] locked in sets)
                {
                    for (int rate = 2; rate <= 5; rate++)
                    {
                        CoinPlan plan = CoinPile.Plan(pile, locked, rate, true);
                        Assert.True(
                            Worth(plan.Tokens, rate) <= Worth(pile, rate),
                            "a pile of " + pile.Count + " at rate " + rate + " grew in value");
                        Assert.True(plan.Tokens.Count <= pile.Count);
                    }
                }
            }
        }

        [Fact]
        public void ReorderingNeverAddsOrDropsACoin()
        {
            List<int> pile = Pile(UltraRare, Common, Rare, Common, Uncommon, Rare);

            CoinPlan plan = CoinPile.Plan(pile, new[] { 1, 1, 1, 1 }, 3, true);

            Assert.Equal(SortedCopy(pile), SortedCopy(plan.Tokens));
        }

        [Fact]
        public void ARarityThisBuildHasNoNameForIsKeptInsideTheLadder()
        {
            CoinPlan plan = CoinPile.Plan(Pile(9, -2), new[] { 1, 0, 0, 1 }, 3, true);

            Assert.Equal(new[] { UltraRare, Common }, plan.Tokens);
        }

        private static List<int> SortedCopy(IEnumerable<int> tokens)
        {
            var copy = new List<int>(tokens);
            copy.Sort();
            return copy;
        }
            // ---- Combining to the largest coin ----

        [Fact]
        public void LiveCoinsCombineToTheLargestCoinTheyCanMake()
        {
            // Every rarity still has something locked. Nine commons are three uncommons are one rare.
            CoinPlan plan = CoinPile.Plan(Many(Common, 9), new[] { 5, 5, 5, 5 }, 3, true);

            Assert.Equal(new[] { Rare }, plan.Tokens);
            Assert.True(plan.Combined);
            Assert.Equal("9 common coins became 3 uncommon", plan.Lines[0]);
            Assert.Equal("3 uncommon coins became 1 rare", plan.Lines[1]);
        }

        [Fact]
        public void ItClimbsAllTheWayToUltraRare()
        {
            CoinPlan plan = CoinPile.Plan(Many(Common, 28), new[] { 5, 5, 5, 5 }, 3, true);

            Assert.Equal(new[] { UltraRare, Common }, plan.Tokens);
        }

        [Fact]
        public void LeftoversSitOnTopAsTheCheapestCoin()
        {
            CoinPlan plan = CoinPile.Plan(Pile(Common, Common, Common, Common, Uncommon), new[] { 5, 5, 5, 5 }, 3, true);

            Assert.Equal(new[] { Uncommon, Uncommon, Common }, plan.Tokens);
        }

        [Fact]
        public void ALiveCoinIsNeverTurnedIntoADeadOne()
        {
            // Uncommon is finished. Three commons would make one uncommon the machine cannot spend
            // and that is too few to climb on to rare, so the commons stay commons.
            CoinPlan plan = CoinPile.Plan(Many(Common, 5), new[] { 5, 0, 5, 0 }, 3, true);

            Assert.False(plan.Combined);
            Assert.Equal(Many(Common, 5), plan.Tokens);
        }

        [Fact]
        public void LiveCoinsCrossADeadRarityWhenTheyReachALiveOne()
        {
            // Nine commons climb through the finished uncommon to a rare; the tenth stays.
            CoinPlan plan = CoinPile.Plan(Many(Common, 10), new[] { 5, 0, 5, 0 }, 3, true);

            Assert.Equal(new[] { Rare, Common }, plan.Tokens);
        }

        [Fact]
        public void NothingCombinesIntoATopThatIsFinished()
        {
            // Ultra rare is all unlocked: three rares stay three rares.
            CoinPlan plan = CoinPile.Plan(Many(Rare, 3), new[] { 5, 5, 5, 0 }, 3, true);

            Assert.False(plan.Combined);
            Assert.Equal(Many(Rare, 3), plan.Tokens);
        }

        [Fact]
        public void SwitchedOffNothingCombinesEvenDeadCoins()
        {
            CoinPlan plan = CoinPile.Plan(Many(Common, 9), new[] { 0, 5, 5, 5 }, 3, false);

            Assert.False(plan.Combined);
            Assert.Equal(9, plan.Tokens.Count);
        }

        private static List<int> Many(int rarity, int count)
        {
            var list = new List<int>();
            for (int i = 0; i < count; i++)
            {
                list.Add(rarity);
            }
            return list;
        }
    }
}
