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

        private static SerializedProperty FindBumpSlot(SerializedProperty slots,string key)
        {
            if(slots==null || !slots.isArray)return null;
            for(int i=0;i<slots.arraySize;i++)
            {
                SerializedProperty item=slots.GetArrayElementAtIndex(i);
                if(item.FindPropertyRelative("key").stringValue==key)return item;
            }
            return null;
        }

        private static void SetBumpSlot(SerializedProperty slots,string key,string textureName)
        {
            SerializedProperty item=FindBumpSlot(slots,key);
            if(item==null)
            {
                if(string.IsNullOrEmpty(textureName))return;
                slots.InsertArrayElementAtIndex(slots.arraySize);
                item=slots.GetArrayElementAtIndex(slots.arraySize-1);
                item.FindPropertyRelative("key").stringValue=key;
            }
            item.FindPropertyRelative("sourceTextureName").stringValue=textureName??string.Empty;
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
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("TEX0 _MM / Grayscale Bump Mapping",EditorStyles.boldLabel);
            Prop("bumpMode","Mode");
            SerializedProperty bumpModeProperty=serializedObject.FindProperty("bumpMode");
            if(bumpModeProperty!=null && !bumpModeProperty.hasMultipleDifferentValues &&
               (BrresBumpMode)bumpModeProperty.enumValueIndex!=BrresBumpMode.Off)
            {
                Prop("bumpStrength","Strength");
                if((BrresBumpMode)bumpModeProperty.enumValueIndex==BrresBumpMode.HeightToNormal)
                    Prop("bumpInvertHeight","Invert Height");
            }
            EditorGUILayout.HelpBox("Off is the safe default. Height To Normal converts _MM grayscale TEX0 into a new normal-map sub-asset, Original Grayscale uses a supported shader parallax property when available. Wii GX/TEV links are not yet decoded: name matching is only a suggestion.",MessageType.Info);
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
            if(manifest.availableMmTextures!=null && manifest.availableMmTextures.Count>0)
                EditorGUILayout.LabelField("Detected _MM TEX0: "+string.Join(", ",manifest.availableMmTextures.ToArray()),EditorStyles.wordWrappedMiniLabel);
            else
                EditorGUILayout.HelpBox("No TEX0 names ending in _MM were decoded. Check Import TEX0 Textures and whether this BRRES contains the _MM images.",MessageType.Info);
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
            SerializedProperty bumpSlots=serializedObject.FindProperty("bumpSlotOverrides");
            foreach(BrresMaterialEntry entry in manifest.entries)
            {
                if(entry==null)continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(entry.sourceName+"  (model "+entry.modelIndex+", slot "+entry.materialIndex+")",EditorStyles.boldLabel);
                if(!string.IsNullOrEmpty(entry.textureName))
                    EditorGUILayout.LabelField("Source texture: "+entry.textureName,EditorStyles.miniLabel);

                // Explicit mapping is required whenever TEV links cannot be inferred
                // from an exact _BM/_MM or material/_MM shared name stem.
                var candidates=manifest.availableMmTextures;
                if(bumpSlots!=null && candidates!=null && candidates.Count>0)
                {
                    var optionNames=new List<string> { "Auto (strict name match)", "None (no _MM)" };
                    optionNames.AddRange(candidates);
                    SerializedProperty selected=FindBumpSlot(bumpSlots,entry.key);
                    string sourceName=selected==null?null:selected.FindPropertyRelative("sourceTextureName").stringValue;
                    int current=0;
                    if(sourceName==BrresBumpMapUtility.NoBumpOverride)current=1;
                    else if(!string.IsNullOrEmpty(sourceName))
                    {
                        int ix=candidates.FindIndex(t=>string.Equals(t,sourceName,StringComparison.OrdinalIgnoreCase));
                        if(ix>=0)current=ix+2;
                    }
                    EditorGUI.BeginChangeCheck();
                    int choice=EditorGUILayout.Popup("_MM Source",current,optionNames.ToArray());
                    if(EditorGUI.EndChangeCheck())
                    {
                        string key=choice==0?"":choice==1?BrresBumpMapUtility.NoBumpOverride:
                            candidates[choice-2];
                        SetBumpSlot(bumpSlots,entry.key,key);
                    }
                }
                if(!string.IsNullOrEmpty(entry.suggestedBumpTextureName))
                    EditorGUILayout.LabelField("Auto-match candidate: "+entry.suggestedBumpTextureName,EditorStyles.miniLabel);
                if(entry.sourceBumpTexture!=null)
                    EditorGUILayout.ObjectField("Original _MM",entry.sourceBumpTexture,typeof(Texture2D),false);
                if(entry.generatedNormalTexture!=null)
                    EditorGUILayout.ObjectField("Generated Normal",entry.generatedNormalTexture,typeof(Texture2D),false);
                if(!string.IsNullOrEmpty(entry.bumpStatus))
                    EditorGUILayout.LabelField(entry.bumpStatus,EditorStyles.wordWrappedMiniLabel);
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
            if(overrides.arraySize>0 && bumpModeProperty!=null &&
               !bumpModeProperty.hasMultipleDifferentValues &&
               (BrresBumpMode)bumpModeProperty.enumValueIndex!=BrresBumpMode.Off)
            {
                EditorGUILayout.HelpBox("External .mat overrides are in use: the generated _MM maps are NOT automatically written into those .mat assets. Use the explicit copy button below, or clear external overrides and Apply.",MessageType.Warning);
                using(new EditorGUI.DisabledScope(serializedObject.hasModifiedProperties))
                {
                    if(GUILayout.Button("Copy _MM Bump To External .mat...",GUILayout.Height(25)))
                        CopyBumpsToExternalMaterials(manifest,overrides,importer);
                }
                if(serializedObject.hasModifiedProperties)
                    EditorGUILayout.LabelField("Click Apply before copying bump maps into external materials.",EditorStyles.miniLabel);
            }
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

        private static void CopyBumpsToExternalMaterials(BrresMaterialManifest manifest,
            SerializedProperty overrides,BrresNativeImporter importer)
        {
            if(manifest==null || manifest.entries==null || overrides==null || importer==null)return;
            if(!EditorUtility.DisplayDialog("Apply _MM bump to extracted Materials?",
                "This will edit existing external .mat assets that have an _MM source selected. "+
                "It will not change the BRRES or any other external material properties. "+
                "You can undo these edits in Unity or source control.",
                "Update .mat Assets","Cancel"))return;
            int updated=0,skipped=0;
            foreach(BrresMaterialEntry entry in manifest.entries)
            {
                if(entry==null || entry.sourceBumpTexture==null)continue;
                SerializedProperty slot=FindOverride(overrides,entry.key);
                if(slot==null)continue;
                Material material=slot.FindPropertyRelative("material").objectReferenceValue as Material;
                if(material==null)continue;
                string path=AssetDatabase.GetAssetPath(material);
                if(string.IsNullOrEmpty(path) || path==importer.assetPath || !path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                { skipped++;continue; }
                Undo.RecordObject(material,"Apply BRRES _MM Bump");
                if(BrresBumpMapUtility.Assign(material,entry.sourceBumpTexture,
                    entry.generatedNormalTexture,importer.bumpMode,importer.bumpStrength))
                { EditorUtility.SetDirty(material);updated++; }
                else skipped++;
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Bump mapping applied",
                "External Materials updated: "+updated+"; skipped: "+skipped+
                ". These are approximate Unity normal/parallax maps, not reconstructed Wii TEV shaders.",
                "OK");
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
