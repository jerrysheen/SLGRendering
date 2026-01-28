#if !UNITY_6000_0_OR_NEWER
using Linework.Common.Attributes;
#endif
using Linework.Common.Utils;
using UnityEngine;

namespace Linework.WideOutline
{
    public class Outline : ScriptableObject
    {
        [SerializeField, HideInInspector] public Material silhouetteMaterial;
        [SerializeField, HideInInspector] public Material silhouetteMaterialInstanced;
        [SerializeField, HideInInspector] public Material informationMaterial;
        [SerializeField, HideInInspector] public Material informationMaterialInstanced;
        [SerializeField, HideInInspector] private bool isActive = true;
        [SerializeField, HideInInspector] private bool customDepthEnabled = true;
        [SerializeField, HideInInspector] private bool disableWidthControl = true;
        
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint RenderingLayer = 1;
#endif
        public LayerMask layerMask = ~0;
        public OutlineRenderQueue renderQueue = OutlineRenderQueue.Opaque;
        public WideOutlineOcclusion occlusion = WideOutlineOcclusion.Always;
        public CullingMode cullingMode = CullingMode.Back;
        public bool closedLoop;
        public bool alphaCutout;
        public Texture2D alphaCutoutTexture;
        [Range(0.0f, 1.0f)] public float alphaCutoutThreshold = 0.5f;
        public Vector4 alphaCutoutUVTransform = Vector4.zero;
        public bool gpuInstancing;
        public bool vertexAnimation;
        
        [ColorUsage(true, true)] public Color color = Color.green;
        [Range(0.0f, 100.0f)] public float width = 20.0f;
        
        private void OnEnable()
        {
            EnsureMaterialsAreInitialized();
        }

        private void EnsureMaterialsAreInitialized()
        {
            if (silhouetteMaterial == null)
            {
                var shader = Shader.Find(ShaderPath.Silhouette);
                if (shader != null)
                {
                    silhouetteMaterial = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            
            if (silhouetteMaterialInstanced == null)
            {
                var shader = Shader.Find(ShaderPath.SilhouetteInstanced);
                if (shader != null)
                {
                    silhouetteMaterialInstanced = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        enableInstancing = true
                    };
                }
            }
            
            if (informationMaterial == null)
            {
                var shader = Shader.Find(ShaderPath.Silhouette);
                if (shader != null)
                {
                    informationMaterial = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            
            if (informationMaterialInstanced == null)
            {
                var shader = Shader.Find(ShaderPath.SilhouetteInstanced);
                if (shader != null)
                {
                    informationMaterialInstanced = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        enableInstancing = true
                    };
                }
            }
        }

        public void AssignMaterials(Material source, Material sourceInstanced)
        {
            EnsureMaterialsAreInitialized();
            
            silhouetteMaterial.CopyPropertiesFromMaterial(source);
            silhouetteMaterialInstanced.CopyPropertiesFromMaterial(sourceInstanced);
            silhouetteMaterialInstanced.enableInstancing = gpuInstancing;
            informationMaterial.CopyPropertiesFromMaterial(source);
            informationMaterialInstanced.CopyPropertiesFromMaterial(sourceInstanced);
            informationMaterialInstanced.enableInstancing = gpuInstancing;
        }
        
        public bool IsActive()
        {
            return isActive;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }

        public void SetAdvancedOcclusionEnabled(bool enable)
        {
            customDepthEnabled = enable;
        }

        public void SetWidthControl(WidthControl control)
        {
            disableWidthControl = control == WidthControl.Shared;
        }
        
        public void Cleanup()
        {
            if (silhouetteMaterial != null)
            {
                DestroyImmediate(silhouetteMaterial);
                silhouetteMaterial = null;
            }
            
            if (silhouetteMaterialInstanced != null)
            {
                DestroyImmediate(silhouetteMaterialInstanced);
                silhouetteMaterialInstanced = null;
            }
            
            if (informationMaterial != null)
            {
                DestroyImmediate(informationMaterial);
                informationMaterial = null;
            }
            
            if (informationMaterialInstanced != null)
            {
                DestroyImmediate(informationMaterialInstanced);
                informationMaterialInstanced = null;
            }
        }
    }
}