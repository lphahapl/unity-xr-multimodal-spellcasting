using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using NarutoVR.WorkshopTenonR03Review;
using Object=UnityEngine.Object;

public static class WorkshopTenonR03ClampReview
{
    const string Root=WorkshopTenonR03LabBuild.Root;
    const string Output=WorkshopTenonR03LabBuild.Output;
    const float ShoulderBottom=.140f,ShoulderInner=.0535f,Stroke=.0575f;
    static readonly Vector3 Origin=new(0,3,0);
    [Serializable] class Profile {public Part[] parts;}
    [Serializable] class Part {public float lower,upper,width,depth;}
    static Vector3[] vertices;
    static int[] indices;
    static void ReadActualMesh()
    {
        var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Model/MasterTenon_LOD0.fbx"));
        var f=instance.GetComponentsInChildren<MeshFilter>().Single();var m=f.sharedMesh;
        vertices=m.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray();indices=m.triangles;
        Object.DestroyImmediate(instance);
    }
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Root+"/Proposal");Directory.CreateDirectory(Root+"/Proposal/Generated");AssetDatabase.Refresh();ReadActualMesh();
        var profile=JsonUtility.FromJson<Profile>(File.ReadAllText(Root+"/Model/collision-profile.json"));
        var meshes=new List<Mesh>();
        for(int i=0;i<profile.parts.Length;i++) {
            var p=profile.parts[i];var v=new List<Vector3>();var t=new List<int>();
            for(int j=0;j<indices.Length;j+=3) {
                var poly=new List<Vector3>{vertices[indices[j]],vertices[indices[j+1]],vertices[indices[j+2]]};
                poly=Clip(poly,p.lower,true);poly=Clip(poly,p.upper,false);
                for(int k=1;k+1<poly.Count;k++){int start=v.Count;v.Add(poly[0]);v.Add(poly[k]);v.Add(poly[k+1]);t.Add(start);t.Add(start+1);t.Add(start+2);}
            }
            if(t.Count==0)throw new Exception("Empty real-mesh collider section "+i);
            var mesh=new Mesh{name="ActualR03ConvexSlice_"+i};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateBounds();
            string path=Root+"/Proposal/Generated/ActualMeshSlice_"+i+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            meshes.Add(mesh);
        }
        var bronze=Mat("FixtureBronze",new Color(.27f,.17f,.065f),.72f,.38f);
        var stone=Mat("FixtureStone",new Color(.13f,.16f,.17f),0,.35f);
        foreach(bool shoulder in new[]{false,true}) {
            var root=new GameObject(shoulder?"R03 New Shoulder Pin Proposal":"R03 Current Installed Top Latch");
            var state=root.AddComponent<WorkshopTenonR03ClampState>();state.shoulderProposal=shoulder;
            var specimen=new GameObject("Frozen R03 specimen");specimen.transform.SetParent(root.transform,false);
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Model/MasterTenon_LOD0.fbx"));model.transform.SetParent(specimen.transform,false);
            foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/MasterTenon_R03.mat");
            var body=specimen.AddComponent<Rigidbody>();body.mass=.8f;body.isKinematic=true;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.interpolation=RigidbodyInterpolation.None;
            // Put real sliced meshes under an identity root: FBX conversion must not apply twice.
            var proxy=new GameObject("Actual mesh section contact shapes");proxy.transform.SetParent(specimen.transform,false);
            foreach(var mesh in meshes){var child=new GameObject(mesh.name);child.transform.SetParent(proxy.transform,false);var c=child.AddComponent<MeshCollider>();c.sharedMesh=mesh;c.convex=true;c.contactOffset=.0001f;}
            state.specimen=body;var pins=new List<Transform>();var closed=new List<Vector3>();var opened=new List<Vector3>();
            Box(root.transform,"Independent support",new Vector3(0,-.030f,0),new Vector3(.28f,.060f,.28f),stone);
            foreach(float sign in new[]{-1f,1f}) {
                Vector3 position,size,open;
                if(shoulder) {
                    position=new Vector3(0,ShoulderBottom+.006f,sign*(ShoulderInner+.024f));size=new Vector3(.030f,.012f,.048f);open=position+Vector3.forward*(sign*Stroke);
                    Box(root.transform,"Fixed shoulder-slide post",new Vector3(0,.069f,sign*.130f),new Vector3(.050f,.138f,.070f),bronze);
                    Box(root.transform,"Fixed shoulder-slide guide",new Vector3(0,.146f,sign*.130f),new Vector3(.050f,.024f,.070f),bronze);
                } else {
                    position=new Vector3(sign*.0595f,.356f,0);size=new Vector3(.048f,.012f,.030f);open=new Vector3(sign*.117f,.356f,0);
                    // The installed guide and top bar move as a pair, exactly like the two source states.
                    var guide=Box(root.transform,"Current installed vertical guide",new Vector3(sign*.0725f,.175f,0),new Vector3(.022f,.350f,.040f),bronze);
                    pins.Add(guide.transform);closed.Add(guide.transform.localPosition);opened.Add(new Vector3(sign*.130f,.175f,0));
                }
                var pin=Box(root.transform,shoulder?"New Z-axis shoulder stop":"Current X-axis top stop",position,size,bronze);
                pins.Add(pin.transform);closed.Add(position);opened.Add(open);
            }
            state.pins=pins.ToArray();state.closedPositions=closed.ToArray();state.openPositions=opened.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Proposal/"+(shoulder?"ShoulderPinProposal":"CurrentInstalledTopLatch")+".prefab");Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }
    static List<Vector3> Clip(List<Vector3> input,float height,bool greater)
    {
        var result=new List<Vector3>();if(input.Count==0)return result;
        for(int i=0;i<input.Count;i++) {
            var a=input[i];var b=input[(i+1)%input.Count];bool ai=greater?a.y>=height:a.y<=height;bool bi=greater?b.y>=height:b.y<=height;
            if(ai)result.Add(a);if(ai!=bi)result.Add(Vector3.LerpUnclamped(a,b,(height-a.y)/(b.y-a.y)));
        }return result;
    }
    static Material Mat(string name,Color color,float metal,float smooth)
    {
        string path=Root+"/Proposal/Generated/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);return m;
    }
    static GameObject Box(Transform root,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;go.GetComponent<BoxCollider>().contactOffset=.0001f;return go;
    }
    public static string[] Run(Action<Vector3,Vector3> view,Action<string> shot)
    {
        ReadActualMesh();var report=new List<string>();
        float shoulderFaceDepth=0;
        for(int i=0;i<indices.Length;i+=3) {
            var poly=Clip(new List<Vector3>{vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]},ShoulderBottom,true);
            poly=Clip(poly,ShoulderBottom+.012f,false);
            foreach(var v in poly)shoulderFaceDepth=Mathf.Max(shoulderFaceDepth,Mathf.Abs(v.z));
        }
        float actualInitialGap=ShoulderInner-shoulderFaceDepth;
        if(actualInitialGap<.0008f || actualInitialGap>.0012f)throw new Exception("Shoulder proposal nominal 1mm gap does not match frozen mesh: "+actualInitialGap);
        report.Add("Scope: independent R03 mechanical previews. Current installed top bars retain original positions, dimensions and 57.5mm stroke. New shoulder proposal is Z-axis, not a pre-existing shoulder implementation or chapter binding.");
        report.Add("Proposal: pin size XYZ=(.030,.012,.048)m, closed centers=(0,.146,+/-.0775), open centers=(0,.146,+/-.135), bottom=.140, closed inner edge=+/-.0535, nominal initial radial gap=1mm at y=.140. New fixed guide centers=(0,.146,+/-.130), size(.050,.024,.070). Contact target is the narrowing shoulder surface above y=.127, not a ledge spanning the 153mm console opening.");
        report.Add($"Frozen exact triangle band y=.140..152 maximum absolute depth {shoulderFaceDepth:F8}m; measured initial radial gap {actualInitialGap:F8}m; retracted inner edge=.111m, full-envelope clearance >=.051m; uppermost fixture y=.158m is below grip y=.231m.");
        report.Add("Contact proxy: convex hulls of actual LOD0 mesh slices at profile boundaries. Thumb concavity is conservatively filled. This precision clamp specimen is independent from the gameplay review's outer boxes; no claim that those boxes already fit the 1mm shoulder gap.");
        foreach(bool shoulder in new[]{false,true}) {
            var scene=SceneManager.CreateScene("R03ClampPhysics_"+(shoulder?"Shoulder":"Top"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Proposal/"+(shoulder?"ShoulderPinProposal":"CurrentInstalledTopLatch")+".prefab"));SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=Origin;
            var state=root.GetComponent<WorkshopTenonR03ClampState>();state.showPanel=false;state.SetOpen(false);state.ResetSpecimen();Physics.SyncTransforms();
            var barriers=root.GetComponentsInChildren<BoxCollider>().Where(x=>x.name!="Independent support").ToArray();
            float initial=Geometry(barriers,0),lift=Geometry(barriers,.010f);
            if(initial>.00002f)throw new Exception("Clamp initially crosses real mesh: "+shoulder+" "+initial);
            if(lift<.0005f)throw new Exception("Clamp fails 10mm upward positive control: "+shoulder+" "+lift);
            view(Origin+new Vector3(.47f,.36f,-.62f),Origin+Vector3.up*.18f);shot(shoulder?"19_ShoulderProposal_Closed":"17_CurrentTopLatch_Closed");
            var body=state.specimen;body.isKinematic=false;body.useGravity=false;float maximum=0;
            for(int i=0;i<120;i++){body.linearVelocity=Vector3.up*.060f;physics.Simulate(.005f);maximum=Mathf.Max(maximum,body.position.y-Origin.y);}
            if(maximum>.012f)throw new Exception("Dynamic closed clamp failed to stop lift: "+shoulder+" "+maximum);
            float stopped=body.position.y-Origin.y;
            if(shoulder)shot("20_ShoulderProposal_BlockedLift");
            state.ResetSpecimen();state.SetOpen(true);Physics.SyncTransforms();float full=0;
            for(int i=0;i<=400;i++)full=Mathf.Max(full,Geometry(barriers,i*.001f));
            if(full>.00002f)throw new Exception("Open clamp obstructs complete 400mm withdrawal: "+shoulder+" "+full);
            shot(shoulder?"21_ShoulderProposal_Open":"18_CurrentTopLatch_Open");
            body.isKinematic=false;body.useGravity=false;
            for(int i=0;i<1400;i++){body.linearVelocity=Vector3.up*.060f;physics.Simulate(.005f);}
            float withdrawn=body.position.y-Origin.y;
            if(withdrawn<.390f)throw new Exception("Open dynamic withdrawal blocked: "+shoulder+" "+withdrawn);
            report.Add($"PASS {(shoulder?"new shoulder proposal":"current installed top latch")}: initial exact surface intersection {initial:F8}m; +10mm lift exact intersection {lift:F8}m; closed dynamic max lift {maximum:F8}m, rest {stopped:F8}m under repeatedly requested +.06m/s; open 401-point/400mm path intersection {full:F8}m; open dynamic rise {withdrawn:F8}m. No Take/clearance event linked.");
            root.SetActive(false);Object.Destroy(root);SceneManager.UnloadSceneAsync(scene);
        }
        File.WriteAllLines(Output+"/clamp-compatibility.txt",report);return report.ToArray();
    }
    static float Geometry(BoxCollider[] barriers,float lift)
    {
        float maximum=0;
        foreach(var box in barriers) {
            var half=Vector3.Scale(box.size,box.transform.lossyScale)*.5f;var center=box.transform.TransformPoint(box.center)-Origin-Vector3.up*lift;
            for(int i=0;i<indices.Length;i+=3) {
                var a=vertices[indices[i]]-center;var b=vertices[indices[i+1]]-center;var c=vertices[indices[i+2]]-center;float depth=float.PositiveInfinity;
                if(!Axis(Vector3.right,a,b,c,half,ref depth)||!Axis(Vector3.up,a,b,c,half,ref depth)||!Axis(Vector3.forward,a,b,c,half,ref depth))continue;
                var ab=b-a;var bc=c-b;var ca=a-c;
                if(!Axis(Vector3.Cross(ab,c-a),a,b,c,half,ref depth)||!Edges(ab,a,b,c,half,ref depth)||!Edges(bc,a,b,c,half,ref depth)||!Edges(ca,a,b,c,half,ref depth))continue;
                if(float.IsFinite(depth))maximum=Mathf.Max(maximum,depth);
            }
        }return maximum;
    }
    static bool Edges(Vector3 edge,Vector3 a,Vector3 b,Vector3 c,Vector3 half,ref float depth)
    {return Axis(Vector3.Cross(edge,Vector3.right),a,b,c,half,ref depth)&&Axis(Vector3.Cross(edge,Vector3.up),a,b,c,half,ref depth)&&Axis(Vector3.Cross(edge,Vector3.forward),a,b,c,half,ref depth);}
    static bool Axis(Vector3 axis,Vector3 a,Vector3 b,Vector3 c,Vector3 half,ref float depth)
    {
        float length=axis.magnitude;if(length<1e-9f)return true;axis/=length;float av=Vector3.Dot(a,axis),bv=Vector3.Dot(b,axis),cv=Vector3.Dot(c,axis);float lo=Mathf.Min(av,bv,cv),hi=Mathf.Max(av,bv,cv);float radius=Vector3.Dot(half,new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)));if(lo>radius||hi<-radius)return false;depth=Mathf.Min(depth,Mathf.Min(radius-lo,hi+radius));return true;
    }
}
