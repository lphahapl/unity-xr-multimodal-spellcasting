// Offline new-content authoring. Original scene renderers/colliders are never modified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class ConnectivityBuilder
{
    const string Art="Assets/Art/Environment/Opening/ConnectivityLOD/",Log="Logs/exterior-world-lod-20261109/connectivity/";
    const float Width=2.1f,Thickness=.28f,Slope=.21255656f; // tan12 degrees
    static Func<float,float,float> sample;static Transform content;static Scene scene;static Material material;
    static readonly List<string> proof=new List<string>();static int serial;
    class Geometry
    {
        public List<Vector3> v=new List<Vector3>();public List<Vector2> uv=new List<Vector2>();public List<int> t=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int i=v.Count;var normal=Vector3.Cross(b-a,c-a).normalized;
            foreach(var p in new[]{a,b,c,d}){v.Add(content.InverseTransformPoint(p));uv.Add(Mathf.Abs(normal.y)>.6f?new Vector2(p.x,p.z)*.5f:Mathf.Abs(normal.x)>.6f?new Vector2(p.z,p.y)*.5f:new Vector2(p.x,p.y)*.5f);}
            t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }
        public void Box(Vector3 center,Vector3 size,Quaternion rotation)
        {
            Vector3 P(float x,float y,float z)=>center+rotation*Vector3.Scale(new Vector3(x,y,z),size*.5f);
            var a=P(-1,-1,-1);var b=P(1,-1,-1);var c=P(1,1,-1);var d=P(-1,1,-1);
            var e=P(-1,-1,1);var f=P(1,-1,1);var g=P(1,1,1);var h=P(-1,1,1);
            Quad(a,d,c,b);Quad(e,f,g,h);Quad(a,e,h,d);Quad(b,c,g,f);Quad(d,h,g,c);Quad(a,b,f,e);
        }
        public Mesh Mesh(string name){var m=new Mesh{name=name};if(v.Count>65000)m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;}
    }
    static float H(float x,float z){float h=sample(x,z);if(float.IsNaN(h)||float.IsInfinity(h))throw new Exception("No triangle terrain beneath bridge at "+x+","+z);return h;}
    public static void Build(Transform root,Scene target,Func<float,float,float> groundHeight)
    {
        if(!root||root.gameObject.scene!=target||groundHeight==null)throw new ArgumentException("Connectivity requires root, scene and actual terrain sampler");
        content=root;scene=target;sample=groundHeight;serial=0;proof.Clear();Directory.CreateDirectory(Art);Directory.CreateDirectory(Log);AssetDatabase.Refresh();
        int initialColliders=content.GetComponentsInChildren<Collider>(true).Length;
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/CourtyardStone.mat");
        if(!material)throw new Exception("Missing existing CourtyardStone material");
        Vector3 west=Anchor(new Vector3(-18.25f,0,8)),east=Anchor(new Vector3(21.95f,0,25)),north=Anchor(new Vector3(23.650f,0,63.971f));
        Path("西坊前庭支路",new[]{new Vector3(0,-.03f,37.2f),new Vector3(-12,-.03f,37.2f),new Vector3(-12,0,8),west});
        Path("东坊前庭支路",new[]{new Vector3(0,-.03f,37.2f),new Vector3(16,0,37.2f),new Vector3(16,0,25),east});
        Path("门庭横接",new[]{new Vector3(5.3f,-.03f,62),new Vector3(10,-.03f,62)});
        Path("门后北坊支路",new[]{new Vector3(10,-.03f,65.5f),new Vector3(10,0,68),new Vector3(20,0,68),north});
        Foundation("门庭厚台基",new Vector2(10,63),new Vector2(5.8f,5.8f),-.57f);
        Foundation("测候台厚台基",new Vector2(2.65f,63.08f),new Vector2(6.1f,5.16f),-.57f);
        Foundation("量水道岸基",new Vector2(2.66f,54.6f),new Vector2(3.2f,11.95f),-.57f);
        Foundation("苏醒室分段石基",new Vector2(0,3.55f),new Vector2(13.2f,13.35f),-.45f);
        Foundation("通廊石基",new Vector2(0,14),new Vector2(6.6f,8),-.45f);
        Foundation("试炼间分段石基",new Vector2(0,26),new Vector2(14.9f,16.6f),-.45f);
        Foundation("校规庭分段石基",new Vector2(3.35f,43.95f),new Vector2(17.9f,10),-.57f);
        proof.Add("SUMMARY|spatialChunks="+serial+"|maxActiveRenderer="+serial+"|newBoxColliders="+(content.GetComponentsInChildren<Collider>(true).Length-initialColliders));
        proof.Add("No original renderer, collider or scene asset edited. Added simple floor/rail BoxColliders only; no MeshColliders/audio/lights/scripts. Parent must remove crossing old rail barriers and wire actual portal triggers.");
        File.WriteAllLines(Log+"connectivity-build.txt",proof);AssetDatabase.SaveAssets();
    }
    static Vector3 Anchor(Vector3 expected)
    {
        // Architecture's runtime group uses a Chinese name, not its asset directory.
        // Exact marker name + nearest expected world point identifies the intended door.
        var anchors=content.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="WalkwayConnection").OrderBy(t=>Vector2.Distance(new Vector2(t.position.x,t.position.z),new Vector2(expected.x,expected.z))).ToArray();
        if(anchors.Length>0&&Vector2.Distance(new Vector2(anchors[0].position.x,anchors[0].position.z),new Vector2(expected.x,expected.z))<10)
        {proof.Add("ANCHOR_ACTUAL|"+Ancestry(anchors[0])+"|world="+anchors[0].position);return anchors[0].position;}
        proof.Add("ANCHOR_EXPLICIT_FALLBACK|No nearby ArchitectureLOD WalkwayConnection; supplied contract point="+expected);return expected;
    }
    static string Ancestry(Transform t)=>t.parent?Ancestry(t.parent)+"/"+t.name:t.name;
    static void Path(string name,Vector3[] anchors)
    {
        var points=new List<Vector3>();
        for(int segment=0;segment<anchors.Length-1;segment++)
        {
            var a=anchors[segment];var b=anchors[segment+1];int count=Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/.85f);
            for(int j=segment==0?0:1;j<=count;j++)points.Add(Vector3.Lerp(a,b,j/(float)count));
        }
        float[] desired=points.Select(p=>p.y).ToArray(),height=(float[])desired.Clone();
        for(int i=0;i<points.Count;i++)
        {
            Vector3 dir=i==points.Count-1?points[i]-points[i-1]:points[i+1]-points[i];dir.y=0;dir.Normalize();var right=Vector3.Cross(Vector3.up,dir);
            float maxGround=float.NegativeInfinity;
            for(int along=-1;along<=1;along++)for(int side=-1;side<=1;side++)
            {var p=points[i]+dir*(along*.35f)+right*(side*Width*.5f);maxGround=Mathf.Max(maxGround,H(p.x,p.z));}
            height[i]=Mathf.Max(height[i],maxGround+.18f);
            proof.Add("ROAD_SUPPORT9|"+name+"|index="+i+"|xz="+points[i]+"|maxGround="+maxGround+"|requestedTop="+desired[i]);
        }
        // Raise-only Lipschitz envelope: no segment can exceed tan12 even over ridges.
        for(int pass=0;pass<points.Count;pass++)
        {
            bool changed=false;
            for(int i=1;i<points.Count;i++)
            {
                float d=Vector2.Distance(new Vector2(points[i].x,points[i].z),new Vector2(points[i-1].x,points[i-1].z));float limit=Slope*d;
                if(height[i]<height[i-1]-limit){height[i]=height[i-1]-limit;changed=true;}
                if(height[i-1]<height[i]-limit){height[i-1]=height[i]-limit;changed=true;}
            }
            if(!changed)break;
        }
        if(height[0]>desired[0]+.025f||height[height.Length-1]>desired[desired.Length-1]+.025f)
            throw new Exception("Road cannot meet exact endpoint at <=12deg without terrain obstruction: "+name+" startRise="+(height[0]-desired[0])+" endRise="+(height[height.Length-1]-desired[desired.Length-1]));
        height[0]=desired[0];height[height.Length-1]=desired[desired.Length-1];
        for(int i=0;i<points.Count;i++)points[i]=new Vector3(points[i].x,height[i],points[i].z);
        int chunkCount=Mathf.CeilToInt((points.Count-1)/24f),chunkLength=Mathf.CeilToInt((points.Count-1)/(float)chunkCount);
        for(int start=0;start<points.Count-1;start+=chunkLength)
        {
            int end=Mathf.Min(points.Count-1,start+chunkLength);var group=Group(name+"_段"+start);var levels=new[]{new Geometry(),new Geometry(),new Geometry()};
            for(int i=start;i<end;i++)
            {
                Vector3 a=points[i],b=points[i+1],dir=(b-a).normalized;Quaternion rotation=Quaternion.LookRotation(dir,Vector3.up);float length=Vector3.Distance(a,b);
                for(int lod=0;lod<3;lod++)levels[lod].Box((a+b)*.5f-rotation*Vector3.up*(Thickness*.5f),new Vector3(Width,Thickness,length+.018f),rotation);
                bool nearOpening=anchors.Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2((a.x+b.x)*.5f,(a.z+b.z)*.5f))<1.8f);
                if(i%3==0||i==start)
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        Vector3 p=a+rotation*Vector3.right*(side*(Width*.5f-.07f));
                        if(!nearOpening)for(int lod=0;lod<2;lod++)if(lod==0||i%6==0)levels[lod].Box(p+Vector3.up*.37f,new Vector3(.13f,.74f,.13f),Quaternion.identity);
                    }
                    // Bearing piers every~2.5m; base samples include four corners+center.
                    Pier(levels,a-Vector3.up*Thickness,Width*.65f,.62f);
                }
                if(!nearOpening)for(int side=-1;side<=1;side+=2)
                {
                    Vector3 offset=rotation*Vector3.right*(side*(Width*.5f-.07f))+Vector3.up*.63f;
                    for(int lod=0;lod<2;lod++)levels[lod].Box((a+b)*.5f+offset,new Vector3(.10f,.10f,length+.02f),rotation);
                }
            }
            // Square landing caps seal miter joints while staying at shared path height.
            foreach(var corner in anchors.Skip(1).Take(Math.Max(0,anchors.Length-2)))
            {
                int i=points.FindIndex(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(corner.x,corner.z))<.01f);
                if(i>=start&&i<=end)
                {
                    var p=points[i];for(int lod=0;lod<3;lod++)levels[lod].Box(p-Vector3.up*(Thickness*.5f),new Vector3(Width,Thickness,Width),Quaternion.identity);Collider(group.transform,"转角连续面",p-Vector3.up*(Thickness*.5f),new Vector3(Width,Thickness,Width),Quaternion.identity);
                    int index=Array.IndexOf(anchors,corner);Vector3 incoming=anchors[index]-anchors[index-1],outgoing=anchors[index+1]-anchors[index];incoming.y=outgoing.y=0;incoming.Normalize();outgoing.Normalize();
                    if(Mathf.Abs(Vector3.Dot(incoming,outgoing))<.05f)
                    {
                        // Guard the two outside edges of a 90-degree landing; leave
                        // incoming/outgoing edges open instead of blocking the bend.
                        foreach(var normal in new[]{incoming,-outgoing})
                        {
                            var rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,normal),Vector3.up);
                            var edge=p+normal*(Width*.5f);
                            for(int lod=0;lod<2;lod++)levels[lod].Box(edge+Vector3.up*.63f,new Vector3(.10f,.10f,Width),rotation);
                            Collider(group.transform,"转角外缘护栏",edge+Vector3.up*.36f,new Vector3(.14f,.75f,Width),rotation);
                        }
                    }
                }
            }
            ColliderRuns(group.transform,points,start,end,anchors);
            Finish(group,levels);
        }
        proof.Add("PATH|"+name+"|start="+points[0]+"|end="+points[points.Count-1]+"|width=2.1|samples="+points.Count+"|maxSlopeDegrees="+Enumerable.Range(0,points.Count-1).Max(i=>Mathf.Atan2(Mathf.Abs(points[i+1].y-points[i].y),Vector2.Distance(new Vector2(points[i].x,points[i].z),new Vector2(points[i+1].x,points[i+1].z)))*Mathf.Rad2Deg));
    }
    static void ColliderRuns(Transform group,List<Vector3> points,int start,int end,Vector3[] anchors)
    {
        bool Opening(int i)=>anchors.Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2((points[i].x+points[i+1].x)*.5f,(points[i].z+points[i+1].z)*.5f))<1.8f);
        for(int i=start;i<end;)
        {
            int stop=i+1;Vector3 direction=(points[stop]-points[i]).normalized;bool opening=Opening(i);
            // Merge up to four collinear .85m cells; preserve all grade breaks,
            // bends and junction gaps. Static collision count drops about3x.
            while(stop<end&&stop-i<4&&Opening(stop)==opening&&Vector3.Dot((points[stop+1]-points[stop]).normalized,direction)>.99999f)stop++;
            Vector3 a=points[i],b=points[stop];var rotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);float length=Vector3.Distance(a,b);
            Collider(group,"连续路面",(a+b)*.5f-rotation*Vector3.up*(Thickness*.5f),new Vector3(Width,Thickness,length+.024f),rotation);
            if(!opening)for(int side=-1;side<=1;side+=2)
                Collider(group,"低护栏碰撞",(a+b)*.5f+rotation*Vector3.right*(side*(Width*.5f+.01f))+Vector3.up*.36f,new Vector3(.14f,.75f,length+.03f),rotation);
            i=stop;
        }
    }
    static void Pier(Geometry[] geometry,Vector3 top,float width,float depth,int onlyLOD=-1)
    {
        float low=H(top.x,top.z);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})low=Mathf.Min(low,H(top.x+x*width*.5f,top.z+z*depth*.5f));
        float height=top.y-low+.20f;if(height<=.05f)return;
        for(int lod=0;lod<3;lod++)if(onlyLOD<0||onlyLOD==lod)geometry[lod].Box(new Vector3(top.x,top.y-height*.5f,top.z),new Vector3(width,height,depth),Quaternion.identity);
        proof.Add("PIER_SUPPORT5|top="+top+"|terrainMin="+low+"|base="+(low-.2f));
    }
    static void Foundation(string name,Vector2 center,Vector2 size,float top)
    {
        var group=Group(name);var geometry=new[]{new Geometry(),new Geometry(),new Geometry()};
        for(int lod=0;lod<3;lod++)for(int side=0;side<4;side++)
        {
            bool alongZ=side<2;float length=alongZ?size.y:size.x;int count=Mathf.CeilToInt(length/(1.75f*(1<<lod)));
            for(int j=0;j<count;j++)
            {
                float d=(j+.5f)/count-.5f;float x=center.x+(alongZ?(side==0?-1:1)*size.x*.5f:d*size.x),z=center.y+(alongZ?d*size.y:(side==2?-1:1)*size.y*.5f);
                float width=alongZ?.34f:length/count-.025f,depth=alongZ?length/count-.025f:.34f;
                Pier(geometry,new Vector3(x,top,z),width,depth,lod);
            }
        }
        Finish(group,geometry);proof.Add("FOUNDATION|"+name+"|center="+center+"|size="+size+"|top="+top+"|segmented textured stone contacts actual terrain");
    }
    static GameObject Group(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(content,false);return go;}
    static void Collider(Transform parent,string name,Vector3 center,Vector3 size,Quaternion rotation)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=center;go.transform.rotation=rotation;go.AddComponent<BoxCollider>().size=size;
    }
    static void Finish(GameObject group,Geometry[] geometry)
    {
        var counts=geometry.Select(g=>g.t.Count/3).ToArray();
        bool tiny=counts[0]<=96;
        if(!tiny&&!(counts[0]>counts[1]&&counts[1]>counts[2]))throw new Exception("Nontrivial connectivity chunk must strictly simplify: "+group.name+" "+string.Join(",",counts));
        int levelCount=tiny?1:3;var levels=new LOD[levelCount];
        for(int lod=0;lod<levelCount;lod++)
        {
            string id="Connectivity_"+serial+"_LOD"+lod;var mesh=geometry[lod].Mesh(id);var stored=AssetDatabase.LoadAssetAtPath<Mesh>(Art+id+".asset");
            if(stored){EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(stored);}else{stored=mesh;AssetDatabase.CreateAsset(stored,Art+id+".asset");}
            var go=new GameObject("LOD"+lod);go.transform.SetParent(group.transform,false);go.AddComponent<MeshFilter>().sharedMesh=stored;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=lod==0?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=lod<2;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.lightProbeUsage=LightProbeUsage.Off;go.isStatic=true;
            levels[lod]=new LOD(lod==0?.45f:lod==1?.16f:.001f,new Renderer[]{r});
            proof.Add("CHUNK|"+serial+"|"+group.name+"|LOD"+lod+"|triangles="+stored.triangles.Length/3);
        }
        if(!tiny){var lodGroup=group.AddComponent<LODGroup>();lodGroup.fadeMode=LODFadeMode.None;lodGroup.animateCrossFading=false;lodGroup.SetLODs(levels);lodGroup.RecalculateBounds();}
        else proof.Add("SINGLE_MESH|"+group.name+"|triangles="+counts[0]+"|tiny geometry has no duplicate fake LODs");
        serial++;
    }
}
