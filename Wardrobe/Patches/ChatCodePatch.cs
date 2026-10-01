using System;
using HarmonyLib;
using Photon.Pun;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // "/outfit" in the chat box goes out as the code for what you have on, and a code anybody
    // posts is offered back as something you can keep. It is an offer and nothing more: no
    // cosmetic goes on, and the message travels as the plain chat line it looks like.
    // "/outfit paste" keeps the code on your clipboard, look or folder, and sends nothing: the
    // chat box takes fifty characters and no pasting, so a code never gets typed in there.
    [HarmonyPatch(typeof(PlayerAvatar))]
    internal static class ChatCodePatch
    {
        // Runs before the game hands a leading slash to the debug commands, so "/outfit" never
        // reaches them and the code it turns into does not start with one either.
        [HarmonyPatch(nameof(PlayerAvatar.ChatMessageSend))]
        [HarmonyPrefix]
        public static bool SendPrefix(ref string _message)
        {
            ShareCodes.Command command = ShareCodes.ReadCommand(_message);
            MetaManager meta = MetaManager.instance;
            if (command == ShareCodes.Command.None || !meta)
            {
                return true;
            }
            if (command == ShareCodes.Command.Paste)
            {
                PopupUi.Focus(LookCodes.SaveClipboard(meta), 5f);
                return false;
            }
            _message = LookCodes.FromEquipped(meta, LookCodes.WornName());
            Log.Info("Wardrobe: posted the outfit code to chat.");
            return true;
        }

        // The chat reads every line aloud. A code is a hundred letters of nothing to say, so a
        // message that is only a code goes to the bubble and the log and skips the voice.
        [HarmonyPatch("ChatMessageSpeak")]
        [HarmonyPrefix]
        public static bool SpeakPrefix(string _message)
        {
            if (string.IsNullOrEmpty(_message))
            {
                return true;
            }
            string body = _message.Trim();
            if (!body.StartsWith(OutfitCodec.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            foreach (char c in body)
            {
                if (char.IsWhiteSpace(c))
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPatch(nameof(PlayerAvatar.ChatMessageSendRPC))]
        [HarmonyPostfix]
        public static void ReceivePostfix(PlayerAvatar __instance, string _message, PhotonMessageInfo _info)
        {
            // The same gate the message itself passed through. A code nobody in the room actually
            // typed has no business turning up as something to keep.
            if (__instance.isLocal || string.IsNullOrEmpty(_message)
                || !SemiFunc.MasterAndOwnerOnlyRPC(_info, __instance.photonView))
            {
                return;
            }
            // Only a look is offered from chat. A folder code is far past what the chat box takes,
            // so one turning up there was not typed by anybody.
            string found = ShareCodes.Find(_message);
            if (!found.StartsWith(OutfitCodec.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (OutfitCodec.TryDecode(found, out OutfitCode? code, out string problem) && code != null)
            {
                LookCodes.Offer(__instance.playerName, code);
            }
            else
            {
                Log.Info("Wardrobe: an outfit code came through chat but " + problem + ".");
            }
        }
    }
}
