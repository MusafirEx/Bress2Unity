// Generate MOT .anim files against a chosen scene model and optional bone mapping CSV.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MusafirEx.Mot
{
    public sealed class MotExportWindow : EditorWindow
    {
        private UnityEngine.Object source;
        private GameObject rig;
        private TextAsset unusedCsv; // CSV is read by path, because Unity may import .csv as DefaultAsset.
        private string mappingCsv = "";
        private string output = "Assets/MOTAnimations";
        private int fps=60,axis=0,rotation2D=0,composition=1;
        private float scale=1f;
        private bool relative=true,rotation4D=false,attachments=true;
        private Vector2 scroll;
        private string status="Select a MOT source and a compatible skeleton root to analyze its tracks.";

        [MenuItem("Tools/MH/MOT to Unity .anim (Experimental)")]
        public static void ShowWindow()
        {
            var w=GetWindow<MotExportWindow>("MOT → Unity .anim");
            w.minSize=new Vector2(600,470);
            var selected=Selection.activeObject;
            string path=AssetDatabase.GetAssetPath(selected);
            if(!string.IsNullOrEmpty(path)&&path.EndsWith(".mot",StringComparison.OrdinalIgnoreCase))w.source=selected;
            w.Show();
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("MOT / GNTA → Unity (EXPERIMENTAL)",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("MOT is a separate format, not CHR0. 0x0202 2D rotations are not yet proven and default to SKIP. Use a copied test scene to try other modes. The tested BRRES/CHR0 importer is unchanged.",MessageType.Warning);
            source=EditorGUILayout.ObjectField("MOT source (.mot)",source,typeof(UnityEngine.Object),false);
            rig=(GameObject)EditorGUILayout.ObjectField("Model root (Hierarchy)",rig,typeof(GameObject),true);
            EditorGUILayout.BeginHorizontal();
            mappingCsv=EditorGUILayout.TextField("Mapping CSV (optional)",mappingCsv);
            if(GUILayout.Button("Browse",GUILayout.Width(75)))
            {
                string p=EditorUtility.OpenFilePanel("MOT bone mapping CSV",Application.dataPath,"csv");
                if(!string.IsNullOrWhiteSpace(p))mappingCsv=p;
            }
            EditorGUILayout.EndHorizontal();
            output=EditorGUILayout.TextField("Output folder",output);
            fps=EditorGUILayout.IntPopup("FPS",fps,new[]{"30","60","120"},new[]{30,60,120});
            axis=EditorGUILayout.Popup("Axis",axis,new[]{"Flip X (native BRRES)","Flip Z","None"});
            scale=EditorGUILayout.FloatField("Translation scale",scale);
            relative=EditorGUILayout.Toggle("Relative translations",relative);
            rotation2D=EditorGUILayout.Popup("0x0202 2D rotations",rotation2D,new[]{"Skip (safe)","X-axis (experimental)","Y-axis (experimental)","Z-axis (experimental)"});
            rotation4D=EditorGUILayout.Toggle("0x0204 axis-angle (experimental)",rotation4D);
            composition=EditorGUILayout.Popup("Rotation composition",composition,new[]{"Absolute","Bind * motion","Motion * bind"});
            attachments=EditorGUILayout.Toggle("Attachment bones",attachments);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Analyze MOT",GUILayout.Height(32)))Run(false);
            GUI.enabled=rig!=null;
            if(GUILayout.Button("Generate .anim files",GUILayout.Height(32)))Run(true);
            GUI.enabled=true;
            EditorGUILayout.EndHorizontal();
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(status,GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        private void Run(bool export)
        {
            string path=AssetDatabase.GetAssetPath(source);
            if(string.IsNullOrEmpty(path)||!path.EndsWith(".mot",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))
            {status="Select a valid .mot file in the Unity Project.";return;}
            if(export && rig==null){status="Assign a GameObject model root from Hierarchy.";return;}
            try
            {
                byte[] bytes=File.ReadAllBytes(path);
                var clips=global::MotGntaUnityConverter.ReadArchive(bytes,Path.GetFileName(path));
                Dictionary<string,Transform> targets=null;
                Dictionary<string,string> paths=null;
                if(rig!=null) MotRigMapping.Collect(rig.transform,out targets,out paths);
                var options=new global::MotGntaUnityConverter.Options {
                    Mapping=global::MotGntaUnityConverter.GetMapping(mappingCsv),FrameRate=fps,Axis=axis,
                    PositionRelative=relative,PositionScale=scale,PositionPlane2D=0,
                    Rotate2D=rotation2D,AxisAngle4D=rotation4D,RotationComposition=composition,IncludeAttachments=attachments
                };
                if(export)
                {
                    if(!output.Replace('\\','/').StartsWith("Assets/",StringComparison.Ordinal) || output.Contains(".."))
                        throw new InvalidDataException("Output must be a folder inside Assets/, without '..'");
                    string full=Path.Combine(Directory.GetCurrentDirectory(),output.Replace('/',Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(full);
                    AssetDatabase.Refresh();
                }
                var reports=new List<string>();int written=0;
                try
                {
                    foreach(var item in clips)
                    {
                        EditorUtility.DisplayProgressBar("MOT animation import",item.Slot.ToString("X4"),clips.Count==0?1f:(float)reports.Count/clips.Count);
                        try
                        {
                            string report;
                            var clip=global::MotGntaUnityConverter.Convert(item,Path.GetFileNameWithoutExtension(path),targets,paths,options,export,out report);
                            if(clip!=null)
                            {
                                string asset=AssetDatabase.GenerateUniqueAssetPath(output.TrimEnd('/')+"/"+clip.name+".anim");
                                AssetDatabase.CreateAsset(clip,asset);
                                written++;
                                report+=" → "+asset;
                            }
                            reports.Add(report);
                        }
                        catch(Exception ex){reports.Add(item.Slot.ToString("X4")+": ERROR "+ex.Message);}
                    }
                }
                finally{EditorUtility.ClearProgressBar();}
                if(export) {AssetDatabase.SaveAssets();AssetDatabase.Refresh();}
                status=(export?"Exported "+written+" / ":"Analyzed ")+clips.Count+" occupied MOT slots\n\n"+string.Join("\n",reports);
            }
            catch(Exception ex){EditorUtility.ClearProgressBar();status="MOT: "+ex;Debug.LogError(ex);}
        }
    }
}
#endif