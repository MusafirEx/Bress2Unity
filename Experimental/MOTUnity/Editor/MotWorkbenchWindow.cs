// Standalone GNTA/MOT workbench: no ScriptedImporter, no dependency on Bress2Unity UPM.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MusafirEx.MotWorkbench
{
    public sealed class MotWorkbenchWindow : EditorWindow
    {
        [SerializeField] private UnityEngine.Object motSource;
        [SerializeField] private GameObject referenceRig;
        [SerializeField] private TextAsset mappingCsv;
        [SerializeField] private int slotIndex;
        [SerializeField] private int axis;
        [SerializeField] private int rotationOrder = 1;
        [SerializeField] private int positionPlane;
        [SerializeField] private float positionScale = 1f;
        [SerializeField] private bool relativePosition = true;
        [SerializeField] private bool enable4DRotation = true;
        [SerializeField] private bool radians4D;
        [SerializeField] private int experimental2D;
        [SerializeField] private bool includeAttachments = true;
        [SerializeField] private int samplingFps = 60;
        [SerializeField] private bool loop = true;
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private float previewTime;
        private readonly List<MotGntaUnityConverter.ClipUnit> clips = new List<MotGntaUnityConverter.ClipUnit>();
        private string loadedPath;
        private string diagnostics = "Select a .mot in Project and a matching BRRES/GLB model to begin.";
        private PreviewRenderUtility preview;
        private GameObject previewCopy;
        private AnimationClip temporaryClip;
        private Bounds modelBounds;
        private float duration;
        private float yaw = -25f, pitch = 15f, zoom = 1.35f;
        private bool playing;
        private double lastEditorTick;
        private Vector2 scroll;

        [MenuItem("Tools/MH/MOT Animation Workbench (Experimental)")]
        public static void ShowWindow()
        {
            var w = GetWindow<MotWorkbenchWindow>("MOT Workbench");
            w.minSize = new Vector2(470f, 660f);
            w.AcceptSelected();
            w.Show();
        }

        private void OnEnable()
        {
            lastEditorTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorTick;
        }
        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
            DiscardPreview();
        }
        private void EditorTick()
        {
            double now=EditorApplication.timeSinceStartup;
            float delta=(float)Math.Max(0d,Math.Min(0.12d,now-lastEditorTick));
            lastEditorTick=now;
            if(!playing || temporaryClip==null || previewCopy==null)return;
            previewTime+=delta*playbackSpeed;
            if(previewTime>=duration)
            {
                if(loop && duration>0f)previewTime%=duration;
                else { previewTime=duration; playing=false; }
            }
            Sample();
            Repaint();
        }

        private static bool IsMot(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".mot",StringComparison.OrdinalIgnoreCase);

        private void AcceptSelected()
        {
            string path=AssetDatabase.GetAssetPath(Selection.activeObject);
            if(!IsMot(path))return;
            if(motSource != Selection.activeObject)
            {
                motSource=Selection.activeObject;
                loadedPath=null;
                clips.Clear();
                DiscardPreview();
            }
            FindSiblingRig();
        }

        private void FindSiblingRig()
        {
            string path=AssetDatabase.GetAssetPath(motSource);
            if(!IsMot(path))return;
            if(referenceRig!=null)return;
            string stem=Path.ChangeExtension(path,null);
            foreach(string extension in new[]{".brres",".mdl0",".glb",".fbx"})
            {
                GameObject candidate=AssetDatabase.LoadAssetAtPath<GameObject>(stem+extension);
                if(candidate!=null){referenceRig=candidate;return;}
            }
        }
        private bool Load()
        {
            string path=AssetDatabase.GetAssetPath(motSource);
            if(!IsMot(path)) { diagnostics="Source must be a .mot file in Assets.";return false; }
            try
            {
                var bytes=File.ReadAllBytes(path);
                if(bytes.Length>256*1024*1024)throw new InvalidDataException("MOT exceeds 256 MiB safety limit.");
                List<MotGntaUnityConverter.ClipUnit> units=MotGntaUnityConverter.ReadArchive(bytes,Path.GetFileName(path));
                clips.Clear();
                clips.AddRange(units);
                loadedPath=path;
                if(slotIndex<0||slotIndex>=clips.Count)slotIndex=0;
                diagnostics=Path.GetFileName(path)+": "+clips.Count+" occupied MOT slots. Use Analyze to inspect track compatibility.";
                return clips.Count>0;
            }
            catch(Exception ex)
            {
                clips.Clear();loadedPath=null;diagnostics="MOT analysis failed: "+ex.Message;
                Debug.LogException(ex);return false;
            }
        }
        private MotGntaUnityConverter.Options Options()
        {
            var o=new MotGntaUnityConverter.Options();
            o.FrameRate=Mathf.Clamp(samplingFps,15,120);
            o.Axis=Mathf.Clamp(axis,0,2);
            o.PositionPlane2D=Mathf.Clamp(positionPlane,0,2);
            o.PositionScale=Mathf.Clamp(positionScale,0.0001f,100f);
            o.PositionRelative=relativePosition;
            o.Rotate2D=Mathf.Clamp(experimental2D,0,3);
            o.AxisAngle4D=enable4DRotation;
            o.AxisAngleRadians=radians4D;
            o.RotationComposition=Mathf.Clamp(rotationOrder,0,2);
            o.IncludeAttachments=includeAttachments;
            string mappingPath=mappingCsv==null?null:AssetDatabase.GetAssetPath(mappingCsv);
            o.Mapping=MotGntaUnityConverter.GetMapping(mappingPath);
            return o;
        }
        private void MapRig(out Dictionary<string,Transform> targets,out Dictionary<string,string> paths)
        {
            targets=new Dictionary<string,Transform>(StringComparer.Ordinal);
            paths=new Dictionary<string,string>(StringComparer.Ordinal);
            if(referenceRig==null)return;
            Walk(referenceRig.transform,"",targets,paths);
        }
        private static void Walk(Transform t,string path,
            Dictionary<string,Transform> targets,Dictionary<string,string> paths)
        {
            if(!targets.ContainsKey(t.name)) { targets.Add(t.name,t);paths.Add(t.name,path); }
            foreach(Transform child in t)
            {
                string p=path.Length==0?child.name:path+"/"+child.name;
                Walk(child,p,targets,paths);
            }
        }
        private AnimationClip Decode(int index,bool generate,out string report)
        {
            if(index<0 || index>=clips.Count)throw new InvalidDataException("No selected MOT slot.");
            Dictionary<string,Transform> targets;
            Dictionary<string,string> paths;
            MapRig(out targets,out paths);
            // With no rig, analysis still lists recognized binary tracks and slots.
            return MotGntaUnityConverter.Convert(clips[index],
                Path.GetFileNameWithoutExtension(loadedPath),
                referenceRig==null?null:targets,
                referenceRig==null?null:paths,
                Options(),generate,out report);
        }
        private void Analyze()
        {
            if(Load())
            {
                string report;
                Decode(slotIndex,false,out report);
                diagnostics=report;
            }
        }
        private void Export(bool all)
        {
            if(!EnsureReady())return;
            string directory=Path.GetDirectoryName(loadedPath).Replace('\\','/');
            string folderName=Path.GetFileNameWithoutExtension(loadedPath)+"_MOT_Animations";
            string folder=directory+"/"+folderName;
            if(!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(directory,folderName);
            int ok=0,empty=0,failed=0,existing=0;
            var errors=new List<string>();
            try
            {
                int count=all?clips.Count:1;
                for(int i=0;i<count;i++)
                {
                    int at=all?i:slotIndex;
                    string dest=folder+"/"+Path.GetFileNameWithoutExtension(loadedPath)+
                        "_MOT_"+clips[at].Slot.ToString("X4")+".anim";
                    if(all && EditorUtility.DisplayCancelableProgressBar("MOT Export",
                        "Slot "+clips[at].Slot.ToString("X4"),(float)i/count))break;
                    if(AssetDatabase.LoadAssetAtPath<AnimationClip>(dest)!=null){existing++;continue;}
                    try
                    {
                        string report;
                        AnimationClip clip=Decode(at,true,out report);
                        if(clip==null) {empty++;continue;}
                        AssetDatabase.CreateAsset(clip,dest);
                        ok++;
                    }
                    catch(Exception ex)
                    {
                        failed++;
                        if(errors.Count<6)errors.Add("0x"+clips[at].Slot.ToString("X4")+": "+ex.Message);
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar();AssetDatabase.SaveAssets();AssetDatabase.Refresh(); }
            diagnostics="MOT .anim export: created="+ok+", skipped without supported curves="+empty+
                ", already exists="+existing+", failed="+failed+". Output: "+folder+
                (errors.Count>0?"\n"+string.Join("\n",errors.ToArray()):"");
            Debug.Log("[MOT Workbench] "+diagnostics);
        }
        private bool EnsureReady()
        {
            if(clips.Count==0 || loadedPath!=AssetDatabase.GetAssetPath(motSource))
                if(!Load())return false;
            if(referenceRig==null)
            {
                FindSiblingRig();
                if(referenceRig==null){diagnostics="Assign the matching BRRES model or GameObject root before export/preview.";return false;}
            }
            return true;
        }

        private void DiscardPreview()
        {
            playing=false;
            if(preview!=null) { preview.Cleanup();preview=null; }
            previewCopy=null;
            if(temporaryClip!=null) { DestroyImmediate(temporaryClip);temporaryClip=null; }
            duration=0f;previewTime=0f;
        }
        private static Bounds FindBounds(GameObject root)
        {
            bool any=false;
            Bounds b=new Bounds(Vector3.zero,Vector3.one);
            foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!any) { b=renderer.bounds;any=true; }
                else b.Encapsulate(renderer.bounds);
            }
            if(!any)throw new InvalidOperationException("Model contains no MeshRenderer/SkinnedMeshRenderer to preview.");
            return b;
        }
        private void BuildPreview()
        {
            if(!EnsureReady())return;
            DiscardPreview();
            try
            {
                string report;
                temporaryClip=Decode(slotIndex,true,out report);
                if(temporaryClip==null)
                {
                    diagnostics=report+"\nNo exportable tracks with these rotation settings.";
                    return;
                }
                temporaryClip.hideFlags=HideFlags.HideAndDontSave;
                preview=new PreviewRenderUtility();
                previewCopy=Instantiate(referenceRig);
                previewCopy.name=referenceRig.name+" [MOT Preview]";
                previewCopy.hideFlags=HideFlags.HideAndDontSave;
                preview.AddSingleGO(previewCopy);
                preview.ambientColor=new Color(.4f,.4f,.4f);
                preview.lights[0].intensity=1.3f;
                preview.lights[0].transform.rotation=Quaternion.Euler(40f,35f,0f);
                preview.lights[1].intensity=.85f;
                duration=Mathf.Max(.01f,temporaryClip.length);
                modelBounds=FindBounds(previewCopy);
                Sample();
                diagnostics=report+"\nPreview uses an isolated copy, not the active scene.";
            }
            catch(Exception ex)
            {
                DiscardPreview();diagnostics="Preview error: "+ex.Message;
                Debug.LogException(ex);
            }
        }
        private void Sample()
        {
            if(temporaryClip!=null && previewCopy!=null)
                temporaryClip.SampleAnimation(previewCopy,Mathf.Clamp(previewTime,0f,duration));
        }
        private void DrawViewport(Rect rect)
        {
            EditorGUI.DrawRect(rect,new Color(.10f,.13f,.16f));
            if(preview==null || previewCopy==null)
            {
                GUI.Label(rect,"Build Preview to display the MOT animation",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel){alignment=TextAnchor.MiddleCenter});
                return;
            }
            Event evt=Event.current;
            if(rect.Contains(evt.mousePosition))
            {
                if(evt.type==EventType.MouseDrag && evt.button==0)
                {
                    yaw+=evt.delta.x*.65f;
                    pitch=Mathf.Clamp(pitch-evt.delta.y*.65f,-85f,85f);
                    evt.Use();Repaint();
                }
                else if(evt.type==EventType.ScrollWheel)
                {
                    zoom=Mathf.Clamp(zoom*(1f+evt.delta.y*.075f),.35f,6f);
                    evt.Use();Repaint();
                }
            }
            if(evt.type!=EventType.Repaint || rect.width<=1 || rect.height<=1)return;
            float radius=Mathf.Max(.1f,modelBounds.extents.magnitude);
            Quaternion orbit=Quaternion.Euler(pitch,yaw,0f);
            Camera cam=preview.camera;
            cam.clearFlags=CameraClearFlags.Color;
            cam.backgroundColor=new Color(.13f,.16f,.20f);
            cam.fieldOfView=32f;
            cam.aspect=rect.width/rect.height;
            float distance=radius/Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad);
            distance*=zoom/Mathf.Min(1f,cam.aspect);
            cam.transform.rotation=orbit;
            cam.transform.position=modelBounds.center+orbit*(Vector3.back*distance);
            cam.nearClipPlane=Mathf.Max(.01f,distance-radius*2.5f);
            cam.farClipPlane=Mathf.Max(100f,distance+radius*4f);
            preview.BeginPreview(rect,GUIStyle.none);
            try{preview.Render(true,false);}
            finally{preview.EndAndDrawPreview(rect);}
        }
        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.MinHeight(260f));
            EditorGUILayout.LabelField("MOT / GNTA Animation Workbench",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Independent experimental package; .mot files stay DefaultAssets. 0x0204 rotation XYZ+angle is supported provisionally; 0x0202 remains UNDECODED.",MessageType.Warning);
            var next=EditorGUILayout.ObjectField("MOT Source",motSource,typeof(UnityEngine.Object),false);
            if(next!=motSource){motSource=next;loadedPath=null;clips.Clear();DiscardPreview();}
            if(GUILayout.Button("Use Selected .mot From Project"))AcceptSelected();
            GameObject rig=(GameObject)EditorGUILayout.ObjectField("Matching Model",referenceRig,typeof(GameObject),true);
            if(rig!=referenceRig){referenceRig=rig;DiscardPreview();}
            if(GUILayout.Button("Find Same-name BRRES / GLB Rig")){referenceRig=null;FindSiblingRig();}
            mappingCsv=(TextAsset)EditorGUILayout.ObjectField("Bone Mapping CSV (Optional)",mappingCsv,typeof(TextAsset),false);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Read MOT / Refresh")){Load();DiscardPreview();}
                if(GUILayout.Button("Analyze Selected Slot"))Analyze();
            }
            if(clips.Count>0)
            {
                string[] names=new string[clips.Count];
                for(int i=0;i<clips.Count;i++)names[i]="0x"+clips[i].Slot.ToString("X4");
                int chosen=EditorGUILayout.Popup("Animation Slot",Mathf.Clamp(slotIndex,0,clips.Count-1),names);
                if(chosen!=slotIndex){slotIndex=chosen;DiscardPreview();}
                EditorGUILayout.LabelField("Occupied Clips",clips.Count.ToString());
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Decode Options",EditorStyles.boldLabel);
            axis=EditorGUILayout.Popup("Basis",axis,new[]{"Flip X (BRRES default)","Flip Z","No flip"});
            positionPlane=EditorGUILayout.Popup("2D Position Plane",positionPlane,new[]{"XY","XZ","YZ"});
            positionScale=EditorGUILayout.FloatField("Translation Scale",positionScale);
            relativePosition=EditorGUILayout.Toggle("Position Relative To Bind",relativePosition);
            rotationOrder=EditorGUILayout.Popup("Rotation Composition",rotationOrder,
                new[]{"Decoded Absolute","Bind * Decoded","Decoded * Bind"});
            enable4DRotation=EditorGUILayout.Toggle("4D Axis-Angle Rotation",enable4DRotation);
            radians4D=EditorGUILayout.Toggle("4D Angles Are Radians (Test)",radians4D);
            experimental2D=EditorGUILayout.Popup("2D Rotation",experimental2D,
                new[]{"SKIP (recommended)","Experiment: axis X","Experiment: axis Y","Experiment: axis Z"});
            if(experimental2D!=0)
                EditorGUILayout.HelpBox("2D GNTA rotation values are NOT established as a sine/cosine pair. This is a visual experiment only.",MessageType.Error);
            includeAttachments=EditorGUILayout.Toggle("Include Attachment Bones",includeAttachments);
            samplingFps=EditorGUILayout.IntSlider("Sample FPS",samplingFps,15,120);
            EditorGUILayout.Space();
            using(new EditorGUI.DisabledScope(clips.Count==0 || referenceRig==null))
            {
                if(GUILayout.Button("Build / Refresh Isolated Preview",GUILayout.Height(27)))BuildPreview();
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Export Selected To .anim"))Export(false);
                    if(GUILayout.Button("Export All Occupied Slots"))Export(true);
                }
            }
            EditorGUILayout.EndScrollView();
            if(temporaryClip!=null)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(playing?"Pause":"Play"))
                    {
                        playing=!playing;lastEditorTick=EditorApplication.timeSinceStartup;
                    }
                    if(GUILayout.Button("Stop")){playing=false;previewTime=0f;Sample();}
                    if(GUILayout.Button("-1 Frame")){playing=false;previewTime=Mathf.Max(0,previewTime-1f/60f);Sample();}
                    if(GUILayout.Button("+1 Frame")){playing=false;previewTime=Mathf.Min(duration,previewTime+1f/60f);Sample();}
                }
                float t=EditorGUILayout.Slider("Time",previewTime,0f,duration);
                if(!Mathf.Approximately(t,previewTime)){previewTime=t;playing=false;Sample();}
                loop=EditorGUILayout.Toggle("Loop",loop);
                playbackSpeed=EditorGUILayout.Slider("Speed",playbackSpeed,.1f,2f);
            }
            Rect rect=GUILayoutUtility.GetRect(1f,260f,GUILayout.ExpandWidth(true));
            DrawViewport(rect);
            EditorGUILayout.HelpBox(diagnostics,MessageType.Info);
            EditorGUILayout.LabelField("Drag to orbit; mouse wheel to zoom. Preview never modifies your Scene.",EditorStyles.centeredGreyMiniLabel);
        }
    }
}
#endif
