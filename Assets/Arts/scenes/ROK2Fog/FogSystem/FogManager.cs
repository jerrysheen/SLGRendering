using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FogManager
{
    /// <summary>
    /// 简化的迷雾系统 - 专注于Mesh管理
    /// 负责mesh块的创建、显示/隐藏管理、视锥剔除等
    /// Simplified Fog System - Focuses on Mesh Management
    /// </summary>
    public class FogManager : MonoBehaviour
    {
        public enum FOG_TYPE
        {
            Default = 0,
            Locked = 1,
            Unlocked = 2,
            Unlocking = 3,
        }

        [Header("Settings")]
        [Tooltip("地图宽度 Map Width")]
        public int MapWidth = 1200;
        
        [Tooltip("地图高度 Map Height")]
        public int MapHeight = 1200;
        
        [Tooltip("迷雾格大小 Grid Cell Size")]
        public int GridCellSize = 3;
        
        [Tooltip("生成的迷雾的mesh高低落差 Fog Height")]
        public int FogHeight = 6;
        
        [Tooltip("迷雾起始位置偏移 (XYZ) Fog Start Position")]
        public Vector3 FogStartPosition = new Vector3(0, 6, 0);
        
        [Tooltip("地图缩放尺寸 Global Scale (default 1)")]
        public int GlobalScale = 1;

        [Header("Materials")]
        public Material FogBaseMaterial; // 地形材质
        public Material FogTopMaterial;  // 地形材质

        private const int MARGIN_SIZE = 160;
        
        // Runtime Data
        public FogData FogData { get; private set; }
        private bool _systemInitialized = false;

        // Unlocking Effect Objects
        private GameObject _fogUnlockingGo;
        private Mesh _fogUnlockingMesh;
        private Material _fogUnlockingBaseMaterial;
        private Material _fogUnlockingTopMaterial;
        private Color _fogBaseLayerColor;
        private Color _fogTopLayerColor;
        
        // World Corner Mesh
        private GameObject _worldCornerMeshGo;
        private Mesh _worldCornerMesh;
        
        // QuadTree & Meshing
        private OptimizedFogQuad _optimizedFogQuad;
        private Mesh[] _quadMeshes;
        private Mesh[] _borderMeshes; // 0:Bottom, 1:Top, 2:Left, 3:Right
        private bool _borderDirty = true;
        
        // Coordinates & Transforms
        private int _logicalGridSize;   // = MapWidth/GridCellSize
        private int _meshGridSize;      // = _logicalGridSize + 2
        private Matrix4x4 _fogTRS;
        
        // Generator
        private SingleFogMeshGenerator _meshGenerator;
        private Tween _tween;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeSystem();
        }

        private void OnEnable()
        {
            if (_systemInitialized)
            {
                if (_fogUnlockingGo != null) _fogUnlockingGo.SetActive(true);
                if (_worldCornerMeshGo != null) _worldCornerMeshGo.SetActive(true);
            }
        }
        
        private void LateUpdate()
        {
            if (!_systemInitialized) return;
            if (FogTopMaterial == null || FogBaseMaterial == null) return;
            
            DrawBorderMeshes();
            
            // Draw Quadtree Meshes
            for (int i = 0; i < _optimizedFogQuad.NodeCapacity; i++)
            {
                if (_quadMeshes[i] != null)
                {
                    Graphics.DrawMesh(_quadMeshes[i], _fogTRS, FogBaseMaterial, 0);                
                    Graphics.DrawMesh(_quadMeshes[i], _fogTRS, FogTopMaterial, 0);                
                }
            }
        }
        
        private void OnDisable()
        {
             if (_fogUnlockingGo != null) _fogUnlockingGo.SetActive(false);
             if (_worldCornerMeshGo != null) _worldCornerMeshGo.SetActive(false);
        }

        private void OnDestroy()
        {
            ShutDownFogManager();
        }

        #endregion

        /// <summary>
        /// 初始化系统
        /// Initialize the Fog System
        /// </summary>
        private void InitializeSystem()
        {
            if (_systemInitialized) return;
            Debug.Log("[FogManager] Initializing...");
            
            if(FogData == null) FogData = new FogData();
            FogData.Initialize(MapWidth, MapHeight, GridCellSize, FogHeight);
            
            // Calculate generator offset (inverse scale for X/Z)
            float scaleXZ = GlobalScale * GridCellSize;
            Vector3 generatorOffset = new Vector3(
                scaleXZ > 0 ? FogStartPosition.x / scaleXZ : 0,
                FogStartPosition.y, 
                scaleXZ > 0 ? FogStartPosition.z / scaleXZ : 0
            );

            // Instantiate the generator instance
            _meshGenerator = new SingleFogMeshGenerator(FogData);
            _meshGenerator.MeshOffset = generatorOffset;
            
            // Note: Keeping the hardcoded 25, 25 from original logic.
            // This relates to the leaf node size in the QuadTree strategy.
            _meshGenerator.InitBuffers(25, 25);

            if (FogBaseMaterial == null) Debug.LogError("FogBaseMaterial is null");
            else _fogBaseLayerColor = FogBaseMaterial.GetColor("_FogColor");
            
            if (FogTopMaterial == null) Debug.LogError("FogTopMaterial is null");
            else _fogTopLayerColor = FogTopMaterial.GetColor("_FogColor");

            _logicalGridSize = (int)(MapWidth / GridCellSize);
            _meshGridSize = _logicalGridSize + 2;

            // Initialize QuadTree
            // Using "Logical Grid" for tree structure.
            // 400 grids divisible by 25 (leaf size), works well with quadtree subdivision.
            _optimizedFogQuad = new OptimizedFogQuad(_logicalGridSize, 25);
            
            // Initialize root node: 1=Locked
            _optimizedFogQuad.Insert(new OptimizedFogQuad.BoundsAABB(new Vector2Int(0, 0), new Vector2Int(_logicalGridSize, _logicalGridSize)), 1);

            _quadMeshes = new Mesh[_optimizedFogQuad.NodeCapacity];
            
            BuildBorderMeshes();

            // 整体平移 (-1, -1) 个 cell，让逻辑(0,0) 对齐到 mesh(1,1)
            // Offset by (-1, -1) cells so Logical(0,0) aligns with Mesh(1,1)
            _fogTRS = Matrix4x4.TRS(
                new Vector3(-GlobalScale * GridCellSize, 0, -GlobalScale * GridCellSize),
                Quaternion.identity, 
                new Vector3(GlobalScale * GridCellSize, 1, GlobalScale * GridCellSize)
            );

            GenerateWorldCornerMesh();
            RebuildFogMesh();
            _borderDirty = false;
            _systemInitialized = true;
        }

        public void ShutDownFogManager()
        {
            Debug.Log("[FogManager] Shutting down...");
            
            FogTopMaterial = null;
            FogBaseMaterial = null;
            
            _tween?.Kill(); 
            _tween = null;

            if(_fogUnlockingMesh != null) Object.Destroy(_fogUnlockingMesh);
            if(_fogUnlockingGo != null) Object.Destroy(_fogUnlockingGo);
            if(_fogUnlockingBaseMaterial != null) Object.Destroy(_fogUnlockingBaseMaterial);
            if(_fogUnlockingTopMaterial != null) Object.Destroy(_fogUnlockingTopMaterial);
            
            if(_worldCornerMesh != null) Object.Destroy(_worldCornerMesh);
            if(_worldCornerMeshGo != null) Object.Destroy(_worldCornerMeshGo);

            if (_optimizedFogQuad != null)
            {
                _optimizedFogQuad.Destroy();
                _optimizedFogQuad = null;
            }

            if (_borderMeshes != null)
            {
                for (int i = 0; i < _borderMeshes.Length; i++)
                {
                    if (_borderMeshes[i] != null) Object.Destroy(_borderMeshes[i]);
                }
                _borderMeshes = null;
            }
            if (_quadMeshes != null)
            {
                for (int i = 0; i < _quadMeshes.Length; i++)
                {
                    if (_quadMeshes[i] != null) Object.Destroy(_quadMeshes[i]);
                }
                _quadMeshes = null;
            }
            
            FogData = null;
            
            if (_meshGenerator != null)
            {
                _meshGenerator.Dispose();
                _meshGenerator = null;
            }
            _systemInitialized = false;
        }

        #region Fog Updates & Meshing

        public void UpdateFogGridInfo(Vector2Int gridID, bool unlockFog)
        {
            FOG_TYPE type = unlockFog ? FOG_TYPE.Unlocked : FOG_TYPE.Locked;
            if (unlockFog)
            {
                if (FogData.UnlockCell(gridID.x, gridID.y))
                {
                    // 稍微扩大脏的区域， 这样确保所有标记为lock的区域直接生成大面片就ok
                    // Expand dirty area slightly to ensure boundary continuity
                    InsertArea(new OptimizedFogQuad.BoundsAABB(
                        new Vector2Int(gridID.x - 1, gridID.y - 1),
                        new Vector2Int(gridID.x + 2, gridID.y + 2)), 
                        (int)type);
                }
            }
        }

        /// <summary>
        /// 批量解锁区域
        /// Try unlocking area using a bitmask array
        /// </summary>
        public void TryUnlockingArea(IntPtr gridStatusBits, int bitLength)
        {
            // 外部传入的是基于 MapWidth * MapHeight 的位数组
            int mapW = MapWidth / GridCellSize;
            int mapH = MapHeight / GridCellSize;
            int totalCells = mapW * mapH;

            if (bitLength != totalCells)
            {
                Debug.LogError($"[FogManager] Grid status bits length mismatch! Expected bits: {totalCells}, Actual: {bitLength}");
                return;
            }

            for (int y = 0; y < mapH; y++)
            {
                for (int x = 0; x < mapW; x++)
                {
                    int index = y * mapW + x;
                    int byteIndex = index / 8;
                    int bitIndex = index % 8;

                    // 检查对应位是否为1 (Check if bit is 1)
                    byte b = Marshal.ReadByte(gridStatusBits, byteIndex);
                    bool isUnlocked = (b & (1 << bitIndex)) != 0;

                    if (isUnlocked)
                    {
                        if (FogData.UnlockCell(x, y))
                        {
                            // 每次解锁一个逻辑格子，就刷新该格子及其周围的一小块区域
                            // InsertArea handles optimization internally
                            InsertArea(new OptimizedFogQuad.BoundsAABB(
                                new Vector2Int(x - 1, y - 1),
                                new Vector2Int(x + 2, y + 2)
                            ), (int)FOG_TYPE.Unlocked);
                        }
                    }
                }
            }
        }

        public void RebuildFogMesh()
        {
            RebuildMeshByQuadTree();
        }
        
        /// <summary>
        /// 对修改的坐标点进行标记。
        /// Mark modified area in QuadTree
        /// </summary>
        private void InsertArea(OptimizedFogQuad.BoundsAABB aabb, int nodeType)
        {
            _optimizedFogQuad.Insert(aabb, (byte)nodeType);
            // If area touches the border, mark border as dirty
            if (aabb.MinXY.x <= 0 || aabb.MaxXY.x >= _logicalGridSize || 
                aabb.MinXY.y <= 0 || aabb.MaxXY.y >= _logicalGridSize)
            {
                _borderDirty = true;
            }
        }

        private void RebuildMeshByQuadTree()
        {
            // Update border meshes if needed (to maintain continuity with inner fog)
            if (_borderDirty)
            {
                BuildBorderMeshes();
                _borderDirty = false;
            }
            
            int cap = _optimizedFogQuad.NodeCapacity;
            for (int i = 0; i < cap; i++)
            {
                // If node doesn't exist or is dirty but NOT a leaf (means it was subdivided), destroy old mesh
                if (!_optimizedFogQuad.Exists(i) || (_optimizedFogQuad.IsDirty(i) && !_optimizedFogQuad.IsLeaf(i)))
                {
                    if (_quadMeshes[i] != null)
                    {
                        Object.Destroy(_quadMeshes[i]);
                        _quadMeshes[i] = null;
                    }
                }
                
                if (!_optimizedFogQuad.IsLeaf(i)) continue;
                if (!_optimizedFogQuad.IsDirty(i)) continue;
                
                BuildQuadMesh(_optimizedFogQuad, i);
            }
        }
        
        private void BuildQuadMesh(OptimizedFogQuad quad, int nodeIndex)
        {
            // Debug.Log("Rebuild quad mesh:" + nodeIndex);
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

            byte type = _optimizedFogQuad.GetNodeType(nodeIndex);
            
            if (type == (byte)FOG_TYPE.Unlocked)
            {
                // Fully unlocked, empty mesh
                _quadMeshes[nodeIndex].Clear(); 
            }
            else if (type == (byte)FOG_TYPE.Locked)
            {
                // Fully locked, use Sparse mesh generation (optimized)
                // Logical(0,0) -> Mesh(1,1)
                _meshGenerator.GenerateSparseMeshBlock(_quadMeshes[nodeIndex], min.x + 1, min.y + 1, blockCountX, blockCountY, FogHeight);
            }
            else
            {
                // Partially unlocked or other state, use Dense mesh generation
                _meshGenerator.GenerateDenseMeshBlock(_quadMeshes[nodeIndex], min.x + 1, min.y + 1, blockCountX, blockCountY, FogHeight);
            }
            
            _optimizedFogQuad.MarkRebuilt(nodeIndex);
        }

        #endregion

        #region Unlocking Visuals

        /// <summary>
        /// 生成一小块目标解锁区域，等待解锁
        /// Generate a temporary mesh for the area being unlocked
        /// </summary>
        public void GenerateUnlockingAreaFogGo(List<Vector2Int> gridList)
        {
            HashSet<Vector2Int> plateauVerts = new HashSet<Vector2Int>();
            foreach (var c in gridList)
            {
                // Collect 3x3 area around each cell to ensure smooth connections
                plateauVerts.Add(new Vector2Int(c.x, c.y));
                plateauVerts.Add(new Vector2Int(c.x - 1, c.y - 1));
                plateauVerts.Add(new Vector2Int(c.x - 1, c.y));
                plateauVerts.Add(new Vector2Int(c.x - 1, c.y + 1));
                plateauVerts.Add(new Vector2Int(c.x, c.y - 1));
                plateauVerts.Add(new Vector2Int(c.x, c.y + 1));
                plateauVerts.Add(new Vector2Int(c.x + 1, c.y - 1));
                plateauVerts.Add(new Vector2Int(c.x + 1, c.y));
                plateauVerts.Add(new Vector2Int(c.x + 1, c.y + 1));
            }

            if (_fogUnlockingMesh == null)
            {
                _fogUnlockingMesh = new Mesh();
            }
            
            // Shift to Mesh Coordinates (+1, +1)
            var shiftedVerts = plateauVerts.Select(v => new Vector2Int(v.x + 1, v.y + 1)).ToList();
            _meshGenerator.GenerateSeperateUnlockFogMesh(_fogUnlockingMesh, shiftedVerts, FogHeight);

            if (_fogUnlockingGo == null)
            {
                _fogUnlockingGo = new GameObject("UnlockingAreaGO");
                _fogUnlockingGo.transform.position = new Vector3(-GlobalScale * GridCellSize, 0, -GlobalScale * GridCellSize);
                _fogUnlockingGo.transform.localScale = new Vector3(GlobalScale * GridCellSize, 1, GlobalScale * GridCellSize);
                
                var mf = _fogUnlockingGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _fogUnlockingMesh;
                
                var mr = _fogUnlockingGo.AddComponent<MeshRenderer>();
                mr.materials = new []{FogBaseMaterial, FogTopMaterial};
                
                _fogUnlockingBaseMaterial = mr.materials[0];
                _fogUnlockingTopMaterial = mr.materials[1];
            }
            else
            {
                // Sync colors if materials were re-instantiated
                _fogUnlockingTopMaterial.SetColor("_FogColor", _fogTopLayerColor);
                _fogUnlockingBaseMaterial.SetColor("_FogColor", _fogBaseLayerColor);
                _fogUnlockingGo.SetActive(true);
            }
        }

        /// <summary>
        /// 迷雾表现，单块销毁渐隐
        /// Start the unlock animation (fade out)
        /// </summary>
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
                            _fogUnlockingBaseMaterial.SetColor("_FogColor", baseLayerColor);
                        },
                        0,
                        duration)
                    .SetEase(Ease.Linear)
            );
            seq.OnComplete(() => _fogUnlockingGo.SetActive(false));
            _tween = seq; 
        }

        #endregion

        #region Border & World Corner

        /// <summary>
        /// 生成 4 条边缘补齐条带
        /// Build 4 border strips to cover the gap between logical grid and world mesh bounds
        /// </summary>
        private void BuildBorderMeshes()
        {
            if (_meshGridSize <= 0) return;

            _borderMeshes ??= new Mesh[4];
            int borderThickness = 1; 

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

            // Generate Dense Meshes for borders to support smooth transitions if edges are unlocked
            
            // Bottom strip: z = 0, x: [0.._meshGridSize)
            _meshGenerator.GenerateDenseMeshBlock(_borderMeshes[0], 0, 0, _meshGridSize, borderThickness, FogHeight);
            
            // Top strip: z = _meshGridSize - 1
            _meshGenerator.GenerateDenseMeshBlock(_borderMeshes[1], 0, _meshGridSize - borderThickness, _meshGridSize, borderThickness, FogHeight);
            
            // Left strip: x = 0, z: [borderThickness.._meshGridSize - borderThickness)
            _meshGenerator.GenerateDenseMeshBlock(_borderMeshes[2], 0, borderThickness, borderThickness, _meshGridSize - borderThickness * 2, FogHeight);
            
            // Right strip: x = _meshGridSize - 1, z: [borderThickness.._meshGridSize - borderThickness)
            _meshGenerator.GenerateDenseMeshBlock(_borderMeshes[3], _meshGridSize - borderThickness, borderThickness, borderThickness, _meshGridSize - borderThickness * 2, FogHeight);
        }

        private void DrawBorderMeshes()
        {
            if (_borderMeshes == null) return;
            for (int i = 0; i < _borderMeshes.Length; i++)
            {
                var m = _borderMeshes[i];
                if (m == null) continue;
                Graphics.DrawMesh(m, _fogTRS, FogBaseMaterial, 0);
                Graphics.DrawMesh(m, _fogTRS, FogTopMaterial, 0);
            }
        }

        private void GenerateWorldCornerMesh()
        {
            if (_worldCornerMeshGo == null)
            {
                _worldCornerMeshGo = new GameObject("WorldCornerMesh");
                MeshFilter meshFilter = _worldCornerMeshGo.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = _worldCornerMeshGo.AddComponent<MeshRenderer>();
                
                Vector3 alignOffset = new Vector3(-GlobalScale * GridCellSize, 0, -GlobalScale * GridCellSize);
                _worldCornerMeshGo.transform.localPosition = alignOffset + FogStartPosition;
                _worldCornerMeshGo.transform.localScale = new Vector3(GlobalScale, 1 , GlobalScale);
                
                _worldCornerMesh = new Mesh();
                meshFilter.mesh = _worldCornerMesh;
                meshRenderer.sharedMaterials = new []{FogBaseMaterial, FogTopMaterial };
                _worldCornerMesh.Clear();

                float totalWidth = MapWidth + 2 * GridCellSize;
                float totalHeight = MapHeight + 2 * GridCellSize;

                // Create a skirt around the map
                _worldCornerMesh.vertices = new Vector3[]
                {
                    new Vector3(0.0f, FogHeight, 0.0f),
                    new Vector3(0.0f, FogHeight, -MARGIN_SIZE),
                    new Vector3(totalWidth + MARGIN_SIZE, FogHeight, -MARGIN_SIZE),
                    new Vector3(totalWidth + MARGIN_SIZE, FogHeight, 0.0f),
                    new Vector3(totalWidth, FogHeight, 0.0f),
                    new Vector3(totalWidth + MARGIN_SIZE, FogHeight, 0.0f),
                    new Vector3(totalWidth, FogHeight, totalHeight + MARGIN_SIZE),
                    new Vector3(totalWidth + MARGIN_SIZE, FogHeight, totalHeight + MARGIN_SIZE),
                    new Vector3(-MARGIN_SIZE, FogHeight, totalHeight),
                    new Vector3(totalWidth, FogHeight, totalHeight),
                    new Vector3(totalWidth, FogHeight, totalHeight + MARGIN_SIZE),
                    new Vector3(-MARGIN_SIZE, FogHeight, totalHeight + MARGIN_SIZE),
                    new Vector3(0.0f, FogHeight, totalHeight),
                    new Vector3(-MARGIN_SIZE, FogHeight, totalHeight),
                    new Vector3(-MARGIN_SIZE, FogHeight, -MARGIN_SIZE),
                    new Vector3(0.0f, FogHeight, -MARGIN_SIZE),
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
                
                // UVs
                Vector2 uvOne = new Vector2(1, 1);
                _worldCornerMesh.uv = Enumerable.Repeat(uvOne, 16).ToArray();
                
                _worldCornerMesh.RecalculateNormals();
                _worldCornerMesh.RecalculateBounds();
            }
        }
        #endregion
    }
}