using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CupkekGames.Localization.Editor
{
    [CustomPropertyDrawer(typeof(StringTableCollectionMapping))]
    public class StringTableCollectionMappingDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom = 2;
            
            var collectionProp = property.FindPropertyRelative("collection");
            var sheetIdProp = property.FindPropertyRelative("sheetId");
            
            var collectionField = new PropertyField(collectionProp, "");
            collectionField.style.flexGrow = 1;
            collectionField.style.flexBasis = 0;
            
            var sheetIdField = new PropertyField(sheetIdProp, "Sheet ID");
            sheetIdField.style.width = 120;
            sheetIdField.style.marginLeft = 4;
            
            container.Add(collectionField);
            container.Add(sheetIdField);
            
            return container;
        }
    }
}
