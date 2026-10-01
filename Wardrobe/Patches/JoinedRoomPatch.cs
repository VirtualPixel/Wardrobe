using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Tell a room you just joined which look you have on.
    [HarmonyPatch(typeof(NetworkConnect), nameof(NetworkConnect.OnJoinedRoom))]
    internal static class JoinedRoomPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            LookState.Publish();
        }
    }
}
