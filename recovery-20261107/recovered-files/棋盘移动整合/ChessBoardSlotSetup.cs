using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor helper for attaching slot components and serializing the Inspector route list.
public static class ChessBoardSlotSetup
{
    private const string Prefabs = "Assets/ModularBoard/Prefabs";

    [MenuItem("Tools/Chess Board/Setup Slots")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("请先退出运行模式，再配置棋盘格子。");
            return;
        }

        AttachPrefabSlots();
        EditorApplication.delayCall += SetupOpenScenes;
    }

    public static void AttachPrefabSlots()
    {
        foreach (string rawPath in Directory.GetFiles(Prefabs, "*.prefab"))
        {
            string path = rawPath.Replace('\\', '/');
            bool board = Path.GetFileNameWithoutExtension(path).StartsWith("Board_", StringComparison.Ordinal);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform[] roots = board ? TileRoots(contents.transform) : new[] { contents.transform };
                if (board && roots.Length != 36)
                    throw new InvalidOperationException("Expected 36 tile roots in " + path);

                for (int i = 0; i < roots.Length; i++)
                {
                    ChessSlot slot = roots[i].GetComponent<ChessSlot>();
                    if (slot == null) slot = roots[i].gameObject.AddComponent<ChessSlot>();
                    slot.index = i;
                    slot.moveDir = roots[i].right;
                    slot.chessType = roots[i].name.EndsWith("_Event", StringComparison.Ordinal)
                        ? ChessType.RandomEvent : ChessType.Common;
                    EditorUtility.SetDirty(slot);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        AssetDatabase.SaveAssets();
    }

    private static Transform[] TileRoots(Transform root)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .Where(t => Regex.IsMatch(t.name, "^Tile_[0-9]{2}_"))
            .OrderBy(t => int.Parse(t.name.Substring(5, 2))).ToArray();
    }

    public static void SetupOpenScenes()
    {
        ChessBoard[] boards = Resources.FindObjectsOfTypeAll<ChessBoard>()
            .Where(b => b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(b)).ToArray();
        if (boards.Length == 0) throw new InvalidOperationException("No ChessBoard found in the open scene");

        int slots = 0;
        foreach (ChessBoard board in boards)
        {
            Transform[] roots = TileRoots(board.transform);
            if (roots.Length != 36) throw new InvalidOperationException("Expected 36 scene tile roots");
            ChessSlot[] discovered = roots.Select(t => t.GetComponent<ChessSlot>()).ToArray();
            if (discovered.Any(s => s == null)) throw new InvalidOperationException("A scene tile is missing ChessSlot");

            // Preserve a complete existing Inspector order; initialize only an empty/incomplete list.
            bool complete = board.chessSlots != null && board.chessSlots.Count == discovered.Length
                && board.chessSlots.All(s => s != null)
                && new HashSet<ChessSlot>(board.chessSlots).SetEquals(discovered);
            Undo.RecordObject(board, "Set up board slots");
            if (!complete) board.chessSlots = discovered.ToList();
            board.startIndex = Mathf.Clamp(board.startIndex, 0, board.chessSlots.Count - 1);
            for (int i = 0; i < board.chessSlots.Count; i++)
            {
                ChessSlot slot = board.chessSlots[i];
                Undo.RecordObject(slot, "Set slot index");
                slot.index = i;
                slot.moveDir = slot.transform.right;
                PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
                EditorUtility.SetDirty(slot);
            }

            if (board.contorllor == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) board.contorllor = player.GetComponent<ChessBoardContorllor>();
            }
            if (board.contorllor != null)
            {
                ChessBoardContorllor controller = board.contorllor;
                Undo.RecordObject(controller, "Set controller board references");
                controller.chessBoard = board;
                controller.chessSlots = board.chessSlots;
                controller.currentChessSlot = board.chessSlots[board.startIndex];
                if (controller.speed <= 0f) controller.speed = 2f;
                EditorUtility.SetDirty(controller);
            }
            EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            if (!string.IsNullOrEmpty(board.gameObject.scene.path))
                EditorSceneManager.SaveScene(board.gameObject.scene);
            slots += board.chessSlots.Count;
        }
        AssetDatabase.SaveAssets();
        string report = "CHESS_BOARD_SETUP_COMPLETE boards=" + boards.Length + " slots=" + slots + "; Inspector order retained; speed configured";
        File.WriteAllText(Path.Combine(Application.dataPath, "../ChessBoardSetupReport.txt"), report);
        Debug.Log(report);
    }
}
