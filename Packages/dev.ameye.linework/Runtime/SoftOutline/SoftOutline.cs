using System;
using Linework.Common.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;
using Resolution = Linework.Common.Utils.Resolution;

namespace Linework.SoftOutline
{
    [ExcludeFromPreset]
    [DisallowMultipleRendererFeature("Soft Outline")]
#if UNITY_6000_0_OR_NEWER
    [SupportedOnRenderer(typeof(UniversalRendererData))]
#endif
    [Tooltip("Soft Outline renders outlines by generating a silhouette of an object and applying a dilation/blur effect, resulting in smooth, soft-edged contours around objects.")]
    [HelpURL("https://linework.ameye.dev/soft-outline")]
    public class SoftOutline : ScriptableRendererFeature
    {
        private class SoftOutlinePass : ScriptableRenderPass
        {
            private SoftOutlineSettings settings;
            private Material mask, silhouetteBase, silhouetteInstancedBase, blur, composite;
            private readonly ProfilingSampler maskSampler, silhouetteSampler, blurSampler, outlineSampler;
            
            public SoftOutlinePass()
            {
                profilingSampler = new ProfilingSampler(nameof(SoftOutlinePass));
                maskSampler = new ProfilingSampler(ShaderPassName.Mask);
                silhouetteSampler = new ProfilingSampler(ShaderPassName.Silhouette);
                blurSampler = new ProfilingSampler(ShaderPassName.Blur);
                outlineSampler = new ProfilingSampler(ShaderPassName.Outline);
            }
            
            public bool Setup(ref SoftOutlineSettings softOutlineSettings, ref Material maskMaterial, ref Material silhouetteMaterial, ref Material silhouetteInstancedMaterial, ref Material blurMaterial, ref Material compositeMaterial)
            {
                settings = softOutlineSettings;
                mask = maskMaterial;
                silhouetteBase = silhouetteMaterial;
                silhouetteInstancedBase = silhouetteInstancedMaterial;
                blur = blurMaterial;
                composite = compositeMaterial;
                renderPassEvent = (RenderPassEvent) softOutlineSettings.InjectionPoint;

                foreach (var outline in settings.Outlines)
                {
                    if (outline.material == null || outline.materialInstanced == null)
                    {
                        outline.AssignMaterials(silhouetteBase, silhouetteInstancedBase);
                    }
                }

                foreach (var outline in settings.Outlines)
                {
                    if (!outline.IsActive())
                    {
                        continue;
                    }
                    
                    var silhouette = outline.gpuInstancing ? outline.materialInstanced : outline.material;
                    
                    silhouette.SetColor(CommonShaderPropertyId.OutlineColor, settings.type == OutlineType.Hard ? Color.white : outline.color);
                    if(outline.occlusion == SoftOutlineOcclusion.AsMask) silhouette.SetColor(CommonShaderPropertyId.OutlineColor, Color.clear);

                    if (outline.alphaCutout) silhouette.EnableKeyword(ShaderFeature.AlphaCutout);
                    else silhouette.DisableKeyword(ShaderFeature.AlphaCutout);
                    silhouette.SetTexture(CommonShaderPropertyId.AlphaCutoutTexture, outline.alphaCutoutTexture);
                    silhouette.SetFloat(CommonShaderPropertyId.AlphaCutoutThreshold, outline.alphaCutoutThreshold);
                    silhouette.SetVector(CommonShaderPropertyId.AlphaCutoutUVTransform, outline.alphaCutoutUVTransform);

                    switch (outline.cullingMode)
                    {
                        case CullingMode.Off:
                            silhouette.SetFloat(CommonShaderPropertyId.CullMode, (float) CullMode.Off);
                            break;
                        case CullingMode.Back:
                            silhouette.SetFloat(CommonShaderPropertyId.CullMode, (float) CullMode.Back);
                            break;
                    }
                    
                    switch (outline.occlusion)
                    {
                        case SoftOutlineOcclusion.Always:
                            silhouette.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Always);
                            break;
                        case SoftOutlineOcclusion.WhenOccluded:
                            silhouette.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Greater);
                            break;
                        case SoftOutlineOcclusion.WhenNotOccluded:
                            silhouette.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.LessEqual);
                            break;
                        case SoftOutlineOcclusion.AsMask:
                            silhouette.SetFloat(CommonShaderPropertyId.ZTest, (float) CompareFunction.Always);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                // Set blur material properties.
                if (settings.scaleWithResolution) blur.EnableKeyword(ShaderFeature.ScaleWithResolution);
                else blur.DisableKeyword(ShaderFeature.ScaleWithResolution);
                switch (settings.referenceResolution)
                {
                    case Resolution._480:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, 480.0f);
                        break;
                    case Resolution._720:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, 720.0f);
                        break;
                    case Resolution._1080:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, 1080.0f);
                        break;
                    case Resolution.Custom:
                        blur.SetFloat(CommonShaderPropertyId.ReferenceResolution, settings.customResolution);
                        break;
                }
                
                if (settings.dilationMethod is DilationMethod.Box or DilationMethod.Gaussian or DilationMethod.Dilate)
                {
                    blur.SetInt(ShaderPropertyId.KernelSize, settings.kernelSize);
                    blur.SetInt(ShaderPropertyId.Samples, settings.kernelSize * 2 + 1);
                }
                if (settings.dilationMethod is DilationMethod.Gaussian)
                {
                    blur.SetFloat(ShaderPropertyId.KernelSpread, settings.blurSpread);
                }

                blur.SetFloat(ShaderPropertyId.OutlineHardness, settings.hardness);

                // Set composite material properties.
                var (srcBlend, dstBlend) = RenderUtils.GetSrcDstBlend(settings.blendMode);
                composite.SetInt(CommonShaderPropertyId.BlendModeSource, srcBlend);
                composite.SetInt(CommonShaderPropertyId.BlendModeDestination, dstBlend);
                composite.SetColor(CommonShaderPropertyId.OutlineColor, settings.sharedColor);
                composite.SetFloat(ShaderPropertyId.OutlineHardness, settings.hardness);
                composite.SetFloat(ShaderPropertyId.OutlineIntensity, settings.type == OutlineType.Hard ? 1.0f : settings.intensity);
                composite.SetFloat(ShaderPropertyId.OutlineGap, settings.dilationMethod is DilationMethod.Box or DilationMethod.Dilate or DilationMethod.Gaussian ? settings.gap : 0.0f);

                if (settings.type == OutlineType.Hard) composite.EnableKeyword(ShaderFeature.HardOutline);
                else composite.DisableKeyword(ShaderFeature.HardOutline);

                // Return true if any of the outlines should be rendered (are active). Otherwise return false.
                // Same as `return settings.Outlines.Any(ShouldRenderOutline);`
                foreach (var outline in settings.Outlines)
                {
                    if (ShouldRenderOutline(outline))
                    {
                        return true;
                    }
                }
                return false;
            }
            
            private static bool ShouldRenderOutline(Outline outline)
            {
                return outline.IsActive() && outline.occlusion != SoftOutlineOcclusion.AsMask;
            }

            private static bool ShouldRenderStencilMask(Outline outline)
            {
                return outline.IsActive() && outline.occlusion == SoftOutlineOcclusion.WhenOccluded;
            }

