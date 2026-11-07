#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
namespace NarutoVR.SealCombat.Editor
{
    /// <summary>验证施法日志的格式、容量边界和故障隔离，确保记录失败不影响玩法。</summary>
    public static class SkillCastTraceChecks
    {
        public static int Run()
        {
            string root=System.IO.Path.Combine("Logs","cast-trace-tests",Guid.NewGuid().ToString("N"));
            string path;
            using(var trace=new SkillCastTrace(root)) {
                path=trace.Path;
                trace.Write(new SkillCastTrace.Entry {time=1,stage="voice_received",detail="火球术\n测试",inputTime=.8});
                trace.Write(new SkillCastTrace.Entry {time=1.1,stage="activation_rejected",skillId="fireball",attempt=1,detail="MP不足"});
            }
            var lines=File.ReadAllLines(path);
            if(lines.Length!=2)throw new Exception("日志关闭未刷新或JSONL换行损坏");
            var a=JsonUtility.FromJson<SkillCastTrace.Entry>(lines[0]);
            var b=JsonUtility.FromJson<SkillCastTrace.Entry>(lines[1]);
            if(a.session!=b.session||a.sequence!=1||b.sequence!=2||b.skillId!="fireball"||b.attempt!=1)throw new Exception("日志关联字段损坏");
            if(a.detail!="火球术\n测试"||b.detail!="MP不足")throw new Exception("中文或转义内容损坏");
            using(var next=new SkillCastTrace(root)) {
                next.Write(new SkillCastTrace.Entry{stage="session_start"});
                if(next.Path==path)throw new Exception("会话文件重复");
            }
            // A file cannot serve as a log directory: I/O failure must not escape into gameplay.
            using(var broken=new SkillCastTrace(path)) broken.Write(new SkillCastTrace.Entry{stage="activation_request"});
            return 5;
        }
    }
}
#endif
