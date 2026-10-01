# Wardrobe

*Outfit presets for R.E.P.O. Keep your looks in folders, flip between them on a wheel mid-run, trade them (or whole folders) as one-line codes, and start with 150+ costumes already built. It never unlocks a thing.*

> **A note from Vippy**
>
> After way too long of a break, I'm finally back and working on these again :D Every one of my mods just got a full pass: bugs fixed, reports read, a few things I'd always meant to do. New builds and fixes land in [Vippy's Discord](https://discord.gg/kKqhck2NrP) before they hit Thunderstore, so come hang out. Thanks for sticking around.

The cosmetics menu gives you a row of numbered preset slots and that's it. I kept losing track of which slot was which, so Wardrobe turns the Presets tab into an actual wardrobe: named looks, filed into folders, with a wheel so you can change outfits without opening a menu.

Every look is made from the game's own cosmetics and palette colours, which is all the game sends to other players anyway. So the lobby sees exactly what you see, and **only you need the mod**.

## What you get

- **Folders.** The Presets tab is split into folders with a jump button each. Right-click a look (or hover it and press F2) to rename it, move it, give it a note, duplicate it or overwrite it with what you have on.
- **150+ looks out of the box.** Mario and Luigi, Link, Sonic, Master Chief, Homer, SpongeBob, the Doctor, Bond and his whole rogues gallery, and a lot more, sorted into Games, Cartoons, Movies, Horror, Holidays and friends.
- **A wheel in game.** Press an arrow key while you're playing. Left and right turn through your starred folders, up and down walk the looks in the one you're on, each with its picture. Stay on a look for a second, or hit Enter, and it goes on, the room sees it right away. **OFF**, first on the ring, puts you back in your own outfit. Escape backs out.
- **Codes.** A whole look, or a whole folder, on one line to drop in Discord. Your friend pastes it back in and has it too.
- **Change the built-in looks.** Overwrite a shipped look (or one a pack brings) with your own take and it stays yours. Updates still add new looks and fix the ones you haven't touched, but never write over yours. The slot says *edited*, and **Revert to original** on its right-click page brings the original back (Undo gets yours back).
- **Undo and redo** for everything on the tab, plus a Trash so a delete is never final by accident.
- **Search** with a random button for when you can't decide.
- **Colour names** and hex codes when you hover a swatch.
- **Your coins sorted out.** Tax coins combine into the biggest coin they can make, and the coin on top is always one the shop machine can actually spend.
- **Save backups** before Wardrobe ever writes to your save.

## It never unlocks anything

Plenty of the shipped looks use pieces you won't have yet. Wardrobe doesn't hand them to you, not in the save and not even for a frame.

Instead, a piece you don't own is swapped for the closest thing you do own in that slot: same item in another variant first, then something from the same family (another moustache, another hat, another pair of glasses), nearest rarity breaking the tie. It keeps the look's colour. If nothing you own comes close, that slot just stays empty.

The preset still remembers the real pieces, so the day you unlock one the look snaps to exact. The slot icon always shows the finished look, locked bits and all, and hovering it tells you what's still locked and what you're wearing in its place. Hover a preset and the doll tries on exactly what clicking it would put on.

## The wheel

Click the star next to a folder's name on the Presets tab and it's on the wheel (click again to take it off). The folders Wardrobe ships come starred the first time you see them; your own folders wait until you star them, and if you unstar something it stays unstarred through updates.

The folder you're in is the header with its neighbours either side, A to Z (a leading "The" is ignored). Left and right change folder, up and down go through its looks, listed top to bottom with a picture of each. Hold an arrow and it keeps going. Looks inside keep the folder's own order, so the shipped sets go most famous first. Let go and stay on one for a second, or hit Enter, and it goes on; Escape backs out. It ticks and clicks with the game's own menu sounds, as loud as the menu's, and only you hear them.

**OFF** sits first on the ring, left of your first folder, and it's always there even with nothing starred. It takes off whatever look the wheel (or another mod, like a Smash & Grab character) put on you and puts back what you were wearing before that: what you launched in, changed by hand in the cosmetics menu, or picked from your own looks on the Presets tab. Up or down on it jumps to the first folder. Wardrobe remembers that outfit across restarts in `Wardrobe/ownlook.txt`; with nothing remembered it's the last look you wore that isn't from a mod's pack, and failing that a plain Semibot. You open on OFF whenever nothing on the wheel is what you've got on.

The pictures get drawn ahead while you're in the menus, the truck or the shop, so they're all there when you open it mid-level.

The wheel doesn't open in menus, the lobby or while you're typing. If another mod wants the bare arrows, set `Wheel / Modifier` to a key and the arrows only work while you hold it.

## Sharing looks

Everything you share is a code, one line of text you can drop in Discord. There's no server and no account; Wardrobe never goes online.

```
WD1-042G-8FAR-CT60-2171-ZW88-030N-2M20-858N-0G20-R58N-1G1G-YHBC-CNV6-AVKM-D0G4-8VV3-EHQQ-423P-60Q3-8BHM-5RSN-6
```

- **One look** starts with `WD1-`. Copy it from the look's right-click page, from **Codes** in the folder row (what you have on), or press C on the wheel for the look you're on.
- **A whole folder** starts with `WD2-` and carries the folder's name and every look in it. Copy it from any look's right-click page, or pick the folder under Codes.
- **Paste one** under Codes. The box starts on whatever's on your clipboard and shows you what's in it before anything is saved. A look goes into `Shared`, a folder goes in under its own name (or pick another).
- **In chat**, `/outfit` posts what you have on. When somebody else posts one you get a line saying who shared what, and F7 keeps it. `/outfit paste` keeps whatever code is on your clipboard without sending anything.

A name you already use gets a number, and pasting the same folder twice only adds the looks you didn't have yet. A code is only names and colours, so it never unlocks anything: pieces you don't own get the usual stand-ins. Codes remember which game version they came from; one from another version still works, the preview just warns you.

## Coins

The cosmetic machine reads the coin on top of your pile. Once you've got every common, a common on top is a coin it can't spend. Wardrobe keeps the pile sorted so the cheapest coin that still has something behind it is on top, and combines the rest upward: three commons make an uncommon, three of those a rare, and so on, as high as they can go.

It never turns a coin you can spend into one you can't, never makes a coin out of thin air, and leaves the pile alone once there's nothing left to unlock. Don't want it? `General / CombineCoins` turns combining off (the sorting stays).

The pile on screen also stops at 12 coins and prints the real count instead of burying the table.

## Save backups

Wardrobe copies your cosmetic saves (`MetaSave.es3`, `MetaSaveModded.es3` and their `.bak` files) to `Wardrobe/backups/` on every launch and right before it writes anything itself. Each copy notes how many cosmetics were unlocked in it, so a copy that suddenly lost unlocks (or suddenly has all of them) counts as bad, and the last good one is never cleaned up. If you unlock everything yourself with the game closed, that becomes the new normal once the game has started on it twice.

**Putting one back:**

1. Close the game.
2. Open the save folder. On Windows that's `%USERPROFILE%\AppData\LocalLow\semiwork\Repo\`, on Linux under Proton it's `steamapps/compatdata/3241660/pfx/drive_c/users/steamuser/AppData/LocalLow/semiwork/Repo/`.
3. Pick a folder in `Wardrobe/backups/`. Its `backup.txt` says when and why it was taken and how many unlocks it holds.
4. Copy its `.es3` and `.bak` files over the ones in the save folder, then start the game.

The save you just replaced got backed up on the launch before, so you can go back the same way.

## Writing your own looks

Your own looks live in `Wardrobe/presets.cfg`, next to the game's saves. The built-in ones come from the mod itself, so updates bring new looks and fixes to old ones; a section in presets.cfg with a built-in look's name is your version of it and wins. One section per look, pieces and colours by name:

```
[Eleventh Doctor]
category = Doctor Who
note = Fez, bow tie, tweed.
skin = Dark Coral
hat = Cone
bodytop = Tie
arms = Jacket Sleeve
color.hat = Dark Red
color.bodytop = Dark Red
```

`Wardrobe/catalogue.txt` lists every cosmetic and colour name the game has. Edit, press F4 in the cosmetics menu, and reopen the Presets tab. F3 writes what you're wearing to `exported.cfg` as a section you can paste straight in.

## Config

In `Vippy.Wardrobe.cfg`, or in game with REPOConfig.

| Section | What's in it |
|---|---|
| **General** | Import the shipped looks, name strips on the slots, colour names, `CombineCoins`. |
| **Wheel** | The optional `Modifier` key, the four arrow keys (left from your first folder is OFF), Enter to put the look on now, C to copy the look you're on, and `SoundVolume`. |
| **Keys** | F2 rename, F3 export, F4 re-read the library, F7 keep a code from chat. |
| **Advanced** | Slot count (grows on its own up to 700), days before Trash empties (30), coin pile size, how many coins make the next one up, how many backups to keep, how long the wheel waits, log level. |

Settings from older builds move over on their own and keep their values.

## Compatibility

- Client-side. Nobody else in the lobby needs anything.
- Needs [MenuLib](https://thunderstore.io/c/repo/p/nickklmao/MenuLib/). Works with or without REPOLib.
- Custom colours, eye colours and new cosmetic models aren't possible without every player installing something, so they're not here.
- Take the mod out and the game drops every slot past 28 the next time it saves.

## Goes well with

- **Jukebox**: sounds per look. Give a Wardrobe look its own set of sounds and they follow whatever you're wearing. Needs Wardrobe.
- **Smash & Grab** is coming later and builds on both.

Making a mod? `Wardrobe.Api` lets you read the current look, hear when it changes, put a look on by name (through the same no-unlock path), take it off again the way OFF does (`ResetToOwnLook`) and ship your own folder of looks as a preset pack. It's all in [Api.cs](https://github.com/VirtualPixel/Wardrobe/blob/main/Wardrobe/Api.cs).

## Come hang out

I'm Vippy. I make R.E.P.O. mods and I read every bug report.

| | |
|---|---|
| **[Vippy's Discord](https://discord.gg/kKqhck2NrP)** | Test builds land here before Thunderstore, you get a say in what comes next, and it's the fastest way to get a bug fixed. Come say hi. |
| **[GitHub](https://github.com/VirtualPixel/Wardrobe)** | The source, plus [issues](https://github.com/VirtualPixel/Wardrobe/issues) for bug reports and ideas that deserve a paper trail. |
| **[R.E.P.O. Modding Server](https://discord.gg/9fDzZ9sk95)** | The whole modding scene, not just me. |
| **[More of my mods](https://thunderstore.io/c/repo/p/Vippy/)** | Everything else I've made for R.E.P.O., like [SharedUpgradesPlus](https://thunderstore.io/c/repo/p/Vippy/SharedUpgradesPlus/) if your crew is tired of fighting over who gets the upgrades. |

## Keep the mods coming

Everything I make is free and stays free. Two ways to help if you feel like it, neither one expected:

- **[Ko-fi](https://ko-fi.com/vippydev)**: buy me a coffee and your name goes on the supporters list in my Discord. Every coffee buys another evening on the next update.
- **[BisectHosting](https://bisecthosting.com/vippy)**: hosting a server for Minecraft or anything else your crew plays? Code `vippy` takes 25% off, and I get a cut at no cost to you. It's where my own servers live.

[![25% off BisectHosting servers with code vippy](https://www.bisecthosting.com/partners/custom-banners/71eecea6-f5bb-437d-ac56-f6fee4266193.png)](https://bisecthosting.com/vippy)
