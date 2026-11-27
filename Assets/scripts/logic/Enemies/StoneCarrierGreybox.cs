using System.Collections.Generic;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.AI;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>运输灰盒的桌面操作面板。使用正式敌人目录、受击及千钧符效果；按钮仅代替头显输入。</summary>
    public sealed class StoneCarrierGreybox : MonoBehaviour
    {
        public EnemyManager manager;
        public StoneCarrierPost post;
        public Transform player;
        public BoxCollider floor;
        public QianjunEffectDefinition qianjun;
        public EnemyLease Lease { get; private set; }
        public string Status { get; private set; }
        NavMeshDataInstance surface;
        NavMeshData navData;
        Vector3 playerStart;
        bool spawning;
        GlyphEffectInstance stroke;
        GlyphEffectContext sample;
        float strokeTime;

        void Start()
        {
            playerStart = player.position;
            var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = floor.size,
                transform = floor.transform.localToWorldMatrix * Matrix4x4.Translate(floor.center) };
            var bounds = floor.bounds; bounds.Expand(new Vector3(2,4,2));
            navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
                new List<NavMeshBuildSource> { source }, bounds, Vector3.zero, Quaternion.identity);
            surface = NavMesh.AddNavMeshData(navData);
            Spawn();
        }

        public async void Spawn()
        {
            if (spawning) return;
            spawning = true;
            stroke?.End(); stroke = null;
            post.ResetPost();
            if (Lease != null) manager.Recycle(Lease);
            Lease = null;
            player.position = playerStart;
            try
            {
                var lease = await manager.SpawnEnemyAsync("stone-carrier", post.transform.position,
                    post.transform.rotation, player, false, destroyCancellationToken);
                if (!this) return;
                if (!post.Bind(lease, player)) { manager.Recycle(lease); Status = "出生点接线失败"; return; }
                Lease = lease;
                Status = "工奴已背石巡逻";
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception error) { if (this) { Status = error.Message; Debug.LogException(error, this); } }
            finally { spawning = false; }
        }

        public bool Strike(bool lethal)
        {
            if (Lease == null || !manager.IsCurrent(Lease)) return false;
            var entity = Lease.Controller.Entity;
            float damage = lethal ? Mathf.Min(entity.Combat.Health, 10000f) : 1;
            return entity.ReceiveHit(System.Guid.NewGuid().ToString("N"), damage, 0, attacker: player);
        }
        public void Board() => player.position = post.basket.passengerVolume.transform.TransformPoint(
            post.basket.passengerVolume.center + new Vector3(0,.35f,.95f));

        public bool Pull()
        {
            if (stroke != null || !post.basket.Stone) return false;
            var grip = player.position + Vector3.down * 1.1f;
            sample = new GlyphEffectContext { hand = grip, tip = grip, body = player.position, owner = player,
                tracked = true, bodyTracked = true, observed = Time.fixedTimeAsDouble,
                forward = (post.basket.Stone.transform.position - grip).normalized };
            stroke = qianjun.Begin(sample);
            stroke.Tick(sample, Time.fixedDeltaTime);
            if (!stroke.IsTethered) { stroke.End(); stroke = null; Status = "石块未开放、被遮挡或超出笔尖距离"; return false; }
            strokeTime = 0;
            Status = "模拟持笔手后拉";
            return true;
        }
        void FixedUpdate()
        {
            if (stroke == null) return;
            float dt = Time.fixedDeltaTime;
            strokeTime += dt;
            sample.hand -= sample.forward * (Mathf.Min(dt, Mathf.Max(0,.38f - strokeTime + dt)) * 1.5f);
            sample.observed += dt;
            stroke.Tick(sample, dt); stroke.FixedTick(dt);
            if (stroke.IsComplete || strokeTime >= 1.6f) { stroke.End(); stroke = null; }
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12,12,320,240), GUI.skin.box);
            GUILayout.Label("背石工奴 / A 篮运输灰盒");
            if (GUILayout.Button("普通受击，观察撤离")) Strike(false);
            if (GUILayout.Button("击破，观察原石落地")) Strike(true);
            if (GUILayout.Button("玩家进入 A 篮")) Board();
            if (GUILayout.Button("笔尖指向镇石，后拉一次")) Pull();
            if (GUILayout.Button("重新生成")) Spawn();
            GUILayout.Label(post.basket.Stone && post.basket.Stone.State == BallastStone.Phase.Docked ? "镇石已入槽" : Status);
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            stroke?.End();
            if (surface.valid) surface.Remove();
            if (navData) Destroy(navData);
        }
    }
}
