# Native BRRES / MDL0 importer — beta

This implementation adds Unity's `ScriptedImporter` for `.brres` and `.mdl0` in the **same UPM package as CHR0 → .anim**.

## What is implemented

- BRRES `bres/root` resource-tree traversal; multiple MDL0 model entries.
- MDL0 v8–v11 header and resource groups; material and mesh names; bone hierarchy and transforms.
- Common indexed Wii GX display-list primitives: triangles, strips, fans and quads.
- Position, normal, UV0 arrays (including common quantized integer attribute formats).
- Bone matrix-node influences and experimental SkinnedMeshRenderer + bone weights.
- Embedded TEX0 to Unity Texture2D for I4, I8, IA4, IA8, RGB565, RGB5A3, RGBA8, CMPR formats.
- Direct asset import (Unity Mesh, Texture2D, Material, GameObject) without spawning nintoolbox, running native executables, or creating intermediate GLB files.

## Known limits

- **Unity Editor compilation and scene rendering are not yet tested.** The GitHub files were statically reviewed; the asset import pipeline requires testing in an actual Unity 2022.3 LTS Editor.
- Native skinning and bind poses are preliminary, especially multi-node blends. Test CHR0 deformation before depending on this importer for animated meshes.
- Palette-indexed TEX0 formats C4/C8/C14X2 with PLT0 need decoding support.
- Complex Wii TEV multi-pass rendering, shader registers and advanced texture transform stages are not reproduced. Standard/URP/HDRP preview materials are only approximations.
- GX `DIRECT` vertex attributes and some obscure display-list configurations are deliberately not decoded. An unsupported mesh should produce a warning.
- Standalone MDL0 may have no embedded textures; prefer the original BRRES.
- Imported models can contain extra bone GameObjects used as attachment points (particle/effect anchors). They are intentionally retained even if not weighted to geometry.

## Validation record

Validation input: `axe001.brres` (197,760 bytes), independently provided by the project user.

| Field | Source sample | Expected reference |
|---|---:|---:|
| MDL0 resources | 1 | 1 |
| TEX0 resources | 3 | 3 |
| Decoded meshes | 7 | 7 |
| Triangles | **1,470** | **1,470** |

Triangles per mesh: `96, 482, 352, 188, 164, 108, 80`.

CMPR texture comparison using the three PNG images embedded in an existing reference GLB showed the correct dimensions and low average channel differences (~0.12–0.13 out of 255). Minor endpoint/interpolation rounding differences remain. **These binary-level checks do not verify the current Unity C# implementation or skinning accuracy.**

## How to test in Unity

1. Install the Git URL from the package README.
2. Remove any old `Assets/**/CHR0ToUnityAnimConverter.cs` script copies first.
3. Put a BRRES with a model (ideally `axe001.brres`) into the project Assets.
4. Select the resource. The Inspector should show `BrresNativeImporter`.
5. Check the Console for warnings/errors, and inspect the generated asset's hierarchy.
6. Place the asset in the Scene and compare against the old `axe001.glb`: polygons, triangle winding, dimensions, UV, textures, and material transparency.
7. Repeat with a skinned character model and test CHR0-generated animation clips against it.
8. Report any errors (include Unity Console log and which BRRES/MDL0 file version is used).

This importer is an independent C# implementation based on binary format research; the package does not bundle any Nintendo assets or nintoolbox's C code. Reference: [quatric/nintoolbox](https://github.com/quatric/nintoolbox).
