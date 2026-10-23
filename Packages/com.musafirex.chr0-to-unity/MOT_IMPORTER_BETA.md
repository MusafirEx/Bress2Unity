# Experimental MOT / GNTA support — v0.4.0-preview.1

**Status: experimental; not part of the previously confirmed BRRES/CHR0 milestone.**

This release adds an Editor-only native import path for Eighting-style `.mot` containers containing one or more GNTA-like animation clips. It is a separate implementation from the CHR0 reader. No C source, native executable, intermediate GLB or Nintendo/game assets are included.

## Automatic import

1. Install the latest Bress2Unity package from Git.
2. Import a compatible `.brres`/ `.mdl0` model into Unity.
3. Put the matching `.mot` in the same Assets directory. If the model and MOT share a basename (e.g. `0000.brres` + `0000.mot`), Bress2Unity attempts to use that BRRES model as the animation rig automatically.
4. Select the `.mot` asset in Unity. Under **Mot Native Importer**, assign **Animation Rig** manually if auto-detection fails. It must be a suitable imported prefab/GameObject asset with matching bones.
5. Click **Reimport**. Expand `.mot` in the Project window to inspect `MOT_slot_XXXX` AnimationClip sub-assets. The `MotArchiveInfo` main asset records the slots and per-clip reports.
6. Put the compatible model in the Scene and preview the animation in an Animator/Animation window.

**Important:** The importer generates clips automatically only when a rig can be resolved. Without a rig, it still displays the MOT archive/slot index so you can assign the proper model and reimport. No 3D model is generated from a MOT file.

## Manual import with a scene rig

Open **Tools > MH > MOT to Unity .anim (Experimental)**, choose the MOT file and a matching scene GameObject root, then run **Analyze MOT** or **Generate .anim files**. Default output is `Assets/MOTAnimations`.

Optional **Mapping CSV** fields: `BoneID,BoneName` or `bone_id_dec,suggested_glb_bone`. Leave blank to use the built-in mappings for common Eighting / GNT bones. Custom mappings override those IDs.

The original tested reference used `0000.brres` for `0000.mot`. The asset has a `nw4r_root/Y/X/Z/hip` skeleton with upper/lower arm and leg chains; the MOT tracks use numeric bone IDs rather than bone names.

## Rotation limitations

**Do not assume exported clips have accurate rotations yet.** GNTA `0x0202` rotation tracks carry two floats per key; their exact rotation axis/convention has not been conclusively established. The former standalone MOT prototype failed to reproduce deformation correctly.

The safe defaults are:
- **2D rotation mode: Skip**. This creates clips from supported position/scale channels without guessing rotation curves.
- **4D axis-angle mode: Disabled** by default. Enable only for explicit visual tests.
- **Translation: relative to bind position**, `Flip X` by default for the current native BRRES skeleton convention.

To *experiment*, set **2D rotation mode** to X, Y or Z and test the clip against its correct model. The approximation treats values as a cosine/sine-like half-angle pair. Samples with `100,0` are treated as initialization markers, not literal rotation angles. Change the **Rotation composition** and axis settings if the pose is not correct. This is a trial mode—not verified GNTA support.

Other limits: unsupported flags/coordinate formats are reported, not silently reinterpreted; unknown bone IDs are skipped. The built-in mapping includes high-confidence numeric IDs 0–21 plus tentative 51/150/160; IDs 50/151/161 still require verification. For other game rigs, supply a mapping CSV.

## Binary sample verification

The existing sample files were validated at the MOT/GNTA container and track level (not by running the Unity Editor in this release):

| Sample | Occupied clips | Valid header/track data | Clips with compatible known translation tracks |
|---|---:|---|---:|
| `0000.mot` | 55 | 55 / 55 | 55 |
| `0010.mot` | 7 | 7 / 7 | 7 |
| `0030.mot` | 2 | 2 / 2 | 2 |
| **Total** | **64** | **64 / 64** | **64** |

This verifies framing, offsets, component counts, key times, and transform values. **It does not verify Unity compilation, animation playback, MOT rotation correctness, or accurate deformation.** The 64 clips with translations are not equivalent to 64 faithfully reconstructed complete animations.

## Source and references

- [Native MOT ScriptedImporter](Editor/MOT/MotNativeImporter.cs)
- [MOT/GNTA decoder and curve builder](Editor/MOT/MotGntaUnityConverter.cs)
- [MOT manual export window](Editor/MOT/MotExportWindow.cs)
- [Naruto GNT MOT file-format research](https://github.com/NicholasMoser/Naruto-GNT-Modding/blob/main/gnt4/docs/file_formats/mot.md)

The pre-existing **v0.3.0-preview.1 Unity-tested snapshot** remains unchanged on its [milestone branch](https://github.com/MusafirEx/Bress2Unity/tree/milestone/v0.3.0-preview.1-unity-tested).
