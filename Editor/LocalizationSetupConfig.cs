using System;
using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;
using UnityEngine.Localization;

namespace CupkekGames.Localization.Editor
{
    [Serializable]
    public class StringTableCollectionMapping
    {
        public StringTableCollection collection;

        public int sheetId;
    }

    [CreateAssetMenu(fileName = "LocalizationSetupConfig", menuName = "Localization/Setup Config")]
    public class LocalizationSetupConfig : ScriptableObject
    {
        public SheetsServiceProvider googleSheetsServiceProvider;

        public string spreadsheetId = "";

        public string keyColumn = "A";

        public List<StringTableCollectionMapping> tableCollections = new List<StringTableCollectionMapping>();

        public List<LocaleColumnMapping> localeColumns = new List<LocaleColumnMapping>();

        public bool removeMissingPulledKeys = true;
    }

    [Serializable]
    public class LocaleColumnMapping
    {
        public string column = "B";

        public Locale locale;

        public bool includeComments = false;
    }
}
