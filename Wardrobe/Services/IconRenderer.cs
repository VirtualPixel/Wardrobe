using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wardrobe.Services
{
    // Renders one preset slot's icon the way MenuElementCosmeticPreset.GetIcon does, but as
    // a routine the mod owns: it waits an extra frame for other mods to finish touching the
    // avatar camera, waits for the game's PNG to land, and hands back a quarter-size sprite.
    // The Presets tab renders through a button; the wheel has no button, so it renders a slot.
    internal static class IconRenderer
    {
        private static readonly HashSet<MenuElementCosmeticPreset> busy = new HashSet<MenuElementCosmeticPreset>();
        private static GameObject? avatarPrefab;

        public static bool IsBusy(MenuElementCosmeticPreset button) => busy.Contains(button);

        // Called when the host object dies mid-render; the finally below never runs for a coroutine that is stopped.
        public static void Reset() => busy.Clear();

        // The avatar the icons are taken of. Every preset button carries it; with no button about
        // (in a level) it comes off the button prefab the cosmetics page is built from.
        public static GameObject? AvatarPrefab()
        {
            if (avatarPrefab)
            {
                return avatarPrefab;
            }
            MenuManager menu = MenuManager.instance;
            if (!menu || menu.menuPages == null)
            {
                return null;
            }
            foreach (MenuManager.MenuPages page in menu.menuPages)
            {
                if (page.menuPageIndex != MenuPageIndex.Cosmetics || !page.menuPage)
                {
                    continue;
                }
                MenuPageCosmetics cosmetics = page.menuPage.GetComponentInChildren<MenuPageCosmetics>(true);
                MenuElementCosmeticPreset? button = cosmetics && cosmetics.presetButtonPrefab
                    ? cosmetics.presetButtonPrefab.GetComponentInChildren<MenuElementCosmeticPreset>(true)
                    : null;
                if (button)
                {
                    avatarPrefab = button!.playerAvatarIconPrefab;
                }
            }
            return avatarPrefab;
        }

        public static bool AnyBusy => busy.Count > 0;

        public static IEnumerator Render(MenuElementCosmeticPreset button)
        {
            MetaManager meta = MetaManager.instance;
            if (!button || !meta || !button.playerAvatarIconPrefab)
            {
                yield break;
            }
            if (!busy.Add(button))
            {
                yield break;
            }
            avatarPrefab = button.playerAvatarIconPrefab;
            try
            {
                Sprite? made = null;
                yield return RenderSlot(button.playerAvatarIconPrefab, button.presetIndex, () => (bool)button, s => made = s);
                if (made && button)
                {
                    button.icon = made;
                    button.UpdateIcon(true);
                }
            }
            finally
            {
                busy.Remove(button);
            }
        }

        // done gets the small sprite (or the full one if the game's file never landed), or null.
        // wanted is asked between frames; once it says no the render is dropped.
        public static IEnumerator RenderSlot(GameObject prefab, int slot, Func<bool> wanted, Action<Sprite?> done)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !prefab || slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                done(null);
                yield break;
            }
            string path = WardrobePaths.PresetIcon(meta, slot);
            GameObject avatar = Dress(meta, prefab, slot, out SemiIconMaker? maker);
            try
            {
                if (!maker)
                {
                    done(null);
                    yield break;
                }

                yield return null;
                yield return null;
                // A scene load in between takes the avatar with it.
                if (!wanted() || !avatar || !maker)
                {
                    done(null);
                    yield break;
                }
                Sprite? sprite = maker!.CreateIconFromRenderTexture(path);
                // The picture is taken and the PNG has its own bytes; the lights go now.
                UnityEngine.Object.Destroy(avatar);
                if (!sprite)
                {
                    Log.Always("Wardrobe: icon " + slot + ": the icon maker returned nothing.");
                    done(null);
                    yield break;
                }
                Log.Verbose("Wardrobe: icon " + slot + " rendered " + sprite!.texture.width + "x" + sprite.texture.height + ".");

                // The game writes the PNG on a worker thread. Wait for it to land and hold still for a
                // frame, so the small copy is keyed to the finished file and not to a half-written one.
                float deadline = Time.unscaledTime + 3f;
                long size = -1;
                while (Time.unscaledTime < deadline)
                {
                    long now = File.Exists(path) ? new FileInfo(path).Length : -1;
                    if (now > 0 && now == size)
                    {
                        break;
                    }
                    size = now;
                    yield return null;
                }
                if (!wanted())
                {
                    // A sprite does not take its texture with it.
                    UnityEngine.Object.Destroy(sprite.texture);
                    UnityEngine.Object.Destroy(sprite);
                    done(null);
                    yield break;
                }
                done(File.Exists(path) ? IconCache.Store(meta, slot, sprite) : sprite);
                Log.Verbose("Wardrobe: icon " + slot + " delivered" + (File.Exists(path) ? " and cached." : ", file still pending."));
            }
            finally
            {
                if (avatar)
                {
                    UnityEngine.Object.Destroy(avatar);
                }
            }
        }

        // The wheel's render: the same avatar and camera, taken straight at quarter size. No
        // full-size texture, no PNG encode and no texture compress on the main thread, which is
        // what made a render a hitch you could feel. done gets a readable texture or null.
        public static IEnumerator RenderSmall(GameObject prefab, int slot, Func<bool> wanted, Action<Texture2D?> done)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !prefab || slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                done(null);
                yield break;
            }
            GameObject avatar = Dress(meta, prefab, slot, out SemiIconMaker? maker);
            try
            {
                if (!maker)
                {
                    done(null);
                    yield break;
                }
                yield return null;
                yield return null;
                if (!wanted() || !avatar || !maker)
                {
                    done(null);
                    yield break;
                }
                done(Capture(maker!, IconCache.SmallWidth, IconCache.SmallHeight));
            }
            finally
            {
                if (avatar)
                {
                    UnityEngine.Object.Destroy(avatar);
                }
            }
        }

        // SemiIconMaker.CreateIconFromRenderTexture's render, into a texture of our own size.
        private static Texture2D? Capture(SemiIconMaker maker, int width, int height)
        {
            Camera camera = maker.iconCamera;
            if (!camera || !maker.renderTexture)
            {
                Log.Always("Wardrobe: the icon maker has no camera or render texture, cannot render.");
                return null;
            }
            if (!maker.gameObject.activeSelf)
            {
                maker.gameObject.SetActive(true);
            }
            RenderTextureDescriptor descriptor = maker.renderTexture.descriptor;
            descriptor.width = width;
            descriptor.height = height;
            descriptor.msaaSamples = Mathf.Max(descriptor.msaaSamples, 4);
            RenderTexture target = RenderTexture.GetTemporary(descriptor);
            PlayerAvatarMenu holder = maker.GetComponentInParent<PlayerAvatarMenu>();
            Transform parked = holder ? holder.transform : maker.transform.parent;
            Vector3 was = parked.position;
            parked.position = new Vector3(-1000f, -1000f, -1000f);
            RenderTexture previousTarget = camera.targetTexture;
            bool fog = RenderSettings.fog;
            Color ambient = RenderSettings.ambientLight;
            camera.targetTexture = target;
            RenderSettings.fog = false;
            RenderSettings.ambientLight = maker.ambientLight;
            camera.Render();
            RenderSettings.fog = fog;
            RenderSettings.ambientLight = ambient;
            camera.targetTexture = previousTarget;
            parked.position = was;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var shot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            shot.Apply(false, false);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            maker.gameObject.SetActive(false);
            return shot;
        }

        // The icon avatar wearing the slot's look, far from anything in a level (the game parks its
        // own menu avatars out here too), with only the one icon maker left on.
        private static GameObject Dress(MetaManager meta, GameObject prefab, int slot, out SemiIconMaker? maker)
        {
            GameObject avatar = UnityEngine.Object.Instantiate(prefab, new Vector3(-1000f, -1000f, -1000f), Quaternion.identity);
            maker = null;
            PlayerCosmetics cosmetics = avatar.GetComponentInChildren<PlayerCosmetics>();
            if (!cosmetics)
            {
                Log.Always("Wardrobe: icon " + slot + ": the avatar prefab has no PlayerCosmetics, cannot render.");
                return avatar;
            }
            var list = new List<int>();
            foreach (int item in meta.cosmeticPresets[slot])
            {
                if (item < 0 || item >= meta.cosmeticAssets.Count || !meta.cosmeticAssets[item])
                {
                    continue;
                }
                list.Add(item);
                foreach (CosmeticAsset evicted in meta.GetCosmeticsToUnequip(list, meta.cosmeticAssets[item]))
                {
                    list.Remove(meta.cosmeticAssets.IndexOf(evicted));
                }
            }
            cosmetics.SetupCosmeticsLogic(list.ToArray(), false);
            cosmetics.SetupColorsLogic(meta.colorPresets[slot].ToArray());

            PlayerAvatarMenu menu = avatar.GetComponentInChildren<PlayerAvatarMenu>();
            maker = menu && menu.cameraAndStuff ? menu.cameraAndStuff.GetComponentInChildren<SemiIconMaker>(true) : null;
            if (!maker)
            {
                Log.Always("Wardrobe: icon " + slot + ": no SemiIconMaker on the avatar prefab, cannot render.");
                return avatar;
            }
            foreach (SemiIconMaker other in avatar.GetComponentsInChildren<SemiIconMaker>())
            {
                if (other != maker)
                {
                    other.gameObject.SetActive(false);
                }
            }
            return avatar;
        }
    }
}
