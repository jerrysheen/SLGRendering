using System.Collections.Generic;
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
    public List<Renderer> obstacleRenderer;

    private RenderTexture _shadowMap;
    private Material _shadowMat;
    private CommandBuffer _cmd;

    private static readonly int PlayerPosID  = Shader.PropertyToID("_PlayerPos");
    private static readonly int MaxRadiusID  = Shader.PropertyToID("_MaxRadius");
    private static readonly int ShadowMapID  = Shader.PropertyToID("_PolarShadowMap");

    void Start()
    {
        if (shadowGenShader == null)
        {
            Debug.LogError("请指定 PolarShadowGen_URP Shader");
            enabled = false;
            return;
        }

        // RGHalf = R16G16 float
        _shadowMap = new RenderTexture(resolution, 1, 0, RenderTextureFormat.RGHalf);
        _shadowMap.name = "PolarShadowMap_RG";
        _shadowMap.filterMode = FilterMode.Bilinear;
        // 两 pass 方案其实更推荐 Repeat + 采样时 frac(u)
        _shadowMap.wrapMode = TextureWrapMode.Repeat;
        _shadowMap.Create();

        _shadowMat = new Material(shadowGenShader);

        _cmd = new CommandBuffer();
        _cmd.name = "Polar Shadow Gen (RG)";

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

        _cmd.SetRenderTarget(_shadowMap);
        _cmd.SetViewport(new Rect(0, 0, resolution, 1));

        // clear: R/G 都是 maxViewRadius
        _cmd.ClearRenderTarget(true, true, new Color(maxViewRadius, maxViewRadius, 0, 0));

        _cmd.SetGlobalVector(PlayerPosID, player.position);
        _cmd.SetGlobalFloat(MaxRadiusID, maxViewRadius);

        // Pass 0 -> R
        foreach (var obstacle in obstacleRenderer)
        {
            if (obstacle == null) continue;
            _cmd.DrawRenderer(obstacle, _shadowMat, 0, 0);
        }

        // Pass 1 -> G
        foreach (var obstacle in obstacleRenderer)
        {
            if (obstacle == null) continue;
            _cmd.DrawRenderer(obstacle, _shadowMat, 0, 1);
        }

        Graphics.ExecuteCommandBuffer(_cmd);
    }

    void OnDestroy()
    {
        if (_shadowMap != null) _shadowMap.Release();
        if (_shadowMat != null) Destroy(_shadowMat);
        if (_cmd != null) _cmd.Release();
    }
}
