// Standalone MOT/GNTA Workbench, NOT part of the main Bress2Unity package.
// Uses native Unity Editor curve bindings; no external Unity packages necessary.
// The container/track structure is derived from actual sample MOTs and the public
// Naruto-GNT-Modding MOT specification. 0x0202 rotation semantics are NOT confirmed:
// skip by default; experimental local-axis interpretation must be chosen explicitly.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class MotGntaUnityConverter
{
    internal sealed class Options
    {
        public int FrameRate = 60;
        public int Axis = 0;
        public float PositionScale = 1.0f;
        public bool PositionRelative = true;
        public int PositionPlane2D = 0;
        public int RotationComposition = 1;
        public int Rotate2D = 0;
        public bool AxisAngle4D = true;
        public bool AxisAngleRadians = false; // Raw W appears to be degrees; test only if needed.
        public bool IncludeAttachments = true;
        public Dictionary<int, string> Mapping;
    }

    internal sealed class ClipUnit
    {
        public int Slot;
        public byte[] Data;
    }

    private sealed class MotionTrack
    {
        public int BoneId, Components;
        public int Kind; // 1 = translation, 2 = scale, 8 = rotation
        public float[] Times;
        public Vector4[] Values;
    }

    private static readonly string[] StandardBones = {
        "Y", "X", "Z", "hip", "waist", "breast", "head", "R_collar", "R_harm", "R_larm",
        "R_hand", "L_collar", "L_harm", "L_larm", "L_hand", "R_hleg", "R_lleg", "R_foot",
        "L_hleg", "L_lleg", "L_foot", "neck"
    };

    internal static Dictionary<int, string> GetMapping(string csvPath)
    {
        var result = new Dictionary<int, string>();
        for (int i = 0; i < StandardBones.Length; i++) result[i] = StandardBones[i];
        // These extra IDs have plausible or exact names in the supplied 0000.glb.
        result[51] = "L_toe";
        result[150] = "w_150L";
        result[160] = "w_160R";
        if (string.IsNullOrWhiteSpace(csvPath)) return result;
        if (!File.Exists(csvPath)) throw new FileNotFoundException("Mapping CSV not found", csvPath);
        string[] lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 1) return result;
        string[] header = CsvFields(lines[0].TrimStart('\ufeff'));
        int idColumn = FindHeader(header, "BoneID", "bone_id_dec", "id");
        int nameColumn = FindHeader(header, "BoneName", "suggested_glb_bone", "bone");
        if (idColumn < 0 || nameColumn < 0)
            throw new InvalidDataException("CSV needs BoneID,BoneName or bone_id_dec,suggested_glb_bone headers.");
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].TrimStart().StartsWith("#")) continue;
            string[] f = CsvFields(lines[i]);
            if (f.Length <= Math.Max(idColumn, nameColumn)) continue;
            string textId = f[idColumn].Trim();
            int id;
            if (textId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(textId.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)) continue;
            }
            else if (!int.TryParse(textId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) continue;
            string name = f[nameColumn].Trim();
            if (name.Length > 0) result[id] = name;
            else result.Remove(id); // A deliberately blank row disables mapping.
        }
        return result;
    }

    private static int FindHeader(string[] fields, params string[] names)
    {
        for (int i = 0; i < fields.Length; i++)
            foreach (string n in names)
                if (string.Equals(fields[i].Trim(), n, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static string[] CsvFields(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted) { result.Add(sb.ToString()); sb.Length = 0; }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    internal static bool IsMotContainer(byte[] b)
    {
        if (b == null || b.Length < 36 || b[0] != 0 || b[1] != 0 || b[2] != 0) return false;
        try
        {
            int count = U16(b, 6), start = (int)U32(b, 8), declared = (int)U32(b, 12);
            if (count < 1 || count > 20000 || start != 16 || declared != b.Length) return false;
            Require(b, start, (long)count * 4);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                uint ofs = U32(b, start + 4 * i);
                if (ofs == 0) continue;
                if (ofs < start + count * 4 || ofs > b.Length - 16) return false;
                found = true;
                if (!IsGntaAt(b, (int)ofs, b.Length - (int)ofs)) return false;
            }
            return found;
        }
        catch (Exception) { return false; }
    }

    internal static bool IsStandaloneGnta(byte[] b)
    {
        return b != null && IsGntaAt(b, 0, b.Length);
    }

    private static bool IsGntaAt(byte[] b, int start, int length)
    {
        try
        {
            Require(b, start, 16);
            int tracks = U16(b, start + 2);
            return length >= 16 && tracks > 0 && tracks < 2048
                   && U32(b, start + 4) == 16 && (long)16 + tracks * 32 <= length
                   && Finite(F32(b, start + 12)) && F32(b, start + 12) >= 0;
        }
        catch (Exception) { return false; }
    }

    internal static List<ClipUnit> ReadArchive(byte[] b, string name)
    {
        if (!IsMotContainer(b)) throw new InvalidDataException("Invalid MOT archive: " + name);
        int count = U16(b, 6);
        var starts = new SortedSet<int>();
        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = (int)U32(b, 16 + i * 4);
            if (offsets[i] > 0) starts.Add(offsets[i]);
        }
        var ends = new Dictionary<int, int>();
        int previous = -1;
        foreach (int s in starts)
        {
            if (previous > 0) ends[previous] = s;
            previous = s;
        }
        if (previous > 0) ends[previous] = b.Length;
        var units = new List<ClipUnit>();
        for (int i = 0; i < count; i++)
        {
            int s = offsets[i];
            if (s <= 0) continue;
            int size = ends[s] - s;
            var data = new byte[size];
            Buffer.BlockCopy(b, s, data, 0, size);
            units.Add(new ClipUnit { Slot = i, Data = data });
        }
        return units;
    }

    internal static List<ClipUnit> ReadStandalone(byte[] b, string name)
    {
        if (!IsStandaloneGnta(b)) throw new InvalidDataException("Invalid GNTA clip: " + name);
        return new List<ClipUnit> { new ClipUnit { Slot = -1, Data = b } };
    }

    private static List<MotionTrack> ReadTracks(byte[] b, out float duration, out int controlCount)
    {
        if (!IsStandaloneGnta(b)) throw new InvalidDataException("GNTA motion header invalid");
        duration = F32(b, 12);
        if (duration <= 0 || duration > 120f) throw new InvalidDataException("GNTA invalid duration: " + duration);
        controlCount = 0;
        int count = U16(b, 2);
        int dataStart = 16 + 32 * count;
        var result = new List<MotionTrack>();
        for (int i = 0; i < count; i++)
        {
            int p = 16 + 32 * i;
            ushort flags = U16(b, p);
            ushort trackFlags = U16(b, p + 2);
            int boneId = U16(b, p + 4);
            int keyCount = U16(b, p + 6);
            int timesOffset = checked((int)U32(b, p + 16));
            int coordsOffset = checked((int)U32(b, p + 20));
            int comp = flags & 0xff;
            int kind = (trackFlags & 0x01) != 0 ? 1 : (trackFlags & 0x08) != 0 ? 8
                      : (trackFlags & 0x02) != 0 ? 2 : 0;
            if (kind == 0 || boneId == 65535) { controlCount++; continue; }
            if ((flags & 0xff00) != 0x0200 || (comp != 2 && comp != 4))
                throw new InvalidDataException("Unsupported GNTA track flag 0x" + flags.ToString("X4") + " on bone " + boneId);
            if (keyCount < 1 || keyCount > 20000 || timesOffset < dataStart || coordsOffset < dataStart)
                throw new InvalidDataException("Invalid GNTA track header on bone " + boneId);
            Require(b, timesOffset, 4L * keyCount);
            Require(b, coordsOffset, (long)comp * keyCount * 4);
            var t = new MotionTrack { Kind = kind, BoneId = boneId, Components = comp,
                                      Times = new float[keyCount], Values = new Vector4[keyCount] };
            float prev = -0.0001f;
            for (int k = 0; k < keyCount; k++)
            {
                float time = F32(b, timesOffset + 4 * k);
                if (!Finite(time) || time < -0.0001f || time < prev - 0.0001f || time > duration + 0.5f)
                    throw new InvalidDataException("Invalid/nonmonotonic keyframe time on bone " + boneId);
                t.Times[k] = Mathf.Max(0, time);
                prev = time;
                int ofs = coordsOffset + k * 4 * comp;
                float x = F32(b, ofs), y = F32(b, ofs + 4);
                float z = comp == 4 ? F32(b, ofs + 8) : 0f;
                float w = comp == 4 ? F32(b, ofs + 12) : 0f;
                if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(w))
                    throw new InvalidDataException("Invalid transform value on bone " + boneId);
                t.Values[k] = new Vector4(x, y, z, w);
            }
            result.Add(t);
        }
        return result;
    }

    internal static AnimationClip Convert(ClipUnit unit, string archiveName,
        Dictionary<string, Transform> targets, Dictionary<string, string> paths,
        Options opts, bool export, out string report)
    {
        float duration;
        int controlCount;
        List<MotionTrack> tracks = ReadTracks(unit.Data, out duration, out controlCount);
        string name = unit.Slot >= 0 ? archiveName + "_slot_" + unit.Slot.ToString("X4") : archiveName;
        int mapped = 0, skippedRotation2D = 0, skippedRotation4D = 0, experimental2D = 0, valid4D = 0, exportedTracks = 0;
        var animatedNames = new HashSet<string>(StringComparer.Ordinal);
        int all2DRotation = 0, all4DRotation = 0, allTranslations = 0;
        var unknownIds = new SortedSet<int>();
        var missingHierarchyIds = new SortedSet<int>();
        var boundTracks = new List<MotionTrack>();
        foreach (MotionTrack t in tracks)
        {
            if(t.Kind==8 && t.Components==2) all2DRotation++;
            else if(t.Kind==8 && t.Components==4) all4DRotation++;
            else if(t.Kind==1) allTranslations++;
            string boneName;
            if (opts.Mapping == null || !opts.Mapping.TryGetValue(t.BoneId, out boneName) ||
                (!opts.IncludeAttachments && (t.BoneId >= 150 || boneName.StartsWith("w_", StringComparison.OrdinalIgnoreCase))))
            {
                unknownIds.Add(t.BoneId);
                continue;
            }
            if (targets != null && !targets.ContainsKey(boneName))
            {
                missingHierarchyIds.Add(t.BoneId);
                continue;
            }
            mapped++;
            if (targets == null) continue; // analyze without a model: IDs mapped but Transform paths unknown.
            if (t.Kind == 8 && t.Components == 2)
            {
                if (opts.Rotate2D == 0) { skippedRotation2D++; continue; }
                experimental2D++;
            }
            if (t.Kind == 8 && t.Components == 4)
            {
                if (!opts.AxisAngle4D || !AxisAngleSamplesValid(t))
                { skippedRotation4D++; continue; }
                valid4D++;
            }
            boundTracks.Add(t);
            animatedNames.Add(boneName);
        }
        string[] unknownStr = new string[unknownIds.Count];
        int ui = 0;
        foreach (int id in unknownIds) unknownStr[ui++] = id.ToString(CultureInfo.InvariantCulture);
        report = "[MOT] " + name + ": " + tracks.Count + " transform tracks, " + mapped + " mapped bone-tracks"
               + (targets == null ? " (no model selected); " : "; ")
               + (unknownIds.Count > 0 ? "unknown IDs " + string.Join(",", unknownStr) + "; " : "")
               + (missingHierarchyIds.Count > 0 ? "mapped but missing in Hierarchy=" + missingHierarchyIds.Count + "; " : "")
               + "; axis-angle 4D tracks=" + valid4D
               + "; 4D skipped=" + skippedRotation4D
               + "; 2D skipped=" + skippedRotation2D
               + "; 2D EXPERIMENTAL=" + experimental2D
               + "; length=" + duration.ToString("F3", CultureInfo.InvariantCulture) + "s"
               + "; SOURCE translations=" + allTranslations
               + ", rotations2D=" + all2DRotation + ", rotations4D=" + all4DRotation
               + "; ACTIVE bone names=" + animatedNames.Count
               + "; active names=[" + string.Join(",", new List<string>(animatedNames).ToArray()) + "]"
               + (skippedRotation2D>0 ? "; IMPORTANT: skipping 2D rotations leaves most character bones unanimated! Enable EXPERIMENTAL 2D Preview for motion tests." : "");
        if (!export) return null;
        if (targets == null || paths == null || boundTracks.Count == 0)
        {
            report += "; no reliable curves to export";
            return null;
        }
        if (!Finite(opts.PositionScale) || opts.PositionScale <= 0f || opts.PositionScale > 100f)
            throw new InvalidDataException("MOT translation scale must be finite and positive");
        if (opts.FrameRate < 1 || opts.FrameRate > 120)
            throw new InvalidDataException("Unsupported sample FPS");
        int frames = Mathf.CeilToInt(duration * opts.FrameRate) + 1;
        if (frames > 15000) throw new InvalidDataException("MOT animation too long for current sampling options");
        var clip = new AnimationClip { name = name, frameRate = opts.FrameRate, legacy = false };
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        foreach (MotionTrack t in boundTracks)
        {
            string boneName = opts.Mapping[t.BoneId];
            Transform xform = targets[boneName];
            string path = paths[boneName];
            if (t.Kind == 1 || t.Kind == 2)
            {
                float[] x = new float[frames], y = new float[frames], z = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float time = Mathf.Min(duration, f / (float)opts.FrameRate);
                    Vector4 v = SampleVector(t, time);
                    Vector3 bind = t.Kind == 1 ? xform.localPosition : xform.localScale;
                    if (t.Kind == 1)
                    {
                        // XY is the working hypothesis for 2-component position tracks;
                        // the unrecorded Z stays in the GLB bind pose.
                        Vector3 pos = new Vector3(v.x * opts.PositionScale, v.y * opts.PositionScale,
                            t.Components == 4 ? v.z * opts.PositionScale : 0f);
                        if (t.Components == 2)
                        {
                            if (opts.PositionPlane2D == 1) pos = new Vector3(pos.x, 0f, pos.y);
                            else if (opts.PositionPlane2D == 2) pos = new Vector3(0f, pos.x, pos.y);
                        }
                        if (opts.Axis == 0) pos.x = -pos.x;
                        else if (opts.Axis == 1) pos.z = -pos.z;
                        Vector3 resultPos = opts.PositionRelative ? bind + pos : pos;
                        // Missing component is not animated; its previous local value is retained.
                        if (t.Components == 2 && !opts.PositionRelative)
                        {
                            if (opts.PositionPlane2D == 0) resultPos.z = bind.z;
                            else if (opts.PositionPlane2D == 1) resultPos.y = bind.y;
                            else resultPos.x = bind.x;
                        }
                        x[f] = resultPos.x; y[f] = resultPos.y; z[f] = resultPos.z;
                    }
                    else
                    {
                        x[f] = v.x; y[f] = v.y; z[f] = t.Components == 4 ? v.z : bind.z;
                    }
                }
                string prefix = t.Kind == 1 ? "m_LocalPosition" : "m_LocalScale";
                // Only bind the components actually present in the source.
                int plane = t.Kind == 1 ? opts.PositionPlane2D : 0;
                if (t.Components == 4 || plane != 2) Write(bindings, curves, path, prefix + ".x", x, opts.FrameRate);
                if (t.Components == 4 || plane != 1) Write(bindings, curves, path, prefix + ".y", y, opts.FrameRate);
                if (t.Components == 4 || plane != 0) Write(bindings, curves, path, prefix + ".z", z, opts.FrameRate);
                exportedTracks++;
            }
            else if (t.Kind == 8)
            {
                float[] x = new float[frames], y = new float[frames], z = new float[frames], w = new float[frames];
                Quaternion prev = Quaternion.identity;
                for (int f = 0; f < frames; f++)
                {
                    float time = Mathf.Min(duration, f / (float)opts.FrameRate);
                    Quaternion q = SampleRotation(t, time, opts.Rotate2D, opts.AxisAngleRadians);
                    if (opts.Axis == 0) q = new Quaternion(q.x, -q.y, -q.z, q.w);
                    else if (opts.Axis == 1) q = new Quaternion(-q.x, -q.y, q.z, q.w);
                    if (opts.RotationComposition == 1) q = xform.localRotation * q;
                    else if (opts.RotationComposition == 2) q = q * xform.localRotation;
                    if (f > 0 && Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    prev = q;
                    x[f] = q.x; y[f] = q.y; z[f] = q.z; w[f] = q.w;
                }
                Write(bindings, curves, path, "m_LocalRotation.x", x, opts.FrameRate);
                Write(bindings, curves, path, "m_LocalRotation.y", y, opts.FrameRate);
                Write(bindings, curves, path, "m_LocalRotation.z", z, opts.FrameRate);
                Write(bindings, curves, path, "m_LocalRotation.w", w, opts.FrameRate);
                exportedTracks++;
            }
        }
        if (bindings.Count == 0) { UnityEngine.Object.DestroyImmediate(clip); return null; }
        AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
        clip.EnsureQuaternionContinuity();
        report += "; written tracks=" + exportedTracks + ", curve bindings=" + bindings.Count;
        return clip;
    }

    private static Vector4 SampleVector(MotionTrack t, float time)
    {
        int i = FindInterval(t.Times, time);
        if (i + 1 >= t.Times.Length) return t.Values[i];
        float span = t.Times[i + 1] - t.Times[i];
        if (span < 0.00001f) return t.Values[i + 1];
        float a = Mathf.Clamp01((time - t.Times[i]) / span);
        return Vector4.LerpUnclamped(t.Values[i], t.Values[i + 1], a);
    }

    // Quaternions are interpolated on the 3D rotation sphere.
    private static Quaternion SampleRotation(MotionTrack track,float time,int mode2D,bool radians)
    {
        int i=FindInterval(track.Times,time);
        Quaternion qa=RotationKey(track,track.Values[i],mode2D,radians);
        if(i+1>=track.Times.Length)return qa;
        float span=track.Times[i+1]-track.Times[i];
        if(span<0.00001f)return RotationKey(track,track.Values[i+1],mode2D,radians);
        Quaternion qb=RotationKey(track,track.Values[i+1],mode2D,radians);
        if(Quaternion.Dot(qa,qb)<0f)qb=new Quaternion(-qb.x,-qb.y,-qb.z,-qb.w);
        return Quaternion.SlerpUnclamped(qa,qb,Mathf.Clamp01((time-track.Times[i])/span));
    }

    private static Quaternion RotationKey(MotionTrack t,Vector4 v,int mode2D,bool radians)
    {
        if(t.Components==4)
        {
            // GNTA uses XYZ axis and W angle, not a PEMT quaternion.
            if(v.x==100f && v.y==0f && v.z==0f && v.w==0f)return Quaternion.identity;
            Vector3 axis=new Vector3(v.x,v.y,v.z);
            if(axis.sqrMagnitude<0.000001f || Mathf.Abs(v.w)<0.000001f)return Quaternion.identity;
            return Quaternion.AngleAxis(radians?v.w*Mathf.Rad2Deg:v.w,axis.normalized);
        }
        // 2-float GNTA rotations have NOT been decoded. Disabled by default.
        if(mode2D==0 || (v.x==100f && v.y==0f))return Quaternion.identity;
        // Two deliberately different, unverified reconstructions are exposed
        // in the Workbench to inspect how much motion the sample actually contains.
        // Mode 4 also responds to x changes when y==0; atan2 does not.
        if(mode2D==4)
        {
            float halfAngle=Mathf.Acos(Mathf.Clamp(v.x,-1f,1f));
            float degrees=2f*halfAngle*Mathf.Rad2Deg;
            if(v.y<0f)degrees=-degrees;
            return Quaternion.AngleAxis(degrees,Vector3.right);
        }
        float angle=2f*Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg;
        Vector3 axis2=mode2D==1?Vector3.right:mode2D==2?Vector3.up:Vector3.forward;
        return Quaternion.AngleAxis(angle,axis2);
    }

    private static bool AxisAngleSamplesValid(MotionTrack t)
    {
        int normal=0;
        foreach(Vector4 v in t.Values)
        {
            if(v.x==100f&&v.y==0f&&v.z==0f&&v.w==0f)continue;
            float n=v.x*v.x+v.y*v.y+v.z*v.z;
            if(n<0.90f || n>1.10f)return false;
            normal++;
        }
        return normal>0;
    }

    private static int FindInterval(float[] times, float t)
    {
        if (t <= times[0]) return 0;
        int lo = 0, hi = times.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (times[mid] <= t) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    private static void Write(List<EditorCurveBinding> bindings, List<AnimationCurve> curves,
        string path, string prop, float[] values, int fps)
    {
        if (values == null || values.Length < 1) return;
        bool constant = true;
        for (int i = 1; i < values.Length; i++)
            if (Mathf.Abs(values[i] - values[0]) > 0.000001f) { constant = false; break; }
        AnimationCurve c;
        if (constant)
        {
            c = values.Length == 1 ? new AnimationCurve(new Keyframe(0f, values[0]))
                : new AnimationCurve(new Keyframe(0f, values[0]),
                    new Keyframe((values.Length - 1) / (float)fps, values[0]));
        }
        else
        {
            var keys = new Keyframe[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float before = i == 0 ? (values[1] - values[0]) * fps : (values[i] - values[i - 1]) * fps;
                float after = i == values.Length - 1 ? (values[i] - values[i - 1]) * fps : (values[i + 1] - values[i]) * fps;
                keys[i] = new Keyframe(i / (float)fps, values[i], before, after);
            }
            c = new AnimationCurve(keys);
        }
        bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), prop));
        curves.Add(c);
    }

    private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    private static void Require(byte[] b, long p, long n)
    {
        if (p < 0 || n < 0 || p > b.Length || n > b.Length - p || p > int.MaxValue)
            throw new InvalidDataException("MOT/GNTA offset invalid: " + p + ", size " + n + ", buffer " + b.Length);
    }
    private static ushort U16(byte[] b, int p)
    {
        Require(b, p, 2); return (ushort)(b[p] << 8 | b[p + 1]);
    }
    private static uint U32(byte[] b, int p)
    {
        Require(b, p, 4); return (uint)b[p] << 24 | (uint)b[p + 1] << 16 | (uint)b[p + 2] << 8 | b[p + 3];
    }
    private static float F32(byte[] b, int p)
    {
        uint raw = U32(b, p);
        var bytes = BitConverter.GetBytes(raw);
        return BitConverter.ToSingle(bytes, 0);
    }
}
#endif