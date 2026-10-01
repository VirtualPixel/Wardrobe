using System;
using System.Collections.Generic;
using System.Text;

namespace Wardrobe.Services
{
    // A whole look as one string: every slot's cosmetic, every colour and the name, packed small
    // enough to paste into Discord. Nothing here knows about the game, so a code off the internet
    // is read by exactly the same rules as one this machine wrote.
    internal sealed class OutfitCode
    {
        public const int MaxNameBytes = 24;

        public string Name = "";

        // The build the code was written on, as the game's own version title. Indices only mean
        // the same thing on the same build, so the reader is told which one and decides himself.
        public string GameVersion = "";

        // Indices into MetaManager.cosmeticAssets.
        public readonly List<int> Cosmetics = new List<int>();

        // Slot index -> palette index, for the slots the look says something about.
        public readonly SortedDictionary<int, int> Colors = new SortedDictionary<int, int>();

        // An empty version on either side is not a mismatch: it is a code from a build that did
        // not say, or a reader that cannot tell.
        public bool SameGame(string gameVersion)
        {
            return GameVersion.Length == 0 || string.IsNullOrEmpty(gameVersion)
                || string.Equals(GameVersion, gameVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    // A folder of looks as one string: its name, the build it came off, and every look in it the
    // way an outfit code carries one.
    internal sealed class FolderCode
    {
        public string Name = "";
        public string GameVersion = "";
        public readonly List<OutfitCode> Looks = new List<OutfitCode>();

        public bool SameGame(string gameVersion)
        {
            return GameVersion.Length == 0 || string.IsNullOrEmpty(gameVersion)
                || string.Equals(GameVersion, gameVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    // The bytes behind an outfit code, and the base32 they travel in.
    //
    //   1   format, 1 today
    //   ... the look, as CodeText.WriteLook lays it out
    //   1   game version length in bytes
    //   n   the game version, UTF-8
    //   1   CRC-8 of everything above it
    //
    // Then Crockford base32 (no I, L, O or U, so nothing reads as something else over voice or in
    // a screenshot), "WD1-" in front and a dash every four characters. Dashes, spaces and case are
    // thrown away on the way back in.
    internal static class OutfitCodec
    {
        public const string Tag = "WD1";
        public const string Prefix = Tag + "-";

        private const byte Format = 1;

        public static string Encode(OutfitCode look)
        {
            var bytes = new List<byte> { Format };
            CodeText.WriteLook(bytes, look);
            CodeText.WriteText(bytes, look.GameVersion, CodeText.MaxVersionBytes);
            bytes.Add(CodeText.Crc8(bytes, bytes.Count));
            return CodeText.Group(Prefix, bytes);
        }

        public static bool TryDecode(string text, out OutfitCode? look, out string problem)
        {
            look = null;
            if (!CodeText.TryBytes(text, Tag, "an outfit code", out List<byte> bytes, out problem))
            {
                return false;
            }
            if (bytes.Count < 2)
            {
                problem = "the code is too short to be one";
                return false;
            }
            if (CodeText.Crc8(bytes, bytes.Count - 1) != bytes[bytes.Count - 1])
            {
                problem = CodeText.Damaged;
                return false;
            }
            if (bytes[0] != Format)
            {
                problem = CodeText.Newer;
                return false;
            }

            int at = 1;
            int end = bytes.Count - 1;
            if (!CodeText.ReadLook(bytes, end, ref at, out OutfitCode code)
                || !CodeText.ReadText(bytes, end, ref at, out code.GameVersion))
            {
                problem = CodeText.CutShort;
                return false;
            }
            look = code;
            return true;
        }
    }

    // A whole folder behind one code.
    //
    //   1   format, 2 today
    //   1   folder name length in bytes, 48 at most
    //   n   the folder name, UTF-8
    //   1   game version length in bytes
    //   n   the game version, UTF-8
    //   1   how many looks follow, 255 at most
    //   ... each look, as CodeText.WriteLook lays it out
    //   2   CRC-16 of everything above it, high byte first
    //
    // "WD2-" in front and the same base32 as a look code. A folder code runs to hundreds of
    // characters, which is why it gets two bytes of checksum where a look gets one.
    internal static class FolderCodec
    {
        public const string Tag = "WD2";
        public const string Prefix = Tag + "-";
        public const int MaxLooks = 255;
        public const int MaxNameBytes = 48;

        private const byte Format = 2;

        public static string Encode(FolderCode folder)
        {
            var bytes = new List<byte> { Format };
            CodeText.WriteText(bytes, folder.Name, MaxNameBytes);
            CodeText.WriteText(bytes, folder.GameVersion, CodeText.MaxVersionBytes);
            int count = Math.Min(folder.Looks.Count, MaxLooks);
            bytes.Add((byte)count);
            for (int i = 0; i < count; i++)
            {
                CodeText.WriteLook(bytes, folder.Looks[i]);
            }
            ushort crc = CodeText.Crc16(bytes, bytes.Count);
            bytes.Add((byte)(crc >> 8));
            bytes.Add((byte)crc);
            return CodeText.Group(Prefix, bytes);
        }

        public static bool TryDecode(string text, out FolderCode? folder, out string problem)
        {
            folder = null;
            if (!CodeText.TryBytes(text, Tag, "a folder code", out List<byte> bytes, out problem))
            {
                return false;
            }
            if (bytes.Count < 3)
            {
                problem = "the code is too short to be one";
                return false;
            }
            int end = bytes.Count - 2;
            if (CodeText.Crc16(bytes, end) != (bytes[end] << 8 | bytes[end + 1]))
            {
                problem = CodeText.Damaged;
                return false;
            }
            if (bytes[0] != Format)
            {
                problem = CodeText.Newer;
                return false;
            }

            var code = new FolderCode();
            int at = 1;
            if (!CodeText.ReadText(bytes, end, ref at, out code.Name)
                || !CodeText.ReadText(bytes, end, ref at, out code.GameVersion)
                || at >= end)
            {
                problem = CodeText.CutShort;
                return false;
            }
            int count = bytes[at++];
            for (int i = 0; i < count; i++)
            {
                if (!CodeText.ReadLook(bytes, end, ref at, out OutfitCode look))
                {
                    problem = CodeText.CutShort;
                    return false;
                }
                look.GameVersion = code.GameVersion;
                code.Looks.Add(look);
            }
            if (at != end)
            {
                problem = "the code has more on the end than a folder holds";
                return false;
            }
            folder = code;
            return true;
        }
    }

    // What both kinds of code are made of: the look layout, the base32 and the checksums.
    //
    // One look:
    //   1   how many cosmetics follow
    //   n   one varint per cosmetic index, ascending
    //   1   how many bytes of colour mask follow
    //   m   the mask, bit s set when slot s carries a colour
    //   k   one palette index per set bit, in slot order
    //   1   name length in bytes, 24 at most
    //   n   the name, UTF-8
    internal static class CodeText
    {
        public const int MaxVersionBytes = 32;
        public const string CutShort = "the code is cut short";
        public const string Damaged = "the code did not come through in one piece, copy the whole thing again";
        public const string Newer = "that code is from a newer Wardrobe than this one";

        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const int GroupSize = 4;
        private const int MaxCosmetics = 255;

        public static void WriteLook(List<byte> bytes, OutfitCode look)
        {
            var indices = new List<int>();
            foreach (int index in look.Cosmetics)
            {
                // A look cannot wear the same thing twice, and the reader would fold a repeat into
                // one anyway. Ascending order also makes the same look always the same code.
                if (index >= 0 && !indices.Contains(index))
                {
                    indices.Add(index);
                }
            }
            indices.Sort();
            if (indices.Count > MaxCosmetics)
            {
                indices.RemoveRange(MaxCosmetics, indices.Count - MaxCosmetics);
            }
            bytes.Add((byte)indices.Count);
            foreach (int index in indices)
            {
                WriteVarint(bytes, index);
            }

            var colors = new List<KeyValuePair<int, int>>();
            foreach (var pair in look.Colors)
            {
                if (pair.Key >= 0 && pair.Key <= byte.MaxValue && pair.Value >= 0 && pair.Value <= byte.MaxValue)
                {
                    colors.Add(pair);
                }
            }
            int maskBytes = colors.Count == 0 ? 0 : colors[colors.Count - 1].Key / 8 + 1;
            var mask = new byte[maskBytes];
            foreach (var pair in colors)
            {
                mask[pair.Key / 8] |= (byte)(1 << pair.Key % 8);
            }
            bytes.Add((byte)maskBytes);
            bytes.AddRange(mask);
            foreach (var pair in colors)
            {
                bytes.Add((byte)pair.Value);
            }

            WriteText(bytes, look.Name, OutfitCode.MaxNameBytes);
        }

        public static bool ReadLook(List<byte> bytes, int end, ref int at, out OutfitCode look)
        {
            look = new OutfitCode();
            if (at >= end)
            {
                return false;
            }
            int count = bytes[at++];
            for (int i = 0; i < count; i++)
            {
                if (!ReadVarint(bytes, end, ref at, out int index))
                {
                    return false;
                }
                look.Cosmetics.Add(index);
            }

            if (at >= end)
            {
                return false;
            }
            int maskBytes = bytes[at++];
            if (at + maskBytes > end)
            {
                return false;
            }
            var slots = new List<int>();
            for (int i = 0; i < maskBytes; i++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((bytes[at + i] & 1 << bit) != 0)
                    {
                        slots.Add(i * 8 + bit);
                    }
                }
            }
            at += maskBytes;
            if (at + slots.Count > end)
            {
                return false;
            }
            foreach (int slot in slots)
            {
                look.Colors[slot] = bytes[at++];
            }
            return ReadText(bytes, end, ref at, out look.Name);
        }

        // Everything that is not a base32 character in its own right. Crockford reads O as zero and
        // I and L as one, because that is what people type when they read a code off a screen.
        private static int Value(char c)
        {
            switch (c)
            {
                case 'O':
                    return 0;
                case 'I':
                case 'L':
                    return 1;
                default:
                    return Alphabet.IndexOf(c);
            }
        }

        // The code with its dashes, spaces and case gone, which is how it is compared and read.
        public static string Squash(string text)
        {
            var clean = new StringBuilder((text ?? "").Length);
            foreach (char c in text ?? "")
            {
                if (c == '-' || char.IsWhiteSpace(c))
                {
                    continue;
                }
                clean.Append(char.ToUpperInvariant(c));
            }
            return clean.ToString();
        }

        public static bool TryBytes(string text, string tag, string what, out List<byte> bytes, out string problem)
        {
            bytes = new List<byte>();
            problem = "";
            string body = Squash(text);
            if (!body.StartsWith(tag, StringComparison.Ordinal))
            {
                problem = "that is not " + what + ", they start with " + tag + "-";
                return false;
            }

            int acc = 0;
            int bits = 0;
            for (int i = tag.Length; i < body.Length; i++)
            {
                int value = Value(body[i]);
                if (value < 0)
                {
                    problem = "'" + body[i] + "' is not part of " + what;
                    return false;
                }
                acc = acc << 5 | value;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    bytes.Add((byte)(acc >> bits & 0xFF));
                }
            }
            return true;
        }

        public static void WriteText(List<byte> bytes, string text, int max)
        {
            byte[] utf8 = new UTF8Encoding(false).GetBytes(text ?? "");
            int cut = utf8.Length;
            if (cut > max)
            {
                // Never slice a character in half: back up over the tail bytes of a wide one.
                cut = max;
                while (cut > 0 && (utf8[cut] & 0xC0) == 0x80)
                {
                    cut--;
                }
            }
            bytes.Add((byte)cut);
            for (int i = 0; i < cut; i++)
            {
                bytes.Add(utf8[i]);
            }
        }

        public static bool ReadText(List<byte> bytes, int end, ref int at, out string text)
        {
            text = "";
            if (at >= end)
            {
                return false;
            }
            int length = bytes[at++];
            if (at + length > end)
            {
                return false;
            }
            text = new UTF8Encoding(false).GetString(bytes.GetRange(at, length).ToArray());
            at += length;
            return true;
        }

        private static void WriteVarint(List<byte> bytes, int value)
        {
            uint left = (uint)value;
            while (left >= 0x80)
            {
                bytes.Add((byte)(left | 0x80));
                left >>= 7;
            }
            bytes.Add((byte)left);
        }

        private static bool ReadVarint(List<byte> bytes, int end, ref int at, out int value)
        {
            value = 0;
            int shift = 0;
            while (at < end)
            {
                byte b = bytes[at++];
                value |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return true;
                }
                shift += 7;
                if (shift > 28)
                {
                    return false;
                }
            }
            return false;
        }

        // CRC-8, polynomial 0x07. One byte is enough to catch a look code that lost a character on
        // the way through a chat window, which is the only damage that happens to these.
        public static byte Crc8(List<byte> bytes, int count)
        {
            int crc = 0;
            for (int i = 0; i < count; i++)
            {
                crc ^= bytes[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x80) != 0 ? crc << 1 ^ 0x07 : crc << 1;
                    crc &= 0xFF;
                }
            }
            return (byte)crc;
        }

        // CRC-16/CCITT-FALSE: polynomial 0x1021, starting from 0xFFFF.
        public static ushort Crc16(List<byte> bytes, int count)
        {
            int crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                crc ^= bytes[i] << 8;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0 ? crc << 1 ^ 0x1021 : crc << 1;
                    crc &= 0xFFFF;
                }
            }
            return (ushort)crc;
        }

        public static string Group(string prefix, List<byte> bytes)
        {
            var sb = new StringBuilder((bytes.Count * 8 + 4) / 5);
            int acc = 0;
            int bits = 0;
            foreach (byte b in bytes)
            {
                acc = acc << 8 | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    sb.Append(Alphabet[acc >> bits & 0x1F]);
                }
            }
            if (bits > 0)
            {
                sb.Append(Alphabet[acc << 5 - bits & 0x1F]);
            }
            string body = sb.ToString();

            var grouped = new StringBuilder(prefix, prefix.Length + body.Length + body.Length / GroupSize + 1);
            for (int i = 0; i < body.Length; i++)
            {
                if (i > 0 && i % GroupSize == 0)
                {
                    grouped.Append('-');
                }
                grouped.Append(body[i]);
            }
            return grouped.ToString();
        }
    }
}
