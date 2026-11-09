// EXTERIOR-WORLD-LOD-20261109. Offline authored opaque nature, no scene save.
// Parent supplies actual triangle-mesh height sampling; there is deliberately no fallback terrain formula.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class NatureBuilder
{
    const string Art="Assets/Art/Environment/Opening/NatureLOD/";
    const string Log="Logs/exterior-world-lod-20261109/nature/";
    static Func<float,float,float> ground;
    static readonly List<string> evidence=new List<string>();
    static Material stone,bark,foliage,bakedPineMaterial;
    static readonly List<Bounds> buildingBounds=new List<Bounds>();
    static readonly List<Vector3[]> protectedPaths=new List<Vector3[]>();
    static readonly List<MeshFilter[]> pineSources=new List<MeshFilter[]>();
    static Vector3 bakedRootPivot;static float bakedSourceHeight,bakedLODBaseLift;
    static readonly List<Surface[]> groundLODs=new List<Surface[]>();
    sealed class Surface
    {
        public Vector3[] vertices;public int[] triangles;public Bounds bounds;
        public bool Height(float x,float z,out float height)
        {
            height=float.NegativeInfinity;
            if(x<bounds.min.x-.001f||x>bounds.max.x+.001f||z<bounds.min.z-.001f||z>bounds.max.z+.001f)return false;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                if(x<Mathf.Min(a.x,Mathf.Min(b.x,c.x))-.0001f||x>Mathf.Max(a.x,Mathf.Max(b.x,c.x))+.0001f||z<Mathf.Min(a.z,Mathf.Min(b.z,c.z))-.0001f||z>Mathf.Max(a.z,Mathf.Max(b.z,c.z))+.0001f)continue;
                float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.000001f)continue;
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det,w=1-u-v;
                if(u>=-.0001f&&v>=-.0001f&&w>=-.0001f)height=Mathf.Max(height,u*a.y+v*b.y+w*c.y);
            }
            return !float.IsNegativeInfinity(height);
        }
    }
    static readonly float[] transitions={.50f,.20f,.0015f};
    struct Piece {public int kind,seed;public Vector3 p;public float radius,height,yaw;}
    class Geometry
    {
        public readonly List<Vector3> v=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> t=new List<int>();
        public readonly List<Vector3> importedNormals=new List<Vector3>();
        public void Tri(Vector3 a,Vector3 b,Vector3 c)
        {
            int i=v.Count;v.Add(a);v.Add(b);v.Add(c);var n=Vector3.Cross(b-a,c-a).normalized;
            foreach(var p in new[]{a,b,c})uv.Add(Mathf.Abs(n.y)>.65f?new Vector2(p.x,p.z)*.28f:Mathf.Abs(n.x)>.65f?new Vector2(p.z,p.y)*.28f:new Vector2(p.x,p.y)*.28f);
            t.Add(i);t.Add(i+1);t.Add(i+2);
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(a,b,c);Tri(a,c,d);}
        public Mesh Mesh(string name,bool smooth=false){var m=new Mesh{name=name};if(v.Count>65000)m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();if(importedNormals.Count==v.Count&&v.Count>0)m.SetNormals(importedNormals);else if(smooth){var normals=m.normals;var sums=new Dictionary<Vector3,Vector3>();for(int i=0;i<v.Count;i++){var key=v[i];if(!sums.ContainsKey(key))sums[key]=Vector3.zero;sums[key]+=normals[i];}for(int i=0;i<v.Count;i++)normals[i]=sums[v[i]].normalized;m.normals=normals;}m.RecalculateTangents();m.RecalculateBounds();return m;}
    }
    static float H(float x,float z)
    {
        // Grounding must survive independent valley/ridge LOD choices for shrubs,
        // moss and rock bases as well as trees. A LOD0-only floor can leave stems airborne.
        float y=groundLODs.Count>0?LowestVisibleSurface(x,z,new float[3]):ground(x,z);
        if(float.IsNaN(y)||float.IsInfinity(y))throw new Exception("No actual triangle ground at "+x+","+z);return y;
    }
    static float Noise(float x,float z,int seed)=>Mathf.PerlinNoise(x*.37f+seed*.071f,z*.31f+seed*.019f);
    static void BuildGroundLODSupport(Transform root)
    {
        groundLODs.Clear();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            string path=AssetDatabase.GetAssetPath(filter.sharedMesh);bool valley=path.EndsWith("/ValleyLandscape.asset",StringComparison.OrdinalIgnoreCase);
            if(!valley&&!(path.Contains("/MountainPolish/Ridge_")&&path.EndsWith(".obj",StringComparison.OrdinalIgnoreCase)))continue;
            var stack=new Surface[3];
            for(int lod=0;lod<3;lod++)
            {
                // Identical true source sampling as ApplyExistingLandscapeLOD; temporary
                // meshes never enter the scene or AssetDatabase and are destroyed below.
                var mesh=lod==0?filter.sharedMesh:SampleGrid(filter.sharedMesh,1<<lod,valley,"ReadOnlyGroundSupport");
                var points=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
                stack[lod]=new Surface{vertices=points,triangles=mesh.triangles,bounds=bounds};
                if(lod>0)Object.DestroyImmediate(mesh);
            }
            groundLODs.Add(stack);
        }
        if(groundLODs.Count!=9)throw new Exception("Expected actual valley + eight mountain surfaces for tree LOD support, got "+groundLODs.Count);
    }
    static float LowestVisibleSurface(float x,float z,float[] uniformHighest)
    {
        for(int lod=0;lod<3;lod++)uniformHighest[lod]=float.NegativeInfinity;
        float worstCase=float.NegativeInfinity;
        foreach(var stack in groundLODs)
        {
            float lowest=float.PositiveInfinity;bool hit=false;
            for(int lod=0;lod<3;lod++)if(stack[lod].Height(x,z,out float y)){hit=true;lowest=Mathf.Min(lowest,y);uniformHighest[lod]=Mathf.Max(uniformHighest[lod],y);}
            if(hit)worstCase=Mathf.Max(worstCase,lowest);
        }
        if(float.IsNegativeInfinity(worstCase))throw new Exception("No real LOD surface supports tree at "+x+","+z);
        // max(surface min(LOD heights)) is conservative even when each mountain and
        // the valley select DIFFERENT LODs, unlike min(max(all surfaces at one LOD)).
        return worstCase;
    }
    static float SegmentDistance(Vector2 p,Vector3 a,Vector3 b)
    {var start=new Vector2(a.x,a.z);var d=new Vector2(b.x-a.x,b.z-a.z);return Vector2.Distance(p,start+d*Mathf.Clamp01(Vector2.Dot(p-start,d)/Mathf.Max(d.sqrMagnitude,.0001f)));}
    static void Protect(Transform root)
    {
        buildingBounds.Clear();protectedPaths.Clear();
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.Find("WalkwayConnection")))
        {
            var rs=t.GetComponentsInChildren<MeshRenderer>(true);if(rs.Length==0)continue;var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);buildingBounds.Add(b);
        }
        protectedPaths.Add(new[]{new Vector3(0,0,34.2f),new Vector3(0,0,44),new Vector3(2.65f,0,49),new Vector3(2.65f,0,66)});
        protectedPaths.Add(new[]{new Vector3(0,0,37.2f),new Vector3(-12,0,37.2f),new Vector3(-12,0,8),new Vector3(-18.25f,0,8)});
        protectedPaths.Add(new[]{new Vector3(0,0,37.2f),new Vector3(16,0,37.2f),new Vector3(16,0,25),new Vector3(21.95f,0,25)});
        protectedPaths.Add(new[]{new Vector3(5.3f,0,62),new Vector3(10,0,62),new Vector3(10,0,65.5f),new Vector3(10,0,68),new Vector3(20,0,68),new Vector3(23.65f,0,63.971f)});
    }
    static bool Safe(Piece piece)
    {
        float x=piece.p.x,z=piece.p.z;
        float foot=piece.kind==5?.45f*piece.height/5:piece.kind==1?.30f:piece.radius;
        if(Mathf.Abs(x)+foot>79||z-foot< -32||z+foot>114)return false;
        if(Mathf.Abs(x)<8.4f+foot&&z>-4-foot&&z<34.8f+foot)return false;
        // Courtyard and door platform, not a blanket strip through all useful middle ground.
        if(x>-5.8f-foot&&x<12.6f+foot&&z>38.7f-foot&&z<49.3f+foot)return false;
        if(x>6.7f-foot&&x<13.3f+foot&&z>59.7f-foot&&z<66.3f+foot)return false;
        // Keep channel/armillary sightline open. Canopies at x<-8 or x>15 frame it.
        if(x>-4.5f-foot&&x<8.3f+foot&&z>48&&z<73)return false;
        foreach(var path in protectedPaths)for(int i=0;i<path.Length-1;i++)if(SegmentDistance(new Vector2(x,z),path[i],path[i+1])<1.25f+foot)return false;
        float crown=piece.kind==5?piece.radius*.66f:foot;
        foreach(var b in buildingBounds)if(x>b.min.x-crown-.55f&&x<b.max.x+crown+.55f&&z>b.min.z-crown-.55f&&z<b.max.z+crown+.55f)return false;
        return true;
    }
    // Every authored root is assessed at nine real terrain points before it is accepted.
    static bool Support(ref Piece p,float allowedDelta)
    {
        for(int attempt=0;attempt<3;attempt++)
        {
            if(!Safe(p)){evidence.Add("SKIP_CLEARANCE|kind="+p.kind+"|seed="+p.seed+"|at="+p.p);return false;}
            var heights=new List<float>();
            float supportRadius=p.kind==5?.45f*p.height/5:p.kind==1?.30f:p.radius;
            // Preserve the original detailed-surface steepness screen; lower LOD
            // grounding must not newly admit rocks on previously rejected cliffs.
            for(int iz=-1;iz<=1;iz++)for(int ix=-1;ix<=1;ix++)heights.Add(ground(p.p.x+ix*supportRadius,p.p.z+iz*supportRadius));
            float delta=heights.Max()-heights.Min();
            if(delta<=allowedDelta)
            {
                if(p.kind==1||p.kind==5)
                {
                    float lowest=float.PositiveInfinity;var minimumByLOD=new[]{float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
                    for(int iz=-1;iz<=1;iz++)for(int ix=-1;ix<=1;ix++)
                    {
                        var uniform=new float[3];lowest=Mathf.Min(lowest,LowestVisibleSurface(p.p.x+ix*supportRadius,p.p.z+iz*supportRadius,uniform));
                        for(int lod=0;lod<3;lod++)minimumByLOD[lod]=Mathf.Min(minimumByLOD[lod],uniform[lod]);
                    }
                    p.p.y=lowest;
                    evidence.Add("TREE_LOD_ROOT|xz="+p.p.x+","+p.p.z+"|uniformMin9="+string.Join(",",minimumByLOD.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"|mixedLODMin9="+lowest+"|extraEmbed=.16");
                    if(p.kind==5)CorrectActualRootContact(ref p);
                }
                else p.p.y=H(p.p.x,p.p.z);
                evidence.Add("SUPPORT9|kind="+p.kind+"|seed="+p.seed+"|center="+p.p+"|radius="+p.radius+"|min="+heights.Min()+"|max="+heights.Max()+"|delta="+delta+"|heights="+string.Join(",",heights.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
                return true;
            }
            p.radius*=.78f;
        }
        evidence.Add("SKIP_STEEP|kind="+p.kind+"|seed="+p.seed+"|at="+p.p);return false;
    }
    public static void Build(Transform root,Scene scene,Func<float,float,float> groundHeight)
    {
        if(!root||root.gameObject.scene!=scene||groundHeight==null)throw new ArgumentException("Nature requires parent root/scene and actual terrain height sampler");
        ground=groundHeight;evidence.Clear();Directory.CreateDirectory(Art);Directory.CreateDirectory(Log);AssetDatabase.Refresh();
        stone=MakeMaterial("NatureSlate",new Color(.64f,.70f,.75f),0);
        bark=MakeMaterial("DarkOak",new Color(.40f,.36f,.29f),1);
        foliage=MakeMaterial("GreyGreenNeedles",new Color(.37f,.43f,.32f),2);
        Protect(root);BuildGroundLODSupport(root);pineSources.Clear();
        foreach(var path in new[]{"Assets/Art/Environment/Opening/ExteriorLOD/MountainPine_LOD1.fbx","Assets/Art/Environment/Opening/ExteriorLOD/MountainPine_NatureMedium.fbx","Assets/Art/Environment/Opening/ExteriorLOD/MountainPine_LOD2.fbx"})
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Missing real baked pine LOD "+path);
            pineSources.Add(asset.GetComponentsInChildren<MeshFilter>());
        }
        bakedPineMaterial=pineSources[0][0].GetComponent<MeshRenderer>().sharedMaterial;
        if(!bakedPineMaterial)throw new Exception("Baked pine atlas material missing");
        var sourcePoints=new Vector3[3][];
        for(int lod=0;lod<3;lod++){var points=new List<Vector3>();foreach(var filter in pineSources[lod])foreach(var p in filter.sharedMesh.vertices)points.Add(filter.transform.TransformPoint(p));sourcePoints[lod]=points.ToArray();}
        // Performance levels start at old LOD1 now, but root normalization MUST
        // remain anchored to the untouched original 4400-triangle source.
        var reference=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Opening/MountainPine/MountainPine.fbx");
        if(!reference)throw new Exception("Original pine root reference missing");
        var referencePoints=new List<Vector3>();foreach(var filter in reference.GetComponentsInChildren<MeshFilter>())foreach(var p in filter.sharedMesh.vertices)referencePoints.Add(filter.transform.TransformPoint(p));
        float sourceMin=referencePoints.Min(p=>p.y);bakedSourceHeight=referencePoints.Max(p=>p.y)-sourceMin;
        var roots=referencePoints.Where(p=>p.y<sourceMin+.12f).ToArray();bakedRootPivot=new Vector3(roots.Average(p=>p.x),sourceMin,roots.Average(p=>p.z));
        bakedLODBaseLift=Mathf.Max(0,sourcePoints.Max(points=>points.Min(p=>p.y))-sourceMin);
        for(int lod=0;lod<3;lod++)
        {
            var bound=new Bounds(sourcePoints[lod][0],Vector3.zero);foreach(var p in sourcePoints[lod])bound.Encapsulate(p);
            evidence.Add("BAKED_SOURCE_BOUNDS|LOD"+lod+"|min="+bound.min+"|max="+bound.max+"|pivot="+bakedRootPivot+"|sourceHeight="+bakedSourceHeight+"|maxLODBaseLift="+bakedLODBaseLift);
        }
        int accepted=0;long[] totals={0,0,0};int block=0,bakedAccepted=0;
        // Explicit player-visible clusters replace the former empty middle-ground exclusion.
        var centers=new[]{new Vector2(-16,35),new Vector2(-16,52),new Vector2(-8.5f,75),new Vector2(20,47),new Vector2(21,83),new Vector2(-58,34),new Vector2(-50,96),new Vector2(61,84)};
        var nearTrees=new[]{new[]{new Vector3(-15.5f,5.5f,34.7f),new Vector3(-19,4.7f,39.0f)},new[]{new Vector3(-14,6.0f,51),new Vector3(-18.2f,4.8f,55.5f)},new[]{new Vector3(-8.5f,5.8f,75)},new[]{new Vector3(18.7f,5.0f,46),new Vector3(22.5f,4.4f,48.5f)},new[]{new Vector3(18,5.2f,81),new Vector3(24,4.3f,85)}};
        foreach(var center in centers)
        {
            var pieces=new List<Piece>();var rng=new System.Random(11909+block*733);bool close=block<5;
            void Add(int kind,float x,float z,float radius,float height)
            {
                var p=new Piece{kind=kind,p=new Vector3(x,0,z),radius=radius,height=height,seed=rng.Next(10000),yaw=(float)rng.NextDouble()*360};
                if(Support(ref p,kind==0?2.7f:kind==1||kind==5?1.2f:.95f)){pieces.Add(p);accepted++;if(kind==5)bakedAccepted++;}
            }
            if(close)foreach(var p in nearTrees[block])Add(5,p.x,p.z,3.45f*p.y/5,p.y);
            else{Add(1,center.x-2,center.y+1,1.55f,5.5f);Add(1,center.x+5,center.y+7,1.2f,4.4f);}
            // One broad, low, planted layered shelf in each cluster, not triangular pillars.
            Add(0,center.x+(close?-2:3),center.y-3,close?2.5f:4.0f,close?.75f:1.45f);
            int shrubs=close?3:1;
            for(int i=0;i<shrubs;i++)Add(2,center.x+(i-1)*1.7f,center.y-1.5f+(i%2)*3,.65f+(float)rng.NextDouble()*.20f,.45f+(float)rng.NextDouble()*.2f);
            Add(3,center.x,center.y+2.2f,close?1.8f:2.5f,.035f);
            if(pieces.Count==0){block++;continue;}
            var group=new GameObject("自然区块_"+block+(close?"_近中景实松":"_山坡树带"));SceneManager.MoveGameObjectToScene(group,scene);group.transform.SetParent(root,false);
            var lods=new LOD[3];
            for(int lod=0;lod<3;lod++)
            {
                var geometry=new[]{new Geometry(),new Geometry(),new Geometry(),new Geometry()};
                foreach(var piece in pieces)
                {
                    if(piece.kind==0)Rock(geometry[0],piece,lod);
                    else if(piece.kind==1)Pine(geometry[1],geometry[2],piece,lod,true);
                    else if(piece.kind==5)BakedPine(geometry[3],piece,lod);
                    else if(piece.kind==2)Bush(geometry[1],geometry[2],piece,lod);
                    else Moss(geometry[2],piece,lod);
                }
                var level=new GameObject("LOD"+lod);level.transform.SetParent(group.transform,false);var renderers=new List<Renderer>();
                for(int mat=0;mat<4;mat++)
                {
                    if(geometry[mat].t.Count==0)continue;
                    for(int i=0;i<geometry[mat].v.Count;i++)geometry[mat].v[i]=root.InverseTransformPoint(geometry[mat].v[i]);
                    var mesh=Save(geometry[mat].Mesh("NatureBlock"+block+"_LOD"+lod+"_M"+mat,mat>=2),"NatureBlock"+block+"_LOD"+lod+"_M"+mat);
                    var go=new GameObject(new[]{"层岩","栎木枝干","松针灌苔","烘焙山松图集"}[mat]);go.transform.SetParent(level.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=new[]{stone,bark,foliage,bakedPineMaterial}[mat];
                    renderer.shadowCastingMode=lod==0&&mat!=2?ShadowCastingMode.On:ShadowCastingMode.Off;
                    renderer.receiveShadows=lod<2;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.lightProbeUsage=LightProbeUsage.Off;
                    go.isStatic=true;renderers.Add(renderer);totals[lod]+=geometry[mat].t.Count/3;
                }
                lods[lod]=new LOD(transitions[lod],renderers.ToArray());
            }
            var lodGroup=group.AddComponent<LODGroup>();lodGroup.fadeMode=LODFadeMode.None;lodGroup.animateCrossFading=false;lodGroup.SetLODs(lods);lodGroup.RecalculateBounds();
            evidence.Add("BLOCK|"+block+"|accepted="+pieces.Count+"|bakedPine="+pieces.Count(p=>p.kind==5)+"|boundsSize="+lodGroup.size+"|LOD=.50,.20,.0015|no_crossfade");block++;
        }
        evidence.Add("BAKED_PINE_ACCEPTED="+bakedAccepted+"/9 planned; old3source trees are separate and not changed");
        evidence.Add("SUMMARY|pieces="+accepted+"|LOD0="+totals[0]+"|LOD1="+totals[1]+"|LOD2="+totals[2]+"|blocks="+block+"|max_active_renderer_per_block=4|new_materials=3|reused_pine_atlas=1|colliders=0|runtime_scripts=0|grass_cards=0");
        evidence.Add("All SUPPORT9 samples use caller's real triangle sampler. Rock basal loops and moss vertices individually conform to that same sampler. Pine/bush trunk bottoms embedded0.16m. This is source construction evidence, not visual or Quest acceptance.");
        File.WriteAllLines(Log+"nature-build.txt",evidence);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("ArtSource/Environment/Opening/NatureLOD");
        File.WriteAllText("ArtSource/Environment/Opening/NatureLOD/provenance.txt","Original procedural layer/bush/far silhouettes plus existing baked Hunyuan MountainPine FBX/atlas and parent-decimated ExteriorLOD/MountainPine_LOD1/2. Seeds11909+block*733. No new image/model generation or alpha grass. Three explicit geometric LODs; baked pine UV/normals/atlas preserved. Caller's actual mesh sampler is required; no synthetic terrain fallback.\n");
    }
    static void Rock(Geometry g,Piece p,int lod)
    {
        int segments=new[]{16,10,6}[lod],rings=new[]{6,4,3}[lod];var points=new Vector3[rings,segments];
        float upperGround=p.p.y;
        for(int j=0;j<24;j++){float a=j*Mathf.PI*2/24+p.yaw*Mathf.Deg2Rad;upperGround=Mathf.Max(upperGround,H(p.p.x+Mathf.Cos(a)*p.radius*1.32f,p.p.z+Mathf.Sin(a)*p.radius*.64f));}
        for(int ring=0;ring<rings;ring++)for(int j=0;j<segments;j++)
        {
            float a=j*Mathf.PI*2/segments+p.yaw*Mathf.Deg2Rad;float f=ring/(float)(rings-1);
            // Wide buried foot, overhanging bedding lips then narrow top, not a pole.
            float shelf=1.20f-f*.40f+Mathf.Sin(f*26+p.seed)*.095f;
            float radius=p.radius*shelf*(.94f+.10f*Mathf.Sin(a*3+p.seed));
            float x=p.p.x+Mathf.Cos(a)*radius,z=p.p.z+Mathf.Sin(a)*radius*.48f;
            float bottom=H(x,z)-.20f;float y=ring==0?bottom:Mathf.Max(bottom+.07f,Mathf.Lerp(p.p.y,upperGround+p.height,f)+.075f*Mathf.Sin(a*3+p.seed)*f);
            points[ring,j]=new Vector3(x,y,z);
        }
        for(int ring=0;ring<rings-1;ring++)for(int j=0;j<segments;j++){int n=(j+1)%segments;g.Quad(points[ring,j],points[ring+1,j],points[ring+1,n],points[ring,n]);}
        Vector3 top=new Vector3(p.p.x,upperGround+p.height-.035f,p.p.z);
        for(int j=0;j<segments;j++)g.Tri(points[rings-1,j],top,points[rings-1,(j+1)%segments]);
    }
    static void BakedPine(Geometry geometry,Piece piece,int lod)
    {
        var placement=TreePlacement(piece);
        foreach(var filter in pineSources[lod])
        {
            var mesh=filter.sharedMesh;var positions=mesh.vertices;var normals=mesh.normals;var texture=mesh.uv;var matrix=placement*filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
            int offset=geometry.v.Count;
            for(int i=0;i<positions.Length;i++){geometry.v.Add(matrix.MultiplyPoint3x4(positions[i]));geometry.uv.Add(texture[i]);geometry.importedNormals.Add(normalMatrix.MultiplyVector(normals[i]).normalized);}
            geometry.t.AddRange(mesh.triangles.Select(i=>i+offset));
        }
    }
    static Matrix4x4 TreePlacement(Piece piece)
    {
        float scale=piece.height/bakedSourceHeight;float lean=(piece.seed%7-3)*1.1f;
        return Matrix4x4.TRS(piece.p-Vector3.up*(.16f+bakedLODBaseLift*scale),Quaternion.Euler(0,piece.yaw,0)*Quaternion.Euler(0,0,lean),Vector3.one*scale)*Matrix4x4.Translate(-bakedRootPivot);
    }
    static void CorrectActualRootContact(ref Piece piece)
    {
        // The nine-point trunk-radius screen does not cover this model's broad
        // exposed root claws. Inspect the REAL transformed basal vertices from
        // every rendered tree LOD, including their yaw, lean and root pivot.
        var placement=TreePlacement(piece);float maxGap=float.NegativeInfinity;
        Vector3 worst=Vector3.zero;float worstSurface=0;int worstLevel=-1;var counts=new int[3];
        for(int lod=0;lod<3;lod++)
        {
            var vertices=new HashSet<Vector3>();
            foreach(var filter in pineSources[lod])
            {var matrix=placement*filter.transform.localToWorldMatrix;foreach(var v in filter.sharedMesh.vertices)vertices.Add(matrix.MultiplyPoint3x4(v));}
            float baseY=vertices.Min(v=>v.y);
            foreach(var v in vertices.Where(v=>v.y<=baseY+.20f))
            {
                var uniform=new float[3];float surface=LowestVisibleSurface(v.x,v.z,uniform),gap=v.y-surface;counts[lod]++;
                if(gap>maxGap){maxGap=gap;worst=v;worstSurface=surface;worstLevel=lod;}
            }
        }
        if(counts.Any(n=>n==0)||float.IsInfinity(maxGap)||float.IsNaN(maxGap))throw new Exception("Actual pine root contact check missing: "+piece.p);
        float before=piece.p.y,drop=maxGap>0?maxGap+.05f:0;
        piece.p.y-=drop; // Only Y can decrease; X/Z, instances, pivot and LOD thresholds stay unchanged.
        evidence.Add("ROOT_VERTEX_CONTACT|xz="+piece.p.x+","+piece.p.z+"|seed="+piece.seed+"|uniqueRootVertices="+string.Join(",",counts)+"|rootBeforeY="+before+"|dropY="+drop+"|rootAfterY="+piece.p.y+"|maxGapBefore="+maxGap+"|maxGapAfter="+(maxGap-drop)+"|worstTreeLOD="+worstLevel+"|worstVertex="+worst+"|supportY="+worstSurface+"|lowRootBand=.20m|all3TreeLOD_and_mixedGroundLOD");
    }
    static void Pine(Geometry wood,Geometry green,Piece p,int lod,bool distant=false)
    {
        int sides=distant?new[]{6,5,4}[lod]:new[]{9,6,4}[lod];float height=p.height;Vector3 bottom=p.p-Vector3.up*.16f;
        Vector3 bend=new Vector3(Mathf.Cos(p.yaw)*.38f,0,Mathf.Sin(p.yaw)*.38f);
        Vector3 mid=p.p+Vector3.up*(height*.57f)+bend,top=p.p+Vector3.up*(height*.96f)+bend*.35f;
        Tube(wood,bottom,mid,.15f,.105f,sides);Tube(wood,mid,top,.105f,.038f,sides);
        var anchors=distant?new[]{0,4,8}:(lod==0?new[]{0,1,2,3,4,5,6,7,8}:lod==1?new[]{0,1,3,4,6,7,8}:new[]{0,2,4,6,8});
        foreach(int j in anchors)
        {
            float f=j/8f;float a=(p.yaw+j*137.5f)*Mathf.Deg2Rad;
            float reach=p.radius*(1-f*.61f)*(.83f+.17f*Mathf.Sin(j*3+p.seed));
            Vector3 start=p.p+Vector3.up*(height*(.43f+.47f*f+.045f*Mathf.Sin(j*13+p.seed)))+Vector3.Lerp(bend,bend*.35f,f);
            Vector3 end=start+new Vector3(Mathf.Cos(a)*reach,.17f,Mathf.Sin(a)*reach);
            if(lod<2)Tube(wood,start,end,.065f*(1-f*.5f),.022f,Mathf.Max(3,sides-1));
            else Tube(wood,start,end,.055f,.025f,3);
            if(distant)
                FarCrown(green,end,new Vector3(reach*.80f+.29f,.43f+.13f*(1-f),reach*.49f+.22f),a,p.seed+j*17,lod);
            else Pad(green,end,new Vector3(reach*.72f+.24f,.27f+.08f*(1-f),reach*.39f+.17f),a,p.seed+j*17,lod);
        }
    }
    static void FarCrown(Geometry g,Vector3 center,Vector3 radius,float yaw,int seed,int lod)
    {
        // Closed flattened ellipsoid with FIVE latitude rings + both pole caps.
        // All three crown layers retain volume even in LOD2; no umbrella apex/fan.
        int n=new[]{10,8,6}[lod];float[] latitude={-.86f,-.46f,0,.46f,.86f};
        var vertices=new Vector3[latitude.Length,n];
        for(int ring=0;ring<latitude.Length;ring++)for(int j=0;j<n;j++)
        {
            float a=j*Mathf.PI*2/n,t=latitude[ring];
            float outline=1+.085f*Mathf.Sin(a*3+seed)+.045f*Mathf.Cos(a*5+seed*.37f);
            float radial=Mathf.Sqrt(1-t*t)*outline;
            float x=Mathf.Cos(a)*radius.x*radial,z=Mathf.Sin(a)*radius.z*radial;
            float y=t*radius.y+.045f*radius.y*Mathf.Sin(a*2+seed)*(1-t*t);
            // Slightly biased, irregular lobes, connected to their real branch.
            x+=radius.x*.08f*t;z+=radius.z*.07f*Mathf.Sin(seed)*t;
            vertices[ring,j]=center+new Vector3(x*Mathf.Cos(yaw)+z*Mathf.Sin(yaw),y,-x*Mathf.Sin(yaw)+z*Mathf.Cos(yaw));
        }
        for(int ring=0;ring<latitude.Length-1;ring++)for(int j=0;j<n;j++)
        {int k=(j+1)%n;g.Quad(vertices[ring,j],vertices[ring+1,j],vertices[ring+1,k],vertices[ring,k]);}
        for(int j=0;j<n;j++)
        {int k=(j+1)%n;g.Tri(vertices[0,k],center-Vector3.up*radius.y,vertices[0,j]);g.Tri(vertices[4,j],center+Vector3.up*radius.y,vertices[4,k]);}
    }
    static void Bush(Geometry wood,Geometry green,Piece p,int lod)
    {
        int count=lod==0?2:1;
        for(int j=0;j<count;j++)
        {
            float a=j*2.4f+p.seed;float x=p.p.x+Mathf.Cos(a)*p.radius*.35f,z=p.p.z+Mathf.Sin(a)*p.radius*.35f;
            Vector3 basePoint=new Vector3(x,H(x,z)-.16f,z);var top=basePoint+Vector3.up*(p.height*.72f);
            Tube(wood,basePoint,top,.032f,.012f,lod<2?4:3);
            Pad(green,top,new Vector3(p.radius*.62f,p.height*.33f,p.radius*.49f),a,p.seed+j*37,lod,true);
        }
    }
    static void Pad(Geometry g,Vector3 center,Vector3 radius,float yaw,int seed,int lod,bool small=false)
    {
        int n=small?new[]{8,6,4}[lod]:new[]{12,8,5}[lod];int rings=3;var p=new Vector3[rings,n];
        for(int ring=0;ring<rings;ring++)for(int j=0;j<n;j++)
        {
            float a=j*Mathf.PI*2/n;float ripple=1+.14f*Mathf.Sin(a*3+seed)+.09f*Mathf.Cos(a*5+seed*.3f);
            float r=ring==0?.60f:ring==1?1:.34f;
            float x=Mathf.Cos(a)*radius.x*r*ripple,z=Mathf.Sin(a)*radius.z*r*ripple;
            float y=(ring==0?-.35f:ring==1?0:.85f)*radius.y+Mathf.Sin(a*2+seed)*radius.y*.12f;
            p[ring,j]=center+new Vector3(x*Mathf.Cos(yaw)+z*Mathf.Sin(yaw),y,-x*Mathf.Sin(yaw)+z*Mathf.Cos(yaw));
        }
        for(int ring=0;ring<rings-1;ring++)for(int j=0;j<n;j++){int k=(j+1)%n;g.Quad(p[ring,j],p[ring+1,j],p[ring+1,k],p[ring,k]);}
        for(int j=0;j<n;j++){int k=(j+1)%n;g.Tri(p[2,j],center+Vector3.up*radius.y,p[2,k]);g.Tri(p[0,k],center-Vector3.up*radius.y*.4f,p[0,j]);}
    }
    static void Moss(Geometry g,Piece p,int lod)
    {
        int n=new[]{20,12,8}[lod];Vector3 center=new Vector3(p.p.x,H(p.p.x,p.p.z)+.012f,p.p.z);
        for(int j=0;j<n;j++)
        {
            Vector3 Point(int k){float a=k*Mathf.PI*2/n;float r=p.radius*(.86f+.12f*Mathf.Sin(a*3+p.seed));float x=p.p.x+Mathf.Cos(a)*r,z=p.p.z+Mathf.Sin(a)*r*.63f;return new Vector3(x,H(x,z)-.018f,z);}
            // Low opaque soil/moss islands follow ground, border sunk beneath it.
            g.Tri(center,Point((j+1)%n),Point(j));
        }
    }
    static void Tube(Geometry g,Vector3 a,Vector3 b,float ra,float rb,int sides)
    {
        Vector3 axis=(b-a).normalized,right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized,up=Vector3.Cross(axis,right);
        for(int j=0;j<sides;j++)
        {
            float x=j*Mathf.PI*2/sides,y=(j+1)*Mathf.PI*2/sides;
            Vector3 da=right*Mathf.Cos(x)+up*Mathf.Sin(x),db=right*Mathf.Cos(y)+up*Mathf.Sin(y);
            g.Quad(a+da*ra,a+db*ra,b+db*rb,b+da*rb);
        }
    }
    static Mesh Save(Mesh mesh,string name)
    {
        var path=Art+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Material MakeMaterial(string name,Color tint,int kind)
    {
        const int n=256;var image=new Texture2D(n,n,TextureFormat.RGB24,false);var colors=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float u=x/(float)n,v=y/(float)n;float broad=Mathf.PerlinNoise(u*7+kind*9,v*7+3),fine=Mathf.PerlinNoise(u*62+3,v*62+kind*7);
            float detail=kind==0?-.07f*Mathf.Pow(Mathf.Abs(Mathf.Sin((v*11+Mathf.Sin(u*12)*.10f)*Mathf.PI)),18):kind==1?-.06f*Mathf.Pow(Mathf.Abs(Mathf.Sin((u*27+Mathf.Sin(v*11)*.1f)*Mathf.PI)),10):-.025f*Mathf.Pow(Mathf.Sin((u*37+v*19)*Mathf.PI),2);
            colors[y*n+x]=new Color(.72f,.73f,.72f)*( .89f+.12f*broad+.035f*fine+detail );
        }
        image.SetPixels(colors);image.Apply();var png=Art+name+"_Albedo.png";File.WriteAllBytes(png,image.EncodeToPNG());Object.DestroyImmediate(image);AssetDatabase.ImportAsset(png,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(png);importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=2;importer.maxTextureSize=256;
        var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=256;android.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+name+".mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Art+name+".mat");}
        mat.SetColor("_BaseColor",tint);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(png));mat.SetFloat("_Smoothness",.10f);mat.SetFloat("_Metallic",0);mat.SetFloat("_SpecularHighlights",0);mat.SetFloat("_EnvironmentReflections",0);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    // Add true sampled LOD meshes to existing mountain/valley renderers without changing LOD0 assets.
    public static void ApplyExistingLandscapeLOD(Transform root)
    {
        Directory.CreateDirectory(Art);Directory.CreateDirectory(Log);AssetDatabase.Refresh();var lines=new List<string>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            string path=AssetDatabase.GetAssetPath(filter.sharedMesh);
            bool valley=path.EndsWith("/ValleyLandscape.asset",StringComparison.OrdinalIgnoreCase);
            bool ridge=path.Contains("/MountainPolish/Ridge_")&&path.EndsWith(".obj",StringComparison.OrdinalIgnoreCase);
            var renderer=filter.GetComponent<MeshRenderer>();if((!valley&&!ridge)||!renderer||filter.GetComponent<LODGroup>())continue;
            var levels=new LOD[3];levels[0]=new LOD(.46f,new Renderer[]{renderer});
            for(int lod=1;lod<3;lod++)
            {
                var mesh=SampleGrid(filter.sharedMesh,1<<lod,valley,"Existing_"+Path.GetFileNameWithoutExtension(path)+"_LOD"+lod);
                mesh=Save(mesh,mesh.name);var go=new GameObject("采样LOD"+lod);go.transform.SetParent(filter.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=lod<2&&renderer.receiveShadows;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.lightProbeUsage=LightProbeUsage.Off;
                levels[lod]=new LOD(lod==1?.16f:.0005f,new Renderer[]{r});go.isStatic=true;
                lines.Add("SAMPLED_LOD|"+path+"|LOD"+lod+"|triangles="+mesh.triangles.Length/3+"|source_LOD0_unchanged");
            }
            var group=filter.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.animateCrossFading=false;group.SetLODs(levels);group.RecalculateBounds();
        }
        File.WriteAllLines(Log+"existing-landscape-lod.txt",lines);AssetDatabase.SaveAssets();
    }
    static Mesh SampleGrid(Mesh source,int step,bool valley,string name)
    {
        // Imported OBJ seam vertices are welded by actual local X/Z; height is sampled
        // verbatim from source positions, never recomputed from a synthetic function.
        var positions=source.vertices;var xs=positions.Select(v=>Mathf.RoundToInt(v.x*10000)).Distinct().OrderBy(x=>x).ToArray();
        var zs=positions.Select(v=>Mathf.RoundToInt(v.z*10000)).Distinct().OrderBy(z=>z).ToArray();
        var map=new Dictionary<string,Vector3>();foreach(var p in positions)map[Mathf.RoundToInt(p.x*10000)+","+Mathf.RoundToInt(p.z*10000)]=p;
        if(map.Count!=xs.Length*zs.Length)throw new Exception("Source is not a complete sampled heightfield: "+source.name);
        int[] Select(int[] values)=>Enumerable.Range(0,values.Length).Where(i=>i%step==0||i==values.Length-1).Select(i=>values[i]).ToArray();
        xs=Select(xs);zs=Select(zs);var points=new Vector3[xs.Length*zs.Length];
        for(int j=0;j<zs.Length;j++)for(int i=0;i<xs.Length;i++)points[j*xs.Length+i]=map[xs[i]+","+zs[j]];
        var geo=new Geometry();
        for(int j=0;j<zs.Length-1;j++)for(int i=0;i<xs.Length-1;i++)
        {int a=j*xs.Length+i,b=a+1,c=a+xs.Length,d=c+1;geo.Tri(points[a],points[c],points[b]);geo.Tri(points[b],points[c],points[d]);}
        // Match source texture metres: valley scree has a different scale from ridges.
        for(int i=0;i<geo.uv.Count;i++)geo.uv[i]=valley?new Vector2(geo.v[i].x,geo.v[i].z)*.16f:geo.uv[i]*(.045f/.28f);
        return geo.Mesh(name);
    }
}
