namespace Wardrobe.Services
{
    internal enum WheelAction
    {
        None,
        Cancel,
        Take,
        Release,
        Settle
    }

    // What one frame of keys does to an open wheel. No Input calls in here: LookWheel reads the
    // keys and hands over what it saw.
    internal static class WheelInput
    {
        public static WheelAction Decide(bool escape, bool confirm, bool modifierSet, bool modifierHeld, bool stepped, bool holding, bool settled)
        {
            if (escape)
            {
                return WheelAction.Cancel;
            }
            if (confirm)
            {
                return WheelAction.Take;
            }
            if (modifierSet && !modifierHeld)
            {
                return WheelAction.Release;
            }
            if (stepped && !holding && settled)
            {
                return WheelAction.Settle;
            }
            return WheelAction.None;
        }

        // Whether closing on this puts the look under the cursor on. Backing out never does, a
        // wheel nobody moved only does when you asked for it with the confirm key.
        public static bool PutsOn(WheelAction action, bool stepped, int slot)
        {
            if ((slot < 0 && slot != WheelModel.OffSlot) || action == WheelAction.Cancel || action == WheelAction.None)
            {
                return false;
            }
            return stepped || action == WheelAction.Take;
        }
    }
}
