using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NarutoVR.SealCombat.Rpg
{
    // The warden in the closed service gallery follows a short rail independently of combat.
    [DisallowMultipleComponent]
    public sealed class CourtyardPatrol : MonoBehaviour
    {
        public Animator figure;
        public AnimationClip walk;
        public Transform end;
        [Min(.05f)] public float speed = .65f;
        Vector3 start;
        PlayableGraph graph;
        bool outbound;

        void OnEnable()
        {
            start = transform.position;
            outbound = true;
            if (!end || !figure || !walk) return;
            graph = PlayableGraph.Create("Courtyard walk");
            var animation = AnimationClipPlayable.Create(graph, walk);
            AnimationPlayableOutput.Create(graph, "Walk", figure).SetSourcePlayable(animation);
            graph.Play();
        }

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

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            transform.position = start;
        }
    }
}
