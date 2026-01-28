using System;
using System.Collections.Generic;
using Linework.FastOutline;
using UnityEditor;
using UnityEngine;

namespace Linework.Editor.FastOutline
{
    public static class ToggleSmoothNormals
    {
        private const string MenuPath = "Assets/Calculate Smoothed Normals";
 
        [MenuItem(MenuPath, true)]
        public static bool ValidateToggle()
        {
            if (Selection.activeObject is not GameObject && Selection.activeObject is not Mesh) return false;
            
            var labels = AssetDatabase.GetLabels(Selection.activeObject);
            Menu.SetChecked(MenuPath, Array.Exists(labels, label => label == FastOutlineUtils.SmoothNormalsLabel));
            return true;
        }

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            var selectedObject = Selection.activeObject;
            if (selectedObject == null) return;
            
            var labels = new List<string>(AssetDatabase.GetLabels(selectedObject));
            var previousLabels = new List<string>(labels);
            
            if (labels.Contains(FastOutlineUtils.SmoothNormalsLabel))
            {
                labels.Remove(FastOutlineUtils.SmoothNormalsLabel);
            }
            else
            {
                labels.Add(FastOutlineUtils.SmoothNormalsLabel);
            }
            
            Undo.RecordObject(selectedObject, "Toggle Smooth Normals Label");
            
            AssetDatabase.SetLabels(selectedObject, labels.ToArray());
      
            Undo.undoRedoPerformed += () =>
            {
                AssetDatabase.SetLabels(selectedObject, previousLabels.ToArray());
            };
        }
    }
}