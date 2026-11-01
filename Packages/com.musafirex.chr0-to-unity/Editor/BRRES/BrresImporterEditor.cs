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
            EditorGUILayout.LabelField("TEX0 _MM_XX / Grayscale Bump Mapping",EditorStyles.boldLabel);
            Prop("bumpMode","Mode");
            SerializedProperty bumpModeProperty=serializedObject.FindProperty("bumpMode");
            if(bumpModeProperty!=null && !bumpModeProperty.hasMultipleDifferentValues &&
               (BrresBumpMode)bumpModeProperty.enumValueIndex!=BrresBumpMode.Off)
            {
                Prop("bumpStrength","Strength");
                if((BrresBumpMode)bumpModeProperty.enumValueIndex==BrresBumpMode.HeightToNormal)
                    Prop("bumpInvertHeight","Invert Height");
            }
            EditorGUILayout.HelpBox("Off is the safe default. Height To Normal converts _MM_XX grayscale TEX0 into a generated normal-map sub-asset. Auto pairing requires the SAME prefix and numeric suffix: body_BM_01 -> body_MM_01 (not MM_02). Old unnumbered _BM/_MM is also supported. Wii GX/TEV links are not yet decoded.",MessageType.Info);
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
                EditorGUILayout.HelpBox("No recognized _MM_XX textures were decoded (older _MM names are also supported). Verify Import TEX0 Textures and the BRRES source texture names.",MessageType.Info);
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
                // from an exact _BM_XX/_MM_XX pair (or older _BM/_MM).
                var candidates=manifest.availableMmTextures;
                if(bumpSlots!=null && candidates!=null && candidates.Count>0)
                {
                    var optionNames=new List<string> { "Auto (exact _BM_XX to _MM_XX)", "None (no _MM)" };
                    optionNames.AddRange(candidates);
                    SerializedProperty selected=FindBumpSlot(bumpSlots,entry.key);
                    string sourceName=selected==null?null:selected.FindPropertyRelative("sourceTextureName").stringValue;
                    int currentBumpIndex=0;
                    if(sourceName==BrresBumpMapUtility.NoBumpOverride)currentBumpIndex=1;
                    else if(!string.IsNullOrEmpty(sourceName))
                    {
                        int ix=candidates.FindIndex(t=>string.Equals(t,sourceName,StringComparison.OrdinalIgnoreCase));
                        if(ix>=0)currentBumpIndex=ix+2;
                    }
                    EditorGUI.BeginChangeCheck();
                    int choice=EditorGUILayout.Popup("_MM Source",currentBumpIndex,optionNames.ToArray());
                    if(EditorGUI.EndChangeCheck())
                    {
                        string key=choice==0?"":choice==1?BrresBumpMapUtility.NoBumpOverride:
                            candidates[choice-2];
                        SetBumpSlot(bumpSlots,entry.key,key);
                    }
                }
                if(!string.IsNullOrEmpty(entry.suggestedBumpTextureName))
                    EditorGUILayout.LabelField("Auto paired _BM / _MM: "+entry.suggestedBumpTextureName,EditorStyles.miniLabel);
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
            int sourceCount=0,overrideCount=0,readyCount=0;
            foreach(BrresMaterialEntry entry in manifest.entries)
            {
                if(entry==null)continue;
                if(entry.sourceBumpTexture!=null)sourceCount++;
                SerializedProperty currentOverride=FindOverride(overrides,entry.key);
                Material mat=currentOverride==null?null:
                    currentOverride.FindPropertyRelative("material").objectReferenceValue as Material;
                string path=mat==null?null:AssetDatabase.GetAssetPath(mat);
                if(!string.IsNullOrEmpty(path) && path.StartsWith("Assets/",StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                {
                    overrideCount++;
                    if(entry.sourceBumpTexture!=null &&
                       (bumpModeProperty==null ||
                        (BrresBumpMode)bumpModeProperty.enumValueIndex!=BrresBumpMode.HeightToNormal ||
                        entry.generatedNormalTexture!=null))
                        readyCount++;
                }
            }
            EditorGUILayout.LabelField("Bump Mapping Preflight",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Decoded _MM(_XX): "+(manifest.availableMmTextures==null?0:manifest.availableMmTextures.Count)+
                " | Materials: "+manifest.entries.Count+
                " | _MM source assigned: "+sourceCount+
                " | External .mat assigned: "+overrideCount+
                " | Candidate pairs: "+readyCount,EditorStyles.wordWrappedMiniLabel);
            if(readyCount==0 && overrides.arraySize>0)
                EditorGUILayout.HelpBox("No material currently has both a linked _MM_XX source and an external .mat. Auto requires an exact corresponding _BM_XX texture name. Otherwise select _MM Source manually for each material, click Apply, and retry.",MessageType.Warning);
            // An opt-in batch action for archives with exactly one _MM image.
            // Never silently apply the sole _MM to all model materials.
            if(bumpSlots!=null && manifest.availableMmTextures!=null &&
               manifest.availableMmTextures.Count==1 && manifest.entries.Count>1)
            {
                if(GUILayout.Button("Assign sole _MM to unmapped slots (experimental)..."))
                {
                    string textureName=manifest.availableMmTextures[0];
                    if(EditorUtility.DisplayDialog("Use one _MM texture across multiple materials?",
                        "This assigns "+textureName+" to any material slot that does not already have a _MM source. "+
                        "The Wii GX/TEV material link is not decoded: some slots may NOT use this bump texture. "+
                        "Inspect each material after clicking Apply.",
                        "Assign _MM (manual confirmation)","Cancel"))
                    {
                        int changed=0;
                        foreach(BrresMaterialEntry entry in manifest.entries)
                        {
                            if(entry==null || entry.sourceBumpTexture!=null)continue;
                            SerializedProperty existing=FindBumpSlot(bumpSlots,entry.key);
                            if(existing!=null &&
                               !string.IsNullOrEmpty(existing.FindPropertyRelative("sourceTextureName").stringValue))
                                continue; // Preserve explicit None and per-slot mappings.
                            SetBumpSlot(bumpSlots,entry.key,textureName);
                            changed++;
                        }
                        if(changed==0)
                            EditorUtility.DisplayDialog("No unmapped slots","Every material already has a _MM source or an explicit selection.","OK");
                    }
                }
            }
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
            int total=0,missingSource=0,missingNormal=0,missingMaterial=0,notEditable=0,unsupportedShader=0;
            var candidates=new List<BrresMaterialEntry>();
            var targetMaterials=new List<Material>();
            foreach(BrresMaterialEntry entry in manifest.entries)
            {
                if(entry==null)continue;
                total++;
                if(entry.sourceBumpTexture==null){missingSource++;continue;}
                if(importer.bumpMode==BrresBumpMode.HeightToNormal && entry.generatedNormalTexture==null)
                {missingNormal++;continue;}
                SerializedProperty slot=FindOverride(overrides,entry.key);
                Material material=slot==null?null:slot.FindPropertyRelative("material").objectReferenceValue as Material;
                if(material==null){missingMaterial++;continue;}
                string path=AssetDatabase.GetAssetPath(material);
                if(string.IsNullOrEmpty(path) || !path.StartsWith("Assets/",StringComparison.OrdinalIgnoreCase) ||
                   path==importer.assetPath || !path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                {notEditable++;continue;}
                bool compatible=importer.bumpMode==BrresBumpMode.HeightToNormal
                    ? material.HasProperty("_BumpMap")||material.HasProperty("_NormalMap")
                    : importer.bumpMode==BrresBumpMode.OriginalGrayscale && material.HasProperty("_ParallaxMap");
                if(!compatible){unsupportedShader++;continue;}
                candidates.Add(entry);
                targetMaterials.Add(material);
            }
            string breakdown="BRRES material slots: "+total+
                "\nNo assigned _MM source: "+missingSource+
                "\nNormal texture unavailable: "+missingNormal+
                "\nNo external .mat override: "+missingMaterial+
                "\nNot an editable Assets/*.mat: "+notEditable+
                "\nShader lacks required bump property: "+unsupportedShader+
                "\nReady to update: "+candidates.Count;
            if(candidates.Count==0)
            {
                EditorUtility.DisplayDialog("No bump mapping was applied",
                    breakdown+"\n\nSelect the matching _MM_XX Source for each material slot, click Apply, and confirm the external .mat assignment. Auto pairs only identical names/suffixes, e.g. body_BM_01 -> body_MM_01. Enable Import TEX0 Textures if no _MM_XX textures are listed.",
                    "OK");
                return;
            }
            if(!EditorUtility.DisplayDialog("Copy _MM bump to external materials?",
                breakdown+"\n\nThis modifies the listed external .mat assets. The original BRRES is not changed. "+
                "You can undo the change in Unity or source control.",
                "Update "+candidates.Count+" .mat Slot(s)","Cancel"))return;
            int updated=0,skipped=0;
            for(int i=0;i<candidates.Count;i++)
            {
                BrresMaterialEntry entry=candidates[i];
                Material material=targetMaterials[i];
                Undo.RecordObject(material,"Apply BRRES _MM Bump");
                if(BrresBumpMapUtility.Assign(material,entry.sourceBumpTexture,
                    entry.generatedNormalTexture,importer.bumpMode,importer.bumpStrength))
                { EditorUtility.SetDirty(material);updated++; }
                else skipped++;
            }
            if(updated>0)AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(updated>0?"Bump mapping copied":"Nothing applied",
                "External .mat slots updated: "+updated+"; unexpected failures: "+skipped+
                "\n\n"+breakdown+
                "\n\nThese are approximate Unity normal/parallax maps, not original Wii TEV shaders.",
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
