using System;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BuildChapterStakeGraybox
{
    public static void Run()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/ToolBeltItems/CooldownStake.prefab");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            var item=go.GetComponent<CooldownStake>();
            var shaft=go.transform.Find("橛身");
            var tip=go.transform.Find("探地锥灰盒");
            // Leave a narrow 55 mm insertion section below the original grip and escapement.
            shaft.localPosition=Vector3.up*.130f;
            shaft.localScale=new Vector3(.017f,.075f,.017f);
            tip.localPosition=Vector3.up*.0275f;
            tip.localScale=new Vector3(.008f,.0275f,.008f);
            var upper=go.GetComponent<BoxCollider>();
            upper.center=Vector3.up*.1375f;
            upper.size=new Vector3(.034f,.165f,.034f);
            var envelope=new GameObject("入鞘段").AddComponent<BoxCollider>();
            envelope.transform.SetParent(go.transform,false);
            envelope.center=Vector3.up*.0275f;
            envelope.size=new Vector3(.008f,.055f,.008f);
            item.stowEnvelope=envelope;
            foreach(var obj in new Object[]{shaft,tip,upper,item}) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            if(!PrefabUtility.SaveAsPrefabAsset(go,ChapterStakeInstaller.PrefabPath))
                throw new InvalidOperationException("二章灰盒保存失败");
        }
        finally{Object.DestroyImmediate(go);}
    }
}
