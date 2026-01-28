using System;
using System.Text.RegularExpressions;
using Linework.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Linework.Editor.Common
{
    [CustomEditor(typeof(OutlineOverride))]
    public class OutlineOverrideEditor : UnityEditor.Editor
    {
        private ReorderableList reorderableList;

        private void OnEnable()
        {
            var overrides = serializedObject.FindProperty(nameof(OutlineOverride.overrides));

            reorderableList = new ReorderableList(serializedObject, overrides, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Overrides");
                },
                drawElementCallback = (rect, index, _, _) =>
                {
                    var element = overrides.GetArrayElementAtIndex(index);
                    DrawOverride(rect, element);
                },
                elementHeightCallback = index =>
                {
                    var element = overrides.GetArrayElementAtIndex(index);
                    return GetElementHeight(element);
                },
                onAddDropdownCallback = (_, _) =>
                {
                    var menu = new GenericMenu();
                    foreach (var propertyType in (ShaderPropertyType[])Enum.GetValues(typeof(ShaderPropertyType)))
                    {
                        menu.AddItem(new GUIContent(Regex.Replace(propertyType.ToString(), "(\\B[A-Z])", " $1")), false, () =>
                        {
                            AddOverride(overrides, propertyType);
                        });
                    }
                    menu.ShowAsContext();
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.Space();
            reorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private void AddOverride(SerializedProperty overrides, ShaderPropertyType type)
        {
            var index = overrides.arraySize;
            overrides.InsertArrayElementAtIndex(index);

            var newElement = overrides.GetArrayElementAtIndex(index);
            var typeProperty = newElement.FindPropertyRelative(nameof(ShaderPropertyOverride.type));
            var nameProperty = newElement.FindPropertyRelative(nameof(ShaderPropertyOverride.propertyName));
            var floatValueProperty = newElement.FindPropertyRelative(nameof(ShaderPropertyOverride.floatValue));
            var colorValueProperty = newElement.FindPropertyRelative(nameof(ShaderPropertyOverride.colorValue));
            var vectorValueProperty = newElement.FindPropertyRelative(nameof(ShaderPropertyOverride.vectorValue));

            // Assign defaults based on the selected type
            switch (type)
            {
                case ShaderPropertyType.Color:
                    typeProperty.enumValueIndex = (int)ShaderPropertyType.Color;
                    nameProperty.stringValue = "_MyColorProperty";
                    colorValueProperty.colorValue = Color.white;
                    break;
                case ShaderPropertyType.Float:
                    typeProperty.enumValueIndex = (int)ShaderPropertyType.Float;
                    nameProperty.stringValue = "_MyFloatProperty";
                    floatValueProperty.floatValue = 1.0f;
                    break;
                
                case ShaderPropertyType.Vector:
                    typeProperty.enumValueIndex = (int)ShaderPropertyType.Vector;
                    nameProperty.stringValue = "_MyVectorProperty";
                    vectorValueProperty.vector4Value = Vector4.one;
                    break;
                case ShaderPropertyType.Int:
                    typeProperty.enumValueIndex = (int)ShaderPropertyType.Int;
                    nameProperty.stringValue = "_MyIntProperty";
                    vectorValueProperty.intValue = 1;
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawOverride(Rect rect, SerializedProperty element)
        {
            var typeProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.type));
            var nameProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.propertyName));
            
            var typeWidth = 100;
            var nameWidth = rect.width - typeWidth - 5;

            // Draw type dropdown
            var typeRect = new Rect(rect.x, rect.y, typeWidth, EditorGUIUtility.singleLineHeight);
            var newType = (ShaderPropertyType)EditorGUI.EnumPopup(typeRect, (ShaderPropertyType)typeProperty.enumValueIndex);
            if (newType != (ShaderPropertyType)typeProperty.enumValueIndex)
            {
                typeProperty.enumValueIndex = (int)newType;
                UpdateDefaultsForType(element, newType);
            }

            // Draw property name
            var nameRect = new Rect(rect.x + typeWidth + 5, rect.y, nameWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, nameProperty, GUIContent.none);

            rect.y += EditorGUIUtility.singleLineHeight + 2;

            // Draw value field based on type
            switch (newType)
            {
                case ShaderPropertyType.Float:
                    var floatProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.floatValue));
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), floatProperty, new GUIContent("Float Value"));
                    break;
                
                case ShaderPropertyType.Int:
                    var intProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.intValue));
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), intProperty, new GUIContent("Int Value"));
                    break;

                case ShaderPropertyType.Color:
                    var colorProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.colorValue));
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), colorProperty, new GUIContent("Color Value"));
                    break;
                
                case ShaderPropertyType.Vector:
                    var vectorProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.vectorValue));
                    var vectorValue = vectorProperty.vector4Value;

                    // Create fields for each component of the Vector4
                    vectorValue = EditorGUI.Vector4Field(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), "Vector Value", vectorValue);

                    // Update the SerializedProperty with the modified Vector4
                    vectorProperty.vector4Value = vectorValue;
                    break;

                default:
                    EditorGUI.LabelField(rect, "Unsupported property type.");
                    break;
            }
        }

        private static void UpdateDefaultsForType(SerializedProperty element, ShaderPropertyType type)
        {
            var nameProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.propertyName));
            var floatValueProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.floatValue));
            var colorValueProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.colorValue));
            var vectorValueProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.vectorValue));
            var intValueProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.intValue));

            switch (type)
            {
                case ShaderPropertyType.Float:
                    nameProperty.stringValue = "_MyFloatProperty";
                    floatValueProperty.floatValue = 1.0f;
                    break;

                case ShaderPropertyType.Color:
                    nameProperty.stringValue = "_MyColorProperty";
                    colorValueProperty.colorValue = Color.white;
                    break;
                
                case ShaderPropertyType.Vector:
                    nameProperty.stringValue = "_MyVectorProperty";
                    vectorValueProperty.vector4Value = Vector4.one;
                    break;
                
                case ShaderPropertyType.Int:
                    nameProperty.stringValue = "_MyIntProperty";
                    intValueProperty.intValue = 1;
                    break;
            }
        }

        private static float GetElementHeight(SerializedProperty element)
        {
            var baseHeight = EditorGUIUtility.singleLineHeight + 4; // Base height for the name and type dropdown
            var typeProperty = element.FindPropertyRelative(nameof(ShaderPropertyOverride.type));
            if (typeProperty == null)
                return baseHeight;

            // Adjust height based on type
            var type = (ShaderPropertyType)typeProperty.enumValueIndex;
            switch (type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Int:
                case ShaderPropertyType.Color:
                    return baseHeight + EditorGUIUtility.singleLineHeight + 2;

                case ShaderPropertyType.Vector:
                    return baseHeight + (EditorGUIUtility.singleLineHeight + 2) * 2; // Vector requires extra space

                default:
                    return baseHeight;
            }
        }

    }
}
