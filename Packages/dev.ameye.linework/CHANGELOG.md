# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!-- Headers should be listed in this order: Added, Changed, Deprecated, Removed, Fixed, Security -->

## [1.5.3] - 2025-12-09

### Added

- Edge Detection: Added option to fade edge thickness by distance

### Fixed

- Surface Fill: Fixed glow pattern not showing the chosen color

## [1.5.2] - 2025-12-02

### Fixed

- Edge Detection: Fixed issue with edge break-up not showing up in several cases

## [1.5.1] - 2025-11-27

### Added

- Edge Detection: Added option to set render queue for section map

### Fixed

- Edge Detection: Fixed the section shader no longer being SRP-batcher compatible
- Edge Detection: Reduced number of shader variants being included in build
- Edge Detection: Keep the section map from being generated when not necessary
- Wide Outline: Fixed issue with 'Scale With Resolution' option causing excessive draw calls during flood step
- Surface Fill: Fixed not all necessary variants being included in build

## [1.5.0] - 2025-11-20

### Added

- Surface Fill: Added the option to set the occluders when using the 'Render When Occluded' option
- Surface Fill: Added 'Squares' pattern option
- Fast Outline: Added Closed Loop option matching the Closed Loop options in Soft/Wide outline
- Wide Outline: Added support for HDR outline colors
- Edge Detection: Added experimental 'hand-drawn effect' settings (edge distortion + edge break up)
- Edge Detection: Added new 'circular' edge detection operator which looks nicer at a performance cost

### Changed

- Improved section map object ID hash function to reduce ID collisions
- Surface Fill: Rewrote from Shader Graph to hlsl for improved performance and stability
- Edge Detection: Changed debug view of section map to display all channels
- Removed package samples since they had little use

### Fixed

- Fast Outline: Fixed render queues not always being respected
- Unity 2022/Compatibility Mode: Fixed GPU instancing not working for Fast Outline
- Removed usage of `System.Linq` in runtime scripts to reduce allocated garbage
- Wide Outline: Fixed issue with 'Scale With Resolution' option

## [1.4.12] - 2025-07-29

### Added

- Wide Outline: Added option to clear stencil buffer before rendering outline to improve compatibility with other effects that render to the stencil buffer
- Wide Outline: Added option to scale shared outline width with screen resolution

### Fixed

- Surface Fill: Set normals buffer input to be required when rendering glow
- Wide Outline: Renamed distance variable to pixelDistance for improved shader compiler compatibility
- Soft Outline: Renamed distance variable to pixelDistance for improved shader compiler compatibility
- Edge Detection: Renamed distance variable to pixelDistance for improved shader compiler compatibility
- Unity 2022/Compatibility Mode: Fixed transparent materials not being outlined
- Wide Outline: Fixed blend mode rendering issues

## [1.4.11] - 2025-07-16

### Changed

- Edge Detection: Fill must now be enabled through a toggle (false by default)

### Fixed

- Wide Outline: Fixed blend mode rendering issues

## [1.4.10] - 2025-07-12

### Added

- Fast Outline: Added option to scale line width with screen resolution
- Surface Fill: Added vertex animation support

### Changed

- Unity 2022: Transparent objects now also draw to the section buffer
- Edge Detection: Keep unused variants from being included in the final build

### Fixed

- Fast Outline: Fixed vertex colors not being remapped to -1,1 range before being used for vertex extrusion
- Unity 2022: Fixed information buffer not being generated for wide outline
- Edge Detection: Fix discontinuity masking issue where enabling it would mask out section input
- Edge Detection: Fix mask and fill values not working

## [1.4.9] - 2025-03-20

### Added

- Edge Detection: Added option to exclude renderers from getting outlined

### Fixed

- Soft Outline: Fixed dilation shader bleeding over edges of screen
- Edge Detection: Fixed edge detection shader bleeding over edges of screen
- Unity 2022: Fixed edge detection vertex animation not working for Unity 2022

## [1.4.8] - 2025-03-13

### Fixed

- Android: Fixed graphics format not being supported
- Fixed some incorrect files being included in 1.4.7

## [1.4.7] - 2025-03-12

