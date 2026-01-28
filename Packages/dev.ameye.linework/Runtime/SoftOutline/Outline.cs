#if !UNITY_6000_0_OR_NEWER
using Linework.Common.Attributes;
#endif
using Linework.Common.Utils;
using UnityEngine;

namespace Linework.SoftOutline
{
    public class Outline : ScriptableObject
    {
        [SerializeField, HideInInspector] public Material material;
        [SerializeField, HideInInspector] public Material materialInstanced;
        [SerializeField, HideInInspector] private bool isActive = true;
        [SerializeField, HideInInspector] private bool disableColor = true;
        
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint RenderingLayer = 1;
#endif
        public LayerMask layerMask = ~0;
        public OutlineRenderQueue renderQueue = OutlineRenderQueue.Opaque;
        public SoftOutlineOcclusion occlusion = SoftOutlineOcclusion.Always;
        public CullingMode cullingMode = CullingMode.Back;
        public bool closedLoop;
        public bool alphaCutout;
        public Texture2D alphaCutoutTexture;
        [Range(0.0f, 1.0f)] public float alphaCutoutThreshold = 0.5f;
        public Vector4 alphaCutoutUVTransform = Vector4.zero;
        public bool gpuInstancing;
        public bool vertexAnimation;
        
        [ColorUsage(true, true)] public Color color = Color.green;
        
        private void OnEnable()
        {
            EnsureMaterialsAreInitialized();
        }
        
        private void EnsureMaterialsAreInitialized()
        {
            if (material == null)
            {
                var shader = Shader.Find(ShaderPath.Silhouette);
                if (shader != null)
                {
                    material = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            
            if (materialInstanced == null)
            {
                var shader = Shader.Find(ShaderPath.SilhouetteInstanced);
                if (shader != null)
                {
                    materialInstanced = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        enableInstancing = true
                    };
                }
            }
        }

        public void AssignMaterials(Material copyFrom, Material copyFromInstanced)
        {
            EnsureMaterialsAreInitialized();
            
            material.CopyPropertiesFromMaterial(copyFrom);
            materialInstanced.CopyPropertiesFromMaterial(copyFromInstanced);
            materialInstanced.enableInstancing = gpuInstancing;
        }
        
        public bool IsActive()
        {
            return isActive;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }

        public void SetOutlineType(OutlineType type)
        {
            disableColor = type == OutlineType.Hard;
        }
        
        public void Cleanup()
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
            
            if (materialInstanced != null)
            {
                DestroyImmediate(materialInstanced);
                materialInstanced = null;
            }
        }
    }
}