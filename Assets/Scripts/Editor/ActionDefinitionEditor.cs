using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionDefinition))]
public sealed class ActionDefinitionEditor : Editor
{
    private const string EffectsPropertyName = "<Effects>k__BackingField";

    private static List<Type> _effectTypes;
    private static string[] _effectTypeNames;

    private int _selectedEffectTypeIndex;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultProperties();
        DrawEffectsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultProperties()
    {
        var iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script" || iterator.propertyPath == EffectsPropertyName)
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    private void DrawEffectsSection()
    {
        var effectsProp = serializedObject.FindProperty(EffectsPropertyName);
        if (effectsProp == null || !effectsProp.isArray)
        {
            EditorGUILayout.HelpBox("Could not find Effects property.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);

        for (int i = 0; i < effectsProp.arraySize; i++)
        {
            var effectProp = effectsProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            string typeLabel = GetEffectLabel(effectProp);
            EditorGUILayout.LabelField($"{i + 1}. {typeLabel}", EditorStyles.boldLabel);

            GUI.enabled = i > 0;
            if (GUILayout.Button("Up", GUILayout.Width(40)))
            {
                effectsProp.MoveArrayElement(i, i - 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            GUI.enabled = i < effectsProp.arraySize - 1;
            if (GUILayout.Button("Down", GUILayout.Width(52)))
            {
                effectsProp.MoveArrayElement(i, i + 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            GUI.enabled = true;
            if (GUILayout.Button("Remove", GUILayout.Width(68)))
            {
                effectsProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(effectProp, true);
            EditorGUILayout.EndVertical();
        }

        EnsureTypeCache();
        if (_effectTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("No EffectConfig types were found.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4f);
        _selectedEffectTypeIndex = EditorGUILayout.Popup("New Effect Type", _selectedEffectTypeIndex, _effectTypeNames);

        if (GUILayout.Button("Add Effect"))
        {
            var selectedType = _effectTypes[_selectedEffectTypeIndex];
            effectsProp.arraySize++;

            var newElement = effectsProp.GetArrayElementAtIndex(effectsProp.arraySize - 1);
            newElement.managedReferenceValue = Activator.CreateInstance(selectedType);
            newElement.isExpanded = true;
        }
    }

    private static string GetEffectLabel(SerializedProperty effectProp)
    {
        if (effectProp.managedReferenceValue is EffectConfig effect)
            return effect.DisplayName;

        string fullTypeName = effectProp.managedReferenceFullTypename;
        if (string.IsNullOrEmpty(fullTypeName))
            return "Effect";

        int splitIndex = fullTypeName.LastIndexOf('.');
        return splitIndex >= 0 ? fullTypeName.Substring(splitIndex + 1) : fullTypeName;
    }

    private static void EnsureTypeCache()
    {
        if (_effectTypes != null)
            return;

        _effectTypes = TypeCache.GetTypesDerivedFrom<EffectConfig>()
            .Where(t => !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name)
            .ToList();

        _effectTypeNames = _effectTypes
            .Select(type => ObjectNames.NicifyVariableName(type.Name.Replace("EffectConfig", string.Empty)))
            .ToArray();
    }
}