using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Rendering;

namespace FogSystem
{
    /// <summary>
    /// 三角形生成类型
    /// </summary>
    public enum TriangleGenerationType
    {
        None = 0,           // 不生成三角形（四个角点都解锁）
        Standard = 1,       // 生成标准两个三角形
        SingleTriangle = 2  // 生成单个三角形（三个角点解锁，一个未解锁）
    }

    /// <summary>
    /// 单个迷雾Mesh生成器
    /// 负责生成单个mesh块的所有逻辑，直接基于TerrainDataReader的角点数据
    /// </summary>
    public class SingleFogMeshGenerator
    {
        private enum VertexFogState
        {
            Locked,     // 高高度 (FogHeight)
            Half,       // 半高度 (FogHeight * 0.5)
            Unlocked    // 地面高度 (通常为0)
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
        // 映射表：记录Block内局部细分坐标 (2x, 2z) 对应的 顶点索引
        private static int[] s_vertexIndexMap; 
        private static int s_mapWidth;
        private static int s_mapHeight;

        public static void InitSingleFogMeshGenerator(int meshSizeX, int meshSizeY)
        {
            // 防止重复初始化导致 NativeArray 泄漏
            if (s_vertexArray.IsCreated) Dispose();

            // 预估上限：假设全部都细分，顶点数是 (2N+1)*(2N+1)
            // 比如 30*30 的格子，细分后是 60*60 个顶点，约 3600
            s_estimateCapacity =  (meshSizeX * 2 + 1) * (meshSizeY * 2 + 1);
            
            s_vertexArray = new NativeArray<Vector3>(s_estimateCapacity, Allocator.Persistent);
            s_uvsArray = new NativeArray<UV16>(s_estimateCapacity, Allocator.Persistent);
            s_trianglesArray = new NativeArray<ushort>(s_estimateCapacity * 8, Allocator.Persistent);

            s_mapWidth = meshSizeX * 2 + 1;
            s_mapHeight = meshSizeY * 2 + 1;
            s_vertexIndexMap = new int[s_mapWidth * s_mapHeight];

            s_positionLayout =
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
            s_uvLayout = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2,
                stream: 1);
        }

        public static void Dispose()
        {
            if(s_vertexArray.IsCreated) s_vertexArray.Dispose();
            if(s_uvsArray.IsCreated) s_uvsArray.Dispose();
            if(s_trianglesArray.IsCreated) s_trianglesArray.Dispose();
            s_vertexIndexMap = null;
        }

