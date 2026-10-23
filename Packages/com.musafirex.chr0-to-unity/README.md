# CHR0 + BRRES Wii Importer (Unity UPM)

**Version 0.2.0-preview.1 — Unity 2022.3 LTS, Editor-only**

This Unity package combines two tools:

1. **CHR0 → Unity `.anim`**: the existing tested NW4R CHR0 animation converter, unchanged.
2. **BRRES / MDL0 → Unity model asset (beta)**: a new, **managed C# ScriptedImporter** that reads Wii models directly in the Editor without running nintoolbox or writing a GLB file.

The BRRES/MDL0 importer is **experimental**. Its first-pass binary parser has been validated against a sample BRRES and the reference GLB geometry/texture counts, but **it has not yet passed a real Unity compilation and visual/skinning test**.

## Install using Unity Package Manager

1. Install Git and ensure Unity can find it on PATH.
2. Unity → **Window → Package Manager** → **+ → Add package from git URL**.
3. Paste:

```text
https://github.com/MusafirEx/CHR0_2_Unity.git?path=/Packages/com.musafirex.chr0-to-unity
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

- [GitHub repository](https://github.com/MusafirEx/CHR0_2_Unity)
- [Importer beta technical notes](BRRES_IMPORTER_BETA.md)
- License: [MIT](LICENSE.md). The C# importer is an independently written managed implementation informed by public format research and verified sample files; no nintoolbox binary or GPL C source is bundled.
- MOT/GNTA remains unsupported and archived outside the UPM package.
