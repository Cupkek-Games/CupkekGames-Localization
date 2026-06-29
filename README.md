# CupkekGames Localization

Editor tooling around `com.unity.localization`: import string-table mappings from CSV columns + bulk-config locale + asset-table layout.

## What's inside

**Editor** (`CupkekGames.Localization.Editor.asmdef`)

- `LocalizationSetupConfig` ScriptableObject + custom inspector (UI Toolkit, built with `CupkekGames.EditorUI`)
- `LocaleColumnMappingDrawer`, `StringTableCollectionMappingDrawer` — property drawers for CSV→table column mapping

There's also an empty runtime asmdef (`CupkekGames.Localization.asmdef`) kept as a placeholder so the Editor asmdef can stand alone.

## Dependencies

- `com.unity.localization` — Unity's localization package (String Tables, Google Sheets sync).
- `com.cupkekgames.editorui` — editor widget/styling library the custom inspector is built with.
