using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEditor.Localization.Reporting;
using CupkekGames.EditorUI;

namespace CupkekGames.Localization.Editor
{
    [CustomEditor(typeof(LocalizationSetupConfig))]
    public class LocalizationSetupConfigEditor : UnityEditor.Editor
    {
        private LocalizationSetupConfig _config;

        // Dynamic UI elements refreshed in-place (never rebuilt, so focus/scroll survive).
        private VisualElement _alertSlot;
        private Label _summaryLabel;
        private Button _openSheetButton;
        private ToolbarSearchField _searchField;
        private VisualElement _perTableContainer;
        private string _searchQuery = "";

        // Setup / bulk action buttons (enabled-state driven by Refresh).
        private Button _btnRunAllSetup;
        private Button _btnFindTables;
        private Button _btnCreateMissing;
        private Button _btnSetupSheetIds;
        private Button _btnApplyConfig;
        private Button _btnAutoDetectLocales;
        private Button _btnPushAll;
        private Button _btnPullAll;

        public override VisualElement CreateInspectorGUI()
        {
            _config = (LocalizationSetupConfig)target;

            var root = new VisualElement();

            // Validation banner — sits above everything so the reason actions are gated is visible.
            _alertSlot = new VisualElement();
            root.Add(_alertSlot);

            // Editable config fields (provider, spreadsheet id, key/locale columns, table list…).
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            root.Add(EditorUIElements.CreateDivider());

            root.Add(BuildSummaryRow());
            root.Add(BuildSetupSection());
            root.Add(BuildBulkSection());
            root.Add(BuildPerTableSection());

            Refresh();

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());

            return root;
        }

        #region UI building

        private VisualElement BuildSummaryRow()
        {
            var row = EditorUIElements.CreateRow(Align.Center, Justify.SpaceBetween);
            row.style.marginBottom = 4f;

            _summaryLabel = EditorUIElements.CreateHeaderLabel("");
            row.Add(_summaryLabel);

            _openSheetButton = EditorUIElements.CreateLinkButton("Open Spreadsheet", OpenSpreadsheet);
            row.Add(_openSheetButton);

            return row;
        }

        private VisualElement BuildSetupSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 6f;
            section.Add(EditorUIElements.CreateHeaderLabel("Setup"));

            _btnRunAllSetup = SolidButton("Run All Setup", EditorSolidColors.Success, EditorSolidColors.SuccessContent, () =>
            {
                FindAllStringTableCollections(_config);
                CreateMissingLocaleTables(_config);
                AutoSetupSheetIds(_config);
                SetupAllCollections(_config);
                serializedObject.Update();
                Refresh();
            });
            _btnRunAllSetup.style.marginTop = 4f;
            _btnRunAllSetup.style.marginBottom = 4f;
            section.Add(_btnRunAllSetup);

            var row1 = EditorUIElements.CreateRow();
            _btnFindTables = Grow(EditorUIElements.CreateButton("1. Find Tables", () =>
            {
                FindAllStringTableCollections(_config);
                serializedObject.Update();
                Refresh();
            }));
            _btnCreateMissing = Grow(EditorUIElements.CreateButton("2. Create Missing", () =>
            {
                CreateMissingLocaleTables(_config);
                serializedObject.Update();
                Refresh();
            }));
            row1.Add(_btnFindTables);
            row1.Add(_btnCreateMissing);
            section.Add(row1);

            var row2 = EditorUIElements.CreateRow();
            _btnSetupSheetIds = Grow(EditorUIElements.CreateButton("3. Setup Sheet IDs", () =>
            {
                AutoSetupSheetIds(_config);
                serializedObject.Update();
                Refresh();
            }));
            _btnApplyConfig = Grow(EditorUIElements.CreateButton("4. Apply Config", () =>
            {
                SetupAllCollections(_config);
                serializedObject.Update();
                Refresh();
            }));
            row2.Add(_btnSetupSheetIds);
            row2.Add(_btnApplyConfig);
            section.Add(row2);

            _btnAutoDetectLocales = EditorUIElements.CreateButton("Auto-detect Locales from Project", AutoDetectLocales);
            _btnAutoDetectLocales.style.marginTop = 4f;
            section.Add(_btnAutoDetectLocales);

            return section;
        }

        private VisualElement BuildBulkSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 6f;
            section.Add(EditorUIElements.CreateHeaderLabel("Bulk Operations"));

            var row = EditorUIElements.CreateRow();
            row.style.marginTop = 4f;

            _btnPushAll = Grow(SolidButton("Push All", EditorSolidColors.Info, EditorSolidColors.InfoContent,
                () => PushAll(_config)));
            _btnPullAll = Grow(SolidButton("Pull All", EditorSolidColors.Warning, EditorSolidColors.WarningContent, () =>
            {
                if (ConfirmPull("Pull all configured String Table Collections from Google Sheets?"))
                    PullAll(_config);
            }));
            row.Add(_btnPushAll);
            row.Add(_btnPullAll);
            section.Add(row);

            return section;
        }

        private VisualElement BuildPerTableSection()
        {
            var foldout = new FoldoutSection("Per-Table Operations", "LocalizationSetupConfig_perTable", true);

            _searchField = new ToolbarSearchField();
            _searchField.style.marginTop = 4f;
            _searchField.style.marginBottom = 4f;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? "";
                RebuildPerTableUI();
            });
            foldout.Content.Add(_searchField);

            _perTableContainer = new VisualElement();
            foldout.Content.Add(_perTableContainer);

            return foldout;
        }

        #endregion

        #region Refresh

        private void Refresh()
        {
            UpdateValidationBanner();
            UpdateSummary();
            UpdateButtonStates(_config);
            RebuildPerTableUI();
        }

        private void UpdateValidationBanner()
        {
            _alertSlot.Clear();

            bool noProvider = _config.googleSheetsServiceProvider == null;
            bool noSpreadsheet = string.IsNullOrEmpty(_config.spreadsheetId);
            if (!noProvider && !noSpreadsheet)
                return;

            string msg;
            if (noProvider && noSpreadsheet)
                msg = "Assign a Google Sheets Service Provider and a Spreadsheet Id to enable Google sync (Run All Setup, Setup Sheet IDs, Push/Pull).";
            else if (noProvider)
                msg = "Assign a Google Sheets Service Provider to enable Google sync (Apply Config, Push/Pull, Setup Sheet IDs).";
            else
                msg = "Set a Spreadsheet Id to enable Google sync (Run All Setup, Setup Sheet IDs, Push/Pull).";

            var alert = EditorUIElements.CreateAlert(msg, EditorUIElements.StatusType.Warning);
            alert.style.marginBottom = 6f;
            _alertSlot.Add(alert);
        }

        private void UpdateSummary()
        {
            int tables = TableMappings.Count(m => m.collection != null);
            int configured = TableMappings.Count(m => m.collection != null && HasGoogleSheetsExtension(m.collection));
            int missingLocales = CountMissingLocales();

            _summaryLabel.text =
                $"{tables} table{Plural(tables)} · {configured} configured · {missingLocales} missing locale{Plural(missingLocales)}";

            _openSheetButton.style.display =
                string.IsNullOrEmpty(_config.spreadsheetId) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdateButtonStates(LocalizationSetupConfig config)
        {
            bool canRunAll = config.googleSheetsServiceProvider != null &&
                             !string.IsNullOrEmpty(config.spreadsheetId);
            bool hasTables = config.tableCollections != null && config.tableCollections.Count > 0;

            _btnRunAllSetup.SetEnabled(canRunAll);
            _btnCreateMissing.SetEnabled(hasTables);
            _btnSetupSheetIds.SetEnabled(canRunAll && hasTables);
            _btnApplyConfig.SetEnabled(config.googleSheetsServiceProvider != null);
            _btnPushAll.SetEnabled(canRunAll && hasTables);
            _btnPullAll.SetEnabled(canRunAll && hasTables);
        }

        private void RebuildPerTableUI()
        {
            if (_perTableContainer == null) return;

            _perTableContainer.Clear();

            var withCollection = TableMappings.Where(m => m.collection != null).ToList();

            if (withCollection.Count == 0)
            {
                _perTableContainer.Add(BuildEmptyState());
                return;
            }

            string q = _searchQuery?.Trim() ?? "";
            var filtered = string.IsNullOrEmpty(q)
                ? withCollection
                : withCollection
                    .Where(m => m.collection.TableCollectionName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            if (filtered.Count == 0)
            {
                _perTableContainer.Add(EditorUIElements.CreateHintLabel($"No tables match '{q}'."));
                return;
            }

            foreach (var mapping in filtered)
                _perTableContainer.Add(CreateTableItem(_config, mapping));
        }

        private VisualElement BuildEmptyState()
        {
            var box = EditorUIElements.CreateBox();
            box.Add(EditorUIElements.CreateHintLabel(
                "No String Table Collections tracked yet. Run 'Find Tables' to scan the project."));

            var cta = EditorUIElements.CreateButton("Find Tables", () =>
            {
                FindAllStringTableCollections(_config);
                serializedObject.Update();
                Refresh();
            });
            cta.style.marginTop = 4f;
            box.Add(cta);

            return box;
        }

        private VisualElement CreateTableItem(LocalizationSetupConfig config, StringTableCollectionMapping mapping)
        {
            var box = EditorUIElements.CreateBox();
            box.style.marginBottom = 4f;

            GoogleSheetsExtension extension =
                mapping.collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
            bool configured = extension != null;
            bool canRunAll = config.googleSheetsServiceProvider != null &&
                             !string.IsNullOrEmpty(config.spreadsheetId);

            // Header: status dot + name (left), sheet-id badge (right).
            var header = EditorUIElements.CreateRow(Align.Center, Justify.SpaceBetween);

            var left = EditorUIElements.CreateRow(Align.Center);
            var dot = EditorUIElements.CreateStatusDot(
                configured ? EditorUIElements.StatusType.Success : EditorUIElements.StatusType.Error);
            dot.style.marginRight = 6f;
            dot.tooltip = configured
                ? "Configured — has a Google Sheets extension"
                : "Not configured — run Config (or All) to add a Google Sheets extension";
            left.Add(dot);
            left.Add(EditorUIElements.CreateHeaderLabel(mapping.collection.TableCollectionName));
            header.Add(left);

            header.Add(EditorUIElements.CreateBadge($"gid {mapping.sheetId}", EditorUIElements.BadgeStyle.Neutral));
            box.Add(header);

            // Setup row: All / Missing / Sheet / Config.
            var setupRow = EditorUIElements.CreateRow();
            setupRow.style.marginTop = 4f;

            var btnAll = Grow(SolidButton("All", EditorSolidColors.Success, EditorSolidColors.SuccessContent, () =>
            {
                CreateMissingLocaleTablesSingle(config, mapping);
                AutoSetupSheetIdSingle(config, mapping);
                SetupSingleCollection(config, mapping);
                PushSingle(mapping);
                Refresh();
            }));
            btnAll.SetEnabled(canRunAll || extension != null);

            var btnMissing = Grow(EditorUIElements.CreateButton("Missing", () =>
            {
                CreateMissingLocaleTablesSingle(config, mapping);
                Refresh();
            }));

            var btnSheet = Grow(EditorUIElements.CreateButton("Sheet", () =>
            {
                AutoSetupSheetIdSingle(config, mapping);
                Refresh();
            }));
            btnSheet.SetEnabled(canRunAll);

            var btnConfig = Grow(EditorUIElements.CreateButton("Config", () =>
            {
                SetupSingleCollection(config, mapping);
                Refresh();
            }));
            btnConfig.SetEnabled(config.googleSheetsServiceProvider != null);

            setupRow.Add(btnAll);
            setupRow.Add(btnMissing);
            setupRow.Add(btnSheet);
            setupRow.Add(btnConfig);
            box.Add(setupRow);

            // Sync row: Push / Pull.
            var syncRow = EditorUIElements.CreateRow();
            syncRow.style.marginTop = 2f;

            var btnPush = Grow(SolidButton("Push", EditorSolidColors.Info, EditorSolidColors.InfoContent,
                () => PushSingle(mapping)));
            btnPush.SetEnabled(extension != null);

            var btnPull = Grow(SolidButton("Pull", EditorSolidColors.Warning, EditorSolidColors.WarningContent, () =>
            {
                if (ConfirmPull($"Pull '{mapping.collection.TableCollectionName}' from Google Sheets?"))
                    PullSingle(mapping);
            }));
            btnPull.SetEnabled(extension != null);

            syncRow.Add(btnPush);
            syncRow.Add(btnPull);
            box.Add(syncRow);

            return box;
        }

        #endregion

        #region UI helpers

        private static Button SolidButton(string text, Color background, Color content, Action onClick)
        {
            var btn = EditorUIElements.CreateButton(text, onClick);
            btn.style.backgroundColor = background;
            btn.style.color = content;
            return btn;
        }

        private static T Grow<T>(T element) where T : VisualElement
        {
            element.style.flexGrow = 1;
            element.style.flexBasis = 0;
            return element;
        }

        private static string Plural(int n) => n == 1 ? "" : "s";

        private static bool HasGoogleSheetsExtension(StringTableCollection collection)
        {
            return collection.Extensions.OfType<GoogleSheetsExtension>().Any();
        }

        // Null-safe view of the mapping list (matches the guard UpdateButtonStates uses).
        private IEnumerable<StringTableCollectionMapping> TableMappings =>
            _config.tableCollections ?? Enumerable.Empty<StringTableCollectionMapping>();

        private int CountMissingLocales()
        {
            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
                return 0;

            int missing = 0;
            foreach (var mapping in TableMappings)
            {
                if (mapping.collection == null) continue;
                foreach (var locale in locales)
                {
                    if (!mapping.collection.ContainsTable(locale.Identifier))
                        missing++;
                }
            }

            return missing;
        }

        private void OpenSpreadsheet()
        {
            if (string.IsNullOrEmpty(_config.spreadsheetId))
                return;

            Application.OpenURL($"https://docs.google.com/spreadsheets/d/{_config.spreadsheetId}");
        }

        private bool ConfirmPull(string question)
        {
            string detail = _config.removeMissingPulledKeys
                ? "Remove Missing Pulled Keys is ON: local keys absent from the sheet will be DELETED."
                : "Remove Missing Pulled Keys is OFF: pulling will not delete local keys.";

            return EditorUtility.DisplayDialog("Pull from Google Sheets", $"{question}\n\n{detail}", "Pull", "Cancel");
        }

        private void AutoDetectLocales()
        {
            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                EditorUtility.DisplayDialog("Auto-detect Locales",
                    "No Locales found in the project. Add Locales via the Localization Tables window first.", "OK");
                return;
            }

            Undo.RecordObject(_config, "Auto-detect Locales");

            var existing = new HashSet<LocaleIdentifier>();
            foreach (var lc in _config.localeColumns)
            {
                if (lc.locale != null)
                    existing.Add(lc.locale.Identifier);
            }

            // Place new locale columns after the key column and any columns already mapped.
            int nextColumn = Math.Max(1, ColumnToIndex(_config.keyColumn));
            foreach (var lc in _config.localeColumns)
                nextColumn = Math.Max(nextColumn, ColumnToIndex(lc.column));

            int added = 0;
            foreach (var locale in locales)
            {
                if (locale == null || existing.Contains(locale.Identifier))
                    continue;

                nextColumn++;
                _config.localeColumns.Add(new LocaleColumnMapping
                {
                    locale = locale,
                    column = IndexToColumn(nextColumn),
                    includeComments = false
                });
                existing.Add(locale.Identifier);
                added++;
            }

            EditorUtility.SetDirty(_config);
            serializedObject.Update();
            Refresh();

            Debug.Log($"Auto-detect Locales: added {added} locale column{Plural(added)} " +
                      $"({locales.Count} project locale{Plural(locales.Count)} found).");
        }

        // Spreadsheet column <-> 1-based index (A=1, B=2, … Z=26, AA=27).
        private static int ColumnToIndex(string column)
        {
            if (string.IsNullOrEmpty(column))
                return 0;

            int index = 0;
            foreach (char c in column.ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') continue;
                index = index * 26 + (c - 'A' + 1);
            }

            return index;
        }

        private static string IndexToColumn(int index)
        {
            if (index < 1) index = 1;

            string result = "";
            while (index > 0)
            {
                int remainder = (index - 1) % 26;
                result = (char)('A' + remainder) + result;
                index = (index - 1) / 26;
            }

            return result;
        }

        #endregion

        #region Operations (unchanged behavior)

        private void FindAllStringTableCollections(LocalizationSetupConfig config)
        {
            string[] guids = AssetDatabase.FindAssets("t:StringTableCollection");
            StringTableCollection[] collections = guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<StringTableCollection>(path))
                .Where(collection => collection != null)
                .ToArray();

            Undo.RecordObject(config, "Find String Table Collections");

            var existingMappings = config.tableCollections.ToDictionary(m => m.collection, m => m.sheetId);
            config.tableCollections.Clear();

            foreach (var collection in collections)
            {
                int sheetId = existingMappings.TryGetValue(collection, out int existing) ? existing : 0;
                config.tableCollections.Add(new StringTableCollectionMapping
                {
                    collection = collection,
                    sheetId = sheetId
                });
            }

            EditorUtility.SetDirty(config);

            Debug.Log($"Found {collections.Length} String Table Collections");
        }

        private void CreateMissingLocaleTables(LocalizationSetupConfig config)
        {
            var projectLocales = LocalizationEditorSettings.GetLocales();
            if (projectLocales.Count == 0)
            {
                Debug.LogWarning("No locales found in project. Please add locales first.");
                return;
            }

            int createdCount = 0;
            int collectionCount = 0;

            try
            {
                for (int i = 0; i < config.tableCollections.Count; i++)
                {
                    var mapping = config.tableCollections[i];
                    if (mapping.collection == null) continue;

                    EditorUtility.DisplayProgressBar("Creating Missing Tables",
                        $"Processing {mapping.collection.TableCollectionName}...",
                        (float)i / config.tableCollections.Count);

                    bool collectionModified = false;

                    foreach (var locale in projectLocales)
                    {
                        if (!mapping.collection.ContainsTable(locale.Identifier))
                        {
                            mapping.collection.AddNewTable(locale.Identifier);
                            createdCount++;
                            collectionModified = true;
                        }
                    }

                    if (collectionModified)
                    {
                        EditorUtility.SetDirty(mapping.collection);
                        collectionCount++;
                    }
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Created {createdCount} missing locale tables across {collectionCount} collections");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CreateMissingLocaleTablesSingle(LocalizationSetupConfig config,
            StringTableCollectionMapping mapping)
        {
            var projectLocales = LocalizationEditorSettings.GetLocales();
            if (projectLocales.Count == 0)
            {
                Debug.LogWarning("No locales found in project. Please add locales first.");
                return;
            }

            int createdCount = 0;

            foreach (var locale in projectLocales)
            {
                if (!mapping.collection.ContainsTable(locale.Identifier))
                {
                    mapping.collection.AddNewTable(locale.Identifier);
                    createdCount++;
                }
            }

            if (createdCount > 0)
            {
                EditorUtility.SetDirty(mapping.collection);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created {createdCount} missing locale tables for {mapping.collection.TableCollectionName}");
            }
        }

        private void AutoSetupSheetIds(LocalizationSetupConfig config)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Auto Setup Sheet IDs", "Fetching sheets from Google...", 0);

                var googleSheets = new GoogleSheets(config.googleSheetsServiceProvider);
                googleSheets.SpreadSheetId = config.spreadsheetId;

                var existingSheets = googleSheets.GetSheets();
                var sheetLookup = existingSheets.ToDictionary(s => s.name, s => s.id);

                Undo.RecordObject(config, "Auto Setup Sheet IDs");

                int foundCount = 0;
                int createdCount = 0;

                for (int i = 0; i < config.tableCollections.Count; i++)
                {
                    var mapping = config.tableCollections[i];
                    if (mapping.collection == null) continue;

                    string sheetName = mapping.collection.TableCollectionName;
                    EditorUtility.DisplayProgressBar("Auto Setup Sheet IDs", $"Processing {sheetName}...",
                        (float)i / config.tableCollections.Count);

                    if (sheetLookup.TryGetValue(sheetName, out int sheetId))
                    {
                        mapping.sheetId = sheetId;
                        foundCount++;
                    }
                    else
                    {
                        int newSheetId = googleSheets.AddSheet(sheetName,
                            config.googleSheetsServiceProvider.NewSheetProperties);
                        mapping.sheetId = newSheetId;
                        sheetLookup[sheetName] = newSheetId;
                        createdCount++;
                    }
                }

                EditorUtility.SetDirty(config);
                Debug.Log($"Auto Setup Sheet IDs completed. Found: {foundCount}, Created: {createdCount}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Auto Setup Sheet IDs failed: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void SetupAllCollections(LocalizationSetupConfig config)
        {
            if (config.googleSheetsServiceProvider == null)
            {
                Debug.LogError("Google Sheets Service Provider is not assigned!");
                return;
            }

            int count = 0;
            foreach (var mapping in config.tableCollections)
            {
                if (mapping.collection == null) continue;

                GoogleSheetsExtension extension = GetOrCreateGoogleSheetsExtension(mapping.collection, config);
                if (extension != null)
                {
                    ConfigureGoogleSheetsExtension(extension, config, mapping);
                    EditorUtility.SetDirty(mapping.collection);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Setup completed. {count} collections configured with Google Sheets settings");
        }

        private void AutoSetupSheetIdSingle(LocalizationSetupConfig config, StringTableCollectionMapping mapping)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Setup Sheet ID",
                    $"Processing {mapping.collection.TableCollectionName}...", 0.5f);

                var googleSheets = new GoogleSheets(config.googleSheetsServiceProvider);
                googleSheets.SpreadSheetId = config.spreadsheetId;

                var existingSheets = googleSheets.GetSheets();
                string sheetName = mapping.collection.TableCollectionName;
                var existingSheet = existingSheets.FirstOrDefault(s => s.name == sheetName);

                Undo.RecordObject(config, "Setup Sheet ID");

                if (existingSheet != default)
                {
                    mapping.sheetId = existingSheet.id;
                    Debug.Log($"Found existing sheet '{sheetName}' with ID: {existingSheet.id}");
                }
                else
                {
                    int newSheetId =
                        googleSheets.AddSheet(sheetName, config.googleSheetsServiceProvider.NewSheetProperties);
                    mapping.sheetId = newSheetId;
                    Debug.Log($"Created new sheet '{sheetName}' with ID: {newSheetId}");
                }

                EditorUtility.SetDirty(config);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Setup Sheet ID failed for {mapping.collection.TableCollectionName}: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void SetupSingleCollection(LocalizationSetupConfig config, StringTableCollectionMapping mapping)
        {
            if (config.googleSheetsServiceProvider == null)
            {
                Debug.LogError("Google Sheets Service Provider is not assigned!");
                return;
            }

            GoogleSheetsExtension extension = GetOrCreateGoogleSheetsExtension(mapping.collection, config);
            if (extension != null)
            {
                ConfigureGoogleSheetsExtension(extension, config, mapping);
                EditorUtility.SetDirty(mapping.collection);
                AssetDatabase.SaveAssets();
                Debug.Log($"Configuration applied to {mapping.collection.TableCollectionName}");
            }
        }

        private void PushSingle(StringTableCollectionMapping mapping)
        {
            GoogleSheetsExtension extension =
                mapping.collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
            if (extension == null)
            {
                Debug.LogError($"No GoogleSheetsExtension found for {mapping.collection.name}");
                return;
            }

            try
            {
                var googleSheets = new GoogleSheets(extension.SheetsServiceProvider);
                googleSheets.SpreadSheetId = extension.SpreadsheetId;
                googleSheets.PushStringTableCollection(
                    extension.SheetId,
                    mapping.collection,
                    extension.Columns,
                    new ProgressBarReporter());
                Debug.Log($"Push completed for {mapping.collection.TableCollectionName}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to push {mapping.collection.name}: {ex.Message}");
            }
        }

        private void PullSingle(StringTableCollectionMapping mapping)
        {
            GoogleSheetsExtension extension =
                mapping.collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
            if (extension == null)
            {
                Debug.LogError($"No GoogleSheetsExtension found for {mapping.collection.name}");
                return;
            }

            try
            {
                var googleSheets = new GoogleSheets(extension.SheetsServiceProvider);
                googleSheets.SpreadSheetId = extension.SpreadsheetId;
                googleSheets.PullIntoStringTableCollection(
                    extension.SheetId,
                    mapping.collection,
                    extension.Columns,
                    extension.RemoveMissingPulledKeys,
                    new ProgressBarReporter());
                AssetDatabase.SaveAssets();
                Debug.Log($"Pull completed for {mapping.collection.TableCollectionName}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to pull {mapping.collection.name}: {ex.Message}");
            }
        }

        private GoogleSheetsExtension GetOrCreateGoogleSheetsExtension(StringTableCollection collection,
            LocalizationSetupConfig config)
        {
            GoogleSheetsExtension extension = collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();

            if (extension == null)
            {
                extension = new GoogleSheetsExtension
                {
                    SheetsServiceProvider = config.googleSheetsServiceProvider
                };
                collection.AddExtension(extension);
            }

            return extension;
        }

        private void ConfigureGoogleSheetsExtension(GoogleSheetsExtension extension, LocalizationSetupConfig config,
            StringTableCollectionMapping mapping)
        {
            extension.SheetsServiceProvider = config.googleSheetsServiceProvider;

            if (!string.IsNullOrEmpty(config.spreadsheetId))
                extension.SpreadsheetId = config.spreadsheetId;

            extension.SheetId = mapping.sheetId;

            extension.RemoveMissingPulledKeys = config.removeMissingPulledKeys;

            if (config.localeColumns.Count > 0)
            {
                extension.Columns.Clear();
                extension.Columns.Add(new KeyColumn { Column = config.keyColumn });

                foreach (var localeMapping in config.localeColumns)
                {
                    if (localeMapping.locale != null)
                    {
                        extension.Columns.Add(new LocaleColumn
                        {
                            Column = localeMapping.column,
                            LocaleIdentifier = localeMapping.locale.Identifier,
                            IncludeComments = localeMapping.includeComments
                        });
                    }
                }
            }
        }

        private void PullAll(LocalizationSetupConfig config)
        {
            int successCount = 0;
            int errorCount = 0;

            foreach (var mapping in config.tableCollections)
            {
                if (mapping.collection == null) continue;

                GoogleSheetsExtension extension =
                    mapping.collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
                if (extension == null)
                {
                    Debug.LogWarning($"Skipping {mapping.collection.name}: No GoogleSheetsExtension found");
                    continue;
                }

                try
                {
                    var googleSheets = new GoogleSheets(extension.SheetsServiceProvider);
                    googleSheets.SpreadSheetId = extension.SpreadsheetId;
                    googleSheets.PullIntoStringTableCollection(
                        extension.SheetId,
                        mapping.collection,
                        extension.Columns,
                        extension.RemoveMissingPulledKeys,
                        new ProgressBarReporter());
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to pull {mapping.collection.name}: {ex.Message}");
                    errorCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Pull completed. Success: {successCount}, Errors: {errorCount}");
        }

        private void PushAll(LocalizationSetupConfig config)
        {
            int successCount = 0;
            int errorCount = 0;

            foreach (var mapping in config.tableCollections)
            {
                if (mapping.collection == null) continue;

                GoogleSheetsExtension extension =
                    mapping.collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
                if (extension == null)
                {
                    Debug.LogWarning($"Skipping {mapping.collection.name}: No GoogleSheetsExtension found");
                    continue;
                }

                try
                {
                    var googleSheets = new GoogleSheets(extension.SheetsServiceProvider);
                    googleSheets.SpreadSheetId = extension.SpreadsheetId;
                    googleSheets.PushStringTableCollection(
                        extension.SheetId,
                        mapping.collection,
                        extension.Columns,
                        new ProgressBarReporter());
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to push {mapping.collection.name}: {ex.Message}");
                    errorCount++;
                }
            }

            Debug.Log($"Push completed. Success: {successCount}, Errors: {errorCount}");
        }

        #endregion
    }
}
