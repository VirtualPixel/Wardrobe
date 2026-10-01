using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Runs after REPOLib has merged its modded save, so imports land on the final preset lists.
    [HarmonyPatch(typeof(MetaManager), "Load")]
    internal static class MetaManagerLoadPatch
    {
        [HarmonyPostfix]
        [HarmonyAfter("REPOLib")]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(MetaManager __instance)
        {
            if (__instance != MetaManager.instance)
            {
                return;
            }
            SaveBackupService.InspectAndPrune();
            PresetNames.Reload(__instance);
            CatalogueWriter.Write(__instance);
            PresetImporter.Run(__instance);
            TrashKeeper.PurgeExpired(__instance, PresetNames.Get()!);
            WardrobeUI.Ensure().WarmIcons(__instance);
            LookState.Recheck(__instance);
            OwnLooks.Launched(__instance);
            CoinPile.Sort();
        }
    }
}
