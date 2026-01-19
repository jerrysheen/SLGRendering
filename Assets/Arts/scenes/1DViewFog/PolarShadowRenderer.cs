using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class PolarShadowRenderer : MonoBehaviour
{
    [Header("Core Settings")]
    public Transform player;
    public float maxViewRadius = 20f;
    [Tooltip("Shadow Map 分辨率，宽度建议 1024 或 2048")]
    public int resolution = 1024;

    [Header("Resources")]
    public Shader shadowGenShader;
    // 拖入场景中所有需要阻挡视线的物体的父节点，或者合并后的 MeshRenderer
    public List<Renderer> obstacleRenderer; 

    private RenderTexture _shadowMap;
    private Material _shadowMat;
    private CommandBuffer _cmd;
    
    // Shader Property IDs
    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPos");
    private static readonly int MaxRadiusID = Shader.PropertyToID("_MaxRadius");
    private static readonly int ShadowMapID = Shader.PropertyToID("_PolarShadowMap");

    void Start()
    {
        if (shadowGenShader == null)
        {
            Debug.LogError("请指定 PolarShadowGen_URP Shader");
            enabled = false;
            return;
        }

        // 1. 创建 1D 纹理 (实际是 Nx1 的 2D 纹理)
        // RFloat 格式对于存储距离至关重要，能保证精度
        _shadowMap = new RenderTexture(resolution, 1, 0, RenderTextureFormat.RFloat);
        _shadowMap.name = "PolarShadowMap";
        _shadowMap.filterMode = FilterMode.Bilinear; // 线性采样能获得免费的软阴影基础
        _shadowMap.wrapMode = TextureWrapMode.Clamp; // 极坐标不需要 Repeat，因为 -PI 和 PI 是两端
        _shadowMap.Create();

        // 2. 创建材质
        _shadowMat = new Material(shadowGenShader);

        // 3. 初始化 CommandBuffer
        _cmd = new CommandBuffer();
        _cmd.name = "Polar Shadow Gen";

        // 4. 设置全局纹理，供后续的渲染流程（地面、后处理）使用
        Shader.SetGlobalTexture(ShadowMapID, _shadowMap);
    }

    void LateUpdate()
    {
        if (player == null || obstacleRenderer == null) return;

        RenderShadowMap();
    }

    private void RenderShadowMap()
    {
        _cmd.Clear();

        // Step 1: 设置渲染目标为我们的 ShadowMap
        _cmd.SetRenderTarget(_shadowMap);

        // Step 2: 清屏
        // 颜色设置为 (maxViewRadius, 0, 0, 0)。
        // 意味着：如果没有东西遮挡，深度就是最大半径（无限远）。
        _cmd.ClearRenderTarget(true, true, new Color(maxViewRadius, 0, 0, 0));
        var cam = Camera.main;
        // Step 3: 更新 Shader 变量
        _cmd.SetGlobalVector(PlayerPosID, player.position);
        _cmd.SetGlobalFloat(MaxRadiusID, maxViewRadius);

        // Step 4: 绘制障碍物
        // 这里只是简单的 DrawRenderer。如果是大量物体，建议使用 DrawMeshInstanced
        foreach (var obstacle in obstacleRenderer)
        {
            _cmd.DrawRenderer(obstacle, _shadowMat);
        }
        // Step 5: 立即执行
        Graphics.ExecuteCommandBuffer(_cmd);
    }

    void OnDestroy()
    {
        if (_shadowMap != null) _shadowMap.Release();
        if (_shadowMat != null) Destroy(_shadowMat);
        if (_cmd != null) _cmd.Release();
    }
    
    // Debug 专用：在 Inspector 预览 RT
    // 这一步对于查错非常有用，能看到一条黑白变化的线
    /*
    void OnGUI() {
        if(_shadowMap != null) GUI.DrawTexture(new Rect(0, 0, 256, 50), _shadowMap, ScaleMode.ScaleToFit, false);
    }
    */
}