using System.Collections.Generic;
using UnityEngine;
using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    // What a pass over the coin pile decided. Tokens is the pile as it should end up; Lines is
    // what to tell the player about it.
    internal sealed class CoinPlan
    {
        public readonly List<int> Tokens = new List<int>();
        public readonly List<string> Lines = new List<string>();

        // Every cosmetic in the game is already unlocked, so no coin can buy anything.
        public bool NothingLocked;

        // Coins of a dead rarity were merged into a higher one.
        public bool Combined;

        // The pile that came in is not the pile going out.
        public bool Changed;
    }

    // The cosmetic shop machine spends the pile from the top: it reads the last token, offers a
    // locked cosmetic of that rarity, and a token whose rarity has nothing left to unlock is a
    // coin the machine will never take. So the pile is kept with the cheapest coin that can still
    // buy something on top, and coins are merged upward at the configured rate into the largest
    // coin they can make. Nothing is ever added: a coin is only ever made by spending CombineRate
    // coins of the rarity below it, and cosmeticUnlocks is never written.
    internal static class CoinPile
    {
        public const int Rarities = 4;

        // Below two, a merge would hand out value for free.
        public const int MinCombineRate = 2;

        private static readonly string[] Names = { "common", "uncommon", "rare", "ultra rare" };

        private static bool toldAboutFullSet;

        // The whole decision, with no game state in it: the pile, how many cosmetics of each
        // rarity are still locked, the merge rate, and whether merging is allowed at all.
        public static CoinPlan Plan(IReadOnlyList<int> tokens, IReadOnlyList<int> locked, int rate, bool combine)
        {
            var plan = new CoinPlan();
            var count = new int[Rarities];
            for (int i = 0; i < tokens.Count; i++)
            {
                count[Band(tokens[i])]++;
            }

            if (!AnyLockedFrom(locked, 0))
            {
                plan.NothingLocked = true;
                for (int i = 0; i < tokens.Count; i++)
                {
                    plan.Tokens.Add(tokens[i]);
                }
                return plan;
            }

            if (combine && rate >= MinCombineRate)
            {
                plan.Combined = Climb(count, 0, locked, rate, plan.Lines);
            }

            // Dead rarities sink to the start of the list, then the usable ones run dear to cheap,
            // which leaves the cheapest coin that can still buy something as the last element.
            Stack(plan.Tokens, count, locked, usable: false);
            Stack(plan.Tokens, count, locked, usable: true);

            plan.Changed = !Same(tokens, plan.Tokens);
            return plan;
        }

        // Every rate coins of a rarity become one of the rarity above, cheapest first, so coins climb
        // as high as they can in one sweep. A merge only happens when something is still locked at
        // or above the rarity it makes, and never turns coins the machine can spend into coins it
        // cannot: when the rarity above is finished, only as many coins are made as go on climbing
        // to one that is not.
        private static bool Climb(int[] count, int from, IReadOnlyList<int> locked, int rate, List<string>? lines)
        {
            bool any = false;
            for (int rarity = from; rarity < Rarities - 1; rarity++)
            {
                if (count[rarity] < rate || !AnyLockedFrom(locked, rarity + 1))
                {
                    continue;
                }
                int made = count[rarity] / rate;
                if (LockedAt(locked, rarity) > 0 && LockedAt(locked, rarity + 1) == 0)
                {
                    made = MostThatKeepClimbing(count, rarity, made, locked, rate);
                }
                if (made <= 0)
                {
                    continue;
                }
                int spent = made * rate;
                count[rarity] -= spent;
                count[rarity + 1] += made;
                any = true;
                lines?.Add(spent + " " + Names[rarity] + " coins became " + made + " " + Names[rarity + 1]);
            }
            return any;
        }

        // The most coins to make out of spendable ones without leaving more dead coins above them
        // than there would be without the merge.
        private static int MostThatKeepClimbing(int[] count, int rarity, int made, IReadOnlyList<int> locked, int rate)
        {
            int baseline = DeadAbove(Simulate(count, rarity, 0, locked, rate), rarity, locked);
            for (int m = made; m > 0; m--)
            {
                if (DeadAbove(Simulate(count, rarity, m, locked, rate), rarity, locked) <= baseline)
                {
                    return m;
                }
            }
            return 0;
        }

        private static int[] Simulate(int[] count, int rarity, int made, IReadOnlyList<int> locked, int rate)
        {
            var copy = (int[])count.Clone();
            copy[rarity] -= made * rate;
            copy[rarity + 1] += made;
            Climb(copy, rarity + 1, locked, rate, null);
            return copy;
        }

        private static int DeadAbove(int[] count, int rarity, IReadOnlyList<int> locked)
        {
            int dead = 0;
            for (int i = rarity + 1; i < Rarities; i++)
            {
                if (LockedAt(locked, i) == 0)
                {
                    dead += count[i];
                }
            }
            return dead;
        }

        // Reorder and merge the real pile, then save it the way the game saves it.
        public static bool Sort()
        {
            MetaManager meta = MetaManager.instance;
            if (!meta)
            {
                return false;
            }

            CoinPlan plan = Plan(
                meta.cosmeticTokens,
                LockedPerRarity(meta),
                PluginConfig.CombineRate.Value,
                PluginConfig.CombineCoins.Value);

            if (plan.NothingLocked)
            {
                SayTheSetIsFull();
                return false;
            }
            if (!plan.Changed)
            {
                return false;
            }

            SaveBackupService.BeforeWrite("coins");
            meta.cosmeticTokens.Clear();
            meta.cosmeticTokens.AddRange(plan.Tokens);
            meta.Save();

            if (plan.Combined)
            {
                string line = string.Join(", ", plan.Lines.ToArray()) + ".";
                Log.Always("Wardrobe: " + line);
                Notice(line);
            }
            return true;
        }

        // Setup only gives a coin its colour the frame it is made, so a coin that changed places
        // in the pile has to be told what it is now.
        public static void Repaint(CosmeticTokenUI ui)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta)
            {
                return;
            }
            List<int> tokens = meta.cosmeticTokens;
            CosmeticTokenUIElement? previous = null;
            int drawn = ui.tokenObjects.Count < tokens.Count ? ui.tokenObjects.Count : tokens.Count;
            for (int i = 0; i < drawn; i++)
            {
                CosmeticTokenUIElement element = ui.tokenObjects[i];
                if (!element)
                {
                    continue;
                }
                var rarity = (SemiFunc.Rarity)Band(tokens[i]);
                if (element.rarity != rarity)
                {
                    element.rarity = rarity;
                    element.index = i;
                    element.Setup(previous!);
                }
                previous = element;
            }
        }

        // Read only. Mirrors what CosmeticLockedGet will accept: a real asset, a valid prefab, and
        // an index the save does not already hold.
        private static int[] LockedPerRarity(MetaManager meta)
        {
            var locked = new int[Rarities];
            List<CosmeticAsset> assets = meta.cosmeticAssets;
            for (int i = 0; i < assets.Count; i++)
            {
                CosmeticAsset asset = assets[i];
                if (!asset || !asset.prefab.IsValid() || meta.cosmeticUnlocks.Contains(i))
                {
                    continue;
                }
                locked[Band((int)asset.rarity)]++;
            }
            return locked;
        }

        private static void Stack(List<int> into, int[] count, IReadOnlyList<int> locked, bool usable)
        {
            for (int rarity = Rarities - 1; rarity >= 0; rarity--)
            {
                if ((LockedAt(locked, rarity) > 0) != usable)
                {
                    continue;
                }
                for (int i = 0; i < count[rarity]; i++)
                {
                    into.Add(rarity);
                }
            }
        }

        private static bool AnyLockedFrom(IReadOnlyList<int> locked, int rarity)
        {
            for (int i = rarity; i < Rarities; i++)
            {
                if (LockedAt(locked, i) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static int LockedAt(IReadOnlyList<int> locked, int rarity)
        {
            return rarity >= 0 && rarity < locked.Count ? locked[rarity] : 0;
        }

        private static bool Same(IReadOnlyList<int> before, List<int> after)
        {
            if (before.Count != after.Count)
            {
                return false;
            }
            for (int i = 0; i < after.Count; i++)
            {
                if (Band(before[i]) != after[i])
                {
                    return false;
                }
            }
            return true;
        }

        // A save written by a future build could hold a rarity this build has no name for.
        private static int Band(int rarity)
        {
            if (rarity < 0)
            {
                return 0;
            }
            return rarity > Rarities - 1 ? Rarities - 1 : rarity;
        }

        private static void SayTheSetIsFull()
        {
            if (toldAboutFullSet || !MissionUI.instance)
            {
                return;
            }
            toldAboutFullSet = true;
            const string line = "Every cosmetic is unlocked, so the coins stay as they are.";
            Log.Always("Wardrobe: " + line);
            Notice(line);
        }

        private static void Notice(string line) => PopupUi.Focus(line, 5f);
    }
}
