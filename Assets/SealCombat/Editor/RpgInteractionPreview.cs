#if UNITY_EDITOR
using System.IO;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class RpgInteractionPreview
    {
        public static void Capture(LinearRpgController controller, string path)
        {
            var movement = controller.locomotion;
            Vector3 origin = movement.PlayerFloorPosition;
            Vector3 target = movement.CandidateNode ? movement.CandidateNode.floorPosition : origin + Vector3.forward;
            Vector3 center = (origin + target) * .5f;
            var go = new GameObject("四向路径_验收相机");
            var camera = go.AddComponent<Camera>();
            var texture = RenderTexture.GetTemporary(1200,800,24); var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.transform.position = center + new Vector3(1,2.7f,-2.4f); camera.transform.LookAt(center);
                camera.fieldOfView = 55; camera.nearClipPlane=.05f; camera.targetTexture=texture;
                camera.Render(); RenderTexture.active=texture;
                pixels = new Texture2D(1200,800,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1200,800),0,0); pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(texture); if(pixels)Object.Destroy(pixels);Object.Destroy(go); }
        }
    }
}
#endif
