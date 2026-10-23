// Generates Unity Generic/Humanoid Avatar sub-assets from an imported NW4R bone tree.
// Editor-only. Invalid or non-humanoid models never prevent BRRES geometry from importing.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusafirEx.Brres
{
    internal static class BrresRigBuilder
    {
        internal static Avatar Build(GameObject modelRoot, BrresNativeImporter settings,
            List<string> warnings)
        {
            if(settings.avatarDefinition==BrresAvatarDefinition.CopyFromOtherAvatar)
            {
                Avatar external=settings.sourceAvatar;
                if(external==null || !external.isValid)
                {
                    warnings.Add("Rig: Copy From Other Avatar requires a valid Source Avatar.");
                    return null;
                }
                if(settings.rigType==BrresRigType.Humanoid && !external.isHuman)
                {
                    warnings.Add("Rig: Source Avatar is Generic, but Humanoid was requested.");
                    return null;
                }
                if(settings.rigType==BrresRigType.Generic && external.isHuman)
                {
                    warnings.Add("Rig: Source Avatar is Humanoid, but Generic was requested.");
                    return null;
                }
                return external; // Never add an external asset as our own sub-asset.
            }

            Transform parent=modelRoot.transform.parent;
            int sibling=modelRoot.transform.GetSiblingIndex();
            Avatar result=null;
            try
            {
                // Unity requires a topmost GameObject to build a Humanoid Avatar.
                // Restore the unchanged model hierarchy immediately afterwards.
                modelRoot.transform.SetParent(null,false);
                if(settings.rigType==BrresRigType.Humanoid)
                {
                    HumanDescription human;
                    if(!TryMakeHumanDescription(modelRoot.transform,settings,warnings,out human))
                        return null;
                    result=AvatarBuilder.BuildHumanAvatar(modelRoot,human);
                    if(result==null || !result.isValid || !result.isHuman)
                    {
                        if(result!=null)UnityEngine.Object.DestroyImmediate(result);
                        warnings.Add("Rig: Unity rejected the Humanoid Avatar. Check the model rest pose, human mappings and bone orientations. Model and CHR0 assets were still imported.");
                        return null;
                    }
                }
                else
                {
                    string motion=settings.genericRootBone;
                    if(!string.IsNullOrEmpty(motion) && FindTransform(modelRoot.transform,motion)==null)
                    {
                        warnings.Add("Rig: Generic Root Bone '"+motion+"' not found. Creating an Avatar with root motion disabled.");
                        motion=string.Empty;
                    }
                    result=AvatarBuilder.BuildGenericAvatar(modelRoot,motion??string.Empty);
                    if(result==null || !result.isValid)
                    {
                        if(result!=null)UnityEngine.Object.DestroyImmediate(result);
                        warnings.Add("Rig: Unity could not build a Generic Avatar for '"+modelRoot.name+"'.");
                        return null;
                    }
                }
                result.name=modelRoot.name+"_Avatar";
                return result;
            }
            catch(Exception ex)
            {
                if(result!=null)UnityEngine.Object.DestroyImmediate(result);
                warnings.Add("Rig: AvatarBuilder failed for '"+modelRoot.name+"': "+ex.Message);
                return null;
            }
            finally
            {
                modelRoot.transform.SetParent(parent,false);
                modelRoot.transform.SetSiblingIndex(sibling);
            }
        }

        private static bool TryMakeHumanDescription(Transform root,
            BrresNativeImporter s,List<string> warnings,out HumanDescription human)
        {
            human=new HumanDescription();
            var mapped=new List<HumanBone>();
            var names=new HashSet<string>(StringComparer.Ordinal);
            var missing=new List<string>();
            // HumanTrait provides the exact humanoid names expected by Unity.
            Add(mapped,names,missing,root,HumanBodyBones.Hips,s.humanHips,true);
            Add(mapped,names,missing,root,HumanBodyBones.Spine,s.humanSpine,true);
            Add(mapped,names,missing,root,HumanBodyBones.Chest,s.humanChest,false);
            Add(mapped,names,missing,root,HumanBodyBones.Neck,s.humanNeck,false);
            Add(mapped,names,missing,root,HumanBodyBones.Head,s.humanHead,true);

            Add(mapped,names,missing,root,HumanBodyBones.LeftUpperArm,s.humanLeftUpperArm,true);
            Add(mapped,names,missing,root,HumanBodyBones.LeftLowerArm,s.humanLeftLowerArm,true);
            Add(mapped,names,missing,root,HumanBodyBones.LeftHand,s.humanLeftHand,true);
            Add(mapped,names,missing,root,HumanBodyBones.RightUpperArm,s.humanRightUpperArm,true);
            Add(mapped,names,missing,root,HumanBodyBones.RightLowerArm,s.humanRightLowerArm,true);
            Add(mapped,names,missing,root,HumanBodyBones.RightHand,s.humanRightHand,true);
            Add(mapped,names,missing,root,HumanBodyBones.LeftUpperLeg,s.humanLeftUpperLeg,true);
            Add(mapped,names,missing,root,HumanBodyBones.LeftLowerLeg,s.humanLeftLowerLeg,true);
            Add(mapped,names,missing,root,HumanBodyBones.LeftFoot,s.humanLeftFoot,true);
            Add(mapped,names,missing,root,HumanBodyBones.RightUpperLeg,s.humanRightUpperLeg,true);
            Add(mapped,names,missing,root,HumanBodyBones.RightLowerLeg,s.humanRightLowerLeg,true);
            Add(mapped,names,missing,root,HumanBodyBones.RightFoot,s.humanRightFoot,true);
            Add(mapped,names,missing,root,HumanBodyBones.LeftShoulder,s.humanLeftShoulder,false);
            Add(mapped,names,missing,root,HumanBodyBones.RightShoulder,s.humanRightShoulder,false);
            if(missing.Count!=0)
            {
                warnings.Add("Rig: Humanoid missing required mappings: "+string.Join(", ",missing.ToArray()));
                return false;
            }
            var skeleton=new List<SkeletonBone>();
            // Include all ancestors as well as attachment bones. Mesh-renderer-only
            // children aren't part of the skeleton and are deliberately excluded.
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t!=root && t.GetComponent<Renderer>()!=null)continue;
                skeleton.Add(new SkeletonBone{
                    name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale
                });
            }
            human.human=mapped.ToArray();
            human.skeleton=skeleton.ToArray();
            human.upperArmTwist=0.5f;
            human.lowerArmTwist=0.5f;
            human.upperLegTwist=0.5f;
            human.legTwist=0.5f;
            human.armStretch=0.05f;
            human.legStretch=0.05f;
            human.feetSpacing=0f;
            human.hasTranslationDoF=false;
            human.rootMotionBoneName=s.humanHips;
            return true;
        }

        private static void Add(List<HumanBone> mapped,HashSet<string> names,
            List<string> missing,Transform root,HumanBodyBones target,string bone,bool required)
        {
            if(string.IsNullOrEmpty(bone))
            {
                if(required)missing.Add(HumanTrait.BoneName[(int)target]);
                return;
            }
            Transform match=FindTransform(root,bone);
            if(match==null)
            {
                if(required)missing.Add(HumanTrait.BoneName[(int)target]+" ("+bone+")");
                return;
            }
            if(!names.Add(bone))
            {
                if(required)missing.Add("duplicate: "+bone);
                return;
            }
            var entry=new HumanBone{humanName=HumanTrait.BoneName[(int)target],boneName=match.name};
            HumanLimit limit=new HumanLimit();
            limit.useDefaultValues=true;
            entry.limit=limit;
            mapped.Add(entry);
        }

        private static Transform FindTransform(Transform root,string bone)
        {
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(string.Equals(t.name,bone,StringComparison.Ordinal))return t;
            return null;
        }
    }
}
#endif
