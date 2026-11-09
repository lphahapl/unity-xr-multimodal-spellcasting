using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Offline authoring only. The result contains static geometry and simple colliders.</summary>
public static class ExteriorAuthor
{
    const string Art="Assets/Art/Environment/Opening/ExpandedExterior/";
    const string Candidate="Assets/Scene/QA_ExteriorExpanded.unity";
    static Transform root,stoneGroup,timberGroup,landGroup,ruinGroup;
    static Material stone,paving,wood,bronze,rock,farRock;
    static readonly List<string> audit=new List<string>();
    [Serializable] public class Change { public long id; public string kind; public string before,after; }
    [Serializable] public class Changes { public Change[] changes; public string prefabGuid; public long prefabRoot; }
    static string Vector(Vector3 v)=>JsonUtility.ToJson(v);
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static T Find<T>(string name)where T:Component=>ExteriorSurvey.All<T>().Single(x=>x.name==name);
    static Transform Group(string name,Transform parent=null){
        var g=new GameObject(name);SceneManager.MoveGameObjectToScene(g,ExteriorSurvey.Work);
        if(parent)g.transform.SetParent(parent,false);return g.transform;
    }
    static Material M(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/"+name+".mat")??throw new Exception("Missing "+name);
    static void Store(Mesh mesh,string name){
        mesh.name=name;var p=Art+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
        else AssetDatabase.CreateAsset(mesh,p);
    }
    static GameObject Box(Transform group,string name,Vector3 at,Vector3 size,Material material,bool collision=false){
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(g,ExteriorSurvey.Work);
        g.name=name;g.transform.SetParent(group,false);g.transform.position=at;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());
        if(collision)Collider(name,at,size);
        return g;
    }
    static void Collider(string name,Vector3 at,Vector3 size){
        var g=Group(name+" · 碰撞",root);g.position=at;
        g.gameObject.AddComponent<BoxCollider>().size=size;
    }
    static void Bake(Transform group,string id){
        var filters=group.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform!=group).ToArray();
        var mats=filters.Select(f=>f.GetComponent<Renderer>().sharedMaterial).Distinct().ToArray();var parts=new List<Mesh>();
        foreach(var mat in mats){
            var temps=new List<Mesh>();var instances=new List<CombineInstance>();
            foreach(var f in filters.Where(f=>f.GetComponent<Renderer>().sharedMaterial==mat)){
                var m=Object.Instantiate(f.sharedMesh);temps.Add(m);var vertices=m.vertices;var normals=m.normals;var uv=new Vector2[vertices.Length];
                for(int i=0;i<uv.Length;i++){
                    var p=f.transform.TransformPoint(vertices[i]);var n=f.transform.TransformDirection(normals[i]);
                    uv[i]=(Mathf.Abs(n.y)>.7f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.7f?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.5f;
                }
                m.uv=uv;instances.Add(new CombineInstance{mesh=m,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix});
            }
            var part=new Mesh();part.CombineMeshes(instances.ToArray());parts.Add(part);foreach(var t in temps)Object.DestroyImmediate(t);
        }
        var mesh=new Mesh();mesh.CombineMeshes(parts.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);
        Store(mesh,id);foreach(var part in parts)Object.DestroyImmediate(part);
        foreach(var f in filters)Object.DestroyImmediate(f.gameObject);
        group.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+id+".asset");
        var renderer=group.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=mats;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    // Remove whole source wall boxes from the combined masonry. Add accurately segmented replacements.
    static void WindowWall(string masonry,string wallName,float x,float wallZ,float wallLength,float thickness,float[] windows,float width){
        var f=Find<MeshFilter>(masonry);var mesh=Object.Instantiate(f.sharedMesh);var vertices=mesh.vertices;
        var bounds=new Bounds(V(x,2,wallZ),V(thickness+.01f,4.12f,wallLength+.01f));int removed=0;
        // Source masonry was combined from 24-vertex cubes; select the entire wall cube.
        // A coordinate-only triangle filter also catches the touching end wall's two faces.
        var wallVertices=new HashSet<int>();
        for(int first=0;first+24<=vertices.Length;first+=24){
            var box=new Bounds(f.transform.TransformPoint(vertices[first]),Vector3.zero);
            for(int j=1;j<24;j++)box.Encapsulate(f.transform.TransformPoint(vertices[first+j]));
            if(Mathf.Abs(box.center.x-x)<.01f && Mathf.Abs(box.center.z-wallZ)<.01f && Mathf.Abs(box.size.z-wallLength)<.01f && Mathf.Abs(box.size.x-thickness)<.01f)
                for(int j=0;j<24;j++)wallVertices.Add(first+j);
        }
        if(wallVertices.Count!=24)throw new Exception("Ambiguous source wall cube: "+wallVertices.Count);
        for(int sub=0;sub<mesh.subMeshCount;sub++){
            var old=mesh.GetTriangles(sub);var keep=new List<int>();
            for(int i=0;i<old.Length;i+=3){
                if(wallVertices.Contains(old[i])&&wallVertices.Contains(old[i+1])&&wallVertices.Contains(old[i+2]))removed++;
                else keep.AddRange(new[]{old[i],old[i+1],old[i+2]});
            }
            mesh.SetTriangles(keep,sub);
        }
        if(removed!=12)throw new Exception("Expected one source wall box: "+masonry+" removed "+removed);
        string id=masonry=="苏醒室石砌"?"WakeMasonryWindows":"TrialMasonryWindows";
        Store(mesh,id);f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+id+".asset");
        foreach(var c in ExteriorSurvey.All<BoxCollider>().Where(c=>c.name==wallName && Mathf.Abs(c.bounds.center.x-x)<.05f))c.enabled=false;
        float bottom=1.18f,top=2.93f,start=wallZ-wallLength*.5f,end=wallZ+wallLength*.5f;
        foreach(float z in windows){
            float a=z-width*.5f,b=z+width*.5f;
            Box(stoneGroup,"窗间石柱",V(x,2,(start+a)*.5f),V(thickness,4.1f,a-start),stone,true);
            Box(stoneGroup,"石窗台",V(x,bottom*.5f-.025f,z),V(thickness+.13f,bottom+.05f,width),stone,true);
            Box(stoneGroup,"窗上石楣",V(x,(top+4.05f)*.5f,z),V(thickness,4.05f-top,width),stone,true);
            float inner=x-Mathf.Sign(x)*.055f;
            foreach(float zz in new[]{a+.06f,b-.06f})Box(timberGroup,"窗立梃",V(inner,(bottom+top)*.5f,zz),V(thickness+.06f,top-bottom,.12f),wood,true);
            foreach(float yy in new[]{bottom+.05f,top-.05f})Box(timberGroup,"窗横梃",V(inner,yy,z),V(thickness+.06f,.10f,width),wood);
            // Two narrow mullions leave a large landscape view and stop a body passing through.
            foreach(float zz in new[]{z-width*.20f,z+width*.20f})Box(timberGroup,"木格栅",V(inner,(bottom+top)*.5f,zz),V(.09f,top-bottom,.065f),wood,true);
            Box(timberGroup,"窗腰横格",V(inner,2.36f,z),V(.09f,.055f,width),wood);
            start=b;
        }
        Box(stoneGroup,"窗侧石柱",V(x,2,(start+end)*.5f),V(thickness,4.1f,end-start),stone,true);
        audit.Add($"Window wall {masonry}, x={x}, openings={windows.Length}, clear width={width}, bottom={bottom}, top={top}, old wall triangles removed={removed}");
    }
    static void Pillar(Transform group,Vector3 at,float height,float width){
        Box(group,"柱础",at+V(0,.22f,0),V(width*1.8f,.44f,width*1.8f),stone);
        Box(group,"遗构石柱",at+V(0,height*.5f,0),V(width,height,width),stone);
        Box(group,"柱头",at+V(0,height,0),V(width*1.6f,.32f,width*1.6f),stone);
    }
    static void Pavilion(Vector3 at,float scale){
        var baseSize=V(7*scale,at.y+2.1f,5*scale);Box(ruinGroup,"远亭台基",V(at.x,(at.y-1.3f)*.5f,at.z),baseSize,stone);
        foreach(float x in new[]{-2.7f,2.7f})foreach(float z in new[]{-1.7f,1.7f})Pillar(ruinGroup,at+V(x*scale,.4f,z*scale),3.2f*scale,.34f*scale);
        foreach(float s in new[]{-1f,1f}){
            var roof=Box(ruinGroup,"斜檐",at+V(s*1.7f*scale,4.2f*scale,0),V(3.8f*scale,.18f*scale,5.5f*scale),wood);
            roof.transform.Rotate(0,0,-s*16);
        }
        Box(ruinGroup,"亭脊",at+V(0,4.72f*scale,0),V(.25f*scale,.2f*scale,5.8f*scale),wood);
    }
    static void Mountains(){
        var rng=new System.Random(61108);var vertices=new List<Vector3>();var triangles=new List<int>();
        const int n=48;
        for(int ring=0;ring<3;ring++)for(int i=0;i<n;i++){
            float a=i*Mathf.PI*2/n;float r=ring==0?43: ring==1?64:91;
            float jitter=(float)rng.NextDouble()*8;float h=ring==0?-6:ring==1?10+(float)rng.NextDouble()*18:-7;
            vertices.Add(V(Mathf.Cos(a)*(r+jitter),h,31+Mathf.Sin(a)*(r+jitter)*1.12f));
        }
        for(int j=0;j<2;j++)for(int i=0;i<n;i++){
            int a=j*n+i,b=j*n+(i+1)%n,c=(j+1)*n+i,d=(j+1)*n+(i+1)%n;
            triangles.AddRange(new[]{a,b,c,b,d,c});
        }
        var m=new Mesh();m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();Store(m,"DistantRidge");
        var t=Group("远山 · 环形轮廓",root);t.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"DistantRidge.asset");
        var mr=t.gameObject.AddComponent<MeshRenderer>();mr.sharedMaterial=farRock;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
    }
    static void Rail(Vector3 center,float length,bool alongZ){
        for(float a=-length*.5f;a<=length*.5f+.01f;a+=2.2f){
            var p=center+(alongZ?V(0,0,a):V(a,0,0));Box(stoneGroup,"望柱",p+V(0,.5f,0),V(.24f,1.05f,.24f),stone);
            Box(stoneGroup,"柱帽",p+V(0,1.01f,0),V(.31f,.12f,.31f),stone);
        }
        foreach(float y in new[]{.35f,.82f})Box(stoneGroup,"石栏横档",center+V(0,y,0),alongZ?V(.14f,.12f,length):V(length,.12f,.14f),stone);
        Collider("外缘护栏",center+V(0,.51f,0),alongZ?V(.26f,1.1f,length):V(length,1.1f,.26f));
    }
    public static void Build(){
        audit.Clear();File.Copy(ExteriorSurvey.ScenePath,ExteriorSurvey.Out+"source-scene.unity",true);
        ExteriorSurvey.Work=EditorSceneManager.OpenPreviewScene(ExteriorSurvey.ScenePath);
        try{
            var originalTransforms=ExteriorSurvey.All<Transform>().ToDictionary(t=>t,t=>(t.localPosition,t.localScale));
            var originalColliders=ExteriorSurvey.All<BoxCollider>().ToDictionary(t=>t,t=>t.enabled);
            var originalMeshes=ExteriorSurvey.All<MeshFilter>().ToDictionary(t=>t,t=>t.sharedMesh);
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();
            root=Group("首章外景拓展 · 窗庭远山");stoneGroup=Group("石台与窗洞",root);timberGroup=Group("窗格木构",root);landGroup=Group("近景地形",root);ruinGroup=Group("远处天工遗构",root);
            stone=M("WorkshopStoneWall");paving=M("WorkshopPaving");wood=M("WorkshopGateTimber");bronze=M("旧铜");
            rock=ExteriorSurvey.All<MeshRenderer>().First(r=>r.name=="CliffPanel").sharedMaterial;
            farRock=new Material(rock){name="DistantRidgeStone"};farRock.SetColor("_BaseColor",new Color(.37f,.44f,.45f));
            var stored=AssetDatabase.LoadAssetAtPath<Material>(Art+"DistantRidgeStone.mat");if(stored){Object.DestroyImmediate(farRock);farRock=stored;}else AssetDatabase.CreateAsset(farRock,Art+"DistantRidgeStone.mat");
            WindowWall("苏醒室石砌","侧壁",-6.38f,3.5f,13.3f,.3f,new[]{.2f,3.6f,7.0f},2.2f);
            WindowWall("苏醒室石砌","侧壁",6.38f,3.5f,13.3f,.3f,new[]{1.2f,6.5f},2.5f);
            WindowWall("实战室石砌","房间侧墙",7.2f,26,16.8f,.4f,new[]{20.8f,26,31.2f},2.6f);
            WindowWall("实战室石砌","房间侧墙",-7.2f,26,16.8f,.4f,new[]{21.7f,29.7f},2.8f);
            // Court expansion keeps every puzzle, gate, trigger and navigation anchor at its existing position.
            Box(stoneGroup,"东庭扩展铺地",V(8.94f,-.35f,43.95f),V(6.7f,.64f,10),M("CourtyardStone"),true);
            Find<Transform>("静态合并_CourtRailEast").position+=V(6.7f,0,0);
            foreach(var c in ExteriorSurvey.All<BoxCollider>().Where(c=>c.name=="连续护栏碰撞"&&Mathf.Abs(c.bounds.center.x-5.45f)<.05f))c.transform.position+=V(6.7f,0,0);
            Box(stoneGroup,"扩庭校验隔墙",V(8.94f,1.43f,49),V(6.7f,2.92f,.48f),M("CourtyardStone"),true);
            Rail(V(8.95f,0,39),6.7f,false);
            // Recede the enclosing cliff modules, but retain the small physical water-impact landmark.
            var cliffRoot=Find<Transform>("小峡壁");
            foreach(var t in cliffRoot.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="CliffPanel").Select(f=>f.transform.parent).Distinct().ToArray()){
                var p=t.position;float sign=p.x<0?-1:1;
                if(p.z<65){p.x=sign*(p.z<49?24:20);p.y-=2;t.localScale*=1.65f;}
                else{p.z+=12;p.x*=2.5f;p.y-=.6f;t.localScale*=1.6f;}
                t.position=p;
            }
            var oldBed=Find<Transform>("破碎峡谷底床");oldBed.position=V(0,-5.4f,33);oldBed.localScale=V(98,8,130);
            var sourceRock=cliffRoot.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="CliffPanel");
            var rockMesh=Object.Instantiate(sourceRock.sharedMesh);var rockVerts=rockMesh.vertices;var rockBounds=rockMesh.bounds;
            for(int i=0;i<rockVerts.Length;i++)rockVerts[i]=Vector3.Scale(rockVerts[i]-rockBounds.center,new Vector3(1.5f/rockBounds.size.x,3.7f/rockBounds.size.y,2.1f/rockBounds.size.z))+V(-1.95f,1.15f,56.2f);
            rockMesh.vertices=rockVerts;rockMesh.RecalculateNormals();rockMesh.RecalculateBounds();
            var support=Group("逆流冲击残壁",landGroup);support.gameObject.AddComponent<MeshFilter>().sharedMesh=rockMesh;
            support.gameObject.AddComponent<MeshRenderer>().sharedMaterial=rock;
            // Lower terraces visible from windows ground the building in the surrounding valley.
            foreach(float s in new[]{-1f,1f}){
                Box(landGroup,"室外石基",V(s*9.2f,-.7f,14),V(5.2f,1.25f,44),paving);
                Box(landGroup,"台基竖面",V(s*11.5f,-2.15f,14),V(1.2f,4,44),rock);
                for(int i=0;i<6;i++)Box(landGroup,"崖阶",V(s*(15+i*.65f),-1.1f-i*.4f,-5+i*10),V(6+i*.6f,2+i*.5f,13),rock);
            }
            // View-only architecture is separated from the walkable route and has no scripts or colliders.
            Pavilion(V(-22,-.2f,8),1.4f);Pavilion(V(26,.6f,25),1.5f);Pavilion(V(28,1,66),1.7f);
            for(int i=0;i<5;i++){
                var at=V(-31+i*4.4f,-2,65);Pillar(ruinGroup,at,8+(i%2)*.7f,.8f);
                if(i!=2)Box(ruinGroup,"断渠横梁",at+V(2.2f,8.1f,0),V(4.6f,.6f,1.5f),stone);
            }
            // Few large pieces provide mid-distance breakup without dense loose props.
            foreach(var p in new[]{V(16,-.7f,42),V(-14,-.8f,33),V(14,-.8f,59),V(-17,-.8f,58)}){
                Pillar(ruinGroup,p,2.4f,.65f);var beam=Box(ruinGroup,"落地断梁",p+V(1.1f,.25f,1),V(3.6f,.45f,.5f),wood);beam.transform.Rotate(0,18,7);
            }
            Mountains();
            // Solid remote limits supplement the visible rails. They never seal the route or the gate aperture.
            Collider("远端空气墙 · 东",V(58,12,32),V(1,40,132));Collider("远端空气墙 · 西",V(-58,12,32),V(1,40,132));
            Collider("远端空气墙 · 北",V(0,12,98),V(117,40,1));Collider("远端空气墙 · 南",V(0,12,-34),V(117,40,1));
            // Close the old unguarded endpoint while leaving the armillary and chapter trigger accessible.
            Rail(V(2.65f,0,65.55f),6.1f,false);
            Bake(stoneGroup,"ExpandedStonework");Bake(timberGroup,"WindowTimber");Bake(landGroup,"ExpandedLandBase");Bake(ruinGroup,"DistantArchitecture");
            AssetDatabase.SaveAssets();
            audit.Add("Added static triangles="+root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3));
            audit.Add("Added renderers="+root.GetComponentsInChildren<MeshRenderer>().Length+" material slots="+root.GetComponentsInChildren<MeshRenderer>().Sum(r=>r.sharedMaterials.Length));
            audit.Add("Added colliders="+root.GetComponentsInChildren<Collider>().Length+" added MonoBehaviours="+root.GetComponentsInChildren<MonoBehaviour>().Length);
            audit.Add("Court footprint 11.2 x 10 -> 17.9 x 10 m; puzzle and 30m route anchors retained.");
            CheckGeometry();
            ExteriorSurvey.Photos("after");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Art+"ExpandedExterior.prefab");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.transform,out string prefabGuid,out long prefabRoot);
            var changes=new List<Change>();
            void Add(Object obj,string kind,string before,string after){
                var id=GlobalObjectId.GetGlobalObjectIdSlow(obj);if(id.targetObjectId==0)throw new Exception("No scene file ID for "+obj.name);
                changes.Add(new Change{id=(long)id.targetObjectId,kind=kind,before=before,after=after});
            }
            foreach(var p in originalTransforms){
                if(p.Key.localPosition!=p.Value.Item1)Add(p.Key,"position",Vector(p.Value.Item1),Vector(p.Key.localPosition));
                if(p.Key.localScale!=p.Value.Item2)Add(p.Key,"scale",Vector(p.Value.Item2),Vector(p.Key.localScale));
            }
            foreach(var p in originalColliders)if(p.Key.enabled!=p.Value)Add(p.Key,"enabled",p.Value?"1":"0",p.Key.enabled?"1":"0");
            foreach(var p in originalMeshes)if(p.Key.sharedMesh!=p.Value){
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(p.Value,out string oldGuid,out long oldId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(p.Key.sharedMesh,out string newGuid,out long newId);
                Add(p.Key,"mesh",oldGuid+":"+oldId,newGuid+":"+newId);
            }
            File.WriteAllText(ExteriorSurvey.Out+"scene-changes.json",JsonUtility.ToJson(new Changes{changes=changes.ToArray(),prefabGuid=prefabGuid,prefabRoot=prefabRoot},true));
            audit.Add("Saved independent prefab and "+changes.Count+" existing scene field changes; no gameplay fields.");
            File.WriteAllLines(ExteriorSurvey.Out+"author-result.txt",audit);
        }finally{EditorSceneManager.ClosePreviewScene(ExteriorSurvey.Work);}
    }
    static void CheckGeometry(){
        Physics.SyncTransforms();
        var cols=ExteriorSurvey.All<Collider>().Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger).ToArray();
        bool Hit(Vector3 origin,Vector3 direction,float distance)=>cols.Any(c=>c.Raycast(new Ray(origin,direction),out var hit,distance));
        foreach(float z in new[]{40.2f,43f,47.5f})foreach(float x in new[]{6f,9f,11.5f})
            if(!Hit(V(x,.5f,z),Vector3.down,.7f))throw new Exception("Terrace floor gap "+x+","+z);
        foreach(float x in new[]{6f,9f,11.5f})if(!Hit(V(x,1.2f,48),Vector3.forward,2))throw new Exception("Gate bypass at "+x);
        foreach(var pt in new[]{V(-4.8f,1.8f,.2f),V(-4.8f,1.8f,3.6f),V(5.4f,1.8f,26)}){
            var dir=pt.x<0?Vector3.left:Vector3.right;
            if(Hit(pt,dir,2.6f))throw new Exception("Window aperture still has a full wall at "+pt);
        }
        foreach(float z in new[]{40.5f,43.3f,47.5f})if(!Hit(V(11.2f,.6f,z),Vector3.right,2))throw new Exception("East rail gap");
        audit.Add("PASS static collider rays: 9 terrace floor points, 3 closed gate wall points, 3 open window apertures, 3 east edge points.");
    }
}
