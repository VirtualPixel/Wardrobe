using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    // The in-game wheel. Your starred folders run across the top with the one you are in as the
    // header; left and right change it, up and down walk its looks, listed top to bottom with
    // their pictures. OFF comes first on the ring and puts your own outfit back (OwnLooks). A held arrow keeps going. Nothing goes on while you look. Let go and stay
    // put until the settle ring fills (or, with a modifier key set, let that up) and the look goes
    // on through the same path the Presets tab wears one with, so the room sees it. Enter puts it
    // on right away, Escape leaves you as you were.
    //
    // By default the bare arrows drive it. Smash & Grab used to own them for its own wheel; set
    // Wheel / Modifier (LeftAlt was the old default) if another mod still wants the arrows.
    internal static class LookWheel
    {
        private const float OpenSeconds = 0.18f;
        private const int Rows = WheelLayout.Rows;
        private const float RowStep = WheelLayout.RowStep;
        private const float RowsTop = WheelLayout.RowsTop;
        private const float HeaderY = WheelLayout.HeaderY;

        private static WheelModel? model;
        private static readonly List<string> ringNames = new List<string>();
        private static readonly List<List<string>> lookNames = new List<List<string>>();
        private static readonly KeyRepeat folderNext = new KeyRepeat();
        private static readonly KeyRepeat folderPrev = new KeyRepeat();
        private static readonly KeyRepeat lookNext = new KeyRepeat();
        private static readonly KeyRepeat lookPrev = new KeyRepeat();
        private static bool open;
        private static bool stepped;
        private static float settleAt;
        private static float bumpAt = -10f;
        private static float openAt;
        private static float closeAt = -10f;
        private static float ringAt;
        private static float listAt;
        private static int shownFolder = -1;
        private static int wantFolder = -1;
        private static int wantLook = -1;
        private static int wantFirst = -1;
        private static string hintLine = "";

        public static bool Open => open;

        // How to get at the wheel with the keys as they are set, for the menu's help lines.
        public static string HowToOpen()
        {
            KeyCode modifier = PluginConfig.WheelModifierKey.Value;
            return modifier == KeyCode.None ? "press an arrow key in game to browse it" : "hold " + modifier + " in game and the arrows browse it";
        }

        public static void Tick()
        {
            if (!Playable())
            {
                if (open)
                {
                    Close();
                }
                Draw();
                return;
            }

            // No modifier set: the arrows open the wheel themselves and the settle ring puts the look on.
            KeyCode modifier = PluginConfig.WheelModifierKey.Value;
            bool bare = modifier == KeyCode.None;
            if (!open)
            {
                bool opening = bare ? AnyStepKeyDown() : Input.GetKeyDown(modifier);
                if (!opening)
                {
                    Draw();
                    return;
                }
                Begin();
            }
            else
            {
                WheelAction action = WheelInput.Decide(Input.GetKeyDown(KeyCode.Escape), ConfirmDown(), !bare, !bare && Input.GetKey(modifier), stepped, false, false);
                if (action != WheelAction.None)
                {
                    Commit(action);
                    Draw();
                    return;
                }
            }

            // Escape is the wheel's while it is up. GameDirector reads the same press for the pause
            // menu, before or after this depending on the frame, so it is held off a little longer
            // than a frame for as long as the wheel is open.
            if (GameDirector.instance)
            {
                GameDirector.instance.SetDisableEscMenu(0.2f);
            }

            ReadKeys();
            if (open)
            {
                WheelAction action = WheelInput.Decide(false, false, false, false, stepped, Holding(), Time.unscaledTime >= settleAt);
                if (action != WheelAction.None)
                {
                    Commit(action);
                }
            }
            Draw();
        }

        // Any scene you actually play in, with nobody typing and no menu page in the way.
        private static bool Playable()
        {
            if (!MetaManager.instance || !RunManager.instance || !HUDCanvas.instance)
            {
                return false;
            }
            if (SemiFunc.MenuLevel() || !SemiFunc.NoTextInputsActive())
            {
                return false;
            }
            return !MenuManager.instance || !MenuManager.instance.currentMenuPage;
        }

        private static void ReadKeys()
        {
            if (model == null)
            {
                return;
            }
            if (Down(PluginConfig.WheelCopyKey.Value))
            {
                CopyOnTop();
                return;
            }
            float now = Time.unscaledTime;
            // All four are read every frame so each knows whether it is still held.
            bool next = Repeat(folderNext, PluginConfig.WheelFolderNextKey.Value, now);
            bool prev = Repeat(folderPrev, PluginConfig.WheelFolderPrevKey.Value, now);
            bool down = Repeat(lookNext, PluginConfig.WheelLookNextKey.Value, now);
            bool up = Repeat(lookPrev, PluginConfig.WheelLookPrevKey.Value, now);
            int folderStep = next ? 1 : prev ? -1 : 0;
            int lookStep = folderStep != 0 ? 0 : down ? 1 : up ? -1 : 0;
            if (Holding())
            {
                // The settle ring waits for the key to come up, however long the run.
                settleAt = now + PluginConfig.WheelSettleSeconds.Value;
            }
            if (folderStep == 0 && lookStep == 0)
            {
                return;
            }
            int folderBefore = model.FolderIndex;
            int lookBefore = model.LookIndex;
            model.TurnFolder(folderStep);
            model.WalkLook(lookStep);
            if (model.FolderIndex == folderBefore && model.LookIndex == lookBefore)
            {
                WheelSound.Play(WheelMove.Nothing);
                return;
            }
            WheelSound.Play(folderStep != 0 ? WheelMove.Folder : WheelMove.Look);
            stepped = true;
            bumpAt = now;
            settleAt = now + PluginConfig.WheelSettleSeconds.Value;
            Refresh();
        }

        private static bool Repeat(KeyRepeat key, KeyCode code, float now) => key.Step(code != KeyCode.None && Input.GetKey(code), now);

        private static bool Holding() => folderNext.Holding || folderPrev.Holding || lookNext.Holding || lookPrev.Holding;

        private static void CopyOnTop()
        {
            MetaManager meta = MetaManager.instance;
            if (model == null || model.Empty || model.Slot < 0)
            {
                return;
            }
            string code = LookCodes.FromSlot(meta, model.Slot, model.Name);
            if (code.Length > 0)
            {
                LookCodes.Copied(code, model.Name);
                hintLine = "copied " + LookCodes.Possessive(model.Name) + " code";
                bumpAt = Time.unscaledTime;
                WheelSound.Play(WheelMove.Copy);
            }
        }

        private static bool Down(KeyCode key) => key != KeyCode.None && Input.GetKeyDown(key);

        // Enter on the number pad counts as Enter.
        private static bool ConfirmDown()
        {
            KeyCode key = PluginConfig.WheelConfirmKey.Value;
            return Down(key) || (key == KeyCode.Return && Input.GetKeyDown(KeyCode.KeypadEnter));
        }

        private static bool AnyStepKeyDown()
        {
            return Down(PluginConfig.WheelFolderNextKey.Value) || Down(PluginConfig.WheelFolderPrevKey.Value)
                || Down(PluginConfig.WheelLookNextKey.Value) || Down(PluginConfig.WheelLookPrevKey.Value);
        }

        private static void Begin()
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            model = Build(meta, names);
            ringNames.Clear();
            lookNames.Clear();
            foreach (WheelFolder folder in model.Folders)
            {
                ringNames.Add(folder.Title.ToUpperInvariant());
                var upper = new List<string>(folder.Names.Count);
                foreach (string name in folder.Names)
                {
                    upper.Add(name.ToUpperInvariant());
                }
                lookNames.Add(upper);
            }
            open = true;
            stepped = false;
            openAt = Time.unscaledTime;
            closeAt = -10f;
            bumpAt = -10f;
            ringAt = model.FolderIndex;
            settleAt = float.MaxValue;
            shownFolder = -1;
            wantFolder = -1;
            folderNext.Reset();
            folderPrev.Reset();
            lookNext.Reset();
            lookPrev.Reset();
            WheelIcons.Reopened();
            Refresh();
        }

        // The ring is OFF, then the starred folders, A to Z, with the looks each holds in the
        // folder's own order. Looks another mod's filter turns down are left out, and so is a
        // folder they empty. OFF is never filtered.
        private static WheelModel Build(MetaManager meta, PresetNames? names)
        {
            var groups = new List<LookGroup>();
            foreach (PresetGroup group in LookCatalog.Groups(meta, names))
            {
                var look = new LookGroup { Title = group.Title };
                foreach (int slot in group.Slots)
                {
                    look.Slots.Add(slot);
                    look.Names.Add(PresetInfo.DisplayName(names, slot));
                }
                groups.Add(look);
            }
            var off = new WheelFolder { Title = "Off" };
            off.Slots.Add(WheelModel.OffSlot);
            off.Names.Add(OwnLooks.Label(meta));
            return new WheelModel(WheelLineup.Build(groups, Favourites.Get().All, WheelFilters.Shows), WornSlot(meta), off, OwnLooks.PictureSlot(meta, names));
        }

        // Everything on the wheel, rows it would open on first, for drawing ahead with it shut.
        public static void Ahead(MetaManager meta, List<int> into)
        {
            WheelModel lineup = Build(meta, PresetNames.Get());
            int count = lineup.Folders[lineup.FolderIndex].Slots.Count;
            IconPlan.Wanted(lineup, IconPlan.Window(count, lineup.LookIndex, Rows), Rows + 1, true, into);
        }

        // Which slot holds what you have on, so the wheel opens where you are instead of at the top.
        private static int WornSlot(MetaManager meta)
        {
            string? current = LookState.Name;
            return current == null ? -1 : LookCatalog.SlotOf(meta, PresetNames.Get(), current);
        }

        private static void Commit(WheelAction action)
        {
            WheelModel? picked = model;
            int slot = picked != null ? picked.Slot : -1;
            string name = picked != null ? picked.Name : "";
            bool take = WheelInput.PutsOn(action, stepped, slot);
            Close();
            if (!take)
            {
                WheelSound.Play(WheelMove.Cancel);
                return;
            }
            WheelSound.Play(WheelMove.PutOn);
            if (slot == WheelModel.OffSlot)
            {
                OwnLooks.PutBack(MetaManager.instance);
                Log.Info("Wardrobe: wheel took the look off.");
                return;
            }
            PresetEquipper.Equip(MetaManager.instance, slot, null, synced: true);
            Log.Info("Wardrobe: wheel put on '" + name + "' from slot " + slot + ".");
        }

        private static void Close()
        {
            open = false;
            stepped = false;
            closeAt = Time.unscaledTime;
            settleAt = float.MaxValue;
        }

        // ---- Drawing ----

        private sealed class Row
        {
            public RectTransform Rect = null!;
            public Image Icon = null!;
            public Image Blank = null!;
            public TextMeshProUGUI Name = null!;
            public int Look = -1;
            public int Slot = -1;
            public int Version = -1;
        }

        private static GameObject? root;
        private static CanvasGroup? group;
        private static RectTransform? spin;
        private static RectTransform? plate;
        private static TextMeshProUGUI? hint;
        private static Image? timer;
        private static RectTransform? hintRect;
        private static RectTransform? timerRect;
        private static readonly List<TextMeshProUGUI> ringText = new List<TextMeshProUGUI>();
        private static readonly List<Row> rows = new List<Row>();
        private static readonly List<int> iconOrder = new List<int>();
        private static readonly List<RingSpot> ringSpots = new List<RingSpot>();
        private static float lastUnit;

        // The two lines that only change when the pick does.
        private static void Refresh()
        {
            if (model == null)
            {
                hintLine = "";
                return;
            }
            // OFF on its own: nothing starred, or a filter left nothing.
            if (!model.HasLooks)
            {
                hintLine = WheelFilters.Count > 0
                    ? "star a folder on the Presets tab (another mod only shows some looks here)"
                    : "star a folder on the Presets tab to put it on the wheel";
                return;
            }
            hintLine = model.FolderCount > 1 ? "arrows browse" : "up and down browse";
            KeyCode confirm = PluginConfig.WheelConfirmKey.Value;
            if (confirm != KeyCode.None)
            {
                hintLine += ", " + KeyName(confirm) + " puts it on";
            }
            KeyCode copy = PluginConfig.WheelCopyKey.Value;
            if (copy != KeyCode.None)
            {
                hintLine += ", " + KeyName(copy) + " copies its code";
            }
            hintLine += ", Esc backs out";
        }

        private static string KeyName(KeyCode key) => key == KeyCode.Return ? "Enter" : key.ToString();

        private static void Draw()
        {
            float now = Time.unscaledTime;
            float fading = closeAt > 0f ? (now - closeAt) / OpenSeconds : 1f;
            if (!open && fading >= 1f)
            {
                if (root && root!.activeSelf)
                {
                    root.SetActive(false);
                }
                return;
            }
            if (!HUDCanvas.instance || !WheelHud.Font)
            {
                return;
            }
            if (!root)
            {
                BuildRoot();
                if (!root)
                {
                    return;
                }
                Refresh();
            }
            root!.SetActive(true);

            float t = open ? Mathf.Clamp01((now - openAt) / OpenSeconds) : Mathf.Clamp01(fading);
            float eval = t >= 1f ? 1f : WheelHud.Woosh(!open, t);
            group!.alpha = open ? eval : 1f - eval;
            float scale = open ? Mathf.LerpUnclamped(1.15f, 1f, eval) : Mathf.LerpUnclamped(1f, 1.15f, eval);
            spin!.localScale = new Vector3(scale, scale, 1f);

            // The canvas says how big a screen unit is, so the same numbers land in the same place
            // at 1080p and at 1440p.
            float unit = HUDCanvas.instance.rect.rect.height / 1080f;
            if (!Mathf.Approximately(unit, lastUnit))
            {
                lastUnit = unit;
                Resize(unit);
            }

            float since = now - bumpAt;
            float wobble = WheelHud.Spring(since, 0.3f, 5f, 0.2f);
            float shake = WheelHud.Spring(since, 20f, 10f, 0.3f) * unit;

            DrawRing(unit, wobble, shake);
            DrawRows(unit, wobble, shake);

            WheelHud.Set(hint!, hintLine, WheelLayout.HintSize * unit, WheelHud.Hint);
            float settle = PluginConfig.WheelSettleSeconds.Value;
            timer!.fillAmount = stepped && settle > 0f ? Mathf.Clamp01(1f - (settleAt - now) / settle) : 0f;
        }

        private static void Resize(float unit)
        {
            float ratio = WheelHud.LineRatio;
            plate!.sizeDelta = new Vector2(WheelLayout.PlateWidth * unit, WheelLayout.PlateHeight * unit);
            hintRect!.anchoredPosition = new Vector2(0f, WheelLayout.HintY * unit);
            hintRect.sizeDelta = new Vector2(WheelLayout.TextWidth * unit, WheelLayout.Box(WheelLayout.HintSize, ratio) * unit);
            timerRect!.sizeDelta = new Vector2(36f * unit, 36f * unit);
            timerRect.anchoredPosition = new Vector2(0f, WheelLayout.TimerY * unit);
        }

        // The folder on top is the header, its neighbours either side of it.
        private static void DrawRing(float unit, float wobble, float shake)
        {
            int count = model == null || model.Empty ? 0 : model.FolderCount;
            while (ringText.Count < WheelRing.Reach * 2 + 1)
            {
                TextMeshProUGUI text = WheelHud.Text(spin!, "Folder" + ringText.Count, WheelLayout.FolderSize, TextAlignmentOptions.Center);
                text.overflowMode = TextOverflowModes.Ellipsis;
                ringText.Add(text);
            }
            int on = count > 0 ? model!.FolderIndex : 0;
            // Turning off Z onto A is a jump, not a slide past every folder in between.
            if (Mathf.Abs(on - ringAt) > WheelRing.Reach + 1)
            {
                ringAt = on;
            }
            ringAt = Mathf.Lerp(ringAt, on, 20f * Time.unscaledDeltaTime);

            List<RingSpot> spots = WheelRing.Place(count, ringAt, ringSpots);
            for (int i = 0; i < ringText.Count; i++)
            {
                TextMeshProUGUI text = ringText[i];
                if (i >= spots.Count)
                {
                    if (text.gameObject.activeSelf)
                    {
                        text.gameObject.SetActive(false);
                    }
                    continue;
                }
                if (!text.gameObject.activeSelf)
                {
                    text.gameObject.SetActive(true);
                }
                RingSpot spot = spots[i];
                bool top = spot.Index == on;
                var rect = (RectTransform)text.transform;
                float size = top ? WheelLayout.HeaderSize : WheelLayout.FolderSize;
                rect.sizeDelta = new Vector2((top ? WheelLayout.HeaderWidth : WheelLayout.FolderWidth) * unit, WheelLayout.Box(size, WheelHud.LineRatio) * unit);
                rect.anchoredPosition = new Vector2(spot.X * unit, (HeaderY + spot.Y) * unit + (top ? shake : 0f));
                float grow = top ? 1f + wobble * 0.5f : 1f;
                rect.localScale = new Vector3(grow, grow, 1f);
                float alpha = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(spot.Away / (WheelRing.Reach + 0.4f)));
                Color colour = top ? WheelHud.Gold : Color.white;
                WheelHud.Set(text, ringNames[spot.Index], size * unit, new Color(colour.r, colour.g, colour.b, alpha));
            }
        }

        // The looks in the folder on top, top to bottom, each with its picture. A long folder
        // scrolls so the one you are on stays in the middle.
        private static void DrawRows(float unit, float wobble, float shake)
        {
            MetaManager meta = MetaManager.instance;
            WheelFolder? folder = model == null || model.Empty ? null : model.Folders[model.FolderIndex];
            int count = folder != null ? folder.Slots.Count : 0;
            int picked = folder != null ? model!.LookIndex : 0;
            float window = IconPlan.Window(count, picked, Rows);
            if (model != null && model.FolderIndex != shownFolder)
            {
                shownFolder = model.FolderIndex;
                listAt = window;
            }
            listAt = Mathf.Lerp(listAt, window, 18f * Time.unscaledDeltaTime);

            int first = Mathf.FloorToInt(listAt);
            if (model != null && !model.Empty && (model.FolderIndex != wantFolder || picked != wantLook || first != wantFirst))
            {
                wantFolder = model.FolderIndex;
                wantLook = picked;
                wantFirst = first;
                WheelIcons.Want(IconPlan.Wanted(model, first, rows.Count, false, iconOrder), IconPlan.OnScreen(model, first, rows.Count));
            }
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                int look = first + i;
                if (folder == null || look < 0 || look >= count)
                {
                    if (row.Rect.gameObject.activeSelf)
                    {
                        row.Rect.gameObject.SetActive(false);
                    }
                    row.Look = -1;
                    continue;
                }
                float at = look - listAt;
                float edge = Mathf.Clamp01(Mathf.Min(at + 0.6f, Rows - 0.4f - at));
                if (edge <= 0f)
                {
                    if (row.Rect.gameObject.activeSelf)
                    {
                        row.Rect.gameObject.SetActive(false);
                    }
                    continue;
                }
                if (!row.Rect.gameObject.activeSelf)
                {
                    row.Rect.gameObject.SetActive(true);
                }
                int slot = folder.Slots[look];
                // A picture can be swapped for a newer one under the row; a destroyed sprite draws white.
                if (row.Look != look || row.Slot != slot || row.Version != WheelIcons.Version || (row.Icon.enabled && !row.Icon.sprite))
                {
                    row.Look = look;
                    row.Slot = slot;
                    row.Version = WheelIcons.Version;
                    Sprite? sprite = meta ? WheelIcons.Get(meta, model!.Picture(slot)) : null;
                    row.Icon.sprite = sprite;
                    row.Icon.enabled = sprite;
                    row.Blank.enabled = !sprite;
                }

                bool on = look == picked;
                float grow = on ? 1f + wobble : 1f;
                row.Rect.anchoredPosition = new Vector2(0f, (RowsTop - at * RowStep) * unit + (on ? shake : 0f));
                row.Rect.localScale = new Vector3(grow, grow, 1f);
                float iconHeight = (on ? 60f : 52f) * unit;
                var iconRect = (RectTransform)row.Icon.transform;
                iconRect.sizeDelta = new Vector2(iconHeight * 0.5f, iconHeight);
                iconRect.anchoredPosition = new Vector2(WheelLayout.IconX * unit, 0f);
                var blankRect = (RectTransform)row.Blank.transform;
                blankRect.sizeDelta = new Vector2(iconHeight * 0.4f, iconHeight * 0.8f);
                blankRect.anchoredPosition = iconRect.anchoredPosition;
                var nameRect = (RectTransform)row.Name.transform;
                float size = on ? WheelLayout.LookOnSize : WheelLayout.LookSize;
                nameRect.sizeDelta = new Vector2(WheelLayout.LookWidth * unit, WheelLayout.Box(size, WheelHud.LineRatio) * unit);
                nameRect.anchoredPosition = new Vector2(WheelLayout.NameX * unit, 0f);

                row.Icon.color = new Color(1f, 1f, 1f, (on ? 1f : 0.55f) * edge);
                row.Blank.color = new Color(1f, 1f, 1f, 0.08f * edge);
                Color colour = on ? WheelHud.Gold : new Color(1f, 1f, 1f, 0.8f);
                WheelHud.Set(row.Name, lookNames[model!.FolderIndex][look], size * unit, new Color(colour.r, colour.g, colour.b, colour.a * edge));
            }

        }

        // The HUD canvas goes with the scene, and everything hanging off it goes with the canvas,
        // so the lists that held it start again.
        private static void BuildRoot()
        {
            ringText.Clear();
            rows.Clear();
            lastUnit = 0f;
            shownFolder = -1;
            root = new GameObject("WardrobeWheel");
            root.transform.SetParent(HUDCanvas.instance.rect, false);
            RectTransform rect = root.AddComponent<RectTransform>();
            Centre(rect);
            group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var holder = new GameObject("Spin");
            holder.transform.SetParent(root.transform, false);
            spin = holder.AddComponent<RectTransform>();
            Centre(spin);

            Image back = WheelHud.Panel(spin, "Plate", WheelHud.Plate);
            plate = (RectTransform)back.transform;
            Centre(plate);

            // One more than fits, for the row sliding in while the list scrolls.
            for (int i = 0; i < Rows + 1; i++)
            {
                var rowObject = new GameObject("Row" + i);
                rowObject.transform.SetParent(spin, false);
                RectTransform rowRect = rowObject.AddComponent<RectTransform>();
                Centre(rowRect);
                Image blank = WheelHud.Panel(rowRect, "Blank", new Color(1f, 1f, 1f, 0.08f));
                Centre((RectTransform)blank.transform);
                var iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(rowRect, false);
                iconObject.AddComponent<RectTransform>();
                Image icon = iconObject.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.enabled = false;
                Centre((RectTransform)icon.transform);
                TextMeshProUGUI name = WheelHud.Text(rowRect, "Name", WheelLayout.LookSize, TextAlignmentOptions.Left);
                name.overflowMode = TextOverflowModes.Ellipsis;
                var nameRect = (RectTransform)name.transform;
                Centre(nameRect);
                nameRect.pivot = new Vector2(0f, 0.5f);
                rows.Add(new Row { Rect = rowRect, Icon = icon, Blank = blank, Name = name });
            }

            timer = WheelHud.Panel(spin, "Settle", WheelHud.Gold);
            timer.sprite = WheelHud.Ring;
            timer.type = Image.Type.Filled;
            timer.fillMethod = Image.FillMethod.Radial360;
            timer.fillOrigin = (int)Image.Origin360.Top;
            timer.fillClockwise = true;
            timer.fillAmount = 0f;
            timerRect = (RectTransform)timer.transform;
            Centre(timerRect);

            hint = WheelHud.Text(spin, "Hint", WheelLayout.HintSize, TextAlignmentOptions.Center);
            hintRect = (RectTransform)hint.transform;
            hint.color = WheelHud.Hint;
        }

        private static void Centre(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
