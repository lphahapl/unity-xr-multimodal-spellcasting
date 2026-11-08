using System;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CameraIntegrationCheck
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("CAMERA_CHECK_PASS: " + message);
    }

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(10, 0, 3);
        var controller = player.AddComponent<ChessBoardContorllor>();
        var cameraObject = new GameObject("CameraUnderTest");
        var camera = cameraObject.AddComponent<ChessBoardCamera>();
        camera.contorllor = controller;
        camera.offset = new Vector3(0, 5, -8);
        camera.rotate = new Vector3(30, 0, 0);
        camera.pivotHeight = 1.5f;
        camera.barrierLayer = 1 << 0;
        var lateUpdate = typeof(ChessBoardCamera).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Action tick = () => { Physics.SyncTransforms(); lateUpdate.Invoke(camera, null); };
        Vector3 initialPosition = player.transform.position + camera.offset;
        tick();
        Check(Vector3.Distance(camera.transform.position, initialPosition) < .0001f, "Original follow offset preserved");
        Check(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(camera.rotate)) < .01f, "Original rotation preserved");

        Vector3 pivot = player.transform.position + Vector3.up * camera.pivotHeight;
        var wall = new GameObject("Barrier");
        wall.transform.position = (pivot + initialPosition) / 2;
        var collider = wall.AddComponent<SphereCollider>();
        collider.radius = .7f;
        tick();
        Vector3 dir = (initialPosition - pivot).normalized;
        float expected = Vector3.Distance(pivot, wall.transform.position) - collider.radius - .05f;
        Check(Mathf.Abs(Vector3.Distance(camera.transform.position, pivot) - expected) < .001f, "Camera stops 0.05m before the obstacle");
        Check(Vector3.Distance(camera.transform.position, wall.transform.position) > collider.radius, "Camera remains outside obstacle");
        Check(camera.offset == new Vector3(0, 5, -8), "Collision does not overwrite desired offset");
        collider.isTrigger = true;
        tick();
        Check(Vector3.Distance(camera.transform.position, initialPosition) < .0001f, "Trigger volumes do not obstruct camera");
        collider.isTrigger = false;
        wall.layer = 8;
        tick();
        Check(Vector3.Distance(camera.transform.position, initialPosition) < .0001f, "Layer mask excludes unrelated colliders");
        UnityEngine.Object.DestroyImmediate(wall);
        tick();
        Check(Vector3.Distance(camera.transform.position, initialPosition) < .0001f, "Camera restores desired position after obstacle removal");

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ChessBoardCamera).GetField("yaw", flags).SetValue(camera, 90f);
        Vector3 pan = new Vector3(2, 0, 1);
        typeof(ChessBoardCamera).GetField("dragingOffset", flags).SetValue(camera, pan);
        tick();
        Vector3 orbited = pivot + Quaternion.AngleAxis(90, Vector3.up) * (initialPosition - pivot) + pan;
        Check(Vector3.Distance(camera.transform.position, orbited) < .0001f, "Orbit and pan are combined before obstruction test");
        Vector3 displacement = new Vector3(3, 1, 2);
        player.transform.position += displacement;
        tick();
        Check(Vector3.Distance(camera.transform.position, orbited + displacement) < .0001f, "Pivot follows moving player");
        File.WriteAllText(Path.Combine(Application.dataPath, "../../camera-validation.txt"),
            "Unity " + Application.unityVersion + "\nPASS: follow, rotation, 0.05m obstacle clearance, no offset feedback, trigger filtering, layer mask, recovery, orbit+pan, moving target\n");
        Debug.Log("CAMERA_INTEGRATION_ALL_PASS");
    }
}
