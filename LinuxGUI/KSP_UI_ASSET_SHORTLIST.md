# KSP UI Asset Shortlist

Last reviewed: 2026-04-21

Purpose: quick internal shortlist of KSP-adjacent asset sources that are
reasonable for LinuxGUI styling work. This is a design vetting note, not legal
advice.

## Best candidates

### 1. KerbalX Font Pack

- Link: <https://kerbalx.com/fontpack>
- Best use: icon glyphs for KSP-themed UI, especially if we want CSS-like font
  icons, scalable symbols, or small aerospace-flavored status markers.
- Why it is useful: it is explicitly presented as a web-project icon/font pack
  and ships font assets plus stylesheet guidance.
- License status: the KerbalX page lists it as "CC 4.0". Before bundling it in
  this repo, verify the exact Creative Commons variant inside the downloaded
  package.
- Current recommendation: best first place to look if we want actual
  KSP-flavored iconography instead of inventing all symbols ourselves.

### 2. Better Icons

- Link: <https://spacedock.info/mod/3718/Better>
- Source: <https://github.com/pasalvetti/BetterIcons/>
- Best use: reference or direct reuse for simple readable category/icon ideas.
- Why it is useful: the SpaceDock page lists it as MIT, and it is specifically
  a UI icon replacement mod.
- Caution: it is a KSP 2 mod, so the visual language may not match stock KSP 1
  exactly.
- Current recommendation: safe candidate if we need permissively licensed UI
  icon ideas quickly.

## Reference-only sources

### HUDReplacer

- Link: <https://github.com/UltraJohn/HUDReplacer>
- Best use: understanding how KSP UI textures are named, swapped, and recolored.
- License status: GPL-3.0 for the repo.
- Current recommendation: use as a technical reference, not as a casual
  copy-paste asset source.

### Official KSP modding/community pages

- KSP site: <https://www.kerbalspaceprogram.com/>
- Forum rules: <https://forum.kerbalspaceprogram.com/topic/154851-add-on-posting-rules-april-13-2021/>
- Best use: confirming whether a mod is likely to have a declared license and
  public source.
- Important note: these rules are good for vetting, but they are not blanket
  permission to reuse art from mods or stock game assets.

## Avoid bundling by default

### RSS-Textures

- Link: <https://github.com/KSP-RO/RSS-Textures>
- Why to avoid by default: repo readme lists CC-BY-NC-SA, which is more
  restrictive than the usual MIT/CC0/CC-BY path we want for app UI assets.
- Current recommendation: visual reference only unless we deliberately accept
  the license obligations.

### StationPartsExpansionRedux art assets

- Link: <https://github.com/post-kerbin-mining-corporation/StationPartsExpansionRedux>
- Why to avoid: the repo states that its `.dds`, `.png`, and `.mu` art assets
  are All Rights Reserved.
- Current recommendation: do not reuse or redistribute those assets without
  explicit permission.

## Practical rules for LinuxGUI

- Prefer self-made XAML/CSS-like shapes for tabs, chips, rails, and frames.
- Prefer permissive assets only: MIT, CC0, or CC-BY.
- Treat stock KSP art and most mod textures as reference material unless reuse
  rights are explicit.
- Do not assume "source available" means "art reusable".
- If a source says only "CC 4.0", verify the exact variant before vendoring.

## Recommendation

For this GUI, the safest path is:

1. Build the core visual language ourselves in XAML.
2. Pull only clearly licensed iconography.
3. Start with KerbalX Font Pack and Better Icons if we want actual
   KSP-adjacent symbols.
4. Use mods like HUDReplacer as inspiration/reference, not as a bundle source.

