using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Wardrobe.Configuration;
using Wardrobe.Services;

namespace Wardrobe
{
    [BepInPlugin(Api.PluginGuid, "Wardrobe", BuildInfo.Version)]
    [BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
    public class Wardrobe : BaseUnityPlugin
    {
        internal static Wardrobe Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance.BaseLogger;
        private ManualLogSource BaseLogger => base.Logger;
        internal Harmony? Harmony { get; set; }

        private void Awake()
        {
            Instance = this;

            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            PluginConfig.Init(Config);

            // Copy the cosmetic saves aside before the game or any other mod touches them.
            SaveBackupService.BackupNow();

            Patch();

            WardrobeUI.Ensure();

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} loaded, log level {PluginConfig.LoggingLevel.Value}");
        }

        internal void Patch()
        {
            Harmony ??= new Harmony(Info.Metadata.GUID);
            Harmony.PatchAll();
        }
    }
}
