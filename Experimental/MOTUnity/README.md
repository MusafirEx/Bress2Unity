# MOT / GNTA Workbench — standalone experimental Unity package

**Version:** 0.1.1-preview.1 · Unity 2022.3 LTS · Editor-only · **not the Bress2Unity BRRES/CHR0 UPM package**.

This is a separate, **opt-in experimental tool**, following the same research workflow as the `.pemt` converter in [EMD2Unity](https://github.com/MusafirEx/EMD2Unity). It reuses the archived MOT/GNTA parser without bringing the old `.mot` ScriptedImporter (or its `MotArchiveInfo` failure) back into the Bress2Unity main package. Original `.mot` files remain ordinary Unity Project assets; no automatic MOT import occurs.

## Install (separately from Bress2Unity)

Unity Package Manager → **Add package from Git URL**:

```
https://github.com/MusafirEx/Bress2Unity.git?path=/Experimental/MOTUnity
```

The BRRES/CHR0 package remains independently installable at `?path=/Packages/com.musafirex.chr0-to-unity`. Both packages have different package identifiers and assembly names.

## Fix for only one bone moving (v0.1.1-preview.1)

**Root cause verified on the uploaded `0000.mot`:** animation slot `0x0000` includes 23 rotation tracks encoded as `0x0202`, but the original Workbench used `2D Rotation: SKIP`. Consequently it generated only the remaining position curves, usually on the `X` root bone. This is an export-setting problem, not evidence that the MOT file lacks limb animations.

**Changes:** On first loading this version, existing Workbench windows migrate to `2D Rotation: Preview: atan2 pair, axis X`. This is an **experimental reconstruction, not verified game decoding**. The Workbench also disables inherited `Animator`/`Animation` components on the disposable preview clone (without modifying original BRRES prefabs), and reports both input transform-track counts and actual Unity curve bindings with their target bone names. Use the alternate Y/Z or `acos X / signed Y` trial modes to compare responses. The `SKIP` mode remains available for strict debugging.

On the supplied `0000.mot`, slot `0x0000` has **23 rotation-2D tracks**. Of those, **20 match the known numeric IDs 0–21** in the current mapping. With the X-axis test decoder, **16 named bones have more than 0.1° of sampled angle variation** in the source; actual Unity movement still depends on hierarchy matching and model skin weights. Slot `0x000A` is a better large-motion visual test than the subtle idle-like slot `0x0000`. The original `0030.mot` relies instead on primarily 4-component rotation tracks and is useful for separately validating axis-angle motion.

**Retest:**
1. Update **only the separate MOT Workbench** package to `0.1.1-preview.1`.
2. Open **Tools → MH → MOT Animation Workbench (Experimental)**. Select the real `0000.mot` and the correct `0000.brres` model.
3. **Read MOT / Refresh** → select `0x000A` → **2D Rotation (experimental): Preview: atan2 pair, axis X**.
4. **Build / Refresh Isolated Preview** → click **Play**. Confirm the diagnostic footer reports multiple **ANIMATED BONES**, rather than one.
5. Try `0x0000` and alternate 2D modes to compare against a reference game video. When the result looks right, export a single `.anim` for controlled inspection. **Do not treat the experimental animation as a faithful recreation yet.**

**Limitations:** The true meaning of `0x0202` rotation values has not been determined. The current modes demonstrate motion but can produce incorrect angles or deformation. This patch has passed source-level checks; Unity compilation and live scene playback are not yet verified.

## Workflow

1. Put `0000.mot` and a matching `0000.brres` model into Unity's `Assets`. The existing native BRRES model importer may be used as the rig source.
2. Select `0000.mot` in Project, open **Tools → MH → MOT Animation Workbench (Experimental)**. Press **Use Selected .mot From Project**, or drag the MOT asset into **MOT Source**.
3. The tool attempts to find a model with the same stem (`.brres`, `.mdl0`, `.glb` or `.fbx`). Alternatively drag a compatible model or scene GameObject into **Matching Model**.
4. **Read MOT / Refresh** discovers occupied GNTA slots. Select a slot (hexadecimal) and **Analyze Selected Slot** to report mapped, skipped and unknown bone tracks.
5. Start with **Basis: Flip X**, **Rotation Composition: Bind * Decoded**, **4D Axis-Angle Rotation: ON**, **4D Angles Are Radians: OFF**, **2D Rotation: Preview: atan2 pair, axis X** for testing the multi-bone behavior (or **SKIP** for safe position-only diagnostics). These are experimental settings, not validated equivalence to the game.
6. Click **Build / Refresh Isolated Preview** and play/scrub. The preview animates a disposable model copy in Unity's private PreviewScene without modifying the active Scene or creating an Animator Controller.
7. **Export Selected To .anim** or **Export All Occupied Slots**. Clips are saved under `<MOT folder>/<name>_MOT_Animations/<name>_MOT_<hexslot>.anim`. Existing clips are skipped to avoid overwrite. Export All may skip slots with no supported/mapped transforms.

Optional **Bone Mapping CSV** supports `BoneID,BoneName` or `bone_id_dec,suggested_glb_bone`. Use the original [MOT_GLB_Bone_Mapping.csv](https://github.com/MusafirEx/Bress2Unity/tree/main/Experimental/MOT) if you have it, or supply your own mapping. The built-in mapping contains published Eighting/GNT IDs 0–21 plus tentative names for 51, 150 and 160; unknown IDs are never guessed.

## What has been improved

- **4D rotation:** samples with flags `0x0204`, track `0x0028`, behave like a near-normalized XYZ axis with W in **degrees**. We now validate axis magnitude and interpret via `Quaternion.AngleAxis(W, XYZ.normalized)` rather than the PEMT XYZW-quaternion decoder. Sentinel `[100,0,0,0]` is not treated as meaningful rotation. Quaternion interpolation and sign continuity are preserved.
- **2D rotation:** flags `0x0202`, track `0x0028`, **remain unsolved**. The original SKIP default resulted in only position/root motion on common `0000.mot` slots. Workbench v0.1.1 now defaults to an explicitly experimental X-axis preview, offers Y/Z and acos-X trials, and retains SKIP for safe diagnostics. These approximations cannot be considered faithful conversion.
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
