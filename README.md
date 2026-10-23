# CHR0 → Unity + Native BRRES/MDL0 Importer

**Unity 2022.3 LTS · Unity Package Manager (UPM) · v0.3.0-preview.1**

One Unity package with two Editor tools:

- **CHR0 → Unity .anim:** Tested CHR0 animation converter (position, rotation, named bone binding). Original converter code remains unchanged.
- **BRRES/MDL0 → Unity model asset:** New **experimental native importer** for Wii models, skeletons, meshes and supported TEX0 textures. No nintoolbox program and no intermediate GLB are needed for supported models.

> **Unity-tested milestone (23 October 2026):** The user has confirmed that the native BRRES importer and embedded/separate CHR0 workflows work in their Unity project. [Preserved working-code snapshot](https://github.com/MusafirEx/CHR0_2_Unity/tree/milestone/v0.3.0-preview.1-unity-tested). The importer remains a preview: uncommon Wii formats, precise TEV shading, and advanced skinning are not comprehensively validated. MOT/GNTA is not supported.

**Milestone:** [First Unity-tested release snapshot](https://github.com/MusafirEx/CHR0_2_Unity/tree/milestone/v0.3.0-preview.1-unity-tested) · `v0.3.0-preview.1` · 2026-10-23.

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

Only **CHR0 v5** is supported by the shared decoder. Other animation formats, including **MOT/GNTA, SRT0, PAT0 and VIS0**, are **not** converted by this feature. Animation-only files with no model cannot guarantee full bone paths without a rig.


## Install with Unity Package Manager

1. Install [Git](https://git-scm.com/download/win) and ensure it is available on PATH.
2. Unity → **Window → Package Manager** → **+ → Add package from git URL**.
3. Paste this package URL:

```text
https://github.com/MusafirEx/CHR0_2_Unity.git?path=/Packages/com.musafirex.chr0-to-unity
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
