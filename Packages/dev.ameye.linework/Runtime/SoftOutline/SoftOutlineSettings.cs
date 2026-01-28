using System;
using System.Collections.Generic;
using Linework.Common.Utils;
using UnityEditor;
using UnityEngine;
using Resolution = Linework.Common.Utils.Resolution;

namespace Linework.SoftOutline
{
    [CreateAssetMenu(fileName = "Soft Outline Settings", menuName = "Linework/Soft Outline Settings")]
    [Icon("Packages/dev.ameye.linework/Editor/Common/Icons/d_SoftOutline.png")]
    public class SoftOutlineSettings : ScriptableObject
    {
        internal Action OnSettingsChanged;

        [SerializeField] private InjectionPoint injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private List<Outline> outlines = new(10);

        // Shared settings.
        public OutlineType type = OutlineType.Soft;
        [Range(0.0f, 1.0f)] public float hardness = 1;
        [ColorUsage(true, true)] public Color sharedColor = Color.green;
        [Range(0.1f, 30.0f)] public float intensity = 1.2f;
        [Range(0.0f, 1.0f)] public float gap = 0.0f;
        public BlendingMode blendMode = BlendingMode.Additive;
        public DilationMethod dilationMethod = DilationMethod.Dilate;
        [Range(0, 50)] public int kernelSize = 20;
        [Range(0.5f, 50.0f)] public float blurSpread = 1.35f;
        [Range(2, 10)] public int blurPasses = 2;
        public bool scaleWithResolution = true;
        public Resolution referenceResolution = Resolution._1080;
        public float customResolution;
        
        public InjectionPoint InjectionPoint => injectionPoint;
        public bool ShowInSceneView => showInSceneView;
        public List<Outline> Outlines => outlines;

        public void Changed()
        {
            foreach (var outline in outlines)
            {
                outline.SetOutlineType(type);
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
        private class OnDestroyProcessor : AssetModificationProcessor
        {
            private static readonly Type Type = typeof(SoftOutlineSettings);
            private const string FileEnding = ".asset";

            public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions _)
            {
                if (!path.EndsWith(FileEnding))
                    return AssetDeleteResult.DidNotDelete;

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == null || assetType != Type && !assetType.IsSubclassOf(Type)) return AssetDeleteResult.DidNotDelete;
                var asset = AssetDatabase.LoadAssetAtPath<SoftOutlineSettings>(path);
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