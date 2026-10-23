// Bress2Unity experimental import of Eighting MOT archives (GNTA animation sub-assets).
// Does not modify BRRES/MDL0 or CHR0 importers.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MusafirEx.Mot
{
    [Serializable]
    internal sealed class MotSlotInfo
    {
        public string name;
        public int slot;
        public int byteLength;
        public bool clipGenerated;
        public string report;
    }

    internal sealed class MotArchiveInfo : ScriptableObject
    {
        public string source;
        public string rig;
        public int slotCount;
        public int exportedClips;
        public List<MotSlotInfo> slots = new List<MotSlotInfo>();
    }

    [ScriptedImporter(1, "mot")]
    public sealed class MotNativeImporter : ScriptedImporter
    {
        [Tooltip("A BRRES/MDL0 prefab with matching bone names. When unset, a matching .brres beside the .mot is used if available.")]
        public GameObject animationRig;
        [Tooltip("Find a same-named .brres/.mdl0 asset beside the .mot when no Animation Rig is assigned.")]
        public bool autoDetectSiblingRig = true;
        [Tooltip("Generate Unity AnimationClip sub-assets for each occupied GNTA slot. If no rig is found, only an index is imported.")]
        public bool importAnimationClips = true;
        [Range(15,120)] public int framesPerSecond = 60;
        [Tooltip("0 Flip X (native BRRES / current glTFast); 1 Flip Z; 2 preserve raw axes.")]
        public int coordinateAxis = 0;
        [Tooltip("MOT track translations multiplied by this number.")]
        public float translationScale = 1f;
        [Tooltip("Apply translation values as offsets from the model bind position.")]
        public bool relativeTranslation = true;
        [Tooltip("Experimental: 0 skip ambiguous 0x0202 rotations (safe default), 1 X, 2 Y, 3 Z local-axis approximation.")]
        public int rotation2DMode = 0;
        [Tooltip("Experimental: treat four component 0x0204 rotations as axis-angle values. Disable to ignore them.")]
        public bool experimentalRotation4D = false;
        [Tooltip("0 = absolute; 1 = bind rotation * decoded rotation; 2 = decoded rotation * bind rotation.")]
        public int rotationComposition = 1;
        [Tooltip("Attach tracks for secondary/effect bones when mappings are present.")]
        public bool includeAttachments = true;

        public override void OnImportAsset(AssetImportContext context)
        {
            var archive = ScriptableObject.CreateInstance<MotArchiveInfo>();
            archive.name = Path.GetFileNameWithoutExtension(context.assetPath) + "_MOT";
            archive.source = context.assetPath;
            context.AddObjectToAsset("mot_manifest", archive);
            context.SetMainObject(archive);
            try
            {
                var bytes = File.ReadAllBytes(context.assetPath);
                if (bytes.Length > 256 * 1024 * 1024) throw new InvalidDataException("MOT exceeds the 256 MiB safety limit");
                var units = global::MotGntaUnityConverter.ReadArchive(bytes, Path.GetFileName(context.assetPath));
                archive.slotCount = units.Count;
                GameObject rig = ResolveRig(context);
                archive.rig = rig == null ? "(none)" : AssetDatabase.GetAssetPath(rig);
                if (rig == null || !importAnimationClips)
                {
                    foreach (var u in units)
                        archive.slots.Add(new MotSlotInfo { name = Name(u.Slot), slot=u.Slot, byteLength=u.Data.Length, clipGenerated=false, report="Requires a compatible rig to create animation paths" });
                    if (importAnimationClips) context.LogImportWarning("MOT contains " + units.Count + " clips. Select Animation Rig in the .mot Inspector (or add same-stem .brres), then Reimport to generate .anim sub-assets.");
                    return;
                }
                Dictionary<string, Transform> targets;
                Dictionary<string, string> paths;
                MotRigMapping.Collect(rig.transform, out targets, out paths);
                var opts = MakeOptions();
                opts.Mapping = global::MotGntaUnityConverter.GetMapping(null);
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (var unit in units)
                {
                    var slot = new MotSlotInfo { name = Name(unit.Slot),slot=unit.Slot,byteLength=unit.Data.Length };
                    try
                    {
                        string report;
                        AnimationClip clip = global::MotGntaUnityConverter.Convert(unit,Path.GetFileNameWithoutExtension(context.assetPath), targets, paths, opts, true,out report);
                        slot.report = report;
                        if (clip != null)
                        {
                            clip.name = Name(unit.Slot);
                            string key = "anim_" + unit.Slot.ToString("X4");
                            if (!used.Add(key)) throw new InvalidDataException("Duplicate MOT slot " + unit.Slot);
                            context.AddObjectToAsset(key, clip);
                            slot.clipGenerated = true;
                            archive.exportedClips++;
                        }
                    }
                    catch(Exception ex) { slot.report=ex.Message;context.LogImportWarning(slot.name+": "+ex.Message); }
                    archive.slots.Add(slot);
                }
                if(archive.exportedClips==0)
                    context.LogImportWarning("No MOT clips generated. See MOT manifest and enable experimental rotation modes if needed; GNTA 0x0202 rotations are not fully decoded.");
                else
                    Debug.Log("[Bress2Unity MOT] " + context.assetPath + ": " + archive.exportedClips + "/" + units.Count + " GNTA clips imported using rig " + archive.rig);
            }
            catch(Exception ex)
            {
                context.LogImportError("MOT import failed for " + context.assetPath + ": " + ex.Message);
                throw;
            }
        }

        private GameObject ResolveRig(AssetImportContext context)
        {
            if(animationRig != null)
            {
                string path = AssetDatabase.GetAssetPath(animationRig);
                if(!string.IsNullOrEmpty(path) && path != context.assetPath) context.DependsOnSourceAsset(path);
                return animationRig;
            }
            if(!autoDetectSiblingRig) return null;
            string prefix = Path.ChangeExtension(context.assetPath,null);
            foreach(string ext in new[]{".brres", ".mdl0"})
            {
                string path = prefix + ext;
                if(!File.Exists(path))continue;
                context.DependsOnSourceAsset(path);
                var found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(found != null) return found;
            }
            return null;
        }
        private global::MotGntaUnityConverter.Options MakeOptions()
        {
            return new global::MotGntaUnityConverter.Options {
                FrameRate=Mathf.Clamp(framesPerSecond,15,120), Axis=Mathf.Clamp(coordinateAxis,0,2),
                PositionScale=translationScale,PositionRelative=relativeTranslation,PositionPlane2D=0,
                Rotate2D=Mathf.Clamp(rotation2DMode,0,3), AxisAngle4D=experimentalRotation4D,
                RotationComposition=Mathf.Clamp(rotationComposition,0,2),IncludeAttachments=includeAttachments
            };
        }
        private static string Name(int slot) { return "MOT_slot_"+slot.ToString("X4"); }
    }

    internal static class MotRigMapping
    {
        internal static void Collect(Transform root, out Dictionary<string, Transform> targets, out Dictionary<string, string> paths)
        {
            targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
            paths = new Dictionary<string, string>(StringComparer.Ordinal);
            // Bind relative to exactly the root supplied, including the prefab's nested model objects.
            Walk(root, "", targets,paths);
        }
        private static void Walk(Transform t,string path,Dictionary<string,Transform> targets,Dictionary<string,string> paths)
        {
            // Prefer a named model skeleton joint to any render-node aliases with the same name.
            if(!targets.ContainsKey(t.name)) { targets[t.name]=t; paths[t.name]=path; }
            foreach(Transform child in t)
            {
                string childPath=string.IsNullOrEmpty(path)?child.name:path+"/"+child.name;
                Walk(child,childPath,targets,paths);
            }
        }
    }
}
#endif