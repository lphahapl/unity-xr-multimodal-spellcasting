using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed partial class LinearRpgController
    {
        // Kept only between an accepted LoadSceneAsync and the destination player's Awake.
        // The JSON copy contains no scene objects and stays valid while the target loads its skills.
        internal sealed class GateEntry
        {
            internal readonly string Destination, PlayerJson;
            internal readonly List<Type> Equipment;

            internal GateEntry(string destination, RpgProgressData player, List<Type> equipment)
            {
                Destination = destination;
                PlayerJson = JsonUtility.ToJson(player);
                Equipment = equipment;
            }
        }

        static GateEntry pendingGateEntry;
        GateEntry gateEntry;
        internal bool HasGateEntry => gateEntry != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGateHandoff()
        {
            pendingGateEntry = null;
            SceneManager.sceneLoaded -= ExpireGateEntry;
        }

        internal static bool TryCaptureGateDeparture(Scene source, Transform head, string target,
            out GateEntry entry, out string reason)
        {
            entry = null;
            reason = null;
            var configured = source.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LinearRpgController>(true)).ToArray();
            if (configured.Length == 0) return true; // Non-RPG environments can also use a travel gate.
            var players = configured.Where(rpg => rpg.isActiveAndEnabled).ToArray();
            var owners = players.Length == 1 ? players :
                players.Where(rpg => rpg.locomotion && rpg.locomotion.head == head).ToArray();
            if (owners.Length != 1)
            {
                reason = "入口无法唯一确定触发传送门的玩家";
                return false;
            }
            var owner = owners[0];
            if (!owner.CanAct || owner.committingPlayerState || owner.CastTransactionActive)
            {
                reason = "玩家尚未就绪、已倒下或正在提交操作";
                return false;
            }
            try
            {
                var carried = new List<Type>();
                var vitals = owner.Player.CaptureCheckpoint(owner.GameTime);
                foreach (var equipment in owner.checkpointEquipment.OfType<IRpgTravelEquipment>())
                    if (equipment.CaptureCarriedEquipment(vitals)) carried.Add(equipment.GetType());
                var player = owner.Model.Export(vitals, owner.CurrentStageCheckpoint.nodeId);
                entry = new GateEntry(target, player, carried);
                return true;
            }
            catch (Exception error)
            {
                reason = "无法捕获当前玩家：" + error.Message;
                return false;
            }
        }

        internal static void PublishGateEntry(GateEntry entry)
        {
            ResetGateHandoff();
            if (entry == null) return;
            pendingGateEntry = entry;
            SceneManager.sceneLoaded += ExpireGateEntry;
        }

        static void ExpireGateEntry(Scene scene, LoadSceneMode mode)
        {
            // sceneLoaded follows Awake. Even a destination without a player cannot leave a ticket for a later visit.
            if (mode != LoadSceneMode.Single) return;
            if (pendingGateEntry != null)
                Debug.LogWarning($"[场景传送门] 已进入 {scene.path}，没有玩家接收发往 {pendingGateEntry.Destination} 的跨章数据");
            ResetGateHandoff();
        }

        bool ClaimGateEntry()
        {
            var scene = gameObject.scene;
            // Claim in Awake, before OnEnable has necessarily run on this or the equipment modules.
            if (pendingGateEntry == null || !enabled || !gameObject.activeInHierarchy ||
                !(string.Equals(pendingGateEntry.Destination, scene.path, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(pendingGateEntry.Destination, scene.name, StringComparison.OrdinalIgnoreCase))) return false;
            gateEntry = pendingGateEntry;
            ResetGateHandoff();
            return true;
        }

        void ClearGateEntry() => gateEntry = null;

        RpgProgressData BuildGateCheckpoint(RpgProgressData fresh)
        {
            var arriving = JsonUtility.FromJson<RpgProgressData>(gateEntry.PlayerJson);
            arriving.checkpointId = fresh.checkpointId;
            arriving.completedStageIds = fresh.completedStageIds;
            arriving.claimedRewardIds = fresh.claimedRewardIds;
            arriving.discoveredSkillIds = arriving.discoveredSkillIds.Union(fresh.discoveredSkillIds).ToList();
            arriving.unlockedSkillIds = arriving.unlockedSkillIds.Union(fresh.unlockedSkillIds).ToList();
            arriving.checkpoint.health = Mathf.Min(arriving.checkpoint.health, Player.MaxHealth);
            arriving.checkpoint.mana = Mathf.Min(arriving.checkpoint.mana, Player.MaxMana);

            var unknown = arriving.discoveredSkillIds.Concat(arriving.unlockedSkillIds)
                .FirstOrDefault(id => !runtimeSkills.TryGet(id, out _));
            if (unknown != null) throw new InvalidOperationException("目标技能目录缺少传入术法：" + unknown);
            foreach (var type in gateEntry.Equipment)
            {
                var target = checkpointEquipment.OfType<IRpgTravelEquipment>()
                    .SingleOrDefault(component => component.GetType() == type && ((MonoBehaviour)component).enabled);
                if (target == null) throw new InvalidOperationException("目标场景缺少随身物品接收组件：" + type.Name);
                target.PrepareArrival(arriving.checkpoint, CheckpointAt(0).floorPosition + Vector3.up * .15f);
            }
            if (!checkpoints.Validate(arriving, out var reason)) throw new InvalidOperationException("传入数据无效：" + reason);
            if (!ValidateProgress(arriving)) throw new InvalidOperationException("传入的成长数据与目标章节配置不兼容");
            return arriving;
        }
    }
}
