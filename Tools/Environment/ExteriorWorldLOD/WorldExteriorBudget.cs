// CPU content estimate only: no Camera.Render, Quality changes, internal LODUtility or XR claims.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class WorldExteriorBudget
{
    const float MobileLodBias=1f;
    [Serializable] public class Counts
    {
        public long wholeMeshTriangles,frustumShadowCasterTriangles;
        public int meshBearingRenderers,skinnedMeshRenderers,meshSubmeshes,materialSlots,uniqueMaterials;
        public int frustumShadowCasters,unaccountedRendererTypes,missingMeshes,missingMaterialSlots;
    }
    [Serializable] public class Scopes
    {
        public Counts wholeScene,newContentRoot,outsideNewContentRoot,oldOutsideRootWithoutLOD,allWithoutLOD;
    }
    [Serializable] public class LodChoice
    {
        public string path,fadeMode;
        public float worldSize,cameraForwardDepth,euclideanDistance,relativeHeightWithBias,distanceRelativeHeightWithBias;
        public float[] transitionHeights;
        public int selectedLevel,euclideanSelectedLevel;
        public long[] wholeLevelTriangles;
        public bool nearThreshold,crossFadePeakNotIncluded;
    }
    [Serializable] public class View
    {
        public string name,kind;
        public Vector3 position,lookAt;
        public float verticalFov,aspect,nearClip=.03f,farClip=320f;
        public Scopes forceLOD0,estimatedRuntimeLOD,euclideanDistanceSensitivity;
        public float wholeSceneTriangleReductionPercent,newRootTriangleReductionPercent;
        public LodChoice[] groupChoices;
    }
    [Serializable] public class Report
    {
        public string scenePath,sceneName,time,unityVersion,newContentRoot;
        public string currentPipelineObserved,currentQualityObserved;
        public float mobileLodBiasAssumed=MobileLodBias,currentQualityLodBiasObserved;
        public int maximumLodLevelAssumed=0,activeLODGroups,disabledLODGroups;
        public View[] views;
        public string formula="worldSize = LODGroup.size * max(abs(lossyScale)); reference = TransformPoint(localReferencePoint); forwardDepth = dot(reference-cameraPosition,cameraForward); relativeHeight = worldSize * assumedLodBias / (2*tan(verticalFov/2)*max(forwardDepth,nearClip)); first level whose transitionHeight <= relativeHeight is selected, otherwise culled. Also reports Euclidean-distance sensitivity separately.";
        public string authority="Unity public LODGroup.size/localReferencePoint/GetLODs and QualitySettings.lodBias semantics; https://docs.unity3d.com/6000.3/Documentation/Manual/class-LODGroup.html and https://docs.unity3d.com/6000.3/Documentation/ScriptReference/QualitySettings-lodBias.html. Perspective projected-height formula is explicit CPU approximation, not a claim to reproduce native engine culling exactly. No UnityEditor internal LODUtility used.";
        public string limitations="Whole-mesh counts after AABB frustum tests, no triangle clipping or occlusion. Not draw calls, batches, SetPass, rendered pixels, GPU frame time or actual stereo geometry. Static batching, instancing, renderer features, shader passes, transparency overdraw, live XR asymmetric per-eye projection, manual ForceLOD and runtime LOD modifiers are not modelled. Shadow counts include only frustum-tested selected renderers with shadowCastingMode != Off; off-frustum casters, light-space culling and cascade/pass duplication are not included. Cross-fade temporarily drawing two levels is omitted and flagged. 110-degree proxies use symmetric square monoscopic projection, not actual XR eye data. Mobile lodBias=1 is assumed without changing the current quality/pipeline. Counts may differ from runtime and require on-device measurement.";
    }
    sealed class Membership {public int group,level;}
    sealed class GroupData {public LODGroup group;public LOD[] levels;public float worldSize;public Vector3 reference;public long[] triangles;}
    sealed class Counter
    {
        public Counts data=new Counts();
        readonly HashSet<Material> materials=new HashSet<Material>();
        public void Add(Renderer renderer,Mesh mesh)
        {
            if(!mesh){data.missingMeshes++;return;}
            long triangles=Triangles(mesh);
            if(renderer.shadowCastingMode!=ShadowCastingMode.Off)
            {data.frustumShadowCasters++;data.frustumShadowCasterTriangles+=triangles;}
            if(renderer.shadowCastingMode==ShadowCastingMode.ShadowsOnly)return;
            data.meshBearingRenderers++;
            if(renderer is SkinnedMeshRenderer)data.skinnedMeshRenderers++;
            data.wholeMeshTriangles+=triangles;data.meshSubmeshes+=mesh.subMeshCount;
            foreach(var material in renderer.sharedMaterials)
            {
                data.materialSlots++;
                if(material)materials.Add(material);else data.missingMaterialSlots++;
            }
            data.uniqueMaterials=materials.Count;
        }
    }

    public static void Write(Scene scene,string outputDirectory)
    {
        if(!scene.IsValid()||!scene.isLoaded)throw new ArgumentException("Budget requires a loaded scene");
        if(string.IsNullOrWhiteSpace(outputDirectory))throw new ArgumentException("Explicit outputDirectory required");
        Directory.CreateDirectory(outputDirectory);
        var roots=scene.GetRootGameObjects();
        var newRoot=roots.SingleOrDefault(g=>g.name=="首章外景细化");
        var renderers=roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true))
            .Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy).Distinct().ToArray();
        var allGroups=roots.SelectMany(g=>g.GetComponentsInChildren<LODGroup>(true)).ToArray();
        var groups=allGroups.Where(g=>g.enabled&&g.gameObject.activeInHierarchy).Select(g=>
        {
            var scale=g.transform.lossyScale;var levels=g.GetLODs();
            return new GroupData{group=g,levels=levels,
                worldSize=g.size*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)),
                reference=g.transform.TransformPoint(g.localReferencePoint),
                triangles=levels.Select(l=>l.renderers.Where(r=>r).Distinct().Sum(r=>Triangles(MeshOf(r)))).ToArray()};
        }).ToArray();
        var membership=new Dictionary<Renderer,List<Membership>>();
        for(int g=0;g<groups.Length;g++)for(int level=0;level<groups[g].levels.Length;level++)
            foreach(var renderer in groups[g].levels[level].renderers.Where(r=>r))
            {
                if(!membership.TryGetValue(renderer,out var entries))membership[renderer]=entries=new List<Membership>();
                entries.Add(new Membership{group=g,level=level});
            }
        var report=new Report
        {
            scenePath=scene.path,sceneName=scene.name,time=DateTimeOffset.Now.ToString("O"),unityVersion=Application.unityVersion,
            newContentRoot=newRoot?newRoot.name:"NOT FOUND: new-root scopes empty",
            currentPipelineObserved=GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"none",
            currentQualityObserved=QualitySettings.names[QualitySettings.GetQualityLevel()],
            currentQualityLodBiasObserved=QualitySettings.lodBias,activeLODGroups=groups.Length,
            disabledLODGroups=allGroups.Length-groups.Length
        };
        var views=ReferenceViews();
        foreach(var view in views)
        {
            Vector3 forward=(view.lookAt-view.position).normalized;
            var cameraToWorld=Matrix4x4.TRS(view.position,Quaternion.LookRotation(forward,Vector3.up),Vector3.one);
            // Unity camera space looks down -Z; Transform forward is +Z.
            var worldToCamera=Matrix4x4.Scale(new Vector3(1,1,-1))*cameraToWorld.inverse;
            var projection=Matrix4x4.Perspective(view.verticalFov,view.aspect,view.nearClip,view.farClip);
            var planes=GeometryUtility.CalculateFrustumPlanes(projection*worldToCamera);
            var visible=renderers.Where(r=>GeometryUtility.TestPlanesAABB(planes,r.bounds)).ToArray();
            var choices=new LodChoice[groups.Length];
            for(int i=0;i<groups.Length;i++)
            {
                var group=groups[i];Vector3 delta=group.reference-view.position;
                float depth=Vector3.Dot(delta,forward),distance=delta.magnitude;
                float projected=Height(group.worldSize,depth,view),euclidean=Height(group.worldSize,distance,view);
                var transitions=group.levels.Select(l=>l.screenRelativeTransitionHeight).ToArray();
                choices[i]=new LodChoice
                {
                    path=PathOf(group.group.transform),fadeMode=group.group.fadeMode.ToString(),worldSize=group.worldSize,
                    cameraForwardDepth=depth,euclideanDistance=distance,relativeHeightWithBias=projected,
                    distanceRelativeHeightWithBias=euclidean,transitionHeights=transitions,
                    selectedLevel=SelectLevel(projected,transitions),euclideanSelectedLevel=SelectLevel(euclidean,transitions),
                    wholeLevelTriangles=group.triangles,
                    nearThreshold=transitions.Any(t=>Mathf.Abs(projected-t)<=Mathf.Max(.00001f,t*.05f)),
                    crossFadePeakNotIncluded=group.group.fadeMode!=LODFadeMode.None
                };
            }
            bool Selected(Renderer renderer,int mode)
            {
                if(!membership.TryGetValue(renderer,out var entries))return true;
                // A renderer can be referenced by nested groups; require every owning
                // group's selected level to reference it. No duplicate triangle sums.
                foreach(var owners in entries.GroupBy(e=>e.group))
                {
                    int wanted=mode==0?0:mode==1?choices[owners.Key].selectedLevel:choices[owners.Key].euclideanSelectedLevel;
                    if(wanted<0||!owners.Any(e=>e.level==wanted))return false;
                }
                return true;
            }
            view.forceLOD0=Count(visible.Where(r=>Selected(r,0)),newRoot,membership);
            view.estimatedRuntimeLOD=Count(visible.Where(r=>Selected(r,1)),newRoot,membership);
            view.euclideanDistanceSensitivity=Count(visible.Where(r=>Selected(r,2)),newRoot,membership);
            view.groupChoices=choices;
            view.wholeSceneTriangleReductionPercent=Reduction(view.forceLOD0.wholeScene.wholeMeshTriangles,view.estimatedRuntimeLOD.wholeScene.wholeMeshTriangles);
            view.newRootTriangleReductionPercent=Reduction(view.forceLOD0.newContentRoot.wholeMeshTriangles,view.estimatedRuntimeLOD.newContentRoot.wholeMeshTriangles);
        }
        report.views=views;
        File.WriteAllText(Path.Combine(outputDirectory,"world-lod-budget.json"),JsonUtility.ToJson(report,true));
        var csv=new List<string>{"view,kind,vFov,aspect,forceLOD0WholeMeshTriangles,estimatedWholeMeshTriangles,euclideanSensitivityTriangles,triangleReductionPercent,forceLOD0Renderers,estimatedRenderers,estimatedSubmeshes,estimatedUniqueMaterials,estimatedFrustumShadowCasters,estimatedFrustumShadowCasterTriangles,forceLOD0NewRootTriangles,estimatedNewRootTriangles,oldOutsideNonLODTriangles"};
        foreach(var v in views)
        {
            var a=v.forceLOD0.wholeScene;var b=v.estimatedRuntimeLOD.wholeScene;
            csv.Add(string.Join(",",new[]{v.name,v.kind,F(v.verticalFov),F(v.aspect),a.wholeMeshTriangles.ToString(),b.wholeMeshTriangles.ToString(),
                v.euclideanDistanceSensitivity.wholeScene.wholeMeshTriangles.ToString(),F(v.wholeSceneTriangleReductionPercent),
                a.meshBearingRenderers.ToString(),b.meshBearingRenderers.ToString(),b.meshSubmeshes.ToString(),b.uniqueMaterials.ToString(),
                b.frustumShadowCasters.ToString(),b.frustumShadowCasterTriangles.ToString(),
                v.forceLOD0.newContentRoot.wholeMeshTriangles.ToString(),v.estimatedRuntimeLOD.newContentRoot.wholeMeshTriangles.ToString(),
                v.estimatedRuntimeLOD.oldOutsideRootWithoutLOD.wholeMeshTriangles.ToString()}));
        }
        File.WriteAllLines(Path.Combine(outputDirectory,"world-lod-budget.csv"),csv);
    }
    static Scopes Count(IEnumerable<Renderer> renderers,GameObject newRoot,Dictionary<Renderer,List<Membership>> membership)
    {
        var whole=new Counter();var content=new Counter();var outside=new Counter();var oldNonLod=new Counter();var nonLod=new Counter();
        foreach(var r in renderers)
        {
            bool isNew=newRoot&&r.transform.IsChildOf(newRoot.transform),hasLod=membership.ContainsKey(r);
            Mesh mesh=MeshOf(r);
            if(!mesh&&!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer))
            {
                whole.data.unaccountedRendererTypes++;
                if(isNew)content.data.unaccountedRendererTypes++;else outside.data.unaccountedRendererTypes++;
                if(!hasLod){nonLod.data.unaccountedRendererTypes++;if(!isNew)oldNonLod.data.unaccountedRendererTypes++;}
                continue;
            }
            whole.Add(r,mesh);if(isNew)content.Add(r,mesh);else outside.Add(r,mesh);
            if(!hasLod){nonLod.Add(r,mesh);if(!isNew)oldNonLod.Add(r,mesh);}
        }
        return new Scopes{wholeScene=whole.data,newContentRoot=content.data,outsideNewContentRoot=outside.data,
            oldOutsideRootWithoutLOD=oldNonLod.data,allWithoutLOD=nonLod.data};
    }
    static float Height(float worldSize,float distance,View view)=>worldSize*MobileLodBias/
        (2*Mathf.Tan(view.verticalFov*.5f*Mathf.Deg2Rad)*Mathf.Max(view.nearClip,distance));
    static int SelectLevel(float height,float[] transitions)
    {
        for(int i=0;i<transitions.Length;i++)if(height>=transitions[i])return i;
        return -1; // below last threshold: culled
    }
    static long Triangles(Mesh mesh)
    {
        if(!mesh)return 0;long triangles=0;
        for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=(long)mesh.GetIndexCount(i)/3;
        return triangles;
    }
    static Mesh MeshOf(Renderer renderer)
    {
        if(renderer is SkinnedMeshRenderer skinned)return skinned.sharedMesh;
        var filter=renderer.GetComponent<MeshFilter>();return filter?filter.sharedMesh:null;
    }
    static string PathOf(Transform transform)=>transform.parent?PathOf(transform.parent)+"/"+transform.name:transform.name;
    static float Reduction(long before,long after)=>before>0?100f*(before-after)/before:0f;
    static string F(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
    static View[] ReferenceViews()
    {
        var views=new List<View>
        {
            new View{name="wake-window",kind="desktop-reference",position=new Vector3(-3.8f,1.65f,3),lookAt=new Vector3(-22,1.8f,4),verticalFov=72,aspect=1.6f},
            new View{name="trial-window",kind="desktop-reference",position=new Vector3(4.8f,1.65f,25),lookAt=new Vector3(24,2,26),verticalFov=72,aspect=1.6f},
            new View{name="court",kind="desktop-reference",position=new Vector3(3.7f,1.65f,40.5f),lookAt=new Vector3(-3,1.8f,48),verticalFov=72,aspect=1.6f},
            new View{name="canal",kind="desktop-reference",position=new Vector3(3.3f,1.65f,53),lookAt=new Vector3(0,1.8f,64),verticalFov=72,aspect=1.6f},
            new View{name="portal",kind="desktop-reference",position=new Vector3(5.3f,1.65f,59.7f),lookAt=new Vector3(10,1.6f,63),verticalFov=72,aspect=1.6f},
            new View{name="overview",kind="desktop-reference",position=new Vector3(45,35,12),lookAt=new Vector3(0,0,42),verticalFov=72,aspect=1.6f}
        };
        foreach(var reference in views.Take(5).ToArray())views.Add(new View
        {name=reference.name+"-xr110-proxy",kind="XR-FOV-proxy-monoscopic",position=reference.position,lookAt=reference.lookAt,verticalFov=110,aspect=1});
        return views.ToArray();
    }
}
