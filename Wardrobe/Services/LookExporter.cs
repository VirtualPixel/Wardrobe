using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // Turns a look into a presets.cfg section. F3 appends one to Wardrobe/exported.cfg, and a
    // pasted code turns into the same text, so anything shared is a section you could have typed
    // yourself.
    internal static class LookExporter
    {
        public static string Export(MetaManager meta)
        {
            WardrobePaths.EnsureRoot();
            string title = "Look " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            string section = FromEquipped(meta, title, PresetNames.DefaultCategory, "");
            File.AppendAllText(WardrobePaths.ExportFile, section + "\n", new UTF8Encoding(false));
            return title;
        }

        public static string FromEquipped(MetaManager meta, string title, string category, string note)
        {
            return Build(meta, title, category, note, meta.cosmeticEquipped, meta.colorsEquipped);
        }

        public static string FromSlot(MetaManager meta, int slot, string title, string category, string note)
        {
            if (slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return "";
            }
            return Build(meta, title, category, note, meta.cosmeticPresets[slot], meta.colorPresets[slot]);
        }

        private static string Build(MetaManager meta, string title, string category, string note, IList<int> cosmetics, IList<int> colors)
        {
            var pieces = new List<KeyValuePair<string, string>>();
            var used = new bool[Slots.Count];
            foreach (int index in cosmetics)
            {
                if (index < 0 || index >= meta.cosmeticAssets.Count || !meta.cosmeticAssets[index])
                {
                    continue;
                }
                CosmeticAsset asset = meta.cosmeticAssets[index];
                used[(int)asset.type] = true;
                pieces.Add(new KeyValuePair<string, string>(Slots.Key(asset.type), asset.assetName.Trim()));
            }
            // A colour line for every slot that is wearing something, and for the bare body parts
            // the skin tints, which have a colour whether or not anything covers them.
            for (int i = 0; i < colors.Count && i < Slots.Count; i++)
            {
                var type = (SemiFunc.CosmeticType)i;
                if (!used[i] && !Slots.IsMesh(type))
                {
                    continue;
                }
                pieces.Add(new KeyValuePair<string, string>("color." + Slots.Key(type), Palette.Name(meta, colors[i])));
            }
            return LookSection.Text(title, category, note, pieces);
        }
    }
}
