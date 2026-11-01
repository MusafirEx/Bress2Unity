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

        // Monster Hunter Tri commonly numbers each matching texture pair:
        // body_BM_01 -> body_MM_01, body_BM_02 -> body_MM_02.
        // Keep the older unnumbered _MM name supported as well.
        // The digits are compared literally: _01 must never pair with _02 or _1.
        private static bool IsAsciiDigits(string value,int start)
        {
            if(string.IsNullOrEmpty(value) || start>=value.Length)return false;
            for(int i=start;i<value.Length;i++)
                if(value[i]<'0'||value[i]>'9')return false;
            return true;
        }

        private static bool HasTextureSuffix(string name,string type)
        {
            if(string.IsNullOrEmpty(name))return false;
            string ending="_"+type;
            if(name.EndsWith(ending,StringComparison.OrdinalIgnoreCase))return true;
            int end=name.LastIndexOf('_');
            if(end<0 || !IsAsciiDigits(name,end+1))return false;
            return name.Substring(0,end).EndsWith(ending,StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsMm(string textureName)
        {
            return HasTextureSuffix(textureName,"MM");
        }

        internal static bool IsBm(string textureName)
        {
            return HasTextureSuffix(textureName,"BM");
        }

        // Returns only literal counterpart names; no fallback to other suffix numbers.
        // Examples: body_BM_01 => body_MM_01; body_BM => body_MM.
        internal static string MatchingMmName(string baseTextureName)
        {
            if(!IsBm(baseTextureName))return null;
            int last=baseTextureName.LastIndexOf('_');
            if(last>=0 && IsAsciiDigits(baseTextureName,last+1))
            {
                string prefix=baseTextureName.Substring(0,last);
                return prefix.Substring(0,prefix.Length-3)+"_MM"+baseTextureName.Substring(last);
            }
            return baseTextureName.Substring(0,baseTextureName.Length-3)+"_MM";
        }

        internal static string BestMatch(string materialName,string diffuseTextureName,
            Dictionary<string,Texture2D> sourceTextures)
        {
            if(sourceTextures==null || sourceTextures.Count==0)return null;
            // Use the actual MDL0 primary _BM texture first. A material name is
            // considered only if it already follows the exact _BM[_NN] convention.
            // TEV links remain unverified, so never guess by archive proximity or
            // by a suffix number without the same texture prefix.
            string match=FindMmCounterpart(diffuseTextureName,sourceTextures);
            if(match!=null)return match;
            return FindMmCounterpart(materialName,sourceTextures);
        }

        private static string FindMmCounterpart(string baseTextureName,
            Dictionary<string,Texture2D> sourceTextures)
        {
            string candidate=MatchingMmName(baseTextureName);
            if(candidate==null)return null;
            foreach(var pair in sourceTextures)
                if(IsMm(pair.Key) &&
                   string.Equals(pair.Key,candidate,StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            return null;
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
