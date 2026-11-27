using System;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class Chapter02PassageBranch
    {
        const string Path = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string GlyphDir = "Assets/Art/Glyphs/";
        const string Branch = "符渡 · 祭坛北沿支路";

        [MenuItem("Tools/NarutoVR/第二章断衡悬道/装配通路符支路与主动符谱")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            var scene = SceneManager.GetSceneByPath(Path);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive);
            var changed = Apply(scene);
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CH02_PASSAGE " + (changed ? "已装配，等待场景负责人保存。" : "已有配置，复核通过。"));
        }

        public static bool Apply(Scene scene)
        {
            var transforms = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
            var lesson = transforms.Select(t => t.GetComponent<Chapter02BridgeEquipment>()).Single(o => o);
            var floor = transforms.Single(t => t.name == "天堑台 · 战斗地面");
            var repaired = lesson.repairedDeck.transform;
            var passage = Asset<GlyphTemplate>(GlyphDir + "Passage.asset");
            var weight = Asset<GlyphTemplate>(GlyphDir + "Qianjun.asset");
            if (!(lesson.drawingStage is Chapter02PassageStage bridgeStage) || bridgeStage.passage != passage || lesson.glyph.penRoot != lesson.equipment.pen.transform)
                throw new InvalidOperationException("请先核对主断桥通路符与正式墨规笔的引用。");
            var existing = transforms.Select(t => t.GetComponent<PassageGlyphAnchor>()).Where(a => a).ToArray();
            var root = repaired.Find(Branch);
            if (root || existing.Length > 0)
            {
                if (!root || existing.Length != 3 || existing.Any(a => !a.transform.IsChildOf(root) || !a.bank || !a.landing) ||
                    !root.TryGetComponent<PassageApproachHint>(out var hint) || hint.player != lesson.player ||
                    !hint.anchors.ToHashSet().SetEquals(existing) ||
                    !lesson.glyph.activeTemplates.Contains(passage) || !lesson.glyph.activeTemplates.Contains(weight))
                    throw new InvalidOperationException("场景已有通路符接线，请核对现有岸位和模板。");
                return false;
            }

            var socket = Asset<GameObject>(GlyphDir + "PassageBankSocket.prefab");
            var stone = Asset<Material>("Assets/Art/Environment/Opening/MountainPolish/FoundationStone.mat");
            var ledge = floor.GetComponents<BoxCollider>().Single(c => c.size.x > 25 && c.size.z > 18);
            // Local -X is the open north edge of the altar. The encounter entrance lies further south.
            var lip = ledge.center + new Vector3(-ledge.size.x * .5f, ledge.size.y * .5f, 3.40f);
            Vector3 edge = floor.TransformPoint(lip);
            Vector3 outwards = -floor.right, across = floor.forward;
            Undo.RecordObject(lesson.glyph, "新增主动符谱");
            lesson.glyph.activeTemplates = (lesson.glyph.activeTemplates ?? Array.Empty<GlyphTemplate>())
                .Concat(new[]{passage, weight}).Distinct().ToArray();
            EditorUtility.SetDirty(lesson.glyph);

            root = Child(repaired, Branch);
            root.SetPositionAndRotation(edge, Quaternion.LookRotation(outwards, Vector3.up));
            // The original repaired deck activates the entire optional spur once the main crossing is complete.
            // Enter via the existing gap between the bridge parapet and the altar's north rail.
            // The sill starts flush with the cliff edge. Its cap ends outside both original rails.
            var sill = Child(root, "旧栏端检修挑台");
            sill.position = edge + outwards * 1.02f;
            Rock("挑台石面", sill, new Vector3(3.20f,.40f,2.04f), Vector3.down*.20f, stone, floor.gameObject.layer);
            Rock("承挑石梁", sill, new Vector3(2.42f,.48f,1.74f), new Vector3(0,-.64f,-.10f), stone, floor.gameObject.layer);
            Vector3 start = edge + outwards * 1.98f;
            var a = Anchor(sill, sill, "甲 · 主岸", start, outwards, socket, floor.gameObject.layer);
            var b = Perch(root, "乙 · 北望台", start + outwards * 5.32f, outwards, 2.6f, socket, stone, floor.gameObject.layer);
            var c = Perch(root, "丙 · 东望台", start + across * 4.1f + outwards * 4.15f, outwards, 2.45f, socket, stone, floor.gameObject.layer);
            var cue = Undo.AddComponent<PassageApproachHint>(root.gameObject);
            cue.anchors = new[]{a,b,c};
            cue.player = lesson.player;
            return true;
        }

        static PassageGlyphAnchor Perch(Transform root, string name, Vector3 center, Vector3 outward, float depth,
            GameObject socket, Material stone, int layer)
        {
            var island = Child(root, name);
            island.SetPositionAndRotation(center, Quaternion.LookRotation(outward));
            Rock("走面石", island, new Vector3(2.4f,.42f,depth), new Vector3(0,-.21f,0), stone, layer);
            Rock("崖柱", island, new Vector3(1.94f,4.7f,depth*.78f), new Vector3(0,-2.75f,.12f), stone, layer);
            return Anchor(island, island, name + "渡榫", center - outward * (depth * .5f - .06f), -outward, socket, layer);
        }

        static void Rock(string name, Transform parent, Vector3 scale, Vector3 offset, Material mat, int layer)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(obj, name);
            obj.name = name;
            obj.layer = layer;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = offset;
            obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic);
        }

        static PassageGlyphAnchor Anchor(Transform parent, Transform bank, string name, Vector3 pos, Vector3 outward,
            GameObject socket, int layer)
        {
            var t = Child(parent,name);
            t.gameObject.layer = layer;
            t.SetPositionAndRotation(pos,Quaternion.LookRotation(outward));
            var hit = Undo.AddComponent<BoxCollider>(t.gameObject);
            hit.isTrigger = true;
            hit.center = Vector3.up*.006f;
            hit.size = new Vector3(.60f,.014f,.30f);
            var anchor = Undo.AddComponent<PassageGlyphAnchor>(t.gameObject);
            anchor.bank = bank;
            anchor.landing = t;
            var art = (GameObject)PrefabUtility.InstantiatePrefab(socket, t);
            Undo.RegisterCreatedObjectUndo(art, name);
            // These socket details are input targets on a narrow walking surface.
            // Keep the plinth and the two piers solid, and let the capsule pass the socket.
            foreach (var shape in art.GetComponentsInChildren<MeshCollider>())
            {
                shape.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
            }
            foreach (float side in new[]{-.35f,.35f})
                PickVolume(t, new Vector3(side,.032f,0), new Vector3(.20f,.14f,.61f));
            PickVolume(t, new Vector3(-.35f,.21f,-.125f), new Vector3(.24f,.32f,.20f));
            foreach (float side in new[]{-.091f,.091f})
                PickVolume(t, new Vector3(-.35f+side,.47f,-.125f), new Vector3(.053f,.21f,.088f));
            PickVolume(t, new Vector3(-.35f,.576f,-.125f), new Vector3(.255f,.054f,.101f));
            PickVolume(t, new Vector3(-.369f,.465f,-.125f), new Vector3(.148f,.032f,.058f));
            return anchor;
        }

        static void PickVolume(Transform anchor, Vector3 center, Vector3 size)
        {
            var shape = Undo.AddComponent<BoxCollider>(anchor.gameObject);
            shape.isTrigger = true;
            shape.center = center;
            shape.size = size;
        }

        static Transform Child(Transform parent,string name)
        {
            var t = new GameObject(name).transform;
            SceneManager.MoveGameObjectToScene(t.gameObject,parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(t.gameObject,name);
            t.SetParent(parent,false);
            return t;
        }

        static T Asset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException(path);
    }
}
