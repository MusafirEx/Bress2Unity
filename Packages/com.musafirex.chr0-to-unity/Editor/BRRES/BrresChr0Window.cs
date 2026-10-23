// Import CHR0 animation resources directly from a Wii BRRES archive.
// Animation-only archives require the matching rig to generate correct hierarchy paths.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MusafirEx.Brres
{
    public sealed class BrresChr0Window : EditorWindow
    {
        private string archivePath = "";
        private GameObject rigRoot;
        private string outputDirectory = "Assets/CHR0Animations";
        private int fps = 60;
        private int conversionAxis = 0;
        private bool sourceLoop = true;
        private string report = "Choose an animation BRRES file and an instantiated model root from the Hierarchy.";
        private Vector2 scroll;

        [MenuItem("Tools/MH/CHR0 from BRRES...")]
        public static void Open()
        {
            var window = GetWindow<BrresChr0Window>("CHR0 from BRRES");
            window.minSize = new Vector2(580, 390);
            string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(selected) &&
                string.Equals(Path.GetExtension(selected), ".brres", StringComparison.OrdinalIgnoreCase))
                window.archivePath = selected;
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Extract CHR0 AnimationClips from BRRES", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Works with animation-only BRRES and model+animation BRRES. Choose the real model root to generate bone bindings matching its hierarchy.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            archivePath = EditorGUILayout.TextField("Source BRRES", archivePath);
            if (GUILayout.Button("Browse", GUILayout.Width(85)))
            {
                string selected = EditorUtility.OpenFilePanel("Choose Wii BRRES animation archive", "", "brres");
                if (!string.IsNullOrEmpty(selected)) archivePath = selected;
            }
            EditorGUILayout.EndHorizontal();
            rigRoot = (GameObject)EditorGUILayout.ObjectField("Model root (Hierarchy)", rigRoot, typeof(GameObject), true);
            outputDirectory = EditorGUILayout.TextField("Output in Assets", outputDirectory);
            fps = EditorGUILayout.IntPopup("Playback FPS", fps, new [] { "30 fps", "60 fps" }, new [] { 30, 60 });
            conversionAxis = EditorGUILayout.Popup("Coordinate conversion", conversionAxis,
                new [] { "Flip X (default native BRRES)", "Flip Z", "None" });
            sourceLoop = EditorGUILayout.Toggle("Respect CHR0 loop flag", sourceLoop);
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analyze BRRES", GUILayout.Height(32))) Process(false);
            EditorGUI.BeginDisabledGroup(rigRoot == null);
            if (GUILayout.Button("Generate .anim files", GUILayout.Height(32))) Process(true);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Process(bool export)
        {
            try
            {
                if (!File.Exists(archivePath)) throw new FileNotFoundException("BRRES source not found", archivePath);
                if (export && rigRoot == null) throw new InvalidOperationException("Select the target model from the Unity Hierarchy");
                byte[] input = File.ReadAllBytes(archivePath);
                if (input.Length > 512 * 1024 * 1024) throw new InvalidDataException("BRRES exceeds 512 MB safety limit");
                var reader = new BrresMdl0Reader(input, true, true, 1f);
                var resources = reader.FindResources();
                var clips = resources.FindAll(x => x.Magic == "CHR0");
                if (clips.Count == 0)
                {
                    report = "No CHR0 resources found in " + archivePath;
                    return;
                }
                string destination = outputDirectory.Replace('\\', '/').TrimEnd('/');
                if (export && !EnsureOutput(destination))
                    throw new InvalidDataException("Output folder must start with Assets/");
                var lines = new List<string>();
                int succeeded = 0;
                try
                {
                    foreach (var r in clips)
                    {
                        EditorUtility.DisplayProgressBar("CHR0 in BRRES", r.Name, (float)succeeded / clips.Count);
                        try
                        {
                            byte[] raw = reader.ReadChr0WithSharedStrings(r);
                            string[] bones = CHR0ToUnityAnimConverter.GetEmbeddedBoneNames(raw);
                            int matched = CHR0ToUnityAnimConverter.CountEmbeddedBoneMatches(raw, rigRoot);
                            string name = string.IsNullOrWhiteSpace(r.Name) ? "CHR0_" + r.Offset.ToString("X") : r.Name;
                            if (export)
                            {
                                int actual;
                                AnimationClip clip = CHR0ToUnityAnimConverter.CreateEmbeddedAnimation(raw, name, rigRoot, fps, conversionAxis, sourceLoop, out actual);
                                string path = AssetDatabase.GenerateUniqueAssetPath(destination + "/" + FileSafe(name) + ".anim");
                                AssetDatabase.CreateAsset(clip, path);
                                lines.Add(name + ": " + actual + "/" + bones.Length + " bones mapped -> " + path);
                            }
                            else lines.Add(name + ": " + bones.Length + " CHR0 bones" + (rigRoot == null ? "" : (", " + matched + " matched")));
                            succeeded++;
                        }
                        catch (Exception ex)
                        {
                            lines.Add("ERROR " + r.Name + ": " + ex.Message);
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    if (export) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
                }
                report = (export ? "Exported " : "Analyzed ") + succeeded + "/" + clips.Count + " CHR0 resources from " + archivePath + "\n\n" + string.Join("\n", lines);
                Debug.Log("CHR0 from BRRES: " + report);
            }
            catch(Exception ex)
            {
                report = "ERROR: " + ex.Message;
                Debug.LogError("CHR0 from BRRES: " + ex);
            }
        }

        private static string FileSafe(string input)
        {
            char[] chars = input.ToCharArray();
            foreach (char c in Path.GetInvalidFileNameChars())
                for (int i = 0; i < chars.Length; i++) if (chars[i] == c) chars[i] = '_';
            return new string(chars);
        }
        private static bool EnsureOutput(string path)
        {
            if (path == "Assets") return true;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            string[] parts = path.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) return false;
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return true;
        }
    }
}
#endif