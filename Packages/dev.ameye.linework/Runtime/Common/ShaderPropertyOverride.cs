using System;
using UnityEngine;

namespace Linework.Common
{
    public enum ShaderPropertyType
    {
        Float,
        Int,
        Vector,
        Color
    }

    [Serializable]
    public class ShaderPropertyOverride
    {
        public ShaderPropertyType type;
        public string propertyName;
        [HideInInspector] public int propertyId;
        
        public float floatValue;
        public int intValue;
        public Vector4 vectorValue;
        public Color colorValue = Color.white;

        public void CachePropertyID()
        {
            propertyId = Shader.PropertyToID(propertyName);
        }

        public object GetValue()
        {
            return type switch
            {
                ShaderPropertyType.Float => floatValue,
                ShaderPropertyType.Int => intValue,
                ShaderPropertyType.Vector => vectorValue,
                ShaderPropertyType.Color => colorValue,
                _ => null
            };
        }
    }
}