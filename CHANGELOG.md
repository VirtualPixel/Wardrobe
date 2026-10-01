# Changelog

## 1.0.0

- first release!
- presets tab is grouped into folders now with jump buttons and a sticky header, name any look and file it wherever you want
- right click a look (or F2) to rename it, move it, add a note, duplicate it or overwrite it with what your wearing
- comes with 150+ looks already made (mario, link, the simpsons, doctor who, bond, spongebob and a ton more) all built from vanilla pieces so the lobby sees them too
- the looks live in `Wardrobe/presets.cfg` so you can write your own by hand, F4 re-reads it without a restart and F3 dumps what you have on
- in-game wheel on the arrow keys, click the star next to a folders name and its on the ring. shipped folders come starred, yours dont untill you star them, unstars stick
- wheel goes a to z (a leading "the" gets skipped) and nothing goes on untill you stay on a look for a sec
- looks on the wheel go top to bottom with a pic of each one, holding an arrow keeps scrolling, and it makes the menu sounds as loud as the menu does (just for you, not over voice, `Wheel / SoundVolume`)
- enter on the wheel puts the look on right away, no waiting for the ring
- OFF is back on the wheel, first on the ring like the old smash & grab one. it takes the character off and puts you back in your own outfit (what you had on before the wheel started dressing you), remembered across restarts. up or down on it jumps to the first folder
- wheel pics get made ahead in the menus truck and shop so there all there when you open it in a level
- outfit codes, copy a whole look as one line, paste it in discord, your friend pastes it back. `/outfit` in chat posts yours
- whole folders share as one code too (WD2-), right click a look or use Codes. pasting the same folder twice only adds whats new, `/outfit paste` grabs a code off your clipboard, C on the wheel copies the look your on. no server, no login, its all just codes
- it never unlocks anything. a piece you dont own gets the closest thing you do own for that slot and keeps the colour, or the slot stays empty
- the preset still remembers the real pieces so once you unlock them the look is exact
- slot icon shows the finished look with locked stuff on, hovering says what's locked and what your wearing instead
- tax coins combine into the biggest coin they can (9 commons is a rare) and the one on top is always one the machine can actually spend. `General / CombineCoins` turns it off
- coin pile stops at 12 and shows the real count
- save backups every launch and right before wardrobe writes anything, bad copies get spotted and the last good one is never pruned. if you unlock everything yourself with the game shut thats the new normal after 2 launches. how to restore is in the readme
- the built in looks come straight from the mod now so updates add new ones and fix old ones. overwrite or recolour one and its yours, updates wont touch that one. slot says edited and theres a revert to original button on the right click page (undo brings your version back)
- delete goes to trash first, trash empties itself after 30 days
- undo and redo for everything on the tab (ctrl+z / ctrl+y)
- search with a random button, colour names and hex on the swatches, no more NEW badges
- slots grow on there own, up to 700, and every profile keeps the same count so none of them cut your looks off
- icons render in the background and get cached so the tab opens fast
- config is just General, Wheel, Keys and Advanced
- api for other mods (jukebox uses it) to read your look, get told when it changes, put a look on and ship a preset pack
