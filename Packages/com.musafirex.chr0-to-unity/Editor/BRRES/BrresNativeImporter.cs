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

namespace MusafirEx.Brres
{
    [ScriptedImporter(1, new[] { "brres", "mdl0" })]
    public sealed class BrresNativeImporter : ScriptedImporter
    {
        [Tooltip("Convert Wii right-handed X into Unity left-handed X.")]
        public bool flipX = true;
        [Tooltip("Flip source UV V coordinate to match imported Wii textures.")]
        public bool flipUV = true;
        [Tooltip("Model unit scale (1 preserves the original BRRES / MDL0 units).")]
        public float unitsToMeters = 1f;
        [Tooltip("Import TEX0 images from BRRES into Unity Texture2D sub-assets.")]
        public bool importTextures = true;
        [Tooltip("Create a skinned renderer when the source has weighted bone/node data; otherwise create a static MeshRenderer.")]
        public bool importSkinning = true;

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
                if(modelResources.Count == 0)
                    throw new InvalidDataException("No MDL0 resource found inside BRRES archive");

                var root = new GameObject(Path.GetFileNameWithoutExtension(context.assetPath));
                context.AddObjectToAsset("root",root);
                context.SetMainObject(root);

                var textures = importTextures ? parser.LoadTextures(records,warnings) : new Dictionary<string,Texture2D>(StringComparer.OrdinalIgnoreCase);
                int texIndex=0;
                foreach(var kv in textures)
                {
                    kv.Value.name=kv.Key;
                    context.AddObjectToAsset("tex_"+(texIndex++)+"_"+Safe(kv.Key),kv.Value);
                }
                int assetSerial=0;
                foreach(var resource in modelResources)
                {
                    var parsed=parser.ReadModel(resource,warnings);
                    var modelRoot=new GameObject(string.IsNullOrEmpty(parsed.Name)?"MDL0":parsed.Name);
                    modelRoot.transform.SetParent(root.transform,false);
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
                        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
                        if(shader==null)shader=Shader.Find("Standard");
                        if(shader==null)shader=Shader.Find("Unlit/Texture");
                        if(shader==null)throw new InvalidOperationException("Unity material shader not available");
                        var mat=new Material(shader);
                        mat.name=parsed.MaterialNames[i]??("Material_"+i);
                        Texture2D tex;
                        string textureName=i<parsed.MaterialTextures.Count?parsed.MaterialTextures[i]:null;
                        if(!string.IsNullOrEmpty(textureName)&&textures.TryGetValue(textureName,out tex))
                        {
                            if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",tex);
                            if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);
                        }
                        context.AddObjectToAsset("mat_"+assetSerial+"_"+i+"_"+Safe(mat.name),mat);
                        materials[i]=mat;
                    }
                    for(int i=0;i<parsed.Meshes.Count;i++)
                    {
                        var src=parsed.Meshes[i];
                        var mesh=new Mesh();
                        mesh.name=src.Name;
                        if(src.Positions.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                        mesh.SetVertices(src.Positions);
                        // Basis reflection reverses handedness; reverse triangle winding accordingly.
                        int[] indices=src.Triangles.ToArray();
                        if(flipX)for(int f=0;f+2<indices.Length;f+=3)
                        {int tmp=indices[f+1];indices[f+1]=indices[f+2];indices[f+2]=tmp;}
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