using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 将此脚本添加到 URP Renderer Data 的 Renderer Features 列表中
public class PolarFogRenderFeature : ScriptableRendererFeature
{
	[System.Serializable]
	public class Settings
	{
		public Material material; // 把上面的 PolarFogDebug 材质拖这就行
		public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
	}

	public Settings settings = new Settings();
	PolarFogPass m_ScriptablePass;

	public override void Create()
	{
		m_ScriptablePass = new PolarFogPass(settings.material);
		m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (settings.material != null)
		{
			renderer.EnqueuePass(m_ScriptablePass);
		}
	}

	class PolarFogPass : ScriptableRenderPass
	{
		private Material m_Material;
		private RenderTargetIdentifier m_CameraColorTarget;

		public PolarFogPass(Material material)
		{
			this.m_Material = material;
		}

		public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
		{
			m_CameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get("Polar Fog Vis");

			// 获取相机的图像，Blit 到屏幕，应用材质
			// 注意：URP 这里的 Blit 稍微有点坑，最简单的全屏覆盖写法如下：
			// 我们不读 MainTex，直接覆盖屏幕，利用 Blend SrcAlpha OneMinusSrcAlpha 混合
            
			// 如果只是测试黑白图，直接画一个全屏三角形覆盖即可
			cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}