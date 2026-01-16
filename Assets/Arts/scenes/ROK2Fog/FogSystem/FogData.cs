using UnityEngine;
using System.IO;
namespace FogSystem
{
    public struct FogVertex
    {
        public float height;
    }

    /// <summary>
    /// 存储地形角点数据，支持高效的顶点高度查询
    /// </summary>
    public  class FogData
    {
        private  FogVertex[,] vertexData;  // 存每个格子的四个顶点高度，四个相邻grid共用
        private  FogManager.FOG_TYPE[,] gridData;  // 存每个格子的解锁状态，预留unlocking，用来表现正在解锁
        private  float fogHeight;     // 生成迷雾的高度数据
        private  float dataCellSize;  // 数据格子大小（米）
        private  int dataGridCountX;  // 数据格子数量X（cell数量）
        private  int dataGridCountZ;  // 数据格子数量Z（cell数量）
        private  int vertexCountX;    // 角点数量X（比格子数多1）
        private  int vertexCountZ;    // 角点数量Z（比格子数多1）

        
        
        /// <summary>
        /// 初始化并生成所有地形角点数据
        /// </summary>
        /// <param name="mapWidth">地图总宽度（米）</param>
        /// <param name="mapHeight">地图总高度（米）</param>
        /// <param name="dataCellSize">数据格子大小（米）</param>
        public  void Initialize(float mapWidth, float mapHeight, float dataCellSize, float fogHeight)
        {
            this.dataCellSize = dataCellSize;
            // 根据地图总尺寸和数据格子大小计算实际数据格子数量
            this.dataGridCountX = Mathf.CeilToInt(mapWidth / dataCellSize);
            this.dataGridCountZ = Mathf.CeilToInt(mapHeight / dataCellSize);
            // 角点数量比格子数量多1（因为格子的边界需要角点）
            this.vertexCountX = dataGridCountX + 1;
            this.vertexCountZ = dataGridCountZ + 1;
            this.fogHeight = fogHeight;
            // 初始化角点数据数组
            vertexData = new FogVertex[vertexCountX, vertexCountZ];
            gridData = new FogManager.FOG_TYPE[dataGridCountX, dataGridCountZ];
            
            // 生成所有角点数据
            GenerateAllVertexData();
            
            Debug.Log($"FogData: 地图尺寸 {mapWidth}x{mapHeight}m，数据精度 {dataCellSize}m，生成了 {dataGridCountX}x{dataGridCountZ} 个格子，{vertexCountX}x{vertexCountZ} 个角点");
        }

        /// <summary>
        /// 生成所有角点数据
        /// </summary>
        private  void GenerateAllVertexData()
        {
            // 第一步：可随机生成，可直接按照预设高度生成
            for (int x = 0; x < vertexCountX; x++)
            {
                for (int z = 0; z < vertexCountZ; z++)
                {
                    vertexData[x, z].height = fogHeight;
                    
                }
            }
        }
        
        /// <summary>
        /// 判断指定格子是否解锁
        /// </summary>
        private  bool IsCellUnlocked(int cellX, int cellZ)
        {
            return gridData[cellX, cellZ] == FogManager.FOG_TYPE.Unlocked;
        }
        
        /// <summary>
        /// 设置指定角点的高度
        /// </summary>
        private  void SetVertexHeight(int x, int z, float height)
        {
            if (x >= 0 && x < vertexCountX && z >= 0 && z < vertexCountZ)
            {
                vertexData[x, z].height = height;
            }
        }
        
        /// <summary>
        /// 获取指定角点坐标的角点数据
        /// </summary>
        public  FogVertex GetVertex(int x, int z)
        {
            if (x < 0 || x >= vertexCountX || z < 0 || z >= vertexCountZ)
                return new FogVertex { height = 0f };
                
            return vertexData[x, z];
        }
        
        /// <summary>
        /// 获取指定角点坐标的高度
        /// </summary>
        public  float GetVertexHeight(int x, int z)
        {
            return GetVertex(x, z).height;
        }

                // =========================
        // Grid 查询：逻辑坐标 vs Mesh 坐标
        // 逻辑格子范围： [0 .. dataGridCountX-1]
        // Mesh格子范围： [0 .. dataGridCountX+1] （四周各多 1 格）
        // Mesh(1,1) <-> 逻辑(0,0)
        // =========================
        public FogManager.FOG_TYPE GetGridInfo(int cellX, int cellZ)
        {
            // 兼容旧调用：默认认为传入的是“逻辑坐标”
            return GetGridInfoLogical(cellX, cellZ);
        }

        /// <summary>
        /// Mesh 坐标查询：mesh(1,1) 对齐逻辑(0,0)，mesh(0,*) / mesh(*,0) 属于边缘补齐区域，永远视为 Locked。
        /// </summary>
        public FogManager.FOG_TYPE GetGridInfoMesh(int meshCellX, int meshCellZ)
        {
            return GetGridInfoLogical(meshCellX - 1, meshCellZ - 1);
        }

        /// <summary>
        /// 逻辑坐标查询（原始实现）
        /// </summary>
        private FogManager.FOG_TYPE GetGridInfoLogical(int cellX, int cellZ)
        {
            if (cellX >= 0 && cellX < dataGridCountX && cellZ >= 0 && cellZ < dataGridCountZ)
            {
                // 检查周围是否有 Unlocking 状态 (传染逻辑)
                // 注意边界检查，防止数组越界
                bool isUnlocking = false;
                if (cellX > 0 && cellZ > 0 && gridData[cellX - 1, cellZ - 1] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;
                else if (cellZ > 0 && gridData[cellX, cellZ - 1] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;
                else if (cellX > 0 && gridData[cellX - 1, cellZ] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;
                else if (gridData[cellX, cellZ] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;

                if (isUnlocking)
                {
                    return FogManager.FOG_TYPE.Unlocking;
                }
                return gridData[cellX, cellZ];
            }
            return FogManager.FOG_TYPE.Locked;
        }

        /// <summary>
        /// 根据世界坐标获取最近角点的高度
        /// </summary>
        public  float GetHeightAtWorldPos(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / dataCellSize);
            int z = Mathf.RoundToInt(worldPos.z / dataCellSize);
            return GetVertexHeight(x, z);
        }
        
        /// <summary>
        /// 高效获取指定格子的四个角点高度（专为FogSystem优化）
        /// </summary>
        /// <param name="cellX">格子X坐标</param>
        /// <param name="cellZ">格子Z坐标</param>
        /// <param name="bottomLeft">左下角点高度</param>
        /// <param name="bottomRight">右下角点高度</param>
        /// <param name="topRight">右上角点高度</param>
        /// <param name="topLeft">左上角点高度</param>
        /// <returns>是否成功获取</returns>
        public  bool GetCellCornerHeights(int cellX, int cellZ, out float bottomLeft, out float bottomRight, out float topRight, out float topLeft)
        {
            if (cellX < 0 || cellX >= dataGridCountX || cellZ < 0 || cellZ >= dataGridCountZ)
            {
                bottomLeft = bottomRight = topRight = topLeft = 0f;
                return false;
            }
            
            // 直接获取四个角点的高度
            bottomLeft = GetVertexHeight(cellX, cellZ);
            bottomRight = GetVertexHeight(cellX + 1, cellZ);
            topRight = GetVertexHeight(cellX + 1, cellZ + 1);
            topLeft = GetVertexHeight(cellX, cellZ + 1);
            
            return true;
        }
        
        /// <summary>
        /// 解锁指定格子（将其四个角点高度设为0）
        /// 如果这个格子已经解锁了， 那么直接回传false，表明无需解锁。
        /// </summary>
        public bool UnlockCell(int cellX, int cellZ)
        {
            if (cellX < 0 || cellX >= dataGridCountX || cellZ < 0 || cellZ >= dataGridCountZ) return false;

            if (cellX >= 0 && cellX < dataGridCountX && cellZ >= 0 && cellZ < dataGridCountZ)
            {
                if (gridData[cellX, cellZ] == FogManager.FOG_TYPE.Unlocked) return false;
                gridData[cellX, cellZ] = FogManager.FOG_TYPE.Unlocked;
                SetVertexHeight(cellX, cellZ, 0f);         // 左下
                SetVertexHeight(cellX + 1, cellZ, 0f);     // 右下
                SetVertexHeight(cellX + 1, cellZ + 1, 0f); // 右上
                SetVertexHeight(cellX, cellZ + 1, 0f);     // 左上
            }

            return true;
        }

        public void SetCellUnlocking(int cellX, int cellZ)
        {
            if (cellX >= 0 && cellX < dataGridCountX && cellZ >= 0 && cellZ < dataGridCountZ)
            {
                gridData[cellX, cellZ] = FogManager.FOG_TYPE.Unlocking;
            }
        }
    }
} 