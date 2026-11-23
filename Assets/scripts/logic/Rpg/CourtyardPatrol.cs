using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class CourtyardPatrol : MonoBehaviour, IResourceCatalog
    {
        public AssetReferenceGameObject visual;
        public AnimationClip walk;
        public Transform end;
        [Min(.05f)] public float speed = .65f;
#if UNITY_EDITOR
        [SerializeField, HideInInspector] GameObject editorPreview;
#endif
        Vector3 start;
        bool outbound;
        PlayableGraph graph;
        GameObject instance;
        AssetLease<GameObject> visualLease;
        CancellationTokenSource lifetime;

        bool IResourceCatalog.TryResolve(Type type, string id, out string address)
        {
            AssetReference reference = type == typeof(GameObject) && id == "visual" ? visual : null;
            address = reference != null && reference.RuntimeKeyIsValid() ? reference.RuntimeKey.ToString() : null;
            return address != null;
        }

        async void OnEnable()
        {
            start = transform.position;
            outbound = true;
#if UNITY_EDITOR
            if (editorPreview) editorPreview.SetActive(false);
#endif
            if (!end || !walk) return;
            lifetime = new CancellationTokenSource();
            var token = lifetime.Token;
            double startedAt = Time.timeAsDouble;
            AssetLease<GameObject> acquired = null;
            try
            {
                acquired = await SharedAssetLoader.AcquireAsync<GameObject>(this, "visual", token);
                token.ThrowIfCancellationRequested();
                instance = Instantiate(acquired.Asset, transform, false);
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var figure = instance.GetComponentInChildren<Animator>(true);
                if (!figure) throw new InvalidOperationException("巡行模型缺少 Animator");
                figure.applyRootMotion = false;
                instance.SetActive(true);
                graph = PlayableGraph.Create("Courtyard walk");
                var animation = AnimationClipPlayable.Create(graph, walk);
                animation.SetTime(Time.timeAsDouble - startedAt);
                AnimationPlayableOutput.Create(graph, "Walk", figure).SetSourcePlayable(animation);
                graph.Play();
                visualLease = acquired; acquired = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (this && !token.IsCancellationRequested)
                {
                    ReleaseVisual();
                    Debug.LogError("侧廊工役加载失败：" + error, this);
                }
            }
            finally
            {
                acquired?.Dispose();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("从巡行外观同步编辑预览")]
        void SyncEditorPreview()
        {
            if (Application.isPlaying || visual?.editorAsset is not GameObject model) return;
            if (editorPreview && (editorPreview.transform.parent != transform || !editorPreview.CompareTag("EditorOnly")))
            {
                Debug.LogError("巡行预览引用必须属于本宿主且标记 EditorOnly", this);
                return;
            }
            if (editorPreview && UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(editorPreview) == model) return;
            if (editorPreview) UnityEditor.Undo.DestroyObjectImmediate(editorPreview);
            var preview = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model, transform);
            UnityEditor.Undo.RegisterCreatedObjectUndo(preview, "同步巡行预览");
            UnityEditor.Undo.RecordObject(this, "同步巡行预览");
            preview.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.tag = "EditorOnly";
            editorPreview = preview;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        void Update()
        {
            if (!end) return;
            var destination = outbound ? end.position : start;
            var delta = destination - transform.position;
            if (delta.sqrMagnitude < .0004f) { outbound = !outbound; return; }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 150f * Time.deltaTime);
            if (Vector3.Dot(transform.forward, delta.normalized) > .9f)
                transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        }

        void ReleaseVisual()
        {
            if (graph.IsValid()) graph.Destroy();
            if (instance) { instance.SetActive(false); Destroy(instance); instance = null; }
            visualLease?.Dispose(); visualLease = null;
        }

        void OnDisable()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            ReleaseVisual();
            transform.position = start;
        }
    }
}
