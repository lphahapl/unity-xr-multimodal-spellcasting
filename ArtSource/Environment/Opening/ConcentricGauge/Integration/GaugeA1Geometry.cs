using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Measurements use imported triangle vertices in the puzzle's reference frame.
public static class GaugeA1Geometry
{
    public static void Verify()
    {
        foreach (string name in GaugeA1Audit.Scenes)
        {
            var p = GaugeA1Audit.Open(name);
            var axis = p.outerDisc.parent;
            var court = axis.parent;
            var log = new List<string>();
            void Check(bool ok, string text)
            {
                log.Add((ok ? "PASS " : "FAIL ") + text);
                File.WriteAllLines(Path.Combine(GaugeA1Audit.Output,name+"-geometry.txt"),log);
                if (!ok) throw new Exception(text);
            }
            var parts = GaugeA1Art.Parts.ToDictionary(part=>part,part=>court.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="A1_"+part));
            foreach (var entry in parts)
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(entry.Value);
                Check(path==GaugeA1Art.Root+"/Prefabs/A1_"+entry.Key+".prefab",entry.Key + " linked " + path);
                var rs = entry.Value.GetComponentsInChildren<MeshRenderer>(true);
                var lod = entry.Value.GetComponent<LODGroup>().GetLODs();
                Check(lod.SelectMany(level=>level.renderers).SequenceEqual(rs) && (rs.Length==3 ||
                      (lod.Length==1 && Mathf.Approximately(lod[0].screenRelativeTransitionHeight,.035f))),
                      entry.Key+" visual LOD transitions "+string.Join(",",lod.Select(x=>x.screenRelativeTransitionHeight)));
                Check(rs.All(r=>r.enabled && r.sharedMaterial && r.sharedMaterial.shader.name=="Universal Render Pipeline/Lit" &&
                     r.sharedMaterial.GetTexture("_BaseMap") && r.sharedMaterial.GetTexture("_BumpMap")),entry.Key + " URP mesh/material references");
            }
            Check(parts["Outer"].parent==p.outerDisc && parts["Inner"].parent==p.innerDisc &&
                  parts["Lid"].parent==p.lid.transform && parts["Bed"].parent==axis,"parenting to original moving/fixed pivots");
            Vector3[] Points(string part, Transform frame)
            {
                var filter=parts[part].GetComponentsInChildren<MeshFilter>(true).First();
                return filter.sharedMesh.vertices.Select(v=>frame.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
            }
            foreach (var handle in new[]{("Outer",p.outerHandle),("Inner",p.innerHandle)})
            {
                var verts=Points(handle.Item1,handle.Item2.parent);
                float top=verts.Max(v=>v.y);
                var crown=verts.Where(v=>v.y>top-.003f).ToArray();
                var center=crown.Aggregate(Vector3.zero,(sum,x)=>sum+x)/crown.Length;
                var delta=center-handle.Item2.localPosition;
                Check(Mathf.Abs(delta.y)<.007f && new Vector2(delta.x,delta.z).magnitude<.004f,
                      handle.Item1+" crown vs locked grip (m) "+delta.ToString("F5"));
            }
            var bed=Points("Bed",axis);
            var pin=bed.Where(v=>v.y>.048f).ToArray();
            var pinHalf=pin.Max(v=>Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.z)));
            var inner=Points("Inner",p.innerDisc);
            var aperture=inner.Min(v=>Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.z)));
            Check(pinHalf>0 && pinHalf<aperture && pin.All(v=>Mathf.Abs(v.x)<=pinHalf+.0001f && Mathf.Abs(v.z)<=pinHalf+.0001f),
                  $"centered square pin half-width {pinHalf:F5} m < square opening {aperture:F5} m");
            var fixedMarks=bed.Where(v=>Mathf.Abs(v.x)>.18f && v.y>.002f && Mathf.Abs(v.z)<.006f).ToArray();
            Check(fixedMarks.Any(v=>v.x<0) && fixedMarks.Any(v=>v.x>0),"fixed index shoulders on both local X rays");
            var track=Points("Outer",p.outerDisc);
            var slot=track.Where(v=>Mathf.Abs(v.x)>.087f && Mathf.Abs(v.x)<.094f && Mathf.Abs(v.z)<.002f).ToArray();
            Check(slot.Any(v=>v.x<0) && slot.Any(v=>v.x>0) && slot.Max(v=>v.y)<.016f,
                  "rotating recessed track coincides with fixed X marks in reference pose");
            float benchTop=Points("Bench",court).Max(v=>v.y);
            float seat=Points("Bed",court).Min(v=>v.y);
            Check(Mathf.Abs(seat-benchTop)<.001f,$"bench top {benchTop:F5} bed underside {seat:F5}");
            float plinth=Points("Plinth",court).Max(v=>v.y);
            float boxBottom=Points("Box",court).Min(v=>v.y);
            Check(Mathf.Abs(plinth-boxBottom)<.001f,$"plinth top {plinth:F5} box underside {boxBottom:F5}");
            Check(!court.Find("静态合并_CourtDesk").GetComponent<MeshRenderer>().enabled &&
                   court.Find("通往双盘的白石嵌线").GetComponent<MeshRenderer>().enabled,"legacy desk hidden and inlaid path rendered independently");
            foreach (var t in new[]{p.outerDisc,p.innerDisc,p.outerHandle,p.innerHandle,p.prizeRest,p.verifier,p.slotEntry,p.leaf.transform,p.lid.transform})
                log.Add($"ANCHOR {GlobalObjectId.GetGlobalObjectIdSlow(t)} {GaugeA1Audit.PathOf(t)} world={t.position:F5} local={t.localPosition:F5}");
            File.WriteAllLines(Path.Combine(GaugeA1Audit.Output,name+"-geometry.txt"),log);
        }
    }
}
