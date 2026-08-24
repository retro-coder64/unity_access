using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityAccess
{
    /// <summary>Isolates the Unity 6.3 TagManager serialization used when no public mutation API exists.</summary>
    internal static class ProjectSettingsTagManager
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        private static readonly HashSet<string> BuiltInTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController"
        };

        internal static int TagCount { get { return GetArray("tags").arraySize; } }
        internal static int SortingLayerCount { get { return GetArray("m_SortingLayers").arraySize; } }
        internal static IReadOnlyList<string> GetTags() { return ReadNames("tags", null); }
        internal static IReadOnlyList<string> GetSortingLayers() { return ReadNames("m_SortingLayers", "name"); }
        internal static IReadOnlyList<string> GetLayers() { return ReadNames("layers", null); }
        internal static bool CanRemoveTag(string tag) { return !BuiltInTags.Contains(tag); }
        internal static bool CanEditLayer(int index) { return index >= 8 && index < 32; }

        internal static void AddTag(string name)
        {
            string validName = ValidateUniqueName(name, GetTags(), "tag");
            SerializedObject manager = OpenManager();
            SerializedProperty tags = RequireArray(manager, "tags");
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = validName;
            Save(manager);
        }

        internal static void RemoveTag(string name)
        {
            if (!CanRemoveTag(name))
            {
                throw new InvalidOperationException(name + " is a built-in tag and cannot be removed.");
            }

            SerializedObject manager = OpenManager();
            SerializedProperty tags = RequireArray(manager, "tags");
            for (int index = 0; index < tags.arraySize; index++)
            {
                if (string.Equals(tags.GetArrayElementAtIndex(index).stringValue, name, StringComparison.Ordinal))
                {
                    tags.DeleteArrayElementAtIndex(index);
                    Save(manager);
                    return;
                }
            }

            throw new InvalidOperationException("The tag " + name + " no longer exists.");
        }

        internal static void AddSortingLayer(string name)
        {
            string validName = ValidateUniqueName(name, GetSortingLayers(), "Sorting Layer");
            SerializedObject manager = OpenManager();
            SerializedProperty layers = RequireArray(manager, "m_SortingLayers");
            layers.InsertArrayElementAtIndex(layers.arraySize);
            SerializedProperty added = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            RequireRelative(added, "name").stringValue = validName;
            RequireRelative(added, "uniqueID").longValue = GenerateSortingLayerId(layers);
            SerializedProperty locked = added.FindPropertyRelative("locked");
            if (locked != null)
            {
                locked.boolValue = false;
            }

            Save(manager);
        }

        internal static void RenameSortingLayer(int index, string name)
        {
            RequireEditableSortingLayer(index);
            List<string> otherNames = new List<string>(GetSortingLayers());
            otherNames.RemoveAt(index);
            string validName = ValidateUniqueName(name, otherNames, "Sorting Layer");
            SerializedObject manager = OpenManager();
            SerializedProperty layers = RequireArray(manager, "m_SortingLayers");
            RequireIndex(layers, index);
            RequireRelative(layers.GetArrayElementAtIndex(index), "name").stringValue = validName;
            Save(manager);
        }

        internal static void RemoveSortingLayer(int index)
        {
            RequireEditableSortingLayer(index);
            SerializedObject manager = OpenManager();
            SerializedProperty layers = RequireArray(manager, "m_SortingLayers");
            RequireIndex(layers, index);
            layers.DeleteArrayElementAtIndex(index);
            Save(manager);
        }

        internal static void MoveSortingLayer(int index, int direction)
        {
            int destination = index + direction;
            if (index <= 0 || destination <= 0)
            {
                throw new InvalidOperationException("The Default Sorting Layer cannot be moved.");
            }

            SerializedObject manager = OpenManager();
            SerializedProperty layers = RequireArray(manager, "m_SortingLayers");
            RequireIndex(layers, index);
            RequireIndex(layers, destination);
            layers.MoveArrayElement(index, destination);
            Save(manager);
        }

        internal static void SetLayerName(int index, string name)
        {
            if (!CanEditLayer(index))
            {
                throw new InvalidOperationException("Layer " + index + " is protected and cannot be changed.");
            }

            string validName = name.Trim();
            IReadOnlyList<string> existing = GetLayers();
            for (int existingIndex = 0; existingIndex < existing.Count; existingIndex++)
            {
                if (existingIndex != index && !string.IsNullOrEmpty(validName) &&
                    string.Equals(existing[existingIndex], validName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A layer named " + validName + " already exists.");
                }
            }

            SerializedObject manager = OpenManager();
            SerializedProperty layers = RequireArray(manager, "layers");
            RequireIndex(layers, index);
            layers.GetArrayElementAtIndex(index).stringValue = validName;
            Save(manager);
        }

        private static IReadOnlyList<string> ReadNames(string propertyName, string relativeName)
        {
            SerializedProperty array = GetArray(propertyName);
            List<string> values = new List<string>(array.arraySize);
            for (int index = 0; index < array.arraySize; index++)
            {
                SerializedProperty item = array.GetArrayElementAtIndex(index);
                values.Add(relativeName == null ? item.stringValue : RequireRelative(item, relativeName).stringValue);
            }

            return values;
        }

        private static SerializedProperty GetArray(string propertyName)
        {
            return RequireArray(OpenManager(), propertyName);
        }

        private static SerializedObject OpenManager()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets.Length == 0 || assets[0] == null)
            {
                throw new InvalidOperationException("Unity could not load ProjectSettings/TagManager.asset.");
            }

            SerializedObject manager = new SerializedObject(assets[0]);
            manager.Update();
            return manager;
        }

        private static SerializedProperty RequireArray(SerializedObject manager, string name)
        {
            SerializedProperty property = manager.FindProperty(name);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException("Unity 6.3 TagManager property " + name + " was not available.");
            }

            return property;
        }

        private static SerializedProperty RequireRelative(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property == null)
            {
                throw new InvalidOperationException("Unity 6.3 Sorting Layer property " + name + " was not available.");
            }

            return property;
        }

        private static void RequireIndex(SerializedProperty array, int index)
        {
            if (index < 0 || index >= array.arraySize)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static void RequireEditableSortingLayer(int index)
        {
            if (index <= 0)
            {
                throw new InvalidOperationException("The Default Sorting Layer is protected.");
            }
        }

        private static string ValidateUniqueName(string name, IReadOnlyList<string> existing, string kind)
        {
            string validName = name.Trim();
            if (string.IsNullOrEmpty(validName))
            {
                throw new InvalidOperationException(kind + " name cannot be empty.");
            }

            for (int index = 0; index < existing.Count; index++)
            {
                if (string.Equals(existing[index], validName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A " + kind + " named " + validName + " already exists.");
                }
            }

            return validName;
        }

        private static long GenerateSortingLayerId(SerializedProperty layers)
        {
            HashSet<long> ids = new HashSet<long>();
            for (int index = 0; index < layers.arraySize - 1; index++)
            {
                ids.Add(RequireRelative(layers.GetArrayElementAtIndex(index), "uniqueID").longValue);
            }

            long candidate;
            do
            {
                candidate = (uint)Guid.NewGuid().GetHashCode();
            }
            while (candidate == 0 || ids.Contains(candidate));
            return candidate;
        }

        private static void Save(SerializedObject manager)
        {
            if (!manager.ApplyModifiedProperties())
            {
                throw new InvalidOperationException("Unity rejected the TagManager change.");
            }

            EditorUtility.SetDirty(manager.targetObject);
            AssetDatabase.SaveAssets();
        }
    }
}
