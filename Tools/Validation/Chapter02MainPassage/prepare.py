"""Prepare the existing timestamped hand/pen replay for the main story crossing in an isolated copy."""
import sys
from pathlib import Path

repo = Path(__file__).resolve().parents[3]
project = Path(sys.argv[1]).resolve()
assert project != repo and "ChapterPassageFormalQA" in (project/"ProjectSettings/ProjectSettings.asset").read_text(encoding="utf8")
base = repo/"Tools/Validation/Evidence/Chapter02Assembly/passage-branch-1126/Chapter02PassageReplay.cs"
s = base.read_text(encoding="utf8")
s = s.replace("Chapter02PassageReplay", "Chapter02MainPassageReplay").replace("CH02.Passage.capture", "CH02.MainPassage.capture").replace("Logs/ChapterPassage-", "Logs/MainPassage-")
a = s.index("            var marks=l.repairedDeck")
b = s.index("            passage=", a)
s = s[:a]+"""            var marks=l.repairedDeck.transform.parent.Find("断桥 · 通路符两岸").GetComponentsInChildren<PassageGlyphAnchor>();
            a=marks.Single(x=>x.name=="近岸铜榫");b=marks.Single(x=>x.name=="对岸铜榫");
"""+s[b:]
s=s.replace("a,b,c;", "a,b;").replace("!pen.enabled&&!a.Available","!pen.enabled&&a.Available")
a=s.index("            foreach(var x in Take(\"square\")")
b=s.index("            Point(a);",a)
s=s[:a]+"""            Ch02BronzeMount.Verify(l.gameObject.scene);
            Check(l.button.cap.name=="青铜压面" && l.panel.IsWaiting && l.panel.template==passage,"真实青铜压面由右手指尖按下，正式石案面板展示通路符");
            var paths=l.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshFilter>(true))
                .Where(m=>m.sharedMesh).Select(m=>AssetDatabase.GetAssetPath(m.sharedMesh)).ToArray();
            Check(paths.Any(x=>x.EndsWith("/CH02BankAWithoutFloorGuide.asset")) && paths.Any(x=>x.EndsWith("/CH02BankBWithoutFloorGuide.asset")),
                "两岸使用已清除旧地线的网格");
            Check(l.player.GetComponentsInChildren<MonoBehaviour>(true).Any(x=>x && x.GetType().Name=="Chapter02GoldTrail") ||
                l.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)).Any(x=>x && x.GetType().Name=="Chapter02GoldTrail"),
                "正式章节保留金色浮光引导组件");
            Check(l.drawingStage is Chapter02PassageStage stage && stage.passage==passage && stage.socketPrefab,"正式阶段资产直接绑定通路符和铜榫");
            var crossingRoot=a.transform.parent;
            Check(!crossingRoot.GetComponent<NarutoVR.SealCombat.NarrationController>(), "未配置旁白时，石案绘符不创建播放源");
            foreach(var skillId in new[]{"fireball","thunder.sword","breath.stream","breath.burst"})
            {
                var skill=p.skills.catalog.skills.Single(s=>s && s.skillId==skillId);
                Check(skill.aimProfile && skill.aimProfile.showReticle,"正式玩家目录 "+skillId+" 准心="+skill.aimProfile.name+" 可用="+p.IsSkillAvailable(skillId));
            }
            foreach(var x in Take("square"))yield return x;
            foreach(var x in Ink(l.panel.board,passage,"square"))yield return x;
            for(int i=0;i<150&&!GameObject.Find("通路符 · 右手金光");i++)yield return null;
            Check(p.StageIndex==1 && !l.repairedDeck.activeSelf && l.brokenBarrier.activeSelf && matches[passage]==1,"画对只进入选岸，断桥仍封闭");
            Check(!crossingRoot.GetComponent<NarutoVR.SealCombat.NarrationController>(), "未配置旁白时，认符后直接进入选岸");
            foreach(var x in Stow(true))yield return x;
            Check(GameObject.Find("通路符 · 右手金光"),"归笔后保留选岸");
            head.rotation=Quaternion.LookRotation((b.landing.position-a.landing.position).normalized);
"""+s[b:]
a=s.index("            Point(c);")
b=s.index("            mode=PoseMode.Fist;",a)
s=s[:a]+"""            Point(b);foreach(var x in Hold("preview",18))yield return x;
            Check(GameObject.Find("通路符搭路预览")&&p.StageIndex==1,"从石案前穿过隐形防坠挡选中对岸，出现预览");
            Grip(head.position-Vector3.up*.72f+head.forward*.20f,head.rotation,false);
            mode=PoseMode.Rest;
            foreach(var x in Hold("cancel",45))yield return x;
            Check(!GameObject.Find("通路符搭路预览") && l.brokenBarrier.activeSelf && p.StageIndex==1,"取消仍在石案教学，未开桥");
            foreach(var x in Take("ink"))yield return x;
            foreach(var x in Ink(pen.activeBoard,passage,"ink"))yield return x;
            foreach(var x in Stow(true))yield return x;
            Point(a);foreach(var x in Hold("first",18))yield return x;
            Point(b);foreach(var x in Hold("preview",18))yield return x;
"""+s[b:]
a=s.index("            foreach(var x in Walk(b.bank.position")
b=s.index('            foreach(var x in Hold("arrived"',a)
s=s[:a]+"""            var direction=(b.landing.position-a.landing.position).normalized;
            var lineup=a.landing.position-direction*.55f;
            foreach(var x in Walk(lineup,"lineup",.05f))yield return x;
            foreach(var x in Walk(b.landing.position+direction*.65f,"cross",.08f))yield return x;
            var middle=(a.landing.position+b.landing.position)*.5f;
            Check(Physics.Raycast(middle+Vector3.up,Vector3.down,out var surfaceHit,2,~0,QueryTriggerInteraction.Ignore) && surfaceHit.collider==road.GetComponent<Collider>(),"断口中点的可走面就是淡金符桥");
            Check(!l.repairedDeck.GetComponent<MeshRenderer>().enabled && l.repairedDeck.GetComponents<BoxCollider>().All(x=>!x.enabled),"原石桥网格与宽走面已停用");
            Check(p.StageIndex==2 && p.Model.Chapter.IsComplete(l.drawingStage.stageId) && !l.brokenBarrier.activeSelf,"过桥到祭坛阶段");
"""+s[b:]
s=s.replace('if(road.GetComponent<Collider>().enabled)throw', 'if(road.GetComponent<Collider>().enabled || !l.brokenBarrier.activeSelf || p.StageIndex!=1)throw')
s=s.replace('pos=a.landing.position+new Vector3(3.05f,3.3f,-1.85f);look=Vector3.Lerp(a.landing.position,b.landing.position,.59f)+Vector3.right*.65f;', 'look=Vector3.Lerp(a.landing.position,b.landing.position,.5f);pos=look+new Vector3(4.2f,3.5f,2.3f);')
s=s.replace('look=eye+new Vector3(0,-1.1f,2.0f)','look=eye+new Vector3(0,-1.1f,-2.0f)')
s=s.replace('p.autoLoad=false; p.saveFileName="qa-passage-"+id+".json";','p.autoLoad=false;')
s=s.replace("        using var f=new Film(l);\n        foreach(var x in f.Route())yield return x;", """        using(var f=new Film(l))
        {
            foreach(var x in f.Route())yield return x;
        }
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scene/Chapter02_SuspendedPath.unity",
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        for(int i=0;i<160;i++)
        {
            l=Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
            if(l && l.player.CanAct && Object.FindFirstObjectByType<PassageGlyphRoad>())break;
            yield return null;
        }
        var restored=Object.FindObjectsByType<PassageGlyphRoad>(FindObjectsSortMode.None);
        Check(l && l.player.StageIndex==2 && !l.brokenBarrier.activeSelf && restored.Length==1 &&
              restored[0].Progress==1f && restored[0].GetComponent<Collider>().enabled,"重新加载正式场景与祭坛存档，恢复唯一一条完成且可走的淡金符桥");
        Check(!l.button.gameObject.activeSelf && !l.panel.IsWaiting,"已完成存档不重复石案教学");""")
s=s.replace('            right.Set(mode,grip,aim,rot,pinch,true,f.right,origin);', '''            if(mode==PoseMode.Rest)
            {
                // Keep the straight-finger joint sample from Skeleton and move its wrist to the requested location.
                var wrist=(int)HandJointId.HandWristRoot;
                var offset=(origin?origin.InverseTransformPoint(grip):grip)-f.right.joints[wrist].position;
                for(int j=0;j<f.right.joints.Length;j++)f.right.joints[j].position+=offset;
            }
            else right.Set(mode,grip,aim,rot,pinch,true,f.right,origin);''')
out=project/"Assets/scripts/logic/Editor/Chapter02MainPassageReplay.cs"
out.write_text(s,encoding="utf8")
