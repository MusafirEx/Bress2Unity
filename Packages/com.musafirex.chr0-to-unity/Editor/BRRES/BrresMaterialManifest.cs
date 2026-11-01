// Material provenance for BRRES/MDL0 imports. Kept as a sub-asset so the
// custom Materials Inspector can distinguish duplicate names across models.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusafirEx.Brres
{
    [Serializable]
    public sealed class BrresMaterialEntry
    {
        public string key;
        public string sourceName;
        public string modelName;
        public string textureName;
        public int modelIndex;
        public int materialIndex;
        public Material generatedMaterial;
        public string suggestedBumpTextureName;
        public string appliedBumpTextureName;
        public Texture2D sourceBumpTexture;
        public Texture2D generatedNormalTexture;
        public string bumpStatus;
    }

    [Serializable]
    public sealed class BrresBumpSlotOverride
    {
        public string key;
        // Null/empty => auto strict stem-match. __BRRES_NO_BUMP__ => explicitly disabled.
        public string sourceTextureName;
    }

    [Serializable]
    public sealed class BrresMaterialOverride
    {
        public string key;
        public Material material;
    }

    // Unity requires a ScriptableObject used as an imported sub-asset to
    // have a matching public class and script file name.
    public sealed class BrresMaterialManifest : ScriptableObject
    {
        public List<BrresMaterialEntry> entries = new List<BrresMaterialEntry>();
        public List<string> availableMmTextures = new List<string>();
    }
}
#endif
