using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Postfixes, so they run when REPOLib's prefix sends the pieces itself and skips the game's own.
    [HarmonyPatch(typeof(PlayerCosmetics), nameof(PlayerCosmetics.SetupCosmetics))]
    internal static class CosmeticsSentPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerCosmetics __instance, bool _synced)
        {
            if (ColourSync.ToRoom(__instance, _synced))
            {
                ColourSync.CosmeticsSent(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerCosmetics), nameof(PlayerCosmetics.SetupColors))]
    internal static class ColoursSentPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerCosmetics __instance, bool _synced)
        {
            if (ColourSync.ToRoom(__instance, _synced))
            {
                ColourSync.ColoursSent(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnPlayerEnteredRoom))]
    internal static class PlayerEnteredPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            ColourSync.PlayerJoined();
        }
    }
}
