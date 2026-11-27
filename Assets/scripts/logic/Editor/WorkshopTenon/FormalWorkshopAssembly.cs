using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class FormalWorkshopAssembly
    {
        const string Data = "Assets/ScriptableObjects/Chapter02Workshop";
        const string Entry = "CH02_REACH_HUB";
        public const string ChapterFile = "chapter02-suspended-path.json";
        static Transform Find(Scene scene, string label) => scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Single(t => t.name == label);

        static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent,false);
            point.localPosition = position;
            return point;
        }

        static RpgStageDefinition Stage(string id)
        {
            var path=Data+"/"+id+".asset";
            var result=AssetDatabase.LoadAssetAtPath<RpgStageDefinition>(path);
            if(!result && id==Entry)
            {
                result=ScriptableObject.CreateInstance<RpgStageDefinition>();
                result.stageId=id;
                result.displayName="抵达司衡台";
                result.instructions="从石门上层前往中层，在司衡台地坪落脚。";
                result.rewardCurrency=0;
                result.startsInCombat=false;
                AssetDatabase.CreateAsset(result,path);
            }
            if(!result || result.stageId!=id || result.rewardCurrency!=0 || result.unlockSkillIds?.Length>0)
                throw new InvalidOperationException("Invalid chapter stage "+id);
            return result;
        }

        static NavigationNode Checkpoint(Transform parent,string id,Vector3 location)
        {
            var node=Point(parent,id,Vector3.zero).gameObject.AddComponent<NavigationNode>();
            node.transform.position=location;
            node.nodeId=id;
            return node;
        }

        // The calling editor owns opening, saving and backing up the formal scene.
        public static void Apply(Scene scene)
        {
            if(Application.isPlaying || scene.path!=WorkshopTenonAssembly.SourceScene)
                throw new InvalidOperationException("Open the formal Chapter02 scene in edit mode.");
            var stand=Find(scene,"主榫与承座").GetComponent<MeshFilter>();
            var peg=WorkshopWhiteboxTenon.FromStand(stand);
            var stages=new[]{Stage(Entry),Stage("CH02_WORKSHOP_TENON"),Stage("CH02_B_DESCENT")};
            var entrance=Find(scene,"入口 · XR 观察位");
            var birth=entrance.position;
            if(Vector3.Distance(birth,new Vector3(-9.121365f,80f,63.7f))>.03f)
                throw new InvalidOperationException("Entrance origin moved "+birth);
            var p=WorkshopTenonAssembly.CreateChapterPlayer(scene,out var flow,out var manager);
            var previous=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<WorkshopTenonObjective>(true)).SingleOrDefault();
            if(previous)
            {
                var same=WorkshopTenonAssembly.BindWorkshop(scene,p,manager,stages[1],peg,previous.holder);
                if(same!=previous || p.saveFileName!=ChapterFile || flow.stages.Length!=3 ||
                    !stages.SequenceEqual(flow.stages.Select(s=>s.stage)) || Vector3.Distance(p.CheckpointAt(0).floorPosition,birth)>.01f)
                    throw new InvalidOperationException("Existing assembly differs from the formal workshop route.");
                return;
            }
            p.saveFileName=ChapterFile;
            var obj=WorkshopTenonAssembly.BindWorkshop(scene,p,manager,stages[1],peg);
            if(obj.closedJaws.transform.childCount!=0 || obj.openJaws.transform.childCount!=0)
                throw new InvalidOperationException("The production setup must only contain functional state markers.");
            var hubFloor=Find(scene,"换篮地坪 · 双接驳缺口").GetComponents<BoxCollider>()
                .OrderByDescending(c=>c.bounds.size.x*c.bounds.size.z).First().bounds;
            var arrive=Point(p.transform,"④ 司衡台地坪到达区域",Vector3.zero).gameObject;
            arrive.transform.position=new Vector3(hubFloor.center.x,hubFloor.max.y+.05f,hubFloor.center.z);
            var region=arrive.AddComponent<BoxCollider>();
            region.isTrigger=true;region.size=new Vector3(hubFloor.size.x-.70f,.70f,hubFloor.size.z-.70f);
            var transit=arrive.AddComponent<ReachAreaChapterObjective>();transit.area=region;
            var waiting=p.gameObject.AddComponent<SignalChapterObjective>();
            var start=Checkpoint(p.transform,"CH02_ENTRANCE",birth);
            var hub=Checkpoint(p.transform,"CH02_HUB",Find(scene,"中控操作位").position);
            flow.stages=new[]{
                new ChapterObjectiveStage{stage=stages[0],objectives=new ChapterObjective[]{transit}},
                new ChapterObjectiveStage{stage=stages[1],objectives=new ChapterObjective[]{obj}},
                new ChapterObjectiveStage{stage=stages[2],objectives=new ChapterObjective[]{waiting}}
            };
            flow.finalCheckpoint=hub;
            p.ConfigureRoute(stages,new[]{start,hub,hub});
            p.locomotion.rigRoot.SetPositionAndRotation(birth,Quaternion.Euler(0,entrance.eulerAngles.y,0));
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
