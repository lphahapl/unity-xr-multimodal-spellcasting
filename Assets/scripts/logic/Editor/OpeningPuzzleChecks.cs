using System;
using NarutoVR.SealCombat.Rpg;

namespace NarutoVR.SealCombat.Editor
{
    public static class OpeningPuzzleChecks
    {
        public static int Run()
        {
            int count = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("方圆仪：" + label);
                count++;
            }
            var discs = new DiscAlignmentState();
            discs.Restore(false, 45, 20);
            discs.Turn(true, -225);
            Check(discs.OuterDegrees == -180 && discs.InnerDegrees == -205, "外盘同步带动内盘");
            discs.Turn(false, 30);
            Check(discs.OuterDegrees == -180 && discs.InnerDegrees == -175, "内盘单独调整至五度边界");
            Check(!discs.TrySettle(true, 5, .6f, 1), "未松手不能结算");
            discs.Release();
            Check(!discs.TrySettle(true, 5, .6f, .4f), "松手后不足稳定时间");
            discs.BeginTurn();
            discs.Release();
            Check(!discs.TrySettle(true, 5, .6f, .3f), "再抓取清除前次计时");
            Check(!discs.TrySettle(false, 5, .6f, 1), "手部仍被占用");
            Check(!discs.TrySettle(true, 5, .6f, .3f) && discs.TrySettle(true, 5, .6f, .3f), "连续稳定时间达标");
            Check(discs.Solved && discs.OuterDegrees == -180 && discs.InnerDegrees == -180, "吸附到最近的负向刻度");
            Check(!discs.TrySettle(true, 5, .6f, 1), "完成通知只产生一次");
            discs.Restore(false, 0, 6);
            discs.Release();
            Check(!discs.TrySettle(true, 5, .6f, 1), "超过角度公差");
            discs.Restore(true, 45, 20);
            Check(discs.Solved && discs.OuterDegrees == 0 && discs.InnerDegrees == 0, "旧档恢复已解盘面");
            return count;
        }
    }
}
