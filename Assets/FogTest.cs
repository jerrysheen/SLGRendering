using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using FogManager;

public class FogTest : MonoBehaviour
{
    public Vector2Int grid;
    
    [SerializeField]
    private FogManager.FogManager _FogManager;

    private void Awake()
    {
        if (_FogManager == null)
        {
            _FogManager = FindObjectOfType<FogManager.FogManager>();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, 0); // 假设地面在 Y=0
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                HandleClick(hitPoint);
            }
        }
    }
    
    private void HandleClick(Vector3 worldPos)
    {
        if (_FogManager == null) return;

        // 计算网格坐标
        // World = Grid * (GlobalScale * CellSize)
        // Grid = World / (GlobalScale * CellSize)
        float unitSize = _FogManager.GlobalScale * _FogManager.GridCellSize;
        
        int gridX = Mathf.FloorToInt(worldPos.x / unitSize);
        int gridY = Mathf.FloorToInt(worldPos.z / unitSize);

        // 边界检查
        int mapW = _FogManager.MapWidth / _FogManager.GridCellSize;
        int mapH = _FogManager.MapHeight / _FogManager.GridCellSize;

        if (gridX >= 0 && gridX < mapW && gridY >= 0 && gridY < mapH)
        {
            Debug.Log($"Click World: {worldPos}, Unlock Grid: ({gridX}, {gridY})");
            _FogManager.UpdateFogGridInfo(new Vector2Int(gridX, gridY), true);
            _FogManager.RebuildFogMesh();
        
        }
    }

    // unity 退出游戏时调用
    void OnApplicationQuit()
    {
        // FogManager handles its own destruction via OnDestroy
    }

    public void UnlockGrid()
    {
        if (_FogManager != null)
        {
            _FogManager.UpdateFogGridInfo(grid, true);
        }
    }

    public void LockGrid()
    {
        if (_FogManager != null)
        {
            _FogManager.UpdateFogGridInfo(grid, false);
        }
    }

    public string NewStringArray()
    {
        if (_FogManager == null) return "";

        // 直接使用 MapWidth 和 MapHeight 作为位数组的大小
        int mapW = _FogManager.MapWidth / _FogManager.GridCellSize;
        int mapH = _FogManager.MapHeight / _FogManager.GridCellSize;
        
        int totalCount = mapW * mapH;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(totalCount);

        float scale = 0.1f;
        float offsetX = UnityEngine.Random.Range(0f, 1000f);
        float offsetY = UnityEngine.Random.Range(0f, 1000f);
        
        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                // 简单的柏林噪声生成
                float perlinValue = Mathf.PerlinNoise(x * scale + offsetX, y * scale + offsetY);
                // 阈值设为0.4，大于0.4为解锁(1)，否则为未解锁(0)
                sb.Append(perlinValue > 0.4f ? '1' : '0');
            }
        }

        string binaryPattern = sb.ToString(); 
        return binaryPattern;
    }

    public void TestUpdateFogByArray()
    {
        if (_FogManager == null) return;

        // 直接使用 MapWidth 和 MapHeight 作为位数组的大小
        int mapW = _FogManager.MapWidth / _FogManager.GridCellSize;
        int mapH = _FogManager.MapHeight / _FogManager.GridCellSize;
        
        int totalCount = mapW * mapH;
        int byteLength = Mathf.CeilToInt(totalCount / 8.0f);
        
        // 创建状态数组, 使用bit位存储
        byte[] statusData = new byte[byteLength];

        // 示例：构造跨越多行的测试数据 (假设 MapWidth = 1200)
        // 使用 StringBuilder 生成长字符串
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(new string('1', mapW));
        // 第1行(y=0): 前100个解锁，中间未解锁，最后100个解锁
        // 视觉效果：第一行两头是通的，中间是黑的
        if (mapW > 200)
        {   
            sb.Append(new string('1', 100));
            sb.Append(new string('0', mapW - 200));
            sb.Append(new string('1', 100));
        }
        else
        {
            sb.Append(new string('1', mapW));
        }

        // 第2行(y=1): 全部解锁
        // 视觉效果：第二行完全是一条通的长条
        sb.Append(new string('1', mapW));

        // 第3行(y=2): 间隔解锁 (101010...)
        // 视觉效果：第三行是虚线
        for (int k = 0; k < mapW / 2; k++) sb.Append("10");

        //string binaryPattern = sb.ToString(); 
        string binaryPattern = NewStringArray(); 
        
        // 我们从(0,0)开始，按照(x,y)顺序写入数据
        // binaryPattern的第0位对应(0,0)，第1位对应(1,0)... 第width位对应(0,1)
        
        // 将 binaryPattern 写入到 statusData 中
        for (int i = 0; i < binaryPattern.Length; i++)
        {
            // 计算当前 bit 对应的坐标
            // 按照行优先顺序：先填满一行(x: 0->mapW-1)，再换下一行(y++)
            int currentX = i % mapW;
            int currentY = i / mapW;

            if (currentY < mapH)
            {
                if (binaryPattern[i] == '1')
                {
                    int index = currentY * mapW + currentX;
                    int byteIndex = index / 8;
                    int bitIndex = index % 8;
                    statusData[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
        }

        // 调用 Manager 进行更新
        GCHandle handle = GCHandle.Alloc(statusData, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            _FogManager.TryUnlockingArea(ptr, totalCount);
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    
    public void RebuildFogMesh()
    {
        if (_FogManager != null)
        {
            _FogManager.RebuildFogMesh();
        }
    }

    [Header("Unlock Test")]
    public Vector2Int UnlockTestCenter;
    public int UnlockTestRadius = 1;

    public void TestUnlockEffect()
    {
        if (_FogManager == null) return;

        List<Vector2Int> gridList = new List<Vector2Int>();
        
        // Random Walk Generation for Irregular Continuous Area
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int current = UnlockTestCenter;
        visited.Add(current);
        gridList.Add(current);

        int steps = UnlockTestRadius * 10; // Number of steps based on radius
        Vector2Int[] directions = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 0; i < steps; i++)
        {
            Vector2Int dir = directions[UnityEngine.Random.Range(0, 4)];
            current += dir;
            
            // Constrain roughly within radius distance to keep it somewhat centered
            if (Vector2Int.Distance(current, UnlockTestCenter) > UnlockTestRadius * 2)
            {
                current = UnlockTestCenter; // Reset to center if too far
                continue;
            }

            if (visited.Add(current))
            {
                gridList.Add(current);
            }
        }

        // 1. Generate visual effect object
        _FogManager.GenerateUnlockingAreaFogGo(gridList);
        
        // 2. Update logical grid data
        foreach (var pos in gridList)
        {
            _FogManager.UpdateFogGridInfo(pos, true);
        }
        _FogManager.RebuildFogMesh(); // Rebuild mesh to reflect logical changes

        // 3. Start the unlock animation
        _FogManager.StartUnlockAreaFogGo();
    }

    [Header("Lock Test")]
    public Vector2Int LockTestCenter;
    public int LockTestRadius = 1;

    [Header("Blink Highlight Test")]
    public Vector2Int BlinkTestCenter;
    public int BlinkTestRadius = 1;
    [Tooltip("呼吸周期时间（秒）")]
    public float BlinkInterval = 1.0f;
    [Tooltip("强度倍数（1.5=柔和呼吸，2.0=明显闪烁）")]
    public float BlinkIntensityMultiplier = 1.5f;

    public void TestLockEffect()
    {
        if (_FogManager == null) return;

        List<Vector2Int> gridList = new List<Vector2Int>();
        
        // Random Walk Generation for Irregular Continuous Area
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int current = LockTestCenter;
        visited.Add(current);
        gridList.Add(current);

        int steps = LockTestRadius * 10; // Number of steps based on radius
        Vector2Int[] directions = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 0; i < steps; i++)
        {
            Vector2Int dir = directions[UnityEngine.Random.Range(0, 4)];
            current += dir;
            
            // Constrain roughly within radius distance to keep it somewhat centered
            if (Vector2Int.Distance(current, LockTestCenter) > LockTestRadius * 2)
            {
                current = LockTestCenter; // Reset to center if too far
                continue;
            }

            if (visited.Add(current))
            {
                gridList.Add(current);
            }
        }

        // 加锁流程（与解锁相反）
        // 1. 先更新逻辑网格数据（加锁）
        foreach (var pos in gridList)
        {
            _FogManager.UpdateFogGridInfo(pos, false); // false = 加锁
        }
        // 注意：这里不调用 RebuildFogMesh()，等动画结束后再调用
        
        // 2. 生成加锁的迷雾视觉对象
        _FogManager.GenerateUnlockingAreaFogGo(gridList);
        
        // 3. 启动加锁动画（从透明到不透明）
        _FogManager.StartLockAreaFogGo();
    }

    public void TestBlinkEffect()
    {
        if (_FogManager == null) return;

        List<Vector2Int> gridList = new List<Vector2Int>();
        
        // Random Walk Generation for Irregular Continuous Area
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int current = BlinkTestCenter;
        visited.Add(current);
        gridList.Add(current);

        int steps = BlinkTestRadius * 10; // Number of steps based on radius
        Vector2Int[] directions = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 0; i < steps; i++)
        {
            Vector2Int dir = directions[UnityEngine.Random.Range(0, 4)];
            current += dir;
            
            // Constrain roughly within radius distance to keep it somewhat centered
            if (Vector2Int.Distance(current, BlinkTestCenter) > BlinkTestRadius * 2)
            {
                current = BlinkTestCenter; // Reset to center if too far
                continue;
            }

            if (visited.Add(current))
            {
                gridList.Add(current);
            }
        }

        // 生成并开始闪烁
        _FogManager.GenerateBlinkAreaFogGo(gridList);
        _FogManager.StartBlinkAreaFogGo(BlinkInterval, BlinkIntensityMultiplier);
    }

    public void StopBlinkEffect()
    {
        if (_FogManager == null) return;
        _FogManager.StopBlinkAreaFogGo();
    }
}

[CustomEditor(typeof(FogTest))]
public class FogTestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        FogTest fogTest = (FogTest)target;
        if (GUILayout.Button("UnLockArea"))
        {
            fogTest.UnlockGrid();
        }        
        
        if (GUILayout.Button("LockArea"))
        {
            fogTest.LockGrid();
        }
        
        if (GUILayout.Button("RebuildFogMesh"))
        {
            fogTest.RebuildFogMesh();
        }
        
        if (GUILayout.Button("Test Bit Array Unlock"))
        {
            fogTest.TestUpdateFogByArray();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Test Unlock Effect"))
        {
            fogTest.TestUnlockEffect();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Test Lock Effect"))
        {
            fogTest.TestLockEffect();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Test Blink Effect"))
        {
            fogTest.TestBlinkEffect();
        }

        if (GUILayout.Button("Stop Blink Effect"))
        {
            fogTest.StopBlinkEffect();
        }
    }
}
