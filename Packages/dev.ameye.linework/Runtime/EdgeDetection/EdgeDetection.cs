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

namespace Linework.EdgeDetection
{
    [ExcludeFromPreset]
    [DisallowMultipleRendererFeature("Edge Detection")]
#if UNITY_6000_0_OR_NEWER
    [SupportedOnRenderer(typeof(UniversalRendererData))]
#endif
    [Tooltip("Edge Detection renders outlines by detecting edges and discontinuities within the scene.")]
    [HelpURL("https://linework.ameye.dev/edge-detection")]
    public class EdgeDetection : ScriptableRendererFeature
    {
        private class EdgeDetectionPass : ScriptableRenderPass
        {
            private EdgeDetectionSettings settings;
            private Material section, mask, outline;
            private readonly ProfilingSampler sectionSampler, outlineSampler;

            public EdgeDetectionPass()
            {
                profilingSampler = new ProfilingSampler(nameof(EdgeDetectionPass));
                sectionSampler = new ProfilingSampler(ShaderPassName.Section);
                outlineSampler = new ProfilingSampler(ShaderPassName.Outline);
            }

            public bool Setup(ref EdgeDetectionSettings edgeDetectionSettings, ref Material sectionMaterial, ref Material sectionMaskMaterial, ref Material outlineMaterial)
            {
                settings = edgeDetectionSettings;
                section = sectionMaterial;
                mask = sectionMaskMaterial;
                outline = outlineMaterial;
                renderPassEvent = (RenderPassEvent) edgeDetectionSettings.InjectionPoint;

                if (settings.objectId) section.EnableKeyword(ShaderFeature.ObjectId);
                else section.DisableKeyword(ShaderFeature.ObjectId);

                if (settings.particles) section.EnableKeyword(ShaderFeature.Particles);
                else section.DisableKeyword(ShaderFeature.Particles);

                switch (edgeDetectionSettings.sectionMapInput)
                {
                    case SectionMapInput.None or SectionMapInput.Custom:
                        section.DisableKeyword(ShaderFeature.InputVertexColor);
                        section.DisableKeyword(ShaderFeature.InputTexture);
                        break;
                    case SectionMapInput.VertexColors:
                        section.EnableKeyword(ShaderFeature.InputVertexColor);
                        section.DisableKeyword(ShaderFeature.InputTexture);
                        switch (edgeDetectionSettings.vertexColorChannel)
                        {
                            case Channel.R:
                                section.EnableKeyword(ShaderFeature.VertexColorChannelR);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelG);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelB);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelA);
                                break;
                            case Channel.G:
                                section.DisableKeyword(ShaderFeature.VertexColorChannelR);
                                section.EnableKeyword(ShaderFeature.VertexColorChannelG);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelB);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelA);
                                break;
                            case Channel.B:
                                section.DisableKeyword(ShaderFeature.VertexColorChannelR);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelG);
                                section.EnableKeyword(ShaderFeature.VertexColorChannelB);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelA);
                                break;
                            case Channel.A:
                                section.DisableKeyword(ShaderFeature.VertexColorChannelR);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelG);
                                section.DisableKeyword(ShaderFeature.VertexColorChannelB);
                                section.EnableKeyword(ShaderFeature.VertexColorChannelA);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                    case SectionMapInput.SectionTexture:
                        section.DisableKeyword(ShaderFeature.InputVertexColor);
                        section.EnableKeyword(ShaderFeature.InputTexture);
                        section.SetTexture(ShaderPropertyId.SectionTexture, edgeDetectionSettings.sectionTexture);
                        switch (edgeDetectionSettings.sectionTextureUvSet)
                        {
                            case UVSet.UV0:
                                section.EnableKeyword(ShaderFeature.TextureUV0);
                                section.DisableKeyword(ShaderFeature.TextureUV1);
                                section.DisableKeyword(ShaderFeature.TextureUV2);
                                section.DisableKeyword(ShaderFeature.TextureUV3);
                                break;
                            case UVSet.UV1:
                                section.DisableKeyword(ShaderFeature.TextureUV0);
                                section.EnableKeyword(ShaderFeature.TextureUV1);
                                section.DisableKeyword(ShaderFeature.TextureUV2);
                                section.DisableKeyword(ShaderFeature.TextureUV3);
                                break;
                            case UVSet.UV2:
                                section.DisableKeyword(ShaderFeature.TextureUV0);
                                section.DisableKeyword(ShaderFeature.TextureUV1);
                                section.EnableKeyword(ShaderFeature.TextureUV2);
                                section.DisableKeyword(ShaderFeature.TextureUV3);
                                break;
                            case UVSet.UV3:
                                section.DisableKeyword(ShaderFeature.TextureUV0);
                                section.DisableKeyword(ShaderFeature.TextureUV1);
                                section.DisableKeyword(ShaderFeature.TextureUV2);
                                section.EnableKeyword(ShaderFeature.TextureUV3);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        switch (edgeDetectionSettings.vertexColorChannel)
                        {
                            case Channel.R:
                                section.EnableKeyword(ShaderFeature.TextureChannelR);
                                section.DisableKeyword(ShaderFeature.TextureChannelG);
                                section.DisableKeyword(ShaderFeature.TextureChannelB);
                                section.DisableKeyword(ShaderFeature.TextureChannelA);
                                break;
                            case Channel.G:
                                section.DisableKeyword(ShaderFeature.TextureChannelR);
                                section.EnableKeyword(ShaderFeature.TextureChannelG);
                                section.DisableKeyword(ShaderFeature.TextureChannelB);
                                section.DisableKeyword(ShaderFeature.TextureChannelA);
                                break;
                            case Channel.B:
                                section.DisableKeyword(ShaderFeature.TextureChannelR);
                                section.DisableKeyword(ShaderFeature.TextureChannelG);
                                section.EnableKeyword(ShaderFeature.TextureChannelB);
                                section.DisableKeyword(ShaderFeature.TextureChannelA);
                                break;
                            case Channel.A:
                                section.DisableKeyword(ShaderFeature.TextureChannelR);
                                section.DisableKeyword(ShaderFeature.TextureChannelG);
                                section.DisableKeyword(ShaderFeature.TextureChannelB);
                                section.EnableKeyword(ShaderFeature.TextureChannelA);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Set outline material properties.
                switch (edgeDetectionSettings.DebugView)
                {
                    case DebugView.None:
                        outline.DisableKeyword(ShaderFeature.DebugSections);
                        outline.DisableKeyword(ShaderFeature.DebugDepth);
                        outline.DisableKeyword(ShaderFeature.DebugNormals);
                        outline.DisableKeyword(ShaderFeature.DebugLuminance);
                        break;
                    case DebugView.Sections:
                        outline.EnableKeyword(ShaderFeature.DebugSections);
                        outline.DisableKeyword(ShaderFeature.DebugDepth);
                        outline.DisableKeyword(ShaderFeature.DebugNormals);
                        outline.DisableKeyword(ShaderFeature.DebugLuminance);
                        break;
                    case DebugView.Depth:
                        outline.DisableKeyword(ShaderFeature.DebugSections);
                        outline.EnableKeyword(ShaderFeature.DebugDepth);
                        outline.DisableKeyword(ShaderFeature.DebugNormals);
                        outline.DisableKeyword(ShaderFeature.DebugLuminance);
                        break;
                    case DebugView.Normals:
                        outline.DisableKeyword(ShaderFeature.DebugSections);
                        outline.DisableKeyword(ShaderFeature.DebugDepth);
                        outline.EnableKeyword(ShaderFeature.DebugNormals);
                        outline.DisableKeyword(ShaderFeature.DebugLuminance);
                        break;
                    case DebugView.Luminance:
                        outline.DisableKeyword(ShaderFeature.DebugSections);
                        outline.DisableKeyword(ShaderFeature.DebugDepth);
                        outline.DisableKeyword(ShaderFeature.DebugNormals);
                        outline.EnableKeyword(ShaderFeature.DebugLuminance);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (edgeDetectionSettings.debugSectionsChannels.HasFlag(DebugSectionsChannels.R)) outline.EnableKeyword(ShaderFeature.DebugSectionsR);
                else outline.DisableKeyword(ShaderFeature.DebugSectionsR);
                if (edgeDetectionSettings.debugSectionsChannels.HasFlag(DebugSectionsChannels.G)) outline.EnableKeyword(ShaderFeature.DebugSectionsG);
                else outline.DisableKeyword(ShaderFeature.DebugSectionsG);
                if (edgeDetectionSettings.debugSectionsChannels.HasFlag(DebugSectionsChannels.B)) outline.EnableKeyword(ShaderFeature.DebugSectionsB);
                else outline.DisableKeyword(ShaderFeature.DebugSectionsB);

                if (edgeDetectionSettings.debugPerceptualSections) outline.EnableKeyword(ShaderFeature.DebugSectionsPerceptual);
                else outline.DisableKeyword(ShaderFeature.DebugSectionsPerceptual);

                if (edgeDetectionSettings.discontinuityInput.HasFlag(DiscontinuityInput.Depth)) outline.EnableKeyword(ShaderFeature.DepthDiscontinuity);
                else outline.DisableKeyword(ShaderFeature.DepthDiscontinuity);
                if (edgeDetectionSettings.discontinuityInput.HasFlag(DiscontinuityInput.Normals)) outline.EnableKeyword(ShaderFeature.NormalDiscontinuity);
                else outline.DisableKeyword(ShaderFeature.NormalDiscontinuity);
                if (edgeDetectionSettings.discontinuityInput.HasFlag(DiscontinuityInput.Luminance)) outline.EnableKeyword(ShaderFeature.LuminanceDiscontinuity);
                else outline.DisableKeyword(ShaderFeature.LuminanceDiscontinuity);
                if (edgeDetectionSettings.discontinuityInput.HasFlag(DiscontinuityInput.Sections)) outline.EnableKeyword(ShaderFeature.SectionDiscontinuity);
                else outline.DisableKeyword(ShaderFeature.SectionDiscontinuity);

                outline.SetFloat(ShaderPropertyId.DepthSensitivity, edgeDetectionSettings.depthSensitivity * 100.0f);
                outline.SetFloat(ShaderPropertyId.DepthDistanceModulation, edgeDetectionSettings.depthDistanceModulation * 10.0f);
                outline.SetFloat(ShaderPropertyId.GrazingAngleMaskPower, edgeDetectionSettings.grazingAngleMaskPower * 10.0f);
                outline.SetFloat(ShaderPropertyId.GrazingAngleMaskHardness, edgeDetectionSettings.grazingAngleMaskHardness);
                outline.SetFloat(ShaderPropertyId.NormalSensitivity, edgeDetectionSettings.normalSensitivity * 10.0f);
                outline.SetFloat(ShaderPropertyId.LuminanceSensitivity, edgeDetectionSettings.luminanceSensitivity * 30.0f);

                switch (edgeDetectionSettings.kernel)
                {
                    case Kernel.RobertsCross:
                        outline.EnableKeyword(ShaderFeature.OperatorCross);
                        outline.DisableKeyword(ShaderFeature.OperatorSobel);
                        outline.DisableKeyword(ShaderFeature.OperatorCircular);
                        break;
                    case Kernel.Sobel:
                        outline.DisableKeyword(ShaderFeature.OperatorCross);
                        outline.EnableKeyword(ShaderFeature.OperatorSobel);
                        outline.DisableKeyword(ShaderFeature.OperatorCircular);
                        break;
                    case Kernel.Circular:
                        outline.DisableKeyword(ShaderFeature.OperatorCross);
                        outline.DisableKeyword(ShaderFeature.OperatorSobel);
                        outline.EnableKeyword(ShaderFeature.OperatorCircular);
                        break;
                }

                // Outline thickness.
                outline.SetFloat(ShaderPropertyId.OutlineThickness, edgeDetectionSettings.outlineThickness);
                if (edgeDetectionSettings.scaleWithDistance)
                {
                    outline.EnableKeyword(ShaderFeature.ScaleWithDistance);
                    outline.SetFloat(ShaderPropertyId.DistanceScaleStart, edgeDetectionSettings.distanceScaleStart);
                    outline.SetFloat(ShaderPropertyId.DistanceScaleDistance, edgeDetectionSettings.distanceScaleDistance);
                    outline.SetFloat(ShaderPropertyId.DistanceScaleMin, edgeDetectionSettings.distanceScaleMin);
                }
                else outline.DisableKeyword(ShaderFeature.ScaleWithDistance);
                if (edgeDetectionSettings.scaleWithResolution) outline.EnableKeyword(ShaderFeature.ScaleWithResolution);
                else outline.DisableKeyword(ShaderFeature.ScaleWithResolution);
                switch (edgeDetectionSettings.referenceResolution)
                {
                    case Resolution._480:
                        outline.SetFloat(ShaderPropertyId.ReferenceResolution, 480.0f);
                        break;
                    case Resolution._720:
                        outline.SetFloat(ShaderPropertyId.ReferenceResolution, 720.0f);
                        break;
                    case Resolution._1080:
                        outline.SetFloat(ShaderPropertyId.ReferenceResolution, 1080.0f);
                        break;
                    case Resolution.Custom:
                        outline.SetFloat(ShaderPropertyId.ReferenceResolution, edgeDetectionSettings.customResolution);
                        break;
                }

                // Distance fade.
                if (edgeDetectionSettings.fadeByDistance) outline.EnableKeyword(ShaderFeature.FadeByDistance);
                else outline.DisableKeyword(ShaderFeature.FadeByDistance);
                outline.SetFloat(ShaderPropertyId.DistanceFadeStart, edgeDetectionSettings.distanceFadeStart);
                outline.SetFloat(ShaderPropertyId.DistanceFadeDistance, edgeDetectionSettings.distanceFadeDistance);
                outline.SetColor(ShaderPropertyId.DistanceFadeColor, edgeDetectionSettings.distanceFadeColor);

                // Height fade.
                if (edgeDetectionSettings.fadeByHeight) outline.EnableKeyword(ShaderFeature.FadeByHeight);
                else outline.DisableKeyword(ShaderFeature.FadeByHeight);
                outline.SetFloat(ShaderPropertyId.HeightFadeStart, edgeDetectionSettings.heightFadeStart);
                outline.SetFloat(ShaderPropertyId.HeightFadeDistance, edgeDetectionSettings.heightFadeDistance);
                outline.SetColor(ShaderPropertyId.HeightFadeColor, edgeDetectionSettings.heightFadeColor);

                // Masks.
                if (settings.SectionMaskRenderingLayer != 0 && settings.maskInfluence.HasFlag(MaskInfluence.Depth)) outline.EnableKeyword(ShaderFeature.DepthMask);
                else outline.DisableKeyword(ShaderFeature.DepthMask);
                if (settings.SectionMaskRenderingLayer != 0 && settings.maskInfluence.HasFlag(MaskInfluence.Normals)) outline.EnableKeyword(ShaderFeature.NormalsMask);
                else outline.DisableKeyword(ShaderFeature.NormalsMask);
                if (settings.SectionMaskRenderingLayer != 0 && settings.maskInfluence.HasFlag(MaskInfluence.Luminance)) outline.EnableKeyword(ShaderFeature.LuminanceMask);
                else outline.DisableKeyword(ShaderFeature.LuminanceMask);

                // Distortion.
                if (edgeDetectionSettings.distortEdges) outline.EnableKeyword(ShaderFeature.Distortion);
                else outline.DisableKeyword(ShaderFeature.Distortion);
                outline.SetInteger(ShaderPropertyId.DistortionStepRate, edgeDetectionSettings.distortionStepRate);
                outline.SetFloat(ShaderPropertyId.DistortionScale, edgeDetectionSettings.distortionScale);
                outline.SetFloat(ShaderPropertyId.DistortionStrength, edgeDetectionSettings.distortionStrength);
                outline.SetTexture(ShaderPropertyId.DistortionTexture, edgeDetectionSettings.distortionTexture);
                outline.SetFloat(ShaderPropertyId.DistortionThicknessInfluence, edgeDetectionSettings.distortionThicknessInfluence);

                // Breakup.
                if (edgeDetectionSettings.breakUpEdges) section.EnableKeyword(ShaderFeature.Breakup);
                else section.DisableKeyword(ShaderFeature.Breakup);
                section.SetFloat(ShaderPropertyId.BreakupScale, edgeDetectionSettings.breakUpNoiseScale);
                section.SetFloat(ShaderPropertyId.BreakupAmount, edgeDetectionSettings.breakUpNoiseAmount);

                // Fill.
                if (edgeDetectionSettings.fill) outline.EnableKeyword(ShaderFeature.Fill);
                else outline.DisableKeyword(ShaderFeature.Fill);

                outline.SetColor(ShaderPropertyId.BackgroundColor, edgeDetectionSettings.backgroundColor);
                outline.SetColor(CommonShaderPropertyId.OutlineColor, edgeDetectionSettings.outlineColor);
                outline.SetColor(ShaderPropertyId.OutlineColorShadow, edgeDetectionSettings.outlineColorShadow);
                if (edgeDetectionSettings.overrideColorInShadow) outline.EnableKeyword(ShaderFeature.OverrideShadow);
                else outline.DisableKeyword(ShaderFeature.OverrideShadow);
                outline.SetColor(ShaderPropertyId.FillColor, edgeDetectionSettings.fillColor);

                var (sourceBlend, destinationBlend) = RenderUtils.GetSrcDstBlend(settings.blendMode);
                outline.SetInt(RenderUtils.BlendModeSourceProperty, sourceBlend);
                outline.SetInt(RenderUtils.BlendModeDestinationProperty, destinationBlend);

                return true;
            }

#if UNITY_6000_0_OR_NEWER
            private class PassData
            {
                internal RendererListHandle SectionRendererListHandle;
                internal RendererListHandle SectionMaskRendererListHandle;
                internal List<RendererListHandle> AdditionalSectionRendererListHandles = new();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                // NOTE: When breaking up the edges, the G channel is needed.
                CreateRenderGraphTextures(renderGraph, resourceData, out var sectionHandle, settings.sectionMapPrecision, settings.breakUpEdges, settings.sectionMapClearValue);

                // NOTE: Only render the section map if it is used as a discontinuity source, or as a mask, or for breaking up the edges.
                if (settings.discontinuityInput.HasFlag(DiscontinuityInput.Sections) || settings.SectionMaskRenderingLayer != 0 || settings.breakUpEdges)
                {
                    // 1. Section.
                    // -> Render section map.
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Section, out var passData))
                    {
                        builder.SetRenderAttachment(sectionHandle, 0);
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                        builder.SetGlobalTextureAfterPass(sectionHandle, ShaderPropertyId.CameraSectioningTexture);

                        InitSectionRendererList(renderGraph, frameData, ref passData);
                        builder.UseRendererList(passData.SectionRendererListHandle);
                        if (settings.SectionMaskRenderingLayer != 0 && settings.maskInfluence != MaskInfluence.Nothing)
                        {
                            builder.UseRendererList(passData.SectionMaskRendererListHandle);
                        }
                        foreach (var handle in passData.AdditionalSectionRendererListHandles)
                        {
                            builder.UseRendererList(handle);
                        }

                        var setSectionPassKeyword = settings.sectionMapInput == SectionMapInput.Custom
                                                    || passData.AdditionalSectionRendererListHandles.Count > 0;
                        if (setSectionPassKeyword) builder.AllowGlobalStateModification(true);

                        builder.AllowPassCulling(false);

                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        {
                            // Enable section pass.
                            if (setSectionPassKeyword)
                            {
                                context.cmd.DisableKeyword(Keyword.ScreenSpaceOcclusion);
                                context.cmd.EnableKeyword(Keyword.SectionPass);
                            }

                            // Section pass.
                            context.cmd.DrawRendererList(data.SectionRendererListHandle);

                            // Section mask pass.
                            // NOTE: The section mask can only be used to mask out other discontinuities if sectioning itself is not used as an input.
                            if (!settings.discontinuityInput.HasFlag(DiscontinuityInput.Sections) && settings.SectionMaskRenderingLayer != 0 &&
                                settings.maskInfluence != MaskInfluence.Nothing)
                            {
                                context.cmd.DrawRendererList(data.SectionMaskRendererListHandle);
                            }

                            // Additional section passes.
                            foreach (var handle in data.AdditionalSectionRendererListHandles)
                            {
                                context.cmd.DrawRendererList(handle);
                            }

                            // Disable section map.
                            if (setSectionPassKeyword)
                            {
                                context.cmd.EnableKeyword(Keyword.ScreenSpaceOcclusion);
                                context.cmd.DisableKeyword(Keyword.SectionPass);
                            }
                        });
                    }
                }

                // 2. Composite outline.
                // -> Add the outline to the scene.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ShaderPassName.Outline, out _))
                {
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.UseAllGlobalTextures(true);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData _, RasterGraphContext context) => { Blitter.BlitTexture(context.cmd, Vector2.one, outline, 0); });
                }
            }

            private void InitSectionRendererList(RenderGraph renderGraph, ContextContainer frameData, ref PassData passData)
            {
                passData.AdditionalSectionRendererListHandles.Clear();

                var universalRenderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                var sortingCriteria = cameraData.defaultOpaqueSortFlags;

                var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, universalRenderingData, cameraData, lightData, sortingCriteria);

                var renderQueueRange = settings.sectionRenderQueue switch
                {
                    OutlineRenderQueue.Opaque => RenderQueueRange.opaque,
                    OutlineRenderQueue.Transparent => RenderQueueRange.transparent,
                    OutlineRenderQueue.OpaqueAndTransparent => RenderQueueRange.all,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var filteringSettings = new FilteringSettings(renderQueueRange, -1, settings.SectionRenderingLayer);
                var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                // Section pass.
                if (settings.sectionMapInput is SectionMapInput.None or SectionMapInput.SectionTexture or SectionMapInput.VertexColors)
                {
                    drawingSettings.overrideMaterial = section;
                }
                RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref universalRenderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                    ref passData.SectionRendererListHandle);

                // Section mask pass.
                if (settings.SectionMaskRenderingLayer != 0 && settings.maskInfluence != MaskInfluence.Nothing)
                {
                    filteringSettings = new FilteringSettings(renderQueueRange, -1, settings.SectionMaskRenderingLayer);
                    drawingSettings.overrideMaterial = mask;
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref universalRenderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref passData.SectionMaskRendererListHandle);
                }

                // Additional section passes.
                foreach (var additionalSectionPass in settings.additionalSectionPasses)
                {
                    filteringSettings = new FilteringSettings(renderQueueRange, -1, additionalSectionPass.RenderingLayer);
                    if (settings.sectionMapInput is SectionMapInput.None or SectionMapInput.SectionTexture or SectionMapInput.VertexColors)
                    {
                        drawingSettings.overrideMaterial = additionalSectionPass.customSectionMaterial;
                    }
                    var handle = new RendererListHandle();
                    RenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref universalRenderingData.cullResults, drawingSettings, filteringSettings, renderStateBlock,
                        ref handle);
                    passData.AdditionalSectionRendererListHandles.Add(handle);
                }
            }

            private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, out TextureHandle sectionHandle, SectionMapPrecision precision, bool multicolor,
                int clearValue)
            {
                var cameraDescriptor = resourceData.activeColorTexture.GetDescriptor(renderGraph);

                const float renderTextureScale = 1.0f;
                var width = (int) (cameraDescriptor.width * renderTextureScale);
                var height = (int) (cameraDescriptor.height * renderTextureScale);

                var baseDescriptor = new TextureDesc(width, height)
                {
                    dimension = TextureDimension.Tex2D,
                    msaaSamples = cameraDescriptor.msaaSamples,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                // Section buffer.
                baseDescriptor.name = Buffer.Section;
                baseDescriptor.colorFormat = GetSectionBufferFormat(precision, multicolor); // TODO: Changed to format somewhere in Unity 6 cycle?
                baseDescriptor.depthBufferBits = (int) DepthBits.None;
                baseDescriptor.clearColor = new Color((float) clearValue / 256, 0.0f, 0.0f, 0.0f);
                baseDescriptor.wrapMode = TextureWrapMode.Clamp;
                sectionHandle = renderGraph.CreateTexture(baseDescriptor);
            }
#endif
            private RTHandle cameraDepthRTHandle, sectionRTHandle;
            private RTHandle[] handles;

            #pragma warning disable 618, 672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (handles is not {Length: 1})
                {
                    handles = new RTHandle[1];
                }
                handles[0] = sectionRTHandle;

                ConfigureTarget(handles, cameraDepthRTHandle);
                ConfigureClear(ClearFlag.Color, new Color((float) settings.sectionMapClearValue / 256, 0.0f, 0.0f, 0.0f));
            }

            public void CreateHandles(RenderingData renderingData)
            {
                // Section buffer.
                var sectionBufferDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                sectionBufferDescriptor.graphicsFormat = GetSectionBufferFormat(settings.sectionMapPrecision, settings.breakUpEdges);
                sectionBufferDescriptor.depthBufferBits = (int) DepthBits.None;
                sectionBufferDescriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref sectionRTHandle, sectionBufferDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: Buffer.Section);
            }

            private static GraphicsFormat GetSectionBufferFormat(SectionMapPrecision precision, bool multicolor)
            {
                switch (precision)
                {
                    case SectionMapPrecision.Bits8:
                        return multicolor ? GraphicsFormat.R8G8B8A8_UNorm : GraphicsFormat.R8_UNorm;
                    case SectionMapPrecision.Bits16:
#if UNITY_2023_2_OR_NEWER
                        // WebGPU does not support R16_UNorm.
                        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU ? multicolor ? GraphicsFormat.R32G32B32A32_SFloat : GraphicsFormat.R32_SFloat :
                            multicolor ? GraphicsFormat.R16G16B16A16_UNorm : GraphicsFormat.R16_UNorm;
#else
                        return multicolor ? GraphicsFormat.R16G16B16A16_UNorm : GraphicsFormat.R16_UNorm;
#endif
                    default:
                        throw new NotImplementedException();
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 1. Section.
                // -> Render section map.
                var sectionCmd = CommandBufferPool.Get();

                using (new ProfilingScope(sectionCmd, sectionSampler))
                {
                    context.ExecuteCommandBuffer(sectionCmd);
                    sectionCmd.Clear();

                    var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    var renderQueueRange = RenderQueueRange.all;
                    var drawingSettings = RenderingUtils.CreateDrawingSettings(RenderUtils.DefaultShaderTagIds, ref renderingData, sortingCriteria);
                    var filteringSettings = new FilteringSettings(renderQueueRange, -1, settings.SectionRenderingLayer);
                    var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                    var setSectionPassKeyword = settings.sectionMapInput == SectionMapInput.Custom
                                                || settings.additionalSectionPasses.Count > 0;

                    // Enable section pass.
                    if (setSectionPassKeyword)
                    {
                        sectionCmd.DisableKeyword(Keyword.ScreenSpaceOcclusion);
                        sectionCmd.EnableKeyword(Keyword.SectionPass);
                    }
                    context.ExecuteCommandBuffer(sectionCmd);

                    // Section pass.
                    if (settings.sectionMapInput is SectionMapInput.None or SectionMapInput.SectionTexture or SectionMapInput.VertexColors)
                    {
                        drawingSettings.overrideMaterial = section;
                    }
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                    // Section mask pass.
                    if (settings.SectionMaskRenderingLayer != 0 && settings.maskInfluence != MaskInfluence.Nothing)
                    {
                        filteringSettings = new FilteringSettings(renderQueueRange, -1, settings.SectionMaskRenderingLayer);
                        drawingSettings.overrideMaterial = mask;
                    }
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                    // Additional section passes.
                    foreach (var additionalSectionPass in settings.additionalSectionPasses)
                    {
                        filteringSettings = new FilteringSettings(renderQueueRange, -1, additionalSectionPass.RenderingLayer);
                        if (settings.sectionMapInput is SectionMapInput.None or SectionMapInput.SectionTexture or SectionMapInput.VertexColors)
                        {
                            drawingSettings.overrideMaterial = additionalSectionPass.customSectionMaterial;
                        }
                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
                    }

                    // Disable section map.
                    if (setSectionPassKeyword)
                    {
                        sectionCmd.EnableKeyword(Keyword.ScreenSpaceOcclusion);
                        sectionCmd.DisableKeyword(Keyword.SectionPass);
                    }
                    context.ExecuteCommandBuffer(sectionCmd);
                }

                sectionCmd.SetGlobalTexture(ShaderPropertyId.CameraSectioningTexture, sectionRTHandle.nameID);
                context.ExecuteCommandBuffer(sectionCmd);
                CommandBufferPool.Release(sectionCmd);

                // 2. Composite outline.
                // -> Add the outline to the scene.
                var outlineCmd = CommandBufferPool.Get();

                using (new ProfilingScope(outlineCmd, outlineSampler))
                {
                    CoreUtils.SetRenderTarget(outlineCmd,
                        renderingData.cameraData.renderer
                            .cameraColorTargetHandle); // if using cameraColorRTHandle this does not render in scene view when rendering after post processing with post processing enabled
                    context.ExecuteCommandBuffer(outlineCmd);
                    outlineCmd.Clear();

                    Blitter.BlitTexture(outlineCmd, Vector2.one, outline, 0);
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

                sectionRTHandle?.Release();
            }
        }

        [SerializeField] private EdgeDetectionSettings settings;
        [SerializeField] private ShaderResources shaders;
        private Material sectionMaterial, sectionMaskMaterial, outlineMaterial;
        private EdgeDetectionPass edgeDetectionPass;

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
            edgeDetectionPass ??= new EdgeDetectionPass();
        }

        /// <summary>
        /// Called
        /// - Every frame, once for each camera.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null || edgeDetectionPass == null) return;

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
                Debug.LogWarning("Not all required materials could be created. Edge Detection will not render.");
                return;
            }

            var input = ScriptableRenderPassInput.None;
            if (settings.discontinuityInput.HasFlag(DiscontinuityInput.Depth) || settings.DebugView == DebugView.Depth)
            {
                input |= ScriptableRenderPassInput.Depth;
            }
            if (settings.discontinuityInput.HasFlag(DiscontinuityInput.Luminance) || settings.DebugView == DebugView.Luminance)
            {
                input |= ScriptableRenderPassInput.Color;
            }
            if (settings.discontinuityInput.HasFlag(DiscontinuityInput.Normals) || settings.discontinuityInput.HasFlag(DiscontinuityInput.Depth) ||
                settings.DebugView == DebugView.Normals)
            {
                input |= ScriptableRenderPassInput.Normal;
            }
            edgeDetectionPass.ConfigureInput(input);

#if UNITY_6000_0_OR_NEWER
            // NOTE: This is needed because the shader needs the current screen contents as input texture, but also needs to write to it, so a copy is needed.
            edgeDetectionPass.requiresIntermediateTexture = true;
#endif
            var render = edgeDetectionPass.Setup(ref settings, ref sectionMaterial, ref sectionMaskMaterial, ref outlineMaterial);
            if (render) renderer.EnqueuePass(edgeDetectionPass);
        }

        #pragma warning disable 618, 672

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (settings == null || edgeDetectionPass == null || renderingData.cameraData.cameraType == CameraType.SceneView && !settings.ShowInSceneView) return;
            if (renderingData.cameraData.cameraType is CameraType.Preview or CameraType.Reflection) return;

            edgeDetectionPass.CreateHandles(renderingData);
            edgeDetectionPass.SetTarget(renderer.cameraDepthTargetHandle);
        }

        #pragma warning restore 618, 672

        /// <summary>
        /// Clean up resources allocated to the Scriptable Renderer Feature such as materials.
        /// </summary>
        override protected void Dispose(bool disposing)
        {
            edgeDetectionPass?.Dispose();
            edgeDetectionPass = null;
            DestroyMaterials();
        }

        private void OnDestroy()
        {
            settings = null; // de-reference settings to allow them to be freed from memory
            edgeDetectionPass?.Dispose();
        }

        private void DestroyMaterials()
        {
            CoreUtils.Destroy(sectionMaterial);
            CoreUtils.Destroy(sectionMaskMaterial);
            CoreUtils.Destroy(outlineMaterial);
        }

        private bool CreateMaterials()
        {
            if (sectionMaterial == null)
            {
                sectionMaterial = CoreUtils.CreateEngineMaterial(shaders.section);
            }

            if (sectionMaskMaterial == null)
            {
                sectionMaskMaterial = CoreUtils.CreateEngineMaterial(shaders.sectionMask);
            }

            if (outlineMaterial == null)
            {
                outlineMaterial = CoreUtils.CreateEngineMaterial(shaders.outline);
            }

            return sectionMaterial != null && sectionMaskMaterial != null && outlineMaterial != null;
        }
    }
}