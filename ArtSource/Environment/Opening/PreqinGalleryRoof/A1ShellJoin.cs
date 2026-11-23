using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UObject=UnityEngine.Object;

// Editor authoring helper. Build the .blend with build.py before invoking Join in the staging project.
public static class GalleryCanopyAuthor {
 static string Work=>Environment.GetEnvironmentVariable("CH_PHOTO");
 [Serializable]class CanopyParts{public Piece[] parts;}
 [Serializable]class Piece{public string name,mat;public float[] points,normals,uv;}
 const string Kit="Assets/Art/Environment/Opening/PreqinGalleryRoof";
 const string RailPrefab="Assets/Art/Environment/Opening/CourtLayout/崖径石栏.prefab";
 static Vector3 V3(float[] a,int i)=>new Vector3(a[3*i],a[3*i+1],a[3*i+2]);
 static Vector2 V2(float[] a,int i)=>new Vector2(a[2*i],a[2*i+1]);
 static string AssetRef(UObject o){AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long id);return id+"@"+g;}
 static Mesh Save(Mesh m,string name){var p=Kit+"/"+name+".asset";m.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(!old){AssetDatabase.CreateAsset(m,p);return m;}EditorUtility.CopySerialized(m,old);EditorUtility.SetDirty(old);UObject.DestroyImmediate(m);return old;}
 static Mesh Canopy(Mesh baseMesh){
 var v=baseMesh.vertices.ToList();var n=baseMesh.normals.ToList();var uv=baseMesh.uv.ToList();var uv2=baseMesh.uv2.ToList();while(uv2.Count<v.Count)uv2.Add(Vector2.zero);
 var col=baseMesh.colors.ToList();while(col.Count<v.Count)col.Add(Color.white);
 var tr=Enumerable.Range(0,baseMesh.subMeshCount).Select(i=>baseMesh.GetTriangles(i).ToList()).ToList();tr.Add(new List<int>());
 var removed=tr[2].ChunkTriangles().Where(t=>t.All(i=>v[i].y>=1.29f&&v[i].y<=1.80f&&(Mathf.Abs(v[i].z+4.964f)<.005f||Mathf.Abs(v[i].z-5.376f)<.005f))).ToArray();
 if(removed.Length!=12)throw new Exception("Old roof slab changed: "+removed.Length);
 var oldRoofIds=new HashSet<int>(removed.SelectMany(t=>t));tr[2]=tr[2].Where(i=>!oldRoofIds.Contains(i)).ToList();
 // Match the mesh's existing front-face convention and weld only matching smoothing / UV seams.
 var refTri=tr[0].ChunkTriangles().First(t=>Vector3.Cross(v[t[1]]-v[t[0]],v[t[2]]-v[t[0]]).sqrMagnitude>1e-10f);
 float sign=Mathf.Sign(Vector3.Dot(Vector3.Cross(v[refTri[1]]-v[refTri[0]],v[refTri[2]]-v[refTri[0]]),n[refTri[0]]));
 var lookup=new Dictionary<(Vector3,Vector3,Vector2),int>();
 foreach(var pc in JsonUtility.FromJson<CanopyParts>(File.ReadAllText(Work+"/canopy.json")).parts){
 int slot=pc.mat=="Timber"?2:tr.Count-1;
 for(int i=0;i<pc.points.Length/3;i+=3){
 var a=V3(pc.points,i);var b=V3(pc.points,i+1);var c=V3(pc.points,i+2);var normal=V3(pc.normals,i);
 bool flip=Vector3.Dot(Vector3.Cross(b-a,c-a),normal)*sign<0;
 for(int j=0;j<3;j++){int k=i+(flip&&j>0?3-j:j);var pos=V3(pc.points,k);var dir=V3(pc.normals,k);var coord=V2(pc.uv,k);var key=(pos,dir,coord);
 if(!lookup.TryGetValue(key,out var id)){lookup[key]=id=v.Count;v.Add(pos);n.Add(dir);uv.Add(coord);uv2.Add(Vector2.zero);col.Add(Color.white);}tr[slot].Add(id);}
 }}
 var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetUVs(1,uv2);mesh.SetColors(col);mesh.subMeshCount=tr.Count;for(int i=0;i<tr.Count;i++)mesh.SetTriangles(tr[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();return Save(mesh,"GalleryCanopyAndFrame");
 }
 static IEnumerable<int[]> ChunkTriangles(this List<int> t){for(int i=0;i<t.Count;i+=3)yield return new[]{t[i],t[i+1],t[i+2]};}
 static void FinishRails(){
 var g=PrefabUtility.LoadPrefabContents(RailPrefab);
 try{var fs=g.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh&&f.sharedMesh.name.StartsWith("StoneRail")).ToArray();if(fs.Length!=20)throw new Exception("Rail count "+fs.Length);
 var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Opening/PhotoArchitecture/Stone.mat");
 var m=UObject.Instantiate(fs[0].sharedMesh);var v=m.vertices;var ns=m.normals;var uv=new Vector2[v.Length];
 for(int i=0;i<v.Length;i++){var p=v[i]*100f;var q=ns[i];float x=Math.Abs(q.x),y=Math.Abs(q.y),z=Math.Abs(q.z);uv[i]=x>y&&x>z?new Vector2(p.y,p.z):y>z?new Vector2(p.x,p.z):new Vector2(p.x,p.y);uv[i]/=1.25f;}m.uv=uv;m.RecalculateTangents();m=Save(m,"StoneRailCutSurface");
 foreach(var f in fs){f.sharedMesh=m;var r=f.GetComponent<MeshRenderer>();r.sharedMaterial=stone;PrefabUtility.RecordPrefabInstancePropertyModifications(f);PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
 PrefabUtility.SaveAsPrefabAsset(g,RailPrefab);
 }finally{PrefabUtility.UnloadPrefabContents(g);}
 }
 public static void Join(){try{Directory.CreateDirectory(Kit);AssetDatabase.Refresh();var lines=new List<string>();Mesh result=null;
 foreach(var n in new[]{"Chapter01_Awakening","Chapter01_Courtyard"}){var s=EditorSceneManager.OpenScene("Assets/PhotoArtCheck/"+n+".unity");var f=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Single(x=>x.name=="静态合并_CourtGallery"&&x.gameObject.activeInHierarchy);var r=f.GetComponent<MeshRenderer>();var mesh=f.sharedMesh;var mats=r.sharedMaterials;var tile=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Opening/TianyiPavilionKit/Tile.mat");if(!result)result=Canopy(mesh);
 lines.Add(string.Join("\t",n,GlobalObjectId.GetGlobalObjectIdSlow(f).targetObjectId,GlobalObjectId.GetGlobalObjectIdSlow(r).targetObjectId,AssetRef(mesh),AssetRef(result),string.Join(",",mats.Select(AssetRef)),string.Join(",",mats.Concat(new[]{tile}).Select(AssetRef))));}
 FinishRails();AssetDatabase.SaveAssets();File.WriteAllLines(Work+"/canopy-map.tsv",lines);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
