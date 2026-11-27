using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class BackCarryBake
{
    const string Rig = "Assets/Art/Enemies/CanJiOu/CanJiOu_LOD0.fbx";
    const string PathClip = "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_BackCarryBones.anim";
    sealed class Joint
    {
        public Transform bone;
        public Quaternion rest;
        public string path;
        public AnimationCurve[] curves = Enumerable.Range(0,7).Select(i=>new AnimationCurve()).ToArray();
        public Quaternion previous;
    }
    public static void Run()
    {
        try { Bake(); Chapter02CarryPoseSampler.Run(); }
        catch (Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void Bake()
    {
        string input = Environment.GetEnvironmentVariable("BACK_CARRY_DELTAS");
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Rig));
        var root=actor.transform;
        var bones=actor.GetComponentsInChildren<Transform>().Where(b=>b.name.Contains("mixamorig:")).ToDictionary(b=>b.name);
        var joints = new Dictionary<string,Joint>();
        var clip = new AnimationClip {name="CanJiOu_BackCarryBones",frameRate=30};
        var cols = File.ReadAllLines(input).Skip(1).Select(x=>x.Split(',')).ToArray();
        foreach (var row in cols.Where(r=>int.Parse(r[0])==-1)){
            if(!bones.TryGetValue(row[1],out var bone))continue;
            var j=new Joint{bone=bone,rest=Quaternion.Inverse(root.rotation)*bone.rotation,path=AnimationUtility.CalculateTransformPath(bone,root)};
            joints.Add(row[1],j);
        }
        if(joints.Count < 21) throw new Exception("Incomplete source skeleton: "+joints.Count);
        var output = new List<string>{"frame,bone,x,y,z,qx,qy,qz,qw"};
        foreach(var row in cols.Where(r=>int.Parse(r[0])>=0 && joints.ContainsKey(r[1])).OrderBy(r=>int.Parse(r[0])).ThenBy(r=>joints[r[1]].path.Length)){
            var j=joints[row[1]];var v=row.Skip(2).Select(n=>float.Parse(n,CultureInfo.InvariantCulture)).ToArray();
            var q = new Quaternion(v[3],v[4],v[5],v[6])*j.rest;
            output.Add(row[0]+","+row[1]+","+string.Join(",",new[]{v[0],v[1],v[2],q.x,q.y,q.z,q.w}.Select(n=>n.ToString("R",CultureInfo.InvariantCulture))));
        }
        File.WriteAllLines(@"D:/Temp/BackCarry1126QA/R02/bones-target.csv",output);
        Debug.Log("Baked root-space joint poses="+output.Count);
        UnityEngine.Object.DestroyImmediate(actor);
    }
}