### Fixed

- Addressables: Added additional null checks to prevent errors during addressables build on Windows

## [1.4.6] - 2025-03-08

### Added

- Compatibility Window: Added check to detect the active pipeline asset
- Compatibility Window: Added check to detect whether Spatial-Temporal Post-processing is enabled

### Fixed

- Unity 2022: Fixed a rendering issue when realtime reflection probes are present
- Compatibility mode: Fixed a rendering issue when realtime reflection probes are present
- Unity 2022: Added step to clear stencil buffer after rendering fast outline to avoid unexpected interactions with other effects
- GPU Instancing: Fixed editor issue for OutlineOverride when showing integer fields

## [1.4.5] - 2025-02-24

### Changed

- Moved SmoothNormalsBaker to editor namespace

### Fixed

- Improved compatibility with other packages depending on older versions of com.unity.collections

## [1.4.4] - 2025-02-24

### Fixed

- Surface Fill: Fixed issue with layer mask

## [1.4.3] - 2025-02-22

### Added

- Added layer mask filter option to filter outlines/fills by layer in addition to rendering layer
- Surface Fill: Added option to set render queue (adds support for transparent objects)

### Changed

- Improved section map object ID hash function to reduce ID collisions

## [1.4.2] - 2025-02-22

### Fixed

- Edge Detection: Fixed depth/height fade variable names

### Changed

- Changed from `TextureDesc.format` to `TextureDesc.colorFormat` for improved compatibility with older version of Unity 6
- Temporarily disabled fill functionality for section map 

## [1.4.1] - 2025-02-18

### Added

- Surface Fill: Added option to set custom fill material (experimental)

### Fixed

- Surface Fill: Fixed issue with glow pattern where secondary color was being set but not needed

## [1.4.0] - 2025-02-17

### Added

- Fast Outline: Added option to set render queue (adds support for outlined transparent objects)
- Soft Outline: Added option to set render queue (adds support for outlined transparent objects)
- Soft Outline: Added UV Transform property to alpha cutout outlines
- Soft Outline: Added gap property which allows for adding a gap between an object and its outline
- Wide Outline: Added option to configure outline width on a per-object basis
- Wide Outline: Added option to set render queue (adds support for outlined transparent objects)
- Wide Outline: Added UV Transform property to alpha cutout outlines
- Wide Outline: Added gap property which allows for adding a gap between an object and its outline
- Wide Outline: Added option to set custom wide outline material (experimental)
- Edge Detection: Added option to fade edge color by height
- Edge Detection: Added option to switch section map precision between 8 bit and 16 bit
- Edge Detection: Added option to set section map clear color
- Edge Detection: Added option for additional custom section passes to be defined (adds support for vertex animation)

### Fixed

- Edge Detection: Fixed shallow angle artifacts fix not working if only depth is used as a discontinuity source
- Edge Detection: Fixed shader error in scene luminance debug view
- Edge Detection: Fixed section buffer graphics format not being supported on WebGPU
- Fixed HelpUrl documentation links in custom editors
- Convert usage of (deprecated) cameraData.cameraTargetDescriptor to resourceData.activeColorTexture.GetDescriptor()

## [1.3.4] - 2025-01-15

### Fixed

- Unity 2022: Fixed vertex animation not working for wide and soft outline
- Unity 2022: Fixed issue with section map custom rendering option not working

## [1.3.3] - 2025-01-04

### Added

- Surface Fill: Added compatibility with VR (not tested on actual hardware)
- Fast Outline: Added compatibility with VR (not tested on actual hardware)
- Soft Outline: Added compatibility with VR (not tested on actual hardware)
- Wide Outline: Added compatibility with VR (not tested on actual hardware)
- Edge Detection: Added compatibility with VR (not tested on actual hardware)

### Fixed

- Fast Outline: Added step to clear stencil buffer after rendering fast outline to avoid unexpected interactions with other effects

## [1.3.2] - 2024-12-30

### Fixed

- Soft Outline: Fixed issue with instanced rendering
- Wide Outline: Fixed issue with instanced rendering

## [1.3.1] - 2024-12-21

