using System.IO;
using System.Text;
using HarmonyLib;
using Wardrobe.Configuration;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // presetSlots is read by PresetsInitialize inside Awake, so it has to change before Awake runs.
    [HarmonyPatch(typeof(MetaManager), "Awake")]
    internal static class MetaManagerAwakePatch
    {
        [HarmonyPrefix]
        public static void Prefix(MetaManager __instance)
        {
            string[] names = File.Exists(WardrobePaths.NamesFile) ? File.ReadAllLines(WardrobePaths.NamesFile, Encoding.UTF8) : new string[0];
            __instance.presetSlots = SlotGrower.StartCount(PluginConfig.PresetSlots.Value, names);
        }
    }
}
