using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Chapter02PassageBuild
{
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var changed=Chapter02PassageBranch.Apply(scene);
        if(Chapter02PassageBranch.Apply(scene))throw new Exception("重复生成支路");
        if(changed&&!EditorSceneManager.SaveScene(scene))throw new Exception("副本未保存");
        var lesson=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Chapter02BridgeEquipment>(true)).Single();
        var marks=lesson.repairedDeck.GetComponentsInChildren<PassageGlyphAnchor>(true).OrderBy(a=>a.name).ToArray();
        var art=marks.SelectMany(a=>a.GetComponentsInChildren<MeshCollider>(true)).ToArray();
        var log=new List<string>();
        // Temporary in-memory comparison after saving. Only touches the verification scene.
        lesson.repairedDeck.SetActive(true);
        foreach(bool original in new[]{true,false})
        {
            foreach(var shape in art)shape.enabled=original;
            Physics.SyncTransforms();
            int count=0;
            foreach(var a in marks)foreach(var b in marks.Where(x=>x!=a))
            {
                Vector3 forward=(b.landing.position-a.landing.position).normalized;
                Vector3 right=Vector3.Cross(Vector3.up,forward);
                foreach(float offset in new[]{-.16f,-.08f,0,.08f,.16f})
                {
                    Vector3 start=a.landing.position-forward*.30f+right*offset;
                    var hits=Physics.CapsuleCastAll(start+Vector3.up*.245f,start+Vector3.up*1.52f,.22f,
                        forward,Vector3.Distance(a.landing.position,b.landing.position)+.60f,lesson.player.locomotion.obstacleLayers,QueryTriggerInteraction.Ignore);
                    if(hits.Length==0)continue;
                    count++;
                    string blockers=string.Join(" / ",hits.Select(h=>h.collider.name).Distinct());
                    log.Add((original?"旧铜榫":"候选")+" "+a.name+" → "+b.name+" 横向偏移="+offset+" m，接触 "+blockers);
                }
            }
            log.Add((original?"旧铜榫":"候选")+" 30 次扫掠，受阻 "+count);
            if(!original&&count!=0){File.WriteAllLines("Logs/PassageClearance.txt",log);throw new Exception("桥口还有实体阻挡");}
        }
        File.WriteAllLines("Logs/PassageClearance.txt",log);
        Debug.Log("PASSAGE_INSTALL_OK changed="+changed);
    }
}
