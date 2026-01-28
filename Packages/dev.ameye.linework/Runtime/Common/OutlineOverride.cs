using System.Collections.Generic;
using UnityEngine;

namespace Linework.Common
{
    [RequireComponent(typeof(Renderer))]
    public class OutlineOverride : MonoBehaviour
    {
        public List<ShaderPropertyOverride> overrides = new();
        private MaterialPropertyBlock propertyBlock;
        
        public void AddFloatOverride(string propertyName, float value)
        {
            overrides.Add(new ShaderPropertyOverride
            {
                type = ShaderPropertyType.Float,
                propertyName = propertyName,
                floatValue = value
            });
        }
        
        public void AddIntOverride(string propertyName, int value)
        {
            overrides.Add(new ShaderPropertyOverride
            {
                type = ShaderPropertyType.Int,
                propertyName = propertyName,
                intValue = value
            });
        }
        
        public void AddColorOverride(string propertyName, Color color)
        {
            overrides.Add(new ShaderPropertyOverride
            {
                type = ShaderPropertyType.Color,
                propertyName = propertyName,
                colorValue = color
            });
        }
        
        public void AddVectorOverride(string propertyName, Vector4 value)
        {
            overrides.Add(new ShaderPropertyOverride
            {
                type = ShaderPropertyType.Vector,
                propertyName = propertyName,
                vectorValue = value
            });
        }

        private void Start()
        {
            SetOverrides();
        }

        private void OnValidate()
        {
            SetOverrides();
        }

        private void SetOverrides()
        {
            var rend = GetComponent<Renderer>();

            if (!enabled)
            {
                rend.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            

            foreach (var propertyOverride in overrides)
            {
                propertyOverride.CachePropertyID();
                
                switch (propertyOverride.type)
                {
                    case ShaderPropertyType.Float:
                        propertyBlock.SetFloat(propertyOverride.propertyId, propertyOverride.floatValue);
                        break;
                    case ShaderPropertyType.Int:
                        propertyBlock.SetInt(propertyOverride.propertyId, propertyOverride.intValue);
                        break;
                    case ShaderPropertyType.Vector:
                        propertyBlock.SetVector(propertyOverride.propertyId, propertyOverride.vectorValue);
                        break;
                    case ShaderPropertyType.Color:
                        propertyBlock.SetColor(propertyOverride.propertyId, propertyOverride.colorValue);
                        break;
                    default:
                        Debug.LogWarning($"Unsupported shader property type: {propertyOverride.type}");
                        break;
                }
            }
            
            rend.SetPropertyBlock(propertyBlock);
        }
    }
}