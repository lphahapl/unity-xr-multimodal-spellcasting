using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    public static class GlyphTemplateChecks
    {
        const string AssetPath = "Assets/Art/Glyphs/Qianjun.asset";
        const string StoryAssetPath = "Assets/Art/Glyphs/SquareRule.asset";

        [MenuItem("Tools/考工律度/墨规笔/验证画符模板")]
        public static void Run()
        {
            var template = AssetDatabase.LoadAssetAtPath<GlyphTemplate>(AssetPath);
            if (!template || template.symbolId != "glyph.qianjun" || !(template.effect is QianjunEffectDefinition))
                throw new InvalidOperationException("千钧符模板或效果接线缺失：" + AssetPath);
            var story = AssetDatabase.LoadAssetAtPath<GlyphTemplate>(StoryAssetPath);
            if (!story || story.symbolId != "glyph.square-rule")
                throw new InvalidOperationException("方直之矩模板接线缺失：" + StoryAssetPath);
            CheckCompletionAssets(template);
            CheckCompletionAssets(story);

            var failures = new List<string>();
            CheckScores(template, Cases(template.Preview()), failures);
            CheckScores(story, SquareCases(story.Preview()), failures);
            if (failures.Count > 0)
                throw new InvalidOperationException("GlyphTemplateChecks FAIL " + string.Join(", ", failures));
            Debug.Log("GlyphTemplateChecks PASS");
        }

        static void CheckScores(GlyphTemplate template, IEnumerable<(string name, bool accept, Vector2[][] ink)> samples,
            List<string> failures)
        {
            var results = new List<string>();
            foreach (var sample in samples)
            {
                float score = template.Score(sample.ink);
                string value = score.ToString("F6", CultureInfo.InvariantCulture);
                results.Add(sample.name + "=" + value);
                if (!float.IsFinite(score) || (score >= template.threshold) != sample.accept)
                    failures.Add(template.Title + "/" + sample.name + "=" + value + (sample.accept ? " 应命中" : " 应拒绝"));
            }
            string summary = template.Title + " threshold=" + template.threshold.ToString("F4", CultureInfo.InvariantCulture)
                + " " + string.Join(", ", results);
            Debug.Log("GlyphTemplateChecks scores " + summary);
        }

        static void CheckCompletionAssets(GlyphTemplate template)
        {
            var prefab = template.completionVfx;
            var clip = template.completionSfx;
            if (template.onCompleted == null || !prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab) ||
                !clip || !EditorUtility.IsPersistent(clip) || clip.length <= 0)
                throw new InvalidOperationException(template.Title + "缺少模板完成事件、粒子预制体或有声长的音频资产");
            Debug.Log($"GlyphTemplateChecks {template.Title}：{AssetDatabase.GetAssetPath(prefab)} / {AssetDatabase.GetAssetPath(clip)}");
        }

        internal static IEnumerable<(string name, bool accept, Vector2[][] ink)> Cases(Vector2[][] reference)
        {
            yield return ("原始图样", true, reference);
            var transformed = Array.ConvertAll(reference, stroke =>
                Array.ConvertAll(stroke, p => new Vector2(.03f, -.011f) + p * .097f));
            yield return ("板面等比缩放和平移", true, transformed);
            var top = reference[0];
            var shaft = reference[1];
            var weight = reference[2];
            for (int count = 3; count <= 5; ++count)
            {
                var strokes = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? top : top.Reverse().ToArray());
                yield return ("上横分笔重描" + count + "遍", true, strokes.Concat(new[] { shaft, weight }).ToArray());
            }
            yield return ("全部逆向且倒换笔顺", true,
                reference.Reverse().Select(stroke => stroke.Reverse().ToArray()).ToArray());
            yield return ("一笔折返接竖和底座", true, new[] {
                new[] { top[0], top[1], top[0], top[1], shaft[0], shaft[1] }.Concat(weight).ToArray()
            });
            yield return ("偏移的两段短横重描", true, new[] {
                top, new Vector2[] { new(.67f,.79f), new(-.28f,.83f) },
                new Vector2[] { new(-.61f,.88f), new(.16f,.91f) }, shaft, weight
            });
            yield return ("分笔倒序拼接底座", true, new[] {
                new[] { weight[3], weight[2] }, shaft.Reverse().ToArray(), new[] { weight[4], weight[3] },
                new[] { weight[0], weight[1], weight[2] }, new[] { shaft[0], top[0] }, new[] { top[1], shaft[0] }
            });
            yield return ("较窄较高", true, reference.Select(stroke =>
                stroke.Select(p => new Vector2(.83f * p.x, 1.08f * p.y)).ToArray()).ToArray());
            yield return ("较宽较矮", true, reference.Select(stroke =>
                stroke.Select(p => new Vector2(1.17f * p.x, .92f * p.y)).ToArray()).ToArray());
            yield return ("底边中间小缺口", true, new[] {
                top, shaft, new[] { weight[0], weight[1], weight[2], new Vector2(.08f,-.85f) },
                new[] { new Vector2(-.055f,-.84f), weight[3], weight[4] }
            });
            // 模拟玩家在上横附近来回重描，再分两次补画底座。坐标独立手工构造。
            yield return ("横画折返偏移且底边断笔", true, new[] {
                new Vector2[] { new(-.78f,.82f), new(.16f,.85f), new(.82f,.84f), new(.22f,.88f),
                    new(-.59f,.90f), new(-.33f,.85f), new(.71f,.79f) },
                new Vector2[] { new(.61f,.83f), new(.18f,.85f), new(-.69f,.88f) },
                new Vector2[] { new(.01f,.83f), new(-.03f,.31f), new(.015f,-.22f) },
                new Vector2[] { new(-.26f,-.22f), new(.28f,-.24f), new(.55f,-.81f), new(.06f,-.86f) },
                new Vector2[] { new(-.05f,-.84f), new(-.55f,-.85f), new(-.24f,-.24f) }
            });
            // 板面坐标以米计：上横宽约 18 厘米，来回描时上下起伏 1 厘米。
            // 垂线偏右约 1.3 厘米，梯形右腰有约 1.5 厘米缺口，整幅再倾斜 6.5 度。
            var boardInk = new[] {
                new Vector2[] { new(-.091f,.081f), new(-.062f,.087f), new(-.025f,.085f),
                    new(.018f,.084f), new(.059f,.080f), new(.090f,.087f), new(.045f,.083f),
                    new(.004f,.088f), new(-.041f,.090f), new(-.079f,.086f), new(-.050f,.082f),
                    new(.011f,.084f), new(.073f,.088f) },
                new Vector2[] { new(.083f,.083f), new(.022f,.080f), new(-.035f,.084f) },
                new Vector2[] { new(.013f,.086f), new(.016f,.054f), new(.010f,.023f), new(.014f,-.022f) },
                new Vector2[] { new(-.025f,-.024f), new(.006f,-.021f), new(.034f,-.024f), new(.043f,-.046f) },
                new Vector2[] { new(.049f,-.060f), new(.062f,-.083f), new(.021f,-.087f),
                    new(-.053f,-.082f), new(-.043f,-.057f), new(-.026f,-.025f) }
            };
            float angle = 6.5f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            yield return ("一厘米浮动偏心断腰倾斜6.5度", true, boardInk.Select(stroke =>
                stroke.Select(p => new Vector2(cos * p.x - sin * p.y, sin * p.x + cos * p.y)).ToArray()).ToArray());
            // 合成一条独立的三笔笔迹，包括横杆起伏、略偏斜的力线、左右不对称的闭合梯形。
            // 此样本检验小幅描画误差，与真人录制分开报告。
            yield return ("轻微偏移与抖动的合成轨迹", true, new[] {
                new Vector2[] { new(-.084f,.081f), new(-.046f,.083f), new(-.023f,.082f),
                    new(0,.086f), new(.038f,.084f), new(.079f,.082f) },
                new Vector2[] { new(-.002f,.083f), new(.001f,.051f), new(0,.022f),
                    new(.004f,.001f), new(.002f,-.018f) },
                new Vector2[] { new(-.032f,-.018f), new(.028f,-.021f), new(.044f,-.048f),
                    new(.058f,-.084f), new(.013f,-.082f), new(-.057f,-.081f),
                    new(-.045f,-.05f), new(-.032f,-.019f) }
            });
            var bar = new Vector2[] { new(-1,1), new(1,1) };
            yield return ("只有横杆", false, new[] { bar });
            yield return ("只有横杆与垂线", false, new[] { bar, new Vector2[] { new(0,1), new(0,-.25f) } });
            yield return ("缺整根垂线", false, new[] { top, weight });
            yield return ("缺顶部横杆", false, new[] { shaft, weight });
            yield return ("只来回描同一横五遍", false, Enumerable.Repeat(top, 5).ToArray());
            yield return ("完整符形多出一条贯穿斜线", false,
                reference.Concat(new[] { new Vector2[] { new(-.8f,.79f), new(.69f,-.80f) } }).ToArray());
            yield return ("交叉乱画", false, new[] {
                new Vector2[] { new(-1,1), new(.75f,-.92f), new(-.6f,-.3f), new(.7f,.85f),
                    new(-.9f,-.7f), new(.6f,-.12f), new(-.55f,.6f) }
            });
            var circle = new Vector2[49];
            for (int i = 0; i < circle.Length; ++i)
            {
                float theta = i * (2 * Mathf.PI / (circle.Length - 1));
                circle[i] = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            }
            yield return ("闭合圆", false, new[] { circle });
            foreach (var sample in QianjunStructureCases(reference)) yield return sample;
        }

        internal static Vector2[][] GameProjection()
        {
            // 1536 × 816 游戏画面的手工读取像素。仅反转屏幕 Y 并等比缩放为可在测试板上播放的大小。
            // 画面没有板面标定、笔顺或时间戳，分笔和米制缩放都是近似值。
            return new[] {
                new Vector2[] { new(1018,130), new(1140,145), new(1220,148), new(1330,141), new(1480,125) },
                new Vector2[] { new(1257,151), new(1270,281), new(1274,348), new(1264,376) },
                new Vector2[] { new(1116,373), new(1170,380), new(1264,375), new(1348,375) },
                new Vector2[] { new(1354,380), new(1400,491), new(1443,561) },
                new Vector2[] { new(1443,561), new(1250,556), new(1119,551), new(1030,535) },
                new Vector2[] { new(1026,530), new(1074,479), new(1126,408) }
            }.Select(stroke => stroke.Select(p => new Vector2(p.x - 1250, 350 - p.y) * .0004f).ToArray()).ToArray();
        }

        static IEnumerable<(string name, bool accept, Vector2[][] ink)> QianjunStructureCases(Vector2[][] reference)
        {
            var projected = GameProjection();
            yield return ("游戏截图六段可见墨迹投影近似", true, projected);
            yield return ("投影笔迹缺少整根竖笔", false, projected.Where((stroke, i) => i != 1).ToArray());
            yield return ("投影笔迹缺少整条梯底", false, projected.Where((stroke, i) => i != 4).ToArray());
            yield return ("投影笔迹缺梯顶变为U形底座", false, projected.Where((stroke, i) => i != 2).ToArray());
            yield return ("投影笔迹叠加贯穿对角", false, projected.Concat(new[] {
                new[] { projected[0][0], projected[3][^1] }
            }).ToArray());
            var brokenShaft = (Vector2[][])projected.Clone();
            var shaft = projected[1];
            brokenShaft[1] = new[] { Vector2.Lerp(shaft[0], shaft[^1], .29f), Vector2.Lerp(shaft[0], shaft[^1], .71f) };
            yield return ("投影竖笔上下断开仅余中央四成", false, brokenShaft);

            // 独立于截图的保留样本。外框保持固定，单独改变竖笔高度、梯形上下边宽度和轻微偏心。
            Vector2[][] Shape(float shoulderHeight, float upperHalfWidth, float baseHalfWidth, float center)
            {
                return new[] {
                    new Vector2[] { new(-.85f,.85f), new(.85f,.85f) },
                    new Vector2[] { new(center,.85f), new(center,shoulderHeight) },
                    new Vector2[] { new(center-upperHalfWidth,shoulderHeight), new(center+upperHalfWidth,shoulderHeight),
                        new(center+baseHalfWidth,-.85f), new(center-baseHalfWidth,-.85f), new(center-upperHalfWidth,shoulderHeight) }
                };
            }
            yield return ("结构保留竖笔占半高且梯底较宽", true, Shape(0f,.43f,.75f,.015f));
            yield return ("结构保留长竖浅梯略偏右", true, Shape(-.33f,.32f,.72f,.07f));
            yield return ("结构保留较短竖与较高梯略偏左", true, Shape(.04f,.40f,.63f,-.04f));
            yield return ("结构保留窄台短底", true, Shape(-.07f,.26f,.50f,-.025f));
            yield return ("上下两腰均向外张开的梯形", true, Shape(-.20f,.35f,.53f,0));
            yield return ("梯顶梯底同时加宽的正梯", true, Shape(-.20f,.44f,.70f,0));
            yield return ("底座误画成等宽直筒", false, Shape(-.20f,.44f,.44f,0));
            yield return ("底座两腰向下反收", false, Shape(-.20f,.48f,.37f,0));
            yield return ("分笔短线仍画出每边八成", true, Dashed(reference, 4, .80f));
            yield return ("三十六条短线只画出每边四分之一", false, Dashed(reference, 6, .25f));
            var bentDashes = Dashed(reference, 4, .35f).Select(dash =>
            {
                var direction = (dash[1] - dash[0]).normalized;
                var middle = (dash[0] + dash[1]) * .5f + .021f * new Vector2(-direction.y, direction.x);
                return new[] { dash[0], middle, dash[1] };
            }).ToArray();
            yield return ("二十四条零碎折线拼轮廓", false, bentDashes);
            var shaftStart = reference[1][0];
            var shaftEnd = reference[1][^1];
            yield return ("竖笔两头抵齐但中央缺三分之一", false, new[] {
                reference[0], new[] { shaftStart, Vector2.Lerp(shaftStart, shaftEnd, .33f) },
                new[] { Vector2.Lerp(shaftStart, shaftEnd, .68f), shaftEnd }, reference[2]
            });
        }

        static Vector2[][] Dashed(Vector2[][] shape, int piecesPerEdge, float paintedFraction)
        {
            var pieces = new List<Vector2[]>();
            foreach (var stroke in shape)
                for (int edge = 1; edge < stroke.Length; ++edge)
                    for (int i = 0; i < piecesPerEdge; ++i)
                    {
                        float mid = (i + .5f) / piecesPerEdge;
                        float half = paintedFraction / (2f * piecesPerEdge);
                        pieces.Add(new[] {
                            Vector2.Lerp(stroke[edge - 1], stroke[edge], mid - half),
                            Vector2.Lerp(stroke[edge - 1], stroke[edge], mid + half)
                        });
                    }
            return pieces.ToArray();
        }

        internal static IEnumerable<(string name, bool accept, Vector2[][] ink)> SquareCases(Vector2[][] reference)
        {
            var corner = reference[0];
            yield return ("原始方直", true, reference);
            yield return ("两边分笔倒序", true, new[] { new[] { corner[2], corner[1] }, new[] { corner[1], corner[0] } });
            yield return ("部分重描略有偏移", true, new[] {
                corner, new Vector2[] { new(.28f,-.76f), new(-.64f,-.83f) },
                new Vector2[] { new(-.76f,-.55f), new(-.80f,.23f) }
            });
            yield return ("圆角方直", true, new[] { new Vector2[] {
                new(-.78f,.77f), new(-.81f,-.55f), new(-.79f,-.67f), new(-.73f,-.74f),
                new(-.64f,-.78f), new(-.53f,-.80f), new(.79f,-.78f)
            } });
            yield return ("缺整条横边", false, new[] { new[] { corner[0], corner[1] } });
            yield return ("重复描单根竖五遍", false, Enumerable.Repeat(new[] { corner[0], corner[1] }, 5).ToArray());
            yield return ("左右镜像方直", false, new[] { corner.Select(p => new Vector2(-p.x, p.y)).ToArray() });
            yield return ("旋转九十度方直", false, new[] { corner.Select(p => new Vector2(p.y, -p.x)).ToArray() });
            yield return ("旋转半圈方直", false, new[] { corner.Select(p => -p).ToArray() });
            yield return ("多一根长斜线闭成三角", false, new[] { corner, new[] { corner[2], corner[0] } });
            yield return ("方直误画成三边U形", false, new[] { new Vector2[] {
                new(-.81f,.78f), new(-.79f,-.67f), new(-.72f,-.78f), new(.65f,-.81f),
                new(.78f,-.73f), new(.81f,.75f)
            } });
            yield return ("长折返乱画", false, new[] { new Vector2[] {
                new(-.83f,.73f), new(.77f,-.79f), new(-.56f,-.63f), new(.73f,.68f), new(-.76f,-.41f)
            } });
        }
    }
}
