using System;
using System.Collections;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class ItemInteractionLabChecks
    {
        const string Path = "Assets/Samples/ToolBeltItems/ItemInteractionLab.unity";
        const string Key = "NarutoVR.ItemLabChecks";
        const string Report = "Logs/item-interaction-lab.txt";
        static IEnumerator steps;
        static double deadline;
        static int lastFrame;

        static ItemInteractionLabChecks()
        {
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Key,false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    deadline = EditorApplication.timeSinceStartup + 95;
                    steps = CheckPlay();
                    EditorApplication.update += Tick;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.EraseBool(Key);
                    EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));
                }
            };
        }
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("批验证请使用独立工程。");
            EditorSceneManager.OpenScene(Path);
            SessionState.SetBool(Key,true);
            SessionState.SetInt(Key+"Exit",1);
            File.WriteAllText(Report,DateTimeOffset.Now.ToString("o")+"\n");
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame=Time.frameCount;
            try {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("道具交互测试超时");
                if (steps.MoveNext()) return;
                File.AppendAllText(Report,"PASS END\n");
                SessionState.SetInt(Key+"Exit",0);
            } catch(Exception e) { File.AppendAllText(Report,"FAIL "+e+"\n"); }
            EditorApplication.update-=Tick;
            EditorApplication.ExitPlaymode();
        }
        static void Check(bool condition,string reason)
        {
            if(!condition) throw new InvalidOperationException(reason);
            File.AppendAllText(Report,"PASS "+reason+"\n");
        }

        static IEnumerator CheckPlay()
        {
            var lab=Object.FindFirstObjectByType<ItemLabControls>();
            double waitUntil=EditorApplication.timeSinceStartup+15;
            while(!lab.Initialised || lab.slots.Any(s => s.IsSnapping))
            {
                if(EditorApplication.timeSinceStartup>waitUntil) throw new InvalidOperationException($"启动失败：{lab.player.Status} / ready={lab.player.IsReady}, belt={lab.Initialised}, enabled={lab.enabled}, move={lab.player.locomotion.Status}");
                yield return null;
            }
            var food=(ConsumableItem)lab.items[0]; var key=lab.items[1]; var plate=lab.items[2];
            var state=lab.player.Player;
            var head=lab.mouth.transform;
            var end=head.GetComponent<NeuGameMRTools.Runtime.Components.SnapZone>();
            Vector3 Near()=>head.position-head.forward*.03f;
            var playerHead=lab.player.locomotion.head;
            Vector3 Far()=>new Vector3(playerHead.position.x,lab.belt.position.y,playerHead.position.z)+lab.belt.forward*(lab.discardReach+.20f);
            void Hand(Vector3 at,bool pinch,bool valid=true,bool reverse=false,float dt=.02f, Quaternion? orientation=null)
            {
                double now=Time.realtimeSinceStartupAsDouble;
                var hand=new PlayerHandObservation{rightValid=valid,tipUnavailable=!valid,gripPoseValid=valid,
                    pinchValid=valid,systemPinch=true,systemPressed=pinch,rightObserved=now,rightTip=at,gripPosition=at,headPosition=playerHead.position,
                    gripRotation=orientation ?? (reverse?Quaternion.LookRotation(-head.forward,head.up):head.rotation)};
                lab.Step(hand,now,dt);
            }
            void Hold(Vector3 at,int frames,bool reverse=false) {for(int i=0;i<frames;i++) Hand(at,true,true,reverse);}
            void Take(GameItemBase item) {var at=item.GripPosition; Hand(at,false); Hand(at,true);}
            void TakeLoose(GameItemBase item)
            {
                if (item.Dock)
                {
                    Take(item);
                    Hand(Far()+lab.belt.right*(Array.IndexOf(lab.items,item)*.22f),false);
                }
                Take(item);
            }
            Transform Rim(int i) => lab.slots[i].GetComponent<ToolBeltSlot>().opening;
            void Place(int i, float pitch = 0, float yaw = 0) =>
                Hand(Rim(i).position, false, orientation: Rim(i).rotation * Quaternion.Euler(pitch,yaw,0));
            bool Model(int index)=>food.stages.Where(s=>s.activeSelf).SequenceEqual(new[]{food.stages[index]});
            Check(food.Dock == food.home && food.home.SnappedObject == food.gameObject &&
                key.Dock == key.home && key.home.SnappedObject == key.gameObject && plate.Dock == lab.slots[3],
                "开场口粮、墨规笔和铜片分别归入陈列位置");
            Check(lab.slots.Length == 5 && lab.slots.Count(s => s.GetComponent<ToolBeltSlot>().penHolster) == 1 &&
                key.Kind==GameItemKind.GlyphPen && key.insertionTip.name == "Nib" &&
                !Enum.GetValues(typeof(GameItemKind)).Cast<GameItemKind>().Except(lab.slots.Select(s => s.GetComponent<ToolBeltSlot>().displayKind).Append(GameItemKind.Generic)).Any(),
                "一专四通，笔套绑定正式墨规笔模型和笔尖");
            Check(food.Remaining==3 && Model(2) && !food.CanEat(null),"三口外观与未绑定玩家保护");
            Check(food.GetComponents<Collider>().Length == 2 && food.config.biteSound && food.config.biteSound.samples > 0,
                "样板绑定复合碰撞体和可播放的进食音效");
            foreach (var target in new[] { key.home, lab.slots[2] })
            {
                var origin = key.Dock;
                bool reacquired = false;
                void Reclaim(GameObject obj, bool completed)
                {
                    origin.OnSnapReleased -= Reclaim;
                    reacquired = target.TrySnapObjectAtRotation(obj,Rim(target==key.home?1:2).rotation * Quaternion.Euler(90,0,0));
                }
                origin.OnSnapReleased += Reclaim;
                Take(key);
                Check(reacquired && !key.IsHeld && !lab.Held && !lab.player.Input.InteractionOwnsHand &&
                    key.Dock == target && target.SnappedObject == key.gameObject && key.GetComponent<Rigidbody>().isKinematic,
                    "取物释放回调重新接管，已成立的槽权胜过手持：" + target.GetComponent<ToolBeltSlot>().opening.name);
                while (target.IsSnapping) yield return null;
            }
            Take(key); Place(1,90);
            while(key.Dock && key.Dock.IsSnapping) yield return null;
            Take(key);
            var vertical = Rim(2).rotation * Quaternion.Euler(90,0,0);
            Hand(Rim(2).position - vertical * Vector3.forward * .108f, false, orientation: vertical);
            Check(key.Dock == lab.slots[2], "笔尖在方匣开口、握点距离超过九厘米也可竖放");
            float placedAt = Time.time;
            while (Time.time - placedAt < 1.8f) yield return null;
            Check(key.Dock == lab.slots[2] && !key.home.SnappedObject &&
                Vector3.Dot(key.transform.forward, -Rim(2).up) > .999f, "放入通用槽后保持原姿态，超过防坠时限仍留在原位");
            var rim = Rim(2);
            var rimPosition = rim.localPosition;
            var rimRotation = rim.localRotation;
            var seatedPosition = rim.InverseTransformPoint(key.transform.position);
            var seatedRotation = Quaternion.Inverse(rim.rotation) * key.transform.rotation;
            rim.localPosition += new Vector3(.085f,.035f,-.055f);
            rim.localRotation *= Quaternion.Euler(12,35,17);
            yield return null; yield return null;
            Check(Vector3.Distance(key.transform.position,rim.TransformPoint(seatedPosition)) < .0002f &&
                Quaternion.Angle(key.transform.rotation,rim.rotation*seatedRotation) < .05f,
                "派生终点在吸附完成后随开口平移旋转，保持笔的实际座姿");
            Take(key); Hand(rim.position+lab.belt.forward*.14f,false);
            while(key.Dock && key.Dock.IsSnapping) yield return null;
            Check(key.Dock==lab.slots[2] && Quaternion.Angle(key.transform.rotation,rim.rotation*seatedRotation) < .05f &&
                Vector3.Distance(key.transform.position,rim.TransformPoint(seatedPosition)) < .0002f,
                "开口旋转后再取再还，恢复相同的局部座姿");
            rim.SetLocalPositionAndRotation(rimPosition,rimRotation);
            yield return null; yield return null;
            TakeLoose(food); Place(1);
            Check(food.Dock==food.home && food.PickupSource==food.home && !key.home.SnappedObject && food.GetComponent<Rigidbody>().isKinematic,
                "专属笔套空置时拒收口粮，地面重新拾起的口粮返回实际取出的软袋");
            while(food.home.IsSnapping) yield return null;
            Take(food); Hand(Far(),false);
            TakeLoose(plate);
            var plateScale = plate.transform.localScale;
            plate.transform.localScale *= .2f;
            Place(1);
            var sleeve = key.home.GetComponent<ToolBeltSlot>();
            Check(plate.MeasureInSlot(Rim(1), plate.transform.rotation, out var small) &&
                small.size.x < sleeve.width && small.size.z < sleeve.thickness && small.size.y < sleeve.depth &&
                plate.Dock==lab.slots[3] && !key.home.SnappedObject, "尺寸合格的非笔也不能占用专属笔套，铜片归原扁袋");
            while(lab.slots[3].IsSnapping) yield return null;
            Take(plate);
            plate.transform.localScale = plateScale;
            Place(0);
            Check(plate.Dock == food.home, "铜片能放入原先陈列口粮的软袋");
            TakeLoose(key); Place(0,90);
            Check(key.Dock==lab.slots[2] && lab.slots[0].SnappedObject == plate.gameObject, "选中已占槽时保留槽内铜片，笔归取出的方匣");
            while(lab.slots[2].IsSnapping) yield return null;
            Take(key); Place(4,90);
            Check(key.Dock == lab.slots[4], "墨规笔可换入同样装得下的窄鞘");
            Take(food); Place(3);
            Check(!food.Dock && !lab.slots[3].SnappedObject && food.PickupSource==food.home &&
                food.home.SnappedObject==plate.gameObject && food.GetComponent<Rigidbody>().useGravity,
                "扁袋拒收厚过开口的口粮，原软袋也被占用时落地");
            Take(food); Place(2);
            Check(food.Dock == lab.slots[2], "口粮进入方匣");
            while (lab.slots.Any(s => s.IsSnapping)) yield return null;
            TakeLoose(plate); Place(3,0,90);
            Check(plate.Dock==food.home && !lab.slots[3].SnappedObject, "同一薄铜片横对扁口时拒收，归原软袋");
            while(food.home.IsSnapping) yield return null;
            Take(plate); Place(3);
            Check(plate.Dock == lab.slots[3], "铜片转正后通过同一开口");
            Take(food); Place(0);
            while (lab.slots.Any(s => s.IsSnapping)) yield return null;
            Take(plate); Place(2);
            Check(plate.Dock == lab.slots[2] && food.Dock == lab.slots[0], "铜片与口粮完成两通用槽互换");
            Take(food); Hand(Far(),false);
            TakeLoose(key); Place(0,90);
            var bag=lab.slots[0].GetComponent<ToolBeltSlot>();
            Check(key.MeasureInSlot(Rim(0),Rim(0).rotation*Quaternion.Euler(90,0,0),out var upright) && upright.size.x < bag.width &&
                upright.size.z < bag.thickness && upright.size.y > bag.depth && key.Dock==lab.slots[4] && !lab.slots[0].SnappedObject,
                "开口够宽而可用深度不足时拒收长笔，笔归原窄鞘");
            while(lab.slots[4].IsSnapping) yield return null;
            Take(key); Place(1,90);
            while(key.home.IsSnapping) yield return null;
            Take(key); Place(4,0,90);
            var narrow=lab.slots[4].GetComponent<ToolBeltSlot>();
            Check(key.MeasureInSlot(Rim(4),Rim(4).rotation*Quaternion.Euler(0,90,0),out var sideways) && sideways.size.x > narrow.width &&
                sideways.size.z < narrow.thickness && sideways.size.y < narrow.depth && key.Dock==key.home && !lab.slots[4].SnappedObject,
                "厚度和深度合格但横放超出开口宽度时拒收，笔归原笔套");
            while(key.home.IsSnapping) yield return null;
            Take(key); Place(4,90);
            while(lab.slots[4].IsSnapping) yield return null;
            Take(key); Place(1,90);
            Check(key.Dock == key.home, "墨规笔可主动放回专属笔套");
            while (key.home.IsSnapping) yield return null;
            Take(food); Place(0);
            while(food.home.IsSnapping) yield return null;
            Take(food);
            Check(lab.Held==food && !lab.slots[0].SnappedObject && lab.player.Input.InteractionOwnsHand,"从槽位捏取并占用手部");
            Hold(Near(),30,true); Hold(Near(),12); Hand(Near(),true,false); Hold(Near(),12);
            Check(lab.Held==food && food.Remaining==3 && state.Health==20 && state.Mana==10,"反向和短暂停留无效，失追冻结且清除旧停留");
            Hold(Near(),12);
            Check(food.Remaining==2 && Model(1) && state.Health==35 && state.Mana==30,"第一口只结算一次");
            Hold(Near(),100); Hand(Near(),true,false); Hold(Near(),40);
            Check(food.Remaining==2,"持续贴嘴与原位恢复追踪不能再吃");
            Hand(Far(),true); Hold(Near(),24);
            Check(food.Remaining==1 && Model(0) && state.Health==50 && state.Mana==50,"退出再入才吃第二口");
            var blocker=new GameObject("占用末口通道");
            Check(end.TrySnapObject(blocker),"准备占用嘴部");
            Hand(Far(),true); Hold(Near(),25);
            Check(lab.Held==food && food.Remaining==1 && state.Health==50,"末口被占保留手持和数值");
            end.ReleaseObject(); Object.Destroy(blocker);
            Hold(Near(),25);
            Check(!lab.Held && end.SnappedObject==food.gameObject && end.IsSnapping && state.Health==50,
                $"末口先向嘴部移交所有权（手={lab.Held}，槽={end.SnappedObject}，路径={end.IsSnapping}，气血={state.Health}，拒绝原因={end.LastFailure}）");
            end.ReleaseObject(); yield return null; yield return null;
            Check(food.Remaining==1 && food.gameObject.activeSelf && !food.IsHeld && !food.GetComponent<Rigidbody>().isKinematic,"中断终结吸附可重拾且未扣次数");
            Take(food); Hold(Near(),25);
            while(food.gameObject.activeSelf) yield return null;
            Check(food.Remaining==0 && !end.SnappedObject && !lab.player.Input.InteractionOwnsHand && state.Health==65 && state.Mana==70,
                "吸附完成后第三口结算并释放手、嘴槽");
            var looseObject=GameObject.CreatePrimitive(PrimitiveType.Cube);
            looseObject.name="未陈列的通用道具";
            looseObject.transform.localScale=Vector3.one*.018f;
            looseObject.transform.position=Far();
            var loose=looseObject.AddComponent<GameItemBase>();
            var initialItems=lab.items;
            lab.items=initialItems.Append(loose).ToArray();
            Take(loose); Hand(Rim(0).position+lab.belt.forward*.14f,false);
            Check(loose.Kind==GameItemKind.Generic && !loose.LastDock && !loose.PickupSource && !loose.Dock &&
                loose.GetComponent<Rigidbody>().useGravity, "无配置的地面物件默认为通用且无隐式归还");
            Take(loose); Place(0);
            Check(loose.Dock==lab.slots[0], "通用物件可以明确放入初始陈列类型不同的槽");
            while(loose.Dock.IsSnapping) yield return null;
            // Pin the reference X to zero during the observation so 0.60f is exactly representable
            // relative to the head, even when the sample scene is placed far from world zero.
            Check(lab.discardReach==.60f,"样板起测的主动丢弃水平半径为 0.60 米");
            Take(loose);
            Vector3 originalHead=playerHead.position;
            try
            {
                playerHead.position=new Vector3(0,originalHead.y,originalHead.z);
                Vector3 edge=playerHead.position+Vector3.right*.60f+Vector3.up*.8f;
                Hand(edge,true); Hand(edge,false);
                Check(loose.GripPosition.x-playerHead.position.x==.60f && loose.GripPosition.z==playerHead.position.z &&
                    !loose.Dock && !loose.IsHeld && loose.PickupSource==lab.slots[0] && loose.GetComponent<Rigidbody>().useGravity,
                    "水平恰好 0.600 米已判弃，原口继续记在该物件上");
                Take(loose);
                Hand(playerHead.position+Vector3.right*.599f+Vector3.up*.8f,false);
                Check(loose.Dock==lab.slots[0] && !loose.IsHeld,"水平 0.599 米且抬高手仍返回实际取出槽");
            }
            finally { playerHead.position=originalHead; }
            while(lab.slots[0].IsSnapping) yield return null;
            Take(loose);
            var distantOpening=Rim(3);
            var localPosition=distantOpening.localPosition;
            try
            {
                distantOpening.position=Far();
                Place(3);
                Check(loose.Dock==lab.slots[3] && !lab.slots[0].SnappedObject,"已伸出丢弃半径，明确放入其他合法空口仍以换槽优先");
                while(lab.slots[3].IsSnapping) yield return null;
            }
            finally { distantOpening.localPosition=localPosition; }
            Take(loose); Hand(Far(),false); lab.items=initialItems; Object.Destroy(looseObject);
            Take(key); Place(4,90);
            lab.slots[4].ReleaseObject();
            while(key.Dock) yield return null;
            Check(key.LastDock==key.home && !key.GetComponent<Rigidbody>().isKinematic,
                "换槽吸附中断后掉落，上一次成功槽没有被未完成路径覆盖");
            Take(key); Place(1,90);
            while(key.Dock.IsSnapping) yield return null;
            Take(key);
            Hand(Rim(1).position + lab.belt.forward*.14f,false);
            Check(key.Dock == key.home, "普通松手回本次取出的专属笔套");
            while (key.Dock.IsSnapping) yield return null;
            Take(key); Place(4,90);
            while (key.Dock.IsSnapping) yield return null;
            Take(key);
            Check(key.PickupSource == lab.slots[4] && key.LastDock == lab.slots[4], "换槽后再次拿起以最新成功停靠的窄鞘为来源");
            var nearSource = Rim(4).position + lab.belt.forward*.14f;
            Hand(nearSource,false);
            Check(key.Dock == lab.slots[4] && !key.home.SnappedObject, "近原口归还一次，恢复在通用槽保存的放置姿态");
            while (key.Dock.IsSnapping) yield return null;
            Take(key);
            var blockerAtSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blockerAtSource.name = "临时占用上次口";
            blockerAtSource.transform.localScale = Vector3.one * .014f;
            blockerAtSource.AddComponent<GameItemBase>();
            Check(lab.slots[4].TrySnapObject(blockerAtSource), "占用上次停靠的通用槽");
            Hand(nearSource,false);
            Check(!key.Dock && key.GetComponent<Rigidbody>().useGravity && lab.slots[4].SnappedObject == blockerAtSource &&
                !key.home.SnappedObject, "来源被占时掉落，保留槽内原物和空笔套");
            lab.slots[4].ReleaseObject(); Object.Destroy(blockerAtSource);
            Take(key); Hand(nearSource,false);
            Check(key.Dock==lab.slots[4] && key.PickupSource==lab.slots[4] && !key.home.SnappedObject,
                "来源重新腾空，地面捡回的同一支笔恢复至实际取出的窄鞘");
            while(lab.slots[4].IsSnapping) yield return null;
            Take(key); Place(4,90);
            while (key.Dock.IsSnapping) yield return null;
            Take(key);
            var sleeveSlot=lab.slots[4].GetComponent<ToolBeltSlot>();
            float previousDepth=sleeveSlot.depth;
            sleeveSlot.depth=.04f;
            Hand(nearSource,false);
            Check(!key.Dock && !sleeveSlot.GetComponent<NeuGameMRTools.Runtime.Components.SnapZone>().SnappedObject, "原口变浅放不下时按重力掉落");
            sleeveSlot.depth=previousDepth;
            Take(key); Place(1,90);
            while (key.Dock.IsSnapping) yield return null;
            Take(key); Hand(Far(),true); Hand(Far(),false,false); Hand(Far(),false);
            Check(lab.Held==key && key.IsHeld && key.GetComponent<Rigidbody>().isKinematic, "失追回报空手及恢复首帧张手都不会触发主动丢弃");
            Hand(Far(),true); Hand(Far(),false);
            Check(!key.Dock && !lab.Held && key.GetComponent<Rigidbody>().useGravity, "重新确认捏持后远拉松手直接掉落");
            float start=Time.time;
            while(Time.time-start<2.15f) yield return null;
            Check(!key.Dock && key.transform.position.y > key.worldFallBoundary.position.y && !key.home.SnappedObject,
                "笔在正常地面上超过两秒仍留在地上");
            Take(key); Check(lab.Held==key && key.PickupSource==key.home,"主动丢下的笔可以重新捡起并记住实际取出的笔套");
            Hand(Far(),false);
            var occupied = GameObject.CreatePrimitive(PrimitiveType.Cube);
            occupied.name = "占用世界下界兜底笔套的另一支笔";
            occupied.transform.localScale = Vector3.one * .014f;
            occupied.AddComponent<GameItemBase>().definition = key.definition;
            Check(key.home.TrySnapObject(occupied), "占住空笔套");
            key.transform.position = key.worldFallBoundary.position - Vector3.up;
            yield return null; yield return null;
            Check(!key.Dock && key.home.SnappedObject == occupied && !key.GetComponent<Rigidbody>().isKinematic,
                "越过世界下界也不能抢占笔套");
            key.home.ReleaseObject(); Object.Destroy(occupied);
            while (!key.Dock || key.home.IsSnapping) yield return null;
            Check(key.Dock == key.home, "真正越过固定世界下界的笔在笔套空出后获救");
            var previousSettings = InputSystem.settings;
            var batchSettings = Object.Instantiate(previousSettings);
            batchSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            batchSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = batchSettings;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F8, UnityEngine.InputSystem.Key.Digit2));
                for (int i = 0; i < 30 && !lab.Desktop; i++) yield return null;
                Check(lab.Desktop, $"F8 开启桌面右手（键盘={keyboard.enabled}，F8={keyboard.f8Key.isPressed}）");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Space));
                for (int i = 0; i < 30 && lab.Held != key; i++) yield return null;
                Check(lab.Held == key && lab.player.Input.InteractionOwnsHand, "桌面键盘从正常玩家更新入口捏取固定物");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.G));
                while (lab.Held) yield return null;
                Check(!key.Dock && !lab.player.Input.InteractionOwnsHand, "桌面移开松手后重力与手部所有权恢复");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.K));
                yield return null; yield return null; // Aim at the released pen with an open hand before closing it.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Space));
                while (lab.Held != key) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Digit5,UnityEngine.InputSystem.Key.R,UnityEngine.InputSystem.Key.Space));
                while (Vector3.Dot(key.transform.forward, -Rim(4).up) < .999f) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                while (lab.Held) yield return null;
                Check(key.Dock == lab.slots[4], "桌面重拾丢弃的笔、转向、放入窄鞘");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = previousSettings;
                Object.DestroyImmediate(batchSettings);
            }
        }
    }
}
