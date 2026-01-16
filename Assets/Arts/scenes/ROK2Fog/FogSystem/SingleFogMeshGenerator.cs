using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Rendering;

namespace FogManager
{
    public enum TriangleGenerationType
    {
        None = 0,
        Standard = 1,
        SingleTriangle = 2
    }

    public class SingleFogMeshGenerator
    {
        private enum VertexFogState
        {
            Locked,
            Half,
            Unlocked
        }

        [StructLayout(LayoutKind.Sequential)]
        struct UV16
        {
            public ushort u;
            public ushort v;
            public UV16(float x, float y)
            {
                u = Mathf.FloatToHalf(x);
                v = Mathf.FloatToHalf(y);
            }
        } 

        private static NativeArray<Vector3> s_vertexArray;
        private static NativeArray<UV16> s_uvsArray;
        private static NativeArray<ushort> s_trianglesArray;
        
        private static VertexAttributeDescriptor s_positionLayout;
        private static VertexAttributeDescriptor s_uvLayout;

        private static int s_estimateCapacity;
        private static int[] s_vertexIndexMap; 
        private static int s_mapWidth;
        // private static int s_mapHeight; // 暂时未用到

        // 缓存当前处理的Block信息，避免传递过多参数
        private static int s_currentStartGridX;
        private static int s_currentStartGridZ;
        private static float s_currentFogHeight;
        private static int s_currentVertexCount;
        
        // Static reference to FogData to replace singleton access
        public static FogData CurrentFogData;

        public static Vector3 MeshOffset = Vector3.zero;

        public static void InitSingleFogMeshGenerator(int meshSizeX, int meshSizeY)
        {
            if (s_vertexArray.IsCreated) Dispose();

            s_estimateCapacity =  (meshSizeX * 2 + 1) * (meshSizeY * 2 + 1);
            
            s_vertexArray = new NativeArray<Vector3>(s_estimateCapacity, Allocator.Persistent);
            s_uvsArray = new NativeArray<UV16>(s_estimateCapacity, Allocator.Persistent);
            s_trianglesArray = new NativeArray<ushort>(s_estimateCapacity * 8, Allocator.Persistent);

            s_mapWidth = meshSizeX * 2 + 1;
            // s_mapHeight = meshSizeY * 2 + 1;
            s_vertexIndexMap = new int[s_mapWidth * (meshSizeY * 2 + 1)];

            s_positionLayout = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
            s_uvLayout = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, stream: 1);
        }

        public static void Dispose()
        {
            if(s_vertexArray.IsCreated) s_vertexArray.Dispose();
            if(s_uvsArray.IsCreated) s_uvsArray.Dispose();
            if(s_trianglesArray.IsCreated) s_trianglesArray.Dispose();
            s_vertexIndexMap = null;
            CurrentFogData = null;
        }

        // ==========================================
        // Init safety: allow generator to be used even if FogManager.InitSingleFogMeshGenerator()
        // has not been called yet (e.g. FogManager builds border meshes during construction).
        // ==========================================
        private static void EnsureInitializedForSparse()
        {
            // Sparse only needs vertex layouts + small temp buffers (4 verts / 6 indices),
            // so a minimal init is enough.
            if (s_estimateCapacity <= 0 || !s_vertexArray.IsCreated)
            {
                InitSingleFogMeshGenerator(1, 1); // => capacity 9
            }
        }

        private static void EnsureInitializedForDense(int blockGridCountX, int blockGridCountZ)
        {
            int needX = Mathf.Max(1, blockGridCountX);
            int needZ = Mathf.Max(1, blockGridCountZ);

            if (s_estimateCapacity <= 0 || !s_vertexArray.IsCreated)
            {
                InitSingleFogMeshGenerator(needX, needZ);
                return;
            }

            int requiredCapacity = (needX * 2 + 1) * (needZ * 2 + 1);
            if (requiredCapacity > s_estimateCapacity)
            {
                // Re-init to a larger buffer if we ever encounter a larger block than the initial estimate.
                InitSingleFogMeshGenerator(needX, needZ);
            }
        }


