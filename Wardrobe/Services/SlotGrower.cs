using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    // Keeps a couple of rows of empty preset slots available, growing the lists in place.
    // REPOLib mirrors the preset list with its own per-slot list of unknown asset ids, so
    // that one is grown too or its save patch indexes past the end.
    internal static class SlotGrower
    {
        public const int Columns = 7;
        public const int MaxSlots = 700;

        public static bool Grew { get; private set; }

        public static void EnsureSpare(MetaManager meta, int spare)
        {
            PresetNames? names = PresetNames.Get();
            int wanted = Wanted(meta.cosmeticPresets.Count, i => IsFree(PresetInfo.IsEmpty(meta, i), names != null && names.GetName(i).Length > 0), spare);
            if (wanted <= meta.cosmeticPresets.Count)
            {
                return;
            }
            Grow(meta, wanted);
        }

        // A named slot with nothing in it is a look waiting to be put back, not room for a new one.
        internal static bool IsFree(bool empty, bool named) => empty && !named;

        // How many slots the list needs so that spare of them are free, in whole rows.
        internal static int Wanted(int count, Func<int, bool> free, int spare)
        {
            int taken = 0;
            for (int i = 0; i < count; i++)
            {
                if (!free(i))
                {
                    taken++;
                }
            }
            int wanted = (taken + spare + Columns - 1) / Columns * Columns;
            return Math.Min(wanted, MaxSlots);
        }

        // The slot count the game starts with, before the save loads.
        // The save is shared by every profile and the config is not, so a profile set lower than the
        // save loads it short and writes it back short. Every slot names.txt has a name for counts.
        internal static int StartCount(int configured, IEnumerable<string> namesLines)
        {
            int count = configured;
            bool plain = false;
            foreach (string raw in namesLines)
            {
                string line = raw.Trim();
                if (line.StartsWith("["))
                {
                    plain = line.IndexOf('.') < 0;
                    continue;
                }
                int eq = line.IndexOf('=');
                if (plain && eq > 0 && int.TryParse(line.Substring(0, eq).Trim(), out int slot))
                {
                    count = Math.Max(count, slot + 1);
                }
            }
            return Math.Max(28, Math.Min(count, MaxSlots));
        }

        private static void Grow(MetaManager meta, int total)
        {
            int before = meta.cosmeticPresets.Count;
            while (meta.cosmeticPresets.Count < total)
            {
                meta.cosmeticPresets.Add(new List<int>());
            }
            while (meta.colorPresets.Count < total)
            {
                meta.colorPresets.Add(new List<int>());
            }
            meta.presetSlots = total;
            GrowRepoLibMirror(total);

            if (PluginConfig.PresetSlots.Value < total)
            {
                // So the next launch allocates the same count before the save loads.
                PluginConfig.PresetSlots.Value = total;
            }
            Grew = true;
            Log.Always("Wardrobe: preset slots grown from " + before + " to " + total + ".");
        }

        private static void GrowRepoLibMirror(int total)
        {
            try
            {
                Type? patch = Type.GetType("REPOLib.Patches.MetaManagerPatch, REPOLib");
                FieldInfo? field = patch?.GetField("missingCosmeticPresets", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field?.GetValue(null) is List<List<string>> mirror)
                {
                    while (mirror.Count < total)
                    {
                        mirror.Add(new List<string>());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Always("Wardrobe: could not grow REPOLib's preset mirror, more slots wait for the next launch. " + ex.Message);
            }
        }
    }
}
