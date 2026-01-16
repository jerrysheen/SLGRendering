using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ELEX.Resource;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FogSystem
{

    /// <summary>
    /// 简化的迷雾系统 - 专注于Mesh管理
    /// 负责mesh块的创建、显示/隐藏管理、视锥剔除等
    /// </summary>
    public class FogSystem 
    {
        private float _mapWidth;   // 地图总宽度（米）
        private float _mapHeight;  // 地图总高度（米）
        private float _cellSize;     // 显示格子的边长（米）
        private float _fogGoPositionZ;  // GameObject整体的高度
        private float _fogHeight;  // 生成mesh的高度，迷雾的高低落差
        private float _globalScale; // 整体缩放， 目前是5倍
        
        private Material _fogBaseMaterial; // 地形材质
        private Material _fogMaterial; // 地形材质
        
        private bool systemInitialized = false;
        

        private GameObject _fogUnlockingGo;
        private Mesh _fogUnlockingMesh;
        private Material _fogUnlockingBaseMaterial;
        private Material _fogUnlockingTopMaterial;
        private Color _fogBaseLayerColor;
        private Color _fogTopLayerColor;
        
        // 持有加载Asset请求，System销毁的时候减少引用。
        
        // 地图边缘生成迷雾， 目前写死了， 1200 / 160，会扩大5倍
        private int _marginSize = 160;
        private GameObject _worldCornerMeshGo;
        private Mesh _worldCornerMesh;
        
        private OptimizedFogQuad _optimizedFogQuad;
        // Mesh 坐标系：比逻辑格子多一圈（+2），并整体平移 (-1,-1) 让 逻辑(0,0) 对齐 mesh(1,1)
        private int _logicalGridSize;   // = _mapWidth/_cellSize
        private int _meshGridSize;      // = _logicalGridSize + 2
        // 4 条边缘补齐条带：需要参与“密集”三角化（Dense），才能随内圈解锁保持连续
        private Mesh[] _borderMeshes;
        private Mesh[] _quadMeshes;
        private Matrix4x4 _fogTRS;
        
        // 初始化迷雾系统，需要参数width = 1200, height = 1200, cellsize = 3,
        // fogHeight为迷雾高低落差，  fogPositionZ = go高度，  globalScale表示全局缩放。
        public FogSystem(float mapWidth, float mapHeight, float cellSize, 
             float fogHeight, float fogGoPositionZ, float globalScale)
        {
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;
            _cellSize = cellSize;
            _fogHeight = fogHeight;
            _fogGoPositionZ = fogGoPositionZ;
            _globalScale = globalScale;
            InitializeSystem();
        }
        
        /// <summary>
        /// 初始化系统
        /// </summary>
        private void InitializeSystem()
        {
            if (_fogBaseMaterial == null)
            { 
                // todo temp方案
                _fogBaseMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/scenes/ROK2Fog/FogBase.mat");
                _fogBaseLayerColor = _fogBaseMaterial.GetColor("_FogColor");
            }
            
            if (_fogMaterial == null)
            {
                _fogMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/scenes/ROK2Fog/FogTop.mat");
                _fogTopLayerColor = _fogMaterial.GetColor("_FogColor");
            }

            _logicalGridSize = (int)(_mapWidth / _cellSize);
            _meshGridSize = _logicalGridSize + 2;

            // 四叉树仍然以“逻辑网格”建树（400 可被 25 整除，且是 2 的幂分割），避免 402 导致的非整除问题。
            // 渲染层通过 mesh(1,1) <-> logical(0,0) 的映射来补齐边缘连续性。
            _optimizedFogQuad = new OptimizedFogQuad(_logicalGridSize, 25);
            // node type: 1 lock, 2 unlock, 3 partial/need dense
            _optimizedFogQuad.Insert(new OptimizedFogQuad.BoundsAABB(new Vector2Int(0, 0), new Vector2Int(_logicalGridSize, _logicalGridSize)), 1);

            _quadMeshes = new Mesh[_optimizedFogQuad.NodeCapacity];
            BuildBorderMeshes();
            // 整体平移 (-1, -1) 个 cell，让逻辑(0,0) 对齐到 mesh(1,1)
            _fogTRS = Matrix4x4.TRS(new Vector3(-_globalScale * _cellSize, _fogGoPositionZ, -_globalScale * _cellSize),
                Quaternion.identity, new Vector3(_globalScale * _cellSize, 1, _globalScale * _cellSize));

            systemInitialized = true;
                    
        }
        
        public void LateUpdate()
        {
            if (_fogMaterial == null || _fogBaseMaterial == null) return;
            GenerateWorldCornerMesh();
            DrawBorderMeshes();
            for (int i = 0; i < _optimizedFogQuad.NodeCapacity; i++)
            {
                if (_quadMeshes[i] != null)
                {
                    Graphics.DrawMesh(_quadMeshes[i], _fogTRS, _fogBaseMaterial,0);                
                    Graphics.DrawMesh(_quadMeshes[i], _fogTRS, _fogMaterial,0);                
                }
            }
        }

        public void Destroy()
        {
            //if(_fogMaterial != null) Object.Destroy(_fogMaterial);
            //if(_fogBaseMaterial != null) Object.Destroy(_fogBaseMaterial);
            _fogMaterial = null;
            _fogBaseMaterial = null;
            _tween?.Kill(); // <--- 添加这行，防止动画回调报错
            _tween = null;
            if(_fogUnlockingMesh != null) Object.Destroy(_fogUnlockingMesh);
            if(_fogUnlockingGo != null) Object.Destroy(_fogUnlockingGo);
            if(_fogUnlockingBaseMaterial != null) Object.Destroy(_fogUnlockingBaseMaterial);
            if(_fogUnlockingTopMaterial != null) Object.Destroy(_fogUnlockingTopMaterial);
            if(_worldCornerMesh != null) Object.Destroy(_worldCornerMesh);
            if(_worldCornerMeshGo != null) Object.Destroy(_worldCornerMeshGo);

            _optimizedFogQuad = null;
            if (_borderMeshes != null)
            {
                for (int i = 0; i < _borderMeshes.Length; i++)
                {
                    if (_borderMeshes[i] != null) Object.Destroy(_borderMeshes[i]);
                }
                _borderMeshes = null;
            }
            for (int i = 0; i < _quadMeshes.Length; i++)
            {
                Object.Destroy(_quadMeshes[i]);
            }
            _quadMeshes = null;
        }

        
        /// <summary>
        /// 对修改的坐标点进行标记。
        /// </summary>
        public void InsertArea(OptimizedFogQuad.BoundsAABB aabb, int nodeType)
        {
            _optimizedFogQuad.Insert(aabb, (byte)nodeType);
        }

        /// <summary>
        /// 只对标记为脏的mesh进行更新。
        /// </summary>
        public void RebuildMesh()
        {
            RebuildMeshByQuadTree();
        }

        // 这个操作似乎可以做成异步。
        public void RebuildMeshByQuadTree()
        {
            // 边缘条带需要随内圈变化更新（解锁到边缘时不出现断裂）
            BuildBorderMeshes();
            int cap = _optimizedFogQuad.NodeCapacity;
            for (int i = 0; i < cap; i++)
            {
                if (!_optimizedFogQuad.Exists(i) || (_optimizedFogQuad.IsDirty(i) && !_optimizedFogQuad.IsLeaf(i)))
                {
                    if (_quadMeshes[i] != null)
                    {
                        GameObject.Destroy(_quadMeshes[i]);
                        _quadMeshes[i] = null;
                    }
                }
                if (!_optimizedFogQuad.IsLeaf(i)) continue;
                if (!_optimizedFogQuad.IsDirty(i)) continue;
                BuildQuadMesh(_optimizedFogQuad, i);
            }
        }
        
        public void BuildQuadMesh(OptimizedFogQuad quad, int nodeIndex)
        {
            Debug.Log("Rebuild quad mesh:" + nodeIndex);
            Vector2Int min, max;
            quad.GetNodeBounds(nodeIndex, out min, out max);

            int blockCountX = max.x - min.x;
            int blockCountY = max.y - min.y;
            if (_quadMeshes[nodeIndex] == null)
            {
                _quadMeshes[nodeIndex] = new Mesh();
#if UNITY_EDITOR
                _quadMeshes[nodeIndex].name = $"FogQuadMesh_{nodeIndex}";
#endif
            }

            if (_optimizedFogQuad.GetNodeType(nodeIndex) == (byte)FogManager.FOG_TYPE.Unlocked)
            {
                return;
            }
            else if (_optimizedFogQuad.GetNodeType(nodeIndex) == (byte)FogManager.FOG_TYPE.Locked)
            {
                // 四叉树在逻辑坐标中：logical(0,0) -> mesh(1,1)
                SingleFogMeshGenerator.GenerateSparseMeshBlock(_quadMeshes[nodeIndex], min.x + 1, min.y + 1, blockCountX, blockCountY, _fogHeight);
            }
            else
            {
                SingleFogMeshGenerator.GenerateDenseMeshBlock(_quadMeshes[nodeIndex], min.x + 1, min.y + 1, blockCountX, blockCountY, _fogHeight);
            }
            
            _optimizedFogQuad.MarkRebuilt(nodeIndex);
        }

        /// <summary>
        ///  生成一小块目标解锁区域，等待解锁，主要思路为采样附近的高度点，生成对应mesh
        ///  使用这些区域还没解锁之前的高度数据，所以可以还原原来的mesh形状，随后高度进行塌陷
        /// </summary>
        public void GenerateUnlockingAreaFogGo(List<Vector2Int> gridList)
        {
            HashSet<Vector2Int> plateauVerts = new();
            foreach (var c in gridList)
            {
                plateauVerts.UnionWith(new[]
                {
                    new Vector2Int(c.x, c.y),
                    new Vector2Int(c.x - 1, c.y - 1),
                    new Vector2Int(c.x - 1, c.y),
                    new Vector2Int(c.x - 1, c.y + 1),
                    new Vector2Int(c.x, c.y - 1),
                    new Vector2Int(c.x, c.y + 1),
                    new Vector2Int(c.x + 1, c.y - 1),
                    new Vector2Int(c.x + 1, c.y),
                    new Vector2Int(c.x + 1, c.y + 1)
                });
            }
                        

            if (_fogUnlockingMesh == null)
            {
                _fogUnlockingMesh = new Mesh();
            }
            var shiftedVerts = plateauVerts.Select(v => new Vector2Int(v.x + 1, v.y + 1)).ToList();
            SingleFogMeshGenerator.GenerateSeperateUnlockFogMesh(_fogUnlockingMesh, shiftedVerts, _fogHeight);

            if (_fogUnlockingGo == null)
            {
                _fogUnlockingGo = new GameObject("UnlockingAreaGO");
                _fogUnlockingGo.transform.position = new Vector3(-_globalScale * _cellSize, _fogGoPositionZ, -_globalScale * _cellSize);
                _fogUnlockingGo.transform.localScale = new Vector3(_globalScale * _cellSize, 1, _globalScale * _cellSize);
                _fogUnlockingGo.AddComponent<MeshFilter>().sharedMesh = _fogUnlockingMesh;
                _fogUnlockingGo.AddComponent<MeshRenderer>().materials = new []{_fogBaseMaterial, _fogMaterial};
                _fogUnlockingBaseMaterial = _fogUnlockingGo.GetComponent<MeshRenderer>().materials[0];
                _fogUnlockingTopMaterial = _fogUnlockingGo.GetComponent<MeshRenderer>().materials[1];
            }
            else
            {
                _fogUnlockingTopMaterial.SetColor("_FogColor", _fogTopLayerColor);      // 可选：同步到材质
                _fogUnlockingBaseMaterial.SetColor("_FogColor", _fogBaseLayerColor);      // 可选：同步到材质
                _fogUnlockingGo.SetActive(true);
            }

        }


        private Color _startColor;
        private Color _endColor;
        private Color _currentColor;
        private Tween _tween;

        // 迷雾表现，单块销毁渐隐
        public void StartUnlockAreaFogGo()
        {
            _tween?.Kill();   
            float duration = 1.5f;
            Sequence seq = DOTween.Sequence();
            Color topLayerColor = _fogTopLayerColor;
            Color baseLayerColor = _fogBaseLayerColor;
            float currentAlpha = 1;
            seq.Append(
                DOTween.To(() => currentAlpha,
                        value => {
                            topLayerColor.a = value;
                            baseLayerColor.a = value;
                            _fogUnlockingTopMaterial.SetColor("_FogColor", topLayerColor);   
                            _fogUnlockingBaseMaterial.SetColor("_FogColor", baseLayerColor);;
                        },
                        0,
                        duration)
                    .SetEase(Ease.Linear)
            );
            seq.OnComplete(() => _fogUnlockingGo.SetActive(false));
            _tween = seq; 
        }

        /// <summary>
        /// 生成 4 条边缘补齐条带（宽 1 格）。
        /// 注意：这里必须用 Dense 方式生成三角形，否则边缘会因为“只是一整块锁定面”而在 0,0 解锁时断裂。
        /// 条带网格使用 mesh 坐标：mesh(1,1) 对齐 logical(0,0)，mesh 外圈(0/401) 永远 Locked。
        /// </summary>
        private void BuildBorderMeshes()
        {
            if (_meshGridSize <= 0) return;

            _borderMeshes ??= new Mesh[4];

            // 0: Bottom, 1: Top, 2: Left, 3: Right
            for (int i = 0; i < 4; i++)
            {
                if (_borderMeshes[i] == null)
                {
                    _borderMeshes[i] = new Mesh();
#if UNITY_EDITOR
                    _borderMeshes[i].name = $"FogBorderMesh_{i}";
#endif
                }
                else
                {
                    _borderMeshes[i].Clear();
                }
            }

            // Bottom strip: z = 0, x: [0.._meshGridSize)
            SingleFogMeshGenerator.GenerateDenseMeshBlock(_borderMeshes[0], 0, 0, _meshGridSize, 1, _fogHeight);
            // Top strip: z = _meshGridSize - 1
            SingleFogMeshGenerator.GenerateDenseMeshBlock(_borderMeshes[1], 0, _meshGridSize - 1, _meshGridSize, 1, _fogHeight);
            // Left strip: x = 0
            SingleFogMeshGenerator.GenerateDenseMeshBlock(_borderMeshes[2], 0, 0, 1, _meshGridSize, _fogHeight);
            // Right strip: x = _meshGridSize - 1
            SingleFogMeshGenerator.GenerateDenseMeshBlock(_borderMeshes[3], _meshGridSize - 1, 0, 1, _meshGridSize, _fogHeight);
        }

        private void DrawBorderMeshes()
        {
            if (_borderMeshes == null) return;
            for (int i = 0; i < _borderMeshes.Length; i++)
            {
                var m = _borderMeshes[i];
                if (m == null) continue;
                Graphics.DrawMesh(m, _fogTRS, _fogBaseMaterial, 0);
                Graphics.DrawMesh(m, _fogTRS, _fogMaterial, 0);
            }
        }

        private void GenerateWorldCornerMesh()
        {
            if (_worldCornerMeshGo == null)
            {
                _worldCornerMeshGo = new GameObject("WorldCornerMesh");
                MeshFilter meshFilter = _worldCornerMeshGo.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = _worldCornerMeshGo.AddComponent<MeshRenderer>();
                _worldCornerMeshGo.transform.localPosition = new Vector3(-_globalScale * _cellSize, _fogGoPositionZ, -_globalScale * _cellSize);
                _worldCornerMeshGo.transform.localScale = new Vector3(5, 1 , 5);
                _worldCornerMesh = new Mesh();
                meshFilter.mesh = _worldCornerMesh;
                meshRenderer.sharedMaterials = new []{_fogBaseMaterial, _fogMaterial };
                _worldCornerMesh.Clear();
                _worldCornerMesh.vertices = new Vector3[]
                {
                    new Vector3(0.0f, _fogHeight, 0.0f),
                    new Vector3(0.0f, _fogHeight, -_marginSize),
                    new Vector3(_mapWidth + _marginSize, _fogHeight, -_marginSize),
                    new Vector3(_mapWidth + _marginSize, _fogHeight, 0.0f),
                    new Vector3(_mapWidth, _fogHeight, 0.0f),
                    new Vector3(_mapWidth + _marginSize, _fogHeight, 0.0f),
                    new Vector3(_mapWidth, _fogHeight, _mapHeight + _marginSize),
                    new Vector3(_mapWidth + _marginSize, _fogHeight, _mapHeight + _marginSize),
                    new Vector3(-_marginSize, _fogHeight, _mapHeight),
                    new Vector3(_mapWidth, _fogHeight, _mapHeight),
                    new Vector3(_mapWidth, _fogHeight, _mapHeight + _marginSize),
                    new Vector3(-_marginSize, _fogHeight, _mapHeight + _marginSize),
                    new Vector3(0.0f, _fogHeight, _mapHeight),
                    new Vector3(-_marginSize, _fogHeight, _mapHeight),
                    new Vector3(-_marginSize, _fogHeight, -_marginSize),
                    new Vector3(0.0f, _fogHeight, -_marginSize),
                };
                _worldCornerMesh.triangles = new int[]
                {
                    1, 0, 3,
                    1, 3, 2,
                    4,6,5,
                    6,7,5,
                    8,11,10,
                    10,9,8,
                    12,14,13,
                    12,15,14
                };
                _worldCornerMesh.uv = new Vector2[]
                {
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1)
                };
                _worldCornerMesh.RecalculateNormals();
                _worldCornerMesh.RecalculateBounds();
            }
        }

    }
} 