// CHR0 playback and binding diagnostics for imported BRRES models.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MusafirEx.Brres
{
    public sealed class BrresChr0PlaybackWindow : EditorWindow
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField] private GameObject model;
        [SerializeField] private float time;
        private bool ownsPreview;
        private Vector2 scroll;
        private string report = "";

        [MenuItem("Tools/MH/CHR0 Playback & Binding Audit...")]
        private static void Open()
        {
            var w = GetWindow<BrresChr0PlaybackWindow>("CHR0 Playback Audit");
            if (Selection.activeObject is AnimationClip)
                w.clip = (AnimationClip)Selection.activeObject;
            w.FindModel();
            w.Audit();
        }

        private static Animator ResolveAnimator(GameObject target)
        {
            if (target == null) return null;
            Animator direct = target.GetComponent<Animator>();
            if (direct != null) return direct;
            Animator[] nested = target.GetComponentsInChildren<Animator>(true);
            return nested.Length == 1 ? nested[0] : null;
        }

        private static bool Matches(Animator animator, EditorCurveBinding binding)
        {
            return animator != null && binding.type == typeof(Transform) &&
                (string.IsNullOrEmpty(binding.path) ||
                 animator.transform.Find(binding.path) != null);
        }

        private void FindModel()
        {
            if (clip == null) return;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            string folder = Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(clip)));
            int bestScore = -1;
            Animator best = null;
            foreach (Animator candidate in Resources.FindObjectsOfTypeAll<Animator>())
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded) continue;
                int score = 0;
                foreach (EditorCurveBinding b in bindings) if (Matches(candidate, b)) score++;
                if (string.Equals(candidate.name, folder, StringComparison.OrdinalIgnoreCase))
                    score += 1000;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            if (best != null) model = best.gameObject;
        }

        private void OnSelectionChange()
        {
            AnimationClip selected = Selection.activeObject as AnimationClip;
            if (selected == null || selected == clip) return;
            StopPreview();
            clip = selected;
            FindModel();
            Audit();
            Repaint();
        }

        private void OnDisable() { StopPreview(); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Use the AnimationClip from mot_em028 / mot_em029 and the inner model Animator in the Scene. This window does not edit BRRES motion data.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            clip = (AnimationClip)EditorGUILayout.ObjectField("CHR0 Clip", clip, typeof(AnimationClip), false);
            model = (GameObject)EditorGUILayout.ObjectField("Scene Model", model, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) { StopPreview(); Audit(); }
            if (GUILayout.Button("Find Matching Scene Animator")) { StopPreview(); FindModel(); Audit(); }
            if (GUILayout.Button("Audit Bone Bindings")) Audit();

            Animator animator = ResolveAnimator(model);
            if (clip != null && animator != null)
            {
                if (animator.runtimeAnimatorController == null)
                    EditorGUILayout.HelpBox("No Animator Controller is assigned. The separate CHR0 clip will not play automatically in Play Mode.", MessageType.Warning);
                time = EditorGUILayout.Slider("Sample Time", time, 0f, Mathf.Max(0.001f, clip.length));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview Sample")) Sample(animator);
                    if (GUILayout.Button("Stop Preview")) StopPreview();
                }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Create + Assign Test Animator Controller"))
                        CreateController(animator);
                }
            }
            else EditorGUILayout.HelpBox("Assign a clip and a Scene model containing one Animator.", MessageType.Warning);

            EditorGUILayout.LabelField("Binding Audit", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report ?? "", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Audit()
        {
            if (clip == null) { report = "Select a CHR0 AnimationClip."; return; }
            Animator animator = ResolveAnimator(model);
            var sb = new StringBuilder();
            sb.AppendLine("Clip: " + clip.name);
            sb.AppendLine("Source: " + AssetDatabase.GetAssetPath(clip));
            sb.AppendLine("Length: " + clip.length.ToString("F3") + "s");
            sb.AppendLine("Animator: " + (animator == null ? "(none)" : animator.name));
            if (animator != null)
            {
                sb.AppendLine("Avatar valid: " + (animator.avatar != null && animator.avatar.isValid));
                sb.AppendLine("Controller: " + (animator.runtimeAnimatorController == null ?
                    "(none)" : animator.runtimeAnimatorController.name));
            }
            int matched = 0, unmatched = 0, varying = 0;
            var missingPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform)) continue;
                if (Matches(animator, binding)) matched++;
                else { unmatched++; missingPaths.Add(binding.path); }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;
                float first = curve.keys[0].value;
                for (int k = 1; k < curve.length; k++)
                    if (Mathf.Abs(curve.keys[k].value - first) > 0.00001f) { varying++; break; }
            }
            sb.AppendLine("Matching transform curves: " + matched);
            sb.AppendLine("Unmatched transform curves: " + unmatched);
            sb.AppendLine("Changing transform curves: " + varying);
            int shown = 0;
            foreach (string path in missingPaths)
            {
                if (shown++ >= 20) break;
                sb.AppendLine("Missing path: " + (string.IsNullOrEmpty(path) ? "<root>" : path));
            }
            sb.AppendLine(matched == 0 ? "RESULT: No usable bone bindings." :
                unmatched > 0 ? "RESULT: Some curve paths do not match." :
                varying == 0 ? "RESULT: All curve values appear constant." :
                "RESULT: Paths match; preview or assign a Controller to verify movement.");
            report = sb.ToString();
            Debug.Log("[Bress2Unity CHR0 Audit]\n" + report);
        }

        private void Sample(Animator animator)
        {
            if (AnimationMode.InAnimationMode() && !ownsPreview)
            {
                EditorUtility.DisplayDialog("CHR0 Preview", "Stop the other active Unity Animation preview first.", "OK");
                return;
            }
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                ownsPreview = true;
            }
            AnimationMode.BeginSampling();
            try { AnimationMode.SampleAnimationClip(animator.gameObject, clip, Mathf.Clamp(time, 0f, clip.length)); }
            finally { AnimationMode.EndSampling(); }
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            if (!ownsPreview) return;
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            ownsPreview = false;
            SceneView.RepaintAll();
        }

        private void CreateController(Animator animator)
        {
            StopPreview();
            const string dir = "Assets/CHR0AnimationTests";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "CHR0AnimationTests");
            string safe = Regex.Replace(clip.name, @"[^a-zA-Z0-9_-]", "_");
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + safe + "_Test.controller");
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState state = stateMachine.AddState(clip.name);
            state.motion = clip;
            stateMachine.defaultState = state;
            Undo.RecordObject(animator, "Assign CHR0 Test Animator Controller");
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
            Audit();
            Debug.Log("[Bress2Unity CHR0 Audit] Test controller assigned to " + animator.name +
                " (Scene child Animator). Controller: " + path + ". Enter Play Mode to test.");
        }
    }
}
#endif
