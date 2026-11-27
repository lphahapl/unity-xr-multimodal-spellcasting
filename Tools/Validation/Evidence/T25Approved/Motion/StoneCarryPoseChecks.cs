// 复制到独立工程 Assets/Editor，从空场景经正式 EnemyManager/Addressables 出生后读回原动画、石根和全身顶点。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StoneCarryPoseChecks
{
    const string Pending="StoneCarryPoseChecks.Pending";
    static readonly MethodInfo Late=typeof(StoneCarrier).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
    static readonly int State=Animator.StringToHash("Base Layer.StoneCarry");
    static readonly int Rate=Animator.StringToHash("StoneCarrySpeed");
    static string directory, signed;
    static StoneCarryPoseChecks()=>EditorApplication.update+=Start;
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("请在独立批处理工程运行");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static async void Start()
    {
        if(!SessionState.GetBool(Pending,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        SessionState.EraseBool(Pending);
        try
        {
            var args=Environment.GetCommandLineArgs();var root=args[Array.IndexOf(args,"-evidenceRoot")+1];
            signed=Path.Combine(root,"Logs/CH02-carry-pose-20261125");
            int outputIndex = Array.IndexOf(args, "-motionOutput");
            directory = outputIndex >= 0 ? args[outputIndex + 1] : Path.Combine(root, "Logs/CH02-T25-A4-production/motion");
            Directory.CreateDirectory(directory);
            await Check();Debug.Log("A4_MOTION_PASS");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);Debug.LogError("A4_MOTION_FAIL");EditorApplication.Exit(1);}
    }
    static void Assert(bool yes,string message){if(!yes)throw new Exception(message);Debug.Log("MOTION_CHECK "+message);}
    static void ReadPose(StoneCarrier w)=>Late.Invoke(w,null);
    static float[] SampleFloats(string file)
    {
        var bytes=File.ReadAllBytes(file);var arr=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,arr,0,bytes.Length);return arr;
    }
    static Vector3 At(float[] arr,int k)=>new Vector3(arr[k],arr[k+1],arr[k+2]);
    static float[] Row(string line)=>line.Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();
    [Serializable] class BoneList { public string[] boneNames; }

    // 用导入的逆绑定矩阵和此帧真实骨骼重新蒙皮，区分两骨/所有权重与 Animator 转向的影响。
    static void CheckSkinWeights(int frame, SkinnedMeshRenderer renderer, Transform actor, Vector3[] baked,
        Matrix4x4 bakedToActor, float[] signedVertices)
    {
        var mesh = renderer.sharedMesh;
        using var meshData = MeshUtility.AcquireReadOnlyMeshData(mesh);
        using var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
        meshData[0].GetVertices(vertices);
        var counts = mesh.GetBonesPerVertex();
        var weights = mesh.GetAllBoneWeights();
        var bindposes = mesh.bindposes;
        var bones = renderer.bones;
        if (vertices.Length != 5301 || counts.Length != vertices.Length || weights.Length < vertices.Length ||
            bones.Length != bindposes.Length) throw new InvalidDataException("重算蒙皮缺少导入顶点、权重或逆绑定矩阵");
        var matrices = bones.Select((bone, index) => actor.worldToLocalMatrix * bone.localToWorldMatrix * bindposes[index]).ToArray();
        float twoToActual = 0, allToActual = 0, allToSigned = 0;
        int weightIndex = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 all = Vector3.zero, two = Vector3.zero;
            float twoWeight = 0;
            for (int j = 0; j < counts[i]; j++)
            {
                var weight = weights[weightIndex++];
                var point = matrices[weight.boneIndex].MultiplyPoint3x4(vertices[i]) * weight.weight;
                all += point;
                if (j < 2) { two += point; twoWeight += weight.weight; }
            }
            var actual = bakedToActor.MultiplyPoint3x4(baked[i]);
            twoToActual = Mathf.Max(twoToActual, Vector3.Distance(two / twoWeight, actual));
            allToActual = Mathf.Max(allToActual, Vector3.Distance(all, actual));
            allToSigned = Mathf.Max(allToSigned, Vector3.Distance(all, At(signedVertices, (frame * vertices.Length + i) * 3)));
        }
        Debug.Log($"MOTION_CPU_SKIN frame={frame} top2ToActualM={twoToActual:R} allToActualM={allToActual:R} allToSignedM={allToSigned:R}");
    }
    static async Task Check()
    {
        var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),new List<NavMeshBuildSource>{
            new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(70,.2f,70),transform=Matrix4x4.Translate(Vector3.down*.1f)}},
            new Bounds(Vector3.zero,new Vector3(74,6,74)),Vector3.zero,Quaternion.identity);
        var surface=NavMesh.AddNavMeshData(nav);
        var m=new GameObject("A4 QA Manager").AddComponent<EnemyManager>();
        m.catalog=AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset");
        var lease=await m.SpawnEnemyAsync("stone-carrier",new Vector3(2,0,-3),Quaternion.Euler(0,37,0),null,false);
        var c=lease.Controller; c.tickAutomatically=false;
        var w=c.GetComponent<StoneCarrier>();var stone=w.Stone;var a=c.Entity.Animator;
        Assert(w&&stone&&w.profile.carryPose&&w.profile.carryPose.FrameCount==87&&a.isHuman&&!a.applyRootMotion,
            "真实工奴租约生成同一个实体镇石，正式配置加载87帧，使用 Humanoid 且关闭根运动");
        var art = stone.transform.Find("T25-A4-Unity");
        Assert(art && art.localPosition.sqrMagnitude < 1e-10f && Quaternion.Angle(art.localRotation, Quaternion.identity) < .001f &&
            Vector3.Distance(art.localScale, Vector3.one) < .00001f, "出生的 A4 模型在持姿所用镇石根下，局部变换为单位矩阵");
        var stoneFilters = art.GetComponentsInChildren<MeshFilter>();
        const string stoneMeshPath = "Assets/Art/Enemies/CanJiOu/BallastStoneA4/T25-A4-Unity.fbx";
        int stoneTriangles = stoneFilters.Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
            .Sum(sub => (int)filter.sharedMesh.GetIndexCount(sub) / 3));
        Assert(stoneFilters.Length == 8 && stoneTriangles == 21456 && stoneFilters.All(filter =>
            AssetDatabase.GetAssetPath(filter.sharedMesh) == stoneMeshPath), "同一携带实例绑定八块正式 FBX 网格，共 21456 个三角面");
        var signedClip=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx")
            .OfType<AnimationClip>().Single(s=>s.name=="CanJiOu_WalkCarryingPerson");
        Assert(w.profile.carryPose.Clip==signedClip&&a.avatar.name=="CanJiOu_LOD0Avatar","目标 Avatar 与原 FBX 对应");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab");
        var shoulder=prefab.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:RightShoulder");
        Assert(Quaternion.Angle(shoulder.localRotation,new Quaternion(-.5848165f,-.39165682f,.59883696f,-.38208508f))<.08f,
            "实际 Room prefab 保留签帧使用的右肩覆盖");
        var external=await m.SpawnEnemyAsync("canjiou",new Vector3(-7,0,-7),Quaternion.identity,null,false);
        var ordinary=external.Controller;ordinary.tickAutomatically=false;
        for(int i=0;i<8;i++){ordinary.Tick(.03f);ordinary.Entity.Animator.Update(.03f);}
        Assert(!ordinary.GetComponent<StoneCarrier>()&&ordinary.Entity.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash!=State,
            "普通残机偶共用控制器仍走原状态");
        c.Tick(.03f);a.Update(0);
        Assert(w.TryGetCarryPhase(out _)&&a.GetFloat(Rate)==1,"真实巡逻入口已播放搬运剪辑");
        // 仅在起点定位一次。之后逐次推进实际 Animator，读取状态相位和 LateUpdate 后的石根。
        a.Play(State,0,0);a.Update(0);ReadPose(w);
        var source=SampleFloats(Path.Combine(signed,"vertices.f32"));
        var reference=SampleFloats(Path.Combine(signed,"bones.f32"));
        var csv=File.ReadAllLines(Path.Combine(Path.GetDirectoryName(signed),"CH02-ballast-A4-20261125/motion/final/candidate.csv")).Skip(1).Select(Row).ToArray();
        var skin=c.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.vertexCount==5301);
        var mesh=new Mesh(); int B=25,V=5301;
        var boneNames=JsonUtility.FromJson<BoneList>(File.ReadAllText(Path.Combine(signed,"manifest.json"))).boneNames;
        var bones=boneNames.Select(name=>skin.bones.First(b=>b.name==name)).ToArray();
        Debug.Log($"MOTION_RENDERER {skin.name} mesh={skin.sharedMesh.name} asset={AssetDatabase.GetAssetPath(skin.sharedMesh)} rendererQuality={skin.quality} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} global={QualitySettings.skinWeights} buildTarget={EditorUserBuildSettings.activeBuildTarget} lodScale={skin.transform.lossyScale} actorScale={c.transform.lossyScale}");
        float maxPhase=0,maxStone=0,maxRot=0,maxWrist=0,maxWristAngle=0,maxVertex=0;
        using(var vout=new BinaryWriter(File.Create(Path.Combine(directory,"runtime-vertices.f32"))))
        using(var bout=new BinaryWriter(File.Create(Path.Combine(directory,"runtime-bones.f32"))))
        using(var two=new BinaryWriter(File.Create(Path.Combine(directory,"runtime-vertices-bone2.f32"))))
        using(var four=new BinaryWriter(File.Create(Path.Combine(directory,"runtime-vertices-bone4.f32"))))
        using(var poses=new StreamWriter(Path.Combine(directory,"runtime-poses.csv")))
        using(var writer=new StreamWriter(Path.Combine(directory,"runtime-frames.csv")))
        {
            poses.WriteLine("frame,x,y,z,qx,qy,qz,qw,sx,sy,sz");
            writer.WriteLine("frame,normalized,phaseError,stone_m,stone_deg,leftWrist_m,rightWrist_m,wrist_deg,allVerticesMax_m");
            for(int f=0;f<87;f++)
            {
                if(f>0)a.Update(1f/30f);
                ReadPose(w);
                if(!w.TryGetCarryPhase(out var phase))throw new Exception("搬运状态中断 frame "+f);
                float expected=f/86f;
                float pe=Mathf.Abs(Mathf.DeltaAngle(phase*360f,expected*360f))/360f;
                var row=csv[f];var qp=new Quaternion(row[4],row[5],row[6],row[7]);var root=c.transform;
                foreach(var b in bones)
                {
                    var xyz=root.InverseTransformPoint(b.position);var q=Quaternion.Inverse(root.rotation)*b.rotation;
                    bout.Write(xyz.x);bout.Write(xyz.y);bout.Write(xyz.z);bout.Write(q.x);bout.Write(q.y);bout.Write(q.z);bout.Write(q.w);
                }
                float sd=Vector3.Distance(stone.transform.localPosition,new Vector3(row[1],row[2],row[3]));
                float sr=Quaternion.Angle(stone.transform.localRotation,qp);
                var t=stone.transform;var p=t.localPosition;var qstone=t.localRotation;var sc=t.localScale;
                if (Vector3.Distance(sc, new Vector3(row[8], row[9], row[10])) > .00001f)
                    throw new Exception("镇石实际缩放与签帧不同 frame " + f);
                poses.WriteLine(string.Join(",",new[]{f,p.x,p.y,p.z,qstone.x,qstone.y,qstone.z,qstone.w,sc.x,sc.y,sc.z}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
                var errs=new float[2];float wristAngle=0;
                for(int k=0;k<2;k++)
                {
                    var hand=a.GetBoneTransform(k==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    int baseId=(f*B+(k==0?10:14))*7;
                    errs[k]=Vector3.Distance(root.InverseTransformPoint(hand.position),At(reference,baseId));
                    var rq=new Quaternion(reference[baseId+3],reference[baseId+4],reference[baseId+5],reference[baseId+6]);
                    wristAngle=Mathf.Max(wristAngle,Quaternion.Angle(Quaternion.Inverse(root.rotation)*hand.rotation,rq));
                }
                skin.BakeMesh(mesh,false);var pts=mesh.vertices;
                var mat=root.worldToLocalMatrix*Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one);
                if (f == 0 || f == 51) CheckSkinWeights(f, skin, root, pts, mat, source);
                float vertexError=0;
                for(int i=0;i<V;i++)
                {
                    var point=mat.MultiplyPoint3x4(pts[i]);vout.Write(point.x);vout.Write(point.y);vout.Write(point.z);
                    vertexError=Mathf.Max(vertexError,Vector3.Distance(point,At(source,(f*V+i)*3)));
                }
                if(f==0) using(var tr=new BinaryWriter(File.Create(Path.Combine(directory,"runtime-triangles.i32"))))
                    foreach(var index in mesh.triangles)tr.Write(index);
                {
                    var original=skin.quality;
                    foreach(var quality in new[]{SkinQuality.Bone2,SkinQuality.Bone4})
                    {
                        skin.quality=quality;skin.BakeMesh(mesh,false);var alternate=mesh.vertices;
                        var stream=quality==SkinQuality.Bone2?two:four;
                        float error=0;
                        for(int j=0;j<V;j++)
                        {
                            var xyz=mat.MultiplyPoint3x4(alternate[j]);stream.Write(xyz.x);stream.Write(xyz.y);stream.Write(xyz.z);
                            error=Mathf.Max(error,Vector3.Distance(xyz,At(source,(f*V+j)*3)));
                        }
                        if(f==0)Debug.Log("MOTION_SKIN_PROBE "+quality+" maxM="+error.ToString("R",CultureInfo.InvariantCulture));
                    }
                    skin.quality=original;
                }
                maxPhase=Mathf.Max(maxPhase,pe);maxStone=Mathf.Max(maxStone,sd);maxRot=Mathf.Max(maxRot,sr);
                maxWrist=Mathf.Max(maxWrist,errs[0],errs[1]);maxWristAngle=Mathf.Max(maxWristAngle,wristAngle);
                maxVertex=Mathf.Max(maxVertex,vertexError);
                writer.WriteLine(string.Join(",",new[]{f,phase,pe,sd,sr,errs[0],errs[1],wristAngle,vertexError}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));
            }
        }
        Debug.Log($"MOTION_ERRORS phase={maxPhase:R} stoneM={maxStone:R} stoneDeg={maxRot:R} wristM={maxWrist:R} wristDeg={maxWristAngle:R} vertsM={maxVertex:R}");
        float before=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
        c.SetSuspended(true);a.Update(.43f);ReadPose(w);
        Assert(Mathf.Abs(a.GetCurrentAnimatorStateInfo(0).normalizedTime-before)<.0001f&&a.speed==1,"房间暂停冻结搬运相位且不改变 Animator 全局速度");
        c.SetSuspended(false);c.Tick(.04f);a.Update(.15f);ReadPose(w);
        Assert(a.GetCurrentAnimatorStateInfo(0).normalizedTime>before+.015f,"恢复继续同一动作");
        before=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
        a.speed=.5f;a.Update(.2f);ReadPose(w);
        Assert(Mathf.Abs(a.GetCurrentAnimatorStateInfo(0).normalizedTime-before-.1f/signedClip.length)<.0005f,
            "动画全局半速时石根从同一半速播放相位采样");
        a.speed=1;
        w.enabled=false; before=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
        a.Update(.22f);Assert(!w.TryGetCarryPhase(out _)&&Mathf.Abs(a.GetCurrentAnimatorStateInfo(0).normalizedTime-before)<.0001f,
            "只禁用持姿组件同样冻结搬运动作");
        w.enabled=true;c.Tick(.03f);a.Update(.08f);ReadPose(w);
        Assert(w.TryGetCarryPhase(out _)&&a.GetCurrentAnimatorStateInfo(0).normalizedTime>before,"重新启用后继续持姿");
        var pos=stone.transform.localPosition;
        Assert(c.TryNotifyHit(),"原反应入口接管动作");
        a.Update(.025f);ReadPose(w);
        Assert(!w.TryGetCarryPhase(out _)&&Vector3.Distance(stone.transform.localPosition,pos)<.0001f,"受击过渡不写石根");
        bool resumed=false,hitSeen=false;
        for(int i=0;i<150;i++)
        {
            a.Update(.025f);c.Tick(.025f);ReadPose(w);
            hitSeen|=a.GetCurrentAnimatorStateInfo(0).IsName("Hit")||a.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Hit");
            if(w.TryGetCarryPhase(out _)&&i>3){resumed=true;break;}
        }
        Assert(hitSeen&&resumed&&w.Stone==stone,"原受击状态实际播放完，恢复同一镇石的搬运");
        Assert(c.TryDie()&&stone.State==BallastStone.Phase.Dropped&&!stone.transform.parent,"死亡立即脱石");
        var arbitrary=new Vector3(8,2,-6);stone.transform.position=arbitrary;
        a.Update(.20f);ReadPose(w);
        Assert(!w.TryGetCarryPhase(out _)&&Vector3.Distance(stone.transform.position,arbitrary)<.0001f,"死亡过渡与晚帧不能写已掉落的石头");
        var oldId=c.GetInstanceID();m.Recycle(lease);
        var next=await m.SpawnEnemyAsync("stone-carrier",Vector3.left*2,Quaternion.identity,null,false);
        var replacement=next.Controller.GetComponent<StoneCarrier>();next.Controller.tickAutomatically=false;
        next.Controller.Entity.Animator.Update(0);ReadPose(replacement);
        Assert(oldId==next.Controller.GetInstanceID()&&replacement.Stone!=stone&&replacement.TryGetCarryPhase(out var start)&&start<.01f,"同物体新租约重置相位、拥有新镇石");
        var attached=replacement.Stone;
        Object.Destroy(next.Controller.gameObject);await Awaitable.NextFrameAsync();await Awaitable.NextFrameAsync();
        Assert(!attached&&stone,"销毁工奴清理当前背载子物体，场景内此前的落石仍存活");
        Assert(maxPhase<.00005f&&maxStone<.0001f&&maxRot<.1f&&maxWrist<.0003f&&maxWristAngle<.12f,
            "87帧实际 Animator 相位、石根位置/朝向/缩放及双腕位置/朝向通过");
        Assert(float.IsFinite(maxVertex) && new FileInfo(Path.Combine(directory,"runtime-vertices.f32")).Length == 87L * V * 3 * sizeof(float),
            "461187个蒙皮顶点样本已导出，表面净空使用同一文件进行三角面复算");
        surface.Remove();Object.Destroy(mesh);
    }
}
