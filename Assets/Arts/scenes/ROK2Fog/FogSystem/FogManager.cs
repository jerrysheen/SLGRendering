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
        SingleFogMeshGenerator.InitSingleFogMeshGenerator((int)(MapWidth / GridCellSize)
            , (int)(MapHeight/GridCellSize));

    }

    private void LateUpdate()
    {
        if(FogSystem != null)FogSystem.LateUpdate();
        //OnSceneGUI();
        //Update Quad Visibility Here
    }
    
    private void OnDestroy()
    {
        Debug.LogWarning("迷雾系统销毁");
        FogData = null;
        if(FogSystem != null)FogSystem.Destroy();
        FogSystem = null;
        isInitialized = false;
        SingleFogMeshGenerator.Dispose();
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
    public void UpdateFogAreaInfo(int AreaID, bool unlockFog)
    {
        // 得到一个unlockArea, 403840384, 那么就是第四层， 384 384中心位置。
        // 第四层格子长度在 private static int CalculateFogGridSizeByLayer(int layer)函数中计算
        int centerX = AreaID % 10000;
        int centerY = AreaID % 100000000 / 10000;
        int layer = AreaID / 100000000;
        int size = (int)Mathf.Pow(4, layer) / 2 * GridCellSize;
        // Debug.Log("ReceivedID:" + centerX + " , " + centerY + " , " + layer + " , " + size);
        FOG_TYPE type = unlockFog ? FOG_TYPE.Unlocked : FOG_TYPE.Locked;
        if (unlockFog)
        {
            for (int x = centerX - size; x < centerX + size; x++)
            {
                for (int y = centerY - size; y < centerY + size; y++)
                {
                    if(y < 0 || y > MapHeight || x < 0 || x > MapWidth) continue;
                    FogData.UnlockCell(x / GridCellSize, y / GridCellSize);
                }
            }
            
            FogSystem.InsertArea(new OptimizedFogQuad.BoundsAABB(new Vector2Int((int)(centerX - size) / GridCellSize - 1, (int)(centerY - size) / GridCellSize -1) ,
                new Vector2Int((int)(centerX + size) / GridCellSize +1, (int)(centerY + size) / GridCellSize + 1)), (int)type);
        }
        else
        {
            // todo
        }
    }

    public void TryUnlockingArea(List<int> gridIDs)
    {
        List<Vector2Int> gridPoints = new List<Vector2Int>();
        foreach (int gridID in gridIDs)
        {
            int centerX = gridID % 10000;
            int centerY = gridID % 100000000 / 10000;
            int layer = gridID / 100000000;
            int size = (int)Mathf.Pow(4, layer) / 2 * GridCellSize;
            if (size == 0)
            {
                int x = gridID % 10000 - (GridCellSize - 1) /2;
                int y = gridID / 10000 - (GridCellSize - 1) /2;
                gridPoints.Add(new Vector2Int(x / GridCellSize, y / GridCellSize));
                FogData.SetCellUnlocking(x / GridCellSize, y / GridCellSize);
            }
            else
            {
                for (int x = centerX - size; x < centerX + size; x++)
                {
                    for (int y = centerY - size; y < centerY + size; y++)
                    {
                        gridPoints.Add(new Vector2Int(x / GridCellSize, y / GridCellSize));
                        FogData.SetCellUnlocking(x / GridCellSize, y / GridCellSize);
                    }
                }  
            }
        }            

        FogSystem.GenerateUnlockingAreaFogGo(gridPoints);
        FogSystem.StartUnlockAreaFogGo();
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
