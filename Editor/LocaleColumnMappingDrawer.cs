using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CupkekGames.Localization.Editor
{
    [CustomPropertyDrawer(typeof(LocaleColumnMapping))]
    public class LocaleColumnMappingDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom = 2;
            
            var columnProp = property.FindPropertyRelative("column");
            var localeProp = property.FindPropertyRelative("locale");
            var includeCommentsProp = property.FindPropertyRelative("includeComments");
            
            var columnField = new PropertyField(columnProp, "");
            columnField.style.width = 40;
            
            var localeField = new PropertyField(localeProp, "");
            localeField.style.flexGrow = 1;
            localeField.style.flexBasis = 0;
            localeField.style.marginLeft = 4;
            
            var commentsField = new PropertyField(includeCommentsProp, "Comments");
            commentsField.style.width = 80;
            commentsField.style.marginLeft = 4;
            
            container.Add(columnField);
            container.Add(localeField);
            container.Add(commentsField);
            
            return container;
        }
    }
}
