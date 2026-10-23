# Changelog

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
