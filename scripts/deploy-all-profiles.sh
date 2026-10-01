#!/usr/bin/env bash
# Copies the built Wardrobe.dll into every Gale REPO profile.
# Safe while the game runs: BepInEx loads the DLL at startup, and the copy lands as an atomic rename.
set -euo pipefail

PROFILES="$HOME/.local/share/com.kesomannen.gale/repo/profiles"
SRC="$PROFILES/Developer/BepInEx/plugins/Wardrobe/Wardrobe.dll"

[ -f "$SRC" ] || { echo "No build at $SRC, run dotnet build first." >&2; exit 1; }

for profile in "$PROFILES"/*/; do
    dest="$profile/BepInEx/plugins/Wardrobe"
    if [ "$(readlink -f "$dest/Wardrobe.dll")" = "$(readlink -f "$SRC")" ]; then
        echo "source   -> $(basename "$profile")"
        continue
    fi
    mkdir -p "$dest"
    cp -p "$SRC" "$dest/.Wardrobe.dll.staging" && mv -f "$dest/.Wardrobe.dll.staging" "$dest/Wardrobe.dll"
    echo "deployed -> $(basename "$profile")"
done