#if UNITY_6000_0_OR_NEWER
            private class PassData
            {
                internal readonly List<RendererListHandle> MaskRendererListHandles = new();
                internal readonly List<(RendererListHandle handle, bool vertexAnimated)> SilhouetteRendererListHandles = new();
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                // Ensure that the render pass doesn't blit from the back buffer.
                if (resourceData.isActiveTargetBackBuffer) return;

                CreateRenderGraphTextures(renderGraph, resourceData, out var silhouetteHandle, out var blurHandle);
                if (!silhouetteHandle.IsValid() || !blurHandle.IsValid()) return;

                // 1. Mask.
                // -> Render a mask to the stencil buffer.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Mask, out var passData))
                {
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                    InitMaskRendererLists(renderGraph, frameData, ref passData);
                    foreach (var rendererListHandle in passData.MaskRendererListHandles)
                    {
                        builder.UseRendererList(rendererListHandle);
                    }
                  
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        foreach (var handle in data.MaskRendererListHandles)
                        {
                            context.cmd.DrawRendererList(handle);
                        }
                    });
                }
                
                // 2. Silhouette.
                // -> Render a silhouette.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Silhouette, out var passData))
                {
                    builder.SetRenderAttachment(silhouetteHandle, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                   
                    builder.SetGlobalTextureAfterPass(silhouetteHandle, ShaderPropertyId.SilhouetteBuffer);
                    
                    InitSilhouetteRendererLists(renderGraph, frameData, ref passData);
                    foreach (var rendererListHandle in passData.SilhouetteRendererListHandles)
                    {
                        builder.UseRendererList(rendererListHandle.handle);
                    }
                    
                    builder.AllowGlobalStateModification(true); // vertex animation
                    builder.AllowPassCulling(false);
                
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        foreach (var handle in data.SilhouetteRendererListHandles)
                        {
                            if (handle.vertexAnimated)
                            {
                                context.cmd.EnableKeyword(Keyword.OutlineColor);
                            }
                            
                            context.cmd.DrawRendererList(handle.handle);
                            
                            if (handle.vertexAnimated)
                            {
                                context.cmd.DisableKeyword(Keyword.OutlineColor);
                            }
                        }
                    });
                }
                
                // 3. Blur.
                // -> Blur the silhouette.
                using (var builder = renderGraph.AddUnsafePass<PassData>(ShaderPassName.Blur, out _))
                {
                    builder.UseTexture(silhouetteHandle);
                    builder.UseTexture(blurHandle, AccessFlags.Write);
                    
                    builder.AllowPassCulling(false);
                
                    builder.SetRenderFunc((PassData _, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                
                        switch (settings.dilationMethod)
                        {
                            case DilationMethod.Box or DilationMethod.Gaussian or DilationMethod.Dilate:
                                Blitter.BlitCameraTexture(cmd, silhouetteHandle, blurHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                    ShaderPass.VerticalBlur);
                                Blitter.BlitCameraTexture(cmd, blurHandle, silhouetteHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                    ShaderPass.HorizontalBlur);
                                break;
                            case DilationMethod.Kawase:
                                for (var i = 1; i < settings.blurPasses; i++)
                                {
                                    blur.SetFloat(ShaderPropertyId.Offset, 0.5f + i);
                                    Blitter.BlitCameraTexture(cmd, silhouetteHandle, blurHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blur,
                                        ShaderPass.Blur);
                                    (silhouetteHandle, blurHandle) = (blurHandle, silhouetteHandle);
                                }
                                break;
                        }
                    });
                }
                
                // 4. Outline.
                // -> Render an outline.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Outline, out _))
                {
                    var source = settings.dilationMethod switch
                    {
                        DilationMethod.Box or DilationMethod.Gaussian => silhouetteHandle,
                        DilationMethod.Kawase => blurHandle,
                        _ => silhouetteHandle
                    };
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                
                    builder.AllowPassCulling(false);
                
                    builder.SetRenderFunc((PassData _, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, source, Vector2.one, composite, ShaderPass.Outline);
                    });
                }
            }

            private void InitMaskRendererLists(RenderGraph renderGraph, ContextContainer frameData, ref PassData passData)
            {
                passData.MaskRendererListHandles.Clear();
                
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                
                var sortingCriteria = cameraData.defaultOpaqueSortFlags;
                
                var i = 0;
                foreach (var outline in settings.Outlines)
                {
                    if (!ShouldRenderStencilMask(outline))
                    {
                        i++;
                        continue;
                    }
                    
                    var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
                    drawingSettings.overrideMaterial = mask;
                    drawingSettings.overrideShaderPassIndex = ShaderPass.Mask;

                    var renderQueueRange = outline.renderQueue switch
                    {
                        OutlineRenderQueue.Opaque => RenderQueueRange.opaque,
                        OutlineRenderQueue.Transparent => RenderQueueRange.transparent,
                        OutlineRenderQueue.OpaqueAndTransparent => RenderQueueRange.all,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    
                    var filteringSettings = new FilteringSettings(renderQueueRange, outline.layerMask, outline.RenderingLayer);
                    var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                    var blendState = BlendState.defaultValue;
                    blendState.blendState0 = new RenderTargetBlendState(0);
                    renderStateBlock.blendState = blendState;
                    
                    var stencilState = StencilState.defaultValue;
                    stencilState.enabled = true;
                    stencilState.SetCompareFunction(CompareFunction.Always);
                    stencilState.SetPassOperation(StencilOp.Replace);
                    stencilState.SetFailOperation(StencilOp.Keep);
                    stencilState.SetZFailOperation(StencilOp.Keep);
                    stencilState.writeMask = (byte) (1 << i);
                    renderStateBlock.mask |= RenderStateMask.Stencil;
                    renderStateBlock.stencilReference = 1 << i;
                    renderStateBlock.stencilState = stencilState;
                    
                    var handle = new RendererListHandle();
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref renderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref handle);
                    passData.MaskRendererListHandles.Add(handle);
                }
            }

            private void InitSilhouetteRendererLists(RenderGraph renderGraph, ContextContainer frameData, ref PassData passData)
            {
                passData.SilhouetteRendererListHandles.Clear();

                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                var sortingCriteria = cameraData.defaultOpaqueSortFlags;

                var i = 0;
                foreach (var outline in settings.Outlines)
                {
                    if (!outline.IsActive())
                    {
                        i++;
                        continue;
                    }

                    var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
                    if (!outline.vertexAnimation)
                    {
                        drawingSettings.overrideMaterial = outline.gpuInstancing ? outline.materialInstanced : outline.material;
                        drawingSettings.overrideMaterialPassIndex = ShaderPass.Silhouette;
                        drawingSettings.enableInstancing = outline.gpuInstancing;;
                    }
                    
                    var renderQueueRange = outline.renderQueue switch
                    {
                        OutlineRenderQueue.Opaque => RenderQueueRange.opaque,
                        OutlineRenderQueue.Transparent => RenderQueueRange.transparent,
                        OutlineRenderQueue.OpaqueAndTransparent => RenderQueueRange.all,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                
                    var filteringSettings = new FilteringSettings(renderQueueRange, outline.layerMask, outline.RenderingLayer);
                    
                    var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                    
                    var stencilState = StencilState.defaultValue;
                    stencilState.enabled = true;
                    stencilState.SetCompareFunction(outline.occlusion == SoftOutlineOcclusion.WhenOccluded ? CompareFunction.NotEqual : CompareFunction.Always);
                    stencilState.SetPassOperation(StencilOp.Replace);
                    stencilState.SetFailOperation(StencilOp.Keep);
                    stencilState.SetZFailOperation(outline.closedLoop ? StencilOp.Keep : StencilOp.Replace);
                    stencilState.readMask = (byte) (1 << i);
                    stencilState.writeMask = (byte) (1 << i);
                    renderStateBlock.mask |= RenderStateMask.Stencil;
                    renderStateBlock.stencilReference = 1 << i;
                    renderStateBlock.stencilState = stencilState;
                    
                    if (outline.vertexAnimation)
                    {
                        var depthState = DepthState.defaultValue;
                        switch (outline.occlusion)
                        {
                            case SoftOutlineOcclusion.Always:
                                depthState.compareFunction = CompareFunction.Always;
                                break;
                            case SoftOutlineOcclusion.WhenOccluded:
                                depthState.compareFunction = CompareFunction.Greater;
                                break;
                            case SoftOutlineOcclusion.WhenNotOccluded:
                                depthState.compareFunction = CompareFunction.LessEqual;
                                break;
                            case SoftOutlineOcclusion.AsMask:
                                depthState.compareFunction = CompareFunction.Always;
                                break;
                        }
                        renderStateBlock.mask |= RenderStateMask.Depth;
                        renderStateBlock.depthState = depthState;
                    }

                    var handle = new RendererListHandle();
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref renderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref handle);
                    passData.SilhouetteRendererListHandles.Add((handle, outline.vertexAnimation));
                    
                    i++;
                }
            }
            
            private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, out TextureHandle silhouetteHandle,
                out TextureHandle blurHandle)
            {
                var cameraDescriptor = resourceData.activeColorTexture.GetDescriptor(renderGraph);
                
                const float renderTextureScale = 1.0f; 
                var width = (int)(cameraDescriptor.width * renderTextureScale);
                var height = (int)(cameraDescriptor.height * renderTextureScale);
                
                var descriptor = new TextureDesc(width, height)
                {
                    dimension = TextureDimension.Tex2D,
                    msaaSamples = cameraDescriptor.msaaSamples,
                    useMipMap = false,
                    autoGenerateMips = false,
                    colorFormat = settings.dilationMethod == DilationMethod.Dilate ? GraphicsFormat.R8G8B8A8_UNorm :
                        settings.type == OutlineType.Hard ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm, // TODO: Changed to format somewhere in Unity 6 cycle?
                    depthBufferBits = (int) DepthBits.None
                };

                descriptor.name = Buffer.Silhouette;
                silhouetteHandle = renderGraph.CreateTexture(descriptor);
                descriptor.name = Buffer.Blur;
                blurHandle = renderGraph.CreateTexture(descriptor);
            }
