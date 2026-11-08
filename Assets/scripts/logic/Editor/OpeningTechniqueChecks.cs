using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class OpeningTechniqueChecks
    {
        static void Require(bool passed, string reason)
        {
            if (!passed) throw new InvalidOperationException("首章铜叶成长检查失败：" + reason);
        }

        public static void Run()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<OpeningTechniqueProgression>("Assets/ScriptableObjects/铜叶成长配置/首章铜叶成长.asset");
            Require(cfg && cfg.leaves.Length > 0, "配置未导入");
            var leaf = cfg.leaves.Single(l => l.leafId == "leaf.fangyuan");
            var skills = new[] { "fireball", "buff.zi", "buff.voice", "buff.hidden" };
            cfg.ValidateSetup(skills, new[] {leaf.collectedStage, leaf.seatedStage, cfg.calibrationStage}, leaf.leafId);
            Require(cfg.basicCourses.Length == 2 && cfg.basicCourses.Any(c => c.skillIds.SequenceEqual(new[] {"buff.zi", "buff.voice"})), "基础护盾的两种方式应合并为一课");
            var p = new RpgProgressData {checkpointId = "opening"};
            Require(cfg.State(p,"fireball") == TechniqueLearningState.Basic && cfg.State(p,"buff.hidden") == TechniqueLearningState.Locked, "新档可读课程");
            Require(!cfg.Available(p,"fireball",false) && cfg.Available(p,"fireball",true) && !cfg.Available(p,"buff.hidden",true), "试炼借用权限");
            Require(!cfg.Record(p,leaf.leafId,true,out _) && !cfg.Record(p,"wrong",false,out _), "未取得或未知实物");
            Require(cfg.Record(p,leaf.leafId,false,out bool changed) && changed && cfg.Record(p,leaf.leafId,false,out changed) && !changed, "拾取幂等");
            Require(cfg.State(p,"buff.hidden") == TechniqueLearningState.LeafCollected && !cfg.Available(p,"buff.hidden",true), "拾取后能学、尚不能施放");
            string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"opening-copper-"+Guid.NewGuid().ToString("N"))).FullName;
            string path = Path.Combine(dir,"player.json");
            try
            {
                var store = new RpgSaveStore(path,"opening.test",skills);
                Require(store.TrySave(p,out string reason) && store.TryLoad(out _,out reason), "拾取即存档：" + reason);
                Require(store.TryLoad(out var loaded,out reason) && cfg.Restore(loaded) && cfg.State(loaded,"buff.hidden") == TechniqueLearningState.LeafCollected, "手持进度重载");
                Require(cfg.Record(loaded,leaf.leafId,true,out changed) && changed && cfg.Record(loaded,leaf.leafId,true,out changed) && !changed, "入匣幂等");
                Require(cfg.State(loaded,"buff.hidden") == TechniqueLearningState.LeafRegistered && loaded.unlockedSkillIds.SequenceEqual(new[] {"buff.hidden"}) &&
                    !cfg.Available(loaded,"buff.hidden",false), "登记资格与安全区实际许可");
                using (new FileStream(path + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    Require(!store.TrySave(loaded,out _) && store.TryLoad(out var oldCopy,out _) &&
                        cfg.State(oldCopy,"buff.hidden") == TechniqueLearningState.LeafCollected, "写盘失败必须保留之前的取得进度");
                }
                Require(store.TrySave(loaded,out reason) && store.TryLoad(out loaded,out reason) && cfg.Restore(loaded) && cfg.State(loaded,"buff.hidden") == TechniqueLearningState.LeafRegistered, "入匣状态重载");
                loaded.completedStageIds.Add(cfg.calibrationStage.stageId);
                Require(cfg.Available(loaded,"buff.hidden",false), "校验后可施放");
                loaded.copperLeaves.Add(new RpgCopperLeafData {leafId=leaf.leafId});
                Require(!store.Validate(loaded,out _), "重复的持有记录须拒绝");
                foreach (var last in new[]{0,1,2})
                {
                    var old = new RpgProgressData {checkpointId="opening"};
                    if (last >= 1) old.completedStageIds.Add(leaf.collectedStage.stageId);
                    if (last >= 2) old.completedStageIds.Add(leaf.seatedStage.stageId);
                    // Use a real v1 payload which predates the ownership field.
                    string legacy = JsonUtility.ToJson(new SaveEnvelope {profileId="opening.test",progress=old}).Replace("\"copperLeaves\":[],","");
                    Require(!legacy.Contains("copperLeaves"), "旧存档构造");
                    File.WriteAllText(path,legacy);
                    Require(store.TryLoad(out var migrated,out reason) && cfg.Restore(migrated), "旧档迁移：" + reason);
                    var wanted = last==0 ? TechniqueLearningState.Locked : last==1 ? TechniqueLearningState.LeafCollected : TechniqueLearningState.LeafRegistered;
                    Require(cfg.State(migrated,"buff.hidden")==wanted && cfg.Restore(migrated) && migrated.copperLeaves.Count==(last==0 ? 0 : 1), "迁移不重复授予");
                }
                Debug.Log("OPENING_COPPER_CHECKS PASS | two basic courses, trial gating, unique pickup, registration, safety gate, disk reload and three v1 milestones");
            }
            finally
            {
                foreach (string suffix in new[] {"", ".bak", ".tmp"}) File.Delete(path+suffix);
                Directory.Delete(dir);
            }
        }
    }
}
