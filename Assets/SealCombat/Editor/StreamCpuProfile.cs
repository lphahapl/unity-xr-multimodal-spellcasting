#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
namespace NarutoVR.SealCombat.Editor
{
    internal static class StreamCpuProfile {
        public static void Capture() {
            var totals=new Dictionary<string,double>();
            var frames=new List<double>();
            // Inspect completed frames preceding this command; stop before this diagnostic frame.
            int last=ProfilerDriver.lastFrameIndex-1;
            for(int i=Math.Max(ProfilerDriver.firstFrameIndex,last-29);i<=last;i++){
                using var v=ProfilerDriver.GetRawFrameDataView(i,0);
                if(!v.valid)continue;
                frames.Add(v.frameTimeMs);
                var stack=new Stack<(int end,double ms,string name)>();
                for(int j=0;j<v.sampleCount;j++){
                    while(stack.Count>0 && stack.Peek().end<j)stack.Pop();
                    var name=v.GetSampleName(j);var duration=v.GetSampleTimeMs(j);
                    if(stack.Count>0)totals[stack.Peek().name]-=duration;
                    totals.TryGetValue(name,out double old);totals[name]=old+duration;
                    int descendants=v.GetSampleChildrenCountRecursive(j);
                    if(descendants>0)stack.Push((j+descendants,duration,name));
                }
            }
            var text=new List<string>{DateTime.UtcNow.ToString("O"),"frames="+frames.Count+"; mean frame ms="+(frames.Count>0?frames.Average():0)};
            foreach(var pair in totals.OrderByDescending(p=>p.Value).Take(18))text.Add(pair.Key+" : "+(pair.Value/Math.Max(1,frames.Count)).ToString("F2")+" ms self/frame");
            File.WriteAllLines("Logs/stream-cpu-sample.txt",text);
        }
    }
}
#endif
