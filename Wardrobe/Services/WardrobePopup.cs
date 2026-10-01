using MenuLib;
using MenuLib.MonoBehaviors;

namespace Wardrobe.Services
{
    // Every Wardrobe popup goes through here. It opens as the game's "page on top" (the popup
    // becomes the current page, the cosmetics page under it goes inactive and takes no clicks)
    // and the game itself wakes the page underneath again when the popup closes.
    internal static class WardrobePopup
    {
        private static REPOPopupPage? current;
        private static MenuScrollBox? lockedScroll;

        public static REPOPopupPage Open(string title, MenuPageCosmetics? page, float spacing = 4f)
        {
            Close();
            REPOPopupPage popup = MenuAPI.CreateREPOPopupPage(title, REPOPopupPage.PresetSide.Left, false, true, spacing);
            current = popup;
            if (page && page!.menuScrollBox)
            {
                lockedScroll = page.menuScrollBox;
                lockedScroll.scrollKeybindsDisabled = true;
            }
            popup.onEscapePressed = () =>
            {
                current = null;
                Unlock();
                return true;
            };
            return popup;
        }

        // False once the page is closed, escaped, torn down with the menu, or replaced by another
        // Wardrobe page.
        public static bool IsShowing(REPOPopupPage? popup) => popup && current == popup;

        // Call instead of popup.OpenPage: false here means "replace as current page, remember the one under it".
        public static void Show(REPOPopupPage popup)
        {
            popup.OpenPage(false);
        }

        public static void Close()
        {
            if (current)
            {
                current!.ClosePage(true);
            }
            current = null;
            Unlock();
        }

        private static void Unlock()
        {
            if (lockedScroll)
            {
                lockedScroll!.scrollKeybindsDisabled = false;
            }
            lockedScroll = null;
        }
    }
}
