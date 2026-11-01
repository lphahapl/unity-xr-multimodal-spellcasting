using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace NarutoVR.SealCombat.Rpg
{
    // Desktop Game view. Text and buttons retain their geometry between diagnostic refreshes.
    internal sealed class RpgDesktopPanel : IDisposable
    {
        readonly GameObject host;
        readonly RectTransform scroll, panel;
        readonly Canvas canvas;
        readonly List<Action> refresh = new();
        readonly RpgSessionView view;
        LinearRpgController Owner => view.controller;
        public RpgDesktopPanel(RpgSessionView owner)
        {
            view=owner;
            host=new GameObject("电脑交互调试面板",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            host.transform.SetParent(owner.transform,false);
            canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=20;
            if(!EventSystem.current && !UnityEngine.Object.FindFirstObjectByType<EventSystem>()) {
                var input=new GameObject("电脑面板鼠标输入");
                input.transform.SetParent(host.transform,false);
                input.AddComponent<EventSystem>();
                input.AddComponent<InputSystemUIInputModule>();
            }
            panel=Child(host.transform,"电脑面板");
            panel.anchorMin=new Vector2(1,0);panel.anchorMax=new Vector2(1,1);panel.pivot=new Vector2(1,.5f);
            panel.offsetMin=new Vector2(-470,10);panel.offsetMax=new Vector2(-10,-10);
            var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.08f,.09f,.12f,.94f);
            var scroller=panel.gameObject.AddComponent<ScrollRect>();scroller.horizontal=false;scroller.scrollSensitivity=32;
            var viewport=Child(panel,"裁切区");viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
            viewport.offsetMin=new Vector2(8,4);viewport.offsetMax=new Vector2(-8,-4);
            viewport.gameObject.AddComponent<RectMask2D>();scroller.viewport=viewport;
            scroll=Child(viewport,"内容");scroll.anchorMin=new Vector2(0,1);scroll.anchorMax=Vector2.one;
            scroll.pivot=new Vector2(0,1);scroll.anchoredPosition=Vector2.zero;scroll.sizeDelta=Vector2.zero;
            Stack(scroll);scroll.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroller.content=scroll;
            AddLabel(scroll,"交互调试 · 实时数据").fontSize=18;
            Section("运行状态与技能冷却",()=>view.showRuntimeSection,v=>view.showRuntimeSection=v,view.BuildRuntimeDetails);
            Section("移动输入与候选评分",()=>view.showMovementSection,v=>view.showMovementSection=v,()=>view.BuildMovementDetails(Time.realtimeSinceStartupAsDouble));
            ActionButton(scroll,"切换世界调试面板",()=>view.SetWorldDiagnostics(!view.showWorldDiagnostics));
            Section("结印匹配 · 得分与特征明细",()=>view.showSealSection,v=>view.showSealSection=v,view.BuildSealDetails);
            Section("麦克风输入与口令命中",()=>view.showVoiceSection,v=>view.showVoiceSection=v,view.BuildVoiceDetails);
            ActionButton(scroll,"关闭所有腕部教学界面",()=>Owner?.wristMenu?.ResetForWorldChange());
            var throwing=Section("投掷动作评分与发射诊断",()=>view.showThrowSection,v=>view.showThrowSection=v,()=>view.BuildThrowDetails(Time.realtimeSinceStartupAsDouble));
            if(view.throwRecorder){
                var recordInfo=AddLabel(throwing,"");
                refresh.Add(()=>{if(view.showThrowSection)SetLabel(recordInfo,$"诊断：{view.throwRecorder.Status}；{view.throwRecorder.Rows} 行\n{view.throwRecorder.FilePath}");});
                ActionButton(throwing,"开始／停止并保存投掷诊断",()=>{if(view.throwRecorder.IsRecording)view.throwRecorder.Stop();else view.throwRecorder.Begin();});
            }
            var simulation=Section("模拟操作与关卡测试",()=>view.showSimulationSection,v=>view.showSimulationSection=v,()=>"模拟施法绕过手印与口令匹配，仍检查资源和施法资格。");
            if(Owner && Owner.skills)foreach(var skill in Owner.skills.ConfiguredSkills){
                if(!skill)continue;
                var button=ActionButton(simulation,"",()=>Owner.DebugCast(skill.skillId));
                refresh.Add(()=>{if(view.showSimulationSection)SetCaption(button,$"模拟施放：{skill.displayName}（{(Owner.Progress?.unlockedSkillIds.Contains(skill.skillId)==true?"已解锁":"未解锁")}）");});
            }
            ActionButton(simulation,"取消待施法输入",()=>Owner.CancelCasting());
            foreach(bool dodge in new[]{false,true})foreach(var dir in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}){
                string label=dir==Vector2.up?"前":dir==Vector2.down?"后":dir==Vector2.left?"左":"右";
                ActionButton(simulation,(dodge?"模拟闪避：":"模拟步进：")+label,()=>Owner.DebugMove(dodge,dir));
            }
            ActionButton(simulation,"模拟受到25点伤害",()=>Owner.DebugDamage(25));
            ActionButton(simulation,"恢复检查点",()=>Owner.RestoreCheckpoint());
            ActionButton(simulation,"保存当前检查点",()=>Owner.SaveCheckpoint());
            ActionButton(simulation,"尝试进入下一阶段",()=>Owner.TryAdvanceStage());
            var hazard=ActionButton(simulation,"训练攻击",()=>Owner.ToggleHazards());
            var buy=ActionButton(simulation,"购买隐藏法术",()=>Owner.PurchaseHiddenSkill());
            var upgrade=ActionButton(simulation,"升级火球",()=>Owner.UpgradeFireball());
            refresh.Add(()=>{if(view.showSimulationSection && Owner){SetCaption(hazard,"训练攻击："+(Owner.HazardsEnabled?"开启":"关闭"));SetCaption(buy,$"购买隐藏法术（{Owner.hiddenSkillPrice}货币）");SetCaption(upgrade,$"升级火球（{Owner.fireballUpgradePrice}货币）");}});
            ActionButton(simulation,"【作弊测试】击破当前阶段全部训练靶",()=>Owner.DebugDefeatTargets());
        }
        static RectTransform Child(Transform parent,string name) {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            return (RectTransform)go.transform;
        }
        static void Stack(RectTransform node) {
            var layout=node.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing=3;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            layout.childControlHeight=true;layout.childForceExpandHeight=false;
        }
        RectTransform Section(string name,Func<bool> open,Action<bool> set,Func<string> read) {
            var button=ActionButton(scroll,name,()=>{set(!open());Refresh();});
            var content=Child(scroll,name);Stack(content);
            var label=AddLabel(content,"");
            refresh.Add(()=>{
                bool expanded=open();
                SetCaption(button,(expanded?"▼ ":"▶ ")+name);
                if(content.gameObject.activeSelf!=expanded)content.gameObject.SetActive(expanded);
                if(expanded)SetLabel(label,read());
            });
            return content;
        }
        static void SetLabel(Text label,string text) { if(label.text!=text)label.text=text; }
        Text AddLabel(RectTransform parent,string text) {
            var label=Child(parent,"文字").gameObject.AddComponent<Text>();
            label.font=view.chineseFont;label.fontSize=14;label.color=Color.white;label.raycastTarget=false;
            label.supportRichText=false;label.verticalOverflow=VerticalWrapMode.Overflow;label.text=text;
            return label;
        }
        // The returned caption is updated directly, without looking up a button hierarchy.
        static void SetCaption(Text caption,string value)=>SetLabel(caption,value);
        Text ActionButton(RectTransform parent,string title,Action action) {
            var node=Child(parent,title);
            var layout=node.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=28;
            var background=node.gameObject.AddComponent<Image>();background.color=new Color(.23f,.29f,.35f);
            var button=node.gameObject.AddComponent<Button>();button.targetGraphic=background;
            button.onClick.AddListener(()=>{if(Owner)action();});
            var caption=AddLabel(node,title);caption.alignment=TextAnchor.MiddleCenter;
            var rt=caption.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            return caption;
        }
        public void Refresh() {
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(Mathf.Clamp(view.desktopPanelWidth,320,720),Mathf.Max(1,Screen.width-20)));
            foreach(var update in refresh)update();
        }
        public void Dispose(){if(host){host.SetActive(false);UnityEngine.Object.Destroy(host);}}
    }
}
