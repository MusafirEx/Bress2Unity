// Native Unity project importer for Wii BRRES and MDL0. No external converter process.
// Unity AssetImportContext owns all sub-assets (meshes, textures and materials).
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Animations;

namespace MusafirEx.Brres
{
    public enum BrresRigType { None=0, Legacy=1, Generic=2, Humanoid=3 }
    public enum BrresAvatarDefinition { CreateFromThisModel=0, CopyFromOtherAvatar=1 }

    [ScriptedImporter(5, new[] { "brres", "mdl0" })]
    public sealed class BrresNativeImporter : ScriptedImporter
    {
        [Tooltip("Convert Wii right-handed X into Unity left-handed X.")]
        public bool flipX = true;
        [Tooltip("Manually reverse triangle faces AFTER automatic correction. Leave disabled normally; enable only for unusual BRRES files still facing the wrong way.")]
        public bool reverseFaceWinding = false;
        [Tooltip("Flip source UV V coordinate to match imported Wii textures.")]
        public bool flipUV = true;
        [Tooltip("Model unit scale (1 preserves the original BRRES / MDL0 units).")]
        public float unitsToMeters = 1f;
        [Tooltip("Import TEX0 images from BRRES into Unity Texture2D sub-assets.")]
        public bool importTextures = true;
        [Tooltip("Create a skinned renderer when the source has weighted bone/node data; otherwise create a static MeshRenderer.")]
        public bool importSkinning = true;

        [Header("Materials")]
        [Tooltip("Import approximate Unity materials derived from MDL0 and embedded TEX0 resources.")]
        public bool importMaterials = true;
        [Tooltip("Optional Unity shader for generated BRRES materials. Leave unset to choose the Standard/URP/HDRP Lit shader automatically.")]
        public Shader materialShaderOverride;
        [Tooltip("Assign external Material assets to specific MDL0 material slots, keyed by model/material index.")]
        public List<BrresMaterialOverride> materialOverrides = new List<BrresMaterialOverride>();

        [Header("Rig")]
        [Tooltip("None: no Animator. Legacy: Animation component. Generic: Unity Generic Avatar. Humanoid: validated Humanoid Avatar if bone mapping/rest pose permit.")]
        public BrresRigType rigType = BrresRigType.Generic;
        [Tooltip("Create an Avatar from each imported model, or reuse an existing external Avatar.")]
        public BrresAvatarDefinition avatarDefinition = BrresAvatarDefinition.CreateFromThisModel;
        [Tooltip("Avatar asset to reuse when Avatar Definition = Copy From Other Avatar.")]
        public Avatar sourceAvatar;
        [Tooltip("Generic root-motion transform name. Y is common for NW4R skeletons; leave blank to disable root motion.")]
        public string genericRootBone = "Y";

        [Header("Humanoid Mapping (when Animation Type = Humanoid)")]
        [Tooltip("Source BRRES skeleton bone names. Humanoid generation requires an appropriate rest pose, not names alone.")]
        public string humanHips="hip";
        public string humanSpine="waist";
        public string humanChest="breast";
        public string humanNeck="neck";
        public string humanHead="head";
        public string humanLeftShoulder="L_collar";
        public string humanLeftUpperArm="L_harm";
        public string humanLeftLowerArm="L_larm";
        public string humanLeftHand="L_hand";
        public string humanRightShoulder="R_collar";
        public string humanRightUpperArm="R_harm";
        public string humanRightLowerArm="R_larm";
        public string humanRightHand="R_hand";
        public string humanLeftUpperLeg="L_hleg";
        public string humanLeftLowerLeg="L_lleg";
        public string humanLeftFoot="L_foot";
        public string humanRightUpperLeg="R_hleg";
        public string humanRightLowerLeg="R_lleg";
        public string humanRightFoot="R_foot";
        [Tooltip("Import embedded CHR0 animations as Unity AnimationClip sub-assets.")]
        public bool importAnimations = true;
        [Tooltip("Animation rig for separate, animation-only BRRES. Assign a model prefab asset for correctly mapped bones.")]
        public GameObject animationRig;
        [Tooltip("Frame rate for embedded CHR0 AnimationClips.")]
        public int animationFps = 60;
        [Tooltip("Coordinate basis of the skeleton, 0=Flip X, 1=Flip Z, 2=None.")]
        public int animationAxis = 0;
        [Tooltip("Respect source CHR0 looping flags.")]
        public bool animationLoop = true;

        public override void OnImportAsset(AssetImportContext context)
        {
            var warnings = new List<string>();
            try
            {
                byte[] source = File.ReadAllBytes(context.assetPath);
                if(source.Length > 512*1024*1024)
                    throw new InvalidDataException("Resource exceeds 512 MB import safety limit");
                var parser = new BrresMdl0Reader(source, flipX, flipUV, Mathf.Clamp(unitsToMeters,0.000001f,10000f));
                var records = parser.FindResources();
                var modelResources = records.FindAll(r => r.Magic == "MDL0");
                var chr0Resources = records.FindAll(r => r.Magic == "CHR0");
                if(modelResources.Count == 0 && chr0Resources.Count == 0)
                    throw new InvalidDataException("No MDL0 or CHR0 resource found inside BRRES archive");

                var root = new GameObject(Path.GetFileNameWithoutExtension(context.assetPath));
                context.AddObjectToAsset("root",root);
                context.SetMainObject(root);

                // Keep stable material slot keys for the Materials Inspector.
                // A manifest is also generated for BRRES files with no models.
                var materialManifest=ScriptableObject.CreateInstance<BrresMaterialManifest>();
                materialManifest.name=root.name+"_Materials";
                context.AddObjectToAsset("brres_materials",materialManifest);

                var textures = importTextures ? parser.LoadTextures(records,warnings) : new Dictionary<string,Texture2D>(StringComparer.OrdinalIgnoreCase);
                int texIndex=0;
                foreach(var kv in textures)
                {
                    kv.Value.name=kv.Key;
                    context.AddObjectToAsset("tex_"+(texIndex++)+"_"+Safe(kv.Key),kv.Value);
                }
                if(importMaterials && materialShaderOverride!=null)
                {
                    string shaderPath=AssetDatabase.GetAssetPath(materialShaderOverride);
                    if(!string.IsNullOrEmpty(shaderPath) && shaderPath!=context.assetPath)
                        context.DependsOnSourceAsset(shaderPath);
                }
                var modelRoots = new List<GameObject>();
                int automaticallyCorrectedMeshes=0;
                int assetSerial=0;
                foreach(var resource in modelResources)
                {
                    var parsed=parser.ReadModel(resource,warnings);
                    var modelRoot=new GameObject(string.IsNullOrEmpty(parsed.Name)?"MDL0":parsed.Name);
                    modelRoot.transform.SetParent(root.transform,false);
                    modelRoots.Add(modelRoot);
                    var boneObjects=new Transform[parsed.Joints.Count];
                    for(int i=0;i<parsed.Joints.Count;i++)
                    {
                        var info=parsed.Joints[i];
                        var obj=new GameObject(string.IsNullOrEmpty(info.Name)?"Bone_"+i:info.Name);
                        boneObjects[i]=obj.transform;
                    }
                    // Keep original hierarchy names for existing CHR0 → .anim binding.
                    for(int i=0;i<parsed.Joints.Count;i++)
                    {
                        int parent=parsed.Joints[i].Parent;
                        boneObjects[i].SetParent(parent>=0&&parent<boneObjects.Length&&parent!=i?boneObjects[parent]:modelRoot.transform,false);
                    }
                    for(int i=0;i<parsed.Joints.Count;i++)
                    {
                        var info=parsed.Joints[i];
                        var t=boneObjects[i];
                        t.localPosition=info.Position;
                        t.localRotation=info.Rotation;
                        t.localScale=info.Scale;
                    }
                    var bindPoses=new Matrix4x4[boneObjects.Length];
                    for(int i=0;i<boneObjects.Length;i++)
                        bindPoses[i]=boneObjects[i].worldToLocalMatrix * modelRoot.transform.localToWorldMatrix;

                    var materials=new Material[parsed.MaterialNames.Count];
                    for(int i=0;i<materials.Length;i++)
                    {
                        string materialName=parsed.MaterialNames[i]??("Material_"+i);
                        string slotKey="mat_"+assetSerial+"_"+i+"_"+Safe(materialName);
                        string textureName=i<parsed.MaterialTextures.Count?parsed.MaterialTextures[i]:null;
                        Material generated=null;
                        if(importMaterials)
                        {
                            Shader shader=materialShaderOverride;
                            if(shader==null)
                            {
                                var pipeline=GraphicsSettings.currentRenderPipeline;
                                if(pipeline==null)shader=Shader.Find("Standard");
                                else if(pipeline.GetType().Name.Contains("HDRenderPipeline"))shader=Shader.Find("HDRP/Lit");
                                else shader=Shader.Find("Universal Render Pipeline/Lit");
                                if(shader==null)shader=Shader.Find("Standard");
                                if(shader==null)shader=Shader.Find("Unlit/Texture");
                            }
                            if(shader==null)throw new InvalidOperationException("Unity material shader not available");
                            generated=new Material(shader);
                            generated.name=materialName;
                            Texture2D tex;
                            if(!string.IsNullOrEmpty(textureName)&&textures.TryGetValue(textureName,out tex))
                            {
                                if(generated.HasProperty("_BaseMap"))generated.SetTexture("_BaseMap",tex);
                                if(generated.HasProperty("_MainTex"))generated.SetTexture("_MainTex",tex);
                            }
                            // Keep the identifier from older versions to preserve
                            // references to imported Material sub-assets.
                            context.AddObjectToAsset(slotKey,generated);
                        }
                        materialManifest.entries.Add(new BrresMaterialEntry {
                            key=slotKey, sourceName=materialName,
                            modelName=modelRoot.name, modelIndex=assetSerial,
                            materialIndex=i, textureName=textureName,
                            generatedMaterial=generated
                        });
                        Material assigned=generated;
                        if(importMaterials && materialOverrides!=null)
                        {
                            foreach(var entry in materialOverrides)
                            {
                                if(entry==null || entry.key!=slotKey || entry.material==null)continue;
                                string externalPath=AssetDatabase.GetAssetPath(entry.material);
                                if(!string.IsNullOrEmpty(externalPath) && externalPath!=context.assetPath)
                                {
                                    context.DependsOnSourceAsset(externalPath);
                                    assigned=entry.material;
                                }
                                else warnings.Add("Material "+materialName+": external override must be an asset outside this BRRES file.");
                                break;
                            }
                        }
                        materials[i]=assigned;
                    }
                    for(int i=0;i<parsed.Meshes.Count;i++)
                    {
                        var src=parsed.Meshes[i];
                        var mesh=new Mesh();
                        mesh.name=src.Name;
                        if(src.Positions.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                        mesh.SetVertices(src.Positions);
                        // First account for the handedness reflection. The source GX
                        // primitive decoder already reverses Wii's hardware winding.
                        int[] indices=src.Triangles.ToArray();
                        if(flipX) ReverseTriangleWinding(indices);
                        // Some Wii titles store geometry with the opposite face winding
                        // from their AUTHORED normals (e.g. the user's 0000.brres), while
                        // others are already correct (e.g. axe001.brres). Detect this
                        // per mesh, instead of forcing a global reversal on all BRRES.
                        if(src.HasNormal && NeedsFaceWindingCorrection(src.Positions,src.Normals,indices))
                        {
                            ReverseTriangleWinding(indices);
                            automaticallyCorrectedMeshes++;
                        }
                        // Explicit Inspector override for unusual assets only.
                        if(reverseFaceWinding) ReverseTriangleWinding(indices);
                        mesh.SetTriangles(indices,0,true);
                        if(src.HasUV)mesh.SetUVs(0,src.UV);
                        if(src.HasNormal)mesh.SetNormals(src.Normals);
                        else mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                        var obj=new GameObject(src.Name);
                        obj.transform.SetParent(modelRoot.transform,false);
                        Material material=src.MaterialIndex>=0&&src.MaterialIndex<materials.Length?materials[src.MaterialIndex]:(materials.Length>0?materials[0]:null);
                        bool weighted=importSkinning && boneObjects.Length>0 && src.Weights.Exists(w=>w.weight0>0.00001f);
                        if(weighted)
                        {
                            mesh.boneWeights=src.Weights.ToArray();
                            mesh.bindposes=bindPoses;
                            var renderer=obj.AddComponent<SkinnedMeshRenderer>();
                            renderer.sharedMesh=mesh;
                            renderer.bones=boneObjects;
                            renderer.rootBone=boneObjects[0];
                            renderer.sharedMaterial=material;
                            renderer.updateWhenOffscreen=true;
                            renderer.localBounds=mesh.bounds;
                        }
                        else
                        {
                            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
                        }
                        context.AddObjectToAsset("mesh_"+assetSerial+"_"+i+"_"+Safe(mesh.name),mesh);
                    }
                    assetSerial++;
                }
                // Build one Avatar per imported MDL0 model. Keep animation bindings
                // relative to modelRoot, matching the existing CHR0 importer.
                if(modelRoots.Count==0 && rigType!=BrresRigType.None)
                    warnings.Add("Rig: This BRRES contains no MDL0 skeleton. Rig configuration is ignored for animation-only BRRES.");
                for(int ri=0;ri<modelRoots.Count;ri++)
                {
                    GameObject rigRoot=modelRoots[ri];
                    if(rigType==BrresRigType.None)continue;
                    if(rigType==BrresRigType.Legacy)
                    {
                        var anim=rigRoot.AddComponent<Animation>();
                        anim.playAutomatically=false;
                        continue;
                    }
                    if(avatarDefinition==BrresAvatarDefinition.CopyFromOtherAvatar && sourceAvatar!=null)
                    {
                        string sourcePath=AssetDatabase.GetAssetPath(sourceAvatar);
                        if(!string.IsNullOrEmpty(sourcePath) && sourcePath!=context.assetPath)
                            context.DependsOnSourceAsset(sourcePath);
                    }
                    Avatar avatar=BrresRigBuilder.Build(rigRoot,this,warnings);
                    if(avatar==null)continue;
                    if(avatarDefinition==BrresAvatarDefinition.CreateFromThisModel)
                    {
                        avatar.name=rigRoot.name+"_Avatar_"+ri;
                        context.AddObjectToAsset("avatar_"+ri,avatar);
                    }
                    Animator animator=rigRoot.AddComponent<Animator>();
                    animator.avatar=avatar;
                    animator.applyRootMotion=false; // Preserve previous CHR0 transform behavior.
                }
                if(importAnimations)
                {
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int clipSerial = 0;
                    foreach(var resource in chr0Resources)
                    {
                        try
                        {
                            byte[] raw = parser.ReadChr0WithSharedStrings(resource);
                            GameObject rig = animationRig;
                            if(rig == null && modelRoots.Count > 0)
                            {
                                int best = 0;
                                foreach(var candidate in modelRoots)
                                {
                                    int matches = CHR0ToUnityAnimConverter.CountEmbeddedBoneMatches(raw,candidate);
                                    if(matches > best) { best=matches; rig=candidate; }
                                }
                            }
                            string clipName = string.IsNullOrEmpty(resource.Name)?"CHR0_"+clipSerial:resource.Name;
                            string unique = clipName;
                            for(int suffix=1;!usedNames.Add(unique);suffix++) unique=clipName+"_"+suffix;
                            int mapped;
                            var clip = CHR0ToUnityAnimConverter.CreateEmbeddedAnimation(raw,unique,rig,
                                Mathf.Clamp(animationFps,1,120),Mathf.Clamp(animationAxis,0,2),animationLoop,out mapped);
                            // Legacy Animation requires legacy clips; Mecanim Generic and
                            // Humanoid use non-legacy clips as before.
                            if(rigType==BrresRigType.Legacy)
                            {
                                clip.legacy=true;
                                if(rig!=null && modelRoots.Contains(rig))
                                {
                                    Animation animation=rig.GetComponent<Animation>();
                                    if(animation!=null)
                                    {
                                        animation.AddClip(clip,unique);
                                        if(animation.clip==null)animation.clip=clip;
                                    }
                                }
                            }
                            context.AddObjectToAsset("chr0_"+clipSerial+"_"+Safe(unique),clip);
                            if(rig == null)
                                warnings.Add("CHR0 " + unique + ": animation-only BRRES; generic one-level bone curves imported. " +
                                    "For a usable rig animation, use Tools > MH > CHR0 from BRRES and choose the matching model in the Hierarchy.");
                            else warnings.Add("CHR0 " + unique + ": " + mapped + " bone names matched " + rig.name);
                            clipSerial++;
                        }
                        catch(Exception ex)
                        {
                            warnings.Add("CHR0 " + resource.Name + " was not imported: " + ex.Message);
                        }
                    }
                }
                if(automaticallyCorrectedMeshes>0)
                    warnings.Add("BRRES: automatically corrected inverted triangle faces on " + automaticallyCorrectedMeshes + " mesh(es) by comparing triangle winding against authored vertex normals.");
                foreach(var warning in warnings)context.LogImportWarning(warning);
                context.LogImportWarning("BRRES importer is beta: palette TEX0/PLT0, full GX TEV materials and uncommon vertex formats are not supported. Visually validate mesh/rig before production use.");
            }
            catch(Exception ex)
            {
                context.LogImportError("Native BRRES/MDL0 import failed: "+ex);
                // Import failures remain explicit; do not silently return an empty prefab.
                throw;
            }
        }
        private static void ReverseTriangleWinding(int[] triangles)
        {
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                int swap=triangles[i+1];
                triangles[i+1]=triangles[i+2];
                triangles[i+2]=swap;
            }
        }

        // Positive geometric normal should agree with the three authored vertex
        // normals. Ignore degenerate/ambiguous faces and require strong consensus.
        // This preserves models with already-correct facing and repairs models
        // whose hardware winding is backwards relative to their vertex normals.
        private static bool NeedsFaceWindingCorrection(List<Vector3> positions,
            List<Vector3> normals,int[] triangles)
        {
            if(normals==null || normals.Count!=positions.Count) return false;
            int aligned=0,opposed=0;
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];
                if(ia<0||ib<0||ic<0||ia>=positions.Count||ib>=positions.Count||ic>=positions.Count)
                    continue;
                Vector3 geometric=Vector3.Cross(positions[ib]-positions[ia],positions[ic]-positions[ia]);
                Vector3 authored=normals[ia]+normals[ib]+normals[ic];
                float threshold=geometric.magnitude*authored.magnitude*0.15f;
                if(threshold<=1e-12f) continue;
                float dot=Vector3.Dot(geometric,authored);
                if(dot>threshold) aligned++;
                else if(dot< -threshold) opposed++;
            }
            return opposed>=8 && opposed>aligned*4;
        }

        private static string Safe(string name)
        {
            if(string.IsNullOrWhiteSpace(name))return "unnamed";
            char[] a=name.ToCharArray();
            for(int i=0;i<a.Length;i++)if(!(char.IsLetterOrDigit(a[i])||a[i]=='_'||a[i]=='-'))a[i]='_';
            return new string(a);
        }
    }
}
#endif