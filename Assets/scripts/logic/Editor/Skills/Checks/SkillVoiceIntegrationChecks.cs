using System;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>观察音频、识别模型、UDP 到控制器的完整链路，以真实识别事件验证激活。</summary>
    public static class SkillVoiceIntegrationChecks
    {
        private static bool running,previousBackground;
        private static SkillTestController controller;
        private static Action<bool,string> completed;
        private static int beforeVoice,beforeActivation;
        private static double deadline;
        public static void Begin(Action<bool,string> callback)
        {
            if(!Application.isPlaying||running)throw new InvalidOperationException("语音链路检查需要播放模式且不能重复启动");
            controller=UnityEngine.Object.FindFirstObjectByType<SkillTestController>();
            if(!controller||controller.Engine==null||!controller.voice||!controller.voice.allowWavReplay)
                throw new InvalidOperationException("缺少可运行控制器或WAV回放未允许");
            running=true;completed=callback;previousBackground=Application.runInBackground;
            Application.runInBackground=true;controller.Cancel();
            beforeVoice=controller.VoiceEventCount;beforeActivation=controller.effects.ActivationCount;
            deadline=EditorApplication.timeSinceStartup+25;
            EditorApplication.update+=Poll;AssemblyReloadEvents.beforeAssemblyReload+=Abort;
        }
        private static void Poll()
        {
            if(!Application.isPlaying||!controller){Finish(false,"播放模式已退出");return;}
            if(controller.VoiceEventCount>beforeVoice&&controller.effects.ActivationCount>beforeActivation&&controller.effects.ShieldPoints>0)
            {Finish(true,"实际WAV模型识别与UDP接收已通过目录配方激活护盾");return;}
            if(EditorApplication.timeSinceStartup>=deadline)
                Finish(false,"语音链路检查超时："+controller.voice.Status+"；"+controller.Engine.Status);
        }
        private static void Abort()=>Finish(false,"程序集重载，检查已结束");
        private static void Finish(bool passed,string detail)
        {
            if(!running)return;running=false;
            EditorApplication.update-=Poll;AssemblyReloadEvents.beforeAssemblyReload-=Abort;
            if(controller)controller.Cancel();Application.runInBackground=previousBackground;
            completed?.Invoke(passed,detail);completed=null;controller=null;
        }
    }
}
