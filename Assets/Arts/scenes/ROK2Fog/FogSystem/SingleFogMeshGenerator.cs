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
        public static void InitSingleFogMeshGenerator(int meshSizeX, int meshSizeY)
        {
            s_estimateCapacity =  30 * 30; // 预估上限
            
            s_vertexArray = new NativeArray<Vector3>(s_estimateCapacity, Allocator.Persistent);
            s_uvsArray = new NativeArray<UV16>(s_estimateCapacity, Allocator.Persistent);
            s_trianglesArray = new NativeArray<ushort>(s_estimateCapacity * 6, Allocator.Persistent);

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
        /// 生成单个mesh块的数据
        /// </summary>
        /// <param name="mesh">要生成的mesh对象</param>
        /// <param name="startGridX">起始格子X坐标</param>
        /// <param name="startGridZ">起始格子Z坐标</param>
        /// <param name="blockGridCountX">块内格子数量X</param>
        /// <param name="blockGridCountZ">块内格子数量Z</param>
        /// <param name="cellSize">格子大小</param>
        public static void GenerateDenseMeshBlock(Mesh mesh, int startGridX, int startGridZ, 
            int blockGridCountX, int blockGridCountZ, float fogHeight)
        {
            if (GetMeshGeneratedState(mesh) != MeshState.Dense)
            {
                mesh.MarkDynamic();
                mesh.SetVertexBufferParams(s_estimateCapacity, s_positionLayout, s_uvLayout);
                mesh.SetIndexBufferParams(s_estimateCapacity * 6, IndexFormat.UInt16);
                mesh.subMeshCount = 1;
                mesh.bounds = new Bounds( 
                    new Vector3((startGridX + blockGridCountX / 2.0f)
                        , fogHeight / 2.0f, (startGridZ + blockGridCountZ / 2.0f)), 
                    new Vector3(blockGridCountX, fogHeight, blockGridCountZ));
            }

            int vertexCount = 0;
            int triangleCount = 0;
            // 计算顶点数量：每个格子需要4个顶点，但相邻格子共享顶点
            int vertexCountX = blockGridCountX + 1;
            int vertexCountZ = blockGridCountZ + 1;
            
            int indexCount   = blockGridCountX * blockGridCountZ * 6;
            Debug.Log($"[FogMesh] block=({blockGridCountX},{blockGridCountZ}) " +
                      $"vertexCount={vertexCount}, indexCount={indexCount}, " +
                      $"capacityV={s_estimateCapacity}, capacityI={s_estimateCapacity * 6}");
            // 生成所有顶点数据
            for (int localZ = 0; localZ <= blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX <= blockGridCountX; localX++)
                {
                    // 计算全局角点坐标
                    int globalVertexX = startGridX + localX;
                    int globalVertexZ = startGridZ + localZ;
                    
                    // 计算世界坐标
                    float worldX = globalVertexX;
                    float worldZ = globalVertexZ;
                    
                    // 从TerrainDataReader获取角点高度
                    float height = FogManager.instance.FogData.GetVertexHeight(globalVertexX, globalVertexZ);
                    
                    // 判断是否解锁（高度为0表示解锁）
                    FogManager.FOG_TYPE gridInfo = FogManager.instance.FogData.GetGridInfo(globalVertexX, globalVertexZ);
                    bool isUnlocked = height < 0.001f;
                    // 添加顶点数据
                    s_vertexArray[vertexCount] = new Vector3(worldX, height, worldZ);
                    s_uvsArray[vertexCount] = GetVertexColor(isUnlocked, gridInfo);
                    vertexCount++;
                }
            }
            
            // 生成三角形（基于格子的解锁状态）
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    // 检查当前格子的四个角点解锁状态，获取生成类型
                    TriangleGenerationType generationType = GetTriangleGenerationType(
                        startGridX + localX, startGridZ + localZ);
                    
                    // 计算四个顶点的索引
                    int bottomLeft = localZ * vertexCountX + localX;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = (localZ + 1) * vertexCountX + localX;
                    int topRight = topLeft + 1;
                    
                    switch (generationType)
                    {
                        case TriangleGenerationType.Standard:
                            // 生成标准的两个三角形
                            GenerateStandardTriangles(bottomLeft, bottomRight, topLeft, topRight, ref triangleCount);
                            break;
                        case TriangleGenerationType.SingleTriangle:
                            // 生成单个三角形（三个角点解锁，一个角点未解锁）
                            GenerateSingleTriangle( bottomLeft, bottomRight, topLeft, topRight,
                                startGridX + localX, startGridZ + localZ, ref triangleCount);
                            break;
                            
                        case TriangleGenerationType.None:
                        default:
                            // 不生成三角形（四个角点都解锁）
                            break;
                    }
                }
            }
            
            // 应用到mesh
            // 1) 写顶点数据（按 stream）
            mesh.SetVertexBufferData(s_vertexArray, 0, 0, vertexCount, stream:0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetVertexBufferData(s_uvsArray, 0, 0, vertexCount, stream:1,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            // 2) 写索引数据
            mesh.SetIndexBufferData(s_trianglesArray, 0, 0, triangleCount,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            
            var subDesc = new SubMeshDescriptor(indexStart: 0, indexCount: triangleCount, topology: MeshTopology.Triangles)
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
