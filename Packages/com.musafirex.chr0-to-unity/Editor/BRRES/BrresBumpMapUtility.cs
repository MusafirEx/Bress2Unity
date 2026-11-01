// Read-only identification of TEX0 _MM names and non-destructive height-to-normal conversion.
// BRRES original grayscale TEX0 remains an independently imported texture sub-asset.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusafirEx.Brres
{
    internal static class BrresBumpMapUtility
    {
        internal const string NoBumpOverride="__BRRES_NO_BUMP__";

        internal static bool IsMm(string textureName)
        {
            return !string.IsNullOrEmpty(textureName) &&
                   textureName.EndsWith("_MM",StringComparison.OrdinalIgnoreCase);
        }

        internal static string BestMatch(string materialName,string diffuseTextureName,
            Dictionary<string,Texture2D> sourceTextures)
        {
            if(sourceTextures==null || sourceTextures.Count==0)return null;
            // Name matching is a hypothesis, NOT proof of the Wii TEV connection.
            // Do not guess from a single unmatched _MM texture in the archive.
            var stems=new List<string>();
            AddStem(stems,diffuseTextureName);
            AddStem(stems,materialName);
            foreach(string stem in stems)
            {
                string candidate=stem+"_MM";
                foreach(var pair in sourceTextures)
                    if(IsMm(pair.Key)&&string.Equals(pair.Key,candidate,StringComparison.OrdinalIgnoreCase))
                        return pair.Key;
            }
            return null;
        }

        private static void AddStem(List<string> stems,string source)
        {
            if(string.IsNullOrEmpty(source))return;
            source=source.Trim();
            if(source.EndsWith("_BM",StringComparison.OrdinalIgnoreCase) ||
               source.EndsWith("_MM",StringComparison.OrdinalIgnoreCase) ||
               source.EndsWith("_SM",StringComparison.OrdinalIgnoreCase))
                source=source.Substring(0,source.Length-3);
            if(source.Length>0 && !stems.Contains(source))stems.Add(source);
        }

        internal static Texture2D HeightToNormal(Texture2D grayscale,float strength,bool invertHeight)
        {
            if(grayscale==null)throw new ArgumentNullException(nameof(grayscale));
            int width=grayscale.width,height=grayscale.height;
            if(width<1 || height<1 || (long)width*height>16777216)
                throw new InvalidOperationException("Invalid bump texture dimensions.");
            // Only the _MM TEX0 sources are kept readable during import.
            Color32[] input=grayscale.GetPixels32();
            if(input==null || input.Length!=width*height)
                throw new InvalidOperationException("Unreadable _MM source; enable texture import.");
            var intensities=new float[input.Length];
            for(int i=0;i<input.Length;i++)
            {
                Color32 c=input[i];
                float v=(0.2126f*c.r+0.7152f*c.g+0.0722f*c.b)/255f;
                intensities[i]=invertHeight?1f-v:v;
            }
            var output=new Color32[input.Length];
            strength=Mathf.Clamp(strength,0f,8f);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                // Repeat wrap, consistent with Wii repeat TEX0 default and UV tiling.
                int xm=(x+width-1)%width,xp=(x+1)%width;
                int ym=(y+height-1)%height,yp=(y+1)%height;
                float dx=(intensities[y*width+xp]-intensities[y*width+xm])*0.5f;
                float dy=(intensities[yp*width+x]-intensities[ym*width+x])*0.5f;
                Vector3 normal=new Vector3(-dx*strength,-dy*strength,1f).normalized;
                output[y*width+x]=new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(normal.x*0.5f+0.5f)*255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(normal.y*0.5f+0.5f)*255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(normal.z*0.5f+0.5f)*255f),255);
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,true)
            {
                name=grayscale.name+"_GeneratedNormal",
                wrapMode=grayscale.wrapMode,
                filterMode=grayscale.filterMode,
                anisoLevel=grayscale.anisoLevel
            };
            texture.SetPixels32(output);
            texture.Apply(true,true);
            return texture;
        }

        internal static bool Assign(Material generated,Texture2D grayscale,
            Texture2D generatedNormal,BrresBumpMode mode,float strength)
        {
            if(generated==null)return false;
            if(mode==BrresBumpMode.HeightToNormal && generatedNormal!=null)
            {
                // Standard and URP Lit use _BumpMap; HDRP Lit uses _NormalMap.
                if(generated.HasProperty("_BumpMap"))
                {
                    generated.SetTexture("_BumpMap",generatedNormal);
                    if(generated.HasProperty("_BumpScale"))generated.SetFloat("_BumpScale",1f);
                    generated.EnableKeyword("_NORMALMAP");
                    return true;
                }
                if(generated.HasProperty("_NormalMap"))
                {
                    generated.SetTexture("_NormalMap",generatedNormal);
                    if(generated.HasProperty("_NormalScale"))generated.SetFloat("_NormalScale",1f);
                    generated.EnableKeyword("_NORMALMAP");
                    return true;
                }
            }
            else if(mode==BrresBumpMode.OriginalGrayscale && grayscale!=null)
            {
                // Optional Unity height/parallax approximation, not Wii TEV emulation.
                if(generated.HasProperty("_ParallaxMap"))
                {
                    generated.SetTexture("_ParallaxMap",grayscale);
                    if(generated.HasProperty("_Parallax"))
                        generated.SetFloat("_Parallax",Mathf.Clamp(strength*0.005f,0f,0.08f));
                    generated.EnableKeyword("_PARALLAXMAP");
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
