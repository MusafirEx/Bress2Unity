// CHR0 to Unity AnimationClip converter for Unity 2022.3 LTS.
// Place under Assets/Editor. Menu: Tools > MH > CHR0 to Unity .anim
// Independent tool: does not require nintoolbox or glTFast at conversion time.
// glTFast 4+ converts coordinate handedness by flipping X (NOT Z).
// Intended for NW4R CHR0 v5. Supports I4/I6/I12/L1/L2/L4 scalar data.
// Reference: nintoolbox/project/src/lib-brres-model.c, CHR0 decoding logic.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class CHR0ToUnityAnimConverter : EditorWindow
{
    private GameObject modelRoot;
    private string inputDirectory = "";
    private string outputAssetFolder = "Assets/CHR0Animations";
    private int sampleRate = 60;
    private int axisConversion = 0; // 0 = glTFast 4+ flip X, 1 = legacy flip Z, 2 = none
    private bool useSourceLoop = true;
    private string status = "Select the instantiated em001.glb root from the Hierarchy, then choose the CHR0 folder.";
    private Vector2 scroll;

    [MenuItem("Tools/MH/CHR0 to Unity .anim")]
    private static void OpenWindow()
    {
        var window = GetWindow<CHR0ToUnityAnimConverter>("CHR0 → Unity .anim");
        window.minSize = new Vector2(540, 360);
        string example = Path.Combine(Application.dataPath, "CHR0UnityConverter/CHR0Files");
        if (Directory.Exists(example)) window.inputDirectory = example;
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Nintendo Wii CHR0 → Unity AnimationClip", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign the same model root that owns the Animator. The exporter builds Transform binding paths from its children, matching the GLB skeleton bone names.", MessageType.Info);
        modelRoot = (GameObject)EditorGUILayout.ObjectField("Model root (Hierarchy)", modelRoot, typeof(GameObject), true);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("CHR0 source folder");
        inputDirectory = EditorGUILayout.TextField(inputDirectory);
        if (GUILayout.Button("Browse...", GUILayout.Width(85)))
        {
            string dir = EditorUtility.OpenFolderPanel("Folder containing CHR0 animations", inputDirectory, "");
            if (!string.IsNullOrEmpty(dir)) inputDirectory = dir;
        }
        EditorGUILayout.EndHorizontal();
        outputAssetFolder = EditorGUILayout.TextField("Output in Assets", outputAssetFolder);
        sampleRate = EditorGUILayout.IntPopup("Playback FPS", sampleRate, new[] {"30 fps", "60 fps"}, new[] {30, 60});
        axisConversion = EditorGUILayout.Popup(new GUIContent("Coordinate conversion", "glTFast 4+ flips X; older glTFast versions flipped Z."), axisConversion, new[] { "Flip X (glTFast 4+, recommended)", "Flip Z (old glTFast)", "None (already in Unity space)" });
        useSourceLoop = EditorGUILayout.Toggle("Respect CHR0 loop flag", useSourceLoop);
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze CHR0", GUILayout.Height(30))) Run(false);
        GUI.enabled = modelRoot != null;
        if (GUILayout.Button("Generate .anim files", GUILayout.Height(30))) Run(true);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Run(bool export)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
        {
            EditorUtility.DisplayDialog("CHR0", "Choose a valid source folder containing e001_001 etc.", "OK");
            return;
        }
        Dictionary<string, Transform> targets = null;
        Dictionary<string, string> paths = null;
        if (modelRoot != null)
        {
            targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
            paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Transform child in modelRoot.transform) CollectTransforms(child, child.name, targets, paths);
            // Include modelRoot itself for CHR0 tracks that target its name.
            if (!targets.ContainsKey(modelRoot.name))
            {
                targets[modelRoot.name] = modelRoot.transform;
                paths[modelRoot.name] = "";
            }
        }
        if (export && targets == null)
        {
            EditorUtility.DisplayDialog("CHR0", "Select the model root in the Hierarchy first.", "OK");
            return;
        }
        var sourceFiles = new List<string>();
        foreach (var file in Directory.GetFiles(inputDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using (var fs = File.OpenRead(file))
                {
                    byte[] magic = new byte[4];
                    if (fs.Read(magic, 0, magic.Length) == 4 && magic[0] == (byte)'C' && magic[1] == (byte)'H' && magic[2] == (byte)'R' && magic[3] == (byte)'0')
                        sourceFiles.Add(file);
                }
            }
            catch (IOException) { /* ignore unreadable non-CHR0 files */ }
        }
        sourceFiles.Sort(StringComparer.OrdinalIgnoreCase);
        if (sourceFiles.Count == 0)
        {
            status = "No CHR0 files detected. The tool recognizes CHR0 binary magic, even when files have no extension.";
            return;
        }
        if (export && !EnsureAssetFolder(outputAssetFolder))
        {
            EditorUtility.DisplayDialog("CHR0", "Output path must be under Assets/, e.g. Assets/CHR0Animations", "OK");
            return;
        }
        string normalizedOutput = outputAssetFolder.Replace('\\', '/').TrimEnd('/');
        var results = new List<string>();
        int success = 0;
        try
        {
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                string path = sourceFiles[i];
                string name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.DisplayProgressBar(export ? "Exporting CHR0" : "Analyzing CHR0", name, (float)i / sourceFiles.Count);
                try
                {
                    Chr0Clip source = DecodeChr0(File.ReadAllBytes(path));
                    int mapped = 0;
                    foreach (var bone in source.Bones) if (targets != null && targets.ContainsKey(bone.Name)) mapped++;
                    if (export)
                    {
                        if (mapped == 0) throw new Exception("No matching Transform names in selected model");
                        AnimationClip clip = CreateClip(name, source, targets, paths, sampleRate, axisConversion, useSourceLoop);
                        string saveAs = AssetDatabase.GenerateUniqueAssetPath(normalizedOutput + "/" + name + ".anim");
                        AssetDatabase.CreateAsset(clip, saveAs);
                        results.Add(name + ": " + source.SourceFrames + " frames, " + source.Bones.Count + " source bones, " + mapped + " matched → " + saveAs);
                    }
                    else
                    {
                        results.Add(name + ": CHR0 v5, " + source.SourceFrames + " frames, loop=" + source.Loop + ", " + source.Bones.Count + " bones" + (targets == null ? "" : ", " + mapped + " matching"));
                    }
                    success++;
                }
                catch (Exception ex)
                {
                    results.Add("ERROR " + name + ": " + ex.Message);
                    Debug.LogError("CHR0 converter: " + name + " - " + ex);
                }
            }
            if (export)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        status = (export ? "Exported " : "Analyzed ") + success + "/" + sourceFiles.Count + " files\n\n" + string.Join("\n", results);
        Debug.Log("CHR0 converter:\n" + status);
    }

    private static void CollectTransforms(Transform t, string path, Dictionary<string, Transform> targets, Dictionary<string, string> paths)
    {
        // CHR0 identifies joints by name. Duplicate names cannot be resolved automatically.
        if (!targets.ContainsKey(t.name)) { targets.Add(t.name, t); paths.Add(t.name, path); }
        foreach (Transform child in t) CollectTransforms(child, path + "/" + child.name, targets, paths);
    }

    private static bool EnsureAssetFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return false;
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (assetPath != "Assets" && !assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return false;
        string[] parts = assetPath.Split('/');
        string folder = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || parts[i] == "." || parts[i] == "..") return false;
            string newFolder = folder + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(newFolder)) AssetDatabase.CreateFolder(folder, parts[i]);
            folder = newFolder;
        }
        return true;
    }

    // Reuse the tested CHR0 decoder for clips discovered inside BRRES archives.
    // The binary buffer may include the trailing BRRES shared string pool.
    // Do not copy only CHR0's declared size: string pointers can reach outside it.
    internal static string[] GetEmbeddedBoneNames(byte[] bytes)
    {
        Chr0Clip decoded = DecodeChr0(bytes);
        var names = new string[decoded.Bones.Count];
        for (int i = 0; i < names.Length; i++) names[i] = decoded.Bones[i].Name;
        return names;
    }

    internal static int CountEmbeddedBoneMatches(byte[] bytes, GameObject root)
    {
        if (root == null) return 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
        int matched = 0;
        foreach (string name in GetEmbeddedBoneNames(bytes)) if (names.Contains(name)) matched++;
        return matched;
    }

    internal static AnimationClip CreateEmbeddedAnimation(byte[] bytes, string name, GameObject root,
        int fps, int axis, bool respectLoop, out int matched)
    {
        Chr0Clip decoded = DecodeChr0(bytes);
        var targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        GameObject fallback = null;
        try
        {
            if (root != null)
            {
                foreach (Transform child in root.transform)
                    CollectTransforms(child, child.name, targets, paths);
                if (!targets.ContainsKey(root.name))
                {
                    targets[root.name] = root.transform;
                    paths[root.name] = "";
                }
            }
            else
            {
                // Animation-only BRRES: generic bone-name curves are viewable as sub-assets.
                // They are NOT guaranteed to match the full path hierarchy of a real model.
                fallback = new GameObject("__CHR0_Import_BindDefaults");
                foreach (Chr0Bone bone in decoded.Bones)
                {
                    targets[bone.Name] = fallback.transform;
                    paths[bone.Name] = bone.Name;
                }
            }
            matched = 0;
            foreach (Chr0Bone bone in decoded.Bones)
                if (targets.ContainsKey(bone.Name)) matched++;
            if (root != null && matched == 0)
                throw new InvalidDataException("No CHR0 bone names match the selected model: " + name);
            return CreateClip(name, decoded, targets, paths, fps, axis, respectLoop);
        }
        finally
        {
            if (fallback != null) UnityEngine.Object.DestroyImmediate(fallback);
        }
    }

    private static AnimationClip CreateClip(string name, Chr0Clip source,
        Dictionary<string, Transform> targets, Dictionary<string, string> paths,
        int fps, int axis, bool copyLoop)
    {
        var clip = new AnimationClip { name = name, legacy = false, frameRate = fps };
        int count = source.FrameCount;
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        for (int n = 0; n < source.Bones.Count; n++)
        {
            Chr0Bone bone = source.Bones[n];
            Transform transform;
            if (!targets.TryGetValue(bone.Name, out transform)) continue;
            string path = paths[bone.Name];
            Vector3 bindPos = transform.localPosition;
            if (axis == 0) bindPos.x = -bindPos.x; // Reverse glTFast 4+ reflection.
            if (axis == 1) bindPos.z = -bindPos.z; // Reverse legacy reflection.
            Vector3 bindScale = transform.localScale;
            Quaternion bindRot = transform.localRotation;
            if (axis == 0) bindRot = new Quaternion(bindRot.x, -bindRot.y, -bindRot.z, bindRot.w);
            if (axis == 1) bindRot = new Quaternion(-bindRot.x, -bindRot.y, bindRot.z, bindRot.w);
            Vector3 bindEuler = bindRot.eulerAngles;

            if (bone.HasChannels(6, 3))
            {
                float[] x = new float[count], y = new float[count], z = new float[count];
                for (int f = 0; f < count; f++)
                {
                    float nativeX = bone.Value(6, f, bindPos.x);
                    x[f] = axis == 0 ? -nativeX : nativeX;
                    y[f] = bone.Value(7, f, bindPos.y);
                    float nativeZ = bone.Value(8, f, bindPos.z);
                    z[f] = axis == 1 ? -nativeZ : nativeZ;
                }
                WriteCurve(bindings, curves, path, "m_LocalPosition.x", x, fps);
                WriteCurve(bindings, curves, path, "m_LocalPosition.y", y, fps);
                WriteCurve(bindings, curves, path, "m_LocalPosition.z", z, fps);
            }
            if (bone.HasChannels(0, 3))
            {
                float[] x = new float[count], y = new float[count], z = new float[count];
                for (int f = 0; f < count; f++)
                {
                    x[f] = bone.Value(0, f, bindScale.x);
                    y[f] = bone.Value(1, f, bindScale.y);
                    z[f] = bone.Value(2, f, bindScale.z);
                }
                WriteCurve(bindings, curves, path, "m_LocalScale.x", x, fps);
                WriteCurve(bindings, curves, path, "m_LocalScale.y", y, fps);
                WriteCurve(bindings, curves, path, "m_LocalScale.z", z, fps);
            }
            if (bone.HasChannels(3, 3))
            {
                float[] x = new float[count], y = new float[count], z = new float[count], w = new float[count];
                Quaternion prev = Quaternion.identity;
                for (int f = 0; f < count; f++)
                {
                    float rx = bone.Value(3, f, bindEuler.x);
                    float ry = bone.Value(4, f, bindEuler.y);
                    float rz = bone.Value(5, f, bindEuler.z);
                    Quaternion q = WiiEulerToQuaternion(rx, ry, rz);
                    if (axis == 0) q = new Quaternion(q.x, -q.y, -q.z, q.w);
                    if (axis == 1) q = new Quaternion(-q.x, -q.y, q.z, q.w);
                    if (f > 0 && Quaternion.Dot(prev, q) < 0f)
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    prev = q;
                    x[f] = q.x; y[f] = q.y; z[f] = q.z; w[f] = q.w;
                }
                WriteCurve(bindings, curves, path, "m_LocalRotation.x", x, fps);
                WriteCurve(bindings, curves, path, "m_LocalRotation.y", y, fps);
                WriteCurve(bindings, curves, path, "m_LocalRotation.z", z, fps);
                WriteCurve(bindings, curves, path, "m_LocalRotation.w", w, fps);
            }
        }
        // One batched Unity editor operation keeps large clips responsive.
        AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = copyLoop && source.Loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    // MDL0 exporter convention: Q = Qz * Qy * Qx, not Unity's Quaternion.Euler (ZXY).
    private static Quaternion WiiEulerToQuaternion(float rx, float ry, float rz)
    {
        float ax = rx * Mathf.Deg2Rad * 0.5f, ay = ry * Mathf.Deg2Rad * 0.5f, az = rz * Mathf.Deg2Rad * 0.5f;
        float sx = Mathf.Sin(ax), cx = Mathf.Cos(ax);
        float sy = Mathf.Sin(ay), cy = Mathf.Cos(ay);
        float sz = Mathf.Sin(az), cz = Mathf.Cos(az);
        return new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz);
    }

    private static void WriteCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, float[] samples, float fps)
    {
        if (samples == null || samples.Length == 0) return;
        // Collapse constant curves from hundreds of keys to just 1-2 keys.
        bool constant = true;
        float first = samples[0];
        for (int i = 1; i < samples.Length; i++)
            if (Mathf.Abs(samples[i] - first) > 0.000001f) { constant = false; break; }
        AnimationCurve curve;
        if (constant)
        {
            if (samples.Length == 1) curve = new AnimationCurve(new Keyframe(0f, first));
            else curve = new AnimationCurve(new Keyframe(0f, first), new Keyframe((samples.Length - 1) / fps, first));
        }
        else
        {
            var keys = new Keyframe[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float before = i == 0 ? (samples[1] - samples[0]) * fps : (samples[i] - samples[i - 1]) * fps;
                float after = i + 1 == samples.Length ? (samples[i] - samples[i - 1]) * fps : (samples[i + 1] - samples[i]) * fps;
                keys[i] = new Keyframe(i / fps, samples[i], before, after);
            }
            curve = new AnimationCurve(keys);
        }
        bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
        curves.Add(curve);
    }

    private sealed class Chr0Clip
    {
        public int SourceFrames;
        public bool Loop;
        public int FrameCount;
        public readonly List<Chr0Bone> Bones = new List<Chr0Bone>();
    }
    private sealed class Chr0Bone
    {
        public string Name;
        public readonly float[][] Channels = new float[9][];
        public bool HasChannels(int start, int length)
        {
            for (int i = start; i < start + length; i++) if (Channels[i] != null) return true;
            return false;
        }
        public float Value(int i, int frame, float fallback) { return Channels[i] == null ? fallback : Channels[i][frame]; }
    }

    private static Chr0Clip DecodeChr0(byte[] b)
    {
        if (b.Length < 0x40 || b[0] != 'C' || b[1] != 'H' || b[2] != 'R' || b[3] != '0') throw new InvalidDataException("Not a CHR0 binary");
        uint version = U32(b, 8);
        if (version != 5) throw new NotSupportedException("CHR0 version " + version + "; this tool supports version 5");
        int sourceFrames = U16(b, 0x20);
        bool loop = U32(b, 0x24) != 0;
        int frames = sourceFrames + (loop ? 1 : 0);
        if (frames < 1 || frames > 20000) throw new InvalidDataException("Invalid CHR0 frame count " + frames);
        int group = CheckedIndex(b, U32(b, 0x10), 8);
        uint entries = U32(b, group + 4);
        if (entries > 5000) throw new InvalidDataException("Too many bone entries");
        CheckedIndex(b, (long)group + 8, ((long)entries + 1) * 16);
        var result = new Chr0Clip { SourceFrames = sourceFrames, Loop = loop, FrameCount = frames };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i <= entries; i++)
        {
            int rec = group + 8 + i * 16;
            int entry = CheckedIndex(b, (long)group + S32(b, rec + 12), 8);
            string name = ReadName(b, group, rec + 8);
            if (string.IsNullOrEmpty(name)) throw new InvalidDataException("Missing bone name at record " + i);
            if (!seen.Add(name)) throw new InvalidDataException("Duplicate bone name: " + name);
            uint code = U32(b, entry + 4);
            int cursor = entry + 8;
            var bone = new Chr0Bone { Name = name };
            for (int groupId = 0; groupId < 3; groupId++)
            {
                if (((code >> (22 + groupId)) & 1) == 0) continue;
                bool isotropic = ((code >> (4 + groupId)) & 1) != 0;
                int rawFormat = groupId == 0 ? (int)((code >> 25) & 3) : groupId == 1 ? (int)((code >> 27) & 7) : (int)((code >> 30) & 3);
                int format = rawFormat == 0 ? (groupId == 1 ? -1 : 2) : rawFormat - 1;
                int baseIndex = groupId * 3;
                if (isotropic)
                {
                    bool fixedValue = ((code >> (13 + baseIndex + 2)) & 1) != 0;
                    float[] vals = ReadAxis(b, entry, ref cursor, fixedValue, format, frames, loop);
                    for (int axis = 0; axis < 3; axis++) bone.Channels[baseIndex + axis] = vals;
                }
                else
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        bool fixedValue = ((code >> (13 + baseIndex + axis)) & 1) != 0;
                        bone.Channels[baseIndex + axis] = ReadAxis(b, entry, ref cursor, fixedValue, format, frames, loop);
                    }
                }
            }
            result.Bones.Add(bone);
        }
        return result;
    }

    private static float[] ReadAxis(byte[] b, int entry, ref int cursor, bool constant, int format, int frames, bool loop)
    {
        CheckedIndex(b, cursor, 4);
        if (constant)
        {
            float v = F32(b, cursor);
            cursor += 4;
            var array = new float[frames];
            for (int i = 0; i < frames; i++) array[i] = v;
            return array;
        }
        uint relativeOffset = U32(b, cursor);
        cursor += 4;
        return DecodeAxisData(b, CheckedIndex(b, (long)entry + relativeOffset, 8), format, frames, loop);
    }

    private struct AnimKey { public int Frame; public float Value, Tangent; public AnimKey(int f, float v, float t) { Frame = f; Value = v; Tangent = t; } }
    private static float[] DecodeAxisData(byte[] b, int pos, int format, int frames, bool loop)
    {
        var samples = new float[frames];
        if (format == 5) // L4: dense big-endian floats, no header.
        {
            CheckedIndex(b, pos, 4L * frames);
            for (int i = 0; i < frames; i++) samples[i] = F32(b, pos + 4 * i);
        }
        else if (format == 4 || format == 3) // L2/L1 with step/base.
        {
            float step = F32(b, pos), basis = F32(b, pos + 4);
            int width = format == 3 ? 1 : 2;
            CheckedIndex(b, (long)pos + 8, (long)frames * width);
            for (int i = 0; i < frames; i++)
                samples[i] = basis + (width == 1 ? b[pos + 8 + i] : U16(b, pos + 8 + i * 2)) * step;
        }
        else if (format == 0 || format == 1 || format == 2) // Sparse keyframes, Hermite interpolation.
        {
            int keys = U16(b, pos);
            if (keys < 1 || keys > 20000) throw new InvalidDataException("Invalid sparse key count " + keys);
            int header = format == 2 ? 8 : 16;
            int width = format == 0 ? 4 : format == 1 ? 6 : 12;
            CheckedIndex(b, (long)pos + header, (long)keys * width);
            float step = format == 2 ? 0f : F32(b, pos + 8);
            float basis = format == 2 ? 0f : F32(b, pos + 12);
            var src = new AnimKey[keys];
            for (int i = 0; i < keys; i++)
            {
                int p = pos + header + i * width;
                if (format == 0)
                {
                    uint packed = U32(b, p);
                    int tangent12 = (int)(packed & 0xfff);
                    if (tangent12 >= 2048) tangent12 -= 4096;
                    src[i] = new AnimKey((int)((packed >> 24) & 0xff), basis + ((packed >> 12) & 0xfff) * step, tangent12 / 32f);
                }
                else if (format == 1)
                    src[i] = new AnimKey(U16(b, p) >> 5, basis + U16(b, p + 2) * step, S16(b, p + 4) / 256f);
                else
                    src[i] = new AnimKey((int)F32(b, p), F32(b, p + 4), F32(b, p + 8));
            }
            Hermite(samples, src, loop);
        }
        else throw new NotSupportedException("Invalid CHR0 channel format " + format);
        for (int i = 0; i < frames; i++)
            if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new InvalidDataException("Non-finite CHR0 animation sample");
        return samples;
    }

    // Matches BrawlLib/Nintendo cubic Hermite formula at integer frames.
    private static void Hermite(float[] values, AnimKey[] src, bool loop)
    {
        int count = values.Length;
        AnimKey wrap = new AnimKey(count - 1, src[0].Value, src[0].Tangent);
        bool hasWrap = loop && wrap.Frame > src[src.Length - 1].Frame;
        int k = 0;
        for (int f = 0; f < count; f++)
        {
            if (f <= src[0].Frame) { values[f] = src[0].Value; continue; }
            if (!hasWrap && f >= src[src.Length - 1].Frame) { values[f] = src[src.Length - 1].Value; continue; }
            if (hasWrap && f >= wrap.Frame) { values[f] = wrap.Value; continue; }
            while (k + 1 < src.Length && src[k + 1].Frame <= f) k++;
            AnimKey a = src[k], end = k + 1 < src.Length ? src[k + 1] : wrap;
            float span = end.Frame - a.Frame, off = f - a.Frame;
            if (off <= 0f || span <= 0f) { values[f] = a.Value; continue; }
            float t = off / span, inv = t - 1f;
            values[f] = a.Value + off * inv * (inv * a.Tangent + t * end.Tangent)
                + t * t * (3f - 2f * t) * (end.Value - a.Value);
        }
    }

    private static string ReadName(byte[] b, int group, int field)
    {
        int rel = S32(b, field);
        if (rel == 0) return "";
        int pos = CheckedIndex(b, (long)group + rel, 1);
        int prefix = CheckedIndex(b, (long)pos - 4, 4);
        uint len = U32(b, prefix);
        if (len == 0 || len > 255) throw new InvalidDataException("Invalid CHR0 string size");
        CheckedIndex(b, pos, len);
        for (int i = 0; i < len; i++) if (b[pos + i] < 32 || b[pos + i] > 126) throw new InvalidDataException("Invalid CHR0 bone name");
        return System.Text.Encoding.ASCII.GetString(b, pos, (int)len);
    }
    private static int CheckedIndex(byte[] b, long pos, long len)
    {
        if (pos < 0 || len < 0 || pos + len > b.Length || pos > int.MaxValue)
            throw new InvalidDataException("Bad CHR0 offset " + pos + " length " + len + " (size " + b.Length + ")");
        return (int)pos;
    }
    private static ushort U16(byte[] b, int i) { CheckedIndex(b, i, 2); return (ushort)((b[i] << 8) | b[i + 1]); }
    private static short S16(byte[] b, int i) { return unchecked((short)U16(b, i)); }
    private static uint U32(byte[] b, int i) { CheckedIndex(b, i, 4); return ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3]; }
    private static int S32(byte[] b, int i) { return unchecked((int)U32(b, i)); }
    private static float F32(byte[] b, int i)
    {
        CheckedIndex(b, i, 4);
        // Wii is big-endian; Unity desktop is little-endian.
        byte[] raw = new byte[] { b[i], b[i + 1], b[i + 2], b[i + 3] };
        if (BitConverter.IsLittleEndian) Array.Reverse(raw);
        return BitConverter.ToSingle(raw, 0);
    }
}
#endif