# Bress2Unity

**Native BRRES / MDL0 model and CHR0 animation importer for Unity 2022.3 LTS**

[![Unity 2022.3](https://img.shields.io/badge/Unity-2022.3%20LTS-222222)](https://unity.com/releases/editor/whats-new/2022.3)
[![UPM Git package](https://img.shields.io/badge/UPM-Git%20package-blue)](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-giturl.html)
[![Version](https://img.shields.io/badge/version-0.8.0--preview.4-orange)](Packages/com.musafirex.chr0-to-unity/package.json)

Import Nintendo Wii **BRRES / MDL0** models, their embedded **TEX0** textures, skeletons, basic materials, and **CHR0** animations directly into Unity. Bress2Unity is an **Editor-only preview** package: some Wii formats and exact GX/TEV rendering are not yet supported.

## Install with Unity Package Manager

**Recommended: main BRRES / MDL0 / CHR0 package.** In Unity, open **Window → Package Manager → + → Add package from git URL...** and paste this exact address:

```text
https://github.com/MusafirEx/Bress2Unity.git?path=/Packages/com.musafirex.chr0-to-unity#main
```

The Git URL above is **for Unity Package Manager, not a web page to open in the browser**. You must have [Git](https://git-scm.com/downloads) installed and accessible on `PATH`.

**Package ID:** `com.musafirex.chr0-to-unity` · **Unity:** 2022.3 LTS · **Package version:** `0.8.0-preview.4`

Alternatively, add this entry **inside the existing `dependencies` object** in your Unity project's `Packages/manifest.json`:

```json
"com.musafirex.chr0-to-unity": "https://github.com/MusafirEx/Bress2Unity.git?path=/Packages/com.musafirex.chr0-to-unity#main"
```

> **Why the subfolder?** The repository contains multiple Unity packages. The main package's `package.json` is at `Packages/com.musafirex.chr0-to-unity/package.json`, **not** the repository root. Keep `?path=/Packages/com.musafirex.chr0-to-unity` before `#main`. Cloning the GitHub repository page URL alone will not install the right package.
>
> **Already installed?** Update the existing Git dependency rather than adding a second copy. Unity may keep the previously resolved commit in `packages-lock.json`; use Package Manager's **Update** action or re-add the Git URL if necessary.

**Package resources:** [UPM package folder](Packages/com.musafirex.chr0-to-unity) · [Package README](Packages/com.musafirex.chr0-to-unity/README.md) · [Changelog](Packages/com.musafirex.chr0-to-unity/CHANGELOG.md) · [BRRES technical notes](Packages/com.musafirex.chr0-to-unity/BRRES_IMPORTER_BETA.md)

## Features

| Feature | What it does |
| --- | --- |
| **Model** | Imports supported BRRES and MDL0 meshes, skeleton hierarchy, UVs and skin weights |
| **Textures** | Decodes common Wii TEX0 formats; paletted TEX0 / PLT0 support remains limited |
| **Rig** | Options for None, Legacy, Generic or experimental Humanoid Avatar generation |
| **Animations** | Imports supported CHR0 v5 clips embedded in BRRES, including animation-only archives |
| **Materials** | Generates basic Standard/URP/HDRP Lit materials; supports external `.mat` assignments and extraction |
| **Grayscale bump** | Optional `_BM_01 ↔ _MM_01` matching and conversion of `_MM_XX` height textures into normal maps |

All BRRES importer settings are available by selecting a `.brres` or `.mdl0` asset and using its **Model / Rig / Animations / Materials** Inspector tabs.

### Quick start — BRRES model

1. Add the UPM package above to your Unity project.
2. Copy a source `model.brres` (or supported `model.mdl0`) into `Assets/`.
3. Select it in the **Project** window. In **Model**, configure scale, UVs, textures and skinning.
4. In **Rig**, start with **Generic** for original Wii skeletons, then click **Apply**.
5. Expand the imported asset for generated meshes, textures, materials, Avatar and clips. Drag the model into the Scene to inspect it.

### Quick start — CHR0 animations

- **Embedded CHR0:** Expand a model BRRES in Project to see generated `AnimationClip` sub-assets.
- **Animation-only BRRES:** The clips can import without MDL0, but may need binding to a separate rig.
- **Separate model/animation files:** Place the model in a Scene and use **Tools → MH → CHR0 from BRRES...**; select the appropriate model hierarchy, analyze the clips, then export `.anim` files.

Only supported **CHR0 v5** animations are decoded by the current importer; SRT0, PAT0 and VIS0 are not automatically converted.

### Quick start — Monster Hunter Tri `_MM_XX` textures

Some textures use numbered pairs such as `body_BM_01` (base) and `body_MM_01` (grayscale bump). The importer matches **the same prefix and numeric suffix only**; it will not pair `_BM_01` with `_MM_02`.

Select the BRRES → **Materials** → set **Height To Normal** (optional) → choose the correct `_MM Source` if not automatically identified → click **Apply**. If you previously extracted external `.mat` assets, check **Bump Mapping Preflight**, then use **Copy _MM Bump To External .mat...** to update those assets explicitly.

This conversion approximates bump mapping in Unity; it is **not** a faithful emulation of the Wii GX/TEV shader.

## Optional: experimental MOT / GNTA Workbench

The MOT decoder is **not included** in the main BRRES/CHR0 UPM package. If you want to test MOT animation decoding separately, install this **additional experimental package**:

```text
https://github.com/MusafirEx/Bress2Unity.git?path=/Experimental/MOTUnity#main
```

**Package ID:** `com.musafirex.mot-workbench` · [MOT Workbench README](Experimental/MOTUnity/README.md). Its 2-component rotation decoding is still experimental; previewed motion must be verified.

## Troubleshooting

- **UPM says `package.json` is missing:** Use the complete Git URL above, including `?path=...` **before** `#main`. Do not install the repository root as a package.
- **Git executable not found:** Install Git and restart Unity so the Editor picks up the updated `PATH`.
- **Old code after an update:** Confirm the resolved Git revision and use **Update** in Package Manager; reimport the affected `.brres` file if necessary.
- **Duplicate C# classes:** Remove any old manually copied `Assets/.../CHR0ToUnityAnimConverter.cs` if you previously installed the standalone converter.
- **Missing `_MM_XX` or no bump copied:** Enable texture import, verify the material's `_MM Source`, click **Apply**, and check the **Bump Mapping Preflight** counts.
- **Unexpected mesh, bone or shading results:** Check the [technical limitations](Packages/com.musafirex.chr0-to-unity/BRRES_IMPORTER_BETA.md) and compare against the game or a separate BRRES reader. Full TEV effects, all paletted formats and all skinning arrangements are not guaranteed.

## Compatibility, source and license

- **Unity version:** 2022.3 LTS, Editor-only package.
- **Main package:** `Packages/com.musafirex.chr0-to-unity`.
- **Experimental MOT package:** `Experimental/MOTUnity`, installed separately.
- **Alternative conversion workflow:** [nintoolbox](https://github.com/quatric/nintoolbox) BRRES → GLB → Unity, if a source asset is not supported by the native importer.
- **License:** [MIT](LICENSE).
- **Preserved Unity-tested baseline:** [v0.3.0-preview.1 milestone](https://github.com/MusafirEx/Bress2Unity/tree/milestone/v0.3.0-preview.1-unity-tested).

This is a community-developed tool. Support for some model variants, materials, shaders, rigs and advanced animations is experimental; no guarantee of 1:1 rendering or animation fidelity is implied.
