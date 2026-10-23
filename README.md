# CHR0 → Unity Animation Converter

A **Unity Package Manager (UPM)** package for importing Nintendo Wii **NW4R CHR0** skeletal animations as native Unity `.anim` AnimationClips.

**Unity 2022.3 LTS** · Editor tool · MIT License

> **Project status:** The CHR0 converter has been successfully used for bone position and rotation animation in Unity. The separate MOT/GNTA experiment did not work and is **not part of this package**.

## Install in Unity (recommended)

1. Make sure **Git** is installed and accessible on your Windows PATH.
2. In Unity, open **Window → Package Manager**.
3. Click **+ → Add package from git URL...**.
4. Paste:

```text
https://github.com/MusafirEx/CHR0_2_Unity.git?path=/Packages/com.musafirex.chr0-to-unity
```

5. Click **Add**. After installation, open **Tools → MH → CHR0 to Unity .anim**.

You can also add this entry to your Unity project's `Packages/manifest.json` dependencies:

```json
{
  "dependencies": {
    "com.musafirex.chr0-to-unity": "https://github.com/MusafirEx/CHR0_2_Unity.git?path=/Packages/com.musafirex.chr0-to-unity"
  }
}
```

This JSON is an **example entry**: merge it into an existing `dependencies` object rather than overwriting your project's manifest.

**Existing users:** Remove your old `Assets/**/CHR0ToUnityAnimConverter.cs` copy before installing the Git package. Otherwise Unity may find duplicate class definitions. Your existing generated `.anim` assets do not need to be removed.

## BRRES → nintoolbox → GLB → Unity

The complete workflow has **two independent input paths**: the character model and the animation files.

```text
MODEL:
Nintendo Wii BRRES / MDL0
       │
       ▼
nintoolbox / wmdlt DECODE
       │
       ▼
    model.glb
       │
       ▼
Unity + glTF importer (e.g. glTFast)
       │
       ▼
Rigged model with named bones
       │
       │
ANIMATION:                       │
CHR0 binary files                │
       │                         │
       ▼                         │
CHR0 → Unity .anim converter     │
       │                         │
       ▼                         ▼
    clip.anim ──────────────► Unity Animator
```

### 1. Convert your BRRES/MDL0 model into a GLB

Download/build [nintoolbox (Wiimms SZS Tools Plus)](https://github.com/quatric/nintoolbox) and use its `wmdlt` model tool.

For a BRRES file that nintoolbox can decode directly:

```powershell
wmdlt DECODE character.brres --dest character.glb
```

For a model previously extracted as MDL0:

```powershell
wmdlt DECODE character.mdl0 --dest character.glb
```

If BRRES contains several models or direct conversion does not work, extract the BRRES contents and pass the desired MDL0 to `wmdlt`. More information: [nintoolbox command reference](https://github.com/quatric/nintoolbox/blob/master/docs/COMMANDS.md).

Import the resulting GLB into Unity with a compatible glTF importer (such as **glTFast**) and place an instance in your scene hierarchy. **nintoolbox is an external project and is not bundled with this package.**

### 2. Convert CHR0 animation files into Unity AnimationClips

CHR0 animations must be extracted separately from the relevant animation resources. They can be named `e001_001`, `e001_001.chr0`, etc.; this converter checks their **CHR0 binary signature**, not their extensions.

1. Open **Tools → MH → CHR0 to Unity .anim**.
2. Assign the imported model's root GameObject from the **Hierarchy**.
3. Browse to the folder containing CHR0 animations.
4. Select FPS (**30 / 60**), coordinate option (**Flip X / Flip Z / None**), and whether to respect the CHR0 loop flag.
5. Click **Analyze CHR0** to inspect the available animations and matched bones.
6. Click **Generate .anim files** (default location: `Assets/CHR0Animations`).
7. Add the clips to an **Animator Controller**.

**Important:** The GLB conversion provides the model and skeleton, whereas CHR0 conversion provides animation curves. This package **does not** perform BRRES → GLB conversion, and it does **not** convert CHR0 → animated GLB.

## Features

- Nintendo Wii NW4R **CHR0 v5** parsing, with no specific filename extension required.
- Keyframe decoding: **I4, I6, I12, L1, L2, L4**.
- Position, rotation (Unity quaternion curves) and scale animation.
- Name-based hierarchy binding, playback FPS and coordinate-convention options.
- Loop flag import and batch conversion of one directory to native `.anim` assets.

## Repository structure

```text
Packages/
└── com.musafirex.chr0-to-unity/  # Install this folder through Unity UPM
    ├── package.json
    ├── README.md
    ├── CHANGELOG.md
    ├── LICENSE.md
    └── Editor/
        ├── CHR0ToUnityAnimConverter.cs
        └── MusafirEx.CHR0ToUnity.Editor.asmdef
Experimental/
└── MOT/
    └── MotGntaUnityConverter.cs  # Archived prototype; do not import into Unity
```

The UPM Git URL installs only the package folder and excludes `Experimental/MOT/`.

## Compatibility and limitations

- Tested workflow: **Unity 2022.3 LTS** with imported GLB skeletons.
- Requires an existing model/bone hierarchy in the Unity scene; the converter only creates `.anim` files.
- Exported animation must be checked for correct bone names, hierarchy, rotations, FPS, and coordinate system.
- **MOT/GNTA is not supported.** The previous attempt is archived, not included in UPM.
- No ripped/copyrighted game resources are included.

## Credits

- Package maintained by **[Musafir Ex](https://github.com/MusafirEx)**.
- The CHR0 decoding implementation was informed by open Wii animation-format research and [nintoolbox](https://github.com/quatric/nintoolbox).
- nintoolbox and glTFast are external, independent tools.

## License

[MIT License](LICENSE). See the [package-specific README](Packages/com.musafirex.chr0-to-unity/README.md) for troubleshooting.
