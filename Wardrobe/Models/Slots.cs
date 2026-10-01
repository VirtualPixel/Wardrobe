using System;
using System.Collections.Generic;

namespace Wardrobe.Models
{
    // Slot vocabulary shared by the preset library, the exporter and the catalogue.
    internal static class Slots
    {
        public static readonly int Count = Enum.GetValues(typeof(SemiFunc.CosmeticType)).Length;

        private static readonly Dictionary<string, SemiFunc.CosmeticType[]> aliases = BuildAliases();

        public static bool TryParse(string key, out SemiFunc.CosmeticType[] types)
        {
            return aliases.TryGetValue(Normalize(key), out types);
        }

        public static string Key(SemiFunc.CosmeticType type) => type.ToString().ToLowerInvariant();

        // Bare body parts: the head, body, limbs, grabber and eyelids the skin colour tints.
        public static bool IsMesh(SemiFunc.CosmeticType type)
        {
            switch (type)
            {
                case SemiFunc.CosmeticType.HeadTopMesh:
                case SemiFunc.CosmeticType.HeadBottomMesh:
                case SemiFunc.CosmeticType.BodyTopMesh:
                case SemiFunc.CosmeticType.BodyBottomMesh:
                case SemiFunc.CosmeticType.ArmRightMesh:
                case SemiFunc.CosmeticType.ArmLeftMesh:
                case SemiFunc.CosmeticType.LegRightMesh:
                case SemiFunc.CosmeticType.LegLeftMesh:
                case SemiFunc.CosmeticType.GrabberMesh:
                case SemiFunc.CosmeticType.EyeLidRightMesh:
                case SemiFunc.CosmeticType.EyeLidLeftMesh:
                    return true;
                default:
                    return false;
            }
        }

        private static string Normalize(string key)
        {
            return key.Trim().Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
        }

        private static Dictionary<string, SemiFunc.CosmeticType[]> BuildAliases()
        {
            var map = new Dictionary<string, SemiFunc.CosmeticType[]>();
            foreach (SemiFunc.CosmeticType type in Enum.GetValues(typeof(SemiFunc.CosmeticType)))
            {
                map[Key(type)] = new[] { type };
            }

            void Pair(string name, SemiFunc.CosmeticType right, SemiFunc.CosmeticType left)
            {
                map[name] = new[] { right, left };
            }

            Pair("arms", SemiFunc.CosmeticType.ArmRight, SemiFunc.CosmeticType.ArmLeft);
            Pair("legs", SemiFunc.CosmeticType.LegRight, SemiFunc.CosmeticType.LegLeft);
            Pair("feet", SemiFunc.CosmeticType.FootRight, SemiFunc.CosmeticType.FootLeft);
            Pair("armsmesh", SemiFunc.CosmeticType.ArmRightMesh, SemiFunc.CosmeticType.ArmLeftMesh);
            Pair("legsmesh", SemiFunc.CosmeticType.LegRightMesh, SemiFunc.CosmeticType.LegLeftMesh);
            Pair("eyelids", SemiFunc.CosmeticType.EyeLidRightMesh, SemiFunc.CosmeticType.EyeLidLeftMesh);
            Pair("bodyoverlay", SemiFunc.CosmeticType.BodyTopOverlay, SemiFunc.CosmeticType.BodyBottomOverlay);
            Pair("headoverlay", SemiFunc.CosmeticType.HeadTopOverlay, SemiFunc.CosmeticType.HeadBottomOverlay);
            Pair("armsoverlay", SemiFunc.CosmeticType.ArmRightOverlay, SemiFunc.CosmeticType.ArmLeftOverlay);
            Pair("legsoverlay", SemiFunc.CosmeticType.LegRightOverlay, SemiFunc.CosmeticType.LegLeftOverlay);
            return map;
        }
    }
}
