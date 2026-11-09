using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    /// <summary>Lightweight shared-aim arrow and two-state wave silhouette. Never owns collision or input.</summary>
    internal sealed class ThunderSwordAimPreview : IDisposable
    {
        GameObject root;
        Material material;
        LineRenderer ray, arc;

        public ThunderSwordAimPreview(Scene scene, bool enabled)
        {
            if (!enabled) return;
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (!shader) return;
            root=new GameObject("雷光剑 · 发波预览"); SceneManager.MoveGameObjectToScene(root,scene);
            material=new Material(shader){name="雷光剑 · 方向提示材质"};
            ray=Line("发波方向",.005f,5);arc=Line("剑波横竖预览",.008f,17);
            Hide();
        }
        LineRenderer Line(string name,float width,int points)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.positionCount=points;line.widthMultiplier=width;line.numCapVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return line;
        }
        public void Show(FireballAim aim,Quaternion orientation,bool ready)
        {
            if(!root) return;
            root.SetActive(true);
            var color=aim.blocked?new Color(1,.32f,.08f):ready?new Color(.14f,.85f,1):new Color(.35f,.48f,.55f);
            if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color")) material.SetColor("_Color",color);
            Vector3 right=orientation*Vector3.right, up=orientation*Vector3.up;
            float distance=Vector3.Distance(aim.origin,aim.endpoint);
            float length=Mathf.Min(.55f,distance);
            Vector3 tip=aim.origin+aim.direction*length;
            ray.SetPosition(0,aim.origin);ray.SetPosition(1,tip);
            ray.SetPosition(2,tip-aim.direction*.06f+right*.025f);ray.SetPosition(3,tip);
            ray.SetPosition(4,tip-aim.direction*.06f-right*.025f);
            // Outline at the actual aim endpoint; apparent size stays legible at distance.
            float size=Mathf.Clamp(distance*.035f,.08f,.45f);
            // The arc's local +Y points along flight after the fixed model-axis correction.
            // Keep the whole preview in front of the hit surface, with its opening toward us.
            Vector3 center=aim.endpoint-aim.direction*(size*.3f+.015f);
            for(int i=0;i<arc.positionCount;i++)
            {
                float t=i/(float)(arc.positionCount-1)*2-1;
                arc.SetPosition(i,center+right*t*size+up*((1-t*t)*size*.3f));
            }
        }
        public void Hide(){if(root)root.SetActive(false);}
        public void Dispose()
        {
            Hide();
            if(root){if(Application.isPlaying)Object.Destroy(root);else Object.DestroyImmediate(root);root=null;}
            if(material){if(Application.isPlaying)Object.Destroy(material);else Object.DestroyImmediate(material);material=null;}
        }
    }
}
