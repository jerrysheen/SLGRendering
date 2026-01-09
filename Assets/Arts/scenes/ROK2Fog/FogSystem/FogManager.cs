using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using FogSystem;
using UnityEngine;
public class FogManager : MonoBehaviourSingle<FogManager>
{
    public enum FOG_TYPE
    {
        Default = 0,
        Locked = 1,
        Unlocked = 2,
        Unlocking = 3,
    }

    public FogData FogData { get; set;}
    public FogSystem.FogSystem FogSystem { get; set;}
    public int MapWidth { get; set;} = 1200;
    public int MapHeight { get; set;} = 1200;
    public int SingleMeshWidth { get; set;} = 120;
    public int SingleMeshHeight { get; set;} = 120;
    public int GridCellSize { get; set;} = 3;
    // 生成的迷雾的mesh高低落差
    public int FogHeight { get; set;} = 6;
    
    // 生成的迷雾的GameObject Transform的z
    public int FogGoPositionZ { get; set;} = 6;

    // 默认的缩放尺寸，5
    public int GlobalScale { get; set; } = 5;

    private bool isInitialized = false;
    // Start is called before the first frame update
    

    public void InitFogManager()
    {
        if (isInitialized) return;
        Debug.LogWarning("迷雾系统初始化创建");
        isInitialized = true;
        if(FogData == null)FogData = new FogData();
        if(FogSystem == null)FogSystem = new FogSystem.FogSystem(MapWidth, MapHeight, GridCellSize, FogHeight, FogGoPositionZ, GlobalScale);
        FogData.Initialize(MapWidth, MapHeight, GridCellSize, FogHeight);
        SingleFogMeshGenerator.InitSingleFogMeshGenerator((int)(25)
            , (int)(25));

    }

    private void LateUpdate()
    {
        if(FogSystem != null)FogSystem.LateUpdate();
        //OnSceneGUI();
        //Update Quad Visibility Here
    }
    
    public void OnDisable()
    {
        // 确保在禁用或AssemblyReload时清理资源，防止Mesh泄漏
        ShutDownFogSystem();
    }
    
    public void OnDestroy()
    {
        ShutDownFogSystem();
    }
    
    public void ShutDownFogSystem()
    {
        Debug.LogWarning("迷雾系统销毁");
        FogData = null;
        if(FogSystem != null)FogSystem.Destroy();
        FogSystem = null;
        isInitialized = false;
        SingleFogMeshGenerator.Dispose();
    }

    public void UpdateFogGridInfo(Vector2Int gridID, bool unlockFog)
    {
        FOG_TYPE type = unlockFog ? FOG_TYPE.Unlocked : FOG_TYPE.Locked;
        if (unlockFog)
        {
            if (FogData.UnlockCell(gridID.x, gridID.y))
            {
                //FogSystem.SetFogMeshDirty(x / GridCellSize, y / GridCellSize);
                // 稍微扩大脏的区域， 这样确保所有标记为lock的区域直接生成大面片就ok
                FogSystem.InsertArea(new OptimizedFogQuad.BoundsAABB(new Vector2Int(gridID.x - 1, gridID.y - 1),
                    new Vector2Int(gridID.x + 2, gridID.y + 2)), (int)type);
            }
        }
    }

    public void TryUnlockingArea(byte[] gridStatusBits)
    {
        // 外部传入的是基于 MapWidth * MapHeight 的位数组
        int mapW = MapWidth / GridCellSize;;
        int mapH = MapHeight / GridCellSize;;
        int totalCells = mapW * mapH;

        if (gridStatusBits.Length < (totalCells + 7) / 8)
        {
            Debug.LogError($"Grid status array size too small! Expected bytes: {(totalCells + 7) / 8}, Actual: {gridStatusBits.Length}");
            return;
        }

        bool hasChange = false;

        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                int index = y * mapW + x;
                int byteIndex = index / 8;
                int bitIndex = index % 8;

                // 检查对应位是否为1
                bool isUnlocked = (gridStatusBits[byteIndex] & (1 << bitIndex)) != 0;

                if (isUnlocked)
                {
                    
                    if (FogData.UnlockCell(x, y))
                    {
                        // 每次解锁一个逻辑格子，就刷新该格子及其周围的一小块区域
                        // 这种方式虽然调用次数多，但 InsertArea 内部有优化，且避免了全图刷新带来的逻辑错误
                        FogSystem.InsertArea(new OptimizedFogQuad.BoundsAABB(
                            new Vector2Int(x - 1, y - 1),
                            new Vector2Int(x + 2, y + 2)
                        ), (int)FOG_TYPE.Unlocked);
                        
                    }
                }
            }
        }
    }

    // 用于取消迷雾解锁时的表现
    // public void OnSuccessfullyUnlockFogArea()
    // {
    //     FogSystem.MarkAreaUnlockingSucceed(1.0f, 0.0f, 3.0f);
    // }    

    // 用于取消迷雾解锁时的表现
    // public void OnCancelUnlocking()
    // {
    //     FogSystem.OnCancelUnlocking();
    // }

    // 真正进行mesh重建工作。
    public void RebuildFogMesh()    
    {
        FogSystem.RebuildMesh();
    }
}
