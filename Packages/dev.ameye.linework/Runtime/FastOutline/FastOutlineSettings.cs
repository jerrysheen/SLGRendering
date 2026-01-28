using System;
using System.Collections.Generic;
using Linework.Common.Utils;
using UnityEditor;
using UnityEngine;

namespace Linework.FastOutline
{
    [CreateAssetMenu(fileName = "Fast Outline Settings", menuName = "Linework/Fast Outline Settings")]
    [Icon("Packages/dev.ameye.linework/Editor/Common/Icons/d_FastOutline.png")]
    public class FastOutlineSettings : ScriptableObject
    {
        internal Action OnSettingsChanged;

        [SerializeField] private InjectionPoint injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private List<Outline> outlines = new(10);
        
        public InjectionPoint InjectionPoint => injectionPoint;
        public bool ShowInSceneView => showInSceneView;
        public List<Outline> Outlines => outlines;

        public void Changed()
        {
            OnSettingsChanged?.Invoke();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;
            OnSettingsChanged?.Invoke();
#endif
        }

        private void OnDestroy()
        {
            OnSettingsChanged = null;
            outlines = null;
        }
        
        public void SetActive(bool active)
        {
            foreach (var outline in outlines)
            {
                outline.SetActive(active);
            }
        }
        
#if UNITY_EDITOR
        private class OnDestroyProcessor: AssetModificationProcessor
        {
            private static readonly Type Type = typeof(FastOutlineSettings);
            private const string FileEnding = ".asset";

            public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions _)
            {
                if (!path.EndsWith(FileEnding))
                    return AssetDeleteResult.DidNotDelete;

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == null || assetType != Type && !assetType.IsSubclassOf(Type)) return AssetDeleteResult.DidNotDelete;
                var asset = AssetDatabase.LoadAssetAtPath<FastOutlineSettings>(path);
                foreach (var outline in asset.Outlines)
                {
                    outline.Cleanup();
                }
                asset.OnDestroy();

                return AssetDeleteResult.DidNotDelete;
            }
        }
#endif
    }
}