using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ApprovedWorkshopBuilder
{
    const string Root = "Assets/Art/Environment/SuspendedPath/";
    const string Out = Root + "ApprovedWorkshop/";
    const string Geometry = Root + "SuspendedPathGeometry.asset";
    const string Cliffs = Root + "SuspendedPathCliffs.asset";
    const string ArtRoot = "中层 A1 · 工坊桥口定稿";
    static Transform[] site;
    static Transform Find(string name) => site.Single(t => t && t.name == name);
    static T Load<T>(string p) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(p) ?? throw new InvalidOperationException("Missing " + p);
    static Mesh GetOriginal(string name, string pack) =>
        AssetDatabase.LoadAllAssetsAtPath(pack).OfType<Mesh>().Single(m => m.name == name);
    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string p = Out + name + ".asset";
        mesh.name = name;
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (!old) { AssetDatabase.CreateAsset(mesh, p); return mesh; }
        old.Clear(false);
        EditorUtility.CopySerialized(mesh, old);
        old.MarkModified();
        old.UploadMeshData(false);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(old);
        return old;
    }
    static Material MaterialAsset(string stem)
    {
        string p = Out + stem + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,p); }
        return m;
    }
    static Material Surface(string stem,string baseMap,string normal,float bump,float metal, string emissive=null)
    {
        var m=MaterialAsset(stem);
        m.SetTexture("_BaseMap", Load<Texture2D>(Out+baseMap+".png"));
        m.SetTexture("_BumpMap", Load<Texture2D>(Out+normal+".png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_BumpScale",bump);
        m.SetColor("_BaseColor",Color.white); m.SetFloat("_Metallic",metal);
        m.SetFloat("_Smoothness",1f);m.SetFloat("_SmoothnessTextureChannel",1f);m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        if(emissive!=null)
        {
            m.SetTexture("_EmissionMap",Load<Texture2D>(Out+emissive+".png"));m.SetColor("_EmissionColor",Color.white*.62f);m.EnableKeyword("_EMISSION");
        }
        m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if (!scene.name.StartsWith("Chapter02_SuspendedPath",StringComparison.Ordinal)) throw new Exception("Expected Chapter02_SuspendedPath, got "+scene.name);
        site=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).ToArray();
        var previous=site.SingleOrDefault(t=>t.name==ArtRoot); if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var group=new GameObject(ArtRoot);
        var paving=Surface("WorkshopFlagstones","Flagstones_BaseSmooth","Flagstones_NormalGL",.82f,0);
        var marker=Surface("SurveyInlay","SurveyInlay_BaseSmooth","SurveyInlay_Normal",.9f,.48f,"SurveyInlay_Emission");
        var guideTransforms=new List<Matrix4x4>();
        Restone("换篮地坪 · 双接驳缺口","HubSurface",paving,guideTransforms);
        Restone("中控导引嵌条","HubRoute",paving,guideTransforms,true);
        Restone("左行栈桥 · 通往工坊","BridgeSurface",paving,guideTransforms);
        Restone("工坊院场 · 地面与崖基","YardSurface",paving,guideTransforms);
        Restone("工坊路面嵌条","YardRoute",paving,guideTransforms,true);
        OpenPass("工坊栈桥 · 西崖肩","WestBridgeShoulder",false);
        OpenPass("谷口 · 北岸断层","NorthPass",true);
        // The near toe is an existing decorative rock outside the playable railing.
        var toe=Find("桥台西肩 · 临桥岩鼻");
        toe.position=new Vector3(22.55f,-3.66f,30.25f);
        toe.localScale=new Vector3(1,.70f,1);
        PrefabUtility.RecordPrefabInstancePropertyModifications(toe);
        BuildInlays(group.transform,guideTransforms,marker);
        DefineHearth(group.transform);
        RockFaces();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ApprovedWorkshop] Opened north and west sightlines, replaced walking surfaces. {guideTransforms.Count} two-LOD survey strips; original courts and collision retained.");
    }
    static void Restone(string node, string id, Material paving, List<Matrix4x4> places,bool singleInk=false)
    {
        var tr=Find(node);var mf=tr.GetComponent<MeshFilter>();var mr=tr.GetComponent<MeshRenderer>();
        var original=GetOriginal(node,Geometry);
        var mesh=UnityEngine.Object.Instantiate(original);
        var slots=mr.sharedMaterials;var uv=mesh.uv;var pos=mesh.vertices;var normals=mesh.normals;
        for(int s=0;s<slots.Length;s++)
        {
            var mat=slots[s];
            var tris=mesh.GetTriangles(s);
            if(singleInk || (node.StartsWith("左行栈桥") && s==5))
            {
                // Each original solid dash is a separate 36-triangle-index cuboid; derive its real frame from its cap.
                if(tris.Length%36!=0) throw new Exception($"Unexpected inlay source {node}: {tris.Length} indices");
                for(int i=0;i<tris.Length;i+=36)
                {
                    var corners=tris.Skip(i).Take(36).Select(k=>tr.TransformPoint(pos[k])).ToArray();
                    float top=corners.Max(p=>p.y);
                    var flat=corners.Where(p=>p.y>top-.0005f).Distinct().ToArray();
                    if(flat.Length<4) throw new Exception($"Invalid route cap {node} {i}");
                    var center=flat.Aggregate(Vector3.zero,(sum,p)=>sum+p)/flat.Length;
                    var along=flat.OrderBy(p=>(p-flat[0]).sqrMagnitude).Skip(2).First()-flat[0];
                    along.y=0; float len=along.magnitude;
                    places.Add(Matrix4x4.TRS(center-Vector3.up*.014f,Quaternion.LookRotation(along.normalized,Vector3.up),new Vector3(1,1,len/.70f)));
                }
                mesh.SetTriangles(Array.Empty<int>(),s,false);
            }
            else if(mat && (mat.name=="WorkshopPaving"||mat==paving))
            {
                foreach(int idx in tris.Distinct())
                {
                    Vector3 w=tr.TransformPoint(pos[idx]);Vector3 n=tr.TransformDirection(normals[idx]);
                    uv[idx]=Mathf.Abs(n.y)>.48f?new Vector2(w.x,w.z)/2.15f :
                        Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(w.z,w.y)/2.15f:new Vector2(w.x,w.y)/2.15f;
                }
                slots[s]=paving;
            }
            else if(mat && mat.name=="旧铜")
            {
                slots[s]=Load<Material>("Assets/Art/Environment/Opening/PreqinGateFittings/GateHammeredBronze.mat");
            }
        }
        mesh.uv=uv;mesh.RecalculateTangents();mesh.RecalculateBounds();
        mf.sharedMesh=SaveMesh(id,mesh);mr.sharedMaterials=slots;
        if(singleInk)mr.enabled=false;
    }
    static void OpenPass(string node,string name,bool north)
    {
        var tr=Find(node);var mf=tr.GetComponent<MeshFilter>();
        // Source collider is a distinct mesh. Leave all physical references untouched.
        var asset=mf.sharedMesh;
        if(AssetDatabase.GetAssetPath(asset).StartsWith(Out,StringComparison.Ordinal))
        {
            var srcs=AssetDatabase.LoadAllAssetsAtPath(Cliffs).OfType<Mesh>().Where(m=>m.name.Contains(node)).ToArray();
            asset=srcs.OrderByDescending(m=>m.vertexCount).First();
        }
        var mesh=UnityEngine.Object.Instantiate(asset);var pos=mesh.vertices;
        for(int i=0;i<pos.Length;i++)
        {
            var p=tr.TransformPoint(pos[i]);
            if(north)
            {
                float t=(p.x-31f)/31f;
                float cut=Mathf.Exp(-t*t*1.2f);
                // Incise an inclined opening into the far-facing lip; distant ridges remain visible through it.
                p.y-=cut*Mathf.Clamp01((p.y+25)/30f)*(19f+Mathf.Clamp(p.y,0,75)*.64f);
            }
            else
            {
                float smooth=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,4,p.y));
                p.x-=Mathf.Lerp(1.4f,6.5f,smooth)*Mathf.Clamp01((p.y+14)/12);
                if(p.y>-5) p.y=-5+(p.y+5)*.26f;
            }
            pos[i]=tr.InverseTransformPoint(p);
        }
        mesh.vertices=pos;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();mf.sharedMesh=SaveMesh(name,mesh);
    }
    enum Wall { West, North, South, East, Alcove }

    static void RockFaces()
    {
        var path=Out+"WorkshopCliff.mat";
        var stone=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Load<Shader>(Out+"WorkshopCliff.shader");
        if (!stone) { stone=new Material(shader);AssetDatabase.CreateAsset(stone,path); }
        stone.shader=shader;
        var scans=Root+"Surfaces/Geology/";
        stone.SetTexture("_BaseMap",Load<Texture2D>(scans+"CH_Rock_BaseColor.png"));
        stone.SetTexture("_BumpMap",Load<Texture2D>(scans+"CH_Rock_Normal.png"));
        stone.SetTexture("_RockMask",Load<Texture2D>(scans+"CH_Rock_Mask.png"));
        stone.SetTexture("_Lichen",Load<Texture2D>(scans+"CH_Scree_BaseColor.png"));
        stone.SetFloat("_Scale",3.7f);stone.enableInstancing=true; EditorUtility.SetDirty(stone);

        // The native altar has one separate 80 m rock skirt. Keep both walkable mesh sections and their UVs.
        var deck=Find("天堑台 · 战斗地面").GetComponent<MeshFilter>();
        var deckSource=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"ApprovedBasket/AltarDeckRockUV.asset");
        if (!deckSource) deckSource=GetOriginal(deck.name,Geometry);
        PlatformShell(deck,deckSource,stone);
        var sites=new[]{
            ("石门断桥 · 西崖",Wall.West,"WestWallRelief"),
            ("双岸栈道 · 南崖",Wall.South,"SouthWallRelief"),
            ("工坊栈桥 · 西崖肩",Wall.West,"BridgeShoulderRelief"),
            ("谷口 · 北岸断层",Wall.North,"NorthWallRelief"),
            ("东谷 · 折壁",Wall.East,"EastWallRelief")
        };
        foreach (var item in sites)
        {
            var filter=Find(item.Item1).GetComponent<MeshFilter>();
            Mesh original;
            if(item.Item3=="NorthWallRelief") original=Load<Mesh>(Out+"NorthPass.asset");
            else if(item.Item3=="BridgeShoulderRelief") original=Load<Mesh>(Out+"WestBridgeShoulder.asset");
            else original=GetOriginal(item.Item3=="WestWallRelief"?"SuspendedPathCliffs":item.Item1,Cliffs);
            if(item.Item2==Wall.North) NorthShell(filter,original,stone);
            else Sculpt(filter,original,item.Item2,3,0,item.Item3,stone);
        }

        var alcove=Find("工坊背岩 · 檐后岩窝");
        foreach(var filter in alcove.GetComponentsInChildren<MeshFilter>(true))
        {
            // Always start at the imported mesh, including when this scene already has local renderer overrides.
            var assetFilter=PrefabUtility.GetCorrespondingObjectFromOriginalSource(filter);
            var source=assetFilter?assetFilter.sharedMesh:filter.sharedMesh;
            int level=int.Parse(filter.name.Substring(filter.name.Length-1));
            Sculpt(filter,source,Wall.Alcove,1,0,"RoofRecess_LOD"+level,stone);
        }
        Debug.Log("[ApprovedWorkshop] Reworked five native cliff skins, three roof-alcove LODs and altar rock side surfaces.");
    }

    static Vector3 WallOutward(Wall wall)
    {
        if(wall==Wall.West || wall==Wall.Alcove)return Vector3.right;
        if(wall==Wall.North)return Vector3.back;
        if(wall==Wall.South)return Vector3.forward;
        return Vector3.left;
    }

    // Deform a coarse existing surface into shelves separated by dipping bedding and jointed buttresses.
    // Low-frequency changes control shelf depth and where water has removed material between exposures.
    static float SouthClearance(Vector3 point)
    {
        // The native south wall already threads the valley and both moving hoist lines.
        // Preserve its exact approved skin near those swept volumes, including the edge of a tessellated face.
        float Footprint(float xmin,float xmax,float zmin,float zmax)
        {
            float dx=Mathf.Max(0,Mathf.Max(xmin-point.x,point.x-xmax));
            float dz=Mathf.Max(0,Mathf.Max(zmin-point.z,point.z-zmax));
            return Mathf.Sqrt(dx*dx+dz*dz);
        }
        float distance=Footprint(58.7235f,78.7185f,16.4025f,40.3975f);
        // The tip moves through +/- asin(2/3) along the 60 m beam, with 3.35 m wide baskets.
        distance=Mathf.Min(distance,Footprint(-26.075f,-7.44f,18.725f,22.075f));
        distance=Mathf.Min(distance,Footprint(78.64f,97.275f,18.725f,22.075f));
        distance=Mathf.Min(distance,Footprint(14.6f,28.6f,42.5f,60.5f));
        distance=Mathf.Min(distance,Footprint(-15.121f,-3.121f,27f,43f));
        return Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,15,distance));
    }

    static Vector3 ExposedRock(Vector3 local,Transform tr,Wall wall,out Color color)
    {
        var p=tr.TransformPoint(local);
        var outward=WallOutward(wall);
        float run=Mathf.Abs(outward.x)>.65f?p.z:p.x;
        float seam=p.y+p.x*.10f-p.z*.14f+.68f*Mathf.Sin(p.z*.075f+p.x*.055f);
        float coarse=Mathf.PerlinNoise(run*.065f+35,p.y*.045f+61);
        float detail=Mathf.PerlinNoise(run*.29f+87,p.y*.21f+19)-.5f;
        float dipping=seam+(coarse-.5f)*3.2f;
        float bed=Mathf.Repeat(dipping/4.7f,1);
        float cornice=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.32f,bed))*
                      (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.49f,.93f,bed)));
        float sensitivity=coarse*.66f+.20f;
        float jointPhase=run*.102f+p.y*.014f+Mathf.PerlinNoise(run*.067f,p.y*.064f)*.42f;
        float joint=Mathf.Exp(-Mathf.Pow(Mathf.Sin(jointPhase*3.14159f)*4.1f,2));
        float block=(coarse-.45f)*4.1f+cornice*sensitivity*1.9f-joint*(.66f+coarse*.72f)+detail*.35f;
        color=new Color(Mathf.Clamp01(.55f+(coarse-.5f)*.43f+cornice*.13f-joint*.20f),
                        Mathf.Clamp01((coarse-.37f)*2.2f+detail*.24f),0,1);

        if(wall==Wall.Alcove)
        {
            float raised=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,9,p.y));
            p+=outward*block*.29f;
            p.y+=raised*(1.15f*(coarse-.44f)+cornice*.24f-.88f);
        }
        else
        {
            float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-85,-58,p.y));
            if(wall==Wall.South)weight*=SouthClearance(p);
            p+=outward*block*(wall==Wall.North?1.45f:1.05f)*weight;
            p.y+=detail*.26f*weight;
        }
        return tr.InverseTransformPoint(p);
    }

    // Closed ridge skins avoid the sub-pixel gaps made by the legacy cliff's folded sheets.
    sealed class RockShell
    {
        public readonly List<Vector3> positions=new List<Vector3>();
        public readonly List<Color> colors=new List<Color>();
        public readonly List<Vector2> uv=new List<Vector2>();
        public readonly List<int> side=new List<int>();
        public int Add(Vector3 p,Color color,Vector2 tex)
        {
            int id=positions.Count;positions.Add(p);colors.Add(color);uv.Add(tex);return id;
        }
        public void Quad(int a,int b,int c,int d)
        { side.Add(a);side.Add(b);side.Add(c);side.Add(a);side.Add(c);side.Add(d); }
        public void Save(MeshFilter filter,Material material,string id,Mesh keep=null)
        {
            var mesh=new Mesh();mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetUVs(0,uv);
            mesh.subMeshCount=keep?4:1;
            if(keep)
            {
                mesh.SetTriangles(keep.GetTriangles(0),0,false);mesh.SetTriangles(keep.GetTriangles(1),1,false);
                mesh.SetTriangles(side,2,false);mesh.SetTriangles(Array.Empty<int>(),3,false);
            }
            else mesh.SetTriangles(side,0,false);
            mesh.RecalculateNormals();
            if(keep)
            {
                var normals=mesh.normals;Array.Copy(keep.normals,normals,keep.vertexCount);mesh.normals=normals;
                var uv2=keep.uv2;
                if(uv2.Length==keep.vertexCount)
                { var light=new Vector2[positions.Count];Array.Copy(uv2,light,uv2.Length);mesh.uv2=light; }
            }
            mesh.RecalculateBounds();mesh.RecalculateTangents();filter.sharedMesh=SaveMesh(id,mesh);
            var r=filter.GetComponent<MeshRenderer>();var mats=r.sharedMaterials;
            for(int i=keep?2:0;i<mats.Length;i++)mats[i]=material;
            r.sharedMaterials=mats;
        }
    }

    static void PlatformShell(MeshFilter filter,Mesh original,Material stone)
    {
        var shell=new RockShell();
        // Retain the original foundations for the altar and the assembly registered on its top surface.
        var top=original.vertices;var uv=original.uv;
        for(int i=0;i<top.Length;i++)shell.Add(top[i],Color.white,uv[i]);
        int start=shell.positions.Count;
        const int perimeter=104, levels=81;
        var tr=filter.transform;
        for(int row=0;row<=levels;row++)
        {
            float depth=row/(float)levels;float y=Mathf.Lerp(-.39f,-81f,depth);
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,4f,-y));
            for(int i=0;i<perimeter;i++)
            {
                float side=i*4f/perimeter; int edge=Mathf.FloorToInt(side); float t=side-edge;
                Vector2 border;
                if(edge==0)border=new Vector2(Mathf.Lerp(-1,1,t),-1);
                else if(edge==1)border=new Vector2(1,Mathf.Lerp(-1,1,t));
                else if(edge==2)border=new Vector2(Mathf.Lerp(1,-1,t),1);
                else border=new Vector2(-1,Mathf.Lerp(1,-1,t));
                float softCorner=Mathf.Pow(Mathf.Pow(Mathf.Abs(border.x),7)+Mathf.Pow(Mathf.Abs(border.y),7),1f/7f);
                var radial=border/Mathf.Lerp(1,softCorner,blend*.87f);
                var local=new Vector3(14+radial.x*(13.89f-depth*1.10f),y,11+radial.y*(10.92f-depth*.80f));
                var p=tr.TransformPoint(local);
                Vector3 n=new Vector3(Mathf.Sign(border.x)*Mathf.Pow(Mathf.Abs(border.x),4),0,
                                      Mathf.Sign(border.y)*Mathf.Pow(Mathf.Abs(border.y),4)).normalized;
                Vector3 norm=tr.TransformDirection(n);
                float warp=(Mathf.PerlinNoise(p.z*.058f+12,p.x*.061f+16)-.5f)*3.9f;
                float h=p.y+.13f*p.z-.075f*p.x+warp;
                float band=Mathf.Floor(h/6.6f);float bed=Mathf.Repeat(h/6.6f,1);
                // The horizontal lips outcrop unevenly along each bed. Differently eroded beds break
                // the 80 m foundation into short buttresses instead of uninterrupted vertical channels.
                float ledge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.07f,.26f,bed))*
                            (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.56f,.98f,bed)));
                float patch=Mathf.PerlinNoise(p.z*.105f+p.x*.081f+43,band*.62f+102);
                float broad=Mathf.PerlinNoise(p.x*.08f+p.z*.087f+31,p.y*.073f+40);
                float breakline=Mathf.PerlinNoise(p.x*.23f+p.z*.30f+73,p.y*.141f+35);
                float relief=(broad-.36f)*2.5f+(ledge-.36f)*(.62f+2.0f*patch)+(breakline-.48f)*.56f;
                p+=norm*relief*blend;
                float tint=Mathf.Clamp01(.49f+broad*.24f+(ledge-.5f)*.16f);
                shell.Add(tr.InverseTransformPoint(p),new Color(tint,Mathf.Clamp01((patch-.36f)*1.9f),0,1),
                          new Vector2(i*100f/perimeter,h)/3.7f);
                if(row>0)
                {
                    int a=start+(row-1)*perimeter+i,b=start+(row-1)*perimeter+(i+1)%perimeter;
                    shell.Quad(a,b,b+perimeter,a+perimeter);
                }
            }
        }
        int baseCenter=shell.Add(new Vector3(14,-81.1f,11),new Color(.45f,.22f,0,1),Vector2.zero);
        int baseRing=start+levels*perimeter;
        for(int i=0;i<perimeter;i++)
        {
            shell.side.Add(baseCenter);shell.side.Add(baseRing+i);shell.side.Add(baseRing+(i+1)%perimeter);
        }
        shell.Save(filter,stone,"AltarRockSides",original);
    }

    static void NorthShell(MeshFilter filter,Mesh original,Material stone)
    {
        var shell=new RockShell();var tr=filter.transform;
        var native=original.vertices.Select(p=>tr.TransformPoint(p)).ToArray();
        const int width=115,height=72, ring=height+5;
        var crest=new float[width+1];var fore=new float[width+1];var rear=new float[width+1];
        float min=native.Min(p=>p.x),max=native.Max(p=>p.x);
        for(int i=0;i<=width;i++)
        {
            float x=Mathf.Lerp(min,max,(float)i/width);
            var sample=native.Where(p=>Mathf.Abs(p.x-x)<3.6f).ToArray();
            if(sample.Length==0)sample=native.OrderBy(p=>Mathf.Abs(p.x-x)).Take(24).ToArray();
            var z=sample.Select(p=>p.z).OrderBy(v=>v).ToArray();
            crest[i]=sample.Max(p=>p.y);fore[i]=z[Mathf.FloorToInt((z.Length-1)*.18f)];
            rear[i]=z[Mathf.FloorToInt((z.Length-1)*.94f)];
        }
        float Smooth(float[] values,int i)
        { return (values[Mathf.Max(i-2,0)]+values[Mathf.Max(i-1,0)]*2+values[i]*3+
                  values[Mathf.Min(i+1,width)]*2+values[Mathf.Min(i+2,width)])/9; }
        for(int col=0;col<=width;col++)
        {
            float x=Mathf.Lerp(min,max,(float)col/width);
            float top=Smooth(crest,col),toe=Smooth(fore,col),back=Mathf.Max(toe+17,Smooth(rear,col));
            for(int j=0;j<ring;j++)
            {
                float y,z;
                if(j<=height)
                {
                    float t=(float)j/height;
                    y=Mathf.Lerp(-87,top-1.5f,t);
                    float dip=.106f*x-.09f*y;
                    float block=Mathf.PerlinNoise(x*.043f+61,y*.035f+23);
                    float ridge=Mathf.Sin(x*.26f+dip*.11f+Mathf.Sin(y*.048f+x*.054f)*.84f);
                    float buttress=(1-.5f*Mathf.Abs(ridge))*2.7f*(.62f+block);
                    float crown=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.74f,1,t));
                    z=toe+1.6f*t+4.1f*crown-buttress-(block-.5f)*4.2f;
                }
                else if(j==height+1){y=top;z=toe+8.2f;}
                else if(j==height+2){y=top-2.5f;z=back-1.2f;}
                else if(j==height+3){y=-87;z=back+3.3f;}
                else{y=-87.8f;z=(toe+back)*.5f;}
                var p=new Vector3(x,y,z);
                var local=ExposedRock(tr.InverseTransformPoint(p),tr,Wall.North,out var color);
                if(j<=height)
                {
                    // The exterior terrain protrudes through this inner bank behind the forge.
                    // Continue its exposed bedding over the contact with a tapered, blind buttress.
                    var face=tr.TransformPoint(local);
                    float across=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-39,-26,face.x))*
                                 (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-10,3,face.x)));
                    float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,12,face.y))*
                               (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(31,49,face.y)));
                    float bedding=5.95f+.40f*Mathf.Sin(face.y*.59f+face.x*.083f);
                    face.z-=across*rise*bedding;
                    local=tr.InverseTransformPoint(face);
                }
                shell.Add(local,color,new Vector2(x,y-.14f*z+.10f*x)/3.7f);
                if(col>0)
                {
                    int a=(col-1)*ring+j,b=(col-1)*ring+(j+1)%ring;
                    shell.Quad(a,b,b+ring,a+ring);
                }
            }
        }
        // End-cap both ridgelines. The top and back close the summit even when seen from the altar above.
        for(int end=0;end<2;end++)
        {
            int col=end*width;
            var midpoint=Vector3.zero;
            for(int j=0;j<ring;j++)midpoint+=shell.positions[col*ring+j];
            midpoint/=ring;int idx=shell.Add(midpoint,new Color(.52f,.31f,0,1),Vector2.zero);
            for(int j=0;j<ring;j++)
            {
                int a=col*ring+j,b=col*ring+(j+1)%ring;
                shell.side.Add(idx);shell.side.Add(end==0?b:a);shell.side.Add(end==0?a:b);
            }
        }
        shell.Save(filter,stone,"NorthWallRelief");
    }

    static void Sculpt(MeshFilter filter,Mesh original,Wall wall,int division,int firstSlot,string id,Material stone)
    {
        var tr=filter.transform;
        var source=original.vertices;
        var vertices=source.ToList();
        var normals=original.normals.ToList();
        var uvs=original.uv.ToList();
        var colors=Enumerable.Repeat(Color.white,source.Length).ToList();
        var welded=new Dictionary<Vector3Int,int>();
        var triangles=new List<int>[original.subMeshCount];
        int Point(Vector3 local)
        {
            Vector3Int key=Vector3Int.RoundToInt(local*1000);
            if(welded.TryGetValue(key,out int index))return index;
            Vector3 shaped=ExposedRock(local,tr,wall,out var color);
            var p=tr.TransformPoint(shaped);var direction=WallOutward(wall);
            index=vertices.Count;welded.Add(key,index); vertices.Add(shaped);normals.Add(Vector3.zero);
            uvs.Add(new Vector2(Mathf.Abs(direction.x)>.65f?p.z:-p.x,p.y+p.x*.10f-p.z*.14f)/3.7f);
            colors.Add(color);return index;
        }
        for(int slot=0;slot<original.subMeshCount;slot++)
        {
            int[] face=original.GetTriangles(slot);
            if(slot<firstSlot){triangles[slot]=face.ToList();continue;}
            var list=new List<int>(face.Length*division*division);
            triangles[slot]=list;
            void Tri(int a,int b,int c)
            {
                if(a==b||a==c||b==c)return;
                var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                if(n.sqrMagnitude<1e-13f)return;
                list.Add(a);list.Add(b);list.Add(c);
                // Angle weighting prevents long, thin boundary triangles from dominating connected ledges.
                var unit=n.normalized;
                normals[a]+=unit*Vector3.Angle(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                normals[b]+=unit*Vector3.Angle(vertices[a]-vertices[b],vertices[c]-vertices[b]);
                normals[c]+=unit*Vector3.Angle(vertices[a]-vertices[c],vertices[b]-vertices[c]);
            }
            int[,] grid=new int[division+1,division+1];
            for(int corner=0;corner<face.Length;corner+=3)
            {
                Vector3 a=source[face[corner]],b=source[face[corner+1]],c=source[face[corner+2]];
                for(int x=0;x<=division;x++)
                    for(int y=0;y<=division-x;y++)
                        grid[x,y]=Point(a+(b-a)*((float)x/division)+(c-a)*((float)y/division));
                for(int x=0;x<division;x++)
                    for(int y=0;y<division-x;y++)
                    {
                        Tri(grid[x,y],grid[x+1,y],grid[x,y+1]);
                        if(y+x<division-1)Tri(grid[x+1,y],grid[x+1,y+1],grid[x,y+1]);
                    }
            }
        }
        for(int i=source.Length;i<normals.Count;i++)normals[i]=normals[i].normalized;
        var result=new Mesh{name=id,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        result.SetVertices(vertices);result.SetNormals(normals);result.SetColors(colors);result.SetUVs(0,uvs);
        if(original.uv2.Length==source.Length)
        {
            var lightUV=original.uv2.ToList();lightUV.AddRange(Enumerable.Repeat(Vector2.zero,vertices.Count-source.Length));result.SetUVs(1,lightUV);
        }
        result.subMeshCount=triangles.Length;
        for(int i=0;i<triangles.Length;i++)result.SetTriangles(triangles[i],i,false);
        result.RecalculateBounds();result.RecalculateTangents();
        filter.sharedMesh=SaveMesh(id,result);
        var r=filter.GetComponent<MeshRenderer>();var materials=r.sharedMaterials;
        for(int slot=firstSlot;slot<materials.Length;slot++)materials[slot]=stone;
        r.sharedMaterials=materials;
        if(PrefabUtility.IsPartOfPrefabInstance(filter))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
    }

    static void BuildInlays(Transform root, List<Matrix4x4> poses,Material mat)
    {
        var model=Load<GameObject>(Out+"SurveyInlay.fbx");
        var mfs=model.GetComponentsInChildren<MeshFilter>(true);
        var go=new GameObject("铜边嵌尺 · 中控至工坊");go.transform.SetParent(root,false);
        var lods=new LOD[2];
        for(int l=0;l<2;l++)
        {
            var mf=mfs.Single(x=>x.name.EndsWith("LOD"+l,StringComparison.Ordinal));
            var m=mf.sharedMesh;var bounds=m.bounds;
            // FBX unit handling varies by importer. Rebuild physical axes from Blender's Y-long/Z-up model.
            var verts=m.vertices.Select(v=>mf.transform.TransformPoint(v)).ToArray();
            var b=new Bounds(verts[0],Vector3.zero);foreach(var v in verts)b.Encapsulate(v);
            if(b.size.y>.1f || b.size.x<.05f || b.size.z<.2f)throw new Exception("Unexpected imported marker axes "+b);
            Matrix4x4 correct=Matrix4x4.Scale(new Vector3(.152f/b.size.x,.015f/b.size.y,.748f/b.size.z))*Matrix4x4.Translate(new Vector3(-b.center.x,-b.min.y,-b.center.z))*mf.transform.localToWorldMatrix;
            var merged=new Mesh();
            merged.CombineMeshes(poses.Select(p=>new CombineInstance {mesh=m,subMeshIndex=0,transform=p*correct}).ToArray());
            merged.RecalculateBounds();
            var child=new GameObject("WorkshopSurvey_LOD"+l);child.transform.SetParent(go.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=SaveMesh("WorkshopSurvey_LOD"+l,merged);
            var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            lods[l]=new LOD(l==0?.33f:.002f,new Renderer[]{renderer});
        }
        var lodGroup=go.AddComponent<LODGroup>();lodGroup.SetLODs(lods);lodGroup.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAssetAndConnect(go,Out+"WorkshopSurvey.prefab",InteractionMode.AutomatedAction);
    }
    static Material Tint(string name, string scan, Color textureTint)
    {
        var source=Load<Material>(Root+"MountainCraft/Materials/"+scan+".mat");
        var m=MaterialAsset(name);m.CopyPropertiesFromMaterial(source);m.SetColor("_BaseColor",textureTint);m.enableInstancing=true;
        EditorUtility.SetDirty(m);return m;
    }
    static void DefineHearth(Transform group)
    {
        var siteObject=Find("工坊器物 · 炉与工案");
        var oldRenderer=siteObject.GetComponent<MeshRenderer>();
        var fireMaterial=MaterialAsset("ForgeEmbers");
        fireMaterial.SetColor("_BaseColor",new Color(.70f,.15f,.018f));
        fireMaterial.SetColor("_EmissionColor",new Color(2.60f,.40f,.042f));fireMaterial.EnableKeyword("_EMISSION");
        fireMaterial.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(fireMaterial);
        var soot=Tint("SootClay","Plaster",new Color(.34f,.29f,.26f));
        var stone=Tint("HearthMasonry","Ashlar",new Color(.68f,.62f,.53f));
        var fuel=Tint("CarbonizedFuel","Rock",new Color(.09f,.07f,.06f));
        var materials=new Dictionary<string,Material>{{"Ashlar",stone},{"BlackenedClay",soot},{"Ember",fireMaterial},{"Fuel",fuel},{"ForgedBand",Load<Material>("Assets/Art/Environment/Opening/PreqinGateFittings/GateHammeredBronze.mat")}};
        var model=Load<GameObject>(Out+"WorkshopHearth.fbx");
        var handle=new GameObject("WorkshopHearth");GameObject asset;
        Vector3 localFront;
        try
        {
            var imported=(GameObject)PrefabUtility.InstantiatePrefab(model,handle.transform);
            foreach(var auto in imported.GetComponentsInChildren<LODGroup>(true))
            {
                auto.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(auto);
            }
            var renderers=imported.GetComponentsInChildren<MeshRenderer>(true);
            var lods=new LOD[2];
            for(int level=0;level<2;level++)
            {
                var r=renderers.Single(x=>x.name.EndsWith("LOD"+level,StringComparison.Ordinal));
                r.sharedMaterials=r.sharedMaterials.Select(m=>materials[m.name]).ToArray();
                r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.enabled=true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);lods[level]=new LOD(level==0?.20f:.006f,new Renderer[]{r});
            }
            var groupLod=handle.AddComponent<LODGroup>();groupLod.SetLODs(lods);groupLod.RecalculateBounds();
            var children=imported.GetComponentsInChildren<Transform>(true);
            localFront=Vector3.ProjectOnPlane(children.Single(t=>t.name=="MouthFacing").position,Vector3.up).normalized;
            var exit=children.Single(t=>t.name=="ChimneyExit").position;
            Smoke(handle.transform,exit);
            asset=PrefabUtility.SaveAsPrefabAsset(handle,Out+"WorkshopHearth.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(handle);}
        // Original forge tops in the yard's own coordinate system. Preserve existing entrance and the 18x14m court.
        int index=0;
        foreach (var source in new[]{new Vector3(7.15f,0,21.10f),new Vector3(26f,0,24f)})
        {
            var p=siteObject.TransformPoint(source);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,group);
            instance.name=(index++==0?"锻造房正炉":"修械小炉")+" · 拱膛与风道";
            float scale=index==1?1.13f:.86f;
            instance.transform.position=p+Vector3.up*.002f;instance.transform.localScale=Vector3.one*scale;
            var forward=Vector3.ProjectOnPlane(new Vector3(31,0,28)-p,Vector3.up).normalized;
            instance.transform.rotation=Quaternion.FromToRotation(localFront,forward);
            if(index==2)
            {
                var ps=instance.GetComponentInChildren<ParticleSystem>(true);
                if(ps){ps.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(ps.gameObject);}
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }
        oldRenderer.enabled=false;
    }
    static void Smoke(Transform parent,Vector3 origin)
    {
        var p=Out+"ForgeSmoke.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!mat)
        {
            var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(!shader)return;
            mat=new Material(shader);AssetDatabase.CreateAsset(mat,p);
        }
        mat.SetTexture("_BaseMap",Load<Texture2D>(Out+"ForgeSmoke.png"));mat.SetColor("_BaseColor",Color.white);
        mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_ZWrite",0);
        mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=(int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        var plume=new GameObject("烟道口 · 轻烟");plume.transform.SetParent(parent,false);plume.transform.localPosition=origin;
        var ps=plume.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.startLifetime=new ParticleSystem.MinMaxCurve(2.6f,3.8f);main.startSpeed=0;
        main.startSize=new ParticleSystem.MinMaxCurve(.48f,.75f);main.startRotation=new ParticleSystem.MinMaxCurve(0,6.2f);
        main.startColor=new Color(.73f,.71f,.65f,.29f);main.maxParticles=14;main.prewarm=true;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var shape=ps.shape;shape.enabled=false;
        var emission=ps.emission;emission.enabled=true;emission.rateOverTime=3.1f;
        var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=-.44f;velocity.y=1.12f;velocity.z=.14f;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(3.4f,new AnimationCurve(new Keyframe(0,.21f),new Keyframe(.55f,.62f),new Keyframe(1,1)));
        var color=ps.colorOverLifetime;color.enabled=true;var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.04f,0),new GradientAlphaKey(.96f,.16f),new GradientAlphaKey(.28f,.70f),new GradientAlphaKey(0,1)});color.color=grad;
        var pr=plume.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=mat;pr.shadowCastingMode=ShadowCastingMode.Off;pr.receiveShadows=false;
    }
}
