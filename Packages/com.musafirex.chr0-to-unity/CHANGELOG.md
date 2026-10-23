# Changelog

## [0.5.0-preview.1] - 2026-10-23

- **Remove the experimental MOT/GNTA importer completely from the installable Unity package** (all `Editor/MOT` scripts, corresponding `.meta` files, and MOT beta notes). The failed attempt is preserved in [the MOT archive branch](https://github.com/MusafirEx/Bress2Unity/tree/archive/mot-experimental-v0.4.0-preview.2); the pre-existing `Experimental/MOT` prototype remains outside the UPM package.
- Fix inside-out faces on some BRRES models (including the `0000.brres` reference) by automatically checking triangle winding against authored vertex normals **per mesh**. Previously-correct `axe001.brres` meshes remain unchanged.
- Add an optional **Reverse Face Winding** importer Inspector override for atypical models where manual correction is needed.
- Bump the BRRES ScriptedImporter from version 2 to 3 to trigger reimport of assets with updated geometry logic.
- Independent reference geometry tests: **26/26 `0000.glb` meshes identified for correction**, **0/7 `axe001.glb` meshes changed**; 9,506 and 1,470 reference triangles respectively.
- This is reference-format verification only; the repaired importer still needs in-Editor confirmation on `0000.brres`.
- Preserve the previously tested BRRES/CHR0 animation decoding and conversion logic; package identifier stays `com.musafirex.chr0-to-unity` for installation compatibility.


## [0.4.0-preview.2] - 2026-10-23

- Fix MOT import failure in Unity 2022.3: `InvalidOperationException: Failed to add object of type MotArchiveInfo` at `MotNativeImporter.cs:62`.
- Move the public `MotArchiveInfo : ScriptableObject` to its matching `MotArchiveInfo.cs` script, with new Unity GUID metadata.
- Move serializable `MotSlotInfo` alongside the manifest class.
- Keep MOT parsing/clip creation behavior unchanged, and leave the tested BRRES/CHR0 importer untouched.
- After installing this patch, **Reimport** affected `.mot` assets to regenerate their archive manifest and AnimationClip sub-assets.
- Rotation decoding remains experimental; successful import does not yet establish accurate original game rotations.


## [0.4.0-preview.1] - 2026-10-23

- Add experimental native `.mot` ScriptedImporter for Eighting MOT archives containing GNTA animation slots.
- Produce `MOT_slot_XXXX` AnimationClip sub-assets when a compatible skeleton asset is available; otherwise import an archive manifest for manual rig assignment.
- Automatically look for a same-stem BRRES/MDL0 model, e.g. `0000.mot` and `0000.brres`, or allow an explicit Animation Rig.
- Add **Tools > MH > MOT to Unity .anim (Experimental)** for using a scene-rig reference, optional mapping CSV, and exporting standalone `.anim` files.
- Include conservative rotation defaults: unverified 0x0202 rotations skipped; optional experimental X/Y/Z rotation modes.
- Validate MOT headers and track structure against sample files containing 55, 7 and 2 clips.
- Preserve all existing BRRES/CHR0 importer source and the first Unity-tested v0.3 milestone branch unchanged.
- New MOT importer has not yet passed Unity compilation/visual playback and remains experimental.


## [0.3.0-preview.1] - 2026-10-23

### Unity-tested milestone — confirmed 2026-10-23

User confirmed the current Unity 2022.3 project workflow works after installing this Git UPM package, including native BRRES model import and CHR0 animation import from BRRES archives. This includes the model/animation importer workflow and separate animation-only BRRES support discussed for `mot_em001.brres`.

- **Milestone:** first user-confirmed working Unity version.
- **Frozen code snapshot:** [`milestone/v0.3.0-preview.1-unity-tested`](https://github.com/MusafirEx/Bress2Unity/tree/milestone/v0.3.0-preview.1-unity-tested).
- **Snapshot commit:** `cd543e5a85324ad30a53bafb58519f2d47a32c76`.
- **Scope:** successful testing in the user's project; **not** a claim that all Wii game variants, GX/TEV materials, shader pipelines, or skinning edge cases have been exhaustively validated.
- No converter/parser source code changed as part of marking this milestone.

- Recognize CHR0 animation resources in BRRES alongside MDL0, TEX0 and PLT0.
- Permit animation-only BRRES files (with no MDL0) to import instead of throwing an error.
- Import multiple CHR0 v5 AnimationClip sub-assets directly from a BRRES archive.
- Automatically bind clips to a model in the same BRRES by choosing the highest matching bone-name count, or to an optional external prefab rig.
- Add **Tools > MH > CHR0 from BRRES...** for converting an animation-only BRRES against a target model in the Hierarchy.
- Retain access to BRRES shared string pools for CHR0 bone names.
- Add missing Unity .meta files for package documentation/manifest and the new Editor window.
- Keep standalone CHR0 decoder and animation generation logic unchanged; only expose reusable internal entrypoints.
- Supported animation type remains **CHR0 v5**; skinning/materials of the native model importer remain experimental.


## [0.2.0-preview.1] - 2026-10-23

- Added first-pass native `.brres` / `.mdl0` `ScriptedImporter` to the **same Unity package**.
- Reads BRRES resource groups, MDL0 v8–v11, common GX mesh primitives, named skeleton hierarchies and node influences.
- Decodes supported TEX0 textures in managed C#, including CMPR, with no external nintoolbox process.
- Builds GameObject/model assets using Unity Mesh, SkinnedMeshRenderer (experimental), Texture2D and Material.
- Added beta validation notes based on `axe001.brres` compared against existing `axe001.glb`.
- CHR0 converter source is unchanged. MOT/GNTA is still excluded.
- This is a **preview** release; Unity Editor compilation, visual comparison and deformation validation are still required.

## [0.1.0] - 2026-10-23

- Packaged the previously tested Unity 2022.3 CHR0 converter as an Editor-only Git-based UPM package.
- Kept CHR0 decoding and AnimationClip generation logic unchanged.
- Added installation, BRRES > nintoolbox > GLB and CHR0 > .anim documentation.
- Excluded the non-working experimental MOT/GNTA converter.
