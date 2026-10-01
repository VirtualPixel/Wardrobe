using System;
using System.IO;
using System.Text;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // Writes Wardrobe/catalogue.txt: the palette with hex codes and every cosmetic by slot, marked unlocked or not.
    internal static class CatalogueWriter
    {
        public static void Write(MetaManager meta)
        {
            WardrobePaths.EnsureRoot();
            var sb = new StringBuilder();
            sb.Append("Wardrobe catalogue, written on launch. Names here are what presets.cfg expects.\n");
            sb.Append("[x] unlocked, [ ] still locked. Written ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(".\n\n");

            sb.Append("## Palette (colour names)\n");
            for (int i = 0; i < meta.colors.Count; i++)
            {
                sb.Append(i.ToString().PadLeft(3)).Append("  ").Append(Palette.Hex(meta, i)).Append("  ").Append(Palette.Name(meta, i)).Append('\n');
            }

            foreach (SemiFunc.CosmeticType type in Enum.GetValues(typeof(SemiFunc.CosmeticType)))
            {
                sb.Append("\n## ").Append(Slots.Key(type)).Append('\n');
                for (int i = 0; i < meta.cosmeticAssets.Count; i++)
                {
                    CosmeticAsset asset = meta.cosmeticAssets[i];
                    if (!asset || asset.type != type)
                    {
                        continue;
                    }
                    bool unlocked = meta.cosmeticUnlocks.Contains(i);
                    sb.Append(unlocked ? "[x] " : "[ ] ");
                    sb.Append(asset.assetName.Trim());
                    sb.Append("  (").Append(asset.rarity);
                    if (asset.defaultColor)
                    {
                        sb.Append(", default ").Append(Palette.Name(meta, meta.colors.IndexOf(asset.defaultColor)));
                    }
                    if (!asset.assetId.StartsWith("vanilla:"))
                    {
                        sb.Append(", modded ").Append(asset.assetId);
                    }
                    sb.Append(", #").Append(i).Append(")\n");
                }
            }

            File.WriteAllText(WardrobePaths.CatalogueFile, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
