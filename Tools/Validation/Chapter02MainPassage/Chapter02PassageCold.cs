using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Chapter02PassageCold
{
    const string Key = "Chapter02PassageCold";
    const string FileName = "Logs/MainPassageCold.txt";
    static double deadline;

    public static void Run()
    {
        if (Application.companyName != "ChapterPassageFormalQA")
            throw new InvalidOperationException("仅在隔离验收工程运行。");
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Hook() => EditorApplication.update += Inspect;

    static void Inspect()
    {
        if (!SessionState.GetBool(Key, false) || !Application.isPlaying) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 30;
        var bridge = Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
        var road = Object.FindFirstObjectByType<PassageGlyphRoad>();
        bool ready = bridge && bridge.player.CanAct && road;
        if (!ready && EditorApplication.timeSinceStartup < deadline) return;
        SessionState.EraseBool(Key);
        bool passed = ready && bridge.player.StageIndex == 2 && bridge.player.Model.Chapter.IsComplete(bridge.drawingStage.stageId)
            && bridge.panel.template.effect is PassageGlyphEffect && !bridge.panel.IsWaiting && !bridge.button.gameObject.activeSelf
            && bridge.button.cap.name == "青铜压面" && !bridge.brokenBarrier.activeSelf && road.Progress == 1
            && road.GetComponent<BoxCollider>().enabled && !bridge.repairedDeck.GetComponent<MeshRenderer>().enabled
            && Object.FindObjectsByType<PassageGlyphRoad>(FindObjectsSortMode.None).Length == 1;
        string result = (passed ? "PASS" : "FAIL") + " new Unity process, unmodified serialized startup settings; stage=" +
            (bridge ? bridge.player.StageIndex : -1) + ", road=" + (road ? road.Progress : -1) +
            ", stone=" + (bridge && bridge.repairedDeck.GetComponent<MeshRenderer>().enabled);
        File.WriteAllText(FileName,result + Environment.NewLine);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
