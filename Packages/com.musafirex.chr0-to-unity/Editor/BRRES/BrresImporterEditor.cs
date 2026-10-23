// FBX-like Model / Rig / Animations tabs for Bress2Unity's native ScriptedImporter.
// The imported object remains a normal Unity GameObject prefab with Avatar sub-assets.
#if UNITY_EDITOR
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
        private static readonly string[] Tabs={"Model","Rig","Animations"};

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            activeTab=GUILayout.Toolbar(activeTab,Tabs);
            EditorGUILayout.Space(7f);
            if(activeTab==0)DrawModel();
            else if(activeTab==1)DrawRig();
            else DrawAnimation();
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
