using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class Ch02BronzeMount
    {
        public const string DeskName = "墨规画符石案";
        public const string MountName = "石案青铜按钮 · 固定座";
        public const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";

        public static bool Apply(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.path != ScenePath) throw new InvalidOperationException("需要正式二章路径。");
            var lesson = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Chapter02BridgeEquipment>(true)).Single();
            var button = lesson.button;
            var cap = button.cap;
            var fixture = lesson.panel.transform.parent;
            var desk = fixture.parent;
            if (desk.name != DeskName) throw new InvalidOperationException("原剧情石案绑定有变。");
            if (desk.Find(MountName)) { Verify(scene); return false; }
            if (cap.parent != button.transform || button.transform.parent != fixture)
                throw new InvalidOperationException("现场已经编辑原扣或画板挂接，请核对。");

            var patina = Load("Assets/Art/Props/Zhunwang/Materials/Zhunwang_PatinatedBronze.mat");
            var brass = Load("Assets/Art/Props/Zhunwang/Materials/Zhunwang_BurnishedBronze.mat");
            var faceBronze = Load("Assets/Materials/Chapter01/旧铜.mat");
            var checkpoint = lesson.player.CheckpointAt(1);
            if (checkpoint.nodeId != "CH02_BRIDGE_DRAW") throw new InvalidOperationException("画符检查点已变更。");
            var reach = checkpoint.GetComponent<ReachAreaChapterObjective>();
            if (!reach || !reach.area || !reach.area.isTrigger) throw new InvalidOperationException("画符区缺失。");

            var stand = desk.TransformPoint(new Vector3(-.67f, 0, -.73f));
            Undo.RecordObject(checkpoint.transform, "设置可触石案站位");
            checkpoint.transform.position = stand;
            Undo.RecordObject(reach.area, "对齐上手位置和来路");
            reach.area.center = checkpoint.transform.InverseTransformPoint(stand + Vector3.up*.10f - Vector3.right*.16f);
            reach.area.size = new Vector3(1.18f, .70f, 1.34f);
            Vector3 board = desk.TransformPoint(new Vector3(-.32f,1.34f,-.375f));
            Vector3 facing = Vector3.ProjectOnPlane(board - stand,Vector3.up).normalized;
            Undo.RecordObject(fixture, "向玩家展开原方矩范本");
            fixture.SetPositionAndRotation(board,Quaternion.LookRotation(facing,Vector3.up));

            var mount = new GameObject(MountName).transform;
            Undo.RegisterCreatedObjectUndo(mount.gameObject,"安装石案青铜按钮");
            mount.SetParent(desk,false);
            mount.localPosition = new Vector3(-.18f,1.012f,-.376f);
            var outward = new Vector3(-.38f,.66f,-.65f).normalized;
            mount.localRotation = Quaternion.LookRotation(-outward,Vector3.up);

            // The lateral lug overlaps the old bronze apron. The dark bezel and narrow polished lip
            // support the shallow convex face. All three are attached to the stone, above the walking plane.
            var lug = Shape(mount,PrimitiveType.Cube,"铜耳托",patina);
            lug.transform.SetLocalPositionAndRotation(new Vector3(0,-.006f,.067f),Quaternion.identity);
            lug.transform.localScale = new Vector3(.095f,.065f,.112f);
            Disc(mount,"包浆凸缘",patina,.153f,.032f,0f);
            Disc(mount,"磨亮座沿",brass,.148f,.007f,-.016f);
            Disc(mount,"压面周槽",patina,.129f,.005f,-.0195f);

            Undo.SetTransformParent(cap,mount,"将原接触帽与常显实体对齐");
            Undo.RecordObject(cap,"调整青铜压面");
            Undo.RecordObject(cap.gameObject,"青铜按钮命名");
            cap.name = "青铜压面";
            cap.SetLocalPositionAndRotation(new Vector3(0,0,-.030f),Quaternion.identity);
            cap.localScale = new Vector3(.114f,.114f,.007f);
            var filter = cap.GetComponent<MeshFilter>();
            var renderer = cap.GetComponent<MeshRenderer>();
            Undo.RecordObject(filter,"青铜压帽网格");
            Undo.RecordObject(renderer,"青铜压帽材质");
            filter.sharedMesh=PrimitiveMesh(PrimitiveType.Sphere);renderer.sharedMaterial=faceBronze;
            var stem=Disc(mount,"压帽侧壁",brass,.112f,.009f,-.0255f);
            stem.transform.SetParent(cap,true);
            Undo.RecordObject(button,"按钮触达范围");
            button.touchRadius=.055f;
            button.travelMetres=.003f;
            // While inactive the button reports its own transform origin. Keep that fallback on the face.
            Undo.RecordObject(button.transform,"按钮接线轴");
            button.transform.SetPositionAndRotation(cap.TransformPoint(new Vector3(0,0,-.5f)),cap.rotation);
            EditorSceneManager.MarkSceneDirty(scene);
            Verify(scene);
            return true;
        }

        static Mesh PrimitiveMesh(PrimitiveType kind)
        {
            var temporary=GameObject.CreatePrimitive(kind);
            var mesh=temporary.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temporary);
            return mesh;
        }
        static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path) ??
            throw new InvalidOperationException(path);
        static GameObject Shape(Transform parent,PrimitiveType kind,string name,Material material)
        {
            var go=GameObject.CreatePrimitive(kind);
            Undo.RegisterCreatedObjectUndo(go,"石案青铜实体");
            go.name=name; go.transform.SetParent(parent,false);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
            return go;
        }
        static GameObject Disc(Transform parent,string name,Material material,float diameter,float depth,float z)
        {
            var go=Shape(parent,PrimitiveType.Cylinder,name,material);
            go.transform.localPosition = new Vector3(0,0,z);
            go.transform.localRotation=Quaternion.Euler(90,0,0);
            go.transform.localScale = new Vector3(diameter,depth*.5f,diameter);
            return go;
        }

        public static void Verify(UnityEngine.SceneManagement.Scene scene)
        {
            var bridge=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Chapter02BridgeEquipment>(true)).Single();
            var cap=bridge.button.cap;
            if (cap.name!="青铜压面" || cap.parent.name!=MountName || cap.parent.parent.name!=DeskName ||
                !cap.gameObject.activeInHierarchy || cap.IsChildOf(bridge.button.transform) ||
                bridge.button.onPressed.GetPersistentEventCount()!=1 ||
                bridge.button.onPressed.GetPersistentTarget(0)!=bridge ||
                bridge.button.onPressed.GetPersistentMethodName(0)!=nameof(Chapter02BridgeEquipment.OpenPanel) ||
                !bridge.panel.template || bridge.panel.template.symbolId!="glyph.square-rule")
                throw new InvalidOperationException("青铜压面、真实回调或方矩板接线不正确。");
        }

        public static void ApplyIsolated()
        {
            if (!UnityEngine.Application.isBatchMode) throw new InvalidOperationException();
            var scene=EditorSceneManager.OpenScene(ScenePath);
            if (Apply(scene) && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存副本");
            Verify(EditorSceneManager.OpenScene(ScenePath)); Debug.Log("CH02_BRONZE_APPLIED");
        }
    }
}
