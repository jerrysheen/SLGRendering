using System;
using System.Collections.Generic;
using Linework.Common.Utils;
using UnityEditor;
using UnityEngine;
using Resolution = Linework.Common.Utils.Resolution;

namespace Linework.WideOutline
{
    [CreateAssetMenu(fileName = "Wide Outline Settings", menuName = "Linework/Wide Outline Settings")]
    [Icon("Packages/dev.ameye.linework/Editor/Common/Icons/d_WideOutline.png")]
    public class WideOutlineSettings : ScriptableObject
    {
        internal Action OnSettingsChanged;

        [SerializeField] private InjectionPoint injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private List<Outline> outlines = new(10);
        
        // Shared settings.
        public MaterialType materialType;
        public Material customMaterial;
        public WidthControl widthControl = WidthControl.Shared;
        [Range(0.0f, 100.0f)] public float sharedWidth = 20.0f;
        [Range(0.0f, 1.0f)] public float gap = 0.0f;
        public BlendingMode blendMode;
        public bool customDepthBuffer;
        public SilhouetteBufferFormat silhouetteBufferFormat;
        [ColorUsage(true, true)] public Color occludedColor = Color.red;
        public bool clearStencil = false;
        public bool scaleWithResolution = false;
        public Resolution referenceResolution = Resolution._1080;
        public float customResolution;
        
        public InjectionPoint InjectionPoint => injectionPoint;
        public bool ShowInSceneView => showInSceneView;
        public List<Outline> Outlines => outlines;
        
        public void Changed()
        {
            foreach (var outline in outlines)
            {
                outline.SetAdvancedOcclusionEnabled(customDepthBuffer);
                outline.SetWidthControl(widthControl);
            }
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
            private static readonly Type Type = typeof(WideOutlineSettings);
            private const string FileEnding = ".asset";

            public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions _)
            {
                if (!path.EndsWith(FileEnding))
                    return AssetDeleteResult.DidNotDelete;

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == null || assetType != Type && !assetType.IsSubclassOf(Type)) return AssetDeleteResult.DidNotDelete;
                var asset = AssetDatabase.LoadAssetAtPath<WideOutlineSettings>(path);
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