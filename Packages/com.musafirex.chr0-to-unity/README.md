# Bress2Unity — BRRES / CHR0 Importer (Unity UPM)

**Version 0.6.0-preview.2 — Unity 2022.3 LTS, Editor-only**

This Unity package combines two Editor tools:

1. **BRRES / MDL0 → Unity model assets:** a native C# ScriptedImporter that reads Wii model files directly without nintoolbox or GLB intermediates.
2. **CHR0 → Unity `.anim` or AnimationClip sub-assets:** the already-tested CHR0 animation converter, including CHR0 resources embedded in BRRES.

**Unity-tested milestone (23 October 2026):** The user confirmed this package's BRRES model and CHR0 animation workflows work in Unity. The [tested source snapshot](https://github.com/MusafirEx/Bress2Unity/tree/milestone/v0.3.0-preview.1-unity-tested) is preserved in a separate Git branch. The importer remains **preview/beta** for unsupported model variants, advanced Wii materials and skinning edge cases.

## FBX-style Rig import settings — v0.6.0-preview.2

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


## Install using Unity Package Manager

1. Install Git and ensure Unity can find it on PATH.
2. Unity → **Window → Package Manager** → **+ → Add package from git URL**.
3. Paste:

```text
https://github.com/MusafirEx/Bress2Unity.git?path=/Packages/com.musafirex.chr0-to-unity
```

4. Click Add.

**Upgrade warning:** If your project already contains `Assets/CHR0UnityConverter/Editor/CHR0ToUnityAnimConverter.cs`, delete that old copy before using this package to avoid duplicate types. Do not import anything from `Experimental/MOT`.

## A. Native BRRES / MDL0 import (beta)

Just drag a `.brres` or `.mdl0` into your Unity project's `Assets/` directory. Unity detects it via `ScriptedImporter`. Inspect the generated model asset and drag it into a Scene to preview it.

The importer attempts to create:

- A GameObject model root for each embedded MDL0.
- Named bone hierarchy and original local transform data.
- Unity meshes using MDL0 vertex position, normals, UV0, GX display-list triangles.
- Bone weights and a `SkinnedMeshRenderer` when usable node influences are present.
- Texture2D assets from embedded TEX0 resources using common *non-paletted* formats including Wii CMPR.
- Basic Unity materials. Complex Wii TEV shaders are **not** reproduced.

Click a BRRES/MDL0 source asset in the Project window to adjust its importer Inspector options: `flipX`, `flipUV`, `unitsToMeters`, `importTextures`, `importSkinning`. Changing these should reimport the source.

A standalone `.mdl0` may refer to textures stored outside the file; importing only the MDL0 will not magically provide missing TEX0 resources. Import the **original BRRES** to get embedded textures where possible.

**Not fully supported:** paletted C4/C8/C14X2 TEX0 + PLT0, faithful TEV shading, uncommon GX direct vertex attributes, all possible NW4R versions, perfect geometry deformation. Unsupported meshes/resources should produce importer warnings instead of silently pretending to succeed.

## B. CHR0 → native Unity .anim (existing stable workflow)

1. Import a rigged model into Unity, either using the BRRES importer (beta) or the prior **nintoolbox → GLB → glTFast** workflow.
2. Instantiate the model and select the root of the skeleton in the **Hierarchy**.
3. Open **Tools → MH → CHR0 to Unity .anim**.
4. Select a folder containing CHR0 animation resources. Their filename extensions do **not** matter (the converter recognizes the `CHR0` signature).
5. Select FPS, coordinate conversion and loop handling.
6. Click **Analyze CHR0** then **Generate .anim files** (default: `Assets/CHR0Animations`).
7. Use the resulting AnimationClips with an Animator Controller.

For a native BRRES rig imported with `flipX = true`, test **Flip X** for CHR0 animation conversion first; check the result visually and adjust if your source rig differs. Binding requires matching bone names and hierarchy paths.

## Legacy workflow (optional): BRRES → nintoolbox → GLB

The established alternative remains available:

```text
BRRES / MDL0 → nintoolbox wmdlt DECODE → GLB → glTFast → Unity
CHR0 skeletal animations ──────────────────────→ CHR0 converter → .anim
```

Example commands:

```powershell
wmdlt DECODE character.brres --dest character.glb
wmdlt DECODE character.mdl0 --dest character.glb
```

Source: [quatric/nintoolbox](https://github.com/quatric/nintoolbox) and its [command documentation](https://github.com/quatric/nintoolbox/blob/master/docs/COMMANDS.md).

**With the new native importer, nintoolbox and glTFast are optional for importing supported BRRES/MDL0 models.** They remain useful as a reference and fallback for unsupported formats.

## Validation performed (without Unity)

Using the user's sample `axe001.brres`, geometry/display-list decoding independently reproduced the **seven mesh triangle counts** from the previous GLB conversion:

`96, 482, 352, 188, 164, 108, 80` — total **1,470 triangles**.

The same BRRES contains one MDL0 and three TEX0 textures with the expected 256×512 and 64×64 dimensions. Comparison to PNG images embedded in the reference GLB produced low mean pixel-channel error (rounding differences remain).

**Passing these format-level checks is not equivalent to passing Unity import, materials or skin deformation tests.** Test with `axe001.brres` in Unity before trying larger models.

## Project scope and licensing

- [GitHub repository](https://github.com/MusafirEx/Bress2Unity)
- [Importer beta technical notes](BRRES_IMPORTER_BETA.md)
- License: [MIT](LICENSE.md). The C# importer is an independently written managed implementation informed by public format research and verified sample files; no nintoolbox binary or GPL C source is bundled.
- MOT/GNTA is excluded from this UPM package; an obsolete prototype is kept in the separate `Experimental/MOT` repository directory and the archived branch.
