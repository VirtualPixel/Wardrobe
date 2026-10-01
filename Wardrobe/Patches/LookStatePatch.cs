using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Every change to what you wear ends in this call (the menu, a preset slot, the wheel, undo),
    // so it is where the current look is worked out again.
    [HarmonyPatch(typeof(MetaManager), nameof(MetaManager.CosmeticPlayerUpdateLocal))]
    internal static class LookStatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(MetaManager __instance)
        {
            if (__instance == MetaManager.instance)
            {
                LookState.Recheck(__instance);
            }
        }
    }
}
