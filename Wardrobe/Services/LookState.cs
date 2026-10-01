using System;
using System.Collections.Generic;
using Photon.Pun;

namespace Wardrobe.Services
{
    // Which of your looks you have on. Set when Wardrobe puts one on, and checked again whenever
    // the game pushes your cosmetics to the doll, so a piece changed by hand drops you out of the
    // look and putting the same pieces back on by hand finds it again.
    internal static class LookState
    {
        // What the room reads to know another player's look. A plain string, empty for none.
        public const string RoomKey = "wardrobe.look";

        private static int slot = -1;

        public static string? Name { get; private set; }
        public static string? Category { get; private set; }
        public static int StandIns { get; private set; }

        public static event Action<string?>? Changed;

        // Wardrobe just put this slot on.
        public static void Wore(MetaManager meta, int wornSlot, int standIns)
        {
            slot = wornSlot;
            Set(meta, wornSlot, standIns);
        }

        public static void Recheck(MetaManager meta)
        {
            if (!meta || !meta.saveReady)
            {
                return;
            }
            if (slot >= 0 && PresetEquipper.IsWorn(meta, slot))
            {
                Set(meta, slot, StandIns);
                return;
            }
            slot = -1;
            PresetNames? names = PresetNames.Get();
            foreach (PresetGroup group in LookCatalog.Groups(meta, names))
            {
                foreach (int candidate in group.Slots)
                {
                    if (PresetEquipper.IsWorn(meta, candidate))
                    {
                        slot = candidate;
                        Set(meta, candidate, PresetEquipper.Resolve(meta, candidate).StandIns);
                        return;
                    }
                }
            }
            Set(meta, -1, 0);
        }

        private static void Set(MetaManager meta, int wornSlot, int standIns)
        {
            PresetNames? names = PresetNames.Get();
            string? name = null;
            string? category = null;
            if (wornSlot >= 0 && !PresetInfo.IsEmpty(meta, wornSlot))
            {
                name = PresetInfo.DisplayName(names, wornSlot);
                string raw = names?.GetCategory(wornSlot) ?? "";
                category = raw.Length > 0 ? raw : PresetNames.DefaultCategory;
            }
            StandIns = name == null ? 0 : standIns;
            Category = category;
            if (string.Equals(name, Name, StringComparison.Ordinal))
            {
                return;
            }
            Name = name;
            Publish();
            Log.Info("Wardrobe: now wearing " + (name ?? "no look") + ".");
            Changed?.Invoke(name);
        }

        // Custom player properties go out with the join and to everyone who joins later, so no
        // RPC is needed for the room to know.
        public static void Publish()
        {
            if (PhotonNetwork.LocalPlayer == null)
            {
                return;
            }
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { RoomKey, Name ?? "" } });
        }

        public static string? Of(PlayerAvatar avatar)
        {
            if (!avatar)
            {
                return null;
            }
            if (avatar == PlayerAvatar.instance || !avatar.photonView || avatar.photonView.IsMine)
            {
                return Name;
            }
            var owner = avatar.photonView.Owner;
            if (owner?.CustomProperties != null && owner.CustomProperties.TryGetValue(RoomKey, out object value)
                && value is string look && look.Length > 0)
            {
                return look;
            }
            return null;
        }
    }
}
