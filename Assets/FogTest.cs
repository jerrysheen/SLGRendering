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

    public void UnlockGrid()
    {
        FogManager.GetInstance().UpdateFogGridInfo(grid, true);
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
    }
}