#endif
            private RTHandle cameraDepthRTHandle, silhouetteRTHandle, blurRTHandle;
            private RTHandle[] handles;
            
            #pragma warning disable 618, 672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if(handles is not {Length: 2})
                {
                    handles = new RTHandle[2];
                }
                handles[0] = silhouetteRTHandle;
                handles[1] = blurRTHandle;
                
                ConfigureTarget(handles, cameraDepthRTHandle);
                ConfigureClear(ClearFlag.Color, Color.clear);
            }
            
            public void CreateHandles(RenderingData renderingData)
            {
                const float renderTextureScale = 1.0f; 
                var width = (int)(renderingData.cameraData.cameraTargetDescriptor.width * renderTextureScale);
                var height = (int)(renderingData.cameraData.cameraTargetDescriptor.height * renderTextureScale);
                
                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    dimension = TextureDimension.Tex2D,
                    msaaSamples = renderingData.cameraData.cameraTargetDescriptor.msaaSamples,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false,
                    graphicsFormat = settings.dilationMethod == DilationMethod.Dilate
                        ? GraphicsFormat.R8G8B8A8_UNorm
                        : settings.type == OutlineType.Hard
                            ? GraphicsFormat.R8_UNorm
                            : GraphicsFormat.R8G8B8A8_UNorm,
                    depthBufferBits = (int) DepthBits.None,
                    colorFormat = RenderTextureFormat.Default
                };
                
                RenderingUtils.ReAllocateIfNeeded(ref silhouetteRTHandle, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Silhouette);
                RenderingUtils.ReAllocateIfNeeded(ref blurRTHandle, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Blur);
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 1. Mask.
                // -> Render a mask to the stencil buffer.
                var maskCmd = CommandBufferPool.Get();

                using (new ProfilingScope(maskCmd, maskSampler))
                {
                    context.ExecuteCommandBuffer(maskCmd);
                    maskCmd.Clear();
                    
                    var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    
                    var maskIndex = 0;
                    foreach (var outline in settings.Outlines)
                    {
                        if (!ShouldRenderStencilMask(outline))
                        {
                            maskIndex++;
                            continue;
                        }
                        
                        var renderQueueRange = outline.renderQueue switch
                        {
                            OutlineRenderQueue.Opaque => RenderQueueRange.opaque,
                            OutlineRenderQueue.Transparent => RenderQueueRange.transparent,
                            OutlineRenderQueue.OpaqueAndTransparent => RenderQueueRange.all,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, ref renderingData, sortingCriteria);
                        drawingSettings.overrideMaterial = mask;
                        drawingSettings.overrideShaderPassIndex = ShaderPass.Mask;

                        var filteringSettings = new FilteringSettings(renderQueueRange, outline.layerMask, outline.RenderingLayer);
                        var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                        var blendState = BlendState.defaultValue;
                        blendState.blendState0 = new RenderTargetBlendState(0);
                        renderStateBlock.blendState = blendState;

                        var stencilState = StencilState.defaultValue;
                        stencilState.enabled = true;
                        stencilState.SetCompareFunction(CompareFunction.Always);
                        stencilState.SetPassOperation(StencilOp.Replace);
                        stencilState.SetFailOperation(StencilOp.Keep);
                        stencilState.SetZFailOperation(StencilOp.Keep);
                        stencilState.writeMask = (byte) (1 << maskIndex);
                        renderStateBlock.mask |= RenderStateMask.Stencil;
                        renderStateBlock.stencilReference = 1 << maskIndex;
                        renderStateBlock.stencilState = stencilState;

                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                        maskIndex++;
                    }
                }
                
                context.ExecuteCommandBuffer(maskCmd);
                CommandBufferPool.Release(maskCmd);

                // 2. Silhouette.
                // -> Render a silhouette.
                var silhouetteCmd = CommandBufferPool.Get();
                
                using (new ProfilingScope(silhouetteCmd, silhouetteSampler))
                { 
                    CoreUtils.SetRenderTarget(silhouetteCmd, silhouetteRTHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    
                    context.ExecuteCommandBuffer(silhouetteCmd);
                    silhouetteCmd.Clear();
                    
                    var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    var i = 0;
                    foreach (var outline in settings.Outlines)
                    {
                        if (!outline.IsActive())
                        {
                            i++;
                            continue;
                        }
                        
                        var renderQueueRange = outline.renderQueue switch
                        {
                            OutlineRenderQueue.Opaque => RenderQueueRange.opaque,
                            OutlineRenderQueue.Transparent => RenderQueueRange.transparent,
                            OutlineRenderQueue.OpaqueAndTransparent => RenderQueueRange.all,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, ref renderingData, sortingCriteria);
                        if (!outline.vertexAnimation)
                        {
                            drawingSettings.overrideMaterial = outline.gpuInstancing ? outline.materialInstanced : outline.material;
                            drawingSettings.overrideShaderPassIndex = ShaderPass.Silhouette;
                            drawingSettings.enableInstancing = outline.gpuInstancing;
                        }
                       
                        var filteringSettings = new FilteringSettings(renderQueueRange, outline.layerMask, outline.RenderingLayer);
                        
                        var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                        
                        var stencilState = StencilState.defaultValue;
                        stencilState.enabled = true;
                        stencilState.SetCompareFunction(outline.occlusion == SoftOutlineOcclusion.WhenOccluded ? CompareFunction.NotEqual : CompareFunction.Always);
                        stencilState.SetPassOperation(StencilOp.Replace);
                        stencilState.SetFailOperation(StencilOp.Keep);
                        stencilState.SetZFailOperation(outline.closedLoop ? StencilOp.Keep : StencilOp.Replace);
                        stencilState.writeMask = (byte) (1 << i);
                        renderStateBlock.mask |= RenderStateMask.Stencil;
                        renderStateBlock.stencilReference = 1 << i;
                        renderStateBlock.stencilState = stencilState;
                        
                        if (outline.vertexAnimation)
                        {
                            var depthState = DepthState.defaultValue;
                            switch (outline.occlusion)
                            {
                                case SoftOutlineOcclusion.Always:
                                    depthState.compareFunction = CompareFunction.Always;
                                    break;
                                case SoftOutlineOcclusion.WhenOccluded:
                                    depthState.compareFunction = CompareFunction.Greater;
                                    break;
                                case SoftOutlineOcclusion.WhenNotOccluded:
                                    depthState.compareFunction = CompareFunction.LessEqual;
                                    break;
                                case SoftOutlineOcclusion.AsMask:
                                    depthState.compareFunction = CompareFunction.Always;
                                    break;
                            }
                            renderStateBlock.mask |= RenderStateMask.Depth;
                            renderStateBlock.depthState = depthState;
                        }
                        
                        if (outline.vertexAnimation) silhouetteCmd.EnableKeyword(Keyword.OutlineColor);
                        context.ExecuteCommandBuffer(silhouetteCmd);
                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
                        if (outline.vertexAnimation) silhouetteCmd.DisableKeyword(Keyword.OutlineColor);
                        context.ExecuteCommandBuffer(silhouetteCmd);
                        
                        i++;
                    }
                }
                
                silhouetteCmd.SetGlobalTexture(ShaderPropertyId.SilhouetteBuffer, silhouetteRTHandle.nameID);
                context.ExecuteCommandBuffer(silhouetteCmd);
                CommandBufferPool.Release(silhouetteCmd);
                
                // 3. Blur.
                // -> Blur the silhouette.
                var blurCmd = CommandBufferPool.Get();
                
                using (new ProfilingScope(blurCmd, blurSampler))
                {
                    context.ExecuteCommandBuffer(blurCmd);
                    blurCmd.Clear();
                
                    switch (settings.dilationMethod)
                    {
                        case DilationMethod.Box or DilationMethod.Gaussian or DilationMethod.Dilate:
                            Blitter.BlitCameraTexture(blurCmd, silhouetteRTHandle, blurRTHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                ShaderPass.VerticalBlur);
                            Blitter.BlitCameraTexture(blurCmd, blurRTHandle, silhouetteRTHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, blur,
                                ShaderPass.HorizontalBlur);
                            break;
                        case DilationMethod.Kawase:
                            for (var i = 1; i < settings.blurPasses; i++)
                            {
                                blur.SetFloat(ShaderPropertyId.Offset, 0.5f + i);
                                Blitter.BlitCameraTexture(blurCmd, silhouetteRTHandle, blurRTHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blur,
                                    ShaderPass.Blur);
                                (silhouetteRTHandle, blurRTHandle) = (blurRTHandle, silhouetteRTHandle);
                            }
                            break;
                    }
                }
                
                context.ExecuteCommandBuffer(blurCmd);
                CommandBufferPool.Release(blurCmd);
                
                // 4. Outline.
                // -> Render an outline.
                var outlineCmd = CommandBufferPool.Get();
                
                using (new ProfilingScope(outlineCmd, outlineSampler))
                {
                    context.ExecuteCommandBuffer(outlineCmd);
                    outlineCmd.Clear();
                    
                    var source = settings.dilationMethod switch
                    {
                        DilationMethod.Box or DilationMethod.Gaussian => silhouetteRTHandle,
                        DilationMethod.Kawase => blurRTHandle,
                        _ => silhouetteRTHandle
                    };
                
                    CoreUtils.SetRenderTarget(outlineCmd, renderingData.cameraData.renderer.cameraColorTargetHandle, cameraDepthRTHandle); // if using cameraColorRTHandle this does not render in scene view when rendering after post processing with post processing enabled
                    Blitter.BlitTexture(outlineCmd, source, Vector2.one, composite, ShaderPass.Outline);
                }
                
                context.ExecuteCommandBuffer(outlineCmd);
                CommandBufferPool.Release(outlineCmd);
            }
            #pragma warning restore 618, 672
            
            public void SetTarget(RTHandle depth)
            {
                cameraDepthRTHandle = depth;
            }
            
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (cmd == null)
                {
                    throw new ArgumentNullException(nameof(cmd));
                }

                cameraDepthRTHandle = null;
            }
            
            public void Dispose()
            {
                settings = null; // de-reference settings to allow them to be freed from memory
                
                silhouetteRTHandle?.Release();
                blurRTHandle?.Release();
            }
        }

        [SerializeField] private SoftOutlineSettings settings;
        [SerializeField] private ShaderResources shaders;
        private Material maskMaterial, silhouetteMaterial, silhouetteInstancedMaterial, blurMaterial, outlineMaterial;
        private SoftOutlinePass softOutlinePass;

        /// <summary>
        /// Called
        /// - When the Scriptable Renderer Feature loads the first time.
        /// - When you enable or disable the Scriptable Renderer Feature.
        /// - When you change a property in the Inspector window of the Renderer Feature.
        /// </summary>
        public override void Create()
        {
            if (settings == null) return;
            settings.OnSettingsChanged = null;
            settings.OnSettingsChanged += Create;

            shaders = new ShaderResources().Load();
            softOutlinePass ??= new SoftOutlinePass();
        }

        /// <summary>
        /// Called
        /// - Every frame, once for each camera.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || softOutlinePass == null) return;

            // Don't render for some views.
            if (renderingData.cameraData.cameraType == CameraType.Preview
                || renderingData.cameraData.cameraType == CameraType.Reflection
                || renderingData.cameraData.cameraType == CameraType.SceneView && !settings.ShowInSceneView
#if UNITY_6000_0_OR_NEWER
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
#else
                )
