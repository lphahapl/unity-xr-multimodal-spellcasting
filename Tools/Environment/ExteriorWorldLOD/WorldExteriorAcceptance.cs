// Isolated Editor batch only. No scene saves, authoring/builders, audio tests or publication.
// EXTERIOR_WORLD_LOG=<absolute task integration directory>; -executeMethod WorldExteriorAcceptance.Run
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NarutoVR.SceneArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class WorldExteriorAcceptance
{
    const string Source="Assets/Scene/Chapter01_Awakening.unity",Candidate="Assets/Scene/QA_WorldExteriorMerged.unity";
    const string Prefab="Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab";
    const string OriginalHash="d0348b2fecbf2ad7630faf51835604ddc6f1a98970cd83b93b5c832e49be1624";
    const string PorchPath="第一章 · 初醒校验室/关外石坪/门廊 · 连续石面";
    [Serializable] public class ProofField {public string id,path,property;}
    [Serializable] public class Proof
    {
        public string baseSha256,candidateSha256,prefabSHA256;
        public bool originalMonoBehavioursByteIdentical;
        public int originalMonoBehaviours;
        public ProofField[] fields;
    }
    [Serializable] public class CheckEntry {public string name,detail;public bool passed;}
    [Serializable] public class RoadPoint
    {
        public string route,obstacles,floor;public int sample;public Vector3 position;
        public bool floorFound,bodyClear;public float floorY;
    }
    [Serializable] public class LodRecord {public string path;public int[] triangles;public float[] thresholds;}
    [Serializable] public class Report
    {
        public bool passed,automaticChecksOnly=true,visualAccepted;
        public string time,unityVersion,baseSha256,candidateSha256,prefabSHA256,exception;
        public CheckEntry[] checks;public RoadPoint[] routeSamples;public LodRecord[] lods;
        public string[] sourceErrors,candidateErrors,sourceWarnings,candidateWarnings;
        public int originalOutsideColliders,originalPrefabColliders,expectedOriginalPrefabAudioSources;
        public string scope="Edit-mode actual merged-scene import, serialized preservation, actual collider/raycast/capsule and LOD/resource checks. Only the two explicitly authorized porch-side BoxColliders may change enabled true to false; all other original collider fields remain fixed, with four new segmented guards required. No old audio scene tests, runtime gameplay state mutation or automatic visual approval. Closed original progression doors remain unchanged; outside route begins at z34.8 beyond their physical tail, without ignoring their collisions.";
    }
    sealed class Snapshot
    {
        public Dictionary<string,string> colliders=new Dictionary<string,string>(),lights=new Dictionary<string,string>();
        public Dictionary<string,string> colliderPaths=new Dictionary<string,string>();
        public List<string> errors=new List<string>(),warnings=new List<string>(),missing=new List<string>();
        public string skybox;
        public int audioOutside;
        public string porchId,porchMaterial;
        public bool porchEnabled;
    }
    static readonly List<CheckEntry> checks=new List<CheckEntry>();
    static readonly List<RoadPoint> road=new List<RoadPoint>();
    static readonly List<LodRecord> lods=new List<LodRecord>();
    static Snapshot collecting;
    static string log;
    static Scene current;
    static Collider[] solids;
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;

    public static void Run()
    {
        if(!Application.isBatchMode){Debug.LogError("World acceptance requires isolated Unity batch.");return;}
        string project=Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
        if(string.Equals(project.TrimEnd('/','\\'),Path.GetFullPath("D:/UnityProjects/NarutoVR").TrimEnd('/','\\'),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to run in the main project");
        Directory.SetCurrentDirectory(project);
        log=Environment.GetEnvironmentVariable("EXTERIOR_WORLD_LOG")??"D:/UnityProjects/NarutoVR/Logs/exterior-world-lod-20261109/integration";
        Directory.CreateDirectory(log);checks.Clear();road.Clear();lods.Clear();
        var report=new Report{time=DateTimeOffset.Now.ToString("O"),unityVersion=Application.unityVersion};
        Snapshot before=null,after=null;
        Application.logMessageReceived+=Log;
        try
        {
            var proof=JsonUtility.FromJson<Proof>(File.ReadAllText(Path.Combine(log,"merge-proof.json")));
            report.baseSha256=Sha(Source);report.candidateSha256=Sha(Candidate);report.prefabSHA256=Sha(Prefab);
            Essential(proof.baseSha256==report.baseSha256 && report.baseSha256==OriginalHash,"Base/source hash binds original scene");
            Essential(proof.candidateSha256==report.candidateSha256 && proof.prefabSHA256==report.prefabSHA256,"Candidate and updated prefab hashes bind Python proof");
            string sourceText=File.ReadAllText(Source),candidateText=File.ReadAllText(Candidate);
            var originalScripts=Blocks(sourceText,"114");var candidateScripts=Blocks(candidateText,"114");
            Essential(proof.originalMonoBehavioursByteIdentical && proof.originalMonoBehaviours==168 && originalScripts.Count==168,"Python proof records original 168 script blocks");
            Check(originalScripts.Count==candidateScripts.Count && originalScripts.All(x=>candidateScripts.ContainsKey(x.Key)&&candidateScripts[x.Key]==x.Value),
                "All original gameplay/audio/stage MonoBehaviour serialization remains exact, including orphan blocks");
            var oldOwners=Blocks(sourceText,"1");var newOwners=Blocks(candidateText,"1");
            string ownerBlock=oldOwners["436909642"], nl=ownerBlock.Contains("\r\n")?"\r\n":"\n";
            string ownerAnchor="  - component: {fileID: 901000001}"+nl;
            Check(newOwners["436909642"]==ownerBlock.Replace(ownerAnchor,ownerAnchor+"  - component: {fileID: 991080001}"+nl),
                "Only restore existing SkillAudioPlayer reciprocal owner reference; no audio payload changes");
            Check(originalScripts["991080001"].Contains("m_GameObject: {fileID: 436909642}")&&
                originalScripts["991080001"].Contains("NarutoVR.SealCombat.SkillAudioPlayer"),
                "Audio owner repair binds actual existing component type and owner");
            var rs=Blocks(sourceText,"104");var rc=Blocks(candidateText,"104");
            Check(rs.Count==1 && rs.All(x=>rc.ContainsKey(x.Key)&&rc[x.Key]==x.Value),"Original RenderSettings, sun reference, skybox and fog bytes unchanged");
            string backup=Path.GetFullPath(Path.Combine(log,"../backup/Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab"));
            Essential(File.Exists(backup),"Original EnvironmentPolish prefab backup exists");

            collecting=before=new Snapshot();
            var baseline=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);Physics.SyncTransforms();
            Essential(baseline.IsValid()&&baseline.isLoaded,"Source opened for outside-new-root collider/light baseline");
            Capture(baseline,before);
            collecting=after=new Snapshot();
            current=EditorSceneManager.OpenScene(Candidate,OpenSceneMode.Single);Physics.SyncTransforms();
            Essential(current.IsValid()&&current.isLoaded,"Merged candidate actually opened");Capture(current,after);
            var root=Root(current);Essential(root,"Merged EnvironmentPolish root resolves");
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)==Prefab,"Updated root remains intended prefab instance");
            report.originalOutsideColliders=before.colliders.Count;
            foreach(var pair in before.colliders)
            {
                bool authorized=AuthorizedCurb(before.colliderPaths[pair.Key]);
                string expected=pair.Value;
                if(authorized)
                {
                    Check(expected.StartsWith("BoxCollider|True|"),"Authorized original curb was enabled BoxCollider "+before.colliderPaths[pair.Key]);
                    expected="BoxCollider|False|"+expected.Substring("BoxCollider|True|".Length);
                }
                Check(after.colliders.ContainsKey(pair.Key)&&after.colliders[pair.Key]==expected,
                    (authorized?"Only authorized curb enabled flag changes ":"Original collider unchanged ")+before.colliderPaths[pair.Key],pair.Key);
            }
            Check(before.colliders.Count==after.colliders.Count,"No outside-root original colliders added/removed");
            ValidateAuthorizedOpenings(root,before,after,sourceText,candidateText,proof);
            Check(before.lights.Count==after.lights.Count&&before.lights.All(x=>after.lights.ContainsKey(x.Key)&&after.lights[x.Key]==x.Value),"Actual original scene lighting unchanged");
            Check(before.skybox==after.skybox,"Actual skybox asset identity unchanged");
            Check(before.audioOutside==after.audioOutside,"Outside-root audio source count unchanged");
            Check(after.missing.Count<=before.missing.Count && !after.missing.Except(before.missing).Any(),"No new missing script components");
            ValidateOriginalPrefab(root,File.ReadAllText(backup),report);
            ValidateLodsAndAssets(root);
            ValidateRing(root);
            ValidateGate(root);
            solids=All<Collider>(current).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger).ToArray();
            ValidateRoutes(root);
            Check(Sha(Source)==report.baseSha256&&Sha(Candidate)==report.candidateSha256&&Sha(Prefab)==report.prefabSHA256,
                "Input scene/prefab hashes unchanged by acceptance");
            CheckNoNewMessages(before.errors,after.errors,"No new Unity errors beyond source baseline");
            CheckNoNewMessages(before.warnings.Where(IsOrphan),after.warnings.Where(IsOrphan),"No additional orphan component warnings");
        }
        catch(Exception e){report.exception=e.ToString();Check(false,"Acceptance exception",e.ToString());}
        finally
        {
            Application.logMessageReceived-=Log;collecting=null;
            report.checks=checks.ToArray();report.routeSamples=road.ToArray();report.lods=lods.ToArray();
            report.sourceErrors=before?.errors.ToArray()??new string[0];report.candidateErrors=after?.errors.ToArray()??new string[0];
            report.sourceWarnings=before?.warnings.ToArray()??new string[0];report.candidateWarnings=after?.warnings.ToArray()??new string[0];
            report.passed=checks.Count>0&&checks.All(c=>c.passed)&&string.IsNullOrEmpty(report.exception);
            File.WriteAllText(Path.Combine(log,"world-parent-review.json"),JsonUtility.ToJson(report,true));
            File.WriteAllLines(Path.Combine(log,"world-acceptance.txt"),checks.Select(c=>(c.passed?"PASS ":"FAIL ")+c.name+" | "+c.detail));
            File.WriteAllLines(Path.Combine(log,"route-failures.txt"),road.Where(p=>!p.floorFound||!p.bodyClear)
                .Select(p=>p.route+" #"+p.sample+" xz="+p.position.ToString("F3")+" floor="+p.floor+" obstacles="+p.obstacles));
        }
        EditorApplication.Exit(report.passed?0:1);
    }
    static T[] All<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static GameObject Root(Scene scene)=>scene.GetRootGameObjects().SingleOrDefault(g=>g.name=="首章外景细化");
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static string F(float v)=>v.ToString("R",Inv);
    static string V(Vector3 v)=>F(v.x)+","+F(v.y)+","+F(v.z);
    static string Q(Quaternion q)=>F(q.x)+","+F(q.y)+","+F(q.z)+","+F(q.w);
    static string Identity(Object o)
    {
        if(!o)return "null";AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long id);return g+":"+id;
    }
    static string Id(Object o)
    {
        var id=GlobalObjectId.GetGlobalObjectIdSlow(o);return unchecked((long)id.targetObjectId)+":"+unchecked((long)id.targetPrefabId);
    }
    static string ColliderValue(Collider c)
    {
        string shape=c is BoxCollider b?V(b.center)+"|"+V(b.size):c is MeshCollider m?Identity(m.sharedMesh)+"|"+m.convex:
            c is SphereCollider s?V(s.center)+"|"+F(s.radius):c is CapsuleCollider p?V(p.center)+"|"+F(p.radius)+"|"+F(p.height)+"|"+p.direction:"other";
        return c.GetType().Name+"|"+c.enabled+"|"+c.isTrigger+"|"+V(c.transform.position)+"|"+Q(c.transform.rotation)+"|"+
            V(c.transform.lossyScale)+"|"+shape+"|"+Identity(c.sharedMaterial);
    }
    static void Capture(Scene scene,Snapshot snapshot)
    {
        var root=Root(scene);Essential(root,"Source/candidate named content root exists");
        foreach(var c in All<Collider>(scene).Where(c=>!c.transform.IsChildOf(root.transform)))
        {snapshot.colliders.Add(Id(c),ColliderValue(c));snapshot.colliderPaths.Add(Id(c),PathOf(c.transform));}
        var porch=All<MeshRenderer>(scene).Where(r=>PathOf(r.transform)==PorchPath).ToArray();
        Essential(porch.Length==1,"Original porch renderer resolves uniquely");
        snapshot.porchId=Id(porch[0]);snapshot.porchEnabled=porch[0].enabled;snapshot.porchMaterial=Identity(porch[0].sharedMaterial);
        foreach(var l in All<Light>(scene))snapshot.lights.Add(Id(l),l.enabled+"|"+l.type+"|"+JsonUtility.ToJson(l.color)+"|"+F(l.intensity)+"|"+
            l.shadows+"|"+F(l.shadowStrength)+"|"+F(l.shadowBias)+"|"+F(l.shadowNormalBias)+"|"+F(l.range)+"|"+V(l.transform.position)+"|"+Q(l.transform.rotation));
        snapshot.skybox=Identity(RenderSettings.skybox);
        snapshot.audioOutside=All<AudioSource>(scene).Count(a=>!a.transform.IsChildOf(root.transform));
        foreach(var t in All<Transform>(scene))for(int i=0;i<GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);i++)snapshot.missing.Add(PathOf(t)+"#"+i);
    }
    static bool AuthorizedCurb(string path)=>path=="第一章 · 初醒校验室/关外石坪/左收边碰撞"||
        path=="第一章 · 初醒校验室/关外石坪/右收边碰撞";
    static string OnlyDisable(string body)
    {
        var matches=Regex.Matches(body,@"^  m_Enabled: 1(?=\r?$)",RegexOptions.Multiline);
        Essential(matches.Count==1,"Original component has one enabled field for exact authorized disable");
        return Regex.Replace(body,@"^  m_Enabled: 1(?=\r?$)","  m_Enabled: 0",RegexOptions.Multiline);
    }
    static void ValidateAuthorizedOpenings(GameObject root,Snapshot before,Snapshot after,string sourceText,string candidateText,Proof proof)
    {
        var curbs=before.colliderPaths.Where(x=>AuthorizedCurb(x.Value)).ToArray();
        Check(curbs.Length==2,"Exactly two named porch curbs receive the enabled-only exception");
        var source=AllBlocks(sourceText);var candidate=AllBlocks(candidateText);
        var authorizedIDs=new HashSet<string>(curbs.Select(c=>c.Key.Split(':')[0]));
        var fields=proof.fields??new ProofField[0];
        Check(fields.Count(f=>AuthorizedCurb(f.path))==2,"Merge proof lists exactly the two authorized curb changes");
        foreach(var curb in curbs)
        {
            string id=curb.Key.Split(':')[0];
            Check(fields.Count(f=>f.id==id&&f.path==curb.Value&&f.property=="m_Enabled")==1,
                "Exact merge proof field binds authorized curb "+curb.Value,id);
            Check(source.ContainsKey(id)&&candidate.ContainsKey(id),"Authorized curb original serialized block retained "+id);
        }
        // Compare entire original collider YAML blocks, not merely the common shape
        // fields captured from the live scene. The two exceptions are scalar-only.
        var colliderClasses=new HashSet<string>{"64","65","135","136","143","146","154"};
        foreach(var block in source)
        {
            string type=Regex.Match(block.Value,@"^--- !u!(\d+)").Groups[1].Value;
            if(!colliderClasses.Contains(type))continue;
            string expected=authorizedIDs.Contains(block.Key)?OnlyDisable(block.Value):block.Value;
            Check(candidate.ContainsKey(block.Key)&&candidate[block.Key]==expected,
                "All unlisted serialized collider fields byte-identical "+block.Key,
                authorizedIDs.Contains(block.Key)?"Only m_Enabled 1 -> 0 permitted":"No change permitted");
        }
        string porchId=before.porchId?.Split(':')[0];
        Check(before.porchEnabled&&!after.porchEnabled&&before.porchId==after.porchId&&before.porchMaterial==after.porchMaterial,
            "Only original continuous porch renderer is disabled; identity/material retained");
        Check(fields.Count(f=>f.id==porchId&&f.path==PorchPath&&f.property=="m_Enabled")==1,
            "Merge proof binds original porch renderer disable");
        if(porchId!=null&&source.ContainsKey(porchId)&&candidate.ContainsKey(porchId))
            Check(candidate[porchId]==OnlyDisable(source[porchId]),"Original porch renderer all other serialized fields unchanged");
        else Check(false,"Original porch renderer serialized block retained");

        var openings=root.transform.Find("关外石坪 · 两侧支路开口");
        Check(openings,"New explicit segmented porch opening root exists");if(!openings)return;
        var guards=openings.GetComponentsInChildren<BoxCollider>(true);
        Check(guards.Length==4&&guards.All(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy),
            "Exactly four new active solid segmented guards");
        foreach(float x in new[]{-2.18f,2.18f})foreach(bool front in new[]{true,false})
        {
            string name=front?"开口前分段护栏":"开口后分段护栏";
            float min=front?34f:38.45f,max=front?35.95f:39.4f;
            var selected=guards.Where(c=>c.name==name&&Mathf.Abs(c.bounds.center.x-x)<.001f).ToArray();
            Check(selected.Length==1,"Unique segmented guard "+name+" x="+x);if(selected.Length!=1)continue;
            var b=selected[0].bounds;
            Check(Mathf.Abs(b.min.z-min)<.001f&&Mathf.Abs(b.max.z-max)<.001f&&
                Mathf.Abs(b.center.y-.4f)<.001f&&Near(b.size,new Vector3(.28f,.94f,max-min)),
                "Segmented guard exact extents "+name+" x="+x,b.ToString("F4"));
        }
        Check(guards.All(c=>!(c.bounds.min.z<38.4499f&&c.bounds.max.z>35.9501f)),
            "Both branch openings remain physically clear over z35.95..38.45");
        var visual=openings.GetComponent<MeshRenderer>();var filter=openings.GetComponent<MeshFilter>();
        Check(visual&&visual.enabled&&Identity(visual.sharedMaterial)==before.porchMaterial&&filter&&filter.sharedMesh,
            "Replacement porch surface uses actual combined mesh and original stone material");
        if(filter&&filter.sharedMesh)
        {
            var vertices=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
            int corners=0;
            foreach(float x in new[]{-2.32f,2.32f})foreach(float y in new[]{-.67f,-.03f})foreach(float z in new[]{33.9f,39.4f})
                if(vertices.Any(v=>(v-new Vector3(x,y,z)).sqrMagnitude<.000001f))corners++;
            Check(corners==8,"Replacement visible floor retains authored 4.64 x .64 x 5.5m slab",corners+"/8 corners");
        }
    }
    static void ValidateOriginalPrefab(GameObject root,string text,Report report)
    {
        var all=AllBlocks(text);var gos=all.Where(x=>x.Value.StartsWith("--- !u!1 ")).ToDictionary(x=>x.Key,x=>x.Value);
        var transforms=all.Where(x=>x.Value.StartsWith("--- !u!4 ")&&!x.Value.Split('\n')[0].Contains("stripped"))
            .Select(x=>new{body=x.Value,go=Field(x.Value,"m_GameObject")}).Where(x=>x.go!=null)
            .ToDictionary(x=>ReferenceID(x.go),x=>x.body);
        var oldBoxes=all.Values.Where(b=>b.StartsWith("--- !u!65 ")).ToArray();
        Check(oldBoxes.Length==4,"Backup contains original four explicit portal/landing colliders");report.originalPrefabColliders=oldBoxes.Length;
        foreach(string body in oldBoxes)
        {
            string go=ReferenceID(Field(body,"m_GameObject"));Essential(gos.ContainsKey(go)&&transforms.ContainsKey(go),"Original prefab collider transform resolves "+go);
            string name=DecodeName(Field(gos[go],"m_Name"));var found=root.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name==name).ToArray();
            Check(found.Length==1,"Original named collider survives uniquely "+name);if(found.Length!=1)continue;
            var collider=found[0];string tr=transforms[go];
            Check(Near(collider.center,VectorField(Field(body,"m_Center")))&&Near(collider.size,VectorField(Field(body,"m_Size")))&&
                collider.enabled==(Field(body,"m_Enabled")=="1")&&collider.isTrigger==(Field(body,"m_IsTrigger")=="1")&&
                Near(collider.transform.localPosition,VectorField(Field(tr,"m_LocalPosition")))&&
                Near(collider.transform.localScale,VectorField(Field(tr,"m_LocalScale")))&&
                Mathf.Abs(Quaternion.Dot(collider.transform.localRotation,QuaternionField(Field(tr,"m_LocalRotation"))))>.999999f,
                "Original prefab collider local shape/state/transform unchanged "+name);
        }
        int audio=all.Values.Count(b=>b.StartsWith("--- !u!82 "));
        foreach(string body in all.Values.Where(b=>b.StartsWith("--- !u!1001 ")))
        {
            string field=Field(body,"m_SourcePrefab");if(field==null)continue;
            var match=Regex.Match(field,@"guid: ([a-f0-9]{32})");if(!match.Success)continue;
            string path=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Essential(prefab,"Original nested prefab dependency loaded "+path);audio+=prefab.GetComponentsInChildren<AudioSource>(true).Length;
        }
        report.expectedOriginalPrefabAudioSources=audio;
        Check(root.GetComponentsInChildren<AudioSource>(true).Length==audio,"New root audio count matches original backed-up prefab",audio.ToString());
    }
    static void ValidateLodsAndAssets(GameObject root)
    {
        var groups=root.GetComponentsInChildren<LODGroup>(true);Check(groups.Length>0,"New content has actual LODGroups");
        foreach(var group in groups)
        {
            var levels=group.GetLODs();var count=new int[levels.Length];var threshold=new float[levels.Length];var used=new HashSet<Renderer>();
            Check(levels.Length==3,"Exactly three real LOD levels "+PathOf(group.transform));
            for(int i=0;i<levels.Length;i++)
            {
                threshold[i]=levels[i].screenRelativeTransitionHeight;
                Check(levels[i].renderers.Length>0,"LOD has renderer references "+group.name+" #"+i);
                foreach(var r in levels[i].renderers)
                {
                    Check(r&&used.Add(r),"LOD renderer exists and is not reused across levels "+group.name+" #"+i);if(!r)continue;
                    var mf=r.GetComponent<MeshFilter>();var mesh=mf?mf.sharedMesh:(r is SkinnedMeshRenderer sm?sm.sharedMesh:null);
                    Check(mesh,"LOD real mesh loaded "+PathOf(r.transform));if(mesh)count[i]+=mesh.triangles.Length/3;
                    Check(r.sharedMaterials.Length>0&&r.sharedMaterials.All(m=>m&&m.shader&&m.shader.name!="Hidden/InternalErrorShader"),"LOD material slots/shaders valid "+PathOf(r.transform));
                }
            }
            Check(count.Length==3&&count[0]>count[1]&&count[1]>count[2]&&count[2]>0,"LOD triangle counts strictly decrease "+PathOf(group.transform),string.Join(",",count));
            Check(threshold.Length==3&&threshold[0]>threshold[1]&&threshold[1]>threshold[2]&&threshold[2]>0,"LOD projected-height thresholds decrease "+group.name);
            lods.Add(new LodRecord{path=PathOf(group.transform),triangles=count,thresholds=threshold});
        }
        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))Check(mf.sharedMesh,"New content mesh reference "+PathOf(mf.transform));
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))Check(r.sharedMaterials.Length>0&&r.sharedMaterials.All(m=>m&&m.shader),"New renderer materials loaded "+PathOf(r.transform));
        foreach(var m in root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct())
        {
            foreach(var message in ShaderUtil.GetShaderMessages(m.shader))Check(message.severity.ToString()!="Error","Shader compile "+m.shader.name,message.message);
            foreach(string dependency in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(m),true))
                if(dependency.StartsWith("Assets/"))Check(AssetDatabase.LoadMainAssetAtPath(dependency),"Material dependency loaded "+dependency);
            string materialPath=AssetDatabase.GetAssetPath(m);
            if(File.Exists(materialPath)&&materialPath.EndsWith(".mat"))foreach(Match match in Regex.Matches(File.ReadAllText(materialPath),@"guid: ([a-f0-9]{32})"))
            {
                string guid=match.Groups[1].Value;if(guid.StartsWith("0000000000000000"))continue;
                Check(!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)),"Material texture/shader GUID resolves "+m.name+" "+guid);
            }
        }
    }
    static void ValidateRing(GameObject root)
    {
        var motions=root.GetComponentsInChildren<ArmillaryRingMotion>(true);Check(motions.Length==1,"Exactly one independently rotating complete ring");if(motions.Length!=1)return;
        var motion=motions[0];var ring=motion.IntactRing;
        Check(ring&&ring.name=="IntactCircle"&&ring.IsChildOf(motion.transform),"Ring motion binds only IntactCircle");
        var frames=motion.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="StationaryFrame").ToArray();
        Check(frames.Length==1,"Fixed StationaryFrame mesh exists");
        if(ring&&frames.Length==1)Check(!frames[0].IsChildOf(ring)&&frames[0]!=ring,"StationaryFrame is outside rotating ring subtree");
        if(ring)Check(!ring.gameObject.isStatic,"Moving complete ring is not static-batched");
        Check(motion.GetComponentsInChildren<ArmillaryRingMotion>(true).Length==1,"Broken frame has no second motion component");
    }
    static void ValidateGate(GameObject root)
    {
        var gates=root.GetComponentsInChildren<SceneTravelGate>(true);Check(gates.Length==1,"Exactly one new reserved SceneTravelGate");if(gates.Length!=1)return;
        var gate=gates[0];Check(!gate.LoadEnabled&&string.IsNullOrEmpty(gate.DestinationScene),"Portal destination remains empty and scene loading disabled");
        var box=gate.GetComponent<BoxCollider>();Check(box&&box.enabled&&box.isTrigger,"Portal trigger volume enabled and isTrigger");
        if(box)Check(Near(box.size,new Vector3(1.75f,2.5f,.7f)),"Portal trigger matches reserved tracked-head volume");
        var portal=gate.transform.parent;Check(portal&&portal.name=="量水道尽头 · 残规门庭","Trigger belongs to portal");
        if(!portal)return;
        foreach(var entry in new[]{new KeyValuePair<string,float>("左门扇 · 开启",82f),new KeyValuePair<string,float>("右门扇 · 开启",-82f)})
        {
            var hinge=portal.Find(entry.Key);
            Check(hinge&&Mathf.Abs(Quaternion.Dot(hinge.localRotation,Quaternion.Euler(0,entry.Value,0)))>.999999f,
                "Door leaf opens toward the north clear of cross-connection "+entry.Key);
        }
        Check(!portal.GetComponentsInChildren<Collider>(true).Any(c=>c.name=="哑铜门带"),"Decorative bronze straps have no physical colliders");
        var obstacles=portal.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger).ToArray();
        foreach(float x in new[]{-.45f,0f,.45f})foreach(float y in new[]{.4f,1.2f,2.2f})
        {
            Vector3 p=portal.TransformPoint(new Vector3(x,y,0));
            var blocked=obstacles.Where(c=>Vector3.Distance(c.ClosestPoint(p),p)<.22f).ToArray();
            Check(blocked.Length==0,"Portal solid aperture clear "+x+","+y,string.Join(";",blocked.Select(c=>PathOf(c.transform))));
        }
        foreach(string name in new[]{"ArrivalAnchor","ExitAnchor"})Check(portal.Find(name),"Portal reserve anchor "+name);
    }
    static void ValidateRoutes(GameObject root)
    {
        var halls=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("工官建筑")&&t.Find("WalkwayConnection")).ToArray();
        Check(halls.Length==3,"Three workshop entrances with actual WalkwayConnection markers");
        Vector3 Anchor(Vector3 expected)
        {
            var list=halls.Select(h=>h.Find("WalkwayConnection")).OrderBy(t=>Vector2.Distance(new Vector2(t.position.x,t.position.z),new Vector2(expected.x,expected.z))).ToArray();
            Essential(list.Length>0&&Vector2.Distance(new Vector2(list[0].position.x,list[0].position.z),new Vector2(expected.x,expected.z))<10,"Actual workshop endpoint found "+expected);
            return list[0].position;
        }
        Road("西坊前庭支路",new[]{new Vector3(0,-.03f,37.2f),new Vector3(-12,-.03f,37.2f),new Vector3(-12,0,8),Anchor(new Vector3(-18.25f,0,8))});
        Road("东坊前庭支路",new[]{new Vector3(0,-.03f,37.2f),new Vector3(16,0,37.2f),new Vector3(16,0,25),Anchor(new Vector3(21.95f,0,25))});
        Road("门庭横接",new[]{new Vector3(5.3f,-.03f,62),new Vector3(10,-.03f,62)});
        Road("门洞穿越",new[]{new Vector3(10,-.03f,62),new Vector3(10,-.03f,65.5f)});
        Road("门后北坊支路",new[]{new Vector3(10,-.03f,65.5f),new Vector3(10,0,68),new Vector3(20,0,68),Anchor(new Vector3(23.65f,0,63.971f))});
        // The preserved closed outer door ends at z34.36. Start after it with
        // physical capsule clearance; never filter that door from obstacle tests.
        Road("原主线门前",new[]{new Vector3(0,-.03f,34.8f),new Vector3(0,-.03f,40),new Vector3(2.65f,-.03f,40),new Vector3(2.65f,-.03f,47.5f)});
        Road("原主线校验后",new[]{new Vector3(2.65f,-.03f,50.5f),new Vector3(2.65f,-.03f,63.4f)});
        foreach(var hall in halls)
        {
            var marker=hall.Find("WalkwayConnection");var floors=hall.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="工坊地面碰撞").ToArray();
            Check(floors.Length==1&&floors[0].enabled&&!floors[0].isTrigger,"Workshop has one enabled solid floor "+hall.name);
            if(floors.Length==1)
            {
                bool hit=floors[0].Raycast(new Ray(marker.position+Vector3.up*3,Vector3.down),out var floor,8);
                Check(hit&&Mathf.Abs(floor.point.y-marker.position.y)<.15f,"Workshop entrance marker supported by own floor "+hall.name,hit?floor.point.ToString("F3"):"MISS");
            }
            var benches=hall.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="A07 · 校器工作台").ToArray();
            Check(benches.Length==1,"One separate A07 workbench in workshop "+hall.name);
            if(benches.Length==1)
            {
                var boxes=benches[0].GetComponentsInChildren<BoxCollider>(true);
                Check(boxes.Length==5&&boxes.All(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy),
                    "A07 retains all five solid collision boxes "+hall.name);
                Check(boxes.All(c=>!Walkable(c)),"A07 colliders cannot masquerade as walkable floor "+hall.name);
            }
            var approach=hall.TransformPoint(new Vector3(0,0,1.25f));
            Road("门口至台前 "+hall.name,new[]{marker.position,approach});
            Road("工作台侧后绕行 "+hall.name,new[]{approach,hall.TransformPoint(new Vector3(1.25f,0,1.25f)),
                hall.TransformPoint(new Vector3(1.25f,0,-1)),hall.TransformPoint(new Vector3(0,0,-1))});
        }
    }
    static bool Walkable(Collider c)
    {
        if(PathOf(c.transform).Contains("A07 · 校器工作台"))return false;
        string n=c.name;
        // Select only real floor surfaces for height. All solid colliders, including
        // old rails and walls, still participate in the subsequent body clearance test.
        return n.Contains("地面")||n.Contains("路面")||n.Contains("连续面")||n.Contains("铺地")||
            n.Contains("地板")||n.Contains("步道")||n.Contains("石台")||n.Contains("台面")||n.Contains("石面")||n.Contains("平台")||n.Contains("巡道");
    }
    static bool Floor(Vector3 xz,out RaycastHit floor)
    {
        floor=default;bool found=false;float highest=float.NegativeInfinity;
        foreach(var c in solids.Where(Walkable))
            if(c.Raycast(new Ray(new Vector3(xz.x,300,xz.z),Vector3.down),out var hit,600)&&hit.normal.y>.75f&&hit.point.y>highest)
            {floor=hit;highest=hit.point.y;found=true;}
        return found;
    }
    static void Road(string name,Vector3[] points)
    {
        int sample=0,failed=0;float? lastY=null;Vector3 previous=default;
        for(int segment=0;segment<points.Length-1;segment++)
        {
            var a=points[segment];var b=points[segment+1];int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/.30f));
            for(int i=segment==0?0:1;i<=count;i++)
            {
                Vector3 p=Vector3.Lerp(a,b,i/(float)count);
                var entry=new RoadPoint{route=name,sample=sample++,position=p};
                entry.floorFound=Floor(p,out var hit);entry.floor=entry.floorFound?PathOf(hit.collider.transform):"NO WALKABLE FLOOR";
                entry.floorY=entry.floorFound?hit.point.y:p.y;
                Vector3 center=new Vector3(p.x,entry.floorY+.9f,p.z);
                var hits=Physics.OverlapCapsule(center-Vector3.up*.58f,center+Vector3.up*.58f,.24f,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&c.gameObject.scene==current).ToArray();
                entry.bodyClear=hits.Length==0;entry.obstacles=string.Join(";",hits.Select(c=>PathOf(c.transform)+" ["+c.GetType().Name+"]"));
                bool continuous=true;
                if(lastY.HasValue&&entry.floorFound)
                {
                    float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(previous.x,previous.z));
                    continuous=Mathf.Abs(entry.floorY-lastY.Value)<=distance*Mathf.Tan(15*Mathf.Deg2Rad)+.08f;
                }
                if(!continuous)entry.obstacles+="; FLOOR STEP/SLOPE exceeds continuous 15deg+8cm allowance";
                if(!entry.floorFound||!entry.bodyClear||!continuous)failed++;
                // Preserve the explicit failing condition in machine-readable point data.
                entry.bodyClear&=continuous;road.Add(entry);
                lastY=entry.floorFound?(float?)entry.floorY:null;previous=p;
            }
        }
        Check(failed==0,"Walkable support and real body clearance "+name,failed+" failed / "+sample+" samples; all old/new solid rails included; see route-failures.txt");
    }
    static void CheckNoNewMessages(IEnumerable<string> source,IEnumerable<string> candidate,string label)
    {
        string Normalize(string s)=>s.Replace(Source,"<SCENE>").Replace(Candidate,"<SCENE>").Replace(Source.Replace('/','\\'),"<SCENE>").Replace(Candidate.Replace('/','\\'),"<SCENE>");
        var old=source.Select(Normalize).GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count());
        var extra=candidate.Select(Normalize).GroupBy(s=>s).Where(g=>!old.ContainsKey(g.Key)||g.Count()>old[g.Key]).Select(g=>g.Key).ToArray();
        Check(extra.Length==0,label,string.Join("\n",extra));
    }
    static bool IsOrphan(string s)=>s.Contains("does not reference component MonoBehaviour");
    static void Log(string message,string stack,LogType type)
    {
        if(collecting==null)return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)collecting.errors.Add(message);
        else if(type==LogType.Warning)collecting.warnings.Add(message);
    }
    static Dictionary<string,string> Blocks(string text,string type)=>Regex.Matches(text,@"^--- !u!"+type+@" &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline)
        .Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
    static Dictionary<string,string> AllBlocks(string text)=>Regex.Matches(text,@"^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline)
        .Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
    static string Field(string text,string field)
    {
        var m=Regex.Match(text,@"^  "+Regex.Escape(field)+@": ([^\r\n]*)",RegexOptions.Multiline);return m.Success?m.Groups[1].Value:null;
    }
    static string ReferenceID(string value)=>Regex.Match(value??"",@"fileID: (-?\d+)").Groups[1].Value;
    static string DecodeName(string value)=>value!=null&&value.StartsWith("\"")?Regex.Unescape(value.Trim('"')):value;
    static float Axis(string value,string axis)=>float.Parse(Regex.Match(value,@"(?:\{|, )"+axis+@": ([^,}]+)").Groups[1].Value,Inv);
    static Vector3 VectorField(string value)=>new Vector3(Axis(value,"x"),Axis(value,"y"),Axis(value,"z"));
    static Quaternion QuaternionField(string value)=>new Quaternion(Axis(value,"x"),Axis(value,"y"),Axis(value,"z"),Axis(value,"w"));
    static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<.00000001f;
    static void Essential(bool value,string name){Check(value,name);if(!value)throw new InvalidOperationException(name);}
    static void Check(bool value,string name,string detail=""){checks.Add(new CheckEntry{passed=value,name=name,detail=detail});}
    static string Sha(string file){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();}
}
