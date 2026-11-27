// Copy into the isolated project's game Editor assembly. Run BackCarryGreyboxSmoke.Run in batch mode.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BackCarryGreyboxSmoke
{
    const string Scene = "Assets/Scene/StoneCarrierGreybox.unity";
    const string Key = "BackCarryGreyboxSmoke.R02";
    static string Folder => Environment.GetEnvironmentVariable("BACKCARRY_GREYBOX_REPORT");
    static void Record(string s) => File.AppendAllText(Path.Combine(Folder,"greybox-play.txt"),s+"\n");
    static void Check(bool ok,string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Record("PASS "+message);
    }
    static BackCarryGreyboxSmoke() => EditorApplication.update += Pending;
    public static void Run()
    {
        if (!Application.isBatchMode || string.IsNullOrEmpty(Folder)) throw new InvalidOperationException("Isolated run required");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder,"greybox-play.txt"),Scene+"\n"+DateTimeOffset.Now.ToString("o")+"\n");
        EditorSceneManager.OpenScene(Scene); SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static async Task Until(Func<bool> ready,string title)
    {
        double end=EditorApplication.timeSinceStartup+35;
        while (!ready()) {if (EditorApplication.timeSinceStartup>end) throw new TimeoutException(title);await Task.Yield();}
    }
    static async void Pending()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling) return;
        SessionState.EraseBool(Key);
        try
        {
            var lab=Object.FindFirstObjectByType<StoneCarrierGreybox>();
            Check(lab && SceneManager.GetActiveScene().path==Scene,"opened the actual StoneCarrierGreybox");
            await Until(()=>lab.Lease!=null && lab.manager.IsCurrent(lab.Lease),"automatic Start spawn: "+lab.Status);
            var c=lab.Lease.Controller;
            var carrier=c.GetComponent<StoneCarrier>();
            var stone=carrier.Stone;
            Check(stone && lab.post.basket.Stone==stone && stone.State==BallastStone.Phase.Carried,"Start generated stone-carrier and bound its real A4");
            var anim=c.GetComponentsInChildren<Animator>().Single(a=>a.isHuman && a.isActiveAndEnabled);
            await Until(()=>anim.GetCurrentAnimatorClipInfo(0).Any(i=>i.clip.name=="CanJiOu_BackCarryWalk"),"new clip");
            Record("clip="+anim.GetCurrentAnimatorClipInfo(0)[0].clip.name+" controller="+anim.runtimeAnimatorController.name+
                   " patrol="+carrier.profile.patrolSpeed+" retreat="+carrier.profile.retreatSpeed);
            var head=anim.GetBoneTransform(HumanBodyBones.Head);
            var hips=anim.GetBoneTransform(HumanBodyBones.Hips);
            var start=c.transform.position;
            await Until(()=>Vector3.Distance(c.transform.position,start)>.40f && carrier.TryGetCarryPhase(out _),"walk .4 m");
            Vector3 hp=c.transform.InverseTransformPoint(hips.position), hd=c.transform.InverseTransformPoint(head.position);
            Record("patrol displacement="+Vector3.Distance(c.transform.position,start)+" hips="+hp.ToString("F4")+" head="+hd.ToString("F4")+" stone="+c.transform.InverseTransformPoint(stone.transform.position).ToString("F4"));
            Check(hp.y<.88f && hd.y<1.36f && hd.z>hp.z && c.transform.InverseTransformPoint(stone.transform.position).z<hp.z,"low hips, lowered head, forward torso and stone behind during normal patrol");
            var retreatAt=c.transform.position;
            Check(lab.Strike(false) && carrier.Retreating && stone.State==BallastStone.Phase.Carried,"standard nonlethal greybox button starts retreat while carrying the original stone");
            await Until(()=>Vector3.Distance(c.transform.position,retreatAt)>.28f,"visible retreat");
            Check(!lab.Lease.Defeated,"alive after retreat");
            var beforePosition=stone.transform.position;
            var beforeRotation=stone.transform.rotation;
            var stoneId=stone.GetInstanceID();
            Check(lab.Strike(true) && lab.Lease.Defeated,"standard lethal button kills the lease");
            float delta=Vector3.Distance(beforePosition,stone.transform.position),angle=Quaternion.Angle(beforeRotation,stone.transform.rotation);
            Record("drop instant delta metres="+delta+" deg="+angle+" original stone ID="+stoneId);
            Check(stone.State==BallastStone.Phase.Dropped && !stone.transform.parent && stone.GetComponent<Collider>().enabled &&
                  delta<=.001f && angle<=.25f && lab.post.basket.Stone==stone && stone.GetInstanceID()==stoneId,
                  "same stone drops directly from its last carry transform with enabled physics");
            Record("GREYBOX_R02_PASS"); EditorApplication.Exit(0);
        }
        catch(Exception e){ Record("FAIL "+e);EditorApplication.Exit(1); }
    }
}
