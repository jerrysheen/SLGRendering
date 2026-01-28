using System.Collections.Generic;
using System.Linq;
using Linework.FastOutline;
using UnityEditor;
using UnityEngine;

namespace Linework.Editor.FastOutline
{
    public class SmoothNormalsMeshImporter : AssetPostprocessor
    {
        public void OnPostprocessModel(GameObject gameObject)
        {
            var smoothNormals = AssetDatabase.GetLabels(assetImporter).Any(label => label.Contains(FastOutlineUtils.SmoothNormalsLabel));

            var meshes = GetMeshesForGameobject(gameObject);

            foreach (var mesh in meshes.Where(mesh => mesh.uv8 == null || mesh.uv8.Length == 0))
            {
                if (smoothNormals)
                {
                    mesh.SetUVs(7, SmoothNormalsBaker.ComputeSmoothedNormals(mesh));
                }
                else
                {
                    mesh.uv8 = null;
                }
            }
        }

        private static List<Mesh> GetMeshesForGameobject(GameObject gameObject)
        {
            var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
            var meshes = meshFilters.Select(item => item.sharedMesh).ToList();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            meshes.AddRange(skinnedMeshRenderers.Select(item => item.sharedMesh));
            return meshes;
        }
    }
}