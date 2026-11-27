using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class Chapter02CarryPoseSampler
{
    // 与数值采样共用 Rig 和 BakeMesh。使用有图形设备的 Unity 运行；-candidate 为 87 行逐帧 TRS。
    public static void Capture()
    {
        try
        {
            string output = Arg("-poseOut");
            var poses = File.ReadAllLines(Arg("-candidate")).Skip(1).Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(',').Select(x => float.Parse(x,CultureInfo.InvariantCulture)).ToArray())
                .ToDictionary(a => (int)a[0]);
            var frames = (Arg("-poseFrames") ?? "0,21,43,65,86").Split(',').Select(int.Parse);
            var witnessCsv = Arg("-witness");
            var witnesses = new System.Collections.Generic.Dictionary<int,Vector3>();
            if(witnessCsv != null)
            {
                var lines=File.ReadAllLines(witnessCsv);
                var fields=lines[0].TrimStart('\uFEFF').Split(',');
                int k=Array.IndexOf(fields,"stone_witness_x"), hit=Array.IndexOf(fields,"all_hit");
                if(k<0 || hit<0) throw new Exception("碰撞记录缺少见证点或相交标识");
                foreach(var row in lines.Skip(1).Select(x=>x.Split(',')))
                    if(row[hit]=="1") witnesses[int.Parse(row[0])] = new Vector3(Number(row[k]),Number(row[k+1]),Number(row[k+2]));
            }
            Directory.CreateDirectory(output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new Exception("截图需要活动图形设备");
            using (var rig = new Rig())
            {
                var shader = Shader.Find("Standard");
                if (!shader || !shader.isSupported) throw new Exception("截图工程的 Standard 着色器不可用");
                var figureMat = Surface(shader, Color.white, .12f, .36f, AssetRoot+"Textures/CanJiOu_BaseColor.png");
                var stoneMat = Surface(shader,new Color(.87f,.85f,.79f),0,.23f,"Assets/Materials/Chapter01/CourtyardStone.png");
                var bronzeMat = Surface(shader,new Color(.6745098f,.50980395f,.28235295f),.52f,.41f);
                var stone = new GameObject("BallastStone");
                var stoneObj = Arg("-stoneObj");
                if (stoneObj != null) LoadStoneObj(stone,stoneObj,bronzeMat);
                else
                {
                    Box(stone.transform,"石体",0,new Vector3(1.015f,1.015f,1.015f),stoneMat);
                    Box(stone.transform,"下铜箍",-.28f,new Vector3(1.05f,.095f,1.05f),bronzeMat);
                    Box(stone.transform,"上铜箍", .28f,new Vector3(1.05f,.095f,1.05f),bronzeMat);
                }
                var freezes = rig.skins.Select(skin =>
                {
                    var obj = new GameObject("Baked " + skin.name);
                    obj.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    var filter = obj.AddComponent<MeshFilter>();
                    filter.sharedMesh = new Mesh();
                    var renderer = obj.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = Enumerable.Repeat(figureMat, skin.sharedMesh.subMeshCount).ToArray();
                    skin.enabled = false;
                    return filter;
                }).ToArray();
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.51f,.54f,.59f);
                RenderSettings.ambientIntensity = 1;
                Directional("主光", new Vector3(40,-48,0),new Color(1,.92f,.82f),1.06f);
                Directional("补光", new Vector3(18,135,0),new Color(.78f,.89f,1),.56f);
                var cam = new GameObject("PoseCamera").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.13f,.16f,.19f);
                cam.orthographic = true;
                cam.nearClipPlane = .04f;
                cam.farClipPlane = 20;
                cam.allowHDR = false;
                foreach(int frame in frames)
                {
                    var pose = poses[frame];
                    rig.Sample(Mathf.Min(frame/rig.clip.frameRate,rig.clip.length));
                    for(int i=0;i<rig.skins.Length;i++)
                    {
                        var skin = rig.skins[i];
                        freezes[i].transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
                        skin.BakeMesh(freezes[i].sharedMesh,false);
                        freezes[i].sharedMesh.RecalculateBounds();
                    }
                    stone.transform.SetPositionAndRotation(new Vector3(pose[1],pose[2],pose[3]),
                        new Quaternion(pose[4],pose[5],pose[6],pose[7]));
                    stone.transform.localScale = new Vector3(pose[8],pose[9],pose[10]);
                    string stem = Path.Combine(output,"frame-"+frame.ToString("D2")+"-");
                    float centerY = stoneObj == null ? 1.26f : 1.32f;
                    float fullSize = stoneObj == null ? 1.54f : 1.70f;
                    Shot(cam,stem+"oblique.png",new Vector3(1,-.10f,.85f),new Vector3(0,centerY,.30f),fullSize);
                    Shot(cam,stem+"side.png",new Vector3(1,.015f,-.075f),new Vector3(0,centerY,.34f),fullSize);
                    Shot(cam,stem+"wrists.png",new Vector3(.19f,-.25f,1),new Vector3(0,1.35f,.43f),.78f);
                    Shot(cam,stem+"front.png",new Vector3(0,-.30f,1),new Vector3(0,centerY,.33f),fullSize+.06f);
                    Shot(cam,stem+"rear-oblique.png",new Vector3(-1,.04f,-.62f),new Vector3(0,centerY,.31f),fullSize);
                    if(witnesses.TryGetValue(frame,out var local))
                    {
                        var witness = stone.transform.TransformPoint(local);
                        Shot(cam,stem+"timber-collision.png",new Vector3(1,-.55f,-.15f),witness,.43f,witness);
                    }
                }
            }
            Debug.Log("CH02_CARRY_CAPTURE_PASS " + SystemInfo.graphicsDeviceName);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static Material Surface(Shader shader,Color color,float metallic,float smooth,string texturePath = null)
    {
        var mat = new Material(shader) { color = color };
        mat.SetFloat("_Metallic",metallic);
        mat.SetFloat("_Glossiness",smooth);
        if (texturePath != null)
        {
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!map) throw new Exception("找不到原始漫反射贴图 " + texturePath);
            mat.mainTexture = map;
        }
        return mat;
    }
    static void Box(Transform parent,string label,float height,Vector3 scale,Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = label;
        box.transform.SetParent(parent,false);
        box.transform.localPosition = Vector3.up * height;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = mat;
    }
    static void Directional(string label, Vector3 rotation, Color color, float intensity)
    {
        var light = new GameObject(label).AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(rotation);
        light.intensity = intensity;
        light.color = color;
    }
    static void Shot(Camera cam,string path,Vector3 direction,Vector3 target,float size,Vector3? witness = null)
    {
        cam.orthographicSize = size;
        cam.transform.position = target + direction.normalized*5;
        cam.transform.LookAt(target,Vector3.up);
        var surface = RenderTexture.GetTemporary(960,960,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
        cam.targetTexture = surface;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = surface;
        var texture = new Texture2D(surface.width,surface.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,surface.width,surface.height),0,0);
        if(witness.HasValue)
        {
            var point=cam.WorldToViewportPoint(witness.Value);
            int x=Mathf.RoundToInt(point.x*surface.width), y=Mathf.RoundToInt(point.y*surface.height);
            for(int j=-16;j<=16;j++)
                for(int i=-16;i<=16;i++)
                {
                    int radius=i*i+j*j;
                    if(radius>=144 && radius<=225) texture.SetPixel(x+i,y+j,new Color(1,.28f,.08f));
                }
        }
        texture.Apply();
        File.WriteAllBytes(path,texture.EncodeToPNG());
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(surface);
        Object.DestroyImmediate(texture);
    }
}
