#if !UNITY_6000_0_OR_NEWER
using Linework.Common.Attributes;
#endif
using Linework.Common.Utils;
using UnityEngine;

namespace Linework.SurfaceFill
{
    public class Fill : ScriptableObject
    {
        [SerializeField, HideInInspector] public Material material;
        [SerializeField, HideInInspector] private bool isActive = true;

#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint RenderingLayer = 1;
#endif
        public LayerMask layerMask = ~0;
        public OutlineRenderQueue renderQueue = OutlineRenderQueue.Opaque;
        public Occlusion occlusion = Occlusion.Always;
        public bool occludedBy = false;
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask OccludersRenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask]
        public uint OccludersRenderingLayer = 1;
#endif
        public BlendingMode blendMode = BlendingMode.Alpha;
        public bool alphaCutout;
        public Texture2D alphaCutoutTexture;
        [Range(0.0f, 1.0f)] public float alphaCutoutThreshold = 0.5f;
        public bool vertexAnimation;
        
        public MaterialType materialType;
        public Material customMaterial;
        public Pattern pattern = Pattern.Dots;
        [ColorUsage(true, true)] public Color primaryColor = Color.green;
        [ColorUsage(true, true)] public Color secondaryColor = Color.red;
        public Texture2D texture;
        public Channel channel;
        [Range(0.1f, 200.0f)] public float frequencyX = 40.0f;
        [Range(0.1f, 5.0f)] public float frequencyY = 2.0f;
        [Range(0.0f, 1.0f)] public float density = 0.5f;
        [Range(0.0f, 360.0f)] public float rotation;
        [Range(0.0f, 360.0f)] public float direction;
        [Range(0.0f, 1.0f)] public float offset;
        [Range(0.0f, 0.2f)] public float speed = 0.02f;
        [Range(0.1f, 100.0f)] public float scale = 1.0f;
        [Range(0.0f, 1.0f)] public float width = 0.3f;
        [Range(0.0f, 1.0f)] public float softness = 0.0f;
        [Range(0.0f, 2.0f)] public float power = 0.8f;

        private void OnEnable()
        {
            EnsureMaterialInitialized();
        }
        
        private void EnsureMaterialInitialized()
        {
            if (material == null)
            {
                var shader = Shader.Find(ShaderPath.Fill);
                if (shader != null)
                {
                    material = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
        }
        
        public void AssignMaterial(Material copyFrom)
        {
            EnsureMaterialInitialized();
            material.CopyPropertiesFromMaterial(copyFrom);
        }
        
        public bool IsActive()
        {
            return isActive;
        }
        
        public void SetActive(bool active)
        {
            isActive = active;
        }
        
        public void Cleanup()
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }
    }
}