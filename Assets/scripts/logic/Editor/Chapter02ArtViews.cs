using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.Editor
{
    public static class Chapter02ArtViews
    {
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string Menu = "Tools/NarutoVR/第二章断衡悬道/";
        static GameObject isolatedHoist;

        [MenuItem(Menu + "全场剖面")]
        static void Overview() => Frame("04", new Vector3(72.5f, 130, 75.4f), new Vector3(-12.5f, 15, .4f));

        [MenuItem(Menu + "衡梁双索正侧面")]
        static void HoistSide() => Frame("衡梁", new Vector3(-230, 5, 0), new Vector3(0, 5, 0), true);

        [MenuItem(Menu + "左侧工坊与下降留白")]
        static void LeftCourt() => Frame("04", new Vector3(6, 5.8f, 8.2f), new Vector3(-21.5f, 1.5f, -5.6f));

        [MenuItem(Menu + "石门与断桥")]
        static void Gate() => Frame("01", new Vector3(-18.1f, 4.4f, 17.1f), new Vector3(-5.1f, 1.5f, 10.1f));

        [MenuItem(Menu + "祭坛与背石工奴")]
        static void Altar() => Frame("02", new Vector3(-9, 10, -10), new Vector3(14, .3f, 11));

        [MenuItem(Menu + "工坊与取榫位")]
        static void Workshop() => Frame("03", new Vector3(.2f, 7.3f, .65f), new Vector3(19.8f, 1.1f, 19.7f));

        [MenuItem(Menu + "工坊连接桥旁的绝壁")]
        static void WorkshopBridge() => Frame("04", new Vector3(-12.55f, 1.62f, 1.3f), new Vector3(-25.5f, 1.88f, -1.1f));

        [MenuItem(Menu + "中控与双篮接驳")]
        static void Docks() => Frame("04", new Vector3(7, 11, 13.4f), new Vector3(-14, .9f, 1.9f));

        [MenuItem(Menu + "谷底平地")]
        static void Valley() => Frame("05", new Vector3(1.12f, 1.72f, .2f), new Vector3(-22, 1.55f, 6));

        static void Frame(string zone, Vector3 eye, Vector3 target, bool orthographic = false)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.Log("退出 Play 后可从这里打开第二章的美术场景。");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith(zone + " ·", System.StringComparison.Ordinal)) continue;
                var visibility = SceneVisibilityManager.instance;
                if (orthographic)
                {
                    visibility.Isolate(root, true);
                    isolatedHoist = root;
                }
                else if (isolatedHoist && isolatedHoist.scene == scene)
                {
                    if (visibility.IsCurrentStageIsolated()) visibility.ExitIsolation();
                    isolatedHoist = null;
                }
                var pivot = root.transform.TransformPoint(target);
                var direction = pivot - root.transform.TransformPoint(eye);
                var view = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
                view.in2DMode = false;
                view.LookAt(pivot, Quaternion.LookRotation(direction), direction.magnitude * .45f, orthographic, true);
                view.Focus();
                return;
            }
            Debug.LogWarning("第二章缺少分区根节点 " + zone);
        }
    }
}
