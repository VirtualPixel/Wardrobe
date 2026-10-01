using System;
using System.Collections.Generic;
using Wardrobe.Services;

namespace Wardrobe
{
    // What other mods get to see and do. Everything here runs on Unity's main thread, and every
    // call is safe before the game has loaded the cosmetic save: the lists come back empty and
    // Equip says no.
    public static class Api
    {
        public const string PluginGuid = "Vippy.Wardrobe";

        // The look you have on, by its name in the Presets tab, or null when what you wear is not
        // one of your looks (nothing picked yet, or you changed a piece by hand since).
        public static string? CurrentLook => LookState.Name;

        // The category the current look sits in, or null with no look on.
        public static string? CurrentCategory => LookState.Category;

        // How many pieces of the current look are stood in for by something you own, or left off.
        public static int CurrentStandIns => LookState.StandIns;

        // Fires when the look you have on changes, with its name, or null when you are no longer
        // in one of your looks. Only for you: see LookOf for the others.
        public static event Action<string?>? LookChanged
        {
            add => LookState.Changed += value;
            remove => LookState.Changed -= value;
        }

        // The look another player has on, as their own Wardrobe told the room. Null when they do not
        // run Wardrobe, wear none of their looks, or are not in a room with you. Your own avatar
        // answers the same as CurrentLook.
        public static string? LookOf(PlayerAvatar avatar) => LookState.Of(avatar);

        // Every category that holds a look, A to Z with a leading "The" ignored. Trash is never in it.
        public static IReadOnlyList<string> Categories() => LookCatalog.Categories(starredOnly: false);

        // Only the starred categories: the ones the wheel can show.
        public static IReadOnlyList<string> StarredCategories() => LookCatalog.Categories(starredOnly: true);

        // The looks in one category, in the category's own order (shipped and pack sets as their file
        // lists them, your own in the order you made them). Empty for a category that does not exist.
        public static IReadOnlyList<string> Looks(string category) => LookCatalog.Looks(category);

        // Puts a look on by name, the same way the wheel does, and sends it to the room when you are in
        // one. Nothing is ever unlocked: a piece you do not own is worn as the closest piece you do own
        // for that slot, or the slot stays empty. False when the look is unknown or the save is not
        // loaded yet.
        public static bool Equip(string look) => LookCatalog.Equip(look);

        // Equip for a moment: a power-up, a costume the game hands you. The same look goes on and
        // out to the room, but the save keeps the look you picked yourself, whatever writes it
        // meanwhile. Your next pick (the wheel, the menu, Equip) ends it.
        public static bool EquipForNow(string look) => LookCatalog.Equip(look, keep: false);

        // For a mod that changes pieces itself for a moment (a hat an ability hands you): call it
        // first, and the save keeps what you have on now until your next pick, as with EquipForNow.
        public static void HoldForNow() => LookCatalog.HoldForNow();

        // What OFF on the wheel does: the outfit the player wore before the wheel (or your pack)
        // started dressing them goes back on and out to the room, and the save keeps it. Never a
        // pack's look, never anything they have not unlocked. Afterwards CurrentLook is null or
        // one of their own looks. False before the save has loaded.
        public static bool ResetToOwnLook() => OwnLooks.PutBack(MetaManager.instance);

        // A presets file your mod ships, in the same section format as presets.cfg. Its looks show up
        // as their own categories (a section's category line, or your plugin's GUID when it has none),
        // come starred the first time Wardrobe sees them, and are never written back to presets.cfg.
        // Call it from your plugin's Awake; a later call imports right away. False when the file is
        // missing.
        //
        // Names: a look named like one Wardrobe ships takes that name while your pack is registered
        // (Wardrobe's own steps aside and comes back if your pack goes). A look named like one of the
        // player's own, or an earlier pack's, is shown as "<name> (<category>)". PackLookShownAs and
        // PackLookOf translate between your names and the ones the player sees.
        public static bool RegisterPack(string pluginGuid, string presetsFilePath) => PresetPacks.Register(pluginGuid, presetsFilePath);

        // The name your pack's look is shown under: packLook itself, or "<packLook> (<category>)"
        // when the player has a look of that name. Null before the save has loaded or when the look
        // is not in your pack.
        public static string? PackLookShownAs(string pluginGuid, string packLook) => PackNames.Current.ShownAs(pluginGuid, packLook);

        // The other way round: the name your pack gives a look the player sees (yours, or one another
        // player sent the room), or null when it is not one of your pack's looks.
        public static string? PackLookOf(string pluginGuid, string look) => PackNames.Current.PackNameOf(pluginGuid, look);

        // Narrows what the wheel shows: a look is on the wheel only when every registered filter says
        // yes to its name, and a category with nothing left is not shown. One filter per owner; a new
        // call replaces your old one, null clears it. It never touches the Presets tab or the lists above.
        public static void SetWheelFilter(string ownerGuid, Func<string, bool>? show) => WheelFilters.Set(ownerGuid, show);

        public static void ClearWheelFilter(string ownerGuid) => WheelFilters.Set(ownerGuid, null);
    }
}
