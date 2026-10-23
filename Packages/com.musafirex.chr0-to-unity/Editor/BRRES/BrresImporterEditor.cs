// FBX-like Model / Rig / Animations / Materials tabs for Bress2Unity's native ScriptedImporter.
// The imported object remains a normal Unity GameObject prefab with Avatar sub-assets.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MusafirEx.Brres
{
    [CustomEditor(typeof(BrresNativeImporter))]
    public sealed class BrresImporterEditor : ScriptedImporterEditor
    {
        private int activeTab;
        private bool showHumanoidBones;
        private static readonly string[] Tabs={"Model","Rig","Animations","Materials"};

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            activeTab=GUILayout.Toolbar(activeTab,Tabs);
            EditorGUILayout.Space(7f);
            if(activeTab==0)DrawModel();
            else if(activeTab==1)DrawRig();
            else if(activeTab==2)DrawAnimation();
            else DrawMaterials();
            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        private void Prop(string field, string label=null)
        {
            SerializedProperty p=serializedObject.FindProperty(field);
            if(p==null)
            {
                EditorGUILayout.HelpBox("Missing importer setting: "+field,MessageType.Warning);
                return;
            }
            if(string.IsNullOrEmpty(label))EditorGUILayout.PropertyField(p,true);
            else EditorGUILayout.PropertyField(p,new GUIContent(label),true);
        }

        private void DrawModel()
        {
            EditorGUILayout.LabelField("Geometry",EditorStyles.boldLabel);
            Prop("flipX","Flip X (Wii → Unity)");
            Prop("flipUV","Flip UV V");
            Prop("unitsToMeters","Units To Meters");
            Prop("importTextures","Import TEX0 Textures");
            Prop("importSkinning","Import Skinning");
            Prop("reverseFaceWinding","Reverse Face Winding (Manual Override)");
            EditorGUILayout.HelpBox("Face orientation is checked automatically against the source normals. Enable the manual override only if the imported mesh still renders inside-out.",MessageType.Info);
        }

        private void DrawRig()
        {
            EditorGUILayout.LabelField("Rig Import Settings",EditorStyles.boldLabel);
            Prop("rigType","Animation Type");
            SerializedProperty kind=serializedObject.FindProperty("rigType");
            if(kind==null || kind.hasMultipleDifferentValues)return;
            BrresRigType rigType=(BrresRigType)kind.enumValueIndex;
            if(rigType==BrresRigType.None)
            {
                EditorGUILayout.HelpBox("No Animation or Animator component and no Avatar will be generated. Bone transforms and embedded CHR0 clips remain available.",MessageType.Info);
                return;
            }
            if(rigType==BrresRigType.Legacy)
            {
                EditorGUILayout.HelpBox("Adds Unity's Legacy Animation component and imports embedded CHR0 as legacy AnimationClips. No Avatar is generated.",MessageType.Info);
                return;
            }
            Prop("avatarDefinition","Avatar Definition");
            SerializedProperty definition=serializedObject.FindProperty("avatarDefinition");
            if(definition==null || definition.hasMultipleDifferentValues)return;
            bool copy=(BrresAvatarDefinition)definition.enumValueIndex==BrresAvatarDefinition.CopyFromOtherAvatar;
            if(copy)
            {
                Prop("sourceAvatar","Source Avatar");
                EditorGUILayout.HelpBox("Select an existing Avatar from another imported asset. Its type must match Generic/Humanoid. No duplicate Avatar is generated.",MessageType.Info);
            }
            else if(rigType==BrresRigType.Generic)
            {
                Prop("genericRootBone","Root Motion Bone");
                EditorGUILayout.HelpBox("Generic preserves the original Wii bone hierarchy. For your 0000.brres skeleton, Y is the suggested root bone. Leave blank to disable root motion.",MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Humanoid requires a compatible human skeleton and a suitable rest pose. Names alone do not guarantee a valid Avatar. Invalid Avatars are reported without cancelling model import.",MessageType.Warning);
                showHumanoidBones=EditorGUILayout.Foldout(showHumanoidBones,"Humanoid Bone Mapping",true);
                if(showHumanoidBones)
                {
                    EditorGUI.indentLevel++;
                    Prop("humanHips","Hips");
                    Prop("humanSpine","Spine");
                    Prop("humanChest","Chest (Optional)");
                    Prop("humanNeck","Neck (Optional)");
                    Prop("humanHead","Head");
                    EditorGUILayout.Space(4);
                    Prop("humanLeftShoulder","Left Shoulder");
                    Prop("humanLeftUpperArm","Left Upper Arm");
                    Prop("humanLeftLowerArm","Left Lower Arm");
                    Prop("humanLeftHand","Left Hand");
                    Prop("humanRightShoulder","Right Shoulder");
                    Prop("humanRightUpperArm","Right Upper Arm");
                    Prop("humanRightLowerArm","Right Lower Arm");
                    Prop("humanRightHand","Right Hand");
                    EditorGUILayout.Space(4);
                    Prop("humanLeftUpperLeg","Left Upper Leg");
                    Prop("humanLeftLowerLeg","Left Lower Leg");
                    Prop("humanLeftFoot","Left Foot");
                    Prop("humanRightUpperLeg","Right Upper Leg");
                    Prop("humanRightLowerLeg","Right Lower Leg");
                    Prop("humanRightFoot","Right Foot");
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.HelpBox("An Animator and generated Avatar are attached to the MDL0 model child inside the BRRES prefab, preserving existing CHR0 bone paths. Unity Humanoid retargeting of CHR0 transform curves is not guaranteed.",MessageType.None);
        }

        private static SerializedProperty FindOverride(SerializedProperty overrides, string key)
        {
            if(overrides==null || !overrides.isArray)return null;
            for(int i=0;i<overrides.arraySize;i++)
            {
                SerializedProperty item=overrides.GetArrayElementAtIndex(i);
                if(item.FindPropertyRelative("key").stringValue==key)return item;
            }
            return null;
        }

        private static void SetOverride(SerializedProperty overrides,string key,Material value)
        {
            SerializedProperty existing=FindOverride(overrides,key);
            if(existing==null)
            {
                if(value==null)return;
                int index=overrides.arraySize;
                overrides.InsertArrayElementAtIndex(index);
                existing=overrides.GetArrayElementAtIndex(index);
                existing.FindPropertyRelative("key").stringValue=key;
            }
            existing.FindPropertyRelative("material").objectReferenceValue=value;
        }

        private void DrawMaterials()
        {
            EditorGUILayout.LabelField("Material Import Settings",EditorStyles.boldLabel);
            Prop("importMaterials","Import Materials");
            SerializedProperty flag=serializedObject.FindProperty("importMaterials");
            if(flag==null || flag.hasMultipleDifferentValues)return;
            if(!flag.boolValue)
            {
                EditorGUILayout.HelpBox("Materials are disabled. Renderer materials will not be assigned; enable Import Materials and Apply to generate and edit them.",MessageType.Info);
                return;
            }
            Prop("materialShaderOverride","Generated Material Shader");
            EditorGUILayout.HelpBox("Bress2Unity creates approximate Unity materials from MDL0 and TEX0. Wii GX/TEV shader effects are not fully converted. Assign an external .mat below to replace each renderer material without editing the source BRRES.",MessageType.Info);

            BrresNativeImporter importer=(BrresNativeImporter)target;
            BrresMaterialManifest manifest=null;
            foreach(UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(importer.assetPath))
            {
                manifest=obj as BrresMaterialManifest;
                if(manifest!=null)break;
            }
            if(manifest==null)
            {
                EditorGUILayout.HelpBox("No material index is available yet. Apply the current importer settings and reimport this BRRES to generate its material list.",MessageType.Info);
                return;
            }
            if(manifest.entries==null || manifest.entries.Count==0)
            {
                EditorGUILayout.HelpBox("No MDL0 materials found (this archive may contain CHR0 animations only).",MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Materials ("+manifest.entries.Count+")",EditorStyles.boldLabel);
            SerializedProperty overrides=serializedObject.FindProperty("materialOverrides");
            if(overrides==null)
            {
                EditorGUILayout.HelpBox("Material override storage is unavailable.",MessageType.Error);
                return;
            }
            foreach(BrresMaterialEntry entry in manifest.entries)
            {
                if(entry==null)continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(entry.sourceName+"  (model "+entry.modelIndex+", slot "+entry.materialIndex+")",EditorStyles.boldLabel);
                if(!string.IsNullOrEmpty(entry.textureName))
                    EditorGUILayout.LabelField("Source texture: "+entry.textureName,EditorStyles.miniLabel);
                SerializedProperty item=FindOverride(overrides,entry.key);
                Material current=item==null?null:item.FindPropertyRelative("material").objectReferenceValue as Material;
                EditorGUI.BeginChangeCheck();
                Material value=(Material)EditorGUILayout.ObjectField("External Material",current,typeof(Material),false);
                if(EditorGUI.EndChangeCheck())
                {
                    if(value!=null && AssetDatabase.GetAssetPath(value)==importer.assetPath)
                        EditorUtility.DisplayDialog("External material required","Select a separate .mat asset, not a material sub-asset from this BRRES. Use Extract Materials first.","OK");
                    else SetOverride(overrides,entry.key,value);
                }
                using(new EditorGUI.DisabledScope(entry.generatedMaterial==null))
                {
                    EditorGUILayout.ObjectField("Generated Material",entry.generatedMaterial,typeof(Material),false);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(6);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Extract Materials...",GUILayout.Height(25)))
                    ExtractMaterials(importer,manifest);
                using(new EditorGUI.DisabledScope(overrides.arraySize==0))
                    if(GUILayout.Button("Clear Overrides",GUILayout.Height(25)))
                        overrides.ClearArray();
            }
            EditorGUILayout.HelpBox("Material assignments are applied when you click Apply. Extract Materials makes editable .mat files inside Assets and assigns them as external overrides.",MessageType.None);
        }

        private static string SafeFileName(string s)
        {
            if(string.IsNullOrWhiteSpace(s))return "Material";
            char[] invalid=System.IO.Path.GetInvalidFileNameChars();
            char[] chars=s.ToCharArray();
            for(int i=0;i<chars.Length;i++)
                if(Array.IndexOf(invalid,chars[i])>=0 || chars[i]=='/' || chars[i]=='\\' || chars[i]==':')
                    chars[i]='_';
            return new string(chars);
        }

        private void ExtractMaterials(BrresNativeImporter importer,BrresMaterialManifest manifest)
        {
            if(manifest==null || manifest.entries==null || manifest.entries.Count==0)return;
            string absolute=EditorUtility.OpenFolderPanel("Extract BRRES Materials",Application.dataPath,"");
            if(string.IsNullOrEmpty(absolute))return;
            string folder=FileUtil.GetProjectRelativePath(absolute);
            if(string.IsNullOrEmpty(folder))
            {
                EditorUtility.DisplayDialog("Invalid destination","Extracted materials must be saved inside this project's Assets folder.","OK");
                return;
            }
            folder=folder.Replace('\\','/');
            if(folder!="Assets" && !folder.StartsWith("Assets/",StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Folder must be inside Assets","Select a folder inside this Unity project's Assets directory.","OK");
                return;
            }

            // Apply any outstanding Inspector changes before modifying the importer directly.
            serializedObject.ApplyModifiedProperties();
            int extracted=0;
            foreach(BrresMaterialEntry entry in manifest.entries)
            {
                if(entry==null || entry.generatedMaterial==null)continue;
                string proposed=folder+"/"+SafeFileName(entry.sourceName)+"_model"+entry.modelIndex+"_slot"+entry.materialIndex+".mat";
                string destination=AssetDatabase.GenerateUniqueAssetPath(proposed);
                Material clone=new Material(entry.generatedMaterial);
                clone.name=entry.sourceName;
                AssetDatabase.CreateAsset(clone,destination);
                if(importer.materialOverrides==null)importer.materialOverrides=new List<BrresMaterialOverride>();
                BrresMaterialOverride record=null;
                foreach(BrresMaterialOverride candidate in importer.materialOverrides)
                    if(candidate!=null && candidate.key==entry.key){record=candidate;break;}
                if(record==null)
                {
                    record=new BrresMaterialOverride {key=entry.key};
                    importer.materialOverrides.Add(record);
                }
                record.material=clone;
                extracted++;
            }
            if(extracted==0)
            {
                EditorUtility.DisplayDialog("Nothing extracted","The imported BRRES contains no generated material assets. Enable Import Materials and Apply first.","OK");
                return;
            }
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            GUIUtility.ExitGUI();
        }

        private void DrawAnimation()
        {
            EditorGUILayout.LabelField("CHR0 Import",EditorStyles.boldLabel);
            Prop("importAnimations","Import Embedded CHR0");
            Prop("animationRig","External Animation Rig");
            Prop("animationFps","Sample Frame Rate");
            Prop("animationAxis","Axis Conversion (0=X, 1=Z, 2=None)");
            Prop("animationLoop","Copy Source Loop Settings");
            EditorGUILayout.HelpBox("CHR0-only BRRES archives are still supported. If the rig is in a separate BRRES, assign its model prefab above or use Tools > MH > CHR0 from BRRES.",MessageType.Info);
        }
    }
}
#endif
