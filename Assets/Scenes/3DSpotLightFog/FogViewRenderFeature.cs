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
        private Material material;
        private readonly int fogViewTextureID = Shader.PropertyToID("_3DFogViewTexture");

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
            // Allocate RT if not already allocated or if size changed
            if (fogViewTextureHandle == null || 
                fogViewTextureHandle.rt.width != settings.textureWidth || 
                fogViewTextureHandle.rt.height != settings.textureHeight)
            {
                // Release old handle if exists
                fogViewTextureHandle?.Release();

                // Create RT descriptor
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
                    settings.textureWidth, 
                    settings.textureHeight, 
                    RenderTextureFormat.R8, 
                    0);
                
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;

                // Allocate RT Handle
                fogViewTextureHandle = RTHandles.Alloc(descriptor, name: "_3DFogViewTexture");
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || fogViewTextureHandle == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("FogViewPass");

            // Set render target to our fog view texture
            ConfigureTarget(fogViewTextureHandle);
            ConfigureClear(ClearFlag.Color, Color.clear);

            // Execute fullscreen blit with the material
            Blitter.BlitCameraTexture(cmd, fogViewTextureHandle, fogViewTextureHandle, material, 0);

            // Set the texture as global texture so it can be accessed by other shaders
            cmd.SetGlobalTexture(fogViewTextureID, fogViewTextureHandle);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            fogViewTextureHandle?.Release();
        }
    }
}
