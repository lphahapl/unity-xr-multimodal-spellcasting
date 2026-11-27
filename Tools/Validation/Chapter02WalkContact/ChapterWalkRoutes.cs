using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;

public static partial class ChapterWalkCheck
{
    static RpgLocomotionController loc;
    static double stamp;
    static StreamWriter frames;
    static string route;
    static int assertions, failures;
    static readonly List<string> report = new();
    static partial void Routes() => RunRoutes();
    static Vector3 Feet => loc.PlayerFloorPosition;
    static void Check(bool passed, string message)
    {
        ++assertions;
        if (!passed) ++failures;
        report.Add((passed ? "PASS " : "FAIL ")+route+" "+message);
    }
    static async Task Sample(Vector3 direction, bool point=true)
    {
        await Awaitable.NextFrameAsync();
        NoXR(XRGeneralSettings.Instance);
        var before=Feet;
        var wrist=loc.head.position+new Vector3(-.22f,-.24f,.12f);
        stamp+=.02;
        var hand=new NavigationHandInput {
            leftValid=true, leftObserved=stamp, leftWrist=wrist,leftIndex=wrist,
            leftOpen=!point,leftPointing=point,leftRayValid=point,leftRayOrigin=wrist,leftRayDirection=direction.normalized,
            rightValid=true,rightObserved=stamp,rightWrist=loc.head.position+new Vector3(.32f,-.28f,.1f)
        };
        loc.ProcessInput(hand,false,false,stamp);
        var p=Feet;
        frames.WriteLine($"{route}\t{Time.frameCount}\t{before:F5}\t{p:F5}\t{hand.leftPointing}\t{direction.normalized:F5}\t{loc.Status}");
    }
    static async Task Place(string label, Vector3 pos, ExplorationMovement mode=ExplorationMovement.PointWalk)
    {
        route=label;
        loc.rigRoot.position+=pos-Feet;
        Physics.SyncTransforms();
        loc.ResetInput();loc.SetEncounterCombat(false);loc.SetExplorationMode(mode);
        await Sample(Vector3.forward,false);
        await Sample(Vector3.forward,false);
        if(Vector3.Distance(pos,Feet)>.003f) throw new Exception("unstable setup "+label+" "+Feet);
    }
    static async Task Step(Vector3 dir,int count)
    {
        for(int i=0;i<count;++i) await Sample(dir);
    }
    static async Task Edge(string label, Vector3 start, Vector3 heading, Vector3 tangent, Collider[] sides)
    {
        await Place(label,start);
        float maxGrowth=0f;
        float Depth(Collider side)
        {
            var body=Feet+Vector3.up*.52f;
            return Mathf.Max(0f,loc.bodyRadius-Vector3.Distance(body,side.ClosestPoint(body)));
        }
        var initial=sides.Select(Depth).ToArray();
        for(int i=0;i<52;i++) {
            await Sample(heading);
            for(int j=0;j<sides.Length;j++) maxGrowth=Mathf.Max(maxGrowth,Depth(sides[j])-initial[j]);
        }
        float along=Vector3.Dot(Feet-start,tangent);
        Check(along>.80f && maxGrowth<.002f && Mathf.Abs(Feet.y-start.y)<.01f,
            $"along={along:F4}m maxAddedOverlap={maxGrowth:F5}m initialDepth=[{string.Join(",",initial.Select(v=>v.ToString("F4")))}] end={Feet:F4}");
    }
    static Collider Near(string name,Vector3 center)
    {
        return UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
            .Single(c=>Hierarchy(c.transform).Contains(name) && Vector3.Distance(c.bounds.center,center)<.015f);
    }
    static async void RunRoutes()
    {
        try {
            var player=UnityEngine.Object.FindFirstObjectByType<LinearRpgController>();
            loc=player.locomotion;
            player.enabled=false; // Feed the original locomotion one sample per Play frame in place of untracked hardware.
            loc.head.position+=Vector3.up*(1.63f-(loc.head.position.y-Feet.y));
            stamp=Time.realtimeSinceStartupAsDouble;
            Time.captureFramerate=50;
            frames=new StreamWriter(Path.Combine(Output,"frames.tsv"));
            frames.WriteLine("route\tunityFrame\tbefore\tafter\tindex\tdirection\tstatus");
            var desk=Near("墨规画符石案",new Vector3(-7.921f,80.47f,58.85f));
            var rail=Near("祭坛区石木护栏",new Vector3(1.659f,80.58f,36.1f));
            var a=Near("A 岸接驳栈道",new Vector3(6.989f,.58f,17.95f));
            var b=Near("B 岸接驳栈道",new Vector3(64.161f,.58f,17.95f));
            var post=Near("B 岸接驳栈道/碰撞 1",new Vector3(78.571f,.58f,18.695f));
            float r=loc.bodyRadius;
            await Edge("desk",new Vector3(desk.bounds.min.x-r+.035f,80,59.03f),new Vector3(.2f,0,-1),Vector3.back,new[]{desk});
            await Edge("altar-tangent",new Vector3(rail.bounds.min.x-r,80,35),Vector3.forward,Vector3.forward,new[]{rail});
            await Edge("A-rail",new Vector3(6,0,a.bounds.max.z+r-.036f),new Vector3(-1,0,-.15f),Vector3.left,new[]{a});
            var corner=new Vector3(post.bounds.min.x-r+.032f,0,b.bounds.max.z+r-.032f);
            await Edge("B-post-release",corner,Vector3.forward,Vector3.forward,new[]{b,post});
            var farPost=Near("B 岸接驳栈道/碰撞 2",new Vector3(78.571f,.58f,22.105f));
            var farRail=Near("B 岸接驳栈道",new Vector3(64.161f,.58f,22.85f));
            var safeCorner=new Vector3(farPost.bounds.min.x-r,0,farRail.bounds.min.z-r);
            await Place("B-closed-corner",safeCorner+new Vector3(-.35f,0,-.30f));
            await Step(new Vector3(1,0,1),65);
            var stop=Feet;
            await Step(new Vector3(1,0,1),12);
            Check(Feet.x<=safeCorner.x+.001f && Feet.z<=safeCorner.z+.001f && (Feet-safeCorner).magnitude<.009f && (Feet-stop).magnitude<.001f,$"end={Feet:F4}");
            // Two walls of the same nonconvex collider, on the real A walkway floor.
            var shape=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var source=shape.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(shape);
            var mesh=new Mesh();
            var meshRoot=new GameObject("QA 单Mesh双墙");
            meshRoot.transform.position=new Vector3(8.5f,0,20);
            var pieces=new[] {
                new CombineInstance { mesh=source, transform=Matrix4x4.TRS(new Vector3(0,.65f,0),Quaternion.identity,new Vector3(3,1.3f,.02f)) },
                new CombineInstance { mesh=source, transform=Matrix4x4.TRS(new Vector3(.9f,.65f,.70f),Quaternion.identity,new Vector3(.02f,1.3f,1.42f)) }
            };
            mesh.CombineMeshes(pieces);
            meshRoot.AddComponent<MeshCollider>().sharedMesh=mesh;
            Physics.SyncTransforms();
            var meshStart=meshRoot.transform.position+Vector3.forward*(.01f+r-.034f);
            await Place("same-concave-mesh",meshStart);
            await Step(new Vector3(1,0,-.16f),65);
            var localEnd=Feet-meshRoot.transform.position;
            Check(localEnd.x>.63f && localEnd.x<=.9f-.01f-r+.002f && localEnd.z>=.01f+r-.035f,
                $"slide along first face, stop at second face of same mesh; local={localEnd:F5}");
            await Step(Vector3.left,25);
            Check(Feet.x<meshRoot.transform.position.x+.26f,"can exit same-mesh corner; end="+Feet.ToString("F4"));
            UnityEngine.Object.DestroyImmediate(meshRoot);
            UnityEngine.Object.DestroyImmediate(mesh);
            await Place("deep-overlap",new Vector3(6,0,a.bounds.max.z+r-.11f));
            var deep=Feet;await Step(Vector3.right,15);
            Check((Feet-deep).magnitude<.001f,$"travel={Vector3.Distance(Feet,deep):F4}");
            await Place("thin-wall",new Vector3(-9.12f,80,57.9f));
            await Step(Vector3.back,70);
            Check(Feet.z>57.225f && Feet.z<57.25f,$"8cm wall end={Feet:F4}");
            await Place("A-cliff",new Vector3(-7.1f,0,20.4f));
            await Step(Vector3.left,60);
            Check(Feet.x> -7.626f && Feet.x< -7.55f && Mathf.Abs(Feet.y)<.01f,$"end={Feet:F4}");
            await Place("free",new Vector3(3,0,20.4f));var origin=Feet;await Step(Vector3.right,50);
            Check(Mathf.Abs(Feet.x-origin.x-loc.exploreSpeed)<.002f,$"50 x 20ms={Feet.x-origin.x:F4}m speed={loc.exploreSpeed:F3}");
            var autoStart=new Vector3(63,0,b.bounds.max.z+r+.04f);
            async Task<Vector3> Select(string label)
            {
                await Place(label,autoStart,ExplorationMovement.AutoWalk);
                var goal=autoStart+Vector3.right*1.5f;
                for(int i=0;i<100 && Feet.x<autoStart.x+.015f;i++)
                {
                    var wrist=loc.head.position+new Vector3(-.22f,-.24f,.12f);
                    await Sample(goal-wrist);
                }
                Check(Feet.x>autoStart.x+.01f,"ground dwell selected; start="+Feet.ToString("F4"));
                return goal;
            }
            var selected=await Select("auto-graze");
            loc.rigRoot.position+=Vector3.back*.052f;Physics.SyncTransforms(); // Simulated lateral room-scale displacement during an already committed route.
            for(int i=0;i<95;i++) await Sample(Vector3.forward,false);
            Check(Vector3.Distance(Feet,selected)<.015f,$"distance to destination={Vector3.Distance(Feet,selected):F4}m end={Feet:F4}");
            selected=await Select("auto-block");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position=autoStart+new Vector3(.63f,.6f,0);
            blocker.transform.localScale=new Vector3(.025f,1.2f,2f);
            Physics.SyncTransforms();
            for(int i=0;i<35;i++) await Sample(Vector3.forward,false);
            Check(!loc.NavigationActive && Feet.x<autoStart.x+.41f,"new 25mm obstacle halted route at "+Feet.ToString("F4"));
            UnityEngine.Object.DestroyImmediate(blocker);
            route="legacy";int count=NarutoVR.SealCombat.Editor.EncounterMovementChecks.Run();
            Check(count>35,$"existing encounter movement checks={count}");
            report.Add($"assertions={assertions} failures={failures} XR auto=false loaders=0 runningDisplays=0 checked every input frame");
            Finish(failures>0?1:0);
        } catch(Exception e) { report.Add(e.ToString());Finish(2); }
    }
    static void Finish(int code)
    {
        frames?.Dispose();
        File.WriteAllLines(Path.Combine(Output,"results.txt"),report);
        EditorApplication.Exit(code);
    }
}