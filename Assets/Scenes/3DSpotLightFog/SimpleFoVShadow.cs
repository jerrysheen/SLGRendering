using UnityEngine;

[ExecuteAlways]
public class SimpleFoVShadow : MonoBehaviour
{
    [Header("设置")]
    [Range(1, 179)] public float fovAngle = 170f; // 179度边缘拉伸会极大约束，建议160-170
    public float viewDistance = 20f;
    public int shadowMapResolution = 1024;
    public LayerMask obstacleLayer; // 只渲染墙壁层
    
    [Header("Shader引用")]
    public Shader depthCasterShader; // 拖入上面的 FoV_DepthCaster

    // 内部变量
    private Camera shadowCam;
    private RenderTexture shadowRT;
    private GameObject camObj;

    void OnEnable()
    {
        if (depthCasterShader == null) depthCasterShader = Shader.Find("Hidden/FoV_DepthCaster");
        SetupCamera();
    }

    void OnDisable()
    {
        if (shadowRT) shadowRT.Release();
        if (camObj) DestroyImmediate(camObj);
    }

    void LateUpdate()
    {
        if (shadowCam == null) SetupCamera();

        // 1. 同步参数
        shadowCam.fieldOfView = fovAngle;
        shadowCam.farClipPlane = viewDistance;
        shadowCam.cullingMask = obstacleLayer;
    
        // 2. 传递光源参数给 Caster Shader
        Shader.SetGlobalVector("_FoVLightPos", transform.position);
        Shader.SetGlobalFloat("_FoVRange", viewDistance);

        // 3. 计算 VP 矩阵
        Matrix4x4 viewMat = shadowCam.worldToCameraMatrix;
        Matrix4x4 projMat = GL.GetGPUProjectionMatrix(shadowCam.projectionMatrix, false);
        Matrix4x4 vpMat = projMat * viewMat;
    
        Shader.SetGlobalMatrix("_FoVShadowVP", vpMat);
        Shader.SetGlobalTexture("_FoVShadowMap", shadowRT);
        Shader.SetGlobalVector("PlayerPos", new Vector4(transform.position.x, 0.0f, transform.position.z, 0));
        // ---------------------------------------------------------
        // 【关键修复】手动清理 RenderTexture
        // ---------------------------------------------------------
        // 保存当前正在渲染的 RT（防止干扰主渲染流程）
        RenderTexture prevRT = RenderTexture.active;
    
        // 1. 指定目标为我们的 ShadowMap
        Graphics.SetRenderTarget(shadowRT);
    
        // 2. 执行清理
        // clearDepth: true
        // clearColor: true
        // color: Color.white (RGBA = 1,1,1,1) -> 代表深度为 "最远/无限远"
        GL.Clear(true, true, Color.white);
    
        // 3. 恢复之前的 RT
        RenderTexture.active = prevRT;
        // ---------------------------------------------------------

        // 4. 渲染阴影图
        shadowCam.RenderWithShader(depthCasterShader, "RenderType");
    }

    void SetupCamera()
    {
        if (camObj != null) return;

        // 创建一个子物体作为相机
        camObj = new GameObject("Hidden_ShadowCam");
        camObj.transform.SetParent(transform, false);
        camObj.transform.localPosition = Vector3.zero;
        camObj.transform.localRotation = Quaternion.LookRotation(Vector3.down); // 垂直向下看

        shadowCam = camObj.AddComponent<Camera>();
        shadowCam.enabled = false; // 我们手动 Render，不需要它自动跑
        shadowCam.aspect = 1.0f;   // 阴影图必须是正方形
        shadowCam.backgroundColor = Color.white; // 默认距离是 1 (无限远)
        shadowCam.clearFlags = CameraClearFlags.SolidColor;
        
        // 创建 RT
        shadowRT = new RenderTexture(shadowMapResolution, shadowMapResolution, 16, RenderTextureFormat.RHalf);
        shadowRT.name = "FoV_ShadowMap";
        shadowCam.targetTexture = shadowRT;
    }
    
    // 画个圈圈方便看范围
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
}