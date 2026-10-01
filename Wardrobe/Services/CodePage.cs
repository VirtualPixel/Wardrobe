using MenuLib;
using MenuLib.MonoBehaviors;
using UnityEngine;

namespace Wardrobe.Services
{
    // The Codes entry in the folder row: copy the code for what you have on or for a whole folder,
    // or paste one somebody sent you, see what it is, and keep it. Nothing on this page puts a
    // cosmetic on.
    internal static class CodePage
    {
        private static MenuPageCosmetics? host;

        public static void Open(MenuPageCosmetics page)
        {
            if (!MetaManager.instance)
            {
                return;
            }
            host = page;
            Render("");
        }

        private static void Render(string line)
        {
            MetaManager meta = MetaManager.instance;
            REPOPopupPage popup = WardrobePopup.Open("Codes", host, 2f);
            if (line.Length > 0)
            {
                PopupUi.Label(popup, line);
            }
            PopupUi.Label(popup, "A code is a look, or a whole folder of them, on one line. Send it to somebody with Wardrobe and they have it too.", line.Length > 0 ? 4f : 0f);

            PopupUi.Button(popup, "Copy the code for what I am wearing", () =>
            {
                string worn = LookCodes.WornName();
                Render(LookCodes.Copied(LookCodes.FromEquipped(meta, worn), worn));
            }, 8f);

            PopupUi.Button(popup, "Copy a folder's code", RenderFolders);
            PopupUi.Button(popup, "Paste a code", () => RenderPaste(FromClipboard(), "", ""));

            if (LookCodes.HasPending)
            {
                PopupUi.Button(popup, "Save " + LookCodes.PendingLine, () =>
                {
                    LookCodes.SavePending();
                    Render("Saved into " + LookCodes.SharedCategory + ".");
                }, 8f);
            }

            PopupUi.Button(popup, "Close", WardrobePopup.Close, 8f);
            WardrobePopup.Show(popup);
        }

        private static void RenderFolders()
        {
            MetaManager meta = MetaManager.instance;
            REPOPopupPage popup = WardrobePopup.Open("Copy a folder's code", host, 2f);
            PopupUi.Label(popup, "Every look in the folder goes into one code, under the folder's name.");
            foreach (PresetGroup group in LookCatalog.Groups(meta, PresetNames.Get()))
            {
                string title = group.Title;
                PopupUi.Button(popup, title + " (" + group.Slots.Count + ")", () =>
                {
                    string code = LookCodes.FromFolder(meta, title, out int count);
                    if (code.Length > 0)
                    {
                        LookCodes.Copied(code, title);
                        Render("Copied the code for " + title + ", " + LookCodes.Looks(count) + ".");
                    }
                });
            }
            PopupUi.Button(popup, "Back", () => Render(""), 8f);
            WardrobePopup.Show(popup);
        }

        // An empty folder box means "wherever the code says": a folder code's own name, or Shared
        // for a single look.
        private static void RenderPaste(string text, string into, string line)
        {
            string typed = text;
            string folder = into;
            REPOPopupPage popup = WardrobePopup.Open("Paste a code", host, 2f);
            if (line.Length > 0)
            {
                PopupUi.Label(popup, line);
            }
            PopupUi.Label(popup, "The box starts on whatever is on your clipboard. Dashes and capitals do not matter.", line.Length > 0 ? 4f : 0f);

            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Code", value => typed = value ?? "", parent, default, false, OutfitCodec.Prefix + " or " + FolderCodec.Prefix, text).rectTransform);
            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Into folder", value => folder = value ?? "", parent, default, false, "the code's own folder", into).rectTransform);

            PopupUi.Button(popup, "Read it", () => RenderRead(typed, folder), 6f);
            PopupUi.Button(popup, "Back", () => Render(""), 6f);
            WardrobePopup.Show(popup);
        }

        // What the code turns out to be, before anything is written: the name, how much of it this
        // game knows, and whether it was made on this build at all.
        private static void RenderRead(string text, string into)
        {
            switch (ShareCodes.Read(text, out OutfitCode? look, out FolderCode? folder, out string problem))
            {
                case ShareCodes.Kind.Look:
                    RenderLook(text, into, look!);
                    return;
                case ShareCodes.Kind.Folder:
                    RenderFolder(text, into, folder!);
                    return;
                default:
                    RenderPaste(text, into, "That did not read: " + problem + ".");
                    return;
            }
        }

