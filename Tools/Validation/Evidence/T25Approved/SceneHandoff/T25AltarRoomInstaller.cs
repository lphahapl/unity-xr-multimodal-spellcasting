using System;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>仅由正式场景负责人放入 Editor 目录后手动运行。对 A4 模型替换不增加运行时脚本。</summary>
public static class T25AltarRoomInstaller
{
    public const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
    const string Id = "stone-carrier";
    const string VolumeName = "祭坛工奴 · 接敌区域";

    [MenuItem("Tools/NarutoVR/第二章断衡悬道/接入已批准的祭坛工奴房间")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式执行");
        bool modified = Apply(SceneManager.GetSceneByPath(ScenePath));
        Debug.Log(modified ? "祭坛接线已修改，可审阅 Undo 并由场景负责人保存。" : "祭坛接线、范围和占位已就绪，无需改动。");
    }

    static void Need(bool ok,string message)
    { if(!ok) throw new InvalidOperationException("祭坛安装停止，未确认的接线须保留：" + message); }

    static bool Covers(BoxCollider box, Transform line)
    {
        bool Inside(Vector3 offset)
        {
            var p=box.transform.InverseTransformPoint(line.TransformPoint(offset))-box.center;
            var h=box.size*.5f;
            return Mathf.Abs(p.x)<=h.x && Mathf.Abs(p.y)<=h.y && Mathf.Abs(p.z)<=h.z;
        }
        // ②入口在局部 X=0，接敌线 X=9，战区边界 X=28，宽 22m。避免压成穿过即退出的一面薄墙。
        return box.enabled && box.isTrigger && box.gameObject.activeInHierarchy &&
            !Inside(new Vector3(-.5f,0,0)) && Inside(new Vector3(.15f,0,-10.7f)) &&
            Inside(new Vector3(.15f,0,10.7f)) && Inside(new Vector3(18.6f,0,-10.7f)) &&
            Inside(new Vector3(18.6f,0,10.7f));
    }

    public static bool Apply(Scene scene)
    {
        Need(scene.IsValid() && scene.isLoaded && scene.path==ScenePath,"请先打开二章正式场景");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var altar=all.Single(t=>t.name.StartsWith("02 · 祭坛战场"));
        var line=altar.Find("首次接敌线");
        var post=altar.Find("背石起始位")?.GetComponent<StoneCarrierPost>();
        var old=altar.Find("背石工奴 · 机体占位");
        Need(line && post && (!old || old.IsChildOf(altar)), "缺少祭坛接敌线或既有工奴出生接点");
        Need(Vector3.Distance(line.localPosition,new Vector3(9,0,11))<.1f &&
            Quaternion.Angle(line.localRotation,Quaternion.identity)<.1f && (line.lossyScale-Vector3.one).sqrMagnitude<.001f,
            "原接敌线的局部坐标、朝向或缩放已变更");
        var rpg=all.Select(t=>t.GetComponent<LinearRpgController>()).Single(c=>c && c.enabled);
        var manager=all.Select(t=>t.GetComponent<EnemyManager>()).Single(c=>c && c.isActiveAndEnabled);
        Need(rpg.roomControlsEncounter && rpg.locomotion && rpg.locomotion.head && manager.catalog &&
            manager.catalog.TryGet(Id,out var entry) && entry.stoneCarrier && entry.stoneCarrier.stone,
            "现有房间管理开关、玩家 head 或敌人目录不完整");
        Need(post.basket && post.basket.slot && post.basket.passengerVolume && post.basket.passengerVolume.isTrigger &&
            post.patrolPoints!=null && post.patrolPoints.Length>0 && post.patrolPoints.All(t=>t),
            "出生点缺少原篮、槽心、乘员盒或巡逻点");
        var allRooms=all.SelectMany(t=>t.GetComponents<CombatRoomEncounter>()).ToArray();
        var candidates=allRooms.Where(room=>room.transform.IsChildOf(altar) ||
            (room.area && (room.area.transform.IsChildOf(altar) ||
             room.area.bounds.Contains(post.transform.position+Vector3.up*.2f))) ||
            (room.spawnPoints??Array.Empty<CombatRoomEncounter.SpawnPoint>()).Any(s=>s!=null && s.point && s.point.IsChildOf(altar))).ToArray();
        Need(candidates.Length<=1,"有多个可能的祭坛房间");
        var room=candidates.SingleOrDefault();
        var existingNode=line.Find(VolumeName);
        var box=room ? room.area : null;
        if(existingNode)
        {
            var boxes=existingNode.GetComponents<BoxCollider>();
            Need(boxes.Length==1 && Covers(boxes[0],line) && (!box || box==boxes[0]),
                "原专用范围缺少唯一有效触发盒，或与房间的范围引用冲突");
            box=boxes[0];
        }
        if(room)
        {
            Need(room.isActiveAndEnabled && (!room.rpg || room.rpg==rpg) &&
                (!room.enemyManager || room.enemyManager==manager) &&
                (room.UsesEnemyManager || room.enemies==null || room.enemies.Length==0),
                "现有祭坛房间的启停、玩家、生成方式或服务与计划不一致");
            if(!box)
            {
                var own=room.GetComponents<BoxCollider>();
                Need(own.Length<=1,"房间存在多种可用范围");
                box=own.SingleOrDefault();
            }
            Need(!box || Covers(box,line),"现有祭坛范围不覆盖接敌线至 A 篮方向的可达地面");
        }
        var points=room ? room.spawnPoints??Array.Empty<CombatRoomEncounter.SpawnPoint>() : Array.Empty<CombatRoomEncounter.SpawnPoint>();
        var found=points.Where(s=>s!=null && s.point==post.transform).ToArray();
        Need(points.All(s=>s!=null && s.point && !string.IsNullOrWhiteSpace(s.enemyId)) &&
            !points.Any(s=>s.enemyId==Id && s.point!=post.transform) &&
            found.Length<=1 && found.All(s=>s.enemyId==Id && !s.isSummoned),"工奴点已作其他用途或存在重复记录");
        bool ready=room && room.rpg==rpg && room.enemyManager==manager && box && room.area==box && found.Length==1;
        if(ready && (!old || !old.gameObject.activeSelf)) return false;

        Undo.IncrementCurrentGroup();
        int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("安装二章祭坛工奴房间");
        try
        {
            if(!box)
            {
                var node=new GameObject(VolumeName);
                Undo.RegisterCreatedObjectUndo(node,"创建祭坛范围");
                node.transform.SetParent(line,false);
                box=Undo.AddComponent<BoxCollider>(node);
                box.isTrigger=true; box.center=new Vector3(9.5f,1,0); box.size=new Vector3(19,3,22);
            }
            if(!room) room=Undo.AddComponent<CombatRoomEncounter>(box.gameObject);
            Undo.RecordObject(room,"补齐工奴出生入口");
            room.rpg=rpg; room.area=box; room.enemyManager=manager;
            if(found.Length==0)
                room.spawnPoints=points.Concat(new[]{new CombatRoomEncounter.SpawnPoint{enemyId=Id,point=post.transform,isSummoned=false}}).ToArray();
            if(old && old.gameObject.activeSelf)
            {
                Undo.RecordObject(old.gameObject,"停用旧工奴及其旧石外观");
                old.gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(room);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            return true;
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
}
