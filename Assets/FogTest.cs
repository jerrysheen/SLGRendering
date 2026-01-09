using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class FogTest : MonoBehaviour
{
    public Vector2Int grid;
    
    private void Awake()
    {
        FogManager.GetInstance().InitFogManager();
        FogManager.GetInstance().RebuildFogMesh();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // unity 退出游戏时调用
    void OnApplicationQuit()
    {
        FogManager.GetInstance().OnDestroy();
    }

    public void UnlockGrid()
    {
        FogManager.GetInstance().UpdateFogGridInfo(grid, true);
    }

    public string NewStringArray()
    {
        var manager = FogManager.GetInstance();
        // 直接使用 MapWidth 和 MapHeight 作为位数组的大小
        int mapW = manager.MapWidth / manager.GridCellSize;
        int mapH = manager.MapHeight / manager.GridCellSize;
        
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
        var manager = FogManager.GetInstance();
        // 直接使用 MapWidth 和 MapHeight 作为位数组的大小
        int mapW = manager.MapWidth / manager.GridCellSize;
        int mapH = manager.MapHeight / manager.GridCellSize;
        
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
        manager.TryUnlockingArea(statusData);
    }

    
    public void RebuildFogMesh()
    {
        FogManager.GetInstance().RebuildFogMesh();
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
        
        if (GUILayout.Button("RebuildFogMesh"))
        {
            fogTest.RebuildFogMesh();
        }
        
        if (GUILayout.Button("Test Bit Array Unlock"))
        {
            fogTest.TestUpdateFogByArray();
        }
    }
}
