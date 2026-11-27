using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class WorkshopTenonAssembly
    {
        public const string SourceScene = "Assets/Scene/Chapter02_SuspendedPath.unity";
        public const string SliceScene = "Assets/Scene/Chapter02_WorkshopTenon_Slice.unity";
        const string Data = "Assets/ScriptableObjects/Chapter02Workshop";
        const string Donor = "Assets/Scene/KaogongP1Trial.unity";
        const string ReviewProp = "Assets/Review/WorkshopMasterTenonR01/WorkshopMasterTenonR01.prefab";

        static Transform Find(Scene scene, string name) => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name==name);
        static Transform Child(Transform parent,string name,Vector3 position)
        {
            var t=new GameObject(name).transform; t.SetParent(parent,false); t.position=position; return t;
        }
        static RpgStageDefinition Stage(string id,string title,string instructions)
        {
            if (!AssetDatabase.IsValidFolder(Data)) AssetDatabase.CreateFolder("Assets/ScriptableObjects","Chapter02Workshop");
            string path=Data+"/"+id+".asset";
            var stage=AssetDatabase.LoadAssetAtPath<RpgStageDefinition>(path);
            if(stage) return stage;
            stage=ScriptableObject.CreateInstance<RpgStageDefinition>();
            stage.stageId=id; stage.displayName=title; stage.instructions=instructions;
            stage.rewardCurrency=0; stage.startsInCombat=false;
            AssetDatabase.CreateAsset(stage,path); return stage;
        }

        // Builds a separate saved scene. The chapter author may call BindWorkshop in their own Undo/backup workflow.
        public static void BuildSlice()
        {
            var scene=EditorSceneManager.OpenScene(SourceScene);
            if (!EditorSceneManager.SaveScene(scene,SliceScene,true)) throw new IOException(SliceScene);
            scene=EditorSceneManager.OpenScene(SliceScene);
            var player=CreateChapterPlayer(scene,out var flow,out var manager);
            player.saveFileName="ch02-workshop-slice.json";
            var root=player.gameObject;
            var workshop=Stage("CH02_WORKSHOP_TENON","取回校准主榫","肃清工坊伏击，从承座取出主榫，返回司衡台插紧。");
            var descent=Stage("CH02_B_DESCENT","呼唤 B 篮","中枢已归正，在分控台操作 B 篮。");
            var checkpoint=Child(root.transform,"④ 中控返程检查点",Find(scene,"中控操作位").position).gameObject.AddComponent<NavigationNode>();
            checkpoint.nodeId="CH02_HUB";
            var obj=BindWorkshop(scene,player,manager,workshop,AssetDatabase.LoadAssetAtPath<GameObject>(ReviewProp), includeReviewJaws:true);
            var next=root.AddComponent<SignalChapterObjective>();
            flow.stages=new[]{new ChapterObjectiveStage{stage=workshop,objectives=new ChapterObjective[]{obj}},new ChapterObjectiveStage{stage=descent,objectives=new ChapterObjective[]{next}}};
            player.ConfigureRoute(new[]{workshop,descent},new[]{checkpoint,checkpoint}); flow.finalCheckpoint=checkpoint;
            foreach (var label in new[]{"工坊院口","主榫取用站位","空地中央"}) {
                var node=Child(root.transform,"导航 · "+label,Find(scene,label).position).gameObject.AddComponent<NavigationNode>();
                node.nodeId="CH02_"+label;
            }
            player.locomotion.rigRoot.position=checkpoint.transform.position;
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException(SliceScene);
            EditorSceneManager.OpenScene(SliceScene);
        }

        public static LinearRpgController CreateChapterPlayer(Scene scene,out ObjectiveChapterFlow flow,out EnemyManager manager)
        {
            if(!scene.isLoaded || Application.isPlaying) throw new InvalidOperationException("需要打开的二章编辑场景");
            var prior=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LinearRpgController>(true)).SingleOrDefault();
            if(prior)
            {
                flow=prior.chapter as ObjectiveChapterFlow;
                manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnemyManager>(true)).SingleOrDefault();
                if(!flow || !manager || !prior.handTracking || !prior.locomotion) throw new InvalidOperationException("请核对已有玩家的章节、手部、导航及敌人目录");
                UseChapterSky(prior.locomotion);
                return prior;
            }
            var oldObserver=Find(scene,"入口 · XR 观察位");
            if(SceneManager.GetSceneByPath(Donor).isLoaded) throw new InvalidOperationException("玩家参考场景已打开。请在独立工程或单独二章场景中装配，以保留其编辑状态");
            var donor=EditorSceneManager.OpenScene(Donor,OpenSceneMode.Additive);
            var template=donor.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            var root=new GameObject("二章 · 玩家与工坊校准");
            SceneManager.MoveGameObjectToScene(root,donor);
            var sk=template.skills; var lab=template.handTracking;
            var rig=template.locomotion.rigRoot; var head=template.locomotion.head;
            var pose=template.handPoseSettings; var catalog=template.skillResources;
            var nav=root.AddComponent<RpgLocomotionController>(); EditorUtility.CopySerialized(template.locomotion,nav);
            rig.SetParent(root.transform,true); sk.transform.SetParent(root.transform,true);
            sk.name="玩家手势、语音与术式"; sk.showDesktopControls=false;
            if (sk.view) sk.view.enabled=false;
            var labView=sk.GetComponent<NarutoVR.SealLab.SealLabView>(); if (labView) labView.enabled=false;
            lab.recordOnStart=false;
            nav.rigRoot=rig; nav.head=head; nav.rigFloorOffset=0;
            nav.useRayNavigation=true; nav.floorLayers=~(1<<2); nav.obstacleLayers=~(1<<2);
            SceneManager.MoveGameObjectToScene(root,scene); EditorSceneManager.CloseScene(donor,true);
            SceneManager.SetActiveScene(scene);
            if (!lab || !(lab.xrHandsSource || (lab.leftTrackedHand && lab.rightTrackedHand))) throw new InvalidOperationException("失去实际手部追踪来源");
            var player=root.AddComponent<LinearRpgController>();
            player.skills=sk; player.handTracking=lab; player.handPoseSettings=pose; player.locomotion=nav;
            player.skillResources=catalog;
            player.playerDefinition=AssetDatabase.LoadAssetAtPath<RpgPlayerDefinition>("Assets/ScriptableObjects/玩家定义配置/默认玩家.asset");
            player.roomControlsEncounter=true; player.autoLoad=true; player.saveFileName="chapter02-suspended-path.json";
            root.AddComponent<RpgDamageReceiver>();
            manager=root.AddComponent<EnemyManager>();
            manager.catalog=AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset");
            flow=root.AddComponent<ObjectiveChapterFlow>(); player.chapter=flow; flow.rpg=player;
            oldObserver.gameObject.SetActive(false);
            UseChapterSky(nav);
            EditorSceneManager.MarkSceneDirty(scene);
            return player;
        }

        static void UseChapterSky(RpgLocomotionController locomotion)
        {
            if (!locomotion.head || !locomotion.head.TryGetComponent<Camera>(out var eye))
                throw new InvalidOperationException("二章玩家的头部锚点缺少眼位相机");
            if (eye.clearFlags == CameraClearFlags.Skybox) return;
            eye.clearFlags = CameraClearFlags.Skybox;
            PrefabUtility.RecordPrefabInstancePropertyModifications(eye);
            EditorSceneManager.MarkSceneDirty(eye.gameObject.scene);
        }

        static Transform PrepareWhiteboxHolder(Transform stand,Transform root)
        {
            var filter=stand.GetComponent<MeshFilter>();
            var renderer=stand.GetComponent<MeshRenderer>();
            if(!filter || !renderer || filter.sharedMesh.subMeshCount!=5 || renderer.sharedMaterials.Length!=5 ||
                renderer.sharedMaterials[3].name!="WorkshopLimeInlay")
                throw new InvalidOperationException("承座外观已替换，请传入真实铜托顶面的 authoredHolder");
            var source=filter.sharedMesh;
            var plate=source.GetSubMesh(1).bounds;
            var marker=source.GetSubMesh(3).bounds;
            if(Mathf.Abs(plate.max.y-.65f)>.005f || Mathf.Abs(marker.min.y-plate.max.y)>.005f ||
                Mathf.Abs(marker.size.y-.35f)>.005f) throw new InvalidOperationException("白盒假榫或铜托已变更");
            if(!AssetDatabase.IsValidFolder(Data)) AssetDatabase.CreateFolder("Assets/ScriptableObjects","Chapter02Workshop");
            var path=Data+"/WorkshopHolder.asset";
            var cleaned=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!cleaned){
                cleaned=UnityEngine.Object.Instantiate(source); cleaned.name="工坊承座（石座、铜托和嵌圈）"; cleaned.SetTriangles(Array.Empty<int>(),3,false);
                var b=source.GetSubMesh(0).bounds; b.Encapsulate(plate); b.Encapsulate(source.GetSubMesh(2).bounds); b.Encapsulate(source.GetSubMesh(4).bounds); cleaned.bounds=b;
                AssetDatabase.CreateAsset(cleaned,path);
            }
            filter.sharedMesh=cleaned;
            var support=stand.gameObject.AddComponent<BoxCollider>(); support.center=plate.center; support.size=plate.size;
            var top=plate.center; top.y=plate.max.y+.003f;
            return Child(root,"主榫插入端 · 取用座",stand.TransformPoint(top));
        }

        public static WorkshopTenonObjective BindWorkshop(Scene scene,LinearRpgController player,EnemyManager manager,RpgStageDefinition stage,GameObject propPrefab,Transform authoredHolder=null,bool includeReviewJaws=false)
        {
            if(!scene.isLoaded || !player || !manager || !stage || !propPrefab || !propPrefab.GetComponent<GameItemBase>() ||
               player.gameObject.scene!=scene || manager.gameObject.scene!=scene)
                throw new InvalidOperationException("工坊接线需要同场景玩家、敌人管理器和已完成的主榫道具 Prefab");
            var existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WorkshopTenonObjective>(true)).SingleOrDefault();
            if(existing) {
                existing.ValidateObjective();
                if(existing.player!=player || existing.stage!=stage || existing.courtyard.enemyManager!=manager ||
                    PrefabUtility.GetCorrespondingObjectFromSource(existing.tenon.gameObject)!=propPrefab) throw new InvalidOperationException("已存在其他工坊主榫装配");
                return existing;
            }
            var roofs=WorkshopRoofEncounterSetup.InstallPosts(scene);
            var elite=Find(scene,"精英出生位"); var floor=Find(scene,"工坊院场 · 地面与崖基").GetComponent<BoxCollider>();
            var stand=authoredHolder ? null : Find(scene,"主榫与承座"); var console=Find(scene,"方榫中控 · 石台与插口");
            var root=new GameObject("③ 工坊清场与④主榫归座"); SceneManager.MoveGameObjectToScene(root,scene);
            var obj=root.AddComponent<WorkshopTenonObjective>(); obj.player=player; obj.stage=stage;
            player.roomControlsEncounter=true;
            var room=Child(root.transform,"工坊实际院坪区域",floor.bounds.center).gameObject;
            var area=room.AddComponent<BoxCollider>(); area.isTrigger=true; area.size=new Vector3(floor.bounds.size.x,4,floor.bounds.size.z);
            room.transform.position+=Vector3.up*.65f;
            CombatRoomEncounter Room(string name) {
                var r=Child(room.transform,name,room.transform.position).gameObject.AddComponent<CombatRoomEncounter>();
                r.rpg=player; r.area=area; r.enabled=false; return r;
            }
            obj.roof=Room("F1/F2 屋顶预置"); obj.roof.enemies=roofs;
            obj.courtyard=Room("院场精英租约"); obj.courtyard.enemyManager=manager;
            obj.courtyard.spawnPoints=new[]{new CombatRoomEncounter.SpawnPoint{enemyId="elite-canjiou",point=elite}};
            obj.holder=authoredHolder ? authoredHolder : PrepareWhiteboxHolder(stand,root.transform);
            var origin=obj.holder.position;
            obj.tenon=((GameObject)PrefabUtility.InstantiatePrefab(propPrefab,root.transform)).GetComponent<GameItemBase>();
            obj.tenon.transform.SetPositionAndRotation(origin,obj.holder.rotation); obj.tenon.enabled=false;
            var body=obj.tenon.GetComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            // Independent proxy jaw sets carry real colliders and can be replaced by the approved stand art.
            GameObject Jaws(string name,float spread){
                var pair=Child(root.transform,name,origin); pair.rotation=obj.holder.rotation;
                var bronze=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Props/Zhunwang/Materials/Zhunwang_PatinatedBronze.mat");
                void Piece(string label,Vector3 offset,Vector3 size)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=label; go.transform.SetParent(pair,false);
                    go.transform.localPosition=offset; go.transform.localScale=size;
                    if(bronze) go.GetComponent<Renderer>().sharedMaterial=bronze;
                }
                foreach(float sign in new[]{-1f,1f}){
                    string side=sign<0?"左":"右";
                    Piece(side+"导座",new Vector3(sign*spread,.175f,0),new Vector3(.022f,.35f,.04f));
                    Piece(side+"横向挡销",new Vector3(sign*(spread-.013f),.356f,0),new Vector3(.048f,.012f,.030f));
                }
                return pair.gameObject;
            }
            obj.closedJaws = includeReviewJaws ? Jaws("取用座 · 抱紧",.0725f) : Child(root.transform,"取用座 · 锁定状态",origin).gameObject;
            obj.openJaws = includeReviewJaws ? Jaws("取用座 · 退开",.13f) : Child(root.transform,"取用座 · 可取状态",origin).gameObject;
            obj.openJaws.SetActive(false);
            var at=console.TransformPoint(new Vector3(-.12f,1.028f,0));
            var zone=Child(root.transform,"方榫中控 · 对位与下压",at).gameObject.AddComponent<WorkshopTenonSocket>();
            zone.objective=obj; obj.socket=zone;
            zone.mouth=Child(zone.transform,"槽口中心",at); zone.mouth.rotation=console.rotation;
            zone.align=Child(zone.transform,"对齐位",at+console.up*.030f);
            zone.bottom=Child(zone.transform,"坐实位",console.TransformPoint(new Vector3(-.12f,.9239401f,0)));
            GameObject Lamp(string name,Color color,Vector3 point)
            {
                var lens=Child(root.transform,name,point);
                var light=lens.gameObject.AddComponent<Light>(); light.type=LightType.Point;
                light.color=color; light.range=.48f; light.intensity=1.3f; light.shadows=LightShadows.None;
                return lens.gameObject;
            }
            var indicator=at+console.up*.035f+console.forward*.12f;
            obj.consoleFault=Lamp("缺榫 · 朱砂",new Color(1,.09f,.025f),indicator);
            var ice=new Color(.10f,.73f,1);
            obj.consoleReady=Lamp("归位 · 冰蓝",ice,indicator); obj.consoleReady.SetActive(false);
            obj.poweredCircuits=new[]{"A 分控杆","B 分控杆"}.Select(n=>Lamp(n+" · 通电",ice,Find(scene,n).position+Vector3.up*.85f)).ToArray();
            foreach(var circuit in obj.poweredCircuits)circuit.SetActive(false);
            var so=new SerializedObject(zone);
            so.FindProperty("_snapMode").enumValueIndex=1; so.FindProperty("_requireNameMatch").boolValue=false;
            var points=so.FindProperty("_snapPoints"); points.arraySize=2;
            for(int i=0;i<2;i++){
                var p=points.GetArrayElementAtIndex(i); var anchor=i==0?zone.align:zone.bottom;
                p.FindPropertyRelative("target").objectReferenceValue=anchor; p.FindPropertyRelative("rotationTarget").objectReferenceValue=anchor;
                p.FindPropertyRelative("overrideDuration").boolValue=true; p.FindPropertyRelative("duration").floatValue=i==0?.12f:.28f;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); obj.ValidateObjective();
            EditorSceneManager.MarkSceneDirty(scene); return obj;
        }
    }
}
