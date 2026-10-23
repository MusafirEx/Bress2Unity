# Bress2Unity — Native BRRES / CHR0 Unity Importer

**Unity 2022.3 LTS · Unity Package Manager (UPM) · v0.7.0-preview.1**

One Unity package with two Editor tools:

- **BRRES / MDL0 → Unity model assets:** Import meshes, skeletons, textures and materials directly, without requiring a GLB conversion.
- **CHR0 → Unity AnimationClips:** Import multiple embedded CHR0 animations from BRRES archives, or convert CHR0 using a selected rig.

> **Unity-tested milestone (23 October 2026):** The user has confirmed that the native BRRES importer and embedded/separate CHR0 workflows work in their Unity project. [Preserved working-code snapshot](https://github.com/MusafirEx/Bress2Unity/tree/milestone/v0.3.0-preview.1-unity-tested). The importer remains a preview: uncommon Wii formats, precise TEV shading, and advanced skinning are not comprehensively validated. MOT/GNTA is no longer included in the current package.

**Milestone:** [First Unity-tested release snapshot](https://github.com/MusafirEx/Bress2Unity/tree/milestone/v0.3.0-preview.1-unity-tested) · `v0.3.0-preview.1` · 2026-10-23.

## FBX-style Materials tab — v0.7.0-preview.1

The BRRES/MDL0 `ScriptedImporter` Inspector now includes **Model / Rig / Animations / Materials** tabs, with working per-material controls.

On **Materials**, choose **Import Materials** (enabled by default) and optionally select a **Generated Material Shader**. The importer uses an appropriate built-in Standard/URP/HDRP Lit shader by default and assigns decoded TEX0 textures when available. The source format's complete Nintendo TEV shading is **not** reproduced.

Each source MDL0 material is listed with its source name, model/slot indexes, referenced source texture, its generated Material and an **External Material** field. Assign an existing `.mat` asset to replace the Material used by that slot's `MeshRenderer` or `SkinnedMeshRenderer`, then click **Apply**.

**Extract Materials...** opens a folder picker. Choose a folder **inside the current project's `Assets` folder**; Bress2Unity copies all generated materials into new editable `.mat` files, sets them as external overrides, and reimports the BRRES. Names include the model and material slot index, so duplicate MDL0 names do not collide. Use **Clear Overrides** and **Apply** to restore generated materials. Shader/texture data are copied, but extracted materials can still reference texture sub-assets from the original BRRES.

The importer keeps existing stable material sub-asset identifiers, stores per-material override keys, and now creates a `BrresMaterialManifest` sub-asset containing slot metadata. Importer revision **5** triggers a fresh asset import.

**Limitations:** Bress2Unity does not automatically translate advanced Wii TEV multi-stage shaders or PLT0 palettes. Editing extracted `.mat` files does not change the source `.brres`. This Materials tab has been committed and statically audited, but **Unity 2022.3 Editor compilation, extraction and renderer assignment still require testing**.

## FBX-style Rig import settings — v0.6.0-preview.1

Select an imported `.brres` or `.mdl0` asset in the Unity **Project** window. Bress2Unity now provides a custom Inspector with **Model**, **Rig**, and **Animations** tabs, similar to FBX Rig settings.

On the **Rig** tab, choose **Animation Type**:

| Option | Result |
|---|---|
| **Generic** (default) | Build a Unity Generic `Avatar` sub-asset for each MDL0 model; attach an `Animator` to that model's child GameObject; preserve exact Wii bone hierarchy and CHR0 transform paths |
| **Humanoid** | Attempt to build a Unity Humanoid `Avatar` with editable source-bone mappings and attach an `Animator` only if the Avatar is valid |
| **Legacy** | Add an `Animation` component and set embedded CHR0 AnimationClips to Legacy; no Avatar |
| **None** | Import model and optional CHR0 clip sub-assets without generating an Animator, Animation component, or Avatar |

**Avatar Definition:** **Create From This Model** or **Copy From Other Avatar**. The latter references an existing external Avatar asset; it must match the requested Generic/Humanoid type. Unity Avatar sub-assets appear when expanding the imported BRRES asset in the Project window. No native `ModelImporter` tab is used; this is an equivalent custom `ScriptedImporter` Inspector.

**Generic Root Motion Bone:** `Y` by default, matching `0000.brres`. Clear this field to disable root motion when creating the Avatar. `Animator.applyRootMotion` defaults to off so existing CHR0 movement behavior stays unchanged.

**Humanoid mapping:** The Inspector provides editable bone names with defaults for the selected NW4R skeleton: `hip`→Hips, `waist`→Spine, `breast`→Chest, `neck`→Neck, `head`→Head, `L_harm`/`R_harm`→Upper Arms, `L_larm`/`R_larm`→Lower Arms, `L_hand`/`R_hand`→Hands, `L_hleg`/`R_hleg`→Upper Legs, `L_lleg`/`R_lleg`→Lower Legs, and `L_foot`/`R_foot`→Feet. Shoulder defaults are `L_collar` and `R_collar`.

**Important:** Humanoid acceptance requires a human-compatible bone hierarchy, orientations and suitable T-pose; matching names alone is not enough. If Unity refuses the generated Humanoid Avatar, the model/animations still import and Unity issues a warning. CHR0 transform-property clips are **not automatically converted to humanoid muscle curves** and cannot be assumed to retarget like Unity-imported humanoid FBX clips. **Use Generic for reliable original CHR0 animations.**

The `Animator` is attached to the imported **MDL0 model child**, not the outer BRRES container, so animation paths continue to match the previously generated CHR0 clips. If an imported BRRES contains multiple MDL0 models, each model gets its own Avatar/Animator.

**To test:** Update the Git package, select `0000.brres`, choose **Rig → Generic → Create From This Model**, click **Apply**, then expand the BRRES and inspect its generated Avatar and model's Animator. After that, try **Humanoid** and check the Console for `Avatar.isValid` diagnostics. **Unity Editor compilation and real Avatar validation have not yet been performed for this update.**

## Automatic MDL0 face winding correction (v0.5.0-preview.1)

Bress2Unity now **detects reversed triangle faces separately for each mesh**, by comparing its geometric face direction against its authored vertex normals. This fixes imported `0000.brres` faces without reversing correctly oriented models such as `axe001.brres`.

After updating the package, right-click `0000.brres` and choose **Reimport**. The importer version was raised to 3 to trigger the new geometry processing. The `Reverse Face Winding` checkbox in the BRRES import Inspector is an **optional manual override**, normally left unchecked. If corrected faces are still visually wrong for a particular model, toggle it and reimport; verify from both inside and outside with a standard back-face-culling material.

Independent reference check: `0000.glb` has 26 meshes / 9,506 triangles, with all 26 meshes showing overwhelmingly opposing geometric and authored normals; `axe001.glb` has seven meshes / 1,470 triangles, all consistently aligned. The automatic-correction rule distinguishes both references without a file-specific exception. This is format-level validation; the modified Unity importer still requires an Editor/Scene visual test.

**MOT/GNTA has been removed from the installed Unity package.** The earlier experimental implementation is kept only in Git history and the [archived MOT branch](https://github.com/MusafirEx/Bress2Unity/tree/archive/mot-experimental-v0.4.0-preview.2).

## Embedded CHR0 animations inside BRRES (v0.3.0-preview.1)

**Supported archive layouts:** `MDL0` only; `MDL0 + one or many CHR0`; or `CHR0` only (no model). The BRRES reader discovers every `CHR0` resource rather than assuming one animation per archive.

When you drag a BRRES into Unity's `Assets` folder, the importer creates `AnimationClip` **sub-assets** for each supported embedded CHR0 (NW4R CHR0 v5). Expand the imported BRRES in the Project window to see its clips. A BRRES containing a model will try to map CHR0 bones to its imported skeleton by name. For BRRES files containing several models, it selects the model with the most matching bone names.

**Animation-only BRRES** (for example, `mot_em001.brres`) now imports without the previous `No MDL0 resource found` error. Because it contains no skeleton hierarchy, clips are generated with **generic one-level bone paths by default**. They are visible, but they might not animate your actual model correctly until rebound.

### Export correctly bound clips for a separate model

1. Import both the animation BRRES and a compatible model into Unity.
2. Put a model instance into the Unity **Hierarchy**.
3. Select the animation BRRES in the Project window.
4. Open **Tools → MH → CHR0 from BRRES...**.
5. Assign the desired **Model root (Hierarchy)** to the model containing the correct bone tree.
6. Click **Analyze BRRES** to see every CHR0 and its matched bone count.
7. Click **Generate .anim files** to write native clips to `Assets/CHR0Animations` (configurable).

The converter reuses the existing CHR0 v5 decoder, including I4/I6/I12/L1/L2/L4 data decoding and existing position/rotation/scale logic.

**Importer Inspector settings:** `Import Animations`, `Animation Rig` (optional prefab asset when model is in a different BRRES), `Animation FPS`, `Animation Axis`, and `Animation Loop`. The Inspector rig asset can be used for automatic CHR0 binding; a **scene hierarchy model** can be assigned in the separate Editor window.

### Shared strings and format limitations

NW4R BRRES may store CHR0 bone names in its **shared string pool**, beyond the CHR0 resource's declared byte length. The importer keeps the source archive's trailing bytes available so those strings can resolve. Data parsing is bounds-checked.

Only **CHR0 v5** is supported by the CHR0 decoder. Other **BRRES animation formats** such as SRT0, PAT0 and VIS0 are not converted by this feature. MOT/GNTA is not supported by this UPM package. Animation-only files with no model cannot guarantee full bone paths without a rig.


## Install with Unity Package Manager

1. Install [Git](https://git-scm.com/download/win) and ensure it is available on PATH.
2. Unity → **Window → Package Manager** → **+ → Add package from git URL**.
3. Paste this package URL:

```text
https://github.com/MusafirEx/Bress2Unity.git?path=/Packages/com.musafirex.chr0-to-unity
```

4. Click **Add**. Delete older copies of `CHR0ToUnityAnimConverter.cs` in `Assets` if you installed the previous standalone version, to avoid duplicate class definitions.

## Native model import (BRRES/MDL0 → Unity)

```text
model.brres / model.mdl0
      │  Drag into Unity Assets/
      ▼
Built-in Unity ScriptedImporter (managed C#)
      │
      ├── Unity Mesh(es) + Renderer(s)
      ├── Bone hierarchy and experimental skin weights
      ├── Embedded TEX0 images → Texture2D
      └── Preview materials
      │
      ▼
Place imported model asset into scene
```

Select the BRRES/MDL0 asset in the Project window to adjust **Flip X**, **Flip UV**, **unitsToMeters**, **Import Textures** or **Import Skinning** options.

**Beta limitations:** paletted PLT0 textures, exact GX/TEV shaders, some uncommon vertex modes and some skinning scenarios are not fully supported. See [BRRES importer technical notes](Packages/com.musafirex.chr0-to-unity/BRRES_IMPORTER_BETA.md).

## Original model workflow (still supported)

If the native importer cannot read your model, you can still use [nintoolbox](https://github.com/quatric/nintoolbox):

```text
BRRES / MDL0 → nintoolbox / wmdlt DECODE → GLB → glTFast → Unity
```

Example commands:

```powershell
wmdlt DECODE character.brres --dest character.glb
wmdlt DECODE character.mdl0 --dest character.glb
```

See [nintoolbox commands](https://github.com/quatric/nintoolbox/blob/master/docs/COMMANDS.md). nintoolbox and glTFast are **not required** for the new native importer when it supports your model.

## CHR0 animation import (existing stable tool)

```text
CHR0 animation files → Tools > MH > CHR0 to Unity .anim → Unity AnimationClips
```

1. Select the root of your imported rigged character in the Unity **Hierarchy**.
2. Open **Tools → MH → CHR0 to Unity .anim**.
3. Choose a folder with extracted CHR0 files (extensions optional).
4. Choose FPS and coordinate conversion settings.
5. Click **Analyze CHR0** and **Generate .anim files**.
6. Assign the clips to an Animator Controller.

The model-importer and CHR0 converter are separate components in **one package**.

## Source structure

```text
Packages/
└── com.musafirex.chr0-to-unity/
    ├── package.json
    ├── README.md
    ├── BRRES_IMPORTER_BETA.md
    ├── CHANGELOG.md
    ├── LICENSE.md
    └── Editor/
        ├── CHR0ToUnityAnimConverter.cs
        ├── MusafirEx.CHR0ToUnity.Editor.asmdef
        └── BRRES/
            ├── BrresNativeImporter.cs
            └── BrresMdl0Reader.cs
Experimental/MOT/
    MotGntaUnityConverter.cs  # Archived only; excluded from UPM
```

## Validation

The first BRRES reader was checked with `axe001.brres` against a previously converted `axe001.glb`: **7 meshes, 1,470 triangles** (all seven polygon triangle counts match), **3 TEX0 textures**. This is format-level validation, **not a completed Unity Editor runtime/compilation test**.

## License and acknowledgements

[MIT License](LICENSE). Package by [Musafir Ex](https://github.com/MusafirEx). nintoolbox is an independent external project; no nintoolbox binary/source or copyrighted game assets are bundled with the importer. Consult original asset owners' rights before redistributing models.
