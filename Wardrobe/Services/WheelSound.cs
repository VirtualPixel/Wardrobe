using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    internal enum WheelMove
    {
        Look,
        Folder,
        PutOn,
        Cancel,
        Copy,
        Nothing
    }

    // The wheel's sounds are the menu's own, played the way the game plays them: on this machine
    // only, never through the voice chat or Jukebox. Every MenuManager sound is 2D on the Default
    // type, in a level as much as in the menu, so nothing here fades with distance. Stepping
    // through looks is the chat box's tick, a folder is the same tick lower; putting a look on is
    // the menu's confirm; Escape is a popup closing.
    internal static class WheelSound
    {
        // The game's tick is quiet next to its clicks (0.2 against 0.5, and a quieter clip), so a
        // step is turned up until it lands as loud as clicking a button in the menu.
        public static float Volume(WheelMove move)
        {
            switch (move)
            {
                case WheelMove.Look:
                    return 0.56f;
                case WheelMove.Folder:
                    return 0.62f;
                default:
                    return 0.5f;
            }
        }

        public static void Play(WheelMove move)
        {
            MenuManager menu = MenuManager.instance;
            float scale = PluginConfig.WheelSoundVolume.Value;
            if (!menu || scale <= 0f)
            {
                return;
            }
            float volume = Volume(move) * scale;
            switch (move)
            {
                case WheelMove.Look:
                    menu.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, null, 1f, volume, soundOnly: true);
                    break;
                case WheelMove.Folder:
                    menu.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, null, 0.7f, volume, soundOnly: true);
                    break;
                case WheelMove.PutOn:
                    menu.MenuEffectClick(MenuManager.MenuClickEffectType.Confirm, null, -1f, volume, soundOnly: true);
                    break;
                case WheelMove.Cancel:
                    // MenuEffectPopUpClose takes no volume; this is the same call with one.
                    menu.soundWindowPopUpClose.Play(menu.soundPosition, scale);
                    break;
                case WheelMove.Copy:
                    menu.MenuEffectClick(MenuManager.MenuClickEffectType.Action, null, -1f, volume, soundOnly: true);
                    break;
                case WheelMove.Nothing:
                    menu.MenuEffectClick(MenuManager.MenuClickEffectType.Dud, null, -1f, volume, soundOnly: true);
                    break;
            }
        }
    }
}
