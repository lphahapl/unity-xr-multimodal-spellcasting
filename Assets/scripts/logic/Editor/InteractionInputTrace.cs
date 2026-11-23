#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using NarutoVR.SealCombat.Rpg;
namespace NarutoVR.SealCombat.Editor {
    public static class InteractionInputTrace {
        static double end,next; static string path;
        [Serializable] class Row { public double time; public string skill,gates,seal,ui,input,handPose,movement; public Vector3 rigPosition; }
        [MenuItem("Tools/NarutoVR/线性RPG/采样手部与移动（30秒）", true)]
        static bool CanBegin() => EditorApplication.isPlaying;
        [MenuItem("Tools/NarutoVR/线性RPG/采样手部与移动（30秒）")]
        public static void Begin() {
            Directory.CreateDirectory("Logs/interaction-input");
            path="Logs/interaction-input/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".jsonl";
            end=EditorApplication.timeSinceStartup+30; next=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick() {
            if(EditorApplication.timeSinceStartup>end || !EditorApplication.isPlaying) {EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.1;
            var c=UnityEngine.Object.FindFirstObjectByType<LinearRpgController>(); if(!c || !c.skills)return;
            var v=c.GetComponentInChildren<RpgSessionView>(true); var m=c.wristMenu;
            var row=new Row {time=Time.realtimeSinceStartupAsDouble,skill=c.skills.LastMessage,
                handPose=c.HandPoseStatus, movement=c.locomotion?c.locomotion.BuildDebugDetails(Time.realtimeSinceStartupAsDouble):"",
                rigPosition=c.locomotion && c.locomotion.rigRoot?c.locomotion.rigRoot.position:default,
                gates=c.skills.Engine?.ObservationDiagnostics,seal=v?v.BuildSealDetails():"无视图",ui=m?m.DebugDetails:"无菜单",input=m?JsonUtility.ToJson(m.LatestInput):""};
            File.AppendAllText(path,JsonUtility.ToJson(row)+Environment.NewLine);
        }
    }
}
#endif
