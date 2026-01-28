using UnityEngine;

namespace Linework.Common.Attributes
{
    public class RenderingLayerMaskAttribute : PropertyAttribute
    {
        public bool ShowLabel { get; }

        public RenderingLayerMaskAttribute(bool showLabel = true)
        {
            ShowLabel = showLabel;
        }
    }
}