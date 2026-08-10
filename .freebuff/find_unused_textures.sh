#!/usr/bin/env bash
# Finds _Pirate textures not referenced with their _Pirate prefix.
# Two index passes (fast, single full-tree greps):
#   pref_refs  = every token containing "_pirate"           -> prefixed references
#   bare_refs  = path-like tokens (contain a '/') without "_pirate" -> unprefixed refs
# Per candidate P (relative to Resources/Textures/_Pirate):
#   P in pref_refs            -> USED (skip)
#   elif P in bare_refs:
#       Resources/Textures/P exists -> DUP  (live unprefixed copy)
#       else                     -> DEAD (only broken unprefixed refs)
#   else                      -> DEAD
set -u
cd "$(dirname "$0")/.." || exit 1

SRC="Resources/Prototypes Resources/ServerInfo Resources/Locale Resources/Maps \
Content.Pirate.Server Content.Pirate.Shared Content.Pirate.Client \
Content.Server Content.Shared Content.Client \
Content.Goobstation.Server Content.Goobstation.Shared Content.Goobstation.Client"
INC="--include=*.yml --include=*.yaml --include=*.cs --include=*.xaml --include=*.xml --include=*.ftl --include=*.json"

grep -rhoiE "[a-z0-9_./\-]*_pirate[a-z0-9_./\-]*" $SRC $INC 2>/dev/null | tr 'A-Z' 'a-z' | sort -u > /tmp/pref_refs.txt
grep -rhoE "[a-zA-Z0-9_./\-]+" $SRC $INC 2>/dev/null | tr 'A-Z' 'a-z' | grep '/' | sort -u > /tmp/path_tokens.txt
grep -v '_pirate' /tmp/path_tokens.txt > /tmp/bare_refs.txt
echo "pref tokens: $(wc -l < /tmp/pref_refs.txt), bare path tokens: $(wc -l < /tmp/bare_refs.txt)"

dead=0; dup=0; used=0
classify() {
  local p="$1" lp
  lp=$(printf '%s' "$p" | tr 'A-Z' 'a-z')
  if grep -Fqi -- "_pirate/$lp" /tmp/pref_refs.txt; then
    used=$((used+1)); return 0
  fi
  if grep -Fqi -- "$lp" /tmp/bare_refs.txt; then
    if [ -e "Resources/Textures/$p" ]; then
      echo "DUP   $p   (live unprefixed copy exists)"
      dup=$((dup+1)); return 0
    fi
  fi
  echo "DEAD  $p"
  dead=$((dead+1))
}

echo "=== RSI folders ==="
while IFS= read -r d; do
  classify "${d#Resources/Textures/_Pirate/}"
done < <(find Resources/Textures/_Pirate -type d -name "*.rsi" | sort)
echo "--- RSI: dead=$dead dup=$dup used=$used ---"

echo "=== standalone PNGs ==="
dead=0; dup=0; used=0
while IFS= read -r f; do
  classify "${f#Resources/Textures/_Pirate/}"
done < <(find Resources/Textures/_Pirate -type f -name "*.png" | grep -v "\.rsi/" | sort)
echo "--- PNG: dead=$dead dup=$dup used=$used ---"
