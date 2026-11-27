using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// First-look geometric regression only, not a replacement for GameItemBase/XR tests.
// SAT is the same 13-axis triangle/OBB oracle already exercised in R02 review.
public static class QingGuiR03QuickFit
{
    static Vector3[] triangles;
    const string Cache="Assets/Review/QingGuiR03/Generated/OriginalConsoleTriangles.bytes";
    public static void PrepareTrianglesInEditor()
    {
        if(Application.isPlaying)throw new Exception("Actual source mesh cache must be prepared in edit mode, before non-readable runtime import restrictions");
        var console=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Review/QingGuiR02/ReferenceConsole/TenonConsole.prefab"));console.transform.position=Vector3.zero;var points=new List<Vector3>();var source=new List<string>();
        foreach(var f in console.GetComponentsInChildren<MeshFilter>(true)){if(!f.sharedMesh||!f.name.Contains("LOD0"))continue;var v=f.sharedMesh.vertices;var index=f.sharedMesh.triangles;if(index.Length==0)throw new Exception("Editor could not read actual source console triangles");points.AddRange(index.Select(k=>f.transform.TransformPoint(v[k])));source.Add(f.name+" | "+AssetDatabase.GetAssetPath(f.sharedMesh)+" | triangles="+index.Length/3+" | readableAtRuntime="+f.sharedMesh.isReadable);}
        Object.DestroyImmediate(console);if(points.Count!=6056*3)throw new Exception("Actual original console expected6056triangles; got"+points.Count/3);var absolute=Path.Combine(Application.dataPath,"Review/QingGuiR03/Generated/OriginalConsoleTriangles.bytes");using(var file=File.Create(absolute))using(var writer=new BinaryWriter(file))foreach(var p in points){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}File.WriteAllLines(absolute+".source.txt",source);AssetDatabase.ImportAsset(Cache,ImportAssetOptions.ForceSynchronousImport);
    }
    static Vector3[] ReadPreparedTriangles(){var bytes=AssetDatabase.LoadAssetAtPath<TextAsset>(Cache);if(!bytes||bytes.bytes.Length!=6056*3*12)throw new Exception("Exact original console6056triangle cache missing or invalid");var result=new Vector3[6056*3];using(var file=new MemoryStream(bytes.bytes))using(var reader=new BinaryReader(file))for(int i=0;i<result.Length;i++)result[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());return result;}
    public static GameObject Check(GameObject item,string output)
    {
        var points=item.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>item.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
        if(points.Any(v=>Mathf.Abs(v.x)>.06001f||Mathf.Abs(v.z)>.06001f||v.y<-.00001f||v.y>.35001f))throw new Exception("R03 actual vertices exceed the separate 120x350 conservative gauge");
        var baseline=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Review/QingGuiR02/RevisionB/Model/QingGuiR02B_LOD0.fbx"));
        var body=baseline.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.Contains("Body_LOD0"));
        var a=body.sharedMesh.vertices.Select(v=>baseline.transform.InverseTransformPoint(body.transform.TransformPoint(v))).Where(v=>v.y<=.105001f).ToArray();
        var b=points.Where(v=>v.y<=.105001f).ToArray();
        float ab=a.Max(v=>b.Min(w=>Vector3.Distance(v,w))),ba=b.Max(v=>a.Min(w=>Vector3.Distance(v,w)));Object.DestroyImmediate(baseline);
        if(Mathf.Max(ab,ba)>.00002f)throw new Exception("Protected insertion vertices changed: "+Mathf.Max(ab,ba));
        var console=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Review/QingGuiR02/ReferenceConsole/TenonConsole.prefab"));console.name="Original production console geometric preview";console.transform.position=Vector3.zero;
        foreach(var c in console.GetComponentsInChildren<Collider>())c.enabled=false;foreach(var lod in console.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
        var colliders=new List<MeshCollider>();var all=new List<Vector3>();
        foreach(var f in console.GetComponentsInChildren<MeshFilter>(true)){if(!f.sharedMesh||!f.name.Contains("LOD0"))continue;var c=f.GetComponent<MeshCollider>();if(!c)c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=false;c.enabled=true;colliders.Add(c);}
        if(colliders.Count!=2)throw new Exception("Expected original body/socket LOD0 geometry");triangles=ReadPreparedTriangles();Physics.SyncTransforms();
        var opening=new Vector3(-.12f,1.028f,0);var seat=new Vector3(-.12f,.9239401f,0);var lines=new List<string>{"GEOMETRIC preview only; not GameItemBase/Meta/XR entry validation.","Protected insertion point distances B->R03="+ab+" R03->B="+ba,"Actual original console triangles="+triangles.Length/3};
        foreach(float depth in new[]{.005f,.015f,.035f,.055f,.075f,.095f}){var hits=new List<float>();foreach(var dir in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back}){float d=float.PositiveInfinity;foreach(var c in colliders)if(c.Raycast(new Ray(opening-Vector3.up*depth,dir),out var hit,.3f))d=Mathf.Min(d,hit.distance);if(!float.IsFinite(d))throw new Exception("Original cavity ray missed");hits.Add(d);}float x=hits[0]+hits[1],z=hits[2]+hits[3];if(Mathf.Abs(x-.153f)>.001f||Mathf.Abs(z-.153f)>.001f)throw new Exception("Original cavity size differs");lines.Add($"depth={depth:F3}m widthX={x:F7} widthZ={z:F7}");}
        float max=0;for(int quarter=0;quarter<4;quarter++)for(int step=0;step<=24;step++)max=Mathf.Max(max,Penetration(Vector3.Lerp(opening+Vector3.up*.03f,seat,step/24f),Quaternion.Euler(0,quarter*90,0)));
        if(max>.0003f)throw new Exception("Conservative gauge intersects original slot insertion path: "+max);
        float tooDeep=Penetration(seat-Vector3.up*.012f,Quaternion.identity),wrong45=Penetration(seat,Quaternion.Euler(0,45,0));lines.Add($"Diagnostic positive controls before assertion: tooDeep={tooDeep:F9};wrong45={wrong45:F9};pathMax={max:F9};consoleMatrix={console.transform.localToWorldMatrix}");File.WriteAllLines(output+"/original-slot-quickfit-partial.txt",lines);if(tooDeep<.001f||wrong45<.001f)throw new Exception($"SAT positive controls failed: tooDeep={tooDeep:F9}, wrong45={wrong45:F9}");
        lines.Add($"PASS quarter-turn straight insertion paths: max gauge intersection={max:F8}m; seat bottom={seat.y:F7}; insertion104.06mm");lines.Add($"Positive controls: tooDeep={tooDeep:F7}, 45deg120mmGauge={wrong45:F7}. The actual106mm end can physically fit45deg; indexed-alignment rejection is a gameplay rule, not a collision claim.");lines.Add("Disabled conceptual full gauge only; this does not prescribe a full 120mm gameplay collider. Source body clearance4mm; shoulder is not a physical stop across153mm hole.");File.WriteAllLines(output+"/original-slot-quickfit.txt",lines);return console;
    }
    static float Penetration(Vector3 root,Quaternion rotation){var inverse=Matrix4x4.TRS(root,rotation,Vector3.one).inverse;Vector3 center=new(0,.175f,0),half=new(.06f,.175f,.06f);float max=0;for(int i=0;i<triangles.Length;i+=3){var a=inverse.MultiplyPoint3x4(triangles[i])-center;var b=inverse.MultiplyPoint3x4(triangles[i+1])-center;var c=inverse.MultiplyPoint3x4(triangles[i+2])-center;float d=float.PositiveInfinity;if(!Axis(Vector3.right,a,b,c,half,ref d)||!Axis(Vector3.up,a,b,c,half,ref d)||!Axis(Vector3.forward,a,b,c,half,ref d))continue;var ab=b-a;var bc=c-b;var ca=a-c;if(!Axis(Vector3.Cross(ab,c-a),a,b,c,half,ref d)||!Edges(ab,a,b,c,half,ref d)||!Edges(bc,a,b,c,half,ref d)||!Edges(ca,a,b,c,half,ref d))continue;if(float.IsFinite(d))max=Mathf.Max(max,d);}return max;}
    static bool Edges(Vector3 e,Vector3 a,Vector3 b,Vector3 c,Vector3 h,ref float d)=>Axis(Vector3.Cross(e,Vector3.right),a,b,c,h,ref d)&&Axis(Vector3.Cross(e,Vector3.up),a,b,c,h,ref d)&&Axis(Vector3.Cross(e,Vector3.forward),a,b,c,h,ref d);
    static bool Axis(Vector3 axis,Vector3 a,Vector3 b,Vector3 c,Vector3 h,ref float depth){float m=axis.magnitude;if(m<1e-9f)return true;axis/=m;float av=Vector3.Dot(a,axis),bv=Vector3.Dot(b,axis),cv=Vector3.Dot(c,axis),min=Mathf.Min(av,Mathf.Min(bv,cv)),max=Mathf.Max(av,Mathf.Max(bv,cv)),radius=h.x*Mathf.Abs(axis.x)+h.y*Mathf.Abs(axis.y)+h.z*Mathf.Abs(axis.z);if(min>radius||max<-radius)return false;depth=Mathf.Min(depth,Mathf.Min(radius-min,max+radius));return true;}
}
