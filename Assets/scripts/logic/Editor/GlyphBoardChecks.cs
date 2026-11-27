using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class GlyphBoardChecks
    {
        static void Require(bool good, string reason)
        {
            if (!good) throw new InvalidOperationException("墨规笔检查失败：" + reason);
        }

        [MenuItem("Tools/考工律度/墨规笔/验证画板与模板")]
        public static void Run()
        {
            var root = new GameObject("画板回放");
            var template = ScriptableObject.CreateInstance<GlyphTemplate>();
            var alternate = ScriptableObject.CreateInstance<GlyphTemplate>();
            try
            {
                // 两笔共用一个角：在旋转的板上以世界笔尖输入，验证板面定位和断笔。
                var left = new Vector2(-.08f, .08f);
                var corner = new Vector2(-.08f, -.08f);
                var right = new Vector2(.08f, -.08f);
                template.SetShape(new[] { left, corner }, new[] { corner, right });
                template.displayName = "方直之矩";
                template.symbolId = "angle";
                alternate.SetShape(new[] { left, corner }, new[] { corner, right });
                alternate.symbolId = "other";
                root.transform.SetPositionAndRotation(new Vector3(1, 1.4f, 2), Quaternion.Euler(15, 71, -8));
                var board = root.AddComponent<GlyphBoard>();
                CheckRetracedInk(board);
                CheckBrushInk(board);
                CheckQianjunRepair(board);
                var story = root.AddComponent<GlyphStoryPanel>();
                story.board = board;
                story.template = template;
                var candidates = new[] { template };

                double time = 100;
                void Sample(float x, float y, float depth = 0)
                {
                    time += .02;
                    board.Sample(root.transform.TransformPoint(new Vector3(x, y, depth)), true, time);
                }
                void Line(Vector2 a, Vector2 b)
                {
                    for (int i = 0; i <= 12; i++)
                    {
                        var p = Vector2.Lerp(a, b, i / 12f);
                        Sample(p.x, p.y);
                    }
                }
                void Shape()
                {
                    Line(left, corner);
                    Sample(corner.x, corner.y, .05f);
                    Line(corner, right);
                }
                void WaitToSubmit()
                {
                    time += board.idleSeconds + .02;
                    Sample(0, 0, .05f);
                }

                story.Show();
                Require(story.IsWaiting && GlyphStoryPanel.FindNear(root.transform.position, .5f) == story, "剧情展开后在原地候笔");
                Require(Array.Exists(root.GetComponentsInChildren<UnityEngine.UI.Text>(), t => t.text == template.displayName), "剧情显示目标符名");
                var layers = root.GetComponentsInChildren<GlyphStrokeGraphic>();
                var guide = Array.Find(layers, graphic => graphic.name == "范本");
                var ink = Array.Find(layers, graphic => graphic.name == "朱砂墨迹");
                Require(ink && guide && guide.lines.Count == 2, "剧情板显示两笔模板图样");
                Require(!Array.Exists(root.GetComponentsInChildren<UnityEngine.UI.Text>(), t => t.text == "完成"), "剧情板不要求点提交按钮");
                Line(new Vector2(-.11f, .09f), new Vector2(.11f, .09f));
                Require(!board.TrySubmit(time, out _), "最后一个有效点刚采样时不提交");
                WaitToSubmit();
                Require(board.TrySubmit(time, out var hit) && !hit && !board.IsShowingSuccess, "剧情横划超时拒识");
                Require(Array.Exists(root.GetComponentsInChildren<UnityEngine.UI.Text>(), t =>
                    t.text.Contains("符形不符") && t.text.Contains("重画")), "拒识文字实际显示在板上");
                Require(!board.TrySubmit(time + board.idleSeconds, out _), "失败笔迹不重复提交");
                Sample(0, -.179f);
                Require(board.PointCount == 0 && board.IsOpen, "拒识后可清空并继续作画");
                Shape();
                Require(board.StrokeCount == 2, "离板仅断笔");
                Require(!board.TrySubmit(time, out _), "剧情新采样会延长停笔时间");
                WaitToSubmit();
                Require(board.TrySubmit(time, out hit) && hit == template, "剧情停笔自动匹配目标模板");
                Require(!board.TrySubmit(time, out _), "同一图案只提交一次");
                double started = time;
                board.BeginStorySuccess(started);
                Require(board.IsShowingSuccess, "判定成功进入吸附反馈");
                Vector2 beforeSnap = ink.lines[0][0];
                int flashOnsets = 0;
                bool wasBright = false, overlapped = false, movedInk = false;
                for (int i = 1; i < 60; ++i)
                {
                    time = started + board.successDuration * (i / 60.0);
                    Require(!board.AdvanceStorySuccess(time), "吸附金闪未结束时不能完成机关");
                    movedInk |= (ink.lines[0][0] - beforeSnap).sqrMagnitude > .000001f;
                    bool overlap = ReferenceEquals(ink.lines, guide.lines);
                    bool bright = overlap && ink.color.r > .95f && ink.color.g > .85f && ink.width > 3.65f;
                    if (bright && !wasBright) flashOnsets++;
                    wasBright = bright;
                    overlapped |= overlap;
                }
                Require(movedInk && overlapped && flashOnsets == 1, "墨迹实际吸附重合，合拢后金闪恰好一次");
                time = started + board.successDuration + .001;
                Require(board.AdvanceStorySuccess(time), "反馈结束后允许机关提交");
                story.Cancel();
                Require(!board.IsOpen && !story.IsWaiting, "关闭并注销剧情板");

                board.Open(candidates, false);
                Require(!board.TrySubmit(time + 100, out _), "空白不触发");
                Sample(0, 0, board.contactDistance + .003f);
                Require(board.PointCount == 0, "落笔门限");
                Sample(0, 0);
                Sample(.03f, 0, board.contactDistance + .003f);
                Require(board.PointCount == 2 && board.StrokeCount == 1, "接触滞回");
                var recorded = board.Samples[1];
                Require(recorded.strokeIndex == 0 && recorded.time == time &&
                        Vector3.Distance(recorded.boardPosition, new Vector3(.03f, 0, board.contactDistance + .003f)) < .00001f,
                    "采样保留板面局部位置、深度、单调时刻与笔画编号");
                Sample(.0301f, .0001f);
                Require(board.PointCount == 2, "微抖不新增采样或延迟识别");
                Sample(0, -.179f);
                Require(board.PointCount == 0, "板外清空键");
                Shape();
                Sample(0, 0, .05f);
                Require(!board.TrySubmit(time, out _), "主动板未到时不提交");
                board.Sample(default, false, time + .1);
                time += 10;
                Require(!board.TrySubmit(time, out _), "缺失追踪不会判符");
                Sample(0, 0, .05f);
                Require(!board.TrySubmit(time, out _), "失追期间时间不计作停笔");
                WaitToSubmit();
                Require(board.TrySubmit(time, out hit) && hit == template, "主动板停笔自动识别");
                Require(!board.TrySubmit(time + 10, out _), "主动重复到时不会再触发");

                board.Open(new[] { template, alternate }, false);
                Shape();
                WaitToSubmit();
                Require(board.TrySubmit(time, out hit) && !hit, "相似的不同符类拒识");
                alternate.symbolId = template.symbolId;
                board.Open(new[] { template, alternate }, false);
                Shape();
                WaitToSubmit();
                Require(board.TrySubmit(time, out hit) && hit, "同符 ID 的多个样本不构成歧义");
                board.Open(candidates, false);
                Line(left, corner);
                board.Sample(default, false, time);
                Line(corner, right);
                Require(board.StrokeCount == 2, "失追缺口不连线");
                Sample(-.119f, -.179f);
                Require(board.StrokeCount == 1, "撤销仅最后一笔");
                board.Close();
                board.Sample(Vector3.zero, true, time);
                Require(!board.TrySubmit(time + 100, out _) && board.PointCount == 0, "关闭无残留结果");
                foreach (bool storyAttempt in new[] { false, true })
                {
                    board.Open(candidates, storyAttempt);
                    Line(left, corner);
                    WaitToSubmit();
                    int originalPoints = board.PointCount;
                    Require(board.TrySubmit(time, out hit) && !hit && board.StrokeCount == 1 && board.PointCount == originalPoints,
                        "判错时保留上一笔");
                    Line(corner, right);
                    WaitToSubmit();
                    Require(board.StrokeCount == 2 && board.PointCount > originalPoints && board.TrySubmit(time, out hit) && hit == template,
                        "保留旧墨，在原板补齐另一边即可通过");
                    Sample(0, -.179f);
                    Require(board.PointCount == 0 && board.StrokeCount == 0, "手动清空后从白板重画");
                    board.Close();
                }
                var empty = new List<Vector2[]>();
                Require(GlyphRecognizer.Similarity(empty, template.Preview()) == 0, "空图案安全拒绝");
                var invalid = new[] { new[] { Vector2.zero, new Vector2(float.NaN, 1) } };
                Require(GlyphRecognizer.Similarity(invalid, template.Preview()) == 0, "非法坐标安全拒绝");
                Debug.Log("GlyphBoardChecks PASS: 画板接触、分笔、失追、主动/剧情匹配、一次消费及拒识。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(template);
                UnityEngine.Object.DestroyImmediate(alternate);
            }
        }

        static void CheckQianjunRepair(GlyphBoard board)
        {
            var template = AssetDatabase.LoadAssetAtPath<GlyphTemplate>("Assets/Art/Glyphs/Qianjun.asset");
            var shape = GlyphTemplateChecks.GameProjection();
            double time = 1;
            void Trace(Vector2[] edge)
            {
                foreach (var point in edge)
                    board.Sample(board.transform.TransformPoint(new Vector3(point.x, point.y, 0)), true, time += .02);
                board.Sample(board.transform.TransformPoint(Vector3.forward * .05f), true, time += .02);
            }

            foreach (bool story in new[] { false, true })
            {
                board.Open(new[] { template }, story);
                const int roof = 2;
                for (int i = 0; i < shape.Length; ++i)
                    if (i != roof) Trace(shape[i]);
                time += board.idleSeconds + .02;
                Require(board.TrySubmit(time, out var hit) && !hit, "千钧漏整条梯顶，停笔拒识");
                int pointsBefore = board.PointCount;
                Trace(shape[roof]);
                time += board.idleSeconds + .02;
                Require(board.TrySubmit(time, out hit) && hit == template && board.PointCount > pointsBefore,
                    "千钧保留原墨补上梯顶，停笔认出手绘截图近似");
                Debug.Log($"GlyphBoardChecks 千钧截图投影补笔：剧情={story}，局部采点={board.PointCount}，得分={board.LastScore:F6}");
                board.Close();
            }
        }

        static void CheckRetracedInk(GlyphBoard board)
        {
            board.Open(Array.Empty<GlyphTemplate>(), false);
            var ink = Array.Find(board.GetComponentsInChildren<GlyphStrokeGraphic>(), g => g.name == "朱砂墨迹");
            double clock = 1;
            void Tip(float x, float y, float z = 0) =>
                board.Sample(board.transform.TransformPoint(new Vector3(x, y, z)), true, clock += .01);
            void Stroke(Vector2 a, Vector2 b)
            {
                Tip(a.x, a.y, .04f);
                Tip(a.x, a.y);
                Tip(b.x, b.y);
            }
            Vector3[] Vertices()
            {
                ink.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                return ink.canvasRenderer.GetMesh().vertices;
            }

            var left = new Vector2(-.08f, 0);
            var right = new Vector2(.04f, 0);
            var jitter = new Vector2(0, .003f);
            Stroke(left, right);
            int initial = Vertices().Length;
            Require(initial == 8, "初笔生成笔头与一段真实网格");
            Stroke(left + jitter, right + jitter);
            Stroke(right - jitter, left - jitter);
            Require(Vertices().Length == initial && board.StrokeCount == 3 && board.PointCount == 6,
                "两侧相差三毫米的同向和反向描线不增加网格，原始分笔和采样均保留");
            Tip(right.x, -jitter.y);
            Require(Vertices().Length == initial, "同一笔折返也不增加墨迹");
            Stroke(left + jitter, new Vector2(.10f, jitter.y));
            var extended = Vertices();
            Require(extended.Length == initial + 4 && extended[initial].x > 38 && extended[initial].x < 42 &&
                extended[extended.Length - 1].x > 99, "同一段采样跨过旧线末端时，只画补出的六厘米");
            Stroke(new Vector2(-.02f, -.05f), new Vector2(-.02f, .05f));
            int cross = Vertices().Length;
            Require(cross == extended.Length + 8, "横竖相交保留完整交叉边");
            Stroke(left + Vector2.up * .009f, right + Vector2.up * .009f);
            Require(Vertices().Length == cross + 8, "分开的九毫米平行线独立绘制");
            Tip(-.119f, -.179f);
            Require(Vertices().Length == cross && board.StrokeCount == 5, "撤销按原始分笔恢复相应的可见墨迹");
            Tip(0, 0, .04f);
            Tip(0, -.179f);
            Require(Vertices().Length == 0 && board.PointCount == 0, "清空同时清掉可见路径索引");
            Stroke(left + jitter, right + jitter);
            Require(Vertices().Length == initial, "清空后可在原位置重新落墨");
            Stroke(new Vector2(left.x, -.009f), new Vector2(right.x, .015f));
            Require(Vertices().Length == initial + 8, "斜穿旧笔迹的小角度交叉也保留完整");
            board.Close();
        }

        static void CheckBrushInk(GlyphBoard board)
        {
            board.Open(Array.Empty<GlyphTemplate>(), false);
            var ink = Array.Find(board.GetComponentsInChildren<GlyphStrokeGraphic>(), g => g.name == "朱砂墨迹");
            Require(ink.material.shader.name == "NarutoVR/UI/Glyph Brush Ink" && ink.material.shader.isSupported,
                "实际画板加载毛笔材质");
            var paper = Array.Find(board.GetComponentsInChildren<UnityEngine.UI.Image>(), g => g.name == "书写面");
            Require(paper && paper.sprite && paper.sprite.name == "GlyphPaper", "实际画板加载纤维纸写面");
            double time = 1;
            void Tip(float x, float y, double dt, float z = 0) =>
                board.Sample(board.transform.TransformPoint(new Vector3(x, y, z)), true, time += dt);
            void Line(float y, double dt, double finalDt)
            {
                Tip(-.05f, y, .08, .04f);
                for (int i = 0; i <= 8; ++i) Tip(-.05f + i * .008f, y, i == 8 ? finalDt : dt);
            }

            Line(.07f, .14, .14);
            Line(.025f, .018, .018);
            Line(-.025f, .018, .14);
            Line(-.07f, .018, .008);
            Tip(-.06f, -.13f, .1, .04f);
            Tip(-.06f, -.13f, .14);
            Tip(-.052f, -.13f, .14);
            Tip(-.044f, -.13f, .14);
            Tip(-.044f, -.122f, .14);
            Tip(-.044f, -.114f, .14);

            ink.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
            var mesh = ink.canvasRenderer.GetMesh();
            var vertices = mesh.vertices;
            var pigment = mesh.colors32;
            var brush = mesh.uv;
            var taper = mesh.uv2;
            const int strip = 9 * 4;
            int middle = 4 * 4;
            // The portion beyond U = ±1 only provides room for feathering.
            float Width(int i) => Vector3.Distance(vertices[i], vertices[i + 1]) *
                (2f / (brush[i + 1].x - brush[i].x));
            Require(vertices.Length == strip * 4 + 5 * 4 && pigment.Length == vertices.Length &&
                taper.Length == vertices.Length && board.PointCount == 41, "速度渲染保留全部原始采样并提供两套笔刷坐标");
            Require(Width(middle) > Width(strip + middle) * 1.35f &&
                pigment[middle].a - pigment[strip + middle].a > 50, "相同路径的慢笔比快笔更粗、更浓");
            int finalEdge = strip - 4;
            Require(taper[2 * strip + finalEdge].x > taper[3 * strip + finalEdge].x &&
                Mathf.Abs(taper[3 * strip + finalEdge + 2].x) < .0001f, "急收和顺势扫出的笔锋不同，均收至真实落点");
            int corner = 4 * strip + 3 * 4;
            Require(Width(corner) > Width(middle) * 1.20f, "慢速转角顿笔加宽并连接两边");
            Debug.Log($"GlyphBrushInk PASS: slow {Width(middle):F2} mm / {pigment[middle].a}, " +
                $"fast {Width(strip + middle):F2} mm / {pigment[strip + middle].a}, corner {Width(corner):F2} mm.");
            board.Close();
        }
    }
}
