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

                mesh.SetVertexBufferParams(s_estimateCapacity, s_positionLayout, s_uvLayout);
                mesh.SetIndexBufferParams(s_estimateCapacity * 8, IndexFormat.UInt16);
                mesh.subMeshCount = 1;
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

                    // 优化：如果格子完全解锁，直接跳过生成三角形 (与旧版 TriangleGenerationType.None 逻辑一致)
                    if (FogManager.instance.FogData.GetGridInfo(globalGridX, globalGridZ) == FogManager.FOG_TYPE.Unlocked)
                    {
                        continue;
                    }

                    // 判断是否需要细分
                    bool needSubdivision = CheckIfNeedSubdivision(globalGridX, globalGridZ, fogHeight);

                    if (!needSubdivision)
                    {
                        // === 模式 A: 1x1 标准格子 ===
                        int subX = localX * 2;
                        int subZ = localZ * 2;
                        
                        // 获取或创建 4 个角点
                        int vBL = GetOrCreateVertex(subX, subZ, globalGridX, globalGridZ, 0, 0, fogHeight, mapW, ref currentVertexCount);
                        int vBR = GetOrCreateVertex(subX + 2, subZ, globalGridX, globalGridZ, 1, 0, fogHeight, mapW, ref currentVertexCount);
                        int vTL = GetOrCreateVertex(subX, subZ + 2, globalGridX, globalGridZ, 0, 1, fogHeight, mapW, ref currentVertexCount);
                        int vTR = GetOrCreateVertex(subX + 2, subZ + 2, globalGridX, globalGridZ, 1, 1, fogHeight, mapW, ref currentVertexCount);

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
                        int vBL = GetOrCreateVertex(subX, subZ, globalGridX, globalGridZ, 0, 0, fogHeight, mapW, ref currentVertexCount);
                        int vBR = GetOrCreateVertex(subX + 2, subZ, globalGridX, globalGridZ, 1, 0, fogHeight, mapW, ref currentVertexCount);
                        int vTL = GetOrCreateVertex(subX, subZ + 2, globalGridX, globalGridZ, 0, 1, fogHeight, mapW, ref currentVertexCount);
                        int vTR = GetOrCreateVertex(subX + 2, subZ + 2, globalGridX, globalGridZ, 1, 1, fogHeight, mapW, ref currentVertexCount);
                        
                        // 边中点和中心点
                        int vB_Mid = GetOrCreateVertex(subX + 1, subZ, globalGridX, globalGridZ, 0.5f, 0, fogHeight, mapW, ref currentVertexCount);
                        int vT_Mid = GetOrCreateVertex(subX + 1, subZ + 2, globalGridX, globalGridZ, 0.5f, 1, fogHeight, mapW, ref currentVertexCount);
                        int vL_Mid = GetOrCreateVertex(subX, subZ + 1, globalGridX, globalGridZ, 0, 0.5f, fogHeight, mapW, ref currentVertexCount);
                        int vR_Mid = GetOrCreateVertex(subX + 2, subZ + 1, globalGridX, globalGridZ, 1, 0.5f, fogHeight, mapW, ref currentVertexCount);
                        int vCenter = GetOrCreateVertex(subX + 1, subZ + 1, globalGridX, globalGridZ, 0.5f, 0.5f, fogHeight, mapW, ref currentVertexCount);

                        // 生成 4 个小 Quad (统一让对角线连接 Center，保证对称性)
                        // 左下块: bl=vBL, tr=vCenter. 需要 Flip (BL-TR)
                        AddQuadTriangles(vBL, vB_Mid, vL_Mid, vCenter, ref currentTriangleCount, true); 
                        
                        // 右下块: tl=vCenter, br=vBR. 默认 (TL-BR) 即可
                        AddQuadTriangles(vB_Mid, vBR, vCenter, vR_Mid, ref currentTriangleCount, false); 
                        
                        // 左上块: tl=vTL, br=vCenter. 默认 (TL-BR) 即可
                        AddQuadTriangles(vL_Mid, vCenter, vTL, vT_Mid, ref currentTriangleCount, false); 
                        
                        // 右上块: bl=vCenter, tr=vTR. 需要 Flip (BL-TR)
                        AddQuadTriangles(vCenter, vR_Mid, vT_Mid, vTR, ref currentTriangleCount, true);
                    }
                }
            }
            
            // 应用到mesh
            // 1) 写顶点数据（按 stream）
            mesh.SetVertexBufferData(s_vertexArray, 0, 0, currentVertexCount, stream:0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetVertexBufferData(s_uvsArray, 0, 0, currentVertexCount, stream:1,
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
            mesh.bounds = new Bounds( 
                new Vector3((startGridX + blockGridCountX / 2.0f)
                    , fogHeight / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                new Vector3(blockGridCountX, fogHeight, blockGridCountZ));

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
        private static bool CheckIfNeedSubdivision(int globalX, int globalZ, float fogHeight)
        {
            // 如果已经解锁了，不需要细分 (保持 1x1 简单网格)
            if (FogManager.instance.FogData.GetGridInfo(globalX, globalZ) == FogManager.FOG_TYPE.Unlocked)
                return false;

            // 如果是 Locked：
            // 检查四个角点高度。如果四个角点都 >= fogHeight，说明周围也是 Locked，不需要细分。
            // 只要有一个角点 < fogHeight (被拉低)，就需要细分来支撑中心。
            FogManager.instance.FogData.GetCellCornerHeights(globalX, globalZ, out float bl, out float br, out float tr, out float tl);
            
            float threshold = fogHeight - 0.01f;
            if (bl < threshold || br < threshold || tr < threshold || tl < threshold)
                return true;

            return false;
        }

        // 辅助：获取或创建顶点
        private static int GetOrCreateVertex(int subX, int subZ, int gridX, int gridZ, float offsetX, float offsetZ, 
            float fogHeight, int mapWidth, ref int vertexCount)
        {
            int mapIndex = subZ * mapWidth + subX;
            if (s_vertexIndexMap[mapIndex] != -1)
            {
                return s_vertexIndexMap[mapIndex];
            }

            // 创建新顶点
            int newIndex = vertexCount;
            vertexCount++;

            // 计算实际世界坐标
            float worldX = gridX + offsetX;
            float worldZ = gridZ + offsetZ;
            
            // 计算高度 (核心逻辑：中心点独立控制)
            float h = GetHybridHeight(worldX, worldZ, fogHeight, offsetX, offsetZ, gridX, gridZ);
            
            // 设置位置
            s_vertexArray[newIndex] = new Vector3(worldX, h, worldZ);
            
            // 设置颜色
            FogManager.FOG_TYPE info = FogManager.instance.FogData.GetGridInfo(gridX, gridZ);
            
            // 修正：UV逻辑需要严格跟随格子状态，防止 Locked 格子的边缘点被误判为 Unlocked
            bool isUnlockedVertex;
            if (info == FogManager.FOG_TYPE.Locked)
            {
                // 如果格子是 Locked，那么它的所有顶点（即使是被邻居拉低到0的边缘点）都应该表现为 Locked
                isUnlockedVertex = false;
            }
            else if (info == FogManager.FOG_TYPE.Unlocked)
            {
                isUnlockedVertex = true;
            }
            else // Unlocking
            {
                // Unlocking 状态下，根据实际高度动态判断
                isUnlockedVertex = h < 0.001f;
            }

            s_uvsArray[newIndex] = GetVertexColor(isUnlockedVertex, info);

            // 记录缓存
            s_vertexIndexMap[mapIndex] = newIndex;
            return newIndex;
        }

        // 计算混合高度
        private static float GetHybridHeight(float worldX, float worldZ, float fogHeight, float offsetX, float offsetZ,
            int currentGridX, int currentGridZ)
        {
            // 1. 判断是否是中心点 (使用传入的 offset 避免浮点误差)
            float fracX = offsetX - Mathf.Floor(offsetX);
            float fracZ = offsetZ - Mathf.Floor(offsetZ);
            
            bool isHalfX = Mathf.Abs(fracX - 0.5f) < 0.01f;
            bool isHalfZ = Mathf.Abs(fracZ - 0.5f) < 0.01f;

            if (isHalfX && isHalfZ)
            {
                // 纯中心点
                int cx = currentGridX + Mathf.FloorToInt(offsetX);
                int cz = currentGridZ + Mathf.FloorToInt(offsetZ);
                // 对于中心点，只要不是 Unlocked，就保持高高度
                var state = FogManager.instance.FogData.GetGridInfo(cx, cz);
                if (state != FogManager.FOG_TYPE.Unlocked) return fogHeight; 
                return 0f;
            }
            
            // 2. 整数角点
            if (!isHalfX && !isHalfZ)
            {
                return FogManager.instance.FogData.GetVertexHeight(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldZ));
            }

            // 3. 边中点逻辑优化：
            // 直接基于 currentGridX/Z 和 offsetX/offsetZ 推导相邻格子，避免 worldPos 反推的浮点误差
            int g1x = currentGridX + Mathf.FloorToInt(offsetX);
            int g1z = currentGridZ + Mathf.FloorToInt(offsetZ);
            int g2x = g1x;
            int g2z = g1z;
            
            if (isHalfX) 
            {
                // 点在 (x.5, z.0) -> 横向边中点，Z方向是边界
                // 如果 offsetZ 接近 0 (下边)，连接 Current 和 Current-Z
                // 如果 offsetZ 接近 1 (上边)，连接 Current+Z 和 Current
                if (fracZ < 0.1f) // offsetZ 的小数部分接近0 (即整数)
                {
                    // g1z 是当前行，g2z 是下一行
                    // 但要注意：如果 offsetZ=1，Mathf.FloorToInt(offsetZ)=1。g1z 已经是 current+1 了。
                    // 所以这里的 g1z 实际上就是 "Upper Grid"。
                    // 我们只需要找到 "Lower Grid"。
                    // 边界线在 Z = currentGridZ + offsetZ
                    // Upper Grid index = currentGridZ + Floor(offsetZ) if offsetZ is int? 
                    // 不，如果 offsetZ=1，这点位于 Grid(x, z+1) 的下边，和 Grid(x, z) 的上边。
                    // 简单来说：边界线 Z 坐标为 BZ。
                    // 格子1：Z = BZ. 格子2：Z = BZ - 1.
                    
                    // 利用 worldZ (它是准确的整数)
                    int boundaryZ = Mathf.RoundToInt(worldZ);
                    g1z = boundaryZ;     // Upper Grid
                    g2z = boundaryZ - 1; // Lower Grid
                }
                else
                {
                    // 不应该发生，因为 isHalfX 意味着 fracZ 是 0 (即 offsetZ 是整数)
                    // 防御性代码
                    int boundaryZ = Mathf.RoundToInt(worldZ);
                    g1z = boundaryZ;
                    g2z = boundaryZ - 1;
                }
            }
            else // isHalfZ
            {
                // 点在 (x.0, z.5) -> 纵向边中点，X方向是边界
                int boundaryX = Mathf.RoundToInt(worldX);
                g1x = boundaryX;     // Right Grid
                g2x = boundaryX - 1; // Left Grid
            }

            var s1 = FogManager.instance.FogData.GetGridInfo(g1x, g1z);
            var s2 = FogManager.instance.FogData.GetGridInfo(g2x, g2z);

            // 如果任意一侧是 Unlocked，这条边就是"岸边"，必须服从角点插值（通常是0）
            if (s1 == FogManager.FOG_TYPE.Unlocked || s2 == FogManager.FOG_TYPE.Unlocked)
            {
                float floorX = Mathf.Floor(worldX);
                float floorZ = Mathf.Floor(worldZ);
                float ceilX = Mathf.Ceil(worldX);
                float ceilZ = Mathf.Ceil(worldZ);
                
                float h1 = FogManager.instance.FogData.GetVertexHeight((int)floorX, (int)floorZ);
                float h2 = FogManager.instance.FogData.GetVertexHeight((int)ceilX, (int)ceilZ);
                return (h1 + h2) * 0.5f;
            }
            
            // 如果两侧都是 Locked (或 Unlocking)，这条边是"内陆边"或"悬崖边"
            return fogHeight;
        }
        
        
        /// <summary>
        /// 获取三角形生成类型
        /// </summary>
        private static TriangleGenerationType GetTriangleGenerationType(int cellX, int cellZ)
        {
            // 获取格子四个角点的高度
            float bottomLeft, bottomRight, topRight, topLeft;
            if (!FogManager.instance.FogData.GetCellCornerHeights(cellX, cellZ, out bottomLeft, out bottomRight, out topRight, out topLeft))
            {
                return TriangleGenerationType.Standard; // 如果获取失败，默认生成标准三角形
            }
            
            // 统计解锁的角点数量（高度为0表示解锁）
            int unlockedCorners = 0;
            if (Mathf.Approximately(bottomLeft, 0f)) unlockedCorners++;
            if (Mathf.Approximately(bottomRight, 0f)) unlockedCorners++;
            if (Mathf.Approximately(topRight, 0f)) unlockedCorners++;
            if (Mathf.Approximately(topLeft, 0f)) unlockedCorners++;
            
            // 根据解锁角点数量返回生成类型
            if (unlockedCorners == 4)
            {
                return TriangleGenerationType.None; // 四个角点都解锁，不生成三角形
            }
            else if (unlockedCorners == 3)
            {
                return TriangleGenerationType.SingleTriangle; // 三个角点解锁，生成单个三角形
            }
            else
            {
                return TriangleGenerationType.Standard; // 其他情况生成标准三角形
            }
        }
        
        /// <summary>
        /// 生成单个三角形（当三个角点解锁，一个角点未解锁时）
        /// </summary>
        private static void GenerateSingleTriangle(int bottomLeft, int bottomRight, 
            int topLeft, int topRight, int cellX, int cellZ, ref int triangleCount)
        {
            // 获取格子四个角点的高度，确定哪个角点未解锁
            float blHeight, brHeight, trHeight, tlHeight;
            if (!FogManager.instance.FogData.GetCellCornerHeights(cellX, cellZ, out blHeight, out brHeight, out trHeight, out tlHeight))
            {
                return;
            }
            
            // 判断哪个角点未解锁（高度不为0）
            bool blUnlocked = blHeight < 0.001f;
            bool brUnlocked = brHeight < 0.001f;
            bool trUnlocked = trHeight < 0.001f;
            bool tlUnlocked = tlHeight < 0.001f;
            
            // 根据未解锁的角点生成相应的三角形（逆时针顺序）
            if (!blUnlocked) // bottomLeft 未解锁
            {
                // 三角形：bottomLeft -> topLeft -> bottomRight
                s_trianglesArray[triangleCount] = (ushort)bottomLeft;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)topLeft;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)bottomRight;
                triangleCount++;
            }
            else if (!brUnlocked) // bottomRight 未解锁
            {
                // 三角形：bottomRight -> bottomLeft -> topRight
                s_trianglesArray[triangleCount] = (ushort)bottomRight;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)bottomLeft;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)topRight;
                triangleCount++;
            }
            else if (!trUnlocked) // topRight 未解锁
            {
                // 三角形：topRight -> topLeft -> bottomRight
                s_trianglesArray[triangleCount] = (ushort)topLeft;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)topRight;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)bottomRight;
                triangleCount++;
            }
            else if (!tlUnlocked) // topLeft 未解锁
            {
               
                // 三角形：topLeft -> bottomLeft -> topRight
                s_trianglesArray[triangleCount] = (ushort)bottomLeft;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)topLeft;
                triangleCount++;
                s_trianglesArray[triangleCount] = (ushort)topRight;
                triangleCount++;
            }
        }
        
        /// <summary>
        /// 生成标准的两个三角形（一个格子）
        /// </summary>
        private static void GenerateStandardTriangles(int bottomLeft, int bottomRight, 
            int topLeft, int topRight, ref int triangleCount)
        {
            // 第一个三角形（逆时针：左下-左上-右下）
            s_trianglesArray[triangleCount] = (ushort)bottomLeft;
            triangleCount++;
            s_trianglesArray[triangleCount] = (ushort)topLeft;
            triangleCount++;
            s_trianglesArray[triangleCount] = (ushort)bottomRight;
            triangleCount++;
            
            // 第二个三角形（逆时针：左上-右上-右下）
            s_trianglesArray[triangleCount] = (ushort)topLeft;
            triangleCount++;
            s_trianglesArray[triangleCount] = (ushort)topRight;
            triangleCount++;
            s_trianglesArray[triangleCount] = (ushort)bottomRight;
            triangleCount++;
            
            
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
