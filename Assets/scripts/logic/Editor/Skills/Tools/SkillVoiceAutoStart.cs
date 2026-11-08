#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace NarutoVR.SealCombat.Editor
{
    // 语音服务归属仅用于桌面开发，Android 构建通过编辑器目录边界排除该代码。
    [InitializeOnLoad]
    /// <summary>管理桌面开发阶段本机语音服务的启动与归属，入口仅编译进 Unity 编辑器。</summary>
    public static class SkillVoiceAutoStart
    {
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/SkillVoice"));
        const string Key="NarutoVR.SkillVoice.Owner";
        static Process launcher;
        static bool shuttingDown;
        static double next;
        static string error="";
        static readonly object Gate=new();
        static SkillVoiceAutoStart() {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=s=> { if(s==PlayModeStateChange.ExitingPlayMode) StopOwned(); if(s==PlayModeStateChange.EnteredPlayMode) {shuttingDown=false;next=0;} };
            EditorApplication.quitting+=StopOwned;
        }
        static string WritePlayConfiguration(SkillVoiceReceiver receiver)
        {
            foreach (var controller in UnityEngine.Object.FindObjectsByType<SkillTestController>(FindObjectsSortMode.None))
            {
                if (controller.voice != receiver || !controller.catalog) continue;
                string json = SkillConfigurationSetup.BuildVoiceJson(controller.catalog, receiver.receivePort, receiver.servicePort);
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/SkillVoice"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "play-config.json");
                File.WriteAllText(path, json, new UTF8Encoding(false));
                return path;
            }
            return null;
        }

        static Process Run(string file,string token,bool capture,string config = null) {
            string arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(Root,file)+"\" -OwnerToken \""+token+"\"";
            if (config != null) arguments += " -Config \"" + config + "\"";
            var info=new ProcessStartInfo("powershell.exe", arguments) {
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Root,RedirectStandardError=capture
            };
            var p=new Process {StartInfo=info};
            if(capture) p.ErrorDataReceived+=(s,e)=> {if(!string.IsNullOrEmpty(e.Data)) lock(Gate) error=e.Data.Length>400?e.Data.Substring(0,400):e.Data;};
            p.Start(); if(capture)p.BeginErrorReadLine(); return p;
        }
        static void Tick() {
            if(shuttingDown || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1;
            var receiver=UnityEngine.Object.FindFirstObjectByType<SkillVoiceReceiver>();
            if(!receiver || !receiver.isActiveAndEnabled || !receiver.autoStartInEditor)return;
            if(receiver.HasServiceHeartbeat) {receiver.LauncherStatus="已连接本机服务";return;}
            if(launcher!=null && !launcher.HasExited) {receiver.LauncherStatus="正在启动中文语音服务";return;}
            if(launcher!=null) {
                int code=launcher.ExitCode;launcher.Dispose();launcher=null;
                if(code!=0) {lock(Gate) receiver.LauncherStatus="启动未成功，15秒后重试："+error; next=EditorApplication.timeSinceStartup+15;return;}
            }
            try {
                string token=SessionState.GetString(Key,"");
                if(token.Length==0) {token=Guid.NewGuid().ToString("N");SessionState.SetString(Key,token);}
                lock(Gate)error="";
                launcher=Run("Start.ps1",token,true,WritePlayConfiguration(receiver));
                receiver.LauncherStatus="已请求自动启动；等待设备和心跳";
                next=EditorApplication.timeSinceStartup+10;
            } catch(Exception e) {receiver.LauncherStatus="启动失败，15秒后重试："+e.Message;next=EditorApplication.timeSinceStartup+15;}
        }
        static void StopOwned() {
            shuttingDown=true;
            string token=SessionState.GetString(Key,""); if(token.Length==0)return;
            try {
                if(launcher!=null && !launcher.HasExited)launcher.WaitForExit(3000);
                if(launcher!=null) {launcher.Dispose();launcher=null;}
                // Stop.ps1 verifies owner token and actual command line before touching a process.
                using(var stop=Run("Stop.ps1",token,false))stop.WaitForExit(3000);
            } catch(Exception e) {Debug.LogWarning("[语音启动管理] 停止失败："+e.Message);}
            next=0;
        }
    }
}
#endif
