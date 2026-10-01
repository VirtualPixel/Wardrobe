using System.Collections.Generic;

namespace Wardrobe.Models
{
    // One [section] of presets.cfg after every name has been resolved against the game's asset list.
    internal sealed class PresetDefinition
    {
        public string Name = "";
        public string Category = "";
        public string Note = "";
        // Empty for presets.cfg, the owning plugin's GUID for a registered pack.
        public string Source = "";
        // A pack's look: the name its file gives it. Differs from Name when that name is one of
        // yours and the pack's look is shown as "<name> (<category>)" instead.
        public string PackName = "";
        // The hash of the shipped look this pack look stands in for while the pack is registered.
        public string? Hides;
        public List<int> Cosmetics = new List<int>();
        // One colour per cosmetic slot, sized by PresetLibrary when it reads the section.
        public int[] Colors = System.Array.Empty<int>();
        public string Hash = "";
        // A shipped look you rewrote in presets.cfg: your version, which the slot wears instead.
        public PresetDefinition? Edit;
        public List<string> Problems = new List<string>();
    }
}
