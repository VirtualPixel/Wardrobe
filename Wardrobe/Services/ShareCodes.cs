using System;

namespace Wardrobe.Services
{
    // Every way a code comes in goes through here: the Codes page, "/outfit paste", a line in chat.
    // It works out which kind of code it is and hands it to that kind's reader.
    internal static class ShareCodes
    {
        public enum Kind
        {
            None,
            Look,
            Folder
        }

        public enum Command
        {
            None,
            Post,
            Paste
        }

        private const string ChatCommand = "/outfit";

        public static Kind Read(string text, out OutfitCode? look, out FolderCode? folder, out string problem)
        {
            look = null;
            folder = null;
            string body = CodeText.Squash(text);
            if (body.StartsWith(OutfitCodec.Tag, StringComparison.Ordinal))
            {
                return OutfitCodec.TryDecode(text, out look, out problem) ? Kind.Look : Kind.None;
            }
            if (body.StartsWith(FolderCodec.Tag, StringComparison.Ordinal))
            {
                return FolderCodec.TryDecode(text, out folder, out problem) ? Kind.Folder : Kind.None;
            }
            problem = body.Length > 2 && body.StartsWith("WD", StringComparison.Ordinal) && char.IsDigit(body[2])
                ? CodeText.Newer
                : "that is not a Wardrobe code, they start with " + OutfitCodec.Prefix + " or " + FolderCodec.Prefix;
            return Kind.None;
        }

        // The first code in a run of text: what was on the clipboard, or a chat line with words
        // around it. A code has no spaces in it, so it ends at the first one.
        public static string Find(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            for (int at = 0; at + 3 < text.Length; at++)
            {
                if (char.ToUpperInvariant(text[at]) != 'W' || char.ToUpperInvariant(text[at + 1]) != 'D'
                    || !char.IsDigit(text[at + 2]) || text[at + 3] != '-')
                {
                    continue;
                }
                int past = at;
                while (past < text.Length && !char.IsWhiteSpace(text[past]))
                {
                    past++;
                }
                return text.Substring(at, past - at);
            }
            return "";
        }

        // "/outfit" posts what you have on, "/outfit paste" keeps whatever code is on your clipboard.
        // Anything else is an ordinary chat line.
        public static Command ReadCommand(string typed)
        {
            string[] words = (typed ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !string.Equals(words[0], ChatCommand, StringComparison.OrdinalIgnoreCase))
            {
                return Command.None;
            }
            if (words.Length == 1)
            {
                return Command.Post;
            }
            return words.Length == 2 && string.Equals(words[1], "paste", StringComparison.OrdinalIgnoreCase)
                ? Command.Paste
                : Command.None;
        }
    }
}