        // 直接生成一个mesh
        public static void GenerateSparseMeshBlock(Mesh mesh, int startGridX, int startGridZ, 
            int blockGridCountX, int blockGridCountZ, float height)
        {
            // 完全没被初始化
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

            s_vertexArray[0] = new Vector3(startX, height, startZ);
            s_vertexArray[1] = new Vector3(endX, height, startZ);
            s_vertexArray[2] = new Vector3(startX, height, endZ);
            s_vertexArray[3] = new Vector3(endX, height, endZ);

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
            
            // 1) 写顶点数据（按 stream）
            mesh.SetVertexBufferData(s_vertexArray, 0, 0, 4, stream:0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetVertexBufferData(s_uvsArray, 0, 0, 4, stream:1,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            // 2) 写索引数据
            mesh.SetIndexBufferData(s_trianglesArray, 0, 0, 6,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            
            var subDesc = new SubMeshDescriptor(indexStart: 0, indexCount: 6, topology: MeshTopology.Triangles)
            {
                baseVertex = 0
            };
            // 先别加 DontRecalculateBounds，确认 OK 后再加优化 flag
            mesh.SetSubMesh(0, subDesc);
            mesh.bounds = new Bounds( 
                new Vector3((startGridX + blockGridCountX / 2.0f)
                    , height / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                new Vector3(blockGridCountX, height, blockGridCountZ));
            //mesh.UploadMeshData(true);
        }
        
        
        /// <summary>
        /// 生成单个mesh块的数据 (支持混合分辨率 1x1 和 2x2)
        /// </summary>
        public static void GenerateDenseMeshBlock(Mesh mesh, int startGridX, int startGridZ, 
            int blockGridCountX, int blockGridCountZ, float fogHeight)
        {
            if (GetMeshGeneratedState(mesh) != MeshState.Dense)
            {
                mesh.MarkDynamic();
                
                // 确保 capacity 足够
                int requiredCapacity = (blockGridCountX * 2 + 1) * (blockGridCountZ * 2 + 1);
                if (requiredCapacity > s_estimateCapacity) {
                    Debug.LogError($"[FogMesh] Mesh size too large for pre-allocated buffer! {requiredCapacity} > {s_estimateCapacity}");
                    return; 
                }

                // mesh.SetVertexBufferParams(s_estimateCapacity, s_positionLayout, s_uvLayout);
                // mesh.SetIndexBufferParams(s_estimateCapacity * 8, IndexFormat.UInt16);
                // mesh.subMeshCount = 1;
                mesh.bounds = new Bounds( 
                    new Vector3((startGridX + blockGridCountX / 2.0f)
                        , fogHeight / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                    new Vector3(blockGridCountX, fogHeight, blockGridCountZ));
            }

            int currentVertexCount = 0;
            int currentTriangleCount = 0;
            
            // 重置顶点索引映射表 (-1 表示未生成)
            int mapW = blockGridCountX * 2 + 1;
            int mapH = blockGridCountZ * 2 + 1;
            int mapSize = mapW * mapH;
            
            // 安全扩容 (理论上 Init 时已经够大，但防止 BlockSize 变化)
            if (s_vertexIndexMap == null || s_vertexIndexMap.Length < mapSize) 
                s_vertexIndexMap = new int[mapSize];
                
            for(int i=0; i<mapSize; i++) s_vertexIndexMap[i] = -1;

            // 逐格子遍历
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    int globalGridX = startGridX + localX;
                    int globalGridZ = startGridZ + localZ;

                    // 优化：已移除直接跳过Unlocked的逻辑，因为Unlocked格子也需要细分扫描以保留边缘斜坡
                    // if (FogManager.instance.FogData.GetGridInfo(globalGridX, globalGridZ) == FogManager.FOG_TYPE.Unlocked) { continue; }

                    // 判断是否需要细分
                    bool needSubdivision = CheckIfNeedSubdivision(startGridX, startGridZ, localX, localZ);

                    if (!needSubdivision)
                    {
                        // === 模式 A: 1x1 标准格子 ===
                        int subX = localX * 2;
                        int subZ = localZ * 2;
                        
                        // 获取或创建 4 个角点
                        int vBL = GetOrCreateVertex(subX, subZ, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vBR = GetOrCreateVertex(subX + 2, subZ, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vTL = GetOrCreateVertex(subX, subZ + 2, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vTR = GetOrCreateVertex(subX + 2, subZ + 2, startGridX, startGridZ, fogHeight, ref currentVertexCount);

                        // 生成 2 个大三角形 (标准 Quad)
                        AddQuadTriangles(vBL, vBR, vTL, vTR, ref currentTriangleCount);
                    }
                    else
                    {
                        // === 模式 B: 2x2 细分格子 ===
                        int subX = localX * 2;
                        int subZ = localZ * 2;

                        // 获取或创建 9 个顶点
                        // 角点
                        int vBL = GetOrCreateVertex(subX, subZ, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vBR = GetOrCreateVertex(subX + 2, subZ, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vTL = GetOrCreateVertex(subX, subZ + 2, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vTR = GetOrCreateVertex(subX + 2, subZ + 2, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        
                        // 边中点和中心点
                        int vB_Mid = GetOrCreateVertex(subX + 1, subZ, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vT_Mid = GetOrCreateVertex(subX + 1, subZ + 2, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vL_Mid = GetOrCreateVertex(subX, subZ + 1, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vR_Mid = GetOrCreateVertex(subX + 2, subZ + 1, startGridX, startGridZ, fogHeight, ref currentVertexCount);
                        int vCenter = GetOrCreateVertex(subX + 1, subZ + 1, startGridX, startGridZ, fogHeight, ref currentVertexCount);

                        // === 1. 生成 4 个角落三角形 (Corner Triangles) ===
                        // 顺时针顺序 (CW)
                        // 左下角: BL -> L_Mid -> B_Mid
                        AddTriangleWithCulling(vBL, vL_Mid, vB_Mid, ref currentTriangleCount);
                        
                        // 右下角: BR -> B_Mid -> R_Mid
                        AddTriangleWithCulling(vBR, vB_Mid, vR_Mid, ref currentTriangleCount);
                        
                        // 左上角: TL -> T_Mid -> L_Mid
                        AddTriangleWithCulling(vTL, vT_Mid, vL_Mid, ref currentTriangleCount);
                        
                        // 右上角: TR -> R_Mid -> T_Mid
                        AddTriangleWithCulling(vTR, vR_Mid, vT_Mid, ref currentTriangleCount);
                        
                        // === 2. 生成 4 个中心三角形 (Center Triangles) ===
                        // 顺时针顺序 (CW)
                        // 下方: Center -> B_Mid -> L_Mid
                        AddTriangleWithCulling(vCenter, vB_Mid, vL_Mid, ref currentTriangleCount);
                        
                        // 右方: Center -> R_Mid -> B_Mid
                        AddTriangleWithCulling(vCenter, vR_Mid, vB_Mid, ref currentTriangleCount);
                        
                        // 上方: Center -> T_Mid -> R_Mid
                        AddTriangleWithCulling(vCenter, vT_Mid, vR_Mid, ref currentTriangleCount);
                        
                        // 左方: Center -> L_Mid -> T_Mid
                        AddTriangleWithCulling(vCenter, vL_Mid, vT_Mid, ref currentTriangleCount);
                    }
                }
            }
            
            // 应用到mesh
            if (currentVertexCount > 0)
            {
                // 按需重新分配Buffer大小，避免显存浪费
                mesh.SetVertexBufferParams(currentVertexCount, s_positionLayout, s_uvLayout);
                mesh.SetIndexBufferParams(currentTriangleCount, IndexFormat.UInt16);
                mesh.subMeshCount = 1;

                // 1) 写顶点数据（按 stream）
                mesh.SetVertexBufferData(s_vertexArray, 0, 0, currentVertexCount, stream: 0,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                mesh.SetVertexBufferData(s_uvsArray, 0, 0, currentVertexCount, stream: 1,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                // 2) 写索引数据
                mesh.SetIndexBufferData(s_trianglesArray, 0, 0, currentTriangleCount,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                var subDesc = new SubMeshDescriptor(indexStart: 0, indexCount: currentTriangleCount, topology: MeshTopology.Triangles)
                {
                    baseVertex = 0
                };
                // 先别加 DontRecalculateBounds，确认 OK 后再加优化 flag
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
            
            // 释放 CPU 端内存拷贝 (System Memory)，避免双份内存
            // mesh.UploadMeshData(true);

        }

        // 新增辅助方法：带剔除的三角形添加
        private static void AddTriangleWithCulling(int v1, int v2, int v3, ref int triCount)
        {
            // 检查三个顶点的高度
            float h1 = s_vertexArray[v1].y;
            float h2 = s_vertexArray[v2].y;
            float h3 = s_vertexArray[v3].y;
            
            // 如果三个顶点的高度都非常小（已解锁），则剔除该三角形
            // 这里使用 0.01f 作为阈值，假设高度为0代表完全解锁
            const float CULL_THRESHOLD = 0.01f;
            if (h1 < CULL_THRESHOLD && h2 < CULL_THRESHOLD && h3 < CULL_THRESHOLD)
            {
                return;
            }

            s_trianglesArray[triCount++] = (ushort)v1;
            s_trianglesArray[triCount++] = (ushort)v2;
            s_trianglesArray[triCount++] = (ushort)v3;
        }
        
        // 辅助：添加一个 Quad 的两个三角形
        // flipDiagonal = false: 剖分线 TL-BR (默认)
        // flipDiagonal = true:  剖分线 BL-TR
        private static void AddQuadTriangles(int bl, int br, int tl, int tr, ref int triCount, bool flipDiagonal = false)
        {
            if (!flipDiagonal)
            {
                // 三角形1: 左下 -> 左上 -> 右下
                s_trianglesArray[triCount++] = (ushort)bl;
                s_trianglesArray[triCount++] = (ushort)tl;
                s_trianglesArray[triCount++] = (ushort)br;
                
                // 三角形2: 左上 -> 右上 -> 右下
                s_trianglesArray[triCount++] = (ushort)tl;
                s_trianglesArray[triCount++] = (ushort)tr;
                s_trianglesArray[triCount++] = (ushort)br;
            }
            else
            {
                // 三角形1: 左下 -> 左上 -> 右上 (BL -> TL -> TR)
                s_trianglesArray[triCount++] = (ushort)bl;
                s_trianglesArray[triCount++] = (ushort)tl;
                s_trianglesArray[triCount++] = (ushort)tr;
                
                // 三角形2: 左下 -> 右上 -> 右下 (BL -> TR -> BR)
                s_trianglesArray[triCount++] = (ushort)bl;
                s_trianglesArray[triCount++] = (ushort)tr;
                s_trianglesArray[triCount++] = (ushort)br;
            }
        }

        // 辅助：判断是否需要细分
        private static bool CheckIfNeedSubdivision(int startGridX, int startGridZ, int localX, int localZ)
        {
            // 如果是 Locked：
            // 检查四个角点状态。如果四个角点都 == Locked，说明周围也是 Locked，不需要细分。
            // 只要有一个角点 != Locked (被拉低)，就需要细分来支撑中心。
            
            // 计算全局 SubGrid 坐标 (2x)
            int globalSubX = (startGridX + localX) * 2;
            int globalSubZ = (startGridZ + localZ) * 2;

            // 检查四个角点 (BL, BR, TL, TR)
            // BL: (x, z)
            // BR: (x+2, z)
            // TL: (x, z+2)
            // TR: (x+2, z+2)
            
            if (CalculateVertexState(globalSubX, globalSubZ) != VertexFogState.Locked) return true;
            if (CalculateVertexState(globalSubX + 2, globalSubZ) != VertexFogState.Locked) return true;
            if (CalculateVertexState(globalSubX, globalSubZ + 2) != VertexFogState.Locked) return true;
            if (CalculateVertexState(globalSubX + 2, globalSubZ + 2) != VertexFogState.Locked) return true;

            return false;
        }

        // 辅助：获取或创建顶点
        // startBlockGridX/Z: Block 的起始网格坐标
        // localSubX/Z: Block 内的局部 SubGrid 坐标 (0, 1, 2...)
        private static int GetOrCreateVertex(int localSubX, int localSubZ, int startBlockGridX, int startBlockGridZ, 
            float fogHeight, ref int vertexCount)
        {
            int mapIndex = localSubZ * s_mapWidth + localSubX;
            if (s_vertexIndexMap[mapIndex] != -1)
            {
                return s_vertexIndexMap[mapIndex];
            }

            // 计算全局 SubGrid 坐标
            int globalSubX = startBlockGridX * 2 + localSubX;
            int globalSubZ = startBlockGridZ * 2 + localSubZ;

            // 1. 计算状态 (纯整数逻辑)
            VertexFogState state = CalculateVertexState(globalSubX, globalSubZ);

            // 2. 根据状态决定高度
            float h = 0f;
            switch (state)
            {
                case VertexFogState.Locked: h = fogHeight; break;
                case VertexFogState.Half:   h = fogHeight * 0.5f; break;
                case VertexFogState.Unlocked: h = 0f; break; 
            }

            // 3. 计算世界坐标
            float worldX = globalSubX * 0.5f;
            float worldZ = globalSubZ * 0.5f;
            
            // 创建新顶点
            int newIndex = vertexCount;
            vertexCount++;
            
            s_vertexArray[newIndex] = new Vector3(worldX, h, worldZ);
            
            // 设置颜色/UV
            // 计算当前顶点所在的 Grid 坐标 (用于获取 GridInfo)
            // 右移1位相当于除以2
            int gridX = globalSubX >> 1;
            int gridZ = globalSubZ >> 1;
            
            FogManager.FOG_TYPE info = FogManager.instance.FogData.GetGridInfo(gridX, gridZ);
            bool isUnlockedVertex = (state == VertexFogState.Unlocked);

            // 特殊情况修正：Locked 格子即便被拉低到 0 (理论上不应该，但作为防御)，也不能视为 Unlocked UV
            if (info == FogManager.FOG_TYPE.Locked)
            {
                isUnlockedVertex = false;
            }

            s_uvsArray[newIndex] = GetVertexColor(isUnlockedVertex, info);

            // 记录缓存
            s_vertexIndexMap[mapIndex] = newIndex;
            return newIndex;
        }

        // 核心算法：完全移除浮点运算，基于整数坐标计算顶点状态
        private static VertexFogState CalculateVertexState(int gx, int gz)
        {
            // 利用位运算判断是否在半格位置
            // 奇数表示在 .5 位置
            bool isHalfX = (gx & 1) != 0; 
            bool isHalfZ = (gz & 1) != 0;
            
            // 坐标右移1位即除以2，得到对应的 Grid 索引
            int gridX = gx >> 1;
            int gridZ = gz >> 1;

            // === 情况 A: 中心点 (Odd, Odd) ===
            if (isHalfX && isHalfZ)
            {
                // 对于中心点，只要不是 Unlocked 格子，就保持 Locked 高度
                var info = FogManager.instance.FogData.GetGridInfo(gridX, gridZ);
                return (info == FogManager.FOG_TYPE.Unlocked) ? VertexFogState.Unlocked : VertexFogState.Locked;
            }

            // === 情况 B: 整数角点 (Even, Even) ===
            if (!isHalfX && !isHalfZ)
            {
                // 角点是周围四个格子的交点
                // TL(x-1, z) | TR(x, z)
                // -----------+-----------
                // BL(x-1, z-1)| BR(x, z-1)
                
                int unlockedCount = 0;
                if (IsGridUnlocked(gridX - 1, gridZ - 1)) unlockedCount++;
                if (IsGridUnlocked(gridX,     gridZ - 1)) unlockedCount++;
                if (IsGridUnlocked(gridX - 1, gridZ))     unlockedCount++;
                if (IsGridUnlocked(gridX,     gridZ))     unlockedCount++;

                if (unlockedCount == 0) return VertexFogState.Locked;
                if (unlockedCount == 1) return VertexFogState.Half;
                return VertexFogState.Unlocked; // 2个及以上解锁 -> 塌陷
            }

            // === 情况 C: 边中点 (Odd, Even) 或 (Even, Odd) ===
            int side1X, side1Z, side2X, side2Z;

            if (isHalfX) // 横向边中点 (Odd, Even) -> 上下两侧格子
            {
                // 边位于 gridZ 和 gridZ-1 之间
                side1X = gridX; side1Z = gridZ;     // 上 (Z)
                side2X = gridX; side2Z = gridZ - 1; // 下 (Z-1)
            }
            else // 纵向边中点 (Even, Odd) -> 左右两侧格子
            {
                // 边位于 gridX 和 gridX-1 之间
                side1X = gridX;     side1Z = gridZ; // 右 (X)
                side2X = gridX - 1; side2Z = gridZ; // 左 (X-1)
            }

            // 逻辑：只要任意一侧是 Unlocked，边中点就被拉低
            if (IsGridUnlocked(side1X, side1Z) || IsGridUnlocked(side2X, side2Z))
            {
                return VertexFogState.Unlocked;
            }
            
            return VertexFogState.Locked;
        }

        private static bool IsGridUnlocked(int x, int z)
        {
            return FogManager.instance.FogData.GetGridInfo(x, z) == FogManager.FOG_TYPE.Unlocked;
        }
        
        
        /// <summary>
        /// 根据解锁状态获取顶点颜色
        /// x控制，与地面高度， y控制是否要高亮。
        /// </summary>
        private static UV16 GetVertexColor(bool isUnlocked, FogManager.FOG_TYPE gridInfo)
        {
            if (gridInfo == FogManager.FOG_TYPE.Unlocking)
            {
                if (isUnlocked)
                {
                    return new UV16(0, 1); // 已解锁: (0, 1)
                }
                else
                {
                    return new UV16(1, 1); // 未解锁: (1, 1)
                }
            }

            if (isUnlocked)
            {
                return new UV16(0, 0); // 已解锁: (0, 1)
            }
            else
            {
                return new UV16(1, 0); // 未解锁: (1, 1)
            }
        }
        
        
        // 解锁Area功能， 主要思路是围绕要解锁的格子生成单独mesh，地格本身数据自己更新，
        // 单独mesh部分可以额外做功能。
        public static void GenerateSeperateUnlockFogMesh(Mesh mesh, List<Vector2Int> gridList,float _fogHeight)
        {
             // 得到最大最小格子。
             int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
             foreach (var c in gridList)
             {
                 minX = Mathf.Min(minX, c.x);
                 minY = Mathf.Min(minY, c.y);
                 maxX = Mathf.Max(maxX, c.x);
                 maxY = Mathf.Max(maxY, c.y);
             }
             int W = maxX - minX + 1;
             int H = maxY - minY + 1;
             GenerateDenseMeshBlock(mesh, minX, minY, W, H, _fogHeight);
             
        }

        public enum MeshState
        {
            Uninitialized,
            Sparse,
            Dense
        }

        // 通过简单判断vertex Count数量，来判断当前mesh的状态，是完全没生成还是生成过简单mesh，还是已经是
        // 致密网格。这个判断影响我们是否需要 mesh.SetVertexBufferParams
        private static MeshState GetMeshGeneratedState(Mesh mesh)
        {
            if (mesh.vertexCount < 1) return MeshState.Uninitialized;
            if (mesh.vertexCount < 10) return MeshState.Sparse;
            return MeshState.Dense;
        }
    }
}
