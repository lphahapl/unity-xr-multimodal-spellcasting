// 放在独立工程 Assets/Editor；参数 -executeMethod Chapter02CarryPoseSampler.Run -poseOut <目录>。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static partial class Chapter02CarryPoseSampler
{
    const string AssetRoot = "Assets/Art/Enemies/CanJiOu/";
    const string Model = AssetRoot + "CanJiOu_LOD0.fbx";
    const string Motion = AssetRoot + "Animations/CanJiOu_BackCarryWalk.fbx";
    const int FrameCount = 49;
    [Serializable] class Layout
    {
        public string unityVersion, model, motion, avatar, clip, prefabOverrideBone, coordinates;
        public float clipLength, frameRate, maximumSkinError, wristExcursion;
        public int frames, vertices, triangles, bones, leftWrist, rightWrist;
        public string[] boneNames, bonePaths, rendererNames;
        public int[] rendererVertexStart;
        public float[] frameSeconds;
    }

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static void Run()
    {
        try
        {
            string output = Arg("-poseOut") ?? throw new ArgumentException("缺少 -poseOut");
            Directory.CreateDirectory(output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            using (var rig = new Rig()) Export(output, rig);
            Debug.Log("CH02_CARRY_POSE_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    sealed class Rig : IDisposable
    {
        public GameObject actor;
        public Animator animator;
        public AnimationClip clip;
        public SkinnedMeshRenderer[] skins;
        public Transform[] bones;
        public Transform leftHand, rightHand;
        public string overrideName;
        PlayableGraph graph;
        AnimationClipPlayable player;

        public Rig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (!prefab) throw new InvalidOperationException("未导入角色模型：" + Model);
            actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator = actor.GetComponentInChildren<Animator>();
            clip = AssetDatabase.LoadAllAssetsAtPath(Motion).OfType<AnimationClip>()
                .Single(c => c.name == "CanJiOu_BackCarryWalk");
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.isHuman || !clip.humanMotion)
                throw new InvalidOperationException("角色或动作没有有效 Humanoid Avatar");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            // 正式 CanJiOu_Enemy.prefab 对原 FBX 唯一的非根骨骼旋转覆盖。
            foreach (var node in actor.GetComponentsInChildren<Transform>(true))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(node);
                if (!src || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src, out string _, out long id) ||
                    id != -3302972225415322985L) continue;
                node.localRotation = new Quaternion(-.5848165f, -.39165682f, .59883696f, -.38208508f);
                overrideName = node.name;
            }
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bones = skins.SelectMany(s => s.bones).Concat(new[] { leftHand, rightHand }).Distinct().ToArray();
            if (!leftHand || !rightHand || skins.Length == 0 || bones.Any(b => !b)) throw new Exception("骨骼不完整");
            graph = PlayableGraph.Create("Chapter02CarryPose");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            player = AnimationClipPlayable.Create(graph, clip);
            player.SetApplyFootIK(false);
            player.SetApplyPlayableIK(false);
            player.SetSpeed(0);
            AnimationPlayableOutput.Create(graph, "target avatar", animator).SetSourcePlayable(player);
            graph.Play();
        }
        public void Sample(float time)
        {
            player.SetTime(time);
            graph.Evaluate(0);
            var f=Mathf.Clamp(Mathf.RoundToInt(time*30),0,48);
            var map=actor.GetComponentsInChildren<Transform>(true).GroupBy(x=>x.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var line in File.ReadAllLines(@"D:/Temp/BackCarry1126QA/R02/bones-target.csv").Skip(1)){
                var row=line.Split(','); if(int.Parse(row[0])!=f)continue;
                var v=row.Skip(2).Select(n=>float.Parse(n,CultureInfo.InvariantCulture)).ToArray();
                map[row[1]].SetPositionAndRotation(actor.transform.TransformPoint(new Vector3(v[0],v[1],v[2])),
                    actor.transform.rotation*new Quaternion(v[3],v[4],v[5],v[6]));
            }
        }
        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (actor) Object.DestroyImmediate(actor);
        }
    }

    static void Export(string dir, Rig rig)
    {
        var data = new Layout
        {
            unityVersion = Application.unityVersion, model = Model, motion = Motion, avatar = rig.animator.avatar.name,
            clip = rig.clip.name, clipLength = rig.clip.length, frameRate = rig.clip.frameRate, frames = FrameCount,
            prefabOverrideBone = rig.overrideName, coordinates = "Unity actor-root space, metres, +Y up +Z forward; little-endian; vertices [frame,vertex,xyz], triangles [triangle,corner], bones [frame,bone,xyz+xyzw]",
            bones = rig.bones.Length, boneNames = rig.bones.Select(b => b.name).ToArray(),
            bonePaths = rig.bones.Select(b => AnimationUtility.CalculateTransformPath(b, rig.actor.transform)).ToArray(),
            leftWrist = Array.IndexOf(rig.bones,rig.leftHand), rightWrist = Array.IndexOf(rig.bones,rig.rightHand),
            rendererNames = rig.skins.Select(s => s.name).ToArray(), rendererVertexStart = new int[rig.skins.Length],
            frameSeconds = Enumerable.Range(0, FrameCount).Select(i => Mathf.Min(i / rig.clip.frameRate, rig.clip.length)).ToArray()
        };
        if (Mathf.Abs(rig.clip.length * rig.clip.frameRate - 48f) > .02f) throw new Exception("帧数与源剪辑不符");
        var root = rig.actor.transform;
        using (var topology = Writer(dir,"triangles.i32"))
        using (var weights = new StreamWriter(Path.Combine(dir,"weights.csv")))
        {
            weights.WriteLine("vertex,bone,weight");
            for (int r = 0; r < rig.skins.Length; r++)
            {
                var skin = rig.skins[r];
                var mesh = skin.sharedMesh;
                data.rendererVertexStart[r] = data.vertices;
                int start = data.vertices;
                data.vertices += mesh.vertexCount;
                var triangles = mesh.triangles;
                data.triangles += triangles.Length / 3;
                foreach (int index in triangles) topology.Write(start + index);
                var counts = mesh.GetBonesPerVertex();
                var values = mesh.GetAllBoneWeights();
                var map = skin.bones.Select(b => Array.IndexOf(rig.bones, b)).ToArray();
                int k = 0;
                for (int i = 0; i < counts.Length; i++)
                    for (int n = 0; n < counts[i]; n++)
                    {
                        var weight = values[k++];
                        weights.WriteLine((start+i) + "," + map[weight.boneIndex] + "," + weight.weight.ToString("R",CultureInfo.InvariantCulture));
                    }
            }
        }
        var baked = new Mesh();
        try
        {
            using (var vertices = Writer(dir,"vertices.f32"))
            using (var bones = Writer(dir,"bones.f32"))
            {
                Vector3 first = Vector3.zero;
                for (int f = 0; f < FrameCount; f++)
                {
                    rig.Sample(data.frameSeconds[f]);
                    foreach (var bone in rig.bones)
                    {
                        Write(bones,root.InverseTransformPoint(bone.position));
                        var q = Quaternion.Inverse(root.rotation) * bone.rotation;
                        bones.Write(q.x); bones.Write(q.y); bones.Write(q.z); bones.Write(q.w);
                    }
                    var wrist = root.InverseTransformPoint(rig.leftHand.position);
                    if (f == 0) first = wrist;
                    data.wristExcursion = Mathf.Max(data.wristExcursion, Vector3.Distance(first, wrist));
                    foreach (var skin in rig.skins)
                    {
                        skin.BakeMesh(baked, false);
                        var pts = baked.vertices;
                        // 此 FBX 蒙皮网格带 161.3 倍导入缩放。BakeMesh(false) 得到的网格
                        // 已按绑定矩阵还原尺寸，仅用渲染器的世界位置和方向恢复世界坐标。
                        var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                        var matrix = root.worldToLocalMatrix * toWorld;
                        foreach (var p in pts) Write(vertices, matrix.MultiplyPoint3x4(p));
                        if (f == 0 || f == 24 || f == 48)
                            data.maximumSkinError = Mathf.Max(data.maximumSkinError, CompareSkinning(skin, pts, toWorld));
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(baked); }
        if (data.wristExcursion < .002f || data.maximumSkinError > .012f)
            throw new Exception("动态或尺寸校验失败 wrist=" + data.wristExcursion + " skinError="+data.maximumSkinError);
        File.WriteAllText(Path.Combine(dir,"manifest.json"), JsonUtility.ToJson(data,true));
        Debug.Log($"POSE_FRAMES {data.frames} V {data.vertices} T {data.triangles} B {data.bones} maxSkinError {data.maximumSkinError} wristExcursion {data.wristExcursion}");
    }

    // 与骨骼世界矩阵 × 绑定逆矩阵得到的位置逐点抽样对照，防止 BakeMesh 尺寸被重复放大。
    static float CompareSkinning(SkinnedMeshRenderer skin, Vector3[] points, Matrix4x4 toWorld)
    {
        var mesh = skin.sharedMesh;
        var source = mesh.vertices;
        var bones = skin.bones;
        var bind = mesh.bindposes;
        var counts = mesh.GetBonesPerVertex();
        var weights = mesh.GetAllBoneWeights();
        int k = 0;
        float error = 0;
        for (int i = 0; i < counts.Length; k += counts[i++])
        {
            if (i % 31 != 0 || counts[i] == 0) continue;
            Vector3 position = Vector3.zero;
            for (int j = 0; j < counts[i]; j++)
            {
                var w = weights[k+j];
                position += w.weight * bones[w.boneIndex].TransformPoint(bind[w.boneIndex].MultiplyPoint3x4(source[i]));
            }
            error = Mathf.Max(error, Vector3.Distance(position, toWorld.MultiplyPoint3x4(points[i])));
        }
        return error;
    }
    static BinaryWriter Writer(string dir, string name) => new BinaryWriter(File.Create(Path.Combine(dir,name)));
    static void Write(BinaryWriter writer, Vector3 p) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
}