#endif
                return;

            if (!CreateMaterials())
            {
                Debug.LogWarning("Not all required materials could be created. Soft Outline will not render.");
                return;
            }

            var render = softOutlinePass.Setup(ref settings, ref maskMaterial, ref silhouetteMaterial, ref silhouetteInstancedMaterial, ref blurMaterial, ref outlineMaterial);
            if (render) renderer.EnqueuePass(softOutlinePass);
        }
        
        #pragma warning disable 618, 672
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (settings == null || softOutlinePass == null || renderingData.cameraData.cameraType == CameraType.SceneView && !settings.ShowInSceneView) return;
            if (renderingData.cameraData.cameraType is CameraType.Preview or CameraType.Reflection) return;
            
            softOutlinePass.CreateHandles(renderingData);
            softOutlinePass.SetTarget(renderer.cameraDepthTargetHandle);
        }
        #pragma warning restore 618, 672

        /// <summary>
        /// Clean up resources allocated to the Scriptable Renderer Feature such as materials.
        /// </summary>
        override protected void Dispose(bool disposing)
        {
            softOutlinePass?.Dispose();
            softOutlinePass = null;
            DestroyMaterials();
        }
        
        private void OnDestroy()
        {
            settings = null; // de-reference settings to allow them to be freed from memory
            softOutlinePass?.Dispose();
        }

        private void DestroyMaterials()
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(silhouetteMaterial);
            CoreUtils.Destroy(silhouetteInstancedMaterial);
            CoreUtils.Destroy(blurMaterial);
            CoreUtils.Destroy(outlineMaterial);
        }

        private bool CreateMaterials()
        {
            if (maskMaterial == null)
            {
                maskMaterial = CoreUtils.CreateEngineMaterial(shaders.mask);
            }

            if (silhouetteMaterial == null)
            {
                silhouetteMaterial = CoreUtils.CreateEngineMaterial(shaders.silhouette);
            }
            
            if (silhouetteInstancedMaterial == null)
            {
                silhouetteInstancedMaterial = CoreUtils.CreateEngineMaterial(shaders.silhouetteInstanced);
            }

            if (blurMaterial != null) CoreUtils.Destroy(blurMaterial);
            blurMaterial = settings.dilationMethod switch
            {
                DilationMethod.Box => CoreUtils.CreateEngineMaterial(shaders.boxBlur),
                DilationMethod.Gaussian => CoreUtils.CreateEngineMaterial(shaders.gaussianBlur),
                DilationMethod.Kawase => CoreUtils.CreateEngineMaterial(shaders.kawaseBlur),
                DilationMethod.Dilate => CoreUtils.CreateEngineMaterial(shaders.dilate),
                _ => blurMaterial
            };

            if (outlineMaterial == null)
            {
                outlineMaterial = CoreUtils.CreateEngineMaterial(shaders.outline);
            }

            return maskMaterial != null && silhouetteMaterial != null && silhouetteInstancedMaterial != null && blurMaterial != null && outlineMaterial != null;
        }
    }
}