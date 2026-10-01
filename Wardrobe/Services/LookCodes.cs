using System.Collections.Generic;
using UnityEngine;
using Wardrobe.Configuration;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // Codes on the game's side of the fence: turn a look or a whole folder into one, turn one back
    // into presets.cfg sections, and hold the last look somebody posted in chat until you say what
    // to do with it. Nothing here ever puts a cosmetic on.
    internal static class LookCodes
    {
        public const string SharedCategory = "Shared";

        private static OutfitCode? pending;
        private static string pendingFrom = "";

        public static bool HasPending => pending != null;

        public static string PendingLine => pending == null
            ? ""
            : (pendingFrom.Length > 0 ? pendingFrom + " shared " : "Somebody shared ") + "'" + Title(pending) + "'";

        // The version title the game shows in the corner of the menu. Two copies of the same title
        // agree on what every cosmetic index means; two that differ might not.
        public static string GameVersion => BuildManager.instance ? BuildManager.instance.version.title : "";

        public static string Title(OutfitCode code) => code.Name.Length > 0 ? code.Name : "a look";

        public static string FromEquipped(MetaManager meta, string name)
        {
            return OutfitCodec.Encode(Build(meta, name, meta.cosmeticEquipped, meta.colorsEquipped));
        }

        // The worn look has no name of its own, so it borrows the one on the slot it came off if
        // that slot still matches, and otherwise says what it is.
        public static string WornName()
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!meta || names == null)
            {
                return "My look";
            }
            string live = PresetLibrary.HashOf(meta.cosmeticEquipped, meta.colorsEquipped);
            for (int slot = 0; slot < meta.cosmeticPresets.Count; slot++)
            {
                if (!PresetInfo.IsEmpty(meta, slot) && names.GetName(slot).Length > 0
                    && PresetLibrary.HashOf(meta.cosmeticPresets[slot], meta.colorPresets[slot]) == live)
                {
                    return names.GetName(slot);
                }
            }
            return "My look";
        }

        public static string FromSlot(MetaManager meta, int slot, string name)
        {
            if (slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return "";
            }
            return OutfitCodec.Encode(Build(meta, name, meta.cosmeticPresets[slot], meta.colorPresets[slot]));
        }

        // Every look in a folder, in the folder's own order, under the folder's name. Empty when
        // there is no such folder or nothing in it.
        public static string FromFolder(MetaManager meta, string folder, out int count)
        {
            count = 0;
            PresetNames? names = PresetNames.Get();
            foreach (PresetGroup group in LookCatalog.Groups(meta, names))
            {
                if (!string.Equals(group.Title, folder, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var code = new FolderCode { Name = group.Title, GameVersion = GameVersion };
                foreach (int slot in group.Slots)
                {
                    if (code.Looks.Count == FolderCodec.MaxLooks)
                    {
                        break;
                    }
                    code.Looks.Add(Build(meta, PresetInfo.DisplayName(names, slot), meta.cosmeticPresets[slot], meta.colorPresets[slot]));
                }
                count = code.Looks.Count;
                return count > 0 ? FolderCodec.Encode(code) : "";
            }
            return "";
        }

        // Puts a code on the clipboard and says so the way the rest of Wardrobe talks.
        public static string Copied(string code, string whose)
        {
            UnityEngine.GUIUtility.systemCopyBuffer = code;
            Log.Info("Wardrobe: copied the code for " + whose + ".");
            return "Copied " + Possessive(whose) + " code.";
        }

        public static string Possessive(string name)
        {
            return name.EndsWith("s", System.StringComparison.OrdinalIgnoreCase) ? name + "'" : name + "'s";
        }

        public static string Looks(int count) => count + (count == 1 ? " look" : " looks");

        // The same rule the exporter uses: a colour for every slot that is wearing something, and
        // for the bare body parts the skin tints whether or not anything covers them.
        private static OutfitCode Build(MetaManager meta, string name, IList<int> cosmetics, IList<int> colors)
        {
            var code = new OutfitCode { Name = name, GameVersion = GameVersion };
            var used = new bool[Slots.Count];
            foreach (int index in cosmetics)
            {
                if (index < 0 || index >= meta.cosmeticAssets.Count || !meta.cosmeticAssets[index])
                {
                    continue;
                }
                code.Cosmetics.Add(index);
                used[(int)meta.cosmeticAssets[index].type] = true;
            }
            for (int i = 0; i < colors.Count && i < Slots.Count; i++)
            {
                if (colors[i] >= 0 && (used[i] || Slots.IsMesh((SemiFunc.CosmeticType)i)))
                {
                    code.Colors[i] = colors[i];
                }
            }
            return code;
        }

        // What this copy of the game makes of a code: the section it would write, and the indices
        // it has no cosmetic for. Those are left out rather than guessed at.
        public static string ToSection(MetaManager meta, OutfitCode code, string folder, string note, out int missing)
        {
            missing = 0;
            var pieces = new List<KeyValuePair<string, string>>();
            var used = new bool[Slots.Count];
            foreach (int index in code.Cosmetics)
            {
                if (index < 0 || index >= meta.cosmeticAssets.Count || !meta.cosmeticAssets[index])
                {
                    missing++;
                    continue;
                }
                CosmeticAsset asset = meta.cosmeticAssets[index];
                used[(int)asset.type] = true;
                pieces.Add(new KeyValuePair<string, string>(Slots.Key(asset.type), asset.assetName.Trim()));
            }
            foreach (var pair in code.Colors)
            {
                if (pair.Key < 0 || pair.Key >= Slots.Count || pair.Value < 0 || pair.Value >= meta.colors.Count)
                {
                    continue;
                }
                var type = (SemiFunc.CosmeticType)pair.Key;
                if (used[pair.Key] || Slots.IsMesh(type))
                {
                    pieces.Add(new KeyValuePair<string, string>("color." + Slots.Key(type), Palette.Name(meta, pair.Value)));
                }
            }
            return LookSection.Text(Title(code), folder, note, pieces);
        }

        // Saved into the library, not put on. Anything in it the reader has not unlocked shows as
        // locked on the slot, the way every other preset with a locked item does.
        public static bool Save(MetaManager meta, OutfitCode code, string into, string note, out string landed)
        {
            landed = "";
            IniSection? section = Section(meta, code, into, note);
            if (section == null)
            {
                return false;
            }
            InboxPlan plan = LookInbox.AddAll(meta, new[] { section }, into, note);
            if (plan.Adds.Count == 0)
            {
                landed = "You already have " + Title(code) + " in " + plan.Folder + ".";
                return true;
            }
            string name = LookSection.Name(plan.Adds[0]);
            landed = "Added " + name + " to " + plan.Folder + ".";
            Log.Always("Wardrobe: saved the outfit code '" + name + "' into " + plan.Folder + ".");
            return true;
        }

        // Every look in the folder code that this game can wear, into one folder. Looks it already
        // has there, piece for piece, are left alone, so the same code pasted twice adds nothing.
        public static bool SaveFolder(MetaManager meta, FolderCode code, string into, out string landed)
        {
            var sections = new List<IniSection>();
            foreach (OutfitCode look in code.Looks)
            {
                IniSection? section = Section(meta, look, into, "");
                if (section != null)
                {
                    sections.Add(section);
                }
            }
            if (sections.Count == 0)
            {
                landed = "There is nothing in that folder this game can wear.";
                return false;
            }
            InboxPlan plan = LookInbox.AddAll(meta, sections, into, "");
            int dropped = code.Looks.Count - sections.Count;
            landed = plan.Adds.Count > 0
                ? "Added " + Looks(plan.Adds.Count) + " to " + plan.Folder
                : "You already have all of " + plan.Folder;
            if (plan.Adds.Count > 0 && plan.AlreadyThere > 0)
            {
                landed += ", " + plan.AlreadyThere + " you already had";
            }
            if (dropped > 0)
            {
                landed += ". " + Looks(dropped) + " had nothing this game can wear";
            }
            landed += ".";
            Log.Always("Wardrobe: folder code '" + code.Name + "' into " + plan.Folder + ": " + plan.Adds.Count + " added, "
                + plan.AlreadyThere + " already there, " + dropped + " unwearable.");
            return true;
        }

        private static IniSection? Section(MetaManager meta, OutfitCode code, string into, string note)
        {
            string text = ToSection(meta, code, into, note, out int missing);
            if (missing >= code.Cosmetics.Count)
            {
                Log.Info("Wardrobe: '" + Title(code) + "' has nothing this game has a cosmetic for.");
                return null;
            }
            if (!LookSection.TryParse(text, out IniSection? section, out string problem))
            {
                Log.Always("Wardrobe: '" + Title(code) + "' does not read as a look: " + problem);
                return null;
            }
            return section;
        }

        // Whatever code is on the clipboard, straight into the library: a look into Shared, a folder
        // under its own name. The line that comes back says what happened.
        public static string SaveClipboard(MetaManager meta)
        {
            string text = ShareCodes.Find(UnityEngine.GUIUtility.systemCopyBuffer ?? "");
            if (text.Length == 0)
            {
                return "There is no Wardrobe code on your clipboard.";
            }
            string landed;
            switch (ShareCodes.Read(text, out OutfitCode? look, out FolderCode? folder, out string problem))
            {
                case ShareCodes.Kind.Look:
                    return Save(meta, look!, SharedCategory, "", out landed) ? landed : "That code has nothing this game can wear.";
                case ShareCodes.Kind.Folder:
                    SaveFolder(meta, folder!, folder!.Name, out landed);
                    return landed;
                default:
                    return "That code did not read: " + problem + ".";
            }
        }

        // ---- What comes in over chat ----

        public static void Offer(string from, OutfitCode code)
        {
            pending = code;
            pendingFrom = from;
            Log.Info("Wardrobe: " + (from.Length > 0 ? from : "somebody") + " posted the outfit code for '" + Title(code) + "'.");
            PopupUi.Focus(PendingLine + ". " + PluginConfig.SaveCodeKey.Value + " saves it.", 6f);
        }

        public static void SavePending()
        {
            MetaManager meta = MetaManager.instance;
            OutfitCode? code = pending;
            if (!meta || code == null)
            {
                return;
            }
            pending = null;
            string note = pendingFrom.Length > 0 ? "From " + pendingFrom + " in chat." : "";
            bool saved = Save(meta, code, SharedCategory, note, out string landed);
            PopupUi.Focus(saved ? landed : "That outfit code has nothing this game can wear.", 5f);
        }
    }
}
