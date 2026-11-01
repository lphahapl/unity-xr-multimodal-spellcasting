using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>Bounded streaming JSONL; observes gameplay without changing its decisions.</summary>
    [DisallowMultipleComponent]
    public sealed class ThrowDiagnosticRecorder : MonoBehaviour
    {
        [Header("投掷诊断自动记录（不含音频或图像）")]
        [InspectorName("投掷效果执行器")] public SkillEffects effects;
        [InspectorName("进入播放自动记录")] public bool autoRecord=true;
        [InspectorName("单份记录最长分钟"),Range(1,60)] public float maximumMinutes=15;
        [InspectorName("单份记录最大兆字节"),Range(1,128)] public int maximumMegabytes=64;
        public bool IsRecording=>writer!=null;
        public string FilePath { get; private set; }="";
        public string Status { get; private set; }="尚未开始";
        public int Rows { get; private set; }
        StreamWriter writer;
        SkillEffects subscribed;
        double started,flushAt,lastObserved=double.NaN;
        string lastShot,lastGate,lastShotConfig;
        long bytes;
        [Serializable] sealed class Header { public string type="header",utc,unity,platform,clock="realtime seconds",positionUnit="world metres",score="0..100 weighted action match; NOT probability"; public int schemaVersion=2; }
        [Serializable] sealed class Config { public string type="shot_config",shotId; public HandThrowSettings settings; public float flightSpeed; }
        [Serializable] sealed class End { public string type="end",reason; public int rows; }

        void OnEnable() { Subscribe(); }
        void Start() { Subscribe(); if(autoRecord&&!IsRecording) Begin(); }
        void Subscribe()
        {
            if(!effects) effects=GetComponent<SkillEffects>();
            if(subscribed==effects) return;
            if(subscribed) subscribed.ThrowObserved-=Observe;
            subscribed=effects;
            if(subscribed) subscribed.ThrowObserved+=Observe;
        }
        void Update()
        {
            if(!IsRecording) return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(now-started>=Mathf.Max(1,maximumMinutes)*60) { Stop("达到时长上限"); return; }
            if(now>=flushAt) { try { writer.Flush(); flushAt=now+1; } catch(Exception e) { Fail(e); } }
        }
        public void Begin() => BeginInDirectory(Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/throw-captures")) : Path.Combine(Application.persistentDataPath,"Diagnostics/throw-captures"));
        public void BeginInDirectory(string directory)
        {
            if(IsRecording) return;
            Subscribe();
            try
            {
                Directory.CreateDirectory(directory);
                FilePath=Path.Combine(directory,"throw-score-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".jsonl");
                writer=new StreamWriter(new FileStream(FilePath,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false),16384);
                Rows=0; bytes=0; lastObserved=double.NaN; lastGate=lastShot=lastShotConfig=null;
                started=Time.realtimeSinceStartupAsDouble; flushAt=started+1;
                Write(JsonUtility.ToJson(new Header{utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,platform=Application.platform.ToString()}));
                Status="正在自动记录";
            }
            catch(Exception e) { Fail(e); }
        }
        public void Observe(HandThrowObservation observation)
        {
            if(!IsRecording) return;
            string gate=observation.eventType+"|"+observation.status;
            bool repeat=observation.shotId==lastShot && gate==lastGate && (!observation.hasSample || observation.observed==lastObserved);
            if(repeat) return;
            if(bytes>=(long)Mathf.Max(1,maximumMegabytes)*1024*1024-32768) { Stop("达到文件大小上限"); return; }
            if(lastShotConfig!=observation.shotId)
            {
                Write(JsonUtility.ToJson(new Config{shotId=observation.shotId,settings=effects?effects.ActiveThrowSettings:null,flightSpeed=effects?effects.ActiveProjectileSpeed:0}));
                lastShotConfig=observation.shotId;
            }
            lastShot=observation.shotId; lastObserved=observation.observed; lastGate=gate;
            Write(JsonUtility.ToJson(observation));
        }
        void Write(string json)
        {
            if(!IsRecording) return;
            try { writer.WriteLine(json); bytes+=Encoding.UTF8.GetByteCount(json)+Environment.NewLine.Length; Rows++; }
            catch(Exception e) { Fail(e); }
        }
        public void Stop() => Stop("手动停止");
        public void Stop(string reason)
        {
            if(!IsRecording) return;
            Write(JsonUtility.ToJson(new End{reason=reason,rows=Rows}));
            try { writer?.Dispose(); writer=null; Status="已保存："+reason; }
            catch(Exception e) { Fail(e); }
        }
        void Fail(Exception e)
        {
            try { writer?.Dispose(); } catch { }
            writer=null; Status="记录失败："+e.Message;
            Debug.LogWarning("投掷诊断"+Status,this);
        }
        void OnDisable() { if(subscribed)subscribed.ThrowObserved-=Observe; subscribed=null; Stop("组件关闭或退出播放"); }
        void OnApplicationQuit() => Stop("应用退出");
    }
}
