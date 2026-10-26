# MOT / GNTA Workbench — standalone experimental Unity package

**Version:** 0.1.0-preview.1 · Unity 2022.3 LTS · Editor-only · **not the Bress2Unity BRRES/CHR0 UPM package**.

This is a separate, **opt-in experimental tool**, following the same research workflow as the `.pemt` converter in [EMD2Unity](https://github.com/MusafirEx/EMD2Unity). It reuses the archived MOT/GNTA parser without bringing the old `.mot` ScriptedImporter (or its `MotArchiveInfo` failure) back into the Bress2Unity main package. Original `.mot` files remain ordinary Unity Project assets; no automatic MOT import occurs.

## Install (separately from Bress2Unity)

Unity Package Manager → **Add package from Git URL**:

```
https://github.com/MusafirEx/Bress2Unity.git?path=/Experimental/MOTUnity
```

The BRRES/CHR0 package remains independently installable at `?path=/Packages/com.musafirex.chr0-to-unity`. Both packages have different package identifiers and assembly names.

## Workflow

1. Put `0000.mot` and a matching `0000.brres` model into Unity's `Assets`. The existing native BRRES model importer may be used as the rig source.
2. Select `0000.mot` in Project, open **Tools → MH → MOT Animation Workbench (Experimental)**. Press **Use Selected .mot From Project**, or drag the MOT asset into **MOT Source**.
3. The tool attempts to find a model with the same stem (`.brres`, `.mdl0`, `.glb` or `.fbx`). Alternatively drag a compatible model or scene GameObject into **Matching Model**.
4. **Read MOT / Refresh** discovers occupied GNTA slots. Select a slot (hexadecimal) and **Analyze Selected Slot** to report mapped, skipped and unknown bone tracks.
5. Start with **Basis: Flip X**, **Rotation Composition: Bind * Decoded**, **4D Axis-Angle Rotation: ON**, **4D Angles Are Radians: OFF**, **2D Rotation: SKIP**. These are experimental settings, not validated equivalence to the game.
6. Click **Build / Refresh Isolated Preview** and play/scrub. The preview animates a disposable model copy in Unity's private PreviewScene without modifying the active Scene or creating an Animator Controller.
7. **Export Selected To .anim** or **Export All Occupied Slots**. Clips are saved under `<MOT folder>/<name>_MOT_Animations/<name>_MOT_<hexslot>.anim`. Existing clips are skipped to avoid overwrite. Export All may skip slots with no supported/mapped transforms.

Optional **Bone Mapping CSV** supports `BoneID,BoneName` or `bone_id_dec,suggested_glb_bone`. Use the original [MOT_GLB_Bone_Mapping.csv](https://github.com/MusafirEx/Bress2Unity/tree/main/Experimental/MOT) if you have it, or supply your own mapping. The built-in mapping contains published Eighting/GNT IDs 0–21 plus tentative names for 51, 150 and 160; unknown IDs are never guessed.

## What has been improved

- **4D rotation:** samples with flags `0x0204`, track `0x0028`, behave like a near-normalized XYZ axis with W in **degrees**. We now validate axis magnitude and interpret via `Quaternion.AngleAxis(W, XYZ.normalized)` rather than the PEMT XYZW-quaternion decoder. Sentinel `[100,0,0,0]` is not treated as meaningful rotation. Quaternion interpolation and sign continuity are preserved.
- **2D rotation:** flags `0x0202`, track `0x0028`, **remain unsolved**. The previous assumption that the two floats are a unit sine/cosine pair is contradicted by the samples. Default **SKIP** is safer. X/Y/Z axis approximations are visually labeled experimental and cannot be considered faithful conversion.
- **Position:** 2D track planes XY/XZ/YZ are configurable. Default bind-relative position, flip X. Since position semantics have not been independently proven, model behavior must be checked in the preview.
- Rig bone names use the published Eighting IDs and matching Unity transform paths. Attachment bones and external mapping files are optional.

## Regression inputs and evidence

Locally supplied sample files were examined independently:

| Archive | Occupied clips | 4D rotation tracks | Valid unit-axis motion tracks | 4D sentinel-only tracks | 2D rotation tracks |
|---|---:|---:|---:|---:|---:|
| `0000.mot` | 55 | 46 | 44 | 2 | 1139 |
| `0010.mot` | 7 | 0 | 0 | 0 | 169 |
| `0030.mot` | 2 | 42 | 40 | 2 | 0 |

**Verified:** MOT header/slot offsets, GNTA track table and keyframe bounds, 4D unit-axis consistency. **NOT verified:** C# Unity compilation for this package; mesh deformation fidelity; full original engine interpolation; 2D rotation meaning; effect bone mappings. `0030.mot` is the first test candidate because its motion tracks use 4D axis-angle.

The original main Bress2Unity package has **no** MOT ScriptedImporter, and remains unchanged by this work.

## Source references

- [Original experimental decoder](https://github.com/MusafirEx/Bress2Unity/blob/main/Experimental/MOT/MotGntaUnityConverter.cs)
- [EMD2Unity PEMT exporter](https://github.com/MusafirEx/EMD2Unity/blob/main/Editor/PemtAnimationClipExporter.cs)
- [Naruto GNT MOT and GNTA format notes](https://github.com/NicholasMoser/Naruto-GNT-Modding/blob/main/gnt4/docs/file_formats/mot.md)
