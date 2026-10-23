// BRRES/MDL0 native reader for Unity 2022.3. Independent managed implementation.
// Supports NW4R MDL0 v8-v11, common indexed GX triangle commands, skeleton,
// node influences, TEX0 (CMPR / common non-paletted formats).
// Beta: Nintendo TEV shaders, uncommon GX direct attributes and PLT0 are not complete.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace MusafirEx.Brres
{
    internal sealed class BrresMdl0Reader
    {
        internal sealed class Resource
        {
            internal int Offset;
            internal int Length;
            internal string Name;
            internal string Magic;
        }
        internal sealed class Joint
        {
            internal string Name;
            internal int Parent = -1;
            internal int NodeId = -1;
            internal Vector3 Position;
            internal Vector3 Scale = Vector3.one;
            internal Quaternion Rotation = Quaternion.identity;
            internal int RecordOffset;
            internal Matrix4x4 WorldMatrix = Matrix4x4.identity;
        }
        internal struct Influence
        {
            internal int Joint;
            internal float Weight;
        }
        internal sealed class MeshData
        {
            internal string Name;
            internal int MaterialIndex;
            internal readonly List<Vector3> Positions = new List<Vector3>();
            internal readonly List<Vector3> Normals = new List<Vector3>();
            internal readonly List<Vector2> UV = new List<Vector2>();
            internal readonly List<Color> Colors = new List<Color>();
            internal readonly List<int> Triangles = new List<int>();
            internal readonly List<BoneWeight> Weights = new List<BoneWeight>();
            internal bool HasNormal;
            internal bool HasUV;
        }
        internal sealed class Model
        {
            internal string Name;
            internal readonly List<Joint> Joints = new List<Joint>();
            internal readonly List<MeshData> Meshes = new List<MeshData>();
            internal readonly List<string> MaterialNames = new List<string>();
            internal readonly List<string> MaterialTextures = new List<string>();
        }
        private readonly byte[] data;
        private readonly bool flipX;
        private readonly bool flipUV;
        private readonly float scale;
        private readonly Dictionary<int, Matrix4x4> nodeMatrices = new Dictionary<int, Matrix4x4>();
        private readonly Dictionary<int, List<Influence>> nodeWeights = new Dictionary<int, List<Influence>>();
        private readonly Dictionary<int, int> jointByNode = new Dictionary<int, int>();
        private readonly Dictionary<string, Resource> resources = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);
        internal BrresMdl0Reader(byte[] bytes, bool flipX, bool flipUV, float scale)
        {
            data = bytes ?? throw new ArgumentNullException(nameof(bytes));
            this.flipX = flipX;
            this.flipUV = flipUV;
            this.scale = scale;
        }

        private bool Bounds(int at, long length) { return at >= 0 && length >= 0 && at <= data.Length && length <= data.Length - at; }
        private void Require(int at, long count) { if (!Bounds(at, count)) throw new InvalidDataException("Invalid NW4R resource offset at 0x" + at.ToString("X")); }
        private byte U8(int at) { Require(at,1); return data[at]; }
        private ushort U16(int at) { Require(at,2); return (ushort)((data[at]<<8)|data[at+1]); }
        private short I16(int at) { return unchecked((short)U16(at)); }
        private uint U32(int at) { Require(at,4); return ((uint)data[at]<<24)|((uint)data[at+1]<<16)|((uint)data[at+2]<<8)|data[at+3]; }
        private int I32(int at) { return unchecked((int)U32(at)); }
        private float F32(int at)
        {
            uint bits=U32(at);
            byte[] raw=BitConverter.GetBytes(bits);
            return BitConverter.ToSingle(raw,0);
        }
        private string Magic(int at) { return Bounds(at,4) ? Encoding.ASCII.GetString(data,at,4) : string.Empty; }
        private string Pooled(int field, int offsetBase)
        {
            if (!Bounds(field,4)) return null;
            int relative=I32(field);
            long address=(long)offsetBase+relative;
            if (relative==0 || address<4 || address>data.Length) return null;
            int ptr=(int)address;
            uint len=U32(ptr-4);
            if (len==0 || len>255 || !Bounds(ptr,len)) return null;
            for (int i=0;i<len;i++) if (data[ptr+i]<32 || data[ptr+i]>126) return null;
            return Encoding.ASCII.GetString(data,ptr,(int)len);
        }
        private int Group(int mdl,int version,int index)
        {
            if (index==6) index=version>=10?8:6; // materials
            else if (index==7) index=version>=10?10:8; // objects
            else if (index==8) index=version>=10?11:9; // texture links
            int rel=I32(mdl+0x10+index*4);
            int pos=checked(mdl+rel);
            return rel>0 && ValidGroup(pos) ? pos : -1;
        }
        private bool ValidGroup(int at)
        {
            if (!Bounds(at,8)) return false;
            uint n=U32(at+4),size=U32(at);
            return n<=8192 && size>=8 && Bounds(at,(long)8+16*(n+1));
        }
        private int Count(int g) { return g<0?0:(int)U32(g+4); }
        private int Entry(int group,int index) { return checked(group+8+(index+1)*16); }
        private int ResourceAt(int group,int index)
        {
            if (group<0 || index<0 || index>=Count(group)) return -1;
            int e=Entry(group,index),delta=I32(e+12);
            long at=(long)group+delta;
            return delta>=0 && at<=int.MaxValue && Bounds((int)at,4)?(int)at:-1;
        }
        private string EntryName(int group,int index)
        {
            if (group<0) return null;
            return Pooled(Entry(group,index)+8,group);
        }
        private void WalkArchive(int at,int depth,HashSet<int> visited)
        {
            if(depth>8 || !ValidGroup(at) || !visited.Add(at)) return;
            int num=Count(at);
            for(int i=0;i<num;i++)
            {
                int child=ResourceAt(at,i);
                if(child<0) continue;
                string name=EntryName(at,i);
                string sig=Magic(child);
                if(sig=="MDL0" || sig=="TEX0" || sig=="PLT0" || sig=="CHR0")
                {
                    int length=I32(child+4);
                    if(length<16 || !Bounds(child,length)) continue;
                    var res=new Resource{Offset=child,Length=length,Name=string.IsNullOrEmpty(name)?sig+"_"+child.ToString("X"):name,Magic=sig};
                    resources[sig+":"+res.Name+":"+child]=res;
                }
                else if(ValidGroup(child)) WalkArchive(child,depth+1,visited);
            }
        }
        internal List<Resource> FindResources()
        {
            resources.Clear();
            if(Magic(0)=="MDL0" || Magic(0)=="TEX0" || Magic(0)=="CHR0")
            {
                string sig=Magic(0);
                resources[sig+":root:0"]=new Resource{Offset=0,Length=data.Length,Name="model",Magic=sig};
            }
            else if(Magic(0)=="bres")
            {
                int root=U16(0x0c);
                if(Magic(root)!="root") throw new InvalidDataException("BRRES root header is missing");
                WalkArchive(root+8,0,new HashSet<int>());
            }
            else throw new InvalidDataException("Expected Wii BRRES (bres) or MDL0 file");
            var result=new List<Resource>(resources.Values);
            result.Sort((a,b)=>a.Offset.CompareTo(b.Offset));
            return result;
        }
        // Include data from CHR0 offset up to end of BRRES: NW4R names can reference
        // the archive's shared string pool located after the resource's declared size.
        internal byte[] ReadChr0WithSharedStrings(Resource resource)
        {
            if (resource == null || resource.Magic != "CHR0")
                throw new ArgumentException("Resource must be CHR0", nameof(resource));
            Require(resource.Offset, resource.Length);
            int size = data.Length - resource.Offset;
            if (size > 128 * 1024 * 1024)
                throw new InvalidDataException("CHR0 resource tail exceeds safety limit");
            var bytes = new byte[size];
            Buffer.BlockCopy(data, resource.Offset, bytes, 0, size);
            return bytes;
        }

        internal Dictionary<string,Texture2D> LoadTextures(List<Resource> found,List<string> warnings)
        {
            var output=new Dictionary<string,Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach(var resource in found)
            {
                if(resource.Magic!="TEX0") continue;
                try
                {
                    Texture2D texture=ReadTexture(resource.Offset);
                    if(texture!=null)
                    {
                        texture.name=resource.Name;
                        output[resource.Name]=texture;
                    }
                    else warnings.Add("TEX0 " + resource.Name + ": unsupported format");
                }
                catch(Exception ex){warnings.Add("TEX0 " + resource.Name + ": " + ex.Message);}
            }
            return output;
        }
        private static Color Rgb565(ushort v)
        {
            return new Color(((v>>11)&31)/31f,((v>>5)&63)/63f,(v&31)/31f,1f);
        }
        private static Color Rgb5a3(ushort v)
        {
            if((v&0x8000)!=0)return new Color(((v>>10)&31)/31f,((v>>5)&31)/31f,(v&31)/31f,1f);
            return new Color(((v>>8)&15)/15f,((v>>4)&15)/15f,(v&15)/15f,((v>>12)&7)/7f);
        }
        private Texture2D ReadTexture(int t)
        {
            int w=U16(t+0x1c),h=U16(t+0x1e),format=I32(t+0x20),rel=I32(t+0x10);
            if(w<1||h<1||w>8192||h>8192||((long)w*h)>16777216 || rel<0) throw new InvalidDataException("Bad TEX0 texture dimensions or offset");
            // Nintendo GX texture tiles are 8x8 (I4), 8x4 (I8), 4x4 (16-bit), 4x4 RGBA8, 8x8 CMPR.
            int tw=4,th=4,bytesPerTile=32;
            if(format==0){tw=8;th=8;bytesPerTile=32;}
            else if(format==1||format==2){tw=8;th=4;bytesPerTile=32;}
            else if(format==3||format==4||format==5){tw=4;th=4;bytesPerTile=32;}
            else if(format==6){tw=4;th=4;bytesPerTile=64;}
            else if(format==14){tw=8;th=8;bytesPerTile=32;}
            else return null; // Palette indices (C4/C8/C14X2) require PLT0: not yet supported.
            int cols=(w+tw-1)/tw, rows=(h+th-1)/th;
            long expected=(long)cols*rows*bytesPerTile;
            int start=checked(t+rel);
            Require(start,expected);
            var pixels=new Color32[w*h];
            int read=start;
            for(int by=0;by<rows;by++) for(int bx=0;bx<cols;bx++)
            {
                int tile=read; read+=bytesPerTile;
                if(format==14)
                {
                    for(int sub=0;sub<4;sub++)
                    {
                        int off=tile+sub*8;
                        ushort a=U16(off),b=U16(off+2);
                        Color[] palette=new Color[4];palette[0]=Rgb565(a);palette[1]=Rgb565(b);
                        if(a>b){palette[2]=palette[0]*(2f/3f)+palette[1]*(1f/3f);palette[3]=palette[0]*(1f/3f)+palette[1]*(2f/3f);}
                        else {palette[2]=(palette[0]+palette[1])*0.5f;palette[3]=new Color(0,0,0,0);}
                        uint indices=U32(off+4);
                        for(int iy=0;iy<4;iy++)for(int ix=0;ix<4;ix++)
                        {
                            int x=bx*8+(sub%2)*4+ix,y=by*8+(sub/2)*4+iy;
                            if(x>=w||y>=h)continue;
                            int shift=30-2*(iy*4+ix);
                            pixels[(h-1-y)*w+x]=(Color32)palette[(int)((indices>>shift)&3)];
                        }
                    }
                }
                else
                {
                    for(int yy=0;yy<th;yy++)for(int xx=0;xx<tw;xx++)
                    {
                        int x=bx*tw+xx,y=by*th+yy;
                        if(x>=w||y>=h)continue;
                        int idx=yy*tw+xx;
                        Color c=Color.white;
                        if(format==0){byte n=U8(tile+idx/2);float v=((idx&1)==0?(n>>4):(n&15))/15f;c=new Color(v,v,v,v);}
                        else if(format==1){float v=U8(tile+idx)/255f;c=new Color(v,v,v,1);}
                        else if(format==2){byte n=U8(tile+idx);float a=(n>>4)/15f, v=(n&15)/15f;c=new Color(v,v,v,a);}
                        else if(format==3){float a=U8(tile+idx*2)/255f,v=U8(tile+idx*2+1)/255f;c=new Color(v,v,v,a);}
                        else if(format==4)c=Rgb565(U16(tile+idx*2));
                        else if(format==5)c=Rgb5a3(U16(tile+idx*2));
                        else if(format==6){byte a=U8(tile+idx*2),r=U8(tile+idx*2+1),g=U8(tile+32+idx*2),b=U8(tile+32+idx*2+1);c=new Color(r/255f,g/255f,b/255f,a/255f);}
                        pixels[(h-1-y)*w+x]=(Color32)c;
                    }
                }
            }
            var result=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
            result.SetPixels32(pixels);result.Apply(false,true);
            result.wrapMode=TextureWrapMode.Repeat;
            result.filterMode=FilterMode.Bilinear;
            return result;
        }
        private static Quaternion WiiEuler(float rx,float ry,float rz)
        {
            float ax=rx*Mathf.Deg2Rad*0.5f,ay=ry*Mathf.Deg2Rad*0.5f,az=rz*Mathf.Deg2Rad*0.5f;
            float sx=Mathf.Sin(ax),cx=Mathf.Cos(ax),sy=Mathf.Sin(ay),cy=Mathf.Cos(ay),sz=Mathf.Sin(az),cz=Mathf.Cos(az);
            return new Quaternion(sx*cy*cz-cx*sy*sz,cx*sy*cz+sx*cy*sz,cx*cy*sz-sx*sy*cz,cx*cy*cz+sx*sy*sz);
        }
        private static Matrix4x4 ChangeBasis(Matrix4x4 source,bool flipX)
        {
            if(!flipX) return source;
            Matrix4x4 c=Matrix4x4.Scale(new Vector3(-1,1,1));
            return c*source*c;
        }
        private Vector3 ConvertVector(Vector3 v){return new Vector3((flipX?-v.x:v.x)*scale,v.y*scale,v.z*scale);}
        private Matrix4x4 ReadMatrix(int at)
        {
            var m=Matrix4x4.identity;
            for(int row=0;row<3;row++)for(int col=0;col<4;col++)m[row,col]=F32(at+(row*4+col)*4);
            return ChangeBasis(m,flipX);
        }
        private float Component(int at,int fmt,int divisor)
        {
            float factor=1f/(1<<Mathf.Clamp(divisor,0,30));
            if(fmt==0)return U8(at)*factor;
            if(fmt==1)return unchecked((sbyte)U8(at))*factor;
            if(fmt==2)return U16(at)*factor;
            if(fmt==3)return I16(at)*factor;
            if(fmt==4)return F32(at);
            throw new InvalidDataException("Unsupported numeric attribute type: "+fmt);
        }
        private static int ComponentSize(int fmt){return fmt<2?1:fmt<4?2:fmt==4?4:0;}
        private Vector3[] ReadVector3(int at,bool normal)
        {
            if(at<0)return new Vector3[0];
            int rel=I32(at+8),dim=I32(at+20),fmt=I32(at+24),div=U8(at+28),stride=U8(at+29),count=U16(at+30);
            int n=normal?3:(dim!=0?3:2),sz=ComponentSize(fmt);
            if(rel<=0||count==0||sz==0||stride<n*sz||count>500000) return new Vector3[0];
            Require(at+rel,(long)count*stride);
            var v=new Vector3[count];
            for(int i=0;i<count;i++)
            {
                int off=at+rel+i*stride;
                v[i]=new Vector3(Component(off,fmt,div),Component(off+sz,fmt,div),n==3?Component(off+2*sz,fmt,div):0);
            }
            return v;
        }
        private Vector2[] ReadVector2(int at)
        {
            if(at<0)return new Vector2[0];
            int rel=I32(at+8),dim=I32(at+20),fmt=I32(at+24),div=U8(at+28),stride=U8(at+29),count=U16(at+30);
            int n=dim!=0?2:1,sz=ComponentSize(fmt);
            if(rel<=0||count==0||sz==0||stride<n*sz||count>500000)return new Vector2[0];
            Require(at+rel,(long)count*stride);
            var v=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                int off=at+rel+i*stride;
                float u=Component(off,fmt,div),y=n==2?Component(off+sz,fmt,div):0;
                v[i]=new Vector2(u,flipUV?1-y:y);
            }
            return v;
        }
        private static int AttributeSize(int fmt){return fmt==2?1:fmt==3?2:0;}
        private sealed class VertexRef
        {
            internal int Pos=-1,Norm=-1,UV=-1,Matrix=-1;
        }
        private List<VertexRef> DecodePrimitives(int start,int size,uint lo,uint hi)
        {
            int posfmt=(int)((lo>>9)&3),normfmt=(int)((lo>>11)&3),uvfmt=(int)(hi&3);
            if(posfmt<2 || normfmt==1 || uvfmt==1)throw new NotSupportedException("Unsupported direct GX vertex attribute layout");
            int stride=(int)(lo&1);
            for(int i=0;i<8;i++)stride+=(int)((lo>>(i+1))&1);
            stride+=AttributeSize(posfmt)+AttributeSize(normfmt)+AttributeSize((int)(lo>>13&3))+AttributeSize((int)(lo>>15&3));
            for(int i=0;i<8;i++)stride+=AttributeSize((int)(hi>>(i*2)&3));
            if(stride<2)throw new InvalidDataException("Empty GX vertex stride");
            Require(start,size);
            int off=start,end=start+size;
            int[] matrixSlots=new int[16];for(int i=0;i<matrixSlots.Length;i++)matrixSlots[i]=-1;
            var triangles=new List<VertexRef>();
            while(off<end)
            {
                int op=U8(off++);
                if(op==0)break;
                if(op>=0x20&&op<=0x38&&(op&7)==0)
                {
                    if(off+4>end)throw new InvalidDataException("Incomplete GX matrix command");
                    if(op==0x20){int slot=(U16(off+2)&0xfff)/12;if(slot<16)matrixSlots[slot]=U16(off);}
                    off+=4;continue;
                }
                int mode=op&0xf8;
                if(mode!=0x80&&mode!=0x90&&mode!=0x98&&mode!=0xa0)throw new NotSupportedException("GX primitive 0x"+mode.ToString("X2"));
                if(off+2>end)throw new InvalidDataException("Incomplete GX primitive header");
                int count=U16(off);off+=2;
                if((long)count*stride>end-off)throw new InvalidDataException("GX vertex block overruns display list");
                var vertices=new VertexRef[count];
                for(int k=0;k<count;k++)
                {
                    int ptr=off+k*stride;
                    var v=new VertexRef();
                    if((lo&1)!=0){int slot=U8(ptr)/3;v.Matrix=slot<16?matrixSlots[slot]:-1;ptr++;}
                    for(int i=0;i<8;i++)ptr+=(int)((lo>>(i+1))&1);
                    int ps=AttributeSize(posfmt);
                    v.Pos=ps==1?U8(ptr):U16(ptr);ptr+=ps;
                    int ns=AttributeSize(normfmt);
                    if(ns>0){v.Norm=ns==1?U8(ptr):U16(ptr);ptr+=ns;}
                    for(int c=0;c<2;c++)ptr+=AttributeSize((int)(lo>>(13+c*2)&3));
                    for(int uv=0;uv<8;uv++)
                    {
                        int sz=AttributeSize((int)(hi>>(uv*2)&3));
                        if(uv==0&&sz>0)v.UV=sz==1?U8(ptr):U16(ptr);
                        ptr+=sz;
                    }
                    vertices[k]=v;
                }
                off+=count*stride;
                if(mode==0x90)
                    for(int k=0;k+2<count;k+=3)AddFace(triangles,vertices[k+2],vertices[k+1],vertices[k]);
                else if(mode==0x80)
                    for(int k=0;k+3<count;k+=4){AddFace(triangles,vertices[k],vertices[k+2],vertices[k+1]);AddFace(triangles,vertices[k],vertices[k+3],vertices[k+2]);}
                else if(mode==0x98)
                    for(int k=2;k<count;k++)
                        if((k&1)==1)AddFace(triangles,vertices[k],vertices[k-2],vertices[k-1]);
                        else AddFace(triangles,vertices[k],vertices[k-1],vertices[k-2]);
                else for(int k=2;k<count;k++)AddFace(triangles,vertices[0],vertices[k],vertices[k-1]);
            }
            return triangles;
        }
        private static void AddFace(List<VertexRef> output,VertexRef a,VertexRef b,VertexRef c)
        {
            output.Add(a);output.Add(b);output.Add(c);
        }
        private List<Influence> GetInfluences(int node)
        {
            List<Influence> value;
            if(nodeWeights.TryGetValue(node,out value))return value;
            int bone;
            if(jointByNode.TryGetValue(node,out bone))return new List<Influence>{new Influence{Joint=bone,Weight=1}};
            return new List<Influence>();
        }
        private static BoneWeight MakeWeight(List<Influence> src)
        {
            // Unity legacy BoneWeight carries up to four influences. Clamp explicitly.
            var sorted=new List<Influence>(src);sorted.Sort((a,b)=>b.Weight.CompareTo(a.Weight));
            BoneWeight w=new BoneWeight();
            float sum=0;
            for(int i=0;i<Math.Min(4,sorted.Count);i++)sum+=Mathf.Max(0,sorted[i].Weight);
            if(sum<=0)return w;
            for(int i=0;i<Math.Min(4,sorted.Count);i++)
            {
                int bone=Mathf.Max(0,sorted[i].Joint);
                float value=Mathf.Max(0,sorted[i].Weight)/sum;
                if(i==0){w.boneIndex0=bone;w.weight0=value;}
                if(i==1){w.boneIndex1=bone;w.weight1=value;}
                if(i==2){w.boneIndex2=bone;w.weight2=value;}
                if(i==3){w.boneIndex3=bone;w.weight3=value;}
            }
            return w;
        }
        private void ReadNodeMix(int defs,List<Joint> joints)
        {
            for(int i=0;i<Count(defs);i++)
            {
                if(EntryName(defs,i)!="NodeMix")continue;
                int p=ResourceAt(defs,i);
                if(p<0)continue;
                int consumed=0;
                while(Bounds(p,5)&&consumed<32768)
                {
                    int flag=U8(p);
                    if(flag==5){p+=5;consumed+=5;continue;}
                    if(flag!=3)break;
                    int node=U16(p+1),n=U8(p+3);
                    if(n<=0||n>32||!Bounds(p+4,n*6))break;
                    var weights=new List<Influence>();
                    for(int k=0;k<n;k++)
                    {
                        int id=U16(p+4+k*6),joint;
                        if(!jointByNode.TryGetValue(id,out joint))continue;
                        float val=F32(p+4+k*6+2);
                        if(float.IsNaN(val)||float.IsInfinity(val)||val<=0)continue;
                        weights.Add(new Influence{Joint=joint,Weight=val});
                    }
                    if(weights.Count>0)
                    {
                        nodeWeights[node]=weights;
                        if(weights.Count==1)nodeMatrices[node]=joints[weights[0].Joint].WorldMatrix;
                        else nodeMatrices[node]=Matrix4x4.identity; // node mix already in model space
                    }
                    int skip=4+n*6;p+=skip;consumed+=skip;
                }
            }
        }
        internal Model ReadModel(Resource resource,List<string> warnings)
        {
            int m=resource.Offset;
            if(Magic(m)!="MDL0")throw new InvalidDataException("Resource does not contain MDL0");
            int version=I32(m+8);
            if(version<8||version>11)throw new NotSupportedException("MDL0 version "+version+" (supports v8-v11)");
            nodeMatrices.Clear();nodeWeights.Clear();jointByNode.Clear();
            var model=new Model{Name=resource.Name};
            int bones=Group(m,version,1);
            for(int i=0;i<Count(bones);i++)
            {
                int b=ResourceAt(bones,i);
                if(b<0 || !Bounds(b,0xd0)){warnings.Add("Invalid MDL0 bone record "+i);continue;}
                string name=EntryName(bones,i)??Pooled(b+8,b)??"Bone_"+i;
                var joint=new Joint{Name=name,RecordOffset=b,NodeId=I32(b+0x10)};
                joint.Scale=new Vector3(F32(b+0x20),F32(b+0x24),F32(b+0x28));
                var angles=new Vector3(F32(b+0x2c),F32(b+0x30),F32(b+0x34));
                joint.Position=ConvertVector(new Vector3(F32(b+0x38),F32(b+0x3c),F32(b+0x40)));
                // NW4R XYZ rotation, imported in the same convention as our CHR0 converter.
                Quaternion q=WiiEuler(angles.x,angles.y,angles.z);
                if(flipX)q=new Quaternion(q.x,-q.y,-q.z,q.w);
                joint.Rotation=q;
                joint.WorldMatrix=ReadMatrix(b+0x70);
                joint.WorldMatrix.m03*=scale;joint.WorldMatrix.m13*=scale;joint.WorldMatrix.m23*=scale;
                model.Joints.Add(joint);
                if(joint.NodeId>=0)jointByNode[joint.NodeId]=i;
            }
            for(int i=0;i<model.Joints.Count;i++)
            {
                var joint=model.Joints[i];
                int relative=I32(joint.RecordOffset+0x5c);
                if(relative==0)continue;
                int parent=joint.RecordOffset+relative;
                for(int k=0;k<model.Joints.Count;k++) if(k!=i && model.Joints[k].RecordOffset==parent){joint.Parent=k;break;}
            }
            // Keep the Wii skinning matrix nodes (original bones and NodeMix influences).
            foreach(var joint in model.Joints)if(joint.NodeId>=0)nodeMatrices[joint.NodeId]=joint.WorldMatrix;
            ReadNodeMix(Group(m,version,0),model.Joints);
            int materials=Group(m,version,6);
            for(int i=0;i<Count(materials);i++)
            {
                int mat=ResourceAt(materials,i);
                string name=EntryName(materials,i)??"Material_"+i;
                model.MaterialNames.Add(name);
                string tex=null;
                if(mat>=0&&Bounds(mat,0x34))
                {
                    int n=I32(mat+0x2c),rel=I32(mat+0x30);
                    if(n>0&&n<=8&&rel>0&&Bounds(mat+rel,0x34))
                        tex=Pooled(mat+rel,mat+rel);
                }
                model.MaterialTextures.Add(tex);
            }
            int[] meshMaterials=new int[Math.Max(0,Count(Group(m,version,7)))];
            for(int i=0;i<meshMaterials.Length;i++)meshMaterials[i]=-1;
            int definitions=Group(m,version,0);
            for(int i=0;i<Count(definitions);i++)
            {
                int p=ResourceAt(definitions,i);
                if(p<0||U8(p)!=4)continue;
                int count=0;
                while(Bounds(p,8)&&U8(p)==4 && count++<meshMaterials.Length*4+4)
                {
                    int material=U16(p+1),which=U16(p+3);
                    if(which<meshMaterials.Length&&material<model.MaterialNames.Count&&meshMaterials[which]<0)meshMaterials[which]=material;
                    p+=8;
                }
            }
            int positions=Group(m,version,2),normals=Group(m,version,3),uv=Group(m,version,5),meshes=Group(m,version,7);
            for(int i=0;i<Count(meshes);i++)
            {
                int obj=ResourceAt(meshes,i);
                if(obj<0||!Bounds(obj,0x60))continue;
                string name=EntryName(meshes,i)??"Polygon_"+i;
                try
                {
                    int vertexId=I16(obj+0x48),normalId=I16(obj+0x4a),uvId=I16(obj+0x50);
                    var p=ReadVector3(ResourceAt(positions,vertexId),false);
                    var n=ReadVector3(ResourceAt(normals,normalId),true);
                    var t=ReadVector2(ResourceAt(uv,uvId));
                    if(p.Length==0){warnings.Add(name+": vertex array missing");continue;}
                    int node=I32(obj+8);
                    uint lo=U32(obj+0x0c),hi=U32(obj+0x10);
                    int primitiveStart=checked(obj+0x24+I32(obj+0x2c));
                    int primitiveSize=I32(obj+0x28);
                    if(primitiveSize<0 || primitiveSize>64*1024*1024)throw new InvalidDataException("Display list size invalid");
                    var triangles=DecodePrimitives(primitiveStart,primitiveSize,lo,hi);
                    var outMesh=new MeshData{Name=name,MaterialIndex=i<meshMaterials.Length?meshMaterials[i]:0,HasNormal=n.Length>0,HasUV=t.Length>0};
                    foreach(var vert in triangles)
                    {
                        if(vert.Pos<0||vert.Pos>=p.Length)throw new InvalidDataException("Position index outside vertex array");
                        int nodeId=vert.Matrix>=0?vert.Matrix:node;
                        Matrix4x4 matrix;
                        if(!nodeMatrices.TryGetValue(nodeId,out matrix))matrix=Matrix4x4.identity;
                        Vector3 local=ConvertVector(p[vert.Pos]);
                        Vector3 transformed=matrix.MultiplyPoint3x4(local);
                        outMesh.Positions.Add(transformed);
                        Vector3 normal=(vert.Norm>=0&&vert.Norm<n.Length?n[vert.Norm]:Vector3.up);
                        if(flipX)normal.x=-normal.x;
                        outMesh.Normals.Add(matrix.MultiplyVector(normal).normalized);
                        outMesh.UV.Add(vert.UV>=0&&vert.UV<t.Length?t[vert.UV]:Vector2.zero);
                        outMesh.Colors.Add(Color.white);
                        outMesh.Weights.Add(MakeWeight(GetInfluences(nodeId)));
                        outMesh.Triangles.Add(outMesh.Triangles.Count);
                    }
                    if(outMesh.Positions.Count>0)model.Meshes.Add(outMesh);
                }
                catch(Exception ex){warnings.Add("Skipped "+name+": "+ex.Message);}
            }
            return model;
        }
    }
}
#endif