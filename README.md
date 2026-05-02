# CupkekGames Localization

Editor tooling around `com.unity.localization`: import string-table mappings from CSV columns + bulk-config locale + asset-table layout.

## What's inside

**Editor** (`CupkekGames.Localization.Editor.asmdef`)

- `LocalizationSetupConfig` ScriptableObject + custom inspector (`.uxml` / `.uss`)
- `LocaleColumnMappingDrawer`, `StringTableCollectionMappingDrawer` — property drawers for CSV→table column mapping

There's also an empty runtime asmdef (`CupkekGames.Localization.asmdef`) kept as a placeholder so the Editor asmdef can stand alone.

## Dependencies

Asmdef references resolve via Unity's `com.unity.localization` package.