        // GenerateSparseMeshBlock 保持不变，省略...
        public static void GenerateSparseMeshBlock(Mesh mesh, int startGridX, int startGridZ, int blockGridCountX, int blockGridCountZ, float height)
        {
            // Ensure static buffers/layouts are initialized (border meshes may call this before any other generator).
            if (s_vertexIndexMap == null)
                EnsureInitializedForSparse();
             // 为了节省篇幅这里不重复粘贴，该函数逻辑简单不涉及大量剔除
             // 如果你需要这里也剔除，逻辑同下
             if (GetMeshGeneratedState(mesh) == MeshState.Uninitialized)
            {
                mesh.Clear();
                int vertexCapacity = 4;
                mesh.MarkDynamic();
                mesh.SetVertexBufferParams(vertexCapacity, s_positionLayout, s_uvLayout);
                mesh.SetIndexBufferParams(vertexCapacity * 6, IndexFormat.UInt16);
                mesh.subMeshCount = 1;
                mesh.bounds = new Bounds( 
                    new Vector3((startGridX + blockGridCountX / 2.0f)
                    , height / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                    new Vector3(blockGridCountX, height, blockGridCountZ));
            }

            int startX, startZ;
            int endX, endZ;
            startX = startGridX;
            startZ = startGridZ;
            endX = (startGridX + blockGridCountX);
            endZ = (startGridZ + blockGridCountZ);

            s_vertexArray[0] = new Vector3(startX, height, startZ) + MeshOffset;
            s_vertexArray[1] = new Vector3(endX, height, startZ) + MeshOffset;
            s_vertexArray[2] = new Vector3(startX, height, endZ) + MeshOffset;
            s_vertexArray[3] = new Vector3(endX, height, endZ) + MeshOffset;

            s_uvsArray[0] = new UV16(1, 1);
            s_uvsArray[1] = new UV16(1, 1);
            s_uvsArray[2] = new UV16(1, 1);
            s_uvsArray[3] = new UV16(1, 1);

            s_trianglesArray[0] = (ushort)0;
            s_trianglesArray[1] = (ushort)2;
            s_trianglesArray[2] = (ushort)1;
            s_trianglesArray[3] = (ushort)1;
            s_trianglesArray[4] = (ushort)2;
            s_trianglesArray[5] = (ushort)3; 
            
            mesh.SetVertexBufferData(s_vertexArray, 0, 0, 4, stream:0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetVertexBufferData(s_uvsArray, 0, 0, 4, stream:1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetIndexBufferData(s_trianglesArray, 0, 0, 6, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            
            var subDesc = new SubMeshDescriptor(indexStart: 0, indexCount: 6, topology: MeshTopology.Triangles) { baseVertex = 0 };
            mesh.SetSubMesh(0, subDesc);
            mesh.bounds = new Bounds( new Vector3((startGridX + blockGridCountX / 2.0f) , height / 2.0f, (startGridZ + blockGridCountZ / 2.0f)),  new Vector3(blockGridCountX, height, blockGridCountZ));
        }
        
        /// <summary>
        /// 生成单个mesh块的数据 (支持混合分辨率 1x1 和 2x2)
        /// </summary>
        public static void GenerateDenseMeshBlock(Mesh mesh, int startGridX, int startGridZ, 
            int blockGridCountX, int blockGridCountZ, float fogHeight)
        {
            
            EnsureInitializedForDense(blockGridCountX, blockGridCountZ);
// 设置静态上下文，供后续方法使用
            s_currentStartGridX = startGridX;
            s_currentStartGridZ = startGridZ;
            s_currentFogHeight = fogHeight;
            s_currentVertexCount = 0; // 重置计数

            if (GetMeshGeneratedState(mesh) != MeshState.Dense)
            {
                mesh.MarkDynamic();
                int requiredCapacity = (blockGridCountX * 2 + 1) * (blockGridCountZ * 2 + 1);
                if (requiredCapacity > s_estimateCapacity) {
                    Debug.LogError($"[FogMesh] Mesh size too large! {requiredCapacity} > {s_estimateCapacity}");
                    return; 
                }
                mesh.bounds = new Bounds( 
                    new Vector3((startGridX + blockGridCountX / 2.0f)
                        , fogHeight / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                    new Vector3(blockGridCountX, fogHeight, blockGridCountZ));
            }

            int currentTriangleCount = 0;
            
            // 重置映射表
            int mapW = blockGridCountX * 2 + 1;
            int mapH = blockGridCountZ * 2 + 1;
            s_mapWidth = mapW;
            int mapSize = mapW * mapH;
            
            if (s_vertexIndexMap == null || s_vertexIndexMap.Length < mapSize) 
                s_vertexIndexMap = new int[mapSize];
                
            // 使用 Array.Fill 会更快 (如果Unity版本支持)，这里保持循环
            for(int i=0; i<mapSize; i++) s_vertexIndexMap[i] = -1;

            // 逐格子遍历
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    bool needSubdivision = CheckIfNeedSubdivision(startGridX, startGridZ, localX, localZ);

                    if (!needSubdivision)
                    {
                        // === 模式 A: 1x1 标准格子 ===
                        int subX = localX * 2;
                        int subZ = localZ * 2;
                        
                        // 注意：这里不再获取 int vBL，而是传递 subX, subZ 坐标
                        // 1x1 格子由四个角点组成
                        TryAddTriangle(subX, subZ, subX, subZ + 2, subX + 2, subZ, ref currentTriangleCount); // BL -> TL -> BR
                        TryAddTriangle(subX, subZ + 2, subX + 2, subZ + 2, subX + 2, subZ, ref currentTriangleCount); // TL -> TR -> BR
                    }
                    else
                    {
                        // === 模式 B: 2x2 细分格子 ===
                        int subX = localX * 2;
                        int subZ = localZ * 2;

                        // 定义9个点的局部坐标偏移
                        // 角点
                        // BL(0,0), BR(2,0), TL(0,2), TR(2,2)
                        // 中点
                        // B_Mid(1,0), T_Mid(1,2), L_Mid(0,1), R_Mid(2,1), Center(1,1)

                        // 1. 左下角 (BL -> L_Mid -> B_Mid)
                        TryAddTriangle(subX, subZ, subX, subZ + 1, subX + 1, subZ, ref currentTriangleCount);
                        // 2. 右下角 (BR -> B_Mid -> R_Mid) -> 注意顺序 BR(2,0) -> B_Mid(1,0) -> R_Mid(2,1)
                        TryAddTriangle(subX + 2, subZ, subX + 1, subZ, subX + 2, subZ + 1, ref currentTriangleCount);
                        // 3. 左上角 (TL -> T_Mid -> L_Mid) -> TL(0,2) -> T_Mid(1,2) -> L_Mid(0,1)
                        TryAddTriangle(subX, subZ + 2, subX + 1, subZ + 2, subX, subZ + 1, ref currentTriangleCount);
                        // 4. 右上角 (TR -> R_Mid -> T_Mid) -> TR(2,2) -> R_Mid(2,1) -> T_Mid(1,2)
                        TryAddTriangle(subX + 2, subZ + 2, subX + 2, subZ + 1, subX + 1, subZ + 2, ref currentTriangleCount);

                        // 中心四个三角形
                        // Center(1,1)
                        // 下方: Center -> B_Mid -> L_Mid (错误? 应该是 Center -> R_Mid(x) -> B_Mid )
                        // 按照你原始代码顺时针：
                        // 下: Center(1,1) -> B_Mid(1,0) -> L_Mid(0,1) 
                        TryAddTriangle(subX + 1, subZ + 1, subX + 1, subZ, subX, subZ + 1, ref currentTriangleCount);
                        // 右: Center -> R_Mid(2,1) -> B_Mid(1,0)
                        TryAddTriangle(subX + 1, subZ + 1, subX + 2, subZ + 1, subX + 1, subZ, ref currentTriangleCount);
                        // 上: Center -> T_Mid(1,2) -> R_Mid(2,1)
                        TryAddTriangle(subX + 1, subZ + 1, subX + 1, subZ + 2, subX + 2, subZ + 1, ref currentTriangleCount);
                        // 左: Center -> L_Mid(0,1) -> T_Mid(1,2)
                        TryAddTriangle(subX + 1, subZ + 1, subX, subZ + 1, subX + 1, subZ + 2, ref currentTriangleCount);
                    }
                }
            }
            
            // 应用到mesh
            if (s_currentVertexCount > 0)
            {
                mesh.SetVertexBufferParams(s_currentVertexCount, s_positionLayout, s_uvLayout);
                mesh.SetIndexBufferParams(currentTriangleCount, IndexFormat.UInt16);
                mesh.subMeshCount = 1;

                mesh.SetVertexBufferData(s_vertexArray, 0, 0, s_currentVertexCount, stream: 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                mesh.SetVertexBufferData(s_uvsArray, 0, 0, s_currentVertexCount, stream: 1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                mesh.SetIndexBufferData(s_trianglesArray, 0, 0, currentTriangleCount, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                var subDesc = new SubMeshDescriptor(indexStart: 0, indexCount: currentTriangleCount, topology: MeshTopology.Triangles)
                {
                    baseVertex = 0
                };
                mesh.SetSubMesh(0, subDesc);
            }
            else
            {
                mesh.Clear();
            }

            mesh.bounds = new Bounds( 
                new Vector3((startGridX + blockGridCountX / 2.0f)
                    , fogHeight / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                new Vector3(blockGridCountX, fogHeight, blockGridCountZ));
        }

        // ==========================================
        // 核心修改：惰性创建与剔除逻辑
        // ==========================================

        /// <summary>
        /// 尝试添加一个三角形。
        /// 1. 先计算3个点的高度。
        /// 2. 如果高度都极低（解锁），则直接返回（剔除）。
        /// 3. 如果有效，才真正去创建/获取顶点索引，并写入 IndexBuffer。
        /// </summary>
        private static void TryAddTriangle(int subX1, int subZ1, int subX2, int subZ2, int subX3, int subZ3, ref int triCount)
        {
            // 1. 预计算高度 (不创建顶点)
            float h1 = GetProjectedHeight(subX1, subZ1);
            float h2 = GetProjectedHeight(subX2, subZ2);
            float h3 = GetProjectedHeight(subZ3, subZ3); // 注意：这里你原本参数可能有笔误，确保 xyz 对应正确
            // 修正参数传递：
            h3 = GetProjectedHeight(subX3, subZ3);

            // 2. 剔除检测
            const float CULL_THRESHOLD = 0.01f;
            // 如果三个点都在地面（已解锁），则不生成三角形
            if (h1 < CULL_THRESHOLD && h2 < CULL_THRESHOLD && h3 < CULL_THRESHOLD)
            {
                return;
            }

            // 3. 真正创建/获取顶点索引
            int v1 = GetOrCreateVertexLazy(subX1, subZ1);
            int v2 = GetOrCreateVertexLazy(subX2, subZ2);
            int v3 = GetOrCreateVertexLazy(subX3, subZ3);

            // 4. 写入三角形索引
            s_trianglesArray[triCount++] = (ushort)v1;
            s_trianglesArray[triCount++] = (ushort)v2;
            s_trianglesArray[triCount++] = (ushort)v3;
        }

        // 纯计算，不产生副作用
        private static float GetProjectedHeight(int localSubX, int localSubZ)
        {
            int globalSubX = s_currentStartGridX * 2 + localSubX;
            int globalSubZ = s_currentStartGridZ * 2 + localSubZ;
            
            VertexFogState state = CalculateVertexState(globalSubX, globalSubZ);
            
            switch (state)
            {
                case VertexFogState.Locked: return s_currentFogHeight;
                case VertexFogState.Half:   return s_currentFogHeight * 0.5f;
                default:                    return 0f; 
            }
        }

        // 真正的顶点创建逻辑 (Lazy)
        private static int GetOrCreateVertexLazy(int localSubX, int localSubZ)
        {
            int mapIndex = localSubZ * s_mapWidth + localSubX;
            
            // 如果已经创建过，直接返回
            if (s_vertexIndexMap[mapIndex] != -1)
            {
                return s_vertexIndexMap[mapIndex];
            }

            // --- 创建新顶点逻辑 ---
            int globalSubX = s_currentStartGridX * 2 + localSubX;
            int globalSubZ = s_currentStartGridZ * 2 + localSubZ;

            VertexFogState state = CalculateVertexState(globalSubX, globalSubZ);

            float h = 0f;
            switch (state)
            {
                case VertexFogState.Locked: h = s_currentFogHeight; break;
                case VertexFogState.Half:   h = s_currentFogHeight * 0.5f; break;
                case VertexFogState.Unlocked: h = 0f; break; 
            }

            float worldX = globalSubX * 0.5f;
            float worldZ = globalSubZ * 0.5f;
            
            int newIndex = s_currentVertexCount;
            s_currentVertexCount++; // 只有这里才增加计数
            
            s_vertexArray[newIndex] = new Vector3(worldX, h, worldZ) + MeshOffset;
            
            int gridX = globalSubX >> 1;
            int gridZ = globalSubZ >> 1;
            
            // Use the static CurrentFogData instead of FogManager.instance
            FogManager.FOG_TYPE info = FogManager.FOG_TYPE.Locked;
            if (CurrentFogData != null)
            {
                info = CurrentFogData.GetGridInfoMesh(gridX, gridZ);
            }
            
            s_uvsArray[newIndex] = GetVertexColor(state, info);

            s_vertexIndexMap[mapIndex] = newIndex;
            return newIndex;
        }

        // ... CalculateVertexState, IsGridUnlocked, GetVertexColor 等辅助函数保持不变 ...
        // 记得要把它们复制过来或者保持在类里
        
        // 核心算法：完全移除浮点运算，基于整数坐标计算顶点状态 (保持不变)
        private static VertexFogState CalculateVertexState(int gx, int gz)
        {
            // ... (同你原本代码) ...
            bool isHalfX = (gx & 1) != 0; 
            bool isHalfZ = (gz & 1) != 0;
            int gridX = gx >> 1;
            int gridZ = gz >> 1;
            if (isHalfX && isHalfZ) {
                // Use static reference
                var info = FogManager.FOG_TYPE.Locked;
                if (CurrentFogData != null) info = CurrentFogData.GetGridInfoMesh(gridX, gridZ);

                if (info == FogManager.FOG_TYPE.Unlocked) return VertexFogState.Unlocked;
                int neighborUnlockedCount = 0;
                if (IsGridUnlocked(gridX - 1, gridZ)) neighborUnlockedCount++;
                if (IsGridUnlocked(gridX + 1, gridZ)) neighborUnlockedCount++;
                if (IsGridUnlocked(gridX, gridZ - 1)) neighborUnlockedCount++;
                if (IsGridUnlocked(gridX, gridZ + 1)) neighborUnlockedCount++;
                if (neighborUnlockedCount >= 2) return VertexFogState.Half;
                return VertexFogState.Locked;
            }
            if (!isHalfX && !isHalfZ) {
                int unlockedCount = 0;
                if (IsGridUnlocked(gridX - 1, gridZ - 1)) unlockedCount++;
                if (IsGridUnlocked(gridX,     gridZ - 1)) unlockedCount++;
                if (IsGridUnlocked(gridX - 1, gridZ))     unlockedCount++;
                if (IsGridUnlocked(gridX,     gridZ))     unlockedCount++;
                if (unlockedCount == 0) return VertexFogState.Locked;
                if (unlockedCount == 1) return VertexFogState.Half;
                return VertexFogState.Unlocked;
            }
            int side1X, side1Z, side2X, side2Z;
            if (isHalfX) {
                side1X = gridX; side1Z = gridZ;
                side2X = gridX; side2Z = gridZ - 1;
            } else {
                side1X = gridX;     side1Z = gridZ;
                side2X = gridX - 1; side2Z = gridZ;
            }
            if (IsGridUnlocked(side1X, side1Z) || IsGridUnlocked(side2X, side2Z)) return VertexFogState.Unlocked;
            return VertexFogState.Locked;
        }

        private static bool IsGridUnlocked(int x, int z) {
            if (CurrentFogData == null) return false;
            return CurrentFogData.GetGridInfoMesh(x, z) == FogManager.FOG_TYPE.Unlocked;
        }
        
        private static UV16 GetVertexColor(VertexFogState state, FogManager.FOG_TYPE gridInfo) {
            if (gridInfo == FogManager.FOG_TYPE.Unlocking) return new UV16(state == VertexFogState.Locked ? 1 : 0, 1);
            switch (state) {
                case VertexFogState.Unlocked: return new UV16(0, 0);
                case VertexFogState.Half: return new UV16(0.5f, 0);
                default: return new UV16(1, 0);
            }
        }
        
        // ... CheckIfNeedSubdivision 保持不变 ...
        private static bool CheckIfNeedSubdivision(int startGridX, int startGridZ, int localX, int localZ)
        {
             int globalSubX = (startGridX + localX) * 2;
             int globalSubZ = (startGridZ + localZ) * 2;
             if (CalculateVertexState(globalSubX, globalSubZ) != VertexFogState.Locked) return true;
             if (CalculateVertexState(globalSubX + 2, globalSubZ) != VertexFogState.Locked) return true;
             if (CalculateVertexState(globalSubX, globalSubZ + 2) != VertexFogState.Locked) return true;
             if (CalculateVertexState(globalSubX + 2, globalSubZ + 2) != VertexFogState.Locked) return true;
             return false;
        }

        public static void GenerateSeperateUnlockFogMesh(Mesh mesh, List<Vector2Int> gridList,float _fogHeight)
        {
             // 保持不变
             int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
             foreach (var c in gridList) { minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y); maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y); }
             int W = maxX - minX + 1; int H = maxY - minY + 1;
             GenerateDenseMeshBlock(mesh, minX, minY, W, H, _fogHeight);
        }

        public enum MeshState { Uninitialized, Sparse, Dense }
        private static MeshState GetMeshGeneratedState(Mesh mesh) {
            if (mesh.vertexCount < 1) return MeshState.Uninitialized;
            if (mesh.vertexCount < 10) return MeshState.Sparse;
            return MeshState.Dense;
        }
    }
}
