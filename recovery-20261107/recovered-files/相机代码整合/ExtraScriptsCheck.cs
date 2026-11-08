using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated diagnostic harness: source game scripts are copied unchanged.
public static class ExtraScriptsCheck
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../extra-scripts-validation.txt"));

    private static void Record(string test, bool pass, string actual)
    {
        string line = (pass ? "PASS " : "FAIL ") + test + " | " + actual;
        Report.AppendLine(line);
        Debug.Log("EXTRA_SCRIPTS_CHECK: " + line);
        if (!pass) failures++;
        File.WriteAllText(ReportPath, Report.ToString(), Encoding.UTF8);
    }

    private static void Initialize(ChessBoard board)
    {
        var awake = typeof(ChessBoard).GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (awake != null) awake.Invoke(board, null);
        else typeof(ChessBoard).GetMethod("Start", PrivateInstance).Invoke(board, null);
    }

    private static ChessBoard Board(int[] indices)
    {
        var board = new GameObject("FixtureBoard").AddComponent<ChessBoard>();
        board.chessSlots = new List<ChessSlot>();
        for (int i = 0; i < indices.Length; i++)
        {
            var child = new GameObject("Tile_" + indices[i].ToString("00"));
            child.transform.SetParent(board.transform, false);
            child.transform.position = new Vector3(i, 0, 0);
            var slot = child.AddComponent<ChessSlot>();
            slot.index = indices[i];
            slot.Awake();
        }
        Initialize(board);
        return board;
    }

    private static ChessBoardContorllor Actor(ChessBoard board)
    {
        var actor = new GameObject("FixturePlayer").AddComponent<ChessBoardContorllor>();
        actor.chessBoard = board;
        actor.chessSlots = board.chessSlots;
        actor.currentChessSlot = board.chessSlots[0];
        actor.transform.position = actor.currentChessSlot.transform.position;
        actor.transform.forward = Vector3.right;
        actor.speed = .01f;
        return actor;
    }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Report.AppendLine("Unity " + Application.unityVersion + "; unchanged Extra game scripts");
        File.WriteAllText(ReportPath, "RUNNING latest source\n", Encoding.UTF8);
        var fixturePlayer = new GameObject("FixtureTaggedPlayer");
        fixturePlayer.tag = "Player";
        fixturePlayer.AddComponent<ChessBoardContorllor>();

        var directionSlot = new GameObject("RotatedSlot").AddComponent<ChessSlot>();
        directionSlot.transform.rotation = Quaternion.Euler(0, 90, 0);
        directionSlot.Awake();
        Record("Slot direction follows local +X", Vector3.Dot(directionSlot.moveDir.normalized, directionSlot.transform.right) > .999f,
            "moveDir=" + directionSlot.moveDir + "; transform.right=" + directionSlot.transform.right);
        UnityEngine.Object.DestroyImmediate(directionSlot.gameObject);

        var board = Board(new[] { 2, 0, 3, 1 });
        ChessSlot zero = board.chessSlots.Find(s => s.index == 0);
        ChessSlot next = board.GetChessSlot(zero);
        Record("Next-slot lookup follows index rather than hierarchy order", next.index == 1,
            "hierarchy indices=[2,0,3,1]; after index 0 returned index " + next.index);
        int initialCount = board.chessSlots.Count;
        Initialize(board);
        Record("Board discovery does not duplicate prefilled entries", board.chessSlots.Count == initialCount,
            "before=" + initialCount + "; after=" + board.chessSlots.Count);
        UnityEngine.Object.DestroyImmediate(board.gameObject);

        board = Board(new[] { 0, 1, 2, 3 });
        var actor = Actor(board);
        actor.isMoving = true;
        var moveTo = (IEnumerator)typeof(ChessBoardContorllor).GetMethod("MoveTo", PrivateInstance).Invoke(actor, new object[] { 4 });
        bool yielded = moveTo.MoveNext();
        Record("Four requested steps on four-slot board execute a full lap", yielded,
            "coroutine yielded=" + yielded + "; current index=" + actor.currentChessSlot.index + "; isMoving=" + actor.isMoving);
        actor.StopAllCoroutines();

        actor.currentChessSlot = board.chessSlots[0];
        actor.transform.position = board.chessSlots[0].transform.position;
        actor.transform.forward = Vector3.right;
        moveTo = (IEnumerator)typeof(ChessBoardContorllor).GetMethod("MoveTo", PrivateInstance).Invoke(actor, new object[] { 2 });
        moveTo.MoveNext();
        Record("Current slot remains the source until reaching next slot", actor.currentChessSlot == board.chessSlots[0],
            "current index=" + actor.currentChessSlot.index + "; distance to newly assigned slot=" + Vector3.Distance(actor.transform.position, actor.currentChessSlot.transform.position));
        actor.StopAllCoroutines();

        actor.transform.position = Vector3.zero;
        var zeroSpeed = (IEnumerator)typeof(ChessBoardContorllor).GetMethod("MoveAStep", PrivateInstance).Invoke(actor,
            new object[] { actor.transform, Vector3.zero, Vector3.right, 0f });
        bool stillRunning = true;
        for (int i = 0; i < 5; i++) stillRunning = zeroSpeed.MoveNext();
        Record("Zero-speed movement is rejected rather than waiting forever", !stillRunning,
            "after 5 frames yielded=" + stillRunning + "; position=" + actor.transform.position);

        try
        {
            CameraIntegrationCheck.Run();
            Record("Camera follow, obstruction, orbit and pan checks", true, "all existing camera physics checks passed");
        }
        catch (Exception e) { Record("Camera follow, obstruction, orbit and pan checks", false, e.ToString()); }

        Report.AppendLine("FAILURES=" + failures);
        Report.AppendLine("COMPLETE");
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../extra-scripts-validation.txt"));
        File.WriteAllText(path, Report.ToString(), Encoding.UTF8);
        Debug.Log("EXTRA_SCRIPTS_CHECK_COMPLETE failures=" + failures);
    }
}
