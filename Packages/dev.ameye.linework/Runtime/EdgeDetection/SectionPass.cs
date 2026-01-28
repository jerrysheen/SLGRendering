#if !UNITY_6000_0_OR_NEWER
using Linework.Common.Attributes;
#endif
using UnityEngine;

namespace Linework.EdgeDetection
{
    [System.Serializable]
    public class SectionPass
    {
#if UNITY_6000_0_OR_NEWER
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
#else
        [RenderingLayerMask(false)]
        public uint RenderingLayer = 1;
#endif
        public Material customSectionMaterial;
    }
}