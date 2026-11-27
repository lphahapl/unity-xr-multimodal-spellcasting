using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class R3WorkshopBuild
    {
        public const string ScenePath = "Assets/Scene/Chapter02_R3_WorkshopGameplay_QA.unity";
        public const string SourceSha = "1b4a4e9d8326d6c9372474dc78f807c625dbe8b0e7e7dd0d202ce88d42761a38";
        public static void RunAndPlay()
        {
            Run();
            R3WorkshopPlay.Run();
        }
        public static void Run()
        {
            var input=WorkshopTenonAssembly.SourceScene;
            using var sha=SHA256.Create();
            string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(input))).Replace("-","").ToLowerInvariant();
            if(hash!=SourceSha) throw new InvalidOperationException("正式源已更新，请重新对齐 "+hash);
            var scene=EditorSceneManager.OpenScene(input);
            if(!EditorSceneManager.SaveScene(scene,ScenePath,true)) throw new IOException(ScenePath);
            scene=EditorSceneManager.OpenScene(ScenePath);
            Transform Named(string name) => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name==name);
            var stand=Named("主榫与承座").GetComponent<MeshFilter>();
            var prop=WorkshopWhiteboxTenon.FromStand(stand);
            var player=WorkshopTenonAssembly.CreateChapterPlayer(scene,out var flow,out var enemies);
            var stage=AssetDatabase.LoadAssetAtPath<RpgStageDefinition>("Assets/ScriptableObjects/Chapter02Workshop/CH02_WORKSHOP_TENON.asset");
            var after=AssetDatabase.LoadAssetAtPath<RpgStageDefinition>("Assets/ScriptableObjects/Chapter02Workshop/CH02_B_DESCENT.asset");
            var objective=WorkshopTenonAssembly.BindWorkshop(scene,player,enemies,stage,prop);
            var checkpoint=Named("中控操作位").gameObject.AddComponent<NavigationNode>();
            checkpoint.nodeId="R3_QA_HUB";
            flow.stages=new[]{new ChapterObjectiveStage{stage=stage,objectives=new ChapterObjective[]{objective}},
                new ChapterObjectiveStage{stage=after,objectives=new ChapterObjective[]{player.gameObject.AddComponent<SignalChapterObjective>()}}};
            player.ConfigureRoute(new[]{stage,after},new[]{checkpoint,checkpoint});
            player.locomotion.rigRoot.position=checkpoint.floorPosition;
            flow.finalCheckpoint=checkpoint;
            player.saveFileName="chapter02-r3-workshop-qa.json";
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException(ScenePath);
            scene=EditorSceneManager.OpenScene(ScenePath);
            var p=WorkshopTenonAssembly.CreateChapterPlayer(scene,out _,out var manager);
            var o=WorkshopTenonAssembly.BindWorkshop(scene,p,manager,stage,prop);
            if(scene.isDirty || !o.closedJaws || !o.openJaws || o.closedJaws.transform.childCount!=0 ||
               o.openJaws.transform.childCount!=0 || o.roof.enemies.Length!=2 || o.courtyard.spawnPoints.Length!=1)
                throw new InvalidOperationException("冷读装配失败");
            if(AssetDatabase.GetDependencies(ScenePath,true).Any(a=>a.StartsWith("Assets/Review/WorkshopMasterTenon")||a.Contains("MasterTenon_R0")))
                throw new InvalidOperationException("检测到待审主榫外观引用");
            File.WriteAllText("R3-cold.txt","Source SHA256 "+hash+"\nR3 cold reopen: 1 RPG, 2 room branches; original placeholder mesh/material, zero review latch objects or R01-R03 prop dependencies; idempotent.\n");
        }
    }
}
