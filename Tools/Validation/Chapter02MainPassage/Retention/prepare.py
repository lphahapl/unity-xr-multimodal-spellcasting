"""Install the retention repro and disable desktop XR in an isolated ChapterPassageFormalQA project."""
import re
import subprocess
import sys
from pathlib import Path

here = Path(__file__).resolve().parent
project = Path(sys.argv[1]).resolve()
subprocess.run([sys.executable, str(here.parent/'Handwriting'/'prepare.py'), str(project)], check=True)
settings = project/'Assets/XR/XRGeneralSettingsPerBuildTarget.asset'
text = settings.read_text(encoding='utf8')
text, n = re.subn(r'(m_Name: Standalone Settings\n(?:(?!---)[^\n]*\n)*?  m_InitManagerOnStart:) [01]', r'\1 0', text)
assert n == 1
settings.write_text(text, encoding='utf8')
f=project/'Assets/scripts/logic/Editor/Chapter02MainPassageReplay.cs'
s=f.read_text(encoding='utf8')
def replace(a,b):
    global s
    assert s.count(a)==1, a
    s=s.replace(a,b)
replace('        EditorApplication.EnterPlaymode();','''        var xr=File.ReadAllText("Assets/XR/XRGeneralSettingsPerBuildTarget.asset").Split("m_Name: Standalone Settings")[1].Split("---")[0];
        if(!xr.Contains("m_InitManagerOnStart: 0"))throw new Exception("Standalone XR auto-init must be off on disk");
        EditorApplication.EnterPlaymode();''')
replace('        var l=Object.FindFirstObjectByType<Chapter02BridgeEquipment>();','''        var displays=new List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        Check(!displays.Any(x=>x.running),"Standalone XR auto-init off, running XR displays=0");
        var l=Object.FindFirstObjectByType<Chapter02BridgeEquipment>();''')
replace('            for(int i=0;i<150&&!GameObject.Find("通路符 · 右手金光");i++)yield return null;','''            Check(l.panel.board.IsShowingSuccess,"符成金闪中立即归笔");
            foreach(var x in Stow(true))yield return x;
            for(int i=0;i<150&&!GameObject.Find("通路符 · 右手金光");i++)yield return null;''')
replace('            foreach(var x in Stow(true))yield return x;\n            Check(GameObject.Find("通路符 · 右手金光"),"归笔后保留选岸");','''            var cursor=GameObject.Find("通路符 · 右手金光");
            Check(cursor && !l.equipment.interaction.Held,"金闪内归笔后，空手接续同一次认符");
            var palm=cursor.transform.Find("右手符纹");
            var selected=cursor.transform.Find("已选岸标").GetComponent<LineRenderer>();
            var beam=cursor.transform.Find("食指指向").GetComponent<LineRenderer>();
            IEnumerable InputFor(string name,float seconds)
            {
                double start=Time.realtimeSinceStartupAsDouble;int frames=0;
                while(Time.realtimeSinceStartupAsDouble-start<seconds){yield return null;frames++;}
                var current=GameObject.Find("通路符 · 右手金光");
                var glow=current?current.transform.Find("右手符纹"):null;
                var bankFrame=current?current.transform.Find("已选岸标").GetComponent<LineRenderer>():null;
                var rayView=current?current.transform.Find("食指指向").GetComponent<LineRenderer>():null;
                Log($"INPUT {name} seconds={Time.realtimeSinceStartupAsDouble-start:F3} frames={frames} tracked={p.Input.LatestHand.rightValid} fist={p.Input.LatestHand.rightClosed} owned={p.Input.InteractionOwnsHand} open={p.Input.LatestHand.rightOpen} hand={glow&&glow.gameObject.activeInHierarchy} first={bankFrame&&bankFrame.gameObject.activeInHierarchy&&bankFrame.enabled} preview={(bool)GameObject.Find("通路符搭路预览")} ray={rayView&&rayView.enabled}");
            }
            Grip(head.position-Vector3.up*.8f,head.rotation,false);
            foreach(var x in InputFor("归笔低手保持",1.18f))yield return x;
            Check(cursor && p.Input.InteractionOwnsHand,"未抬手瞄准前，停留在笔套处保留符");
            Point(a);''')
replace('            Point(b);foreach(var x in Hold("preview",18))yield return x;\n            Check(GameObject.Find', '''            mode=PoseMode.Off; foreach(var x in InputFor("第一岸后短失追",.48f))yield return x;
            Check(cursor && selected.enabled && selected.gameObject.activeInHierarchy && !palm.gameObject.activeInHierarchy && !beam.enabled,"失追只隐藏手光和射线，保留第一岸");
            Point(b);foreach(var x in Hold("preview",18))yield return x;
            Check(GameObject.Find''')
replace('            Grip(head.position-Vector3.up*.72f+head.forward*.20f,head.rotation,false);\n            mode=PoseMode.Rest;\n            foreach(var x in Hold("cancel",45))yield return x;', '''            var preview=GameObject.Find("通路符搭路预览");
            aim=head.position+Vector3.up*4;
            foreach(var x in InputFor("射线指空",.55f))yield return x;
            Check(preview.activeInHierarchy && !preview.GetComponent<Collider>().enabled,"指空仍保留同一无碰撞预览");
            Grip(head.position-Vector3.up*.84f,head.rotation,false);
            foreach(var x in InputFor("短暂放低",.28f))yield return x;
            Grip(head.position-Vector3.up*.21f,head.rotation,false);
            foreach(var x in InputFor("重新抬手",.46f))yield return x;
            Check(preview.activeInHierarchy && selected.enabled && p.Input.InteractionOwnsHand,"自然换姿、低手再抬仍有两岸");
            mode=PoseMode.Off;foreach(var x in InputFor("预览阶段失追",1.35f))yield return x;
            Check(preview.activeInHierarchy && !palm.gameObject.activeInHierarchy,"预览失追仍留在世界");
            mode=PoseMode.Fist;foreach(var x in InputFor("以握拳重获追踪",.4f))yield return x;
            Check(!Object.FindFirstObjectByType<PassageGlyphRoad>() && preview.activeInHierarchy,"重获时已有握拳不会误搭桥");
            Grip(head.position-Vector3.up*.20f,head.rotation,false);
            foreach(var x in InputFor("取消前抬手放松",.3f))yield return x;
            Grip(head.position-Vector3.up*.84f,head.rotation,false);
            // Keep this already-closed fist below the threshold after a tracking break: no confirmation, no cancellation.
            mode=PoseMode.Off;foreach(var x in InputFor("低手前中断",.16f))yield return x;
            mode=PoseMode.Fist;foreach(var x in InputFor("握拳低放不取消",1.16f))yield return x;
            Check(preview.activeInHierarchy&&!Object.FindFirstObjectByType<PassageGlyphRoad>(),"低处非张掌不算主动取消");
            mode=PoseMode.Rest;
            foreach(var x in InputFor("主动低位张掌",1.45f))yield return x;''')
replace('            mode=PoseMode.Fist;\n            for(int i=0;i<18&&!road;', '''            var secondPreview=GameObject.Find("通路符搭路预览");
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name="回归用动态实体遮挡";block.transform.localScale=Vector3.one*.9f;
            block.transform.position=Vector3.Lerp(grip,b.landing.position,.70f);
            Physics.SyncTransforms();
            mode=PoseMode.Fist;foreach(var x in InputFor("两岸已选但面前出现实体遮挡",.35f))yield return x;
            Check(!Object.FindFirstObjectByType<PassageGlyphRoad>() && secondPreview.activeInHierarchy,"保留选择，握拳无法穿过新遮挡成路");
            block.SetActive(false);Object.Destroy(block);Physics.SyncTransforms();
            Point(b);foreach(var x in InputFor("移除遮挡，重新伸食指",.38f))yield return x;
            b.enabled=false;yield return null;yield return null;
            Check(!secondPreview.activeInHierarchy,"目标岸失效时撤销预览");
            b.enabled=true;Point(b);foreach(var x in InputFor("对岸恢复后主动重选",.36f))yield return x;
            Check(secondPreview.activeInHierarchy,"重选合法对岸后再次出现预览");
            Grip(head.position-Vector3.up*.23f,head.rotation,false);
            foreach(var x in InputFor("食指至握拳间张手",.80f))yield return x;
            Check(secondPreview.activeInHierarchy,"超过旧 0.32 秒仍有确认资格");
            mode=PoseMode.Fist;
            for(int i=0;i<18&&!road;''')
replace('            foreach(var x in Take("ink"))yield return x;','''            foreach(var x in Take("ink"))yield return x;
            var item=l.equipment.pen;
            var dropPoint=Floor+head.right*.35f+head.forward*.30f+Vector3.up*.14f;
            Grip(dropPoint,head.rotation,true);yield return null;yield return null;
            Grip(dropPoint,head.rotation,false);yield return null;yield return null;
            for(int i=0;i<24;i++)yield return null;
            Check(!item.Dock && !item.IsHeld && !l.equipment.interaction.Held && !pen.IsHeld,
                "正常松手，原笔留在脚边，可再次拾取");
            var atPen=item.GripPosition;
            Grip(atPen,item.transform.rotation,false);yield return null;yield return null;
            Grip(item.GripPosition,item.transform.rotation,true);yield return null;yield return null;
            Check(l.equipment.interaction.Held==item && pen.IsHeld && pen.activeBoard && pen.activeBoard.IsOpen,
                "从地面重新拾取同一支笔，公共交互交还书写输入");''')
f.write_text(s,encoding='utf8')
