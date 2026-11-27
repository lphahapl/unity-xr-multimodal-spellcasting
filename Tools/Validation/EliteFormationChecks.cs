// 复制到隔离工程 Assets/Editor，由敌人 Play 验收入口使用真实出生租约和配置材质调用。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEngine;

public static class EliteFormationChecks
{
    static readonly MethodInfo LateTick = typeof(EliteCommandFormation).GetMethod("LateUpdate",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Roster = typeof(EliteCommandFormation).GetField("members",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>在两名精英同时指挥且名单均非空时调用，同时检查生产入口已绑定表现。</summary>
    public static void Verify(EliteCommandFormation left, EliteCommandFormation right, EnemyLease leftLeader,
        EnemyLease rightLeader, IReadOnlyList<EnemyLease> leftMinions, IReadOnlyList<EnemyLease> rightMinions,
        Material material)
    {
        Require(left && right && left != right && leftMinions.Count > 0 && rightMinions.Count > 0,
            "T18 双精英须同时存在且各有成员");
        Require(!leftMinions.Intersect(rightMinions).Any(), "T18 两组用例名单须独立");
        CheckMaterial(material);
        AssertGroup(left, leftMinions, material);
        AssertGroup(right, rightMinions, material);
        var originalRoster = (IReadOnlyList<EnemyLease>)Roster.GetValue(left);
        var original = Lines(left).Select(l => l.GetInstanceID()).OrderBy(id => id).ToArray();
        CheckGeometry(left);
        Debug.Log("ENEMY_CHECK T18 production presentations: assigned material, centered links, two rosters and 3-4-5");

        left.ShowFormation = false;
        AssertHidden(left);
        AssertGroup(right, rightMinions, material);
        left.ShowFormation = true;
        AssertGroup(left, leftMinions, material);
        left.enabled = false;
        AssertHidden(left);
        left.enabled = true;
        AssertGroup(left, leftMinions, material);
        left.Hide();
        AssertHidden(left);
        AssertGroup(right, rightMinions, material);
        left.ShowFormation = false;
        left.ShowFormation = true;
        AssertHidden(left);
        left.Present(leftLeader, originalRoster, material);
        left.Present(rightLeader, rightMinions, material);
        AssertGroup(left, leftMinions, material);
        Require(original.SequenceEqual(Lines(left).Select(l => l.GetInstanceID()).OrderBy(id => id)),
            "T18 恢复同一指挥时应复用原有线组件");
        Debug.Log("ENEMY_CHECK T18 toggling, interrupt, resumption, foreign-owner rejection and pooled line identity");

        // 同帧移动后主动驱动真实 LateUpdate，避免等待下一帧时被导航挪回。
        var child = leftMinions[0].Controller.transform;
        var initial = child.position;
        try
        {
            child.position += Vector3.right * .125f;
            LateTick.Invoke(left, null);
            AssertGroup(left, leftMinions, material);
            AssertGroup(right, rightMinions, material);
        }
        finally { child.position = initial; LateTick.Invoke(left, null); }
        Debug.Log("ENEMY_CHECK T18 member movement updates link endpoint");
    }

    /// <summary>成员死亡或已退池复用后调用。将旧租约明确留在名单中，验证它不会连到尸体或新生物体。</summary>
    public static void VerifyRetiredMember(EliteCommandFormation visual, EnemyLease leader, EnemyLease retired,
        IReadOnlyList<EnemyLease> surviving, Material material)
    {
        Require(retired != null && !surviving.Contains(retired), "T18 传入失效成员及其余存活成员");
        var originalRoster = (IReadOnlyList<EnemyLease>)Roster.GetValue(visual);
        var mixed = new List<EnemyLease>(surviving) { retired };
        visual.Present(leader, mixed, material);
        try { AssertGroup(visual, surviving, material); }
        finally { visual.Present(leader, originalRoster, material); }
        Debug.Log("ENEMY_CHECK T18 stale/dead member omitted, surviving endpoints intact");
    }

    /// <summary>指挥官第一次退池后、复用物体的下一次指挥前调用。晚到的旧会话不能重开几何或连线。</summary>
    public static void VerifyRetiredCommander(EliteCommandFormation visual, EnemyLease retired,
        IReadOnlyList<EnemyLease> oldMembers, Material material)
    {
        AssertHidden(visual);
        visual.Present(retired, oldMembers, material);
        AssertHidden(visual);
        Debug.Log("ENEMY_CHECK T18 retired commander cannot reopen a pooled formation");
    }

    /// <summary>读取当前已渲染的端点，不触发刷新，可在真实中断、恢复和成员死亡后的帧上调用。</summary>
    public static void AssertGroup(EliteCommandFormation visual, IReadOnlyList<EnemyLease> living, Material material)
    {
        Require(visual, "T18 缺少算阵组件");
        var lines = Lines(visual).Where(Visible).ToArray();
        var geometry = lines.Where(line => line.loop && !line.useWorldSpace).ToArray();
        Require(geometry.Length == 2, "T18 应有一圆一三角形");
        Require(lines.All(l => l.sharedMaterial == material), "T18 所有可见线应使用配置指定的同一材质");
        var links = lines.Where(line => !line.loop && line.useWorldSpace && line.positionCount == 2).ToArray();
        Require(lines.Length == 2 + living.Count && links.Length == living.Count, "T18 本组可见测绘线数量错误");
        var center = geometry[0].transform.position;
        var endpoints = new List<Vector3>();
        foreach (var line in links)
        {
            Require(Near(line.GetPosition(0), center), "T18 测绘线没有从算阵圆心出发");
            endpoints.Add(line.GetPosition(1));
        }
        foreach (var lease in living)
        {
            var expected = lease.Controller.transform.position + Vector3.up * .9f;
            int match = endpoints.FindIndex(end => Near(end, expected));
            Require(match >= 0, "T18 缺少本组存活成员端点，租约 " + lease.Id);
            endpoints.RemoveAt(match);
        }
    }

    public static void AssertHidden(EliteCommandFormation visual)
    {
        Require(visual && !Lines(visual).Any(Visible), "T18 退出指挥或禁用后仍有可见几何/测绘线");
    }

    static void CheckGeometry(EliteCommandFormation visual)
    {
        var shapes = Lines(visual).Where(l => Visible(l) && l.loop).ToArray();
        var circle = shapes.Single(l => l.positionCount == 36);
        var triangle = shapes.Single(l => l.positionCount == 3);
        Require(circle.startColor.r > circle.startColor.b && triangle.startColor.r > triangle.startColor.b &&
            Lines(visual).Where(l => Visible(l) && l.useWorldSpace).All(l => l.startColor.b > l.startColor.r),
            "T18 圆阵与测绘线应分别提供暖色和青色顶点色");
        var a = triangle.GetPosition(0);
        var b = triangle.GetPosition(1);
        var c = triangle.GetPosition(2);
        float scale = Vector3.Distance(a, b) / 5f;
        float radius = scale * 2.5f;
        Require(scale > .01f && Mathf.Abs(Vector3.Distance(a, c) - 3f * scale) < .001f &&
            Mathf.Abs(Vector3.Distance(b, c) - 4f * scale) < .001f, "T18 三角形边长比例错误");
        Require(Near((a + b) * .5f, Vector3.zero), "T18 斜边应穿过圆心");
        foreach (var line in new[] { circle, triangle })
            for (int i = 0; i < line.positionCount; i++)
            {
                var point = line.GetPosition(i);
                Require(Mathf.Abs(point.magnitude - radius) < .001f && Mathf.Abs(point.y) < .001f,
                    "T18 几何顶点应共面且都在圆周上");
            }
    }

    static void CheckMaterial(Material material)
    {
        Require(material && AssetDatabase.Contains(material), "T18 线材质须来自持久资产");
        Require(material.shader && material.shader.name == "Universal Render Pipeline/Particles/Unlit",
            "T18 测绘线应能加载 URP 粒子无光照着色器");
        var texture = material.GetTexture("_BaseMap");
        Require((!texture || !AssetDatabase.Contains(texture)) && material.GetColor("_BaseColor") == Color.white &&
            material.GetFloat("_ColorMode") == 0, "T18 材质应以白底乘顶点色且无底图资产依赖");
        Require(material.GetFloat("_Surface") == 0 && material.GetFloat("_ZWrite") == 1 &&
            material.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.GeometryLast,
            "T18 测绘线应走场景不透明深度测试与深度写入");
    }

    static LineRenderer[] Lines(EliteCommandFormation visual)
    {
        var root = visual.transform.Find("EliteCommandFormationVisual");
        return root ? root.GetComponentsInChildren<LineRenderer>(true) : Array.Empty<LineRenderer>();
    }

    static bool Visible(LineRenderer line) => line.enabled && line.gameObject.activeInHierarchy;
    static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;
    static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
}
