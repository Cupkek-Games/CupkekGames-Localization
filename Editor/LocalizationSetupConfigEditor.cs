using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using System.Linq;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEditor.Localization.Reporting;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace CupkekGames.Localization.Editor
{
    [CustomEditor(typeof(LocalizationSetupConfig))]
    public class LocalizationSetupConfigEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset uxmlAsset = default;

        private VisualElement root;
        private VisualElement perTableContainer;
        private Foldout perTableFoldout;
        private Button btnRunAllSetup;
        private Button btnFindTables;
        private Button btnCreateMissing;
        private Button btnSetupSheetIds;
        private Button btnApplyConfig;
        private Button btnPushAll;
        private Button btnPullAll;

        public override VisualElement CreateInspectorGUI()
        {
            LocalizationSetupConfig config = (LocalizationSetupConfig)target;

            root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            if (uxmlAsset != null)
            {
                var customUI = uxmlAsset.Instantiate();
                root.Add(customUI);

                CacheUIElements(customUI);
                RegisterCallbacks(config);
                UpdateButtonStates(config);
                RebuildPerTableUI(config);
            }

            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                UpdateButtonStates(config);
                RebuildPerTableUI(config);
            });

            return root;
        }

        private void CacheUIElements(VisualElement customUI)
        {
            btnRunAllSetup = customUI.Q<Button>("btn-run-all-setup");
            btnFindTables = customUI.Q<Button>("btn-find-tables");
            btnCreateMissing = customUI.Q<Button>("btn-create-missing");
            btnSetupSheetIds = customUI.Q<Button>("btn-setup-sheet-ids");
            btnApplyConfig = customUI.Q<Button>("btn-apply-config");
            btnPushAll = customUI.Q<Button>("btn-push-all");
            btnPullAll = customUI.Q<Button>("btn-pull-all");
            perTableFoldout = customUI.Q<Foldout>("per-table-foldout");
            perTableContainer = customUI.Q<VisualElement>("per-table-container");
        }

        private void RegisterCallbacks(LocalizationSetupConfig config)
        {
            btnRunAllSetup.clicked += () =>
            {
                FindAllStringTableCollections(config);
                CreateMissingLocaleTables(config);
                AutoSetupSheetIds(config);
                SetupAllCollections(config);
                serializedObject.Update();
                UpdateButtonStates(config);
                RebuildPerTableUI(config);
            };

            btnFindTables.clicked += () =>
            {
                FindAllStringTableCollections(config);
                serializedObject.Update();
                UpdateButtonStates(config);
                RebuildPerTableUI(config);
            };

            btnCreateMissing.clicked += () =>
            {
                CreateMissingLocaleTables(config);
                serializedObject.Update();
            };

            btnSetupSheetIds.clicked += () =>
            {
                AutoSetupSheetIds(config);
                serializedObject.Update();
                RebuildPerTableUI(config);
            };

            btnApplyConfig.clicked += () =>
            {
                SetupAllCollections(config);
                serializedObject.Update();
            };

            btnPushAll.clicked += () => PushAll(config);
            btnPullAll.clicked += () => PullAll(config);
        }

        private void UpdateButtonStates(LocalizationSetupConfig config)
        {
            bool canRunAll = config.googleSheetsServiceProvider != null &&
                             !string.IsNullOrEmpty(config.spreadsheetId);
            bool hasTables = config.tableCollections != null && config.tableCollections.Count > 0;

            btnRunAllSetup.SetEnabled(canRunAll);
            btnCreateMissing.SetEnabled(hasTables);
            btnSetupSheetIds.SetEnabled(canRunAll && hasTables);
            btnApplyConfig.SetEnabled(config.googleSheetsServiceProvider != null);
            btnPushAll.SetEnabled(canRunAll && hasTables);
            btnPullAll.SetEnabled(canRunAll && hasTables);
        }

        private void RebuildPerTableUI(LocalizationSetupConfig config)
        {
            if (perTableContainer == null) return;

            perTableContainer.Clear();

            if (config.tableCollections == null || config.tableCollections.Count == 0)
                return;

            foreach (var mapping in config.tableCollections)
            {
                if (mapping.collection == null) continue;

                var tableItem = CreateTableItem(config, mapping);
                perTableContainer.Add(tableItem);
            }
        }

        private VisualElement CreateTableItem(LocalizationSetupConfig config, StringTableCollectionMapping mapping)
        {
            var container = new VisualElement();
            container.AddToClassList("table-item");

            var header = new VisualElement();
            header.AddToClassList("table-item-header");

            var nameLabel = new Label(mapping.collection.TableCollectionName);
            nameLabel.AddToClassList("table-item-name");
            header.Add(nameLabel);

            var idLabel = new Label($"ID: {mapping.sheetId}");
            idLabel.AddToClassList("table-item-id");
            header.Add(idLabel);

            container.Add(header);

            var buttonsRow = new VisualElement();
            buttonsRow.AddToClassList("table-item-buttons");

            GoogleSheetsExtension extension =
                mapping.collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
            bool canRunAll = config.googleSheetsServiceProvider != null &&
                             !string.IsNullOrEmpty(config.spreadsheetId);

            var btnAll = new Button(() =>
            {
                CreateMissingLocaleTablesSingle(config, mapping);
                AutoSetupSheetIdSingle(config, mapping);
                SetupSingleCollection(config, mapping);
                PushSingle(mapping);
                RebuildPerTableUI(config);
            }) { text = "All" };
            btnAll.AddToClassList("btn-all");
            btnAll.SetEnabled(canRunAll || extension != null);
            buttonsRow.Add(btnAll);

            var btnMissing = new Button(() => { CreateMissingLocaleTablesSingle(config, mapping); })
                { text = "Missing" };
            btnMissing.AddToClassList("btn-missing");
            buttonsRow.Add(btnMissing);

            var btnSheet = new Button(() =>
            {
                AutoSetupSheetIdSingle(config, mapping);
                RebuildPerTableUI(config);
            }) { text = "Sheet" };
            btnSheet.AddToClassList("btn-sheet");
            btnSheet.SetEnabled(canRunAll);
            buttonsRow.Add(btnSheet);

            var btnConfig = new Button(() => { SetupSingleCollection(config, mapping); }) { text = "Config" };
            btnConfig.AddToClassList("btn-config");
            btnConfig.SetEnabled(config.googleSheetsServiceProvider != null);
            buttonsRow.Add(btnConfig);

            container.Add(buttonsRow);

            var syncRow = new VisualElement();
            syncRow.AddToClassList("table-item-sync-buttons");

            var btnPush = new Button(() => PushSingle(mapping)) { text = "Push" };
            btnPush.AddToClassList("btn-push");
            btnPush.SetEnabled(extension != null);
            syncRow.Add(btnPush);

            var btnPull = new Button(() => PullSingle(mapping)) { text = "Pull" };
            btnPull.AddToClassList("btn-pull");
            btnPull.SetEnabled(extension != null);
            syncRow.Add(btnPull);

            container.Add(syncRow);

            return container;
        }

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
    }
}