        private static void RenderLook(string text, string into, OutfitCode read)
        {
            MetaManager meta = MetaManager.instance;
            string target = into.Trim().Length > 0 ? into : LookCodes.SharedCategory;
            LookCodes.ToSection(meta, read, target, "", out int missing);
            int known = read.Cosmetics.Count - missing;

            REPOPopupPage popup = WardrobePopup.Open(LookCodes.Title(read), host, 2f);
            PopupUi.Label(popup, known + (known == 1 ? " piece" : " pieces") + " and " + read.Colors.Count + " colours.");
            if (missing > 0)
            {
                PopupUi.Label(popup, missing + (missing == 1 ? " piece is" : " pieces are") + " not in your game and will be left out. That usually means a mod the sender has and you do not.", 4f);
            }
            if (!read.SameGame(LookCodes.GameVersion))
            {
                PopupUi.Label(popup, VersionWarning(read.GameVersion), 4f);
            }
            if (known == 0)
            {
                PopupUi.Label(popup, "There is nothing in it this game can wear.", 6f);
                PopupUi.Button(popup, "Back", () => RenderPaste(text, into, ""), 10f);
                WardrobePopup.Show(popup);
                return;
            }

            string folder = target;
            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Into folder", value => folder = value ?? "", parent, default, false, "any folder name", target).rectTransform, 6f);
            PopupUi.Button(popup, "Save it", () =>
            {
                if (LookCodes.Save(meta, read, folder, "", out string landed))
                {
                    Finish(landed);
                }
                else
                {
                    RenderPaste(text, folder, "There is nothing in that one this game can wear.");
                }
            }, 6f);
            PopupUi.Button(popup, "Back", () => RenderPaste(text, into, ""), 6f);
            WardrobePopup.Show(popup);
        }

        private static void RenderFolder(string text, string into, FolderCode read)
        {
            MetaManager meta = MetaManager.instance;
            string target = into.Trim().Length > 0 ? into : read.Name;
            int wearable = 0;
            int partial = 0;
            foreach (OutfitCode look in read.Looks)
            {
                LookCodes.ToSection(meta, look, target, "", out int missing);
                if (missing < look.Cosmetics.Count)
                {
                    wearable++;
                }
                if (missing > 0)
                {
                    partial++;
                }
            }

            REPOPopupPage popup = WardrobePopup.Open(read.Name.Length > 0 ? read.Name : "A folder", host, 2f);
            PopupUi.Label(popup, LookCodes.Looks(read.Looks.Count) + " in this folder.");
            if (partial > 0)
            {
                PopupUi.Label(popup, LookCodes.Looks(partial) + " use pieces your game does not have, and those pieces will be left out. That usually means a mod the sender has and you do not.", 4f);
            }
            if (!read.SameGame(LookCodes.GameVersion))
            {
                PopupUi.Label(popup, VersionWarning(read.GameVersion), 4f);
            }
            if (wearable == 0)
            {
                PopupUi.Label(popup, "There is nothing in it this game can wear.", 6f);
                PopupUi.Button(popup, "Back", () => RenderPaste(text, into, ""), 10f);
                WardrobePopup.Show(popup);
                return;
            }

            string folder = target;
            PopupUi.Label(popup, "Looks you already have in that folder are skipped, and a name you already use gets a number.", 6f);
            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Into folder", value => folder = value ?? "", parent, default, false, "any folder name", target).rectTransform, 6f);
            PopupUi.Button(popup, "Add them", () =>
            {
                if (LookCodes.SaveFolder(meta, read, folder, out string landed))
                {
                    Finish(landed);
                }
                else
                {
                    RenderPaste(text, folder, landed);
                }
            }, 6f);
            PopupUi.Button(popup, "Back", () => RenderPaste(text, into, ""), 6f);
            WardrobePopup.Show(popup);
        }

        private static string VersionWarning(string made)
        {
            return "This was made on " + made + " and you are on " + LookCodes.GameVersion
                + ". The pieces are numbered per build, so some of them may come out as the wrong item. Worth a look before you wear it.";
        }

        private static void Finish(string landed)
        {
            WardrobePopup.Close();
            if (host && host!.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets)
            {
                ScrollKeeper.Refresh(host);
            }
            PopupUi.Notice(landed);
        }

        private static string FromClipboard()
        {
            return ShareCodes.Find(GUIUtility.systemCopyBuffer ?? "");
        }
    }
}
