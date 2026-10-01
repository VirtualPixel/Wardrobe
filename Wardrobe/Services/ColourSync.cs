using Photon.Pun;
using UnityEngine;

namespace Wardrobe.Services
{
    // Keeps the room's picture of your colours in step with your own (RoomColours says why it can
    // drift). Pieces sent with no colours after them get colours at the end of the frame, and a
    // while after each level generates, or someone joins, your colours go out once more.
    internal static class ColourSync
    {
        private static readonly ColourDebt<PlayerCosmetics> debt = new ColourDebt<PlayerCosmetics>();
        private static readonly ColourResend resend = new ColourResend();
        private static LevelGenerator? armedFor;

        // The test SetupCosmetics and SetupColors make before they send: synced, in a room, and
        // yours (a death head goes by the Semibot it belongs to). Menu dolls never reach the room.
        public static bool ToRoom(PlayerCosmetics cosmetics, bool synced)
        {
            if (!synced || !cosmetics || !SemiFunc.IsMultiplayer())
            {
                return false;
            }
            if (cosmetics.playerAvatarVisuals && cosmetics.playerAvatarVisuals.isMenuAvatar)
            {
                return false;
            }
            PlayerDeathHead head = cosmetics.deathHead;
            PhotonView view = head && head.setup && head.playerAvatar ? head.playerAvatar.photonView : cosmetics.photonView;
            return view && view.IsMine;
        }

        public static void CosmeticsSent(PlayerCosmetics cosmetics)
        {
            debt.CosmeticsSent(cosmetics);
            WardrobeUI.Ensure();
        }

        public static void ColoursSent(PlayerCosmetics cosmetics) => debt.ColoursSent(cosmetics);

        public static void PlayerJoined() => resend.Arm(Time.unscaledTime);

        public static void Tick()
        {
            LevelGenerator level = LevelGenerator.Instance;
            if (level && level.Generated && level != armedFor)
            {
                armedFor = level;
                resend.Arm(Time.unscaledTime);
            }
            if (resend.Due(Time.unscaledTime))
            {
                Resend();
            }
        }

        // Runs after every Update, so a mod that sent pieces this frame has had its chance to send
        // colours too.
        public static void Settle()
        {
            if (!debt.Owes)
            {
                return;
            }
            foreach (PlayerCosmetics cosmetics in debt.Collect())
            {
                if (cosmetics && ToRoom(cosmetics, true))
                {
                    Send(cosmetics, "pieces went out with no colours");
                }
            }
        }

        private static void Resend()
        {
            PlayerAvatar me = SemiFunc.PlayerGetLocal();
            if (!me || !SemiFunc.IsMultiplayer())
            {
                return;
            }
            Send(me.playerCosmetics, "level or player join");
            if (me.playerDeathHead)
            {
                Send(me.playerDeathHead.playerCosmetics, "level or player join");
            }
        }

        // Before its first setup a Semibot has no colours of its own yet, and that setup sends them.
        private static void Send(PlayerCosmetics cosmetics, string why)
        {
            MetaManager meta = MetaManager.instance;
            if (!cosmetics || cosmetics.firstSetup || cosmetics.colorsEquipped == null || !meta || meta.colors == null || meta.colors.Count == 0)
            {
                return;
            }
            int[] colours = RoomColours.ForRoom(cosmetics.colorsEquipped, meta.colorsEquipped, cosmetics.colorsEquipped.Length, meta.colors.Count);
            cosmetics.SetupColors(true, colours);
            Log.Verbose("Wardrobe: colours sent to the room again (" + why + ").");
        }
    }
}
