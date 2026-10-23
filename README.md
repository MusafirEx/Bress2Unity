# CHR0 → Unity + Native BRRES/MDL0 Importer

**Unity 2022.3 LTS · Unity Package Manager (UPM) · v0.2.0-preview.1**

One Unity package with two Editor tools:

- **CHR0 → Unity .anim:** Tested CHR0 animation converter (position, rotation, named bone binding). Original converter code remains unchanged.
- **BRRES/MDL0 → Unity model asset:** New **experimental native importer** for Wii models, skeletons, meshes and supported TEX0 textures. No nintoolbox program and no intermediate GLB are needed for supported models.

> **Status:** BRRES/MDL0 importer has been validated with binary structure and reference triangle/texture data, but **has not yet been compiled and visually tested in Unity**. The BRRES importer and skinning are beta. MOT/GNTA conversion is not supported.

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
