# Changelog

## [0.3.0-preview.1] - 2026-10-23

### Unity-tested milestone — confirmed 2026-10-23

User confirmed the current Unity 2022.3 project workflow works after installing this Git UPM package, including native BRRES model import and CHR0 animation import from BRRES archives. This includes the model/animation importer workflow and separate animation-only BRRES support discussed for `mot_em001.brres`.

- **Milestone:** first user-confirmed working Unity version.
- **Frozen code snapshot:** [`milestone/v0.3.0-preview.1-unity-tested`](https://github.com/MusafirEx/CHR0_2_Unity/tree/milestone/v0.3.0-preview.1-unity-tested).
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
