using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FogViewRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public int textureWidth = 1024;
        public int textureHeight = 1024;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        
        [Header("Blur Settings")]
        public bool enableBlur = true; // 是否启用模糊（关闭可节省性能）
        public Material blurMaterial;
        [Range(1, 4)]
        public int downsampleLevel = 2; // 降采样层级 (1=1/2, 2=1/4, 3=1/8, 4=1/16)
        [Range(1, 5)]
        public int blurIterations = 2; // 每层的模糊迭代次数
        [Range(0.5f, 4.0f)]
        public float blurSpread = 1.0f; // 模糊扩散范围
    }

    public Settings settings = new Settings();
    private FogViewPass fogViewPass;

    public override void Create()
    {
        fogViewPass = new FogViewPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
        {
            Debug.LogWarning("FogViewRenderFeature: Material is not assigned!");
            return;
        }

        // 如果启用了模糊但没有设置模糊材质，给出警告
        if (settings.enableBlur && settings.blurMaterial == null)
        {
            Debug.LogWarning("FogViewRenderFeature: Blur is enabled but Blur Material is not assigned! Blur will be skipped.");
        }

        fogViewPass.Setup(renderer);
        renderer.EnqueuePass(fogViewPass);
    }

    protected override void Dispose(bool disposing)
    {
        fogViewPass?.Dispose();
    }

    class FogViewPass : ScriptableRenderPass
    {
        private Settings settings;
        private RTHandle fogViewTextureHandle;
        private RTHandle[] downsampleHandles;
        private Material material;
        private readonly int fogViewTextureID = Shader.PropertyToID("_3DFogViewTexture");
        private readonly int blurOffsetID = Shader.PropertyToID("_BlurOffset");
        private const int MaxPyramidSize = 4;

        public FogViewPass(Settings settings)
        {
            this.settings = settings;
            this.material = settings.material;
            this.renderPassEvent = settings.renderPassEvent;

            // Configure input requirements - we need depth
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

    public void Setup(ScriptableRenderer renderer)
    {
        // 检查是否需要重新分配 RT（尺寸变化或模糊状态变化）
        bool needRealloc = fogViewTextureHandle == null || 
                          fogViewTextureHandle.rt.width != settings.textureWidth || 
                          fogViewTextureHandle.rt.height != settings.textureHeight;
        
        // 如果启用了模糊但没有分配 downsample handles，也需要重新分配
        bool needBlurHandles = settings.enableBlur && downsampleHandles == null;
        
        if (needRealloc || needBlurHandles)
        {
            // Release old handles if exists
            if (needRealloc)
            {
                fogViewTextureHandle?.Release();
            }
            
            if (downsampleHandles != null)
            {
                foreach (var handle in downsampleHandles)
                {
                    handle?.Release();
                }
                downsampleHandles = null;
            }

            // Create main RT descriptor
            if (needRealloc)
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                    settings.textureWidth, 
                    settings.textureHeight, 
                    RenderTextureFormat.R8, 
                    0);
                
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;

                // Allocate main RT
                fogViewTextureHandle = RTHandles.Alloc(descriptor, name: "_3DFogViewTexture");
                fogViewTextureHandle.rt.filterMode = FilterMode.Bilinear;
                fogViewTextureHandle.rt.wrapMode = TextureWrapMode.Clamp;
            }

            // 只在启用模糊时分配 downsample RTs（节省内存）
            if (settings.enableBlur)
            {
                downsampleHandles = new RTHandle[MaxPyramidSize];
                int width = settings.textureWidth;
                int height = settings.textureHeight;
                
                for (int i = 0; i < MaxPyramidSize; i++)
                {
                    width = Mathf.Max(1, width / 2);
                    height = Mathf.Max(1, height / 2);
                    
                    RenderTextureDescriptor mipDesc = new RenderTextureDescriptor(
                        width, height, RenderTextureFormat.R8, 0);
                    mipDesc.msaaSamples = 1;
                    mipDesc.useMipMap = false;
                    
                    downsampleHandles[i] = RTHandles.Alloc(mipDesc, name: $"_FogBlurMip{i}");
                    downsampleHandles[i].rt.filterMode = FilterMode.Bilinear;
                    downsampleHandles[i].rt.wrapMode = TextureWrapMode.Clamp;
                }
            }
        }
        
        // 如果关闭了模糊但还有旧的 downsample handles，释放它们
        if (!settings.enableBlur && downsampleHandles != null)
        {
            foreach (var handle in downsampleHandles)
            {
                handle?.Release();
            }
            downsampleHandles = null;
        }
    }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || fogViewTextureHandle == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("FogViewPass");

            // Step 1: 渲染原始 Fog View 到 RT
            ConfigureTarget(fogViewTextureHandle);
            ConfigureClear(ClearFlag.Color, Color.clear);
            Blitter.BlitCameraTexture(cmd, fogViewTextureHandle, fogViewTextureHandle, material, 0);

            // Step 2: 应用降采样+模糊（如果启用且配置了）
            if (settings.enableBlur && settings.blurMaterial != null && settings.downsampleLevel > 0 && downsampleHandles != null)
            {
                int iterations = Mathf.Min(settings.downsampleLevel, MaxPyramidSize);
                
                // === 降采样阶段 ===
                RTHandle lastDown = fogViewTextureHandle;
                for (int i = 0; i < iterations; i++)
                {
                    // 降采样到下一层
                    cmd.Blit(lastDown, downsampleHandles[i], settings.blurMaterial, 0); // Pass 0: Downsample
                    lastDown = downsampleHandles[i];
                    
                    // 在当前层应用 Kawase 模糊
                    for (int j = 0; j < settings.blurIterations; j++)
                    {
                        // 使用临时RT进行模糊（ping-pong）
                        RTHandle temp = (i + 1 < MaxPyramidSize) ? downsampleHandles[i + 1] : downsampleHandles[i];
                        
                        float offset = 0.5f + j * settings.blurSpread;
                        settings.blurMaterial.SetFloat(blurOffsetID, offset);
                        
                        if (i + 1 < MaxPyramidSize)
                        {
                            cmd.Blit(lastDown, temp, settings.blurMaterial, 2); // Pass 2: Kawase Blur
                            cmd.Blit(temp, lastDown);
                        }
                    }
                }
                
                // === 升采样阶段（带模糊） ===
                for (int i = iterations - 1; i >= 0; i--)
                {
                    RTHandle upTarget = (i == 0) ? fogViewTextureHandle : downsampleHandles[i - 1];
                    settings.blurMaterial.SetFloat(blurOffsetID, 1.0f);
                    
                    // 升采样并混合
                    cmd.Blit(lastDown, upTarget, settings.blurMaterial, 1); // Pass 1: Upsample
                    lastDown = upTarget;
                }
            }

            // Step 3: 设置为全局纹理
            cmd.SetGlobalTexture(fogViewTextureID, fogViewTextureHandle);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            fogViewTextureHandle?.Release();
            
            if (downsampleHandles != null)
            {
                foreach (var handle in downsampleHandles)
                {
                    handle?.Release();
                }
            }
        }
    }
}
