using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class CourtFarAuthor {
 const string Root="Assets/Art/Environment/Opening/";
 const string Out=Root+"CourtFarLOD/";
 public static void Run(){
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var log=new StringBuilder();
   var src=new[]{ "CourtPineFar","CourtPineHorizon","CourtWallFar"};
   foreach(var n in src){
      var go=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/CourtFarInput/"+n+".fbx");var mf=go.GetComponentInChildren<MeshFilter>();
      var m=new Mesh{name=n};m.CombineMeshes(new[]{new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=0,transform=mf.transform.localToWorldMatrix}},true,true,false);
      m.RecalculateBounds();if(m.bounds.size.y<1f||m.bounds.size.z/m.bounds.size.y>(n=="CourtWallFar"?.45f:.85f))throw new Exception("Y/Z mismatch: "+n+" bounds="+m.bounds+" source="+mf.transform.localToWorldMatrix);
      if(m.bounds.min.y<-.012f||m.bounds.min.y>.012f)throw new Exception("Footing "+n+" "+m.bounds);
      string path=Out+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path);
      var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
      if(mesh.uv.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount||mesh.subMeshCount!=1)throw new Exception("UV/tangent verification: "+n);
      log.AppendLine(n+" tris="+mesh.GetIndexCount(0)/3+" verts="+mesh.vertexCount+" low="+mesh.bounds.min.ToString("F3")+" high="+mesh.bounds.max.ToString("F3")+" uv/tangent="+mesh.uv.Length+"/"+mesh.tangents.Length);
   }
   AssetDatabase.SaveAssets();
   var sc=EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Awakening.unity");
   var tree=sc.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<LODGroup>(true)).First(x=>x.name=="西窗外山松");
   var wall=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"CourtyardInfill/B01/WallStraight.prefab"))).GetComponent<LODGroup>();
   Mesh BakeOld(LODGroup g){var r=g.GetLODs()[2].renderers[0];var m=new Mesh();
       m.CombineMeshes(new[]{new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,subMeshIndex=0,transform=g.transform.worldToLocalMatrix*r.transform.localToWorldMatrix}},true,true,false);
       m.RecalculateBounds();return m;}
   var oldTree=BakeOld(tree);var oldWall=BakeOld(wall);oldTree.hideFlags=HideFlags.HideAndDontSave;oldWall.hideFlags=HideFlags.HideAndDontSave;var treeMat=tree.GetLODs()[2].renderers[0].sharedMaterial;var wallMat=wall.GetLODs()[2].renderers[0].sharedMaterial;
   log.AppendLine("Previous pine "+oldTree.bounds.ToString("F3")+" material="+AssetDatabase.GetAssetPath(treeMat));log.AppendLine("Previous wall "+oldWall.bounds.ToString("F3")+" material="+AssetDatabase.GetAssetPath(wallMat));
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var cam=new GameObject().AddComponent<Camera>();cam.enabled=false;
   cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.67f,.74f,.76f);cam.orthographic=true;cam.nearClipPlane=.05f;cam.farClipPlane=60;
   cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;cam.allowHDR=false;
   var sun=new GameObject().AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(42,-28,0);sun.intensity=1.65f;
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.67f,.65f);var item=new GameObject();var f=item.AddComponent<MeshFilter>();var rr=item.AddComponent<MeshRenderer>();
   var rt=new RenderTexture(600,420,24);rt.Create();cam.targetTexture=rt;var tex=new Texture2D(600,420,TextureFormat.RGB24,false);
   foreach(var n in new[]{"ExistingPine","CourtPineFar","CourtPineHorizon","ExistingWall","CourtWallFar"}){
      bool pine=n.Contains("Pine");f.sharedMesh=n=="ExistingPine"?oldTree:n=="ExistingWall"?oldWall:AssetDatabase.LoadAssetAtPath<Mesh>(Out+n+".asset");
      rr.sharedMaterial=pine?treeMat:wallMat;Vector3 focus=pine?new Vector3(0,2.35f,0):new Vector3(0,.82f,0);
      foreach(float a in new[]{-25f,112f}){
        cam.transform.position=focus+Quaternion.Euler(0,a,0)*new Vector3(0,pine?2f:.7f,pine?-12f:-5.5f);
        cam.transform.LookAt(focus);cam.orthographicSize=pine?2.85f:1.05f;cam.Render();cam.Render();
        RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,600,420),0,0);tex.Apply();
        File.WriteAllBytes("FarMeshReview/"+n+(a<0?"_front":"_rear")+".png",tex.EncodeToPNG());
      }
   }
   cam.targetTexture=null;RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
   File.WriteAllText("FarMeshReview/import.txt",log.ToString());Debug.Log("COURT_FAR_UNITY_PASS "+log);
 }
}