### Fixed

- Added com.unity.collections as a dependency since it is needed for the SmoothNormalsBaker to work
- Edge Detection: Fixes for masks/fills
- Edge Detection: Reduced number of shader variants
- Edge Detection: Fixed compilation error in section shader

## [1.3.0] - 2024-12-18

### Added

- Edge Detection: Added option to scale edge thickness with screen resolution
- Edge Detection: Added option to fade edge color with depth
- Edge Detection: Added section map support for particles
- Fast Outline: Added smoothed normals baker allowing for rendering smoother outlines with fewer artifacts
- Compatibility Window: Added check to detect outline overrides in the scene which break SRP batching
- GPU Instancing: Added GPU instancing option for improved performance when rendering many different outline variants at once
- Wide Outline: Added vertex animation support
- Soft Outline: Added vertex animation support

### Changed

- Edge Detection: Reorganized settings
- Edge Detection: Simplified debug view options
- Edge Detection: Changed precision of section map texture from 8 bit to 16 bit precision

### Fixed

- Compatibility Window: Fixed styling for light mode of Unity Editor
- MSAA + Soft Outline: Fixed rendering issues when MSAA is enabled for soft outline
- MSAA + Wide Outline: Fixed console errors when MSAA is enabled for wide outline (rendering artifacts are still present!)
- Surface Fill: Fixed rotation values not being applied correctly for texture patterns
- Unity 6: Fixed stencil rendering issue 

## [1.2.6] - 2024-11-27

### Fixed

- Unity 2022: Fixed an issue with profiling samplers being created every frame, potentially causing a crash in builds

## [1.2.5] - 2024-11-17

### Added

- Added BeforeRenderingTransparents as outline injection point

## [1.2.4] - 2024-11-13

### Added

- Soft outline: Added scale-with-resolution option for soft outline resulting in better performance at higher resolutions

### Fixed

- Android: Fixed graphics format not being supported

## [1.2.3] - 2024-11-09

### Fixed

- Edge Detection: Fixed edge detection not rendering on Unity 6000.0.22f1 or newer and Unity 2022.3.49f1 or newer

## [1.2.2] - 2024-11-06

### Changed

- Edge Detection: Changed default background to clear instead of white

### Fixed

- Edge Detection: Fixed masking not working
- Fixed potential UnassignedReferenceExceptions when outline/fill material was not assigned
- Fixed package samples missing scripts and materials

## [1.2.1] - 2024-11-03

### Added

- Added custom property drawer for rendering layer mask in Unity 2022

### Fixed

- Wide Outline: Fixed Wide Outline not working with render scales different from 1
- Compatibility Check: Fixed error when using compatibility check in a project using a 2D renderer

## [1.2.0] - 2024-10-25

### Added

- Wide Outline: Added alpha cutout support
- Soft Outline: Added alpha cutout support
- WebGL: Added support for WebGL (except for Soft Outline)
- iOS: Added support for iOS
- Added the SetActive method for enabling/disabling outlines through code

### Fixed

- Fixed typos

## [1.1.1] - 2024-10-12

### Fixed

- Fixed a compilation error on older version of Unity 2022.3

## [1.1.0] - 2024-10-07

### Added

- Added support for Unity 2022.3
- Added support for Unity 6 with compatibility mode enabled
- Added (experimental) support for the DOTS Hybrid Renderer
- Compatibility Check: Added new compatibility check window to see if Linework will work with your project
- Added option to create outline settings directly from within the renderer feature UI

### Removed

- Removed unused code
- Removed old 'About and Support' window

### Fixed

- Fixed various memory leaks and unnecessary memory allocations

## [1.0.0] - 2024-08-25

### Added

- Fast Outline: Added the Fast Outline effect for rendering simple outlines using vertex extrusion
- Soft Outline: Added the Soft Outline effect for rendering soft and glowy outlines
- Wide Outline: Added the Wide Outline effect for rendering consistent and smooth outlines
- Edge Detection: Added the Edge Detection effect for rendering a full-screen outline effect that applies to the whole scene
- Surface Fill: Added the Surface Fill effect for rendering screen-space fill effects and patterns