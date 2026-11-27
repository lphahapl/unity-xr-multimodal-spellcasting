using System;
using System.Collections;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class R3WorkshopPlay
    {
        const string Key="Ch02Tenon.R3.Play";
        static string Report=>Path.GetFullPath("R3-tenon-play.txt");
        static IEnumerator run;
        static double began;
        static R3WorkshopPlay() { EditorApplication.update+=Poll; }
        public static void Run()
        {
            EditorSceneManager.OpenScene(R3WorkshopBuild.ScenePath);
            var player=UnityEngine.Object.FindFirstObjectByType<LinearRpgController>();
            player.skills.voice.autoStartInEditor=false; player.autoLoad=false; player.saveFileName="tenon-qa-"+Guid.NewGuid().ToString("N")+".json";
            if(XRGeneralSettings.Instance) XRGeneralSettings.Instance.InitManagerOnStart=false;
            SessionState.SetBool(Key,true);
            File.WriteAllText(Report,"Unity "+Application.unityVersion+" / PlayMode\n");
            EditorApplication.EnterPlaymode();
        }
        static void Poll()
        {
            if(!SessionState.GetBool(Key,false)|| !EditorApplication.isPlaying || EditorApplication.isCompiling || !Application.isPlaying) return;
            try {
                if(run==null) {began=EditorApplication.timeSinceStartup; run=Checks();}
                if(EditorApplication.timeSinceStartup-began>105) throw new TimeoutException(Status());
                if(!run.MoveNext()) Stop(0);
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n"+Status()+"\n");Stop(1);}
        }
        static string Status() {
            var p=UnityEngine.Object.FindFirstObjectByType<LinearRpgController>();
            var o=UnityEngine.Object.FindFirstObjectByType<WorkshopTenonObjective>();
            return p? $"{p.Status}; nav={p.locomotion.Status}; ready={p.IsReady}; hand={p.handTracking.TrackingSource}; phase={p.StageIndex}; run={o.IsRunning}; roof={o.roof.Status}; ground={o.courtyard.Status}; seat={o.socket.LastFailure}":"no player";
        }
        static void Stop(int code){SessionState.EraseBool(Key);run=null; EditorApplication.Exit(code);}
        static void Check(bool value,string message){ if(!value) throw new Exception(message); File.AppendAllText(Report,"PASS "+message+"\n"); }
        static IEnumerator Checks()
        {
            yield return null;
            var obj=UnityEngine.Object.FindFirstObjectByType<WorkshopTenonObjective>();
            var p=obj.player; var floor=p.locomotion; var flow=(ObjectiveChapterFlow)p.chapter;
            var room=obj.courtyard; var roof=obj.roof; var item=obj.tenon; var socket=obj.socket;
            int commits=0; obj.Seated+=()=>commits++;
            while (!p.CanAct || !obj.IsRunning) yield return null;
            Check(p.IsReady && p.chapter==flow && p.handTracking.xrHandsSource && p.skills.Engine!=null &&
                p.locomotion.head.IsChildOf(floor.rigRoot), "独立 RPG、ObjectiveChapterFlow、双手 XR 来源和真实技能匹配器已就绪");
            Check(!room.HasStarted && !roof.HasStarted && !obj.IsUnlocked && obj.closedJaws.activeSelf && !item.enabled, "④初抵、空房和未启动敌人不能开扣");
            void Move(Vector3 position){floor.rigRoot.position+=position-floor.PlayerFloorPosition;Physics.SyncTransforms();roof.TickEncounter();room.TickEncounter();}
            var yard=room.spawnPoints.Single().point.position;
            var hub=p.CheckpointAt(0).floorPosition;
            Check(obj.consoleFault.activeSelf && !obj.consoleReady.activeSelf,"中控初始朱砂红");
            Check(new[]{obj.closedJaws,obj.openJaws}.All(g=>g.GetComponentsInChildren<Collider>(true).Length==0 && g.GetComponentsInChildren<Renderer>(true).Length==0) && item.GetComponent<MeshRenderer>().sharedMaterial.name=="WorkshopLimeInlay","生产接线采用原白盒材质，不创建待审双销");
            var stand=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Single(m=>m.name=="主榫与承座");
            Check(stand.sharedMesh.subMeshCount==5 && stand.sharedMesh.GetIndexCount(3)==0 && stand.sharedMesh.GetIndexCount(4)>0 && Mathf.Abs(obj.holder.position.y-.653f)<.001f &&
                  Mathf.Abs(stand.GetComponents<BoxCollider>().Max(c=>c.bounds.max.y)-.65f)<.001f,"承座已移除焊死假榫，实体铜托顶面 0.650 m，主榫底 0.653 m");
            
            Move(yard); Move(hub);
            Check(!room.IsComplete && !roof.IsComplete && !obj.IsUnlocked,"触发入区随即退出或取消生成时不集合作清场");
            yield return null;
            Move(yard);
            while(!room.HasStarted || !roof.HasStarted) yield return null;
            Check(roof.enemies.Length==2 && room.UsesEnemyManager && room.enemies.Length==1 &&
                  room.enemies.All(e=>e.target==floor.head) && roof.enemies.All(e=>e.target==floor.head),"F1/F2 预置双射手和真实精英租约同时取得玩家头部");
            Check(roof.enemies.All(e=>e.Agent && e.Agent.isOnNavMesh && e.Agent.areaMask!=(1 << UnityEngine.AI.NavMesh.GetAreaFromName("Walkable"))), "现版两名屋顶射手分别驻在有效屋面导航");
            var groundFilter=new UnityEngine.AI.NavMeshQueryFilter{ agentTypeID=0,areaMask=1<<UnityEngine.AI.NavMesh.GetAreaFromName("Walkable") };
            var route=new UnityEngine.AI.NavMeshPath();
            Check(UnityEngine.AI.NavMesh.SamplePosition(yard,out var start,.4f,groundFilter) &&
                UnityEngine.AI.NavMesh.SamplePosition(new Vector3(27.125f,0,47.202f),out var end,.4f,groundFilter) &&
                UnityEngine.AI.NavMesh.CalculatePath(start.position,end.position,groundFilter,route) &&
                route.status==UnityEngine.AI.NavMeshPathStatus.PathComplete &&
                room.enemies.Single().Agent.isOnNavMesh, "现版院场精英落在地面导航且能走完整路线");
            void Kill(EnemyController enemy){if(!enemy.Entity.ReceiveHit("tenon-qa-"+Guid.NewGuid().ToString("N"),1000,0)) throw new Exception("击败失败 "+enemy.name);}
            Kill(roof.enemies[0]); roof.TickEncounter();
            Move(hub); Check(!roof.PlayerInside && !room.PlayerInside && !obj.IsUnlocked && !item.enabled,"离房、只击败一名屋顶射手均不会解锁");
            Move(yard); Kill(roof.enemies[1]); roof.TickEncounter();
            Check(roof.IsComplete && !room.IsComplete && !obj.IsUnlocked && !item.enabled,"屋顶组单独清完仍然锁住");
            Kill(room.enemies.Single()); room.TickEncounter();
            while(!obj.IsUnlocked) yield return null;
            Check(room.IsComplete && !obj.closedJaws.activeSelf && obj.openJaws.activeSelf && p.StageIndex==0,"两组首次清场才开放拾取，阶段未提前提交");
            
            void Hand(bool down,Vector3 tip,Vector3 grip,Quaternion rot){
                var now=Time.realtimeSinceStartupAsDouble;
                obj.Step(new PlayerHandObservation{rightValid=true,rightObserved=now,pinchValid=true,systemPinch=true,systemPressed=down,
                    rightTip=tip,gripPoseValid=true,gripPosition=grip,gripRotation=rot}, now);
            }
            void Take(){
                var pt=item.GripPosition; var rot=item.transform.rotation;
                Hand(false,pt,pt,rot); Hand(true,pt,pt,rot);
                if(!item.IsHeld) throw new Exception("真实右手握持入口未接管 "+p.Input.MovementBlockReason);
            }
            void Place(Vector3 pivot,Quaternion rot){
                var local=Quaternion.Inverse(item.transform.rotation)*(item.GripPosition-item.transform.position);
                var pt=pivot+rot*local; Hand(true,pt,pt,rot);Hand(false,pt,pt,rot);
            }
            Take(); Check(item.IsHeld && p.Input.CanMove(false,out _) && !p.Input.CanMove(true,out _),"右手持榫期间左手导航可用，持物闪避按玩家既有规则处理");
            Check(!socket.TrySnapObject(item.gameObject),"握持时脚本请求吸附被拒");
            Check(p.RestoreCheckpoint(),"持榫未插时恢复至工坊目标准备点");
            yield return null; yield return null;
            Check(!obj.IsUnlocked && !item.IsHeld && !room.IsComplete && !roof.IsComplete &&
                obj.closedJaws.activeSelf && p.StageIndex==0,"未提交恢复重新锁住拾取，清除手占用和上轮敌人状态");
            Move(yard); while(!room.HasStarted || !roof.HasStarted) yield return null;
            Kill(room.enemies.Single()); room.TickEncounter();
            Check(!obj.IsUnlocked,"精英先死、屋顶仍有守机时不能取榫");
            foreach(var e in roof.enemies) Kill(e); roof.TickEncounter();
            Take();
            Move(hub); Check(obj.IsUnlocked,"持榫离开工坊保持取用权限");
            Place(obj.holder.position+Vector3.down*4,Quaternion.identity);
            yield return null; yield return null;
            Check(!item.IsHeld && Vector3.Distance(item.transform.position,obj.holder.position)<.01f,"失手坠崖回同一承座，未复制道具");
            var mouth=socket.mouth; var above=mouth.position+mouth.up*.045f;
            Take(); Place(above,mouth.rotation*Quaternion.Euler(0,45,0));
            Check(!socket.SnappedObject && !obj.IsSeated && p.StageIndex==0,"45° 错面松手未插紧");
            Take(); Place(above+mouth.right*.04f,mouth.rotation);
            Check(!socket.SnappedObject,"横向偏移 4 cm 被拒");
            Check(!socket.TrySnapObject(obj.holder.gameObject),"错误物件被拒");
            Take(); Place(above,mouth.rotation); Check(socket.IsSnapping,"正确方向可开始吸附");
            socket.ReleaseObject();
            Check(!obj.IsSeated && !socket.SnappedObject && p.StageIndex==0,"吸附中途取消不会提交或点亮");
            Take(); Place(above,mouth.rotation*Quaternion.Euler(0,90,0));
            Check(socket.IsSnapping && !obj.IsComplete && p.StageIndex==0,"正确四分之一转进入 SnapZone，过渡中仍未提交");
            while(!obj.IsSeated || p.StageIndex==0) yield return null;
            Check(commits==1 && p.StageIndex==1 && p.Model.Chapter.IsComplete(obj.stage.stageId) &&
                Vector3.Distance(item.transform.position,socket.bottom.position)<.001f && !obj.IsRunning,
                "插到底后由 ObjectiveChapterFlow 提交一次并进入 B 篮阶段");
            Check(!obj.consoleFault.activeSelf && obj.consoleReady.activeSelf && obj.poweredCircuits.Length==2 && obj.poweredCircuits.All(c=>c.activeSelf) &&
                Mathf.Abs(Vector3.Distance(socket.mouth.position,socket.bottom.position)-.10406f)<.001f,
                "朱砂退、冰蓝亮、两侧分控通电，真实槽坐入深度约 104 mm");
            Check(!socket.TrySnapObject(item.gameObject) && !item.enabled,"坐实后不能反复结算或重新抓取");
            Check(p.RestoreCheckpoint(),"从二章独立检查点读取成功");
            yield return null; yield return null;
            Check(obj.IsSeated && !roof.enabled && !room.enabled && !item.enabled &&
                Vector3.Distance(item.transform.position,socket.bottom.position)<.001f && p.StageIndex==1 && commits==1,"完成后读档保留已就座与下一阶段，不重生已清的工坊");
            File.AppendAllText(Report,"PASS END\n");
        }
    }
}
