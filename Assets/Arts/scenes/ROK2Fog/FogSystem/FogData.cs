using UnityEngine;

namespace FogManager
{
    public struct FogVertex
    {
        public float height;
    }

    /// <summary>
    /// 存储地形角点数据，支持高效的顶点高度查询
    /// Stores terrain vertex data and supports efficient vertex height queries.
    /// </summary>
    public class FogData
    {
        private FogVertex[,] _vertexData;       // 存每个格子的四个顶点高度，四个相邻grid共用
        private FogManager.FOG_TYPE[,] _gridData; // 存每个格子的解锁状态
        private float _fogHeight;               // 生成迷雾的高度数据
        private float _dataCellSize;            // 数据格子大小（米）
        private int _dataGridCountX;            // 数据格子数量X（cell数量）
        private int _dataGridCountZ;            // 数据格子数量Z（cell数量）
        private int _vertexCountX;              // 角点数量X（比格子数多1）
        private int _vertexCountZ;              // 角点数量Z（比格子数多1）

        /// <summary>
        /// 初始化并生成所有地形角点数据
        /// Initialize and generate all terrain vertex data.
        /// </summary>
        /// <param name="mapWidth">地图总宽度（米）</param>
        /// <param name="mapHeight">地图总高度（米）</param>
        /// <param name="dataCellSize">数据格子大小（米）</param>
        /// <param name="fogHeight">迷雾高度</param>
        public void Initialize(float mapWidth, float mapHeight, float dataCellSize, float fogHeight)
        {
            _dataCellSize = dataCellSize;
            // 根据地图总尺寸和数据格子大小计算实际数据格子数量
            _dataGridCountX = Mathf.CeilToInt(mapWidth / dataCellSize);
            _dataGridCountZ = Mathf.CeilToInt(mapHeight / dataCellSize);
            // 角点数量比格子数量多1（因为格子的边界需要角点）
            _vertexCountX = _dataGridCountX + 1;
            _vertexCountZ = _dataGridCountZ + 1;
            _fogHeight = fogHeight;

            // 初始化角点数据数组
            _vertexData = new FogVertex[_vertexCountX, _vertexCountZ];
            _gridData = new FogManager.FOG_TYPE[_dataGridCountX, _dataGridCountZ];
            
            // 生成所有角点数据
            GenerateAllVertexData();
            
            Debug.Log($"[FogData] Initialized: Map {mapWidth}x{mapHeight}m, Cell {_dataCellSize}m, Grids {_dataGridCountX}x{_dataGridCountZ}, Vertices {_vertexCountX}x{_vertexCountZ}");
        }

        /// <summary>
        /// 生成所有角点数据
        /// </summary>
        private void GenerateAllVertexData()
        {
            // 默认全部初始化为迷雾高度
            for (int x = 0; x < _vertexCountX; x++)
            {
                for (int z = 0; z < _vertexCountZ; z++)
                {
                    _vertexData[x, z].height = _fogHeight;
                }
            }
        }
        
        /// <summary>
        /// 设置指定角点的高度
        /// </summary>
        private void SetVertexHeight(int x, int z, float height)
        {
            if (x >= 0 && x < _vertexCountX && z >= 0 && z < _vertexCountZ)
            {
                _vertexData[x, z].height = height;
            }
        }
        
        /// <summary>
        /// 获取指定角点坐标的角点数据
        /// </summary>
        public FogVertex GetVertex(int x, int z)
        {
            if (x < 0 || x >= _vertexCountX || z < 0 || z >= _vertexCountZ)
                return new FogVertex { height = 0f };
                
            return _vertexData[x, z];
        }
        
        /// <summary>
        /// 获取指定角点坐标的高度
        /// </summary>
        public float GetVertexHeight(int x, int z)
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
        /// 逻辑坐标查询
        /// </summary>
        private FogManager.FOG_TYPE GetGridInfoLogical(int cellX, int cellZ)
        {
            if (cellX >= 0 && cellX < _dataGridCountX && cellZ >= 0 && cellZ < _dataGridCountZ)
            {
                // 检查周围是否有 Unlocking 状态 (传染逻辑)
                // 只要周围有 Unlocking，当前也视为 Unlocking (用于平滑过渡)
                bool isUnlocking = false;
                
                // 检查自身
                if (_gridData[cellX, cellZ] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;
                // 检查左边
                else if (cellX > 0 && _gridData[cellX - 1, cellZ] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;
                // 检查下边
                else if (cellZ > 0 && _gridData[cellX, cellZ - 1] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;
                // 检查左下
                else if (cellX > 0 && cellZ > 0 && _gridData[cellX - 1, cellZ - 1] == FogManager.FOG_TYPE.Unlocking) isUnlocking = true;

                if (isUnlocking)
                {
                    return FogManager.FOG_TYPE.Unlocking;
                }
                return _gridData[cellX, cellZ];
            }
            return FogManager.FOG_TYPE.Locked;
        }

        /// <summary>
        /// 根据世界坐标获取最近角点的高度
        /// </summary>
        public float GetHeightAtWorldPos(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / _dataCellSize);
            int z = Mathf.RoundToInt(worldPos.z / _dataCellSize);
            return GetVertexHeight(x, z);
        }
        
        /// <summary>
        /// 高效获取指定格子的四个角点高度（专为FogManager优化）
        /// </summary>
        /// <param name="cellX">格子X坐标</param>
        /// <param name="cellZ">格子Z坐标</param>
        public bool GetCellCornerHeights(int cellX, int cellZ, out float bottomLeft, out float bottomRight, out float topRight, out float topLeft)
        {
            if (cellX < 0 || cellX >= _dataGridCountX || cellZ < 0 || cellZ >= _dataGridCountZ)
            {
                bottomLeft = bottomRight = topRight = topLeft = 0f;
                return false;
            }
            
            // 直接获取四个角点的高度，已知坐标有效，直接访问数组以提高性能
            bottomLeft = _vertexData[cellX, cellZ].height;
            bottomRight = _vertexData[cellX + 1, cellZ].height;
            topRight = _vertexData[cellX + 1, cellZ + 1].height;
            topLeft = _vertexData[cellX, cellZ + 1].height;
            
            return true;
        }
        
        /// <summary>
        /// 解锁指定格子（将其四个角点高度设为0）
        /// 如果这个格子已经解锁了， 那么直接回传false，表明无需解锁。
        /// </summary>
        public bool UnlockCell(int cellX, int cellZ)
        {
            if (cellX < 0 || cellX >= _dataGridCountX || cellZ < 0 || cellZ >= _dataGridCountZ) return false;

            if (_gridData[cellX, cellZ] == FogManager.FOG_TYPE.Unlocked) return false;
            
            _gridData[cellX, cellZ] = FogManager.FOG_TYPE.Unlocked;
            SetVertexHeight(cellX, cellZ, 0f);         // 左下
            SetVertexHeight(cellX + 1, cellZ, 0f);     // 右下
            SetVertexHeight(cellX + 1, cellZ + 1, 0f); // 右上
            SetVertexHeight(cellX, cellZ + 1, 0f);     // 左上

            return true;
        }

        public void SetCellUnlocking(int cellX, int cellZ)
        {
            if (cellX >= 0 && cellX < _dataGridCountX && cellZ >= 0 && cellZ < _dataGridCountZ)
            {
                _gridData[cellX, cellZ] = FogManager.FOG_TYPE.Unlocking;
            }
        }
    }
}