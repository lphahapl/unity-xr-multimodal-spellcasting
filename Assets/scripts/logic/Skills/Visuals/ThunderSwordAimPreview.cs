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
        readonly Vector3[] rayPoints = new Vector3[5];
        readonly Vector3[] arcPoints = new Vector3[17];
        Color currentColor;
        GameObject root;
        Material material;
        LineRenderer ray, arc;

        public bool IsAvailable => root;

        public ThunderSwordAimPreview(Scene scene)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (!shader) return;
            root=new GameObject("雷光剑 · 发波预览"); SceneManager.MoveGameObjectToScene(root,scene);
            material=new Material(shader){name="雷光剑 · 方向提示材质"};
            ray = Line("发波方向", .005f, rayPoints.Length);
            arc = Line("剑波横竖预览", .008f, arcPoints.Length);
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
            if (!root.activeSelf) root.SetActive(true);
            var color=aim.blocked?new Color(1,.32f,.08f):ready?new Color(.14f,.85f,1):new Color(.35f,.48f,.55f);
            if (currentColor != color)
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                currentColor = color;
            }
            Vector3 right=orientation*Vector3.right, up=orientation*Vector3.up;
            float distance=Vector3.Distance(aim.origin,aim.endpoint);
            float length=Mathf.Min(.55f,distance);
            Vector3 tip=aim.origin+aim.direction*length;
            rayPoints[0] = aim.origin;
            rayPoints[1] = rayPoints[3] = tip;
            rayPoints[2] = tip - aim.direction * .06f + right * .025f;
            rayPoints[4] = tip - aim.direction * .06f - right * .025f;
            ray.SetPositions(rayPoints);
            // Outline at the actual aim endpoint; apparent size stays legible at distance.
            float size=Mathf.Clamp(distance*.035f,.08f,.45f);
            // The arc's local +Y points along flight after the fixed model-axis correction.
            // Keep the whole preview in front of the hit surface, with its opening toward us.
            Vector3 center=aim.endpoint-aim.direction*(size*.3f+.015f);
            for (int i = 0; i < arcPoints.Length; i++)
            {
                float t = i / (float)(arcPoints.Length - 1) * 2 - 1;
                arcPoints[i] = center + right * t * size + up * ((1 - t * t) * size * .3f);
            }
            arc.SetPositions(arcPoints);
        }
        public void Hide()
        {
            if (root && root.activeSelf) root.SetActive(false);
        }
        public void Dispose()
        {
            Hide();
            if(root){if(Application.isPlaying)Object.Destroy(root);else Object.DestroyImmediate(root);root=null;}
            if(material){if(Application.isPlaying)Object.Destroy(material);else Object.DestroyImmediate(material);material=null;}
        }
    }
}
