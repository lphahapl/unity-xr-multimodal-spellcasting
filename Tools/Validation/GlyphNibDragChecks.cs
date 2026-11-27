using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    // Copy this fixture into the isolated project's Assets/scripts/logic/Editor before running.
    public static class GlyphNibDragChecks
    {
        const string Prefab = "Assets/Art/Props/MoGuiPen/MoGuiPen.prefab";
        const string PrefabGuid = "943f8f70076a96145966963f2e01f78a";
        const string MeshGuid = "070df29b345a1474e84427b719f7371c";
        const float Root = .0677f, Tip = .108f;
        const float CloseEnough = .0000003f;

        public static void Run()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Require(asset, "正式墨规笔 Prefab 可加载");
            CheckAnchor(asset, "Grip", 6730505530849376532L, Vector3.zero);
            CheckAnchor(asset, "Nib", 173608077789528672L, new Vector3(0, 0, Tip));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            var second = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            var board = new GameObject("笔锋 QA 画板");
            var otherBoard = new GameObject("笔锋 QA 换板");
            try
            {
                CheckSynthetic(instance, second, board.transform, otherBoard.transform);
                Debug.Log("[GlyphNibDragChecks] PASS: 原 Prefab 两档实网格、材质、端点、旋转、实例隔离、停止、抬笔、失追、换板和重拿");
            }
            finally
            {
                Object.DestroyImmediate(otherBoard);
                Object.DestroyImmediate(board);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(instance);
            }
        }

        static void CheckAnchor(GameObject asset, string name, long expectedId, Vector3 position)
        {
            var anchor = asset.transform.Find(name);
            Require(anchor && anchor.parent == asset.transform &&
                anchor.localPosition == position && anchor.localRotation == Quaternion.identity &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(anchor, out string guid, out long id) &&
                guid == PrefabGuid && id == expectedId, "正式锚节点、姿态和 fileID：" + name);
        }

        static void CheckSynthetic(GameObject instance, GameObject second, Transform board, Transform otherBoard)
        {
            var pen = instance.transform;
            var nib = pen.Find("Nib");
            var grip = pen.Find("Grip");
            var visual = instance.GetComponent<GlyphNibDragVisual>();
            Require(visual && visual.enabled && instance.GetComponentsInChildren<Collider>(true).Length == 0,
                "运行时只有正式笔锋视觉，保持 Prefab 原碰撞");
            var mesh = Geometry.Read(pen);
            var untouched = Geometry.Read(second.transform);
            int[] triangles = { 2228, 802 };
            for (int i = 0; i < mesh.Length; i++)
            {
                Require(mesh[i].source.isReadable && mesh[i].source == untouched[i].source &&
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh[i].source)) == MeshGuid &&
                    mesh[i].source.subMeshCount == 1 && mesh[i].triangles.Length == triangles[i] * 3,
                    "正式独立笔模型 GUID、可读网格、子网格和面数 " + i);
            }
            Require(mesh[0].source != mesh[1].source, "近、远档网格独立");

            const double dt = 1.0 / 90;
            double time = 1;
            void Input(Vector3 point, bool tracked = true, bool contact = true)
            {
                time += dt;
                Vector3 tip = board.TransformPoint(point);
                pen.position += tip - nib.position;
                visual.Observe(board, nib.position, tracked, contact, time);
                Require(Vector3.Distance(nib.position, tip) < .00002f &&
                    nib.localPosition == new Vector3(0, 0, Tip) && grip.localPosition == Vector3.zero,
                    "表现更新保持逻辑 Nib 和原 Grip");
            }
            void Drive(Vector3 start, Vector3 direction, float speed, int frames)
            {
                for (int i = 0; i <= frames; i++)
                    Input(start + direction * (speed * (float)(i * dt)));
            }
            void Settle(Vector3 at, int frames, bool tracked, bool contact)
            {
                for (int i = 0; i < frames; i++) Input(at, tracked, contact);
            }
            void AssertBend(Vector3 direction, float target)
            {
                foreach (var lod in mesh) lod.CheckBend(pen, direction, target);
            }

            var start = new Vector3(-.04f, .01f, 0);
            pen.rotation = Quaternion.Euler(14, 36, 24);
            Drive(start, Vector3.right, .025f, 80);
            AssertBend(-board.right, .002f);
            for (int i = 0; i < mesh.Length; i++)
                Require(untouched[i].filter.sharedMesh == mesh[i].source && mesh[i].filter.sharedMesh != mesh[i].source &&
                    mesh[i].source.vertices.SequenceEqual(mesh[i].positions), "实例网格隔离及原网格无修改");
            Vector3 end = start + Vector3.right * (.025f * (float)(80 * dt));
            Settle(end, 54, true, true);
            NearRest(mesh, "停笔 600 ms 回正");

            board.SetPositionAndRotation(new Vector3(.42f, 80.31f, -.25f), Quaternion.Euler(18, 45, 7));
            visual.ResetDrag();
            Drive(start, Vector3.up, .025f, 80);
            AssertBend(-board.up, .002f);
            end = start + Vector3.up * (.025f * (float)(80 * dt));
            Settle(end + Vector3.back * .055f, 45, true, false);
            CheckResidual(mesh, .0000006f, "抬笔 500 ms 基本回正");
            Settle(end + Vector3.back * .055f, 24, true, false);
            NearRest(mesh, "抬笔后恢复原边界");

            visual.ResetDrag();
            Drive(start, Vector3.right, .30f, 33);
            AssertBend(-board.right, .0045f);
            Settle(end, 45, false, false);
            CheckResidual(mesh, .0000012f, "失追 500 ms 基本回正");
            Settle(end, 24, false, false);
            NearRest(mesh, "持续失追后恢复原边界");
            Input(new Vector3(.12f, 0, 0));
            NearRest(mesh, "恢复首帧按新位置起算");

            visual.ResetDrag();
            Drive(start, Vector3.right, .025f, 80);
            visual.Observe(otherBoard, otherBoard.TransformPoint(start), true, true, time += dt);
            NearRest(mesh, "更换面板立即重建基线");
            Drive(start, Vector3.right, .025f, 80);
            visual.ResetDrag();
            Input(new Vector3(.12f, 0, 0));
            NearRest(mesh, "收笔重拿首帧无拖弯");

            visual.ResetDrag();
            Drive(start, Vector3.forward, .30f, 30);
            NearRest(mesh, "沿板法向移动无拖弯");
            visual.ResetDrag();
            Drive(new Vector3(-.04f, GlyphBoard.ButtonY, 0), Vector3.right, .14f, 40);
            NearRest(mesh, "撤销清空键区域无拖弯");
        }

        // Call after taking the actual item and opening its board, before the existing SquareRule replay.
        // The caller's draw function sends board-local targets through the real Observe/Advance and player input.
        public static void CheckHeldStroke(GlyphPenController glyph, GlyphBoard board, Action<Vector3, double> draw)
        {
            Require(glyph && glyph.IsHeld && glyph.nib && board && board.IsOpen && board.PointCount == 0,
                "实笔已通过玩家输入拿取并打开干净剧情板");
            var visual = glyph.nib.GetComponentInParent<GlyphNibDragVisual>();
            Require(visual && visual.transform.IsChildOf(glyph.penRoot), "实体道具的同一 Nib 绑定原墨规笔视觉");
            var mesh = Geometry.Read(visual.transform);
            var local = new Vector3(-.055f, .02f, .001f);
            draw(local + Vector3.back * .056f, .02);
            draw(local, .02);
            for (int step = 1; step <= 24; step++)
                draw(local + Vector3.right * (.0015f * step), .02);
            foreach (var lod in mesh) lod.CheckBend(visual.transform, -board.transform.right, .0045f);
            Require(board.PointCount > 4, "真实书写已提交多个墨迹样本");
            var point = board.Samples[board.PointCount - 1].boardPosition;
            Require(Vector3.Distance(point, board.transform.InverseTransformPoint(glyph.nib.position)) < .0031f &&
                Mathf.Abs(point.z) < .0001f, "实笔变形时墨迹仍来自逻辑 Nib 的板面采样");

            int count = board.PointCount;
            for (int frame = 0; frame < 36; frame++)
                draw(local + Vector3.right * .036f + Vector3.back * .056f, .02);
            NearRest(mesh, "真实 Observe/Advance 抬笔回正");
            Require(board.PointCount == count, "抬笔时无新增墨迹");
            var clear = new Vector3(0, GlyphBoard.ButtonY, .001f);
            draw(clear + Vector3.back * .056f, .02);
            draw(clear, .02);
            draw(clear + Vector3.back * .056f, .02);
            Require(board.IsOpen && board.PointCount == 0 && glyph.IsHeld, "通过真实清空键返回干净剧情板");
            NearRest(mesh, "清空键保持笔毫中性");
            Debug.Log("[GlyphNibDragChecks] PASS: 实体墨规笔 Observe/Advance、板面墨迹、抬笔与清空");
        }

        static void NearRest(Geometry[] mesh, string message)
        {
            CheckResidual(mesh, CloseEnough, message);
            foreach (var lod in mesh)
            {
                var active = lod.filter.sharedMesh;
                Require(active.bounds.center.Equals(lod.bounds.center) && active.bounds.size.Equals(lod.bounds.size),
                    message + "及原边界");
            }
        }

        static void CheckResidual(Geometry[] mesh, float maximum, string message)
        {
            foreach (var lod in mesh)
            {
                var vertices = lod.filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    Require(Vector3.Distance(vertices[i], lod.positions[i]) <= maximum, message);
            }
        }

        sealed class Geometry
        {
            public readonly MeshFilter filter;
            public readonly Mesh source;
            public readonly Vector3[] positions;
            public readonly int[] triangles;
            public readonly Bounds bounds;
            readonly Vector2[] uv;
            readonly Vector3[] restNormals;
            readonly Vector4[] restTangents;
            readonly Material[] materials;
            readonly UnityEngine.Rendering.ShadowCastingMode shadowMode;

            Geometry(MeshRenderer render)
            {
                filter = render.GetComponent<MeshFilter>();
                source = filter.sharedMesh;
                positions = source.vertices;
                triangles = source.GetIndices(0);
                bounds = source.bounds;
                uv = source.uv;
                restNormals = source.normals;
                restTangents = source.tangents;
                materials = render.sharedMaterials;
                shadowMode = render.shadowCastingMode;
            }

            public static Geometry[] Read(Transform pen)
            {
                var lods = pen.GetComponent<LODGroup>().GetLODs();
                Require(lods.Length == 2 && lods.All(lod => lod.renderers.Length == 1 &&
                    lod.renderers[0] is MeshRenderer), "原两档渲染节点仍分别接到 LODGroup");
                return lods.Select(lod => new Geometry((MeshRenderer)lod.renderers[0])).ToArray();
            }

            public void CheckBend(Transform pen, Vector3 worldAxis, float amount)
            {
                var active = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                var current = active.vertices;
                Require(active != source && current.Length == positions.Length && active.subMeshCount == 1 &&
                    active.GetIndices(0).SequenceEqual(triangles) && active.uv.SequenceEqual(uv) &&
                    materials.SequenceEqual(renderer.sharedMaterials) && renderer.shadowCastingMode == shadowMode,
                    "每档仅更改顶点位置/方向，保留材料、阴影、UV 和三角索引");
                var meshToPen = GlyphNibDragVisual.MeshToPen(pen, filter.transform);
                int moved = 0, fixedTip = 0;
                float observedMax = 0;
                var normals = active.normals;
                var tangents = active.tangents;
                Require(normals.Length == current.Length && tangents.Length == current.Length, "两档提供导入法线与切线");
                for (int i = 0; i < current.Length; i++)
                {
                    var original = meshToPen.MultiplyPoint3x4(positions[i]);
                    float t = (original.z - Root) / (Tip - Root);
                    var movement = filter.transform.TransformVector(current[i] - positions[i]);
                    if (Vector3.Distance(original, new Vector3(0, 0, Tip)) < .0000003f)
                    {
                        fixedTip++;
                        Require(current[i].Equals(positions[i]), "最前端真实网格与 Nib 同点且固定");
                    }
                    if (original.z <= Root + 1e-7f || original.z >= Tip - 1e-7f)
                        Require(current[i].Equals(positions[i]) && normals[i].Equals(restNormals[i]) &&
                            tangents[i].Equals(restTangents[i]), "锋根、尖端、铜箍、木杆、握点与尾环范围全部静止");
                    else
                    {
                        Require(uv[i].x < .172f, "活动顶点来自原笔毫纹理区，铜箍不参与");
                        float weight = Mathf.Sin(Mathf.PI * t);
                        var expected = worldAxis * (amount * weight * weight);
                        Require(Vector3.Distance(movement, expected) < .00009f && active.bounds.Contains(current[i]),
                            "局部 XY 反向拖弯吻合 B 的 sin² 权重和限幅，且顶点留在更新边界内");
                        if (movement.sqrMagnitude > 1e-12f) moved++;
                        observedMax = Mathf.Max(observedMax, movement.magnitude);
                        var tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                        Require(Mathf.Abs(Vector3.Dot(normals[i], tangent)) < .037f &&
                            Mathf.Abs(normals[i].sqrMagnitude - 1) < .01f &&
                            Mathf.Abs(tangent.sqrMagnitude - 1) < .01f,
                            "变形法线切线保持正常，保留 PBR 原光照响应");
                    }
                }
                Require(fixedTip > 0 && moved > 15, "两档有真实固定尖和中段拖弯");
                Debug.Log($"[GlyphNibDragChecks] {source.name}: {moved} 个笔毫顶点，峰值 {observedMax * 1000:F4} mm");
            }
        }

        static void Require(bool pass, string check)
        {
            if (!pass) throw new InvalidOperationException("GlyphNibDragChecks FAIL: " + check);
        }
    }
}
