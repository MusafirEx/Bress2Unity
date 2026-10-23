// Unity requires ScriptableObject asset classes to have a matching script filename.
// MOT manifest sub-assets share the .mot importer assembly and capture per-clip diagnostics.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusafirEx.Mot
{
    [Serializable]
    public sealed class MotSlotInfo
    {
        public string name;
        public int slot;
        public int byteLength;
        public bool clipGenerated;
        public string report;
    }

    public sealed class MotArchiveInfo : ScriptableObject
    {
        public string source;
        public string rig;
        public int slotCount;
        public int exportedClips;
        public List<MotSlotInfo> slots = new List<MotSlotInfo>();
    }
}
#endif
