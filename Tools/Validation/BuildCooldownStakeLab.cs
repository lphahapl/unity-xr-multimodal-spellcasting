using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BuildCooldownStakeLab
{
    const string Dir = "Assets/Samples/ToolBeltItems/";
    public const string Scene = Dir+"CooldownStakeLab.unity";
    static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
        throw new InvalidOperationException(path);

    static Transform Node(Transform parent, string name, Vector3 at)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent,false); t.localPosition = at;
        return t;
    }

    static Transform Solid(Transform parent,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material mat)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent,false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        return part.transform;
    }

    static Vector3 Polar(float angle,float radius) => Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
    static Mesh BakeDial()
    {
        var path=Dir+"StakeDialMesh.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh=!mesh;
        if(fresh) mesh=new Mesh(); else mesh.Clear();
        var vertices=new List<Vector3>();
        var faces=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
            faces.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }
        foreach(float radius in new[]{.18f,.90f,1f})
            for(int i=0;i<96;i++)
            {
                float a=i*3.75f,b=(i+1)*3.75f;
                Quad(Polar(a,radius+.004f),Polar(b,radius+.004f),Polar(b,radius-.004f),Polar(a,radius-.004f));
            }
        for(int i=0;i<24;i++)
        {
            var angle=i*15f;var side=Polar(angle+90,.0045f);
            var a=Polar(angle,i%6==0?.78f:.84f);var b=Polar(angle,.895f);
            Quad(a-side,b-side,b+side,a+side);
        }
        mesh.SetVertices(vertices);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(fresh) AssetDatabase.CreateAsset(mesh,path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static GameObject Bake()
    {
        var bronze=Load<Material>(Dir+"Bronze.mat");
        var ice=AssetDatabase.LoadAssetAtPath<Material>(Dir+"StakeDialIce.mat");
        if (!ice)
        {
            ice=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ice.SetColor("_BaseColor",new Color(.31f,.86f,1f));
            AssetDatabase.CreateAsset(ice,Dir+"StakeDialIce.mat");
        }
        var go=new GameObject("定律铜橛");
        var item=go.AddComponent<CooldownStake>();
        var body=go.GetComponent<Rigidbody>();
        body.isKinematic=true;body.useGravity=false;
        item.definition=Load<GameItemConfigSO>(Dir+"CooldownStakeItem.asset");
        item.startAtDisplaySlot=true;
        item.keyItem=true;
        var root=go.transform;
        var collider=go.AddComponent<BoxCollider>();
        collider.size=new Vector3(.034f,.22f,.034f); collider.center=Vector3.up*.11f;
        item.grip=Node(root,"握点",Vector3.up*.17f);
        item.insertionTip=Node(root,"探地端",Vector3.zero);
        Solid(root,"橛身",PrimitiveType.Cylinder,Vector3.up*.105f,new Vector3(.017f,.100f,.017f),bronze);
        Solid(root,"探地锥灰盒",PrimitiveType.Cylinder,Vector3.up*.010f,new Vector3(.008f,.010f,.008f),bronze);
        for(int i=0;i<4;i++)
        {
            var h=Solid(root,"方孔盘",PrimitiveType.Cube,Polar(90*i,.013f)+Vector3.up*.207f,new Vector3(.032f,.012f,.006f),bronze);
            h.localRotation=Quaternion.Euler(0,i*90,0);
        }
        item.gear=Node(root,"擒纵齿环",Vector3.up*.105f);
        Solid(item.gear,"环体",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.025f,.006f,.025f),bronze);
        for(int i=0;i<8;i++)
        {
            var t=Solid(item.gear,"齿",PrimitiveType.Cube,Polar(i*45,.0135f),new Vector3(.006f,.011f,.006f),i==0?ice:bronze);
            t.localRotation=Quaternion.Euler(0,i*45,0);
        }
        item.dial=Node(root,"冰蓝日晷",Vector3.zero);
        item.dial.gameObject.AddComponent<MeshFilter>().sharedMesh=BakeDial();
        var dialRenderer=item.dial.gameObject.AddComponent<MeshRenderer>();
        dialRenderer.sharedMaterial=ice;
        dialRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        dialRenderer.receiveShadows=false;
        item.dial.gameObject.SetActive(false);
        item.screenMaterial=Load<Material>("Assets/Materials/StakeGauze.mat");
        var saved=PrefabUtility.SaveAsPrefabAsset(go,Dir+"CooldownStake.prefab");
        Object.DestroyImmediate(go);
        return saved;
    }

    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(Dir+"ItemInteractionLab.unity");
        var prefab=Bake();
        var old=Object.FindFirstObjectByType<ItemLabControls>();
        var host=old.gameObject;
        var p=old.player;var belt=old.belt;var slots=old.slots;var offset=old.beltOffset;
        var boundary=old.items.Select(x=>x.worldFallBoundary).FirstOrDefault(x=>x);
        var center=new Vector3(120,0,120);
        var start=p.CheckpointAt(0);
        start.transform.position=center;
        foreach(var r in start.GetComponentsInChildren<Renderer>())r.enabled=false;
        foreach(var c in start.GetComponentsInChildren<Collider>())c.enabled=false;
        var slab=GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name="铜橛试验石台";slab.transform.position=center-Vector3.up*.10f;
        slab.transform.localScale=new Vector3(11,.20f,11);
        slab.GetComponent<Renderer>().sharedMaterial=Load<Material>("Assets/Materials/Chapter01/CourtyardStone.mat");
        foreach(var other in old.items) other.gameObject.SetActive(false);
        Object.DestroyImmediate(old);
        var lab=host.AddComponent<CooldownStakeLabControls>();
        lab.belt=belt;lab.beltOffset=offset;lab.player=p;lab.slots=slots;
        var stake=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).GetComponent<CooldownStake>();
        stake.transform.position=p.CheckpointAt(0).floorPosition+new Vector3(1.4f,.8f,0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stake.transform);
        stake.home=slots.Cast<ToolBeltSlot>().Single(x=>x.displayKind==GameItemKind.CooldownStake);
        stake.worldFallBoundary=boundary;
        PrefabUtility.RecordPrefabInstancePropertyModifications(stake);
        lab.stake=stake;lab.items=new GameItemBase[]{stake};
        var playerAsset=AssetDatabase.LoadAssetAtPath<RpgPlayerDefinition>(Dir+"CooldownStakeLabPlayer.asset");
        if(!playerAsset)
        {
            playerAsset=Object.Instantiate(p.playerDefinition);
            AssetDatabase.CreateAsset(playerAsset,Dir+"CooldownStakeLabPlayer.asset");
        }
        playerAsset.startingSkills=new[]{"技能定义配置/火球术","技能定义配置/左手护盾","雷光剑配置/雷光剑","技能定义配置/飞炎术"}
            .Select(s=>Load<SkillDefinition>("Assets/ScriptableObjects/"+s+".asset")).ToArray();
        EditorUtility.SetDirty(playerAsset);
        p.playerDefinition=playerAsset;p.autoLoad=false;p.saveFileName="cooldown-stake-lab.json";
        if(!EditorSceneManager.SaveScene(scene,Scene)) throw new InvalidOperationException(Scene);
        AssetDatabase.SaveAssets();
        Debug.Log("STAKE_FIXTURE_SAVED "+Scene);
    }
}
