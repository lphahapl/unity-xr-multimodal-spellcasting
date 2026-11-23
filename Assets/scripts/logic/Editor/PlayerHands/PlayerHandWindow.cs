using System;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.Editor
{
    public sealed class PlayerHandWindow : EditorWindow
    {
        [SerializeField] bool right;
        [SerializeField] GameObject master, model, leftPrefab, rightPrefab;
        [SerializeField] Material body, end;
        [SerializeField] WatchMountProfile profile;
        string report;
        Vector2 scroll;
        const string Art=PlayerHandRig.Art;
        [MenuItem("Tools/NarutoVR/首章/玩家手模与准望匣")]
        static void Open() => GetWindow<PlayerHandWindow>("玩家手模与准望匣");
        static T Load<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        void OnEnable()
        {
            if (!master) SetSide(right);
            string side=right ? "Right" : "Left";
            if (!body) body=Load<Material>(Art+side+"Hand.mat");
            if (!end) end=Load<Material>(Art+side+"Cuff.mat");
            if (!profile) profile=Load<WatchMountProfile>(Art+"ZhunwangFit.asset");
            if (!leftPrefab) leftPrefab=Load<GameObject>(Art+"PlayerLeftHand.prefab");
            if (!rightPrefab) rightPrefab=Load<GameObject>(Art+"PlayerRightHand.prefab");
        }
        void SetSide(bool useRight)
        {
            right=useRight;
            string side=right?"Right":"Left";
            master=Load<GameObject>(PlayerHandRig.Master(side));
            model=Load<GameObject>(Art+"Player"+side+"Hand.fbx");
            body=Load<Material>(Art+side+"Hand.mat");
            end=Load<Material>(Art+side+"Cuff.mat");
            report=null;
        }
        static T Field<T>(string name,T value) where T:UnityEngine.Object =>
            (T)EditorGUILayout.ObjectField(name,value,typeof(T),false);
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            bool selectRight=GUILayout.Toolbar(right ? 1 : 0,new[]{"左手","右手"})==1;
            if (selectRight!=right) SetSide(selectRight);
            master=Field("官方母版 FBX",master);
            model=Field("自定义 FBX",model);
            body=Field("槽 0 · 手部主体",body);
            end=Field("槽 1 · 小臂末端",end);
            if (!right) profile=Field("准望匣佩戴配置",profile);
            if (GUILayout.Button("同步导入参数并重新导入")) Run(()=>{
                string path=AssetDatabase.GetAssetPath(model);
                PlayerHandRig.Sync(master,model);
                model=Load<GameObject>(path);
                return "已同步 " + path;
            });
            if (GUILayout.Button("校验骨架和蒙皮")) Run(()=>{
                var issues=PlayerHandRig.Compare(master,model);
                return issues.Count==0 ? "通过：26 个关节、父链、静置变换与蒙皮绑定矩阵一致。" : string.Join("\n",issues);
            });
            if (GUILayout.Button("生成玩家手 Prefab…")) Run(()=>{
                string side=PlayerHandRig.Side(master);
                string path=EditorUtility.SaveFilePanelInProject("保存玩家手","Player"+side+"Hand","prefab","选择输出位置",Art.TrimEnd('/'));
                if (string.IsNullOrEmpty(path)) return "已取消";
                var prefab=PlayerHandAssembly.Build(master,model,body,end,profile,path);
                if (side=="Left") leftPrefab=prefab; else rightPrefab=prefab;
                return "已生成 " + path;
            });
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("当前首章场景",EditorStyles.boldLabel);
            leftPrefab=Field("左手 Prefab",leftPrefab);
            rightPrefab=Field("右手 Prefab",rightPrefab);
            var scene=SceneManager.GetActiveScene();
            if (GUILayout.Button("安装 / 切换玩家手")) Run(()=>{
                PlayerHandAssembly.Install(scene,leftPrefab,rightPrefab);
                return "已连接两只玩家手及现有准望匣。可撤销，场景可手动保存。";
            });
            if (GUILayout.Button("切换为官方玩家手")) Run(()=>{
                PlayerHandAssembly.Install(scene,Load<GameObject>(PlayerHandRig.Visual("Left")),Load<GameObject>(PlayerHandRig.Visual("Right")));
                return "已使用官方显示和原准望匣定位。";
            });
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("开启骨骼线")) Run(()=>{PlayerHandAssembly.BoneLines(scene,true);return "骨骼线已开启";});
            if (GUILayout.Button("关闭骨骼线")) Run(()=>{PlayerHandAssembly.BoneLines(scene,false);return "骨骼线已关闭";});
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report,MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void Run(Func<string> action)
        {
            try { report=action(); }
            catch (Exception ex) { report=ex.Message; }
            Debug.Log("[玩家手模] "+report);
        }
    }
}
