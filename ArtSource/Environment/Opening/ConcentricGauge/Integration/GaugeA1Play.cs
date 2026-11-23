using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Obj = UnityEngine.Object;

// Run on an isolated scene copy with a private checkpoint name. The hand fixture feeds
// production SealLab observations through PlayerInputController and OpeningLeafPuzzle.
[InitializeOnLoad]
public static class GaugeA1Play
{
    const string Key = "GaugeA1Play";
    static string Prefix => Path.Combine(GaugeA1Audit.Output, Environment.GetEnvironmentVariable("A1_PASS") + "-" + Environment.GetEnvironmentVariable("A1_SCENE"));
    static OpeningLeafPuzzle puzzle;
    static double deadline;
    static int solved;
    static bool released;

    static GaugeA1Play()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 130;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + "Result", 1));
            }
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode || !Application.dataPath.Contains("ConcentricGaugeA1-Integration"))
            throw new InvalidOperationException("Start this check in the isolated A1 project.");
        var p = GaugeA1Audit.Open(Environment.GetEnvironmentVariable("A1_SCENE"));
        p.rpg.autoLoad = false;
        p.rpg.saveFileName = "probe-mvc-a1-" + Guid.NewGuid().ToString("N") + ".json";
        p.rpg.skills.voice.autoStartInEditor = false;
        var scene = p.gameObject.scene;
        EditorSceneManager.SaveScene(scene, "Assets/Scene/GaugeA1PlayCopy.unity");
        File.WriteAllText(Prefix + ".txt", DateTimeOffset.Now.ToString("o") + "\n");
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + "Result", 1);
        EditorApplication.EnterPlaymode();
    }
    public static void VerifyAndPlay()
    {
        GaugeA1Geometry.Verify();
        Run();
    }
    public static void StageAndPlay()
    {
        var copy = Directory.GetParent(Application.dataPath).FullName;
        if (!Application.isBatchMode || !Directory.Exists(Path.Combine(copy,"Baseline")) || !copy.EndsWith("ConcentricGaugeA1-Integration"))
            throw new InvalidOperationException("Expected frozen snapshot in the isolated project.");
        GaugeA1Scene.Stage();
        string script = Environment.GetEnvironmentVariable("A1_TRANSFER");
        var start = new System.Diagnostics.ProcessStartInfo("python",
            $"-X utf8 \"{script}\" --copy \"{copy}\" --target \"{copy}\" --evidence \"{GaugeA1Audit.Output}\"")
            { UseShellExecute=false, CreateNoWindow=true };
        using var patch=System.Diagnostics.Process.Start(start);
        patch.WaitForExit();
        if (patch.ExitCode!=0) throw new Exception("Isolated scene patch failed");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        VerifyAndPlay();
    }

    static void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        File.AppendAllText(Prefix + ".txt", "PASS " + message + "\n");
    }

    static void Tick()
    {
        try
        {
            if (!puzzle) puzzle = Obj.FindFirstObjectByType<OpeningLeafPuzzle>(FindObjectsInactive.Include);
            if (EditorApplication.timeSinceStartup >= deadline)
                throw new TimeoutException($"ready={puzzle?.rpg?.IsReady} status={puzzle?.rpg?.Status} released={released} solved={solved}");
            if (!puzzle || !puzzle.rpg.CanAct) return;
            if (!released)
            {
                puzzle.Restore(Array.Empty<string>());
                puzzle.EnablePuzzle();
                puzzle.Solved += () => solved++;
                Check(puzzle.CanDemonstrate(puzzle.outerHandle) && !puzzle.CanDemonstrate(puzzle.innerHandle) && puzzle.lid.activeInHierarchy, "initial outer hint and closed lid");
                Capture("wide", new Vector3(.08f,.69f,-2.95f), new Vector3(0,-.19f,.05f), 45);
                Capture("close", new Vector3(.14f,.51f,-.52f), Vector3.up*.017f, 44);
                Capture("side", new Vector3(.43f,.06f,-.48f),new Vector3(0,-.008f,0),32);
                var restBefore = puzzle.outerDisc.parent.InverseTransformPoint(puzzle.prizeRest.position);
                Capture("closed",restBefore + new Vector3(-.35f,.39f,-.43f),restBefore,42);
                TurnBoth();
                released = true;
                return;
            }
            if (solved == 0) return;
            Check(solved == 1 && !puzzle.lid.activeSelf && puzzle.leaf.activeInHierarchy, "one solved event, lid hidden and physical reward active");
            Check(Vector3.Distance(puzzle.leaf.transform.position, puzzle.prizeRest.position) < .001f, "reward remains at original rest");
            var rest = puzzle.outerDisc.parent.InverseTransformPoint(puzzle.prizeRest.position);
            Capture("open", rest + new Vector3(-.35f,.39f,-.43f), rest, 42);
            Capture("aligned",new Vector3(.04f,.54f,-.31f), Vector3.up*.017f, 44);
            SessionState.SetInt(Key + "Result", 0);
        }
        catch (Exception ex)
        {
            File.AppendAllText(Prefix + ".txt", ex + "\n");
            Debug.LogException(ex);
        }
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }

    static void TurnBoth()
    {
        var fixture = typeof(OpeningRouteCheckpointChecks).GetNestedType("PuzzleHand", BindingFlags.NonPublic);
        using var hand = (IDisposable)Activator.CreateInstance(fixture, puzzle.rpg, puzzle);
        var send = fixture.GetMethod("Move");
        void Move(Vector3 pos, bool pressed) => send.Invoke(hand, new object[] { pos, pressed });
        void Turn(Transform handle, float angle)
        {
            var tip = handle.position;
            Move(tip, false);
            Move(tip, true);
            Check(puzzle.rpg.Input.InteractionOwnsHand, "pinch acquired " + handle.name);
            var axis = puzzle.outerDisc;
            var dest = axis.position + Quaternion.AngleAxis(angle, axis.up) * (tip - axis.position);
            Move(dest, true);
            Check(solved == 0 && puzzle.lid.activeSelf, "lid stays closed during turn");
            Move(dest, false);
            Check(!puzzle.rpg.Input.InteractionOwnsHand, "release returned hand");
        }
        var initialInner = puzzle.innerDisc.localRotation;
        Turn(puzzle.outerHandle, -puzzle.outerStartDegrees);
        Check(Quaternion.Angle(puzzle.outerDisc.localRotation, Quaternion.identity) < .2f &&
              Quaternion.Angle(initialInner, puzzle.innerDisc.localRotation) > 1, "outer aligned on local X and carried inner");
        Check(!puzzle.CanDemonstrate(puzzle.outerHandle) && puzzle.CanDemonstrate(puzzle.innerHandle), "inner handle replaces outer as guidance target");
        Turn(puzzle.innerHandle, puzzle.outerStartDegrees - puzzle.innerStartDegrees);
        Check(Quaternion.Angle(puzzle.outerDisc.localRotation, Quaternion.identity) < .2f &&
              Quaternion.Angle(puzzle.innerDisc.localRotation, Quaternion.identity) < .2f,
              "inner aligned independently, both pivots in reference pose");
    }

    static void Capture(string name, Vector3 eye, Vector3 aim, float fov)
    {
        var axis = puzzle.outerDisc.parent;
        var cam = new GameObject("A1 evidence camera").AddComponent<Camera>();
        cam.enabled = false;
        cam.transform.SetPositionAndRotation(axis.TransformPoint(eye), Quaternion.LookRotation(axis.TransformDirection(aim-eye), axis.up));
        cam.nearClipPlane = .018f;
        cam.farClipPlane = 300;
        cam.fieldOfView = fov;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var rt = RenderTexture.GetTemporary(1600,1000,24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var tex = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
            tex.Apply();
            File.WriteAllBytes(Prefix + "-" + name + ".png",tex.EncodeToPNG());
            if (Environment.GetEnvironmentVariable("A1_PASS")=="final")
            {
                if (name=="close" || name=="aligned")
                {
                    Visible(cam, puzzle.outerDisc.Find("A1_Outer").GetComponentInChildren<MeshRenderer>(), puzzle.outerHandle.position);
                    Visible(cam, puzzle.innerDisc.Find("A1_Inner").GetComponentInChildren<MeshRenderer>(), puzzle.innerHandle.position);
                }
                if (name=="closed") Visible(cam,puzzle.lid.transform.Find("A1_Lid").GetComponentInChildren<MeshRenderer>(),puzzle.lid.transform.position);
                if (name=="open") Visible(cam,puzzle.leaf.GetComponentInChildren<MeshRenderer>(),puzzle.leaf.GetComponentInChildren<MeshRenderer>().bounds.center);
            }
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Obj.DestroyImmediate(tex);
            Obj.DestroyImmediate(cam.gameObject);
        }
    }

    static void Visible(Camera cam,MeshRenderer wanted,Vector3 target)
    {
        var test = typeof(HandleUtility).GetMethod("IntersectRayMesh",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static);
        var surfaces = Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                          .Where(r=>r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        // Rays sample actual triangles near the grip or reward, allowing for holes
        // in the grip bow and the leaf silhouette.
        var filter=wanted.GetComponent<MeshFilter>();
        using var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
        using var vertices=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp);
        using var indices=new NativeArray<int>(data[0].GetSubMesh(0).indexCount,Allocator.Temp);
        data[0].GetVertices(vertices);
        data[0].GetIndices(indices,0);
        var points=Enumerable.Range(0,indices.Length/3).Select(i=>filter.transform.TransformPoint(
            (vertices[indices[3*i]]+vertices[indices[3*i+1]]+vertices[indices[3*i+2]])/3f))
            .OrderBy(v=>(v-target).sqrMagnitude).Take(100);
        foreach (var point in points)
        {
            var ray=new Ray(cam.transform.position,(point-cam.transform.position).normalized);
            float best=float.PositiveInfinity;
            MeshRenderer hit=null;
            foreach (var r in surfaces)
            {
                if (!r.bounds.IntersectRay(ray,out var entry) || entry>best) continue;
                var args=new object[]{ray,r.GetComponent<MeshFilter>().sharedMesh,r.localToWorldMatrix,null};
                if (!(bool)test.Invoke(null,args)) continue;
                float distance=((RaycastHit)args[3]).distance;
                if (distance<best) { best=distance; hit=r; }
            }
            if (hit==wanted)
            {
                Check(true,"visible triangle nearest to camera belongs to "+GaugeA1Audit.PathOf(wanted.transform));
                return;
            }
        }
        throw new Exception("No unobstructed sample for "+wanted.name);
    }
}
