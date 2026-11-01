#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace NarutoVR.SealCombat.Editor
{
    public static class WristViewLifecycleChecks
    {
        const string RootName="腕部界面 · 自有世界根";
        static GameObject[] Roots() => Resources.FindObjectsOfTypeAll<GameObject>().Where(x=>x.name==RootName && x.scene.IsValid()).ToArray();
        public static string Inspect()
        {
            var roots=Roots(); var views=Resources.FindObjectsOfTypeAll<WristMenuView>().Where(x=>x.gameObject.scene.IsValid()).ToArray();
            return $"播放={Application.isPlaying}；场景未保存={SceneManager.GetActiveScene().isDirty}；View数量={views.Length}；世界根数量={roots.Length}\n"+
                string.Join("\n",roots.Select(r=>$"根={r.GetInstanceID()}；场景={r.scene.name}；活动={r.activeSelf}；flags={r.hideFlags}；所有者="+
                    string.Join(",",views.Where(v=>v.OwnedRoot==r).Select(v=>v.name))));
        }
        public static int RequireNoOrphans()
        {
            var views=Resources.FindObjectsOfTypeAll<WristMenuView>();
            int orphans=Roots().Count(r=>!views.Any(v=>v && v.OwnedRoot==r));
            if(orphans>0) throw new InvalidOperationException($"当前场景还有 {orphans} 个无所有者腕部UI根");
            return 1;
        }
        public static int Run()
        {
            if(Application.isPlaying) throw new InvalidOperationException("生命周期检查需要编辑模式");
            var before=Roots().Select(x=>x.GetInstanceID()).ToHashSet();
            var fixture=new GameObject("腕饰生命周期检查"){hideFlags=HideFlags.HideAndDontSave};
            int after;
            try
            {
                var view=fixture.AddComponent<WristMenuView>(); view.audioFeedback=false;
                var controller=fixture.AddComponent<WristMenuController>(); controller.view=view;
                view.EnsureBuilt();
                Object.DestroyImmediate(fixture);
                after=Roots().Count(x=>!before.Contains(x.GetInstanceID()));
                Directory.CreateDirectory("Logs/wrist-lifecycle-20261031");
                File.WriteAllText("Logs/wrist-lifecycle-20261031/probe.txt",$"销毁拥有者后新增残留根={after}\n"+Inspect());
            }
            finally
            {
                if(fixture) Object.DestroyImmediate(fixture);
                foreach(var r in Roots()) if(!before.Contains(r.GetInstanceID())) Object.DestroyImmediate(r);
            }
            if(after!=0) throw new InvalidOperationException($"腕饰销毁后仍残留 {after} 个世界根");
            var cold=new GameObject("未显示的腕饰检查"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var view=cold.AddComponent<WristMenuView>();
                view.SetDecoration(Pose.identity,false); view.SetPointer(default,default,false,false);
                view.SetOpeningHint(Pose.identity,"",0,false); view.HideLauncher(); view.HidePanel(); view.SetHover(null,0);
                if(Roots().Any(x=>!before.Contains(x.GetInstanceID()))) throw new InvalidOperationException("只调用隐藏操作却新建了腕部世界根");
            }
            finally { Object.DestroyImmediate(cold); }
            var repeated=new GameObject("重复创建与重载检查"){hideFlags=HideFlags.HideAndDontSave};
            int count=2;
            try
            {
                var controller=repeated.AddComponent<WristMenuController>();
                var view=repeated.AddComponent<WristMenuView>(); controller.view=view; view.audioFeedback=false;
                for(int i=0;i<3;i++)
                {
                    view.EnsureBuilt(); view.EnsureBuilt();
                    if(Roots().Count(x=>!before.Contains(x.GetInstanceID()))!=1 || view.Targets.Count!=9)
                        throw new InvalidOperationException("重复构建只能有一套根和七个交互目标");
                    WristViewEditorLifetime.ReleaseView(view); WristViewEditorLifetime.ReleaseView(view);
                    if(Roots().Any(x=>!before.Contains(x.GetInstanceID()))) throw new InvalidOperationException("重载清理钩子未释放世界根");
                    count++;
                }
                view.EnsureBuilt(); view.enabled=false;
                if(Roots().Any(x=>!before.Contains(x.GetInstanceID()))) throw new InvalidOperationException("编辑模式禁用视图后仍有残留");
                count++;
                view.enabled=true; view.EnsureBuilt();
            }
            finally { Object.DestroyImmediate(repeated); }
            if(Roots().Any(x=>!before.Contains(x.GetInstanceID()))) throw new InvalidOperationException("逆序组件销毁后残留世界根");
            return count+1;
        }
    }
}
#endif
