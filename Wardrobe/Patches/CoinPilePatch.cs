using HarmonyLib;

namespace Wardrobe.Patches
{
    // The pile is combined and sorted whenever it can have changed or is about to be read: the UI
    // that draws it (which the game rebuilds on every coin gained or spent and every scene), the
    // cosmetics menu, the shop machine, and the save loading (MetaManagerLoadPatch).
    internal static class CoinPilePatch
    {
        [HarmonyPatch(typeof(CosmeticTokenUI), "Setup")]
        internal static class Pile
        {
            // Before the pile is drawn, so the game builds it in the order it ends up in.
            [HarmonyPrefix]
            public static void Prefix()
            {
                Services.CoinPile.Sort();
            }

            // After, because a coin that was already on screen keeps the colour it was made with.
            [HarmonyPostfix]
            public static void Postfix(CosmeticTokenUI __instance)
            {
                Services.CoinPile.Repaint(__instance);
            }
        }

        // Opening the cosmetics menu is a moment the pile gets looked at: combine before it is.
        [HarmonyPatch(typeof(MenuPageCosmetics), "Start")]
        internal static class Menu
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (Services.CoinPile.Sort() && (bool)CosmeticTokenUI.instance)
                {
                    CosmeticTokenUI.instance.Setup();
                }
            }
        }

        // CosmeticShopMachine.Interact reads cosmeticTokens[Count - 1] and hands that rarity to
        // CosmeticLockedGet, which returns nothing for a rarity the player has finished. Sort
        // first and the coin it reads is one that still buys something.
        [HarmonyPatch(typeof(CosmeticShopMachine), "Interact")]
        internal static class Machine
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                if (Services.CoinPile.Sort() && (bool)CosmeticTokenUI.instance)
                {
                    CosmeticTokenUI.instance.Setup();
                }
            }
        }
    }
}
