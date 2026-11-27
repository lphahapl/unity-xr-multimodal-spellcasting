using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PassageHandwritingChecks
{
    [Serializable] sealed class Point { public float x,y; public double t; }
    [Serializable] sealed class Trace { public Point[] inner,outer,secondInner; public double decisionAfterStart,secondDecisionAfterStart; }

    public static void Run()
    {
        var root=new GameObject("通路符原时序复核");
        try
        {
            var board=root.AddComponent<GlyphBoard>();
            var data=JsonUtility.FromJson<Trace>(File.ReadAllText("Logs/passage-handwriting.json"));
            var template=AssetDatabase.LoadAssetAtPath<GlyphTemplate>("Assets/Art/Glyphs/Passage.asset");
            var qianjun=AssetDatabase.LoadAssetAtPath<GlyphTemplate>("Assets/Art/Glyphs/Qianjun.asset");
            double t=0;
            string Hint() => root.GetComponentsInChildren<Text>().Single(x=>x.name=="笔顺").text;
            void Check(bool pass,string message)
            {
                if (!pass) throw new InvalidOperationException(message);
                Debug.Log("PASSAGE_INK_CHECK "+message);
            }
            void Sample(Point point,float z=0) => board.Sample(new Vector3(point.x,point.y,z),true,t);
            void Draw(Point[] stroke)
            {
                foreach (var point in stroke) { t+=.034;Sample(point); }
                t+=.033;Sample(stroke[stroke.Length-1],-.032f);
            }
            GlyphTemplate Judge() { t+=1;Check(board.TrySubmit(t,out var result),"停笔完成一次辨符");return result; }
            board.Open(new[]{template},true);
            foreach(var point in data.inner) { t=point.t;Sample(point); }
            Check(!board.TrySubmit(t+.88,out _),"真实 44 点，停笔 0.88 秒尚不提交");
            t=data.decisionAfterStart;
            Check(board.TrySubmit(t,out var reject)&&!reject&&board.LastScore==0&&board.PointCount==44&&
                board.Status.Contains("内横已留")&&Hint().Contains("门框"),
                $"原 44 点 {data.inner.Last().t:F3} 秒，原决策间隔 {t-data.inner.Last().t:F3} 秒，{board.Status}，得分 {board.LastScore:F3}");
            t+=.1;Sample(data.inner.Last(),-.032f);
            Draw(data.outer);
            Check(Judge()==template,"原内横补独立外框 "+board.LastScore.ToString("F3"));
            board.Open(new[]{template},true);
            Draw(data.outer);
            Check(Judge()==null&&Hint().Contains("2/2")&&board.Status.Contains("门框已留"),
                "先画外框后提示第二笔起止和内横，外框单独不通过");
            // Reach the undo and clear buttons with the actual nib hit-test.
            t+=.05;board.Sample(new Vector3(-.119f,-.179f,0),true,t);
            Check(board.PointCount==0&&Hint().Contains("1/2"),"撤销还原第一笔");
            Draw(data.outer.Reverse().ToArray());Draw(data.inner.Reverse().ToArray());
            Check(Judge()==template,"两笔逆向仍辨认 "+board.LastScore.ToString("F3"));
            board.Open(new[]{template},true);
            Draw(data.inner.Select(p=>new Point{x=p.y,y=p.x}).ToArray());
            Check(!Judge() && board.Status.Contains("门框未描全"),"仅画竖线拒识并提示缺框");
            t+=.05;board.Sample(new Vector3(0,-.179f,0),true,t);
            Check(board.PointCount==0 && Hint().Contains("1/2"),"清空还原第一笔");
            board.Open(new[]{qianjun},true);
            Check(!root.GetComponentsInChildren<Text>().Any(x=>x.name=="笔顺"),"千钧剧情板没有通路提示");
            board.Open(new[]{template,qianjun},false);
            Check(!root.GetComponentsInChildren<Text>().Any(x=>x.name=="笔顺"),"主动板保留自由绘制");
            Draw(data.outer);Draw(data.inner);Check(Judge()==template,"主动板同形正确归类");
            board.Open(new[]{template},true);
            foreach(var point in data.secondInner) { t=point.t;Sample(point); }
            Check(board.TrySubmit(data.secondDecisionAfterStart,out var secondResult) && !secondResult &&
                board.PointCount==42 && board.Status.Contains("内横已留"),
                $"20:38 现场 42 点 {t:F3} 秒，停笔 {data.secondDecisionAfterStart-t:F3} 秒，{board.Status}");
            NarutoVR.SealCombat.Editor.GlyphBoardChecks.Run();
            Debug.Log("PASSAGE_INK_CHECK PASS END");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
