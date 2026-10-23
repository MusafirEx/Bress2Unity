# CHR0 → Unity Animation Converter

Unity Editor tool for converting Nintendo Wii NW4R **CHR0 skeletal animations** into native Unity `.anim` AnimationClip assets.

**Status:** The original CHR0 converter has been tested in Unity with working bone position and rotation animation. The experimental MOT/GNTA implementation was not successful and is **archived, not supported**.

## Requirements

- Unity 2022.3 LTS (Editor; Windows recommended).
- A rigged model with a bone hierarchy whose bone names match the CHR0 animation tracks.
- CHR0 animation files. File extensions are optional: the tool identifies files using the `CHR0` binary signature.
- A GLB importer such as glTFast is useful if your model is in `.glb` format, but is **not required** to decode CHR0.

## Installation

Copy the folder `Assets/CHR0UnityConverter/Editor/` into the matching location in your Unity project's `Assets` directory.

Do **not** install `Experimental/MOT/` inside Unity's `Assets` folder: that code is preserved for historical reference and has not passed practical testing.

Remove older duplicate copies of `CHR0ToUnityAnimConverter.cs` before importing.

## Usage

1. Import your model and place an instance of its hierarchy in the Unity **Hierarchy**.
2. Open **Tools → MH → CHR0 to Unity .anim**.
3. Assign the **Model root (Hierarchy)** GameObject.
4. Select the directory containing extracted CHR0 animations.
5. Choose an output folder under `Assets/`, e.g. `Assets/CHR0Animations`.
6. Choose playback FPS (30 or 60), the coordinate convention (Flip X / Flip Z / None), and whether to honor the CHR0 loop flag.
7. Click **Analyze CHR0**, then **Generate .anim files**.
8. Use the generated AnimationClips in an Animator Controller or the Unity Animation window.

### Supported CHR0 features

- NW4R CHR0 version 5
- Scale, rotation and translation tracks
- I4, I6, I12, L1, L2 and L4 data encoding
- Interpolation of sparse keys
- Transform binding by bone name/hierarchy path
- Automatic detection of CHR0 files **without file extensions**

The tool generates `.anim` clips; it does not import meshes or material resources.

## Repository layout

```text
Assets/
  CHR0UnityConverter/
    Editor/
      CHR0ToUnityAnimConverter.cs    # Supported CHR0 Editor window
Experimental/
  MOT/
    MotGntaUnityConverter.cs        # Archived experiment; not supported
README.md
LICENSE
.gitignore
```

## Notes

- Choose the model root carefully: binding paths are relative to the chosen GameObject.
- FPS and coordinate conversion must match the rig/importer used in Unity.
- This repository intentionally **does not redistribute game model or animation files**.
- The MOT/GNTA code is included only because it was part of the earlier project. It should not be presented as a working converter.

## License

See [LICENSE](LICENSE).
