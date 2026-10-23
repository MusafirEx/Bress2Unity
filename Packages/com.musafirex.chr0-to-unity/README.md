# CHR0 to Unity Animation Converter (UPM)

**Unity 2022.3 LTS+ | Editor-only | Nintendo Wii NW4R CHR0 | MIT**

Converts extracted **CHR0 animation files** into native Unity `.anim` AnimationClips for an existing rigged character. The converter matches bones by **Transform name**, writes animation curves, and does not require nintoolbox to run once CHR0 files have been extracted.

## Install from Git (Unity Package Manager)

1. Install [Git for Windows](https://git-scm.com/download/win) and ensure `git` is on PATH.
2. In Unity, open **Window > Package Manager**.
3. Select **+ > Add package from git URL...** (wording can differ slightly by Unity version).
4. Paste the URL below, then click **Add**:

```text
https://github.com/MusafirEx/CHR0_2_Unity.git?path=/Packages/com.musafirex.chr0-to-unity
```

5. Open **Tools > MH > CHR0 to Unity .anim**.

**Important:** If you previously copied `CHR0ToUnityAnimConverter.cs` to your project's `Assets/` folder, **remove that older copy** before installing this Git package. Otherwise Unity can report a duplicate class or menu command.

## Usage

1. Obtain a rigged model with a bone hierarchy, e.g. convert a Wii BRRES/MDL0 model to GLB using [nintoolbox](https://github.com/quatric/nintoolbox) (workflow below).
2. Import the GLB into Unity with a compatible glTF importer (for example glTFast) and instantiate it in the **Hierarchy**.
3. Extract the matching Wii **CHR0** skeletal animation files separately.
4. Open the converter through **Tools > MH > CHR0 to Unity .anim**.
5. Set **Model root (Hierarchy)** to the GameObject that owns the intended animation hierarchy.
6. Set **CHR0 source folder** to the directory containing the extracted animations. Extensions are optional: the converter checks the binary **CHR0** signature.
7. Choose **Playback FPS** (30 or 60) and **Coordinate conversion** (Flip X, Flip Z, or None) for your GLB importer.
8. Click **Analyze CHR0**, then **Generate .anim files**. Default output: `Assets/CHR0Animations`.
9. Assign the generated AnimationClips to an Animator Controller.

The converter supports CHR0 v5, I4/I6/I12/L1/L2/L4 tracks, position, rotation, scale and source loop flags. Results depend on matching bone names, hierarchy and coordinate system. Check the animation visually in the Unity Editor.

## BRRES > nintoolbox > GLB workflow

**Model/skeleton pipeline:**

```text
Nintendo Wii BRRES or MDL0 model
            |
            v
nintoolbox / wmdlt DECODE
            |
            v
         model.glb
            |
            v
Unity glTF importer (e.g. glTFast)
            |
            v
Rigged model in Unity Hierarchy
```

nintoolbox model-decode command (if the BRRES can be decoded directly):

```powershell
wmdlt DECODE character.brres --dest character.glb
```

Alternatively, for a separately extracted model:

```powershell
wmdlt DECODE character.mdl0 --dest character.glb
```

If an archive contains multiple models or the direct BRRES conversion fails, extract its contents with nintoolbox/wszst and convert the required **MDL0** separately. See the [nintoolbox command reference](https://github.com/quatric/nintoolbox/blob/master/docs/COMMANDS.md).

**Animation pipeline (separate from GLB model conversion):**

```text
Extracted CHR0 skeletal animation(s)
            |
            v
This Unity CHR0 converter
            |
            v
       clip.anim
            |
            v
Unity Animator + matching GLB skeleton
```

> **The GLB model does not need to contain animations.** nintoolbox handles the model's BRRES/MDL0 → GLB step; this package handles **CHR0 → Unity .anim**. The package does **not** itself parse BRRES/MDL0, export GLB, or convert CHR0 directly to animated GLB.

## Supported / unsupported

- Supported: NW4R CHR0 v5; files with or without a `.chr0` extension; batch conversion from one directory.
- Supported: bones matched by name; position/rotation/scale curves; CHR0 loop flags; 30/60 FPS.
- Not supported: **MOT/GNTA** containers; the previous MOT experiment was unsuccessful and is excluded from the UPM package.
- No character models, BRRES files, copyrighted game assets or bundled example animations are included.

## Troubleshooting

- **No CHR0 files detected:** Ensure the data begins with the ASCII signature `CHR0`; changing a filename extension cannot convert an unrelated format.
- **No matching Transform names:** Select the correct root and check bone names in the imported GLB hierarchy.
- **Motion is mirrored/wrong:** Change the coordinate conversion option, then regenerate a test clip.
- **Duplicate class/menu item:** Remove any older standalone `Assets/**/CHR0ToUnityAnimConverter.cs` copy.
- **Package download fails:** Confirm that the Git client is installed and discoverable by Unity on PATH.

Source repository: [MusafirEx/CHR0_2_Unity](https://github.com/MusafirEx/CHR0_2_Unity).

This package is licensed under the MIT License. See [LICENSE.md](LICENSE.md).
