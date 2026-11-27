using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NarutoVR.SealCombat.Editor
{
    public static partial class Chapter02BasketBChecks
    {
        static void SupportReport(Chapter02BasketBControl b)
        {
            var m=b.motion;
            var loc=b.player.locomotion;
            var f=loc.PlayerFloorPosition;
            string Hit() => loc.PreviewNavigationOrigin(out var hit) ?
                hit.collider.name+"@"+hit.point.ToString("F5")+" own="+hit.collider.transform.IsChildOf(m.basketB) : "none";
            var readings=new[] {"feet="+f.ToString("F6"),"local="+m.passengerB.transform.InverseTransformPoint(f).ToString("F6"),
                 "size="+m.passengerB.size.ToString("F5")+" center="+m.passengerB.center.ToString("F5"),
                 "bounds="+m.passengerB.bounds+" basket="+m.basketB.position.ToString("F5"),
                 "inB beforeSync="+m.PlayerInB+" support="+Hit(),
                 "hits="+string.Join(" | ",Physics.RaycastAll(f+Vector3.up*.35f,Vector3.down,1.25f,~0,QueryTriggerInteraction.Ignore)
                    .OrderBy(h=>h.distance).Select(h=>h.collider.name+"@"+h.point.ToString("F5")+" own="+h.collider.transform.IsChildOf(m.basketB)))};
            File.WriteAllLines(Path.Combine(Out,"support-"+mode+".txt"),readings);
        }
        static void ProbeOwnPen(LinearRpgController p)
        {
            var pen=One<Chapter02BridgeEquipment>().equipment.pen;
            var collider=pen.GetComponentsInChildren<Collider>(true).First(c=>!c.isTrigger);
            var ignored=typeof(RpgLocomotionController).GetMethod("Ignored",BindingFlags.Instance|BindingFlags.NonPublic);
            bool IsIgnored(Collider c)=>(bool)ignored.Invoke(p.locomotion,new object[]{c});
            Check(pen.Dock && IsIgnored(collider),"已入本人槽位的真实墨规笔不挡身体移动");
            pen.Undock();
            pen.DropInWorld();
            Check(!pen.Dock && !IsIgnored(collider),"同一支笔落地后重新参与身体阻挡");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Check(!IsIgnored(wall.GetComponent<Collider>()),"世界墙保留身体阻挡");
            UnityEngine.Object.DestroyImmediate(wall);
        }
        static void Aim(Chapter02BasketBControl b, Vector3 tip, Vector3 target, bool pinch,
            bool valid = true, bool systemRay = false)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var direction = (target - tip).normalized;
            var hand = new PlayerHandObservation
            {
                rightValid = valid, rightObserved = now, rightTip = tip,
                rightPointing = !pinch, rightIndexRayValid = !systemRay, rightIndexDirection = direction,
                rayValid = systemRay, rayAnchorValid = systemRay, rayOrigin = tip, rayDirection = direction,
                gripPoseValid = true, gripPosition = tip, gripRotation = Quaternion.LookRotation(direction),
                pinchValid = valid, systemPinch = true, systemPressed = pinch, pinchDistance = pinch ? .015f : .06f
            };
            b.Step(hand, now);
        }

        static void Photo(Camera camera, string label)
        {
            const int w = 960, h = 720;
            var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);
            var texture = new Texture2D(w,h,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0,0,w,h),0,0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(Out, label + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void CheckRay(Chapter02BasketBControl b, Vector3 tip)
        {
            var line = b.GetComponentsInChildren<LineRenderer>(true).Single(l=>l.name.Contains("命中射线"));
            var end = b.leverMesh.transform.InverseTransformPoint(line.GetPosition(1));
            var m = b.leverMesh.sharedMesh;
            var tri = m.GetIndices(3);
            var v = m.vertices;
            bool onFace = false;
            for (int j = 0; j < tri.Length; j += 3)
            {
                var a = v[tri[j]]; var e = v[tri[j+1]]-a; var f = v[tri[j+2]]-a;
                var n = Vector3.Cross(e,f);
                if (n.sqrMagnitude < 1e-12f || Mathf.Abs(Vector3.Dot(n.normalized,end-a))>.001f) continue;
                float x = Vector3.Dot(Vector3.Cross(end-a,f),n)/n.sqrMagnitude;
                float y = Vector3.Dot(Vector3.Cross(e,end-a),n)/n.sqrMagnitude;
                if (x>=-.001f && y>=-.001f && x+y<=1.001f) {onFace=true;break;}
            }
            Check(line.enabled && line.useWorldSpace && line.positionCount==2 && line.sharedMaterial &&
                  Vector3.Distance(line.GetPosition(0),tip)<.002f && onFace,
                  "可见射线始于真实手部采样、末端在铜手柄显示网格三角面上");
        }

        static async Task Remote(Chapter02BasketBControl b, bool locked)
        {
            var p = b.player;
            var loc = p.locomotion;
            var grip = b.GripPosition;
            var lip = Nodes().Single(t => t.name == "B 中层篮口").position;
            await Walk(loc, new Vector3(grip.x,lip.y,lip.z), "步行退回东栈道中线");
            await Walk(loc, new Vector3(grip.x-3.95f,lip.y,lip.z), "步行到射程外");
            var eye = loc.head;
            eye.position += Vector3.up * (1.63f - (eye.position - loc.PlayerFloorPosition).y);
            Vector3 HandAtChestReach() => eye.position - Vector3.up*.24f +
                Vector3.ProjectOnPlane(grip-eye.position,Vector3.up).normalized*.42f;
            var tip = HandAtChestReach();
            Aim(b,tip,grip,false);
            Aim(b,tip,grip,true);
            Check(!b.RemoteHighlighted && !p.Input.InteractionOwnsHand && !b.DepartureArmed,
                $"射程外真杆不高亮且无法远抓，指尖至柄 {Vector3.Distance(tip,grip):F2}m");
            await Walk(loc,new Vector3(grip.x-1.75f,lip.y,lip.z),"步行回可远拉位置");
            eye.rotation = Quaternion.LookRotation(grip-eye.position,Vector3.up);
            tip = HandAtChestReach();
            var chest = eye.position - Vector3.up*.24f;
            var toward = (chest-tip).normalized;
            var feet = loc.PlayerFloorPosition;
            float headHeight = (eye.position - feet).y;
            float reach = Vector3.Distance(tip,grip);
            Check(headHeight > 1.5f && reach >= 1.5f && reach <= 2.5f && !b.motion.PlayerInB,
                $"常态站姿远拉，头高 {headHeight:F3}m，真实指尖至铜柄 {reach:F3}m，脚底 {feet:F3}");
            Aim(b,tip,grip,false);
            if (locked)
            {
                Aim(b,tip,grip,true);
                Aim(b,tip+toward*.22f,grip,true);
                Check(!b.RemoteHighlighted && !b.DepartureArmed && !p.Input.InteractionOwnsHand,
                    "未插榫时实际指向捏合回拉不高亮、不认领、不待发");
                return;
            }
            Check(b.RemoteHighlighted && !b.DepartureArmed, "张手右食指命中真实杆柄才高亮");
            CheckRay(b,tip);
            var camera = eye.GetComponent<Camera>();
            Check(camera, "实拍使用生产玩家的头部相机");
            Photo(camera,"standing-aim");
            Aim(b,tip,grip+Vector3.up*.75f,true);
            Check(!p.Input.InteractionOwnsHand && !b.DepartureArmed && !b.RemoteHighlighted,
                "上一帧瞄中、捏下时已偏离手柄不能抓取");
            Aim(b,tip,grip,false);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "QA occluding wall";
            blocker.transform.position = Vector3.Lerp(tip,grip,.55f);
            blocker.transform.rotation = Quaternion.LookRotation(grip-tip);
            blocker.transform.localScale = new Vector3(1.1f,1.1f,.08f);
            Physics.SyncTransforms();
            Aim(b,tip,grip,false); Aim(b,tip,grip,true);
            Check(!b.RemoteHighlighted && !b.DepartureArmed && !p.Input.InteractionOwnsHand,
                "墙体遮挡同一根柄时高亮熄灭且无法抓取");
            Photo(camera,"standing-blocked");
            UnityEngine.Object.DestroyImmediate(blocker);
            Physics.SyncTransforms();
            Aim(b,tip,grip,false,true,true); Aim(b,tip,grip,true,true,true);
            Check(p.Input.InteractionOwnsHand && !b.DepartureArmed, "生产手射线捏下仅认领，不立即开杆");
            Aim(b,tip+toward*.045f,grip,true,true,true);
            Check(b.PullFraction>0 && b.PullFraction<.8f, "小幅回拉驱动铜柄部分行程");
            Aim(b,tip+toward*.045f,grip,false);
            Check(!b.DepartureArmed && b.PullFraction < .01f && !p.Input.InteractionOwnsHand,
                "半程松手回弹释放");
            Aim(b,tip,grip,false); Aim(b,tip,grip,true); Aim(b,tip+toward*.04f,grip,true);
            Aim(b,tip,grip,true,false);
            Check(!b.DepartureArmed && b.PullFraction < .01f && !p.Input.InteractionOwnsHand,
                "中途追踪失效回弹释放");
            p.wristMenu.State.Reset(true);
            Check(p.UiInputOwned,"腕部状态机持有输入");
            Aim(b,tip,grip,false); Aim(b,tip,grip,true); Aim(b,tip+toward*.2f,grip,true);
            Check(!b.DepartureArmed && !b.RemoteHighlighted && !p.Input.InteractionOwnsHand,"UI 占用时远拉无效");
            p.wristMenu.State.Reset(false);
            Aim(b,tip,grip,false); Aim(b,tip,grip,true); Aim(b,tip+toward*.06f,grip,true);
            CheckRay(b,tip+toward*.06f);
            Photo(camera,"standing-pull");
            Aim(b,tip+toward*.21f,grip,true);
            Check(b.DepartureArmed && !p.Input.InteractionOwnsHand,"面向原铜杆捏住向胸回拉达到阈值，完成设定");
            var station=b.motion.basketB.position;
            for (int i=0;i<35;i++) await Awaitable.NextFrameAsync();
            Check(b.DepartureArmed && !b.motion.PlayerInB && !b.motion.IsMoving &&
                  Vector3.Distance(station,b.motion.basketB.position)<.001f &&
                  Vector3.Distance(feet,loc.PlayerFloorPosition)<.005f && Mathf.Abs(eye.position.y-feet.y-headHeight)<.005f,
                "岸上设定后无人登篮，连续35帧吊篮不动、玩家脚底及站姿高度保持");
        }

        static async Task ProbeBoarding(Chapter02BasketBControl b, Vector3 lip)
        {
            var m=b.motion;
            var loc=b.player.locomotion;
            var eye=loc.head;
            var foot=loc.PlayerFloorPosition;
            var root=loc.rigRoot.position;
            var savedEye=eye.position;
            var bounds=m.passengerB.bounds;
            var shallow=new Vector3(bounds.min.x+.12f,foot.y,lip.z);
            // The hand crosses well inside the passenger trigger while the tracked body stays on shore.
            Aim(b,new Vector3(bounds.center.x,savedEye.y-.25f,lip.z),b.GripPosition,true);
            for(int i=0;i<4;i++) await Awaitable.NextFrameAsync();
            Check(!m.PlayerInB && !m.IsMoving && m.AtMiddle && b.DepartureArmed,
                "只伸手入篮，岸上脚底未移动，空篮不开程");
            eye.position += Vector3.ProjectOnPlane(shallow-foot,Vector3.up);
            Physics.SyncTransforms();
            for(int i=0;i<4;i++) await Awaitable.NextFrameAsync();
            Check(!m.PlayerInB && !m.IsMoving && b.DepartureArmed && Vector3.Distance(loc.rigRoot.position,root)<.01f,
                "头投影探入原乘员区12厘米而根留岸，未认作登板");
            eye.position=savedEye;
            Physics.SyncTransforms();
            if (mode != "interrupted") return;
            var seatedFoot = new Vector3(bounds.min.x+.58f,foot.y,lip.z);
            eye.position = seatedFoot+Vector3.up*.99f;
            Physics.SyncTransforms();
            Check(Mathf.Abs(loc.PlayerFloorPosition.x-seatedFoot.x)<.012f && m.PlayerInB &&
                  Vector3.Distance(loc.rigRoot.position,root)<.005f,
                "坐姿房间尺度登板：头高0.99米、根仍岸上、脚投影在甲板内0.58米");
        }
    }
}
