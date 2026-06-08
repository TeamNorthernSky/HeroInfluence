# KJ Work Final Handover Report

## 2026-05-13

### ToonShading_Test Outline rendering analysis

- Requested context files checked: `AGENTS.md`, `CLAUDE.md`.
- `Assets/KJ_Work/final_handover_report.md` did not exist at session start, so this file was recreated to preserve the current handover context.
- Scene checked: `Assets/KJ_Work/Scenes/ToonShading_Test.unity`.
- Main Camera in `ToonShading_Test` has URP Additional Camera Data `m_RendererIndex: -1`, so it uses the active pipeline asset's default renderer.
- Current project quality setting is `Ultra`; `ProjectSettings/QualitySettings.asset` points Ultra to `Assets/DH_Work/Settings/DH_URP-HighFidelity.asset`.
- `DH_URP-HighFidelity.asset` default renderer is `DH_URP-HighFidelity-Renderer`, which does not include KJ outline render feature.
- `Assets/Settings/URP-HighFidelity.asset` contains `URP-JC-KJ-Renderer` at renderer index 2, but `ToonShading_Test` is not explicitly using that renderer.
- `Assets/KJ_Work/RenderData/OutlineRendererData.asset` is not referenced by the checked URP pipeline assets' `m_RendererDataList`; only its own `.meta` contains its GUID.
- `Assets/KJ_Work/RenderData/OutlineRendererData.asset` has `KJOutlineRendererFeature`, but the serialized settings only contain `outlineMaterial`; `stencilWriteMaterial` is missing/null.
- `KJ_OutlineRendererFeature.AddRenderPasses()` returns early when either `settings.stencilWriteMaterial` or `settings.outlineMaterial` is null, so missing stencil material prevents outline pass enqueue.
- `Assets/KJ_Work/Materials/KJ_OutlineMaterial.mat` correctly uses `Assets/KJ_Work/Shaders/Outline/KJ_Outline.shader`.
- However, `Assets/KJ_Work/RenderData/OutlineRendererData.asset`'s serialized `outlineMaterial` reference points to `KJ_OutLineShaderGraph.shadergraph` GUID instead of `KJ_OutlineMaterial.mat` GUID, so the renderer feature material slot is likely misassigned.

### Outline issue conclusion

The most likely reason the outline is not rendered is not `_OutlineWidth`, but render pipeline wiring:

1. `ToonShading_Test` camera uses renderer index `-1`, and the active quality pipeline default renderer does not include KJ outline feature.
2. The standalone `OutlineRendererData.asset` is not wired into the active URP pipeline/renderer list.
3. The standalone `OutlineRendererData.asset` outline feature is incomplete because `stencilWriteMaterial` is not assigned.
4. `OutlineRendererData.asset` appears to reference the wrong object in its `outlineMaterial` slot.

Recommended fix direction:

- Use a URP renderer that actually contains `KJ_OutlineRendererFeature`.
- Assign `KJ_StencilWriteMaterial` to `stencilWriteMaterial`.
- Assign `KJ_OutlineMaterial` to `outlineMaterial` if using `KJ_OutlineRendererFeature`'s stencil + outline pass design.
- Set the `ToonShading_Test` camera renderer index explicitly to the renderer containing the outline feature, or make that renderer the active pipeline's default renderer for the test.

### Outline smoothing follow-up

- Outline is now confirmed visible by the user.
- New issue: hard/bent mesh edges show broken or discontinuous outline because `KJ_OutlineMaterial` currently uses `_NORMALSOURCE_NORMAL`.
- `KJ_Outline.shader` already supports alternate outline expansion sources: `Normal`, `Color`, and `UV2`.
- Recommended approach for the current inverted-hull outline: bake averaged/smoothed normals into UV2 or vertex color, then set `KJ_OutlineMaterial`'s Normal Source to `UV2` or `Color`.
- This keeps the mesh's real lighting normals/hard edges intact while the outline shell expands using smooth normals.
- Clarification: the shader should read the baked smooth normal, but the actual bake step should be done before rendering by an editor/import script or DCC tool. A normal vertex shader cannot reliably average normals across duplicate vertices/seams because it only sees one vertex at a time.
- Implemented `Assets/KJ_Work/Scripts/Editor/KJ_OutlineSmoothNormalBaker.cs`.
- Added editor menu: `KJ Work/Outline/Bake Smooth Normals To UV2 (Selected)`.
  - Select GameObjects, run the menu, and the tool creates duplicated baked mesh assets under `Assets/KJ_Work/Generated/SmoothOutlineMeshes`.
  - The original imported/shared mesh assets are not overwritten.
  - `MeshFilter` and `SkinnedMeshRenderer` are supported.
- Added editor menu: `KJ Work/Outline/Set KJ Outline Material Normal Source To UV2`.
- Updated `Assets/KJ_Work/Shaders/Outline/KJ_Outline.shader` to normalize the selected outline normal and fall back to the original mesh normal if the selected source is empty.
- Unity AssetDatabase refresh completed successfully after the change.
- Console showed an unrelated existing error in `KJ_HeroBuildingStateController.OnValidate()` about using `Renderer.materials` on prefab objects; this was not modified.

### Herobuilding outline offset/shadow-like issue

- User confirmed other objects work, but `herobuilding` outline appears shifted to one side like a shadow.
- Runtime diagnostics on `ToonShading_Test` found `herobuilding` still uses original meshes from `Assets/KJ_Work/Prefabs/herobuilding.fbx`.
- `herobuilding` renderers checked: `antena`, `Body`, `Floor`, `Stair`.
- All checked `herobuilding` meshes had `uv2=0`, so they do not currently contain baked smooth normals.
- `KJ_OutlineMaterial` is currently set to `_NORMALSOURCE_UV2`, so this object does not have the same baked input data as the objects that render correctly.
- `herobuilding` FBX child renderers use tiny mesh bounds with child `localScale=(100,100,100)` and rotated FBX transforms. This makes normal-based extrusion more sensitive and can make a shifted shell read visually like a shadow.
- Additional risk: the current baker groups vertices by object-space position tolerance `0.0001`. For very small imported meshes, that tolerance may be too large relative to mesh size if `herobuilding` is baked later, causing unrelated nearby vertices to be averaged together.

Recommended follow-up:

- Re-bake the scene `herobuilding` instance, then confirm its renderers reference generated `*_OutlineSmoothUV2.asset` meshes instead of `herobuilding.fbx`.
- If the issue persists after bake, reduce the smooth-normal baker position tolerance for small FBX meshes or use a bounds-relative tolerance.
- For building-like flat/open meshes, also test a smaller `_OutlineWidth` or `_WIDTHMODE_WORLD`, because normal-based inverted hulls can look like offset shadows on broad planar surfaces.

## 2026-05-14

### Root-separated outline depth handling

- New issue: when different root objects overlap on screen, their outlines merge into one silhouette.
- Cause: previous stencil outline flow wrote every outlined renderer with the same `Stencil Ref 1`, so separate root objects were treated as one mask.
- Goal: keep multiple meshes under the same root object merged into one outer outline, but allow different root objects to draw outlines against each other, with depth testing suppressing outlines from objects behind the front object.
- Updated `Assets/KJ_Work/Shaders/Outline/KJ_StencilWrite.shader` to expose `_StencilRef` and use `Ref [_StencilRef]`.
- Updated `Assets/KJ_Work/Shaders/Outline/KJ_Outline.shader` to expose `_StencilRef` and use `Ref [_StencilRef]`.
- Reworked `Assets/KJ_Work/Scripts/KJ_OutlineRendererFeature.cs`:
  - Added `usePerRootStencil` setting, default `true`.
  - In per-root mode, visible renderers on the outline layer are grouped by the highest parent transform that is also on an outline layer.
  - Each root group gets a separate stencil ref, while meshes inside that root share the same ref.
  - Groups are drawn back-to-front and still use the outline shader depth test, so front roots can draw separating outlines over roots behind them.
  - Legacy single-stencil mode remains available by disabling `usePerRootStencil`.
- Unity `AssetDatabase.Refresh()` completed successfully after fixing one compile error caused by a readonly `FilteringSettings` field passed by `ref`.
- A compile probe script executed successfully afterward, confirming scripts compile.

### Game View low-resolution appearance check

- User reported that the Game View/camera looked low resolution.
- Runtime diagnostics:
  - Quality level: `Ultra`.
  - Current URP asset: `DH_URP-HighFidelity`.
  - URP `renderScale=1`, `msaa=4`, HDR supported.
  - Main Camera has no target texture, `allowDynamicResolution=False`.
  - Main Camera pixel size: `1141x642`, scaled pixel size: `1141x642`.
  - Game View selected size: `16x9`; target size: approximately `1141x642`.
  - Game View zoom area scale: approximately `2.37x`.
- Conclusion: no evidence that URP render scale or camera dynamic resolution is lowering the render. The low-resolution look is most likely from the Game View being in aspect-ratio mode with a relatively small panel target size and a zoom/scale value above 1x.
- Suggested fix: set Game View Scale/Fit to 1x or Fit, maximize the Game View, or add/select a fixed resolution such as `1920x1080` instead of the `16x9` aspect-only preset.

### Outline disappears at farther camera distances

- User reported that the outline disappears after the camera moves beyond a certain distance.
- Environment checks:
  - Main Camera far clip is `1000`, near clip is `0.3`, FOV is `60`.
  - No nonzero per-layer cull distances were found.
  - Current outline targets were still inside the camera frustum in the diagnostic snapshot.
  - `KJ_OutlineMaterial` is using screen-space width mode (`_WIDTHMODE_SCREEN`), so the outline should not simply shrink away because of distance.
- Platform probe confirmed `SystemInfo.graphicsDeviceType=Direct3D11` and `SystemInfo.usesReversedZBuffer=True`.
- Current `KJ_Outline.shader` applies depth bias as `clipPos.z -= _DepthBias * clipPos.w`.
- With reversed-Z, subtracting clip-space Z pushes the outline farther away instead of pulling it toward the camera. At larger distances, this can make the expanded outline shell fail `ZTest LEqual` against the original mesh/depth buffer, so the outline looks like it disappears.
- Recommended fix: make `_DepthBias` account for `UNITY_REVERSED_Z`, using `clipPos.z += _DepthBias * clipPos.w` on reversed-Z platforms and subtracting only on non-reversed-Z platforms.

### Smooth outline normal workflow note

- Question raised: whether every object must generate dedicated outline UV data for this shader.
- Clarification: the shader does not generate UV data at runtime. The current smooth outline workflow stores pre-baked smooth normals in UV2 only for meshes that need smoother inverted-hull expansion.
- Objects that look acceptable with their original mesh normals can keep using the Normal source and do not need baked UV2.
- This is a per-mesh asset preprocessing step, not a per-frame shader operation, but it can be inconvenient if many imported meshes need the same treatment.
- If per-object/mesh preprocessing becomes too costly for production workflow, consider switching to or adding a screen-space outline path based on depth/normal edge detection. That avoids baked smooth-normal data but produces a different look and has different limitations around internal edges, thickness stability, and object separation.

### HDR and IBL task ordering

- Upcoming rendering tasks identified: HDR setup and IBL setup.
- Recommended order: apply and verify HDR rendering first, then add IBL.
- Reason: IBL commonly depends on HDR environment/cubemap data, exposure range, bloom/tone mapping, and color grading. If HDR range and post-processing are not stable first, IBL intensity and color will be tuned against an unreliable baseline.
- Note: HDR rendering in the pipeline should be handled before IBL. HDR display output for HDR monitors can be treated as a later polish/export target if needed.

### Current HDR state check

- Runtime probe in `ToonShading_Test`:
  - Quality level: `Ultra`.
  - Current URP asset: `DH_URP-HighFidelity`.
  - URP `supportsHDR=True`, `renderScale=1`, HDR color buffer precision `_32Bits`.
  - Main Camera `allowHDR=True`, no target texture.
  - Main Camera URP additional data `renderPostProcessing=False`.
  - Active `Global Volume` profile currently has no Bloom, Tonemapping, or Color Adjustments overrides.
- Conclusion: HDR rendering support is already enabled, so the base HDR pipeline does not need major changes. The remaining HDR-related work is likely look-development/post-processing: enabling camera post-processing if needed, adding Tonemapping/Bloom/Exposure/Color Adjustments, and tuning them after IBL is introduced.

### IBL texture requirement clarification

- Question raised: whether IBL requires applying a texture.
- Clarification: IBL needs some kind of environment lighting data, but it does not strictly have to be a manually assigned HDRI texture.
- Common IBL sources in Unity:
  - HDRI/cubemap texture assigned through a skybox or custom reflection source.
  - Procedural Skybox, which Unity can use to generate ambient/reflection data.
  - Reflection Probe cubemap captured from the scene.
  - Simple Gradient/Color ambient lighting, which is much less image-based but can still provide environment-like diffuse lighting.
- For convincing IBL, especially specular reflections and directional sky color, an HDRI or generated cubemap is usually preferred.
- For the current toon style, use IBL lightly: start with skybox/reflection probe based environment lighting, then limit intensity so it supports the toon shading instead of overpowering it.

## 2026-05-15

### IBL resource preparation

- User asked what resources are needed before applying IBL to the current rendering setup.
- Current `ToonShading_Test` scene uses Unity's default skybox material and default reflection source; no custom reflection cubemap is assigned.
- Existing `KJ_ToonBase.shader` samples spherical harmonics via `SampleSH(normalWS)`, so sky/ambient diffuse lighting can affect that toon shader.
- The current KJ toon shader search did not show Unity reflection probe/cubemap specular sampling in `KJ_ToonBase.shader`; specular IBL will require either a shader addition or use of materials/shaders that already consume reflection probes.
- Recommended preparation:
  - One environment source: HDRI/cubemap texture, procedural skybox, or captured reflection probe.
  - A skybox material or RenderSettings custom reflection setup.
  - At least one Reflection Probe if scene-local reflections are needed.
  - Toon shader controls for indirect diffuse intensity and optional specular/reflection intensity.
  - Optional post-processing/tone mapping once IBL intensity is introduced.

## 2026-05-19

### Outline UV2 missing or invalid data shift analysis

- User asked why increasing outline width causes the outline pass to shift left when a mesh does not have valid outline UV2 data.
- Current `KJ_OutlineMaterial` uses `_NORMALSOURCE_UV2`, so `KJ_Outline.shader` reads `input.uv2.xyz` as the outline extrusion normal.
- `KJ_Outline.shader` does include a zero-vector fallback: if `dot(outlineNormal, outlineNormal) <= 0.000001`, it falls back to `input.normalOS`.
- Therefore, a truly empty UV2 channel should fall back to the mesh normal and should not cause a consistent one-sided shift by itself.
- The likely problematic case is a mesh that has UV2 data, but that UV2 contains ordinary 2D UV/lightmap coordinates rather than baked smooth normals. Since those values are nonzero, the shader normalizes them and treats them as an object-space normal.
- In screen-width mode, that invalid "normal" is transformed into clip-space direction and applied via `clipPos.xy += offset * _OutlineWidth * widthMult * clipPos.w`, so increasing `_OutlineWidth` amplifies the wrong directional offset.
- Recommended fixes:
  - Only set `KJ_OutlineMaterial` to UV2 mode for meshes whose UV2 contains baked smooth normals.
  - Use Normal mode for meshes without baked smooth-normal UV2.
  - Add a stronger validity check or encode marker if UV2 may contain unrelated data.
  - Prefer per-object/material normal source separation or make the renderer feature choose a fallback material for meshes without baked outline data.

## 2026-05-20

### Outline UV2 and non-UV2 path separation note

- User asked whether `KJ_Outline_UV` can use UV2 while `KJ_Outline` avoids UV2 and works without creating UV2 in a post-processing stage.
- Recommended architecture:
  - Keep `KJ_Outline_UV` as the inverted-hull outline shader that reads baked smooth normals from UV2.
  - Make `KJ_Outline` a non-UV2 inverted-hull shader that uses only `input.normalOS`.
  - Route renderers to the correct material by layer, renderer feature setting, or a per-renderer selection rule.
- Important clarification: a post-processing pass cannot create real UV2 because UV2 is per-vertex mesh data. Post-processing only has screen-space inputs such as color, depth, normals, stencil/mask textures, or custom render targets.
- A UV2-free post-process outline is still possible, but it should be treated as a different technique: detect/dilate edges from depth, normals, stencil, or an object mask texture.
- Tradeoff:
  - UV2 inverted-hull gives better mesh silhouette control and smooth-normal expansion, but needs baked mesh data on selected assets.
  - Post-process outline needs no mesh UV2, but requires extra screen-space buffers/masks and may behave differently around internal edges, overlaps, and depth discontinuities.

### KJ_Work to HeroInfluence/KJ folder comparison precheck

- User asked for differences before moving `Assets/KJ_Work` into `HeroInfluence/KJ`.
- In the current workspace, no `HeroInfluence` directory and no `HeroInfluence/KJ` directory were found under the project root or `Assets`.
- Therefore, a direct two-folder file diff could not be performed yet; currently all `Assets/KJ_Work` contents should be treated as absent from the intended destination.
- Current `Assets/KJ_Work` inventory: 743 files, about 27.89 MB total.
- Top-level file distribution: root 17, Docs 18, Editor 4, Generated 431, Materials 50, Prefabs 6, RenderData 8, Scenes 11, Scripts 73, Shaders 119, Textures 6.
- Move/copy caution: this folder includes Unity `.meta` files and many serialized references; if moving inside Unity, prefer Unity Editor/AssetDatabase move so GUID references are preserved.

### KJ_Work to Prototype_Merge/KJ folder comparison correction

- User corrected the intended destination from `HeroInfluence/KJ` to `Prototype_Merge/KJ`.
- In the current workspace, no `Prototype_Merge` directory and no `Prototype_Merge/KJ` directory were found under the project root or `Assets`.
- Closest similarly named existing folder is `Assets/JC_Work/__ProtoType`, which contains Prefabs, Scenes, Scripts, and Sprites, but it is not `Prototype_Merge/KJ`.
- Therefore, a direct `Assets/KJ_Work` vs `Prototype_Merge/KJ` file diff still could not be performed until the destination folder/path is available.

## 2026-05-21

### Notion daily work log update

- User added `D:/Project_ORORA/CLAUDE.md` and `D:/Project_ORORA/AGENTS.md` as context files.
- `CLAUDE.md` confirms cautious, minimal, assumption-explicit workflow.
- `AGENTS.md` confirms Korean responses, environment/render-pipeline checks during symptom analysis, and daily work logging under the Notion `유니티 졸업 프로젝트` page.
- Current workspace does not have `Assets/KJ_Work/final_handover_report.md`; the active handover context is this file at `Assets/_ProtoType_Merge/KJ/final_handover_report.md`.
- Notion parent page found: `유니티 졸업 프로젝트`.
- Existing daily Notion pages were found for 2026-05-13, 2026-05-14, 2026-05-15, 2026-05-19, and 2026-05-20.
- Created new Notion page: `2026-05-21 KJ Outline/GitHub 복구 분석 작업 기록`.
- New page records today's ASB vs KJ outline comparison, ASBStyle Mask/Fill shader/material creation, four outline approaches in `ToonShading_Test`, and the GitHub/main branch round-trip breakage analysis.
