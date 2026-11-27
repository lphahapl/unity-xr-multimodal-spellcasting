using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>独立探针共享的只读检查。统计同一批守敌与镇石的身份，保留场景的原 XR 玩家和两只手。</summary>
internal sealed class Chapter02AltarPreviewWitness
{
    static readonly string[] EnemyIds={"stone-carrier","canjiou","zhihuocanji"};
    const string RigGuid="f9f107e8ecae49048bda9d4fd7e1d1ec";
    const string LeftGuid="67c4027c76c769346ab6711c6a38d07a";
    const string RightGuid="7c1640bff2820dd4ebbbc9aca5d3322d";
    readonly CombatRoomEncounter room;
    readonly StoneCarrierPost post;
    readonly EnemyLease[] leases;
    readonly BallastStone stone;
    readonly Rigidbody body;
    readonly int created, reused;

    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static bool HistoricalScene(string path)
    {
        using var sha=SHA256.Create();
        using var file=File.OpenRead(path);
        var hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
        return hash=="A5842A97DADEED8482750C2A95E8B810D39F42848F2BC7212E24A94952A4F13C" ||
               hash=="E7AEB66BBE4F23BD8FF8B209DB005A47565C2E4736F6C92625302145FE785CC6";
    }
    internal static CombatRoomEncounter ReadScene(Scene scene,out bool patched)
    {
        var post=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StoneCarrierPost>(true)).Single();
        var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CombatRoomEncounter>(true))
            .Single(r=>r.spawnPoints.Any(s=>s!=null && s.point==post.transform));
        patched=false;
        if(room.spawnPoints.Length!=3)
        {
            Require(HistoricalScene(scene.path),"正式祭坛缺少三人登记，验收不会补写场景");
            patched=Chapter02AltarTrioInstaller.Apply(scene);
            Require(!Chapter02AltarTrioInstaller.Apply(scene),"历史快照重复安装改写场景");
        }
        Require(room.isActiveAndEnabled && room.rpg && room.area && room.enemyManager &&
            room.spawnPoints.Length==3 && EnemyIds.All(id=>room.spawnPoints.Count(s=>s!=null && s.enemyId==id && s.point && !s.isSummoned)==1) &&
            room.spawnPoints.Single(s=>s.enemyId=="stone-carrier").point==post.transform,"正式三敌登记或原背石接点错误");
        if(!patched)
            Require(room.showEnemiesBeforeEngagement && Mathf.Approximately(room.previewDistance,32f),"正式祭坛预展示开关/距离错误");
        var rig=new Rig(room.rpg);
        Require(SourceGuid(rig.rig)==RigGuid && SourceGuid(rig.left)==LeftGuid && SourceGuid(rig.right)==RightGuid,
            "原玩家 Rig 或左右手实例与 Meta SDK 来源不同");
        return room;
    }
    static string SourceGuid(Transform root)
    {
        var source=PrefabUtility.GetCorrespondingObjectFromSource(root.gameObject);
        return source?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)):"";
    }

    internal sealed class Rig
    {
        internal readonly Transform rig,left,right;
        readonly Transform head;
        readonly LinearRpgController player;
        readonly SealXRHandsSource source;
        readonly SkinnedMeshRenderer leftMesh,rightMesh;
        readonly Mesh leftAsset,rightAsset;
        readonly Component leftInput,rightInput;
        public Rig(LinearRpgController player)
        {
            this.player=player;rig=player.locomotion.rigRoot;head=player.locomotion.head;
            source=rig.GetComponentsInChildren<SealXRHandsSource>(true).Single();
            left=source.transform.Find("UnityXRHandVisualLeft");
            right=source.transform.Find("UnityXRHandVisualRight");
            Require(left && right && source.IsConfigured,"场景 XR 手来源或双手外观不全");
            leftInput=source.leftHand;rightInput=source.rightHand;
            leftMesh=left.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            rightMesh=right.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            leftAsset=leftMesh.sharedMesh;rightAsset=rightMesh.sharedMesh;
            Check();
        }
        internal void Check()
        {
            Require(player.locomotion.rigRoot==rig && player.locomotion.head==head && head.IsChildOf(rig) &&
                source && source.IsConfigured && source.leftHand==leftInput && source.rightHand==rightInput &&
                left && right && left.parent==source.transform && right.parent==source.transform &&
                leftMesh && rightMesh && leftAsset && rightAsset &&
                leftMesh.sharedMesh==leftAsset && rightMesh.sharedMesh==rightAsset,"场景玩家、追踪手或官方双手网格发生替换");
        }
        internal string Ids => $"原玩家={player.GetInstanceID()} rig={rig.GetInstanceID()} head={head.GetInstanceID()} 左/右手={left.GetInstanceID()}/{right.GetInstanceID()} 左/右网格={leftAsset.GetInstanceID()}/{rightAsset.GetInstanceID()}";
    }

    internal Chapter02AltarPreviewWitness(CombatRoomEncounter room,StoneCarrierPost post)
    {
        this.room=room;this.post=post;
        Require(room.showEnemiesBeforeEngagement && !room.PlayerInside && !room.HasStarted && !room.IsComplete &&
            room.enemies.Length==3 && !post.basket.Boarded,"线外守敌预展示状态不符");
        leases=EnemyIds.Select(id=>room.enemyManager.ActiveEnemies.Values.Single(l=>l.EnemyId==id && room.enemies.Contains(l.Controller))).ToArray();
        Require(leases.All(l=>room.enemyManager.IsCurrent(l) && !l.Defeated && !l.Controller.CanReceiveHit && l.Controller.CurrentAttack<0 &&
            l.Controller.GetComponentsInChildren<Renderer>().Any(r=>r.enabled && (r is SkinnedMeshRenderer skinned && skinned.sharedMesh ||
            r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh))),"预展示期间守敌不可见、可受击或正在攻击");
        stone=post.basket.Stone;
        Require(stone && stone==leases[0].Controller.GetComponent<StoneCarrier>().Stone && stone.State==BallastStone.Phase.Carried,"预展示未绑定原持石工奴");
        body=stone.GetComponent<Rigidbody>();Require(body,"预展示镇石缺少刚体");
        created=room.enemyManager.CreatedCount;reused=room.enemyManager.ReusedCount;
    }
    internal void CheckEngaged()
    {
        Require(room.PlayerInside && room.HasStarted && !room.IsComplete && leases.All(l=>room.enemyManager.IsCurrent(l) &&
            room.enemies.Contains(l.Controller) && l.Controller.CanReceiveHit) && room.enemyManager.CreatedCount==created &&
            room.enemyManager.ReusedCount==reused && post.basket.Stone==stone && stone.GetComponent<Rigidbody>()==body &&
            leases[0].Controller.GetComponent<StoneCarrier>().Stone==stone,"过线重新生成敌人、替换镇石或未全员激活");
    }
    internal string Ids => $"租约/控制器={string.Join(",",leases.Select(l=>l.EnemyId+"="+l.Id+"/"+l.Controller.GetInstanceID()))} 石/刚体={stone.GetInstanceID()}/{body.GetInstanceID()} 新建={created} 取池={reused}";
}
