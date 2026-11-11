using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public enum PlayerMovementKind { Walk, Dodge, Navigation, Checkpoint }

    public readonly struct MovementResult
    {
        public readonly PlayerMovementKind Kind;
        public readonly Vector3 Delta, Feet, Right;
        public MovementResult(PlayerMovementKind kind, Vector3 delta, Vector3 feet, Vector3 right)
        {
            Kind = kind;
            Delta = delta;
            Feet = feet;
            Right = right;
        }
    }

    // Movement occurs every frame. Cache the invocation list when a listener changes.
    internal sealed class MovementEvent<T>
    {
        Action<T> listeners;
        Delegate[] snapshot = Array.Empty<Delegate>();
        internal void Add(Action<T> listener)
        {
            listeners += listener;
            snapshot = listeners?.GetInvocationList() ?? Array.Empty<Delegate>();
        }
        internal void Remove(Action<T> listener)
        {
            listeners -= listener;
            snapshot = listeners?.GetInvocationList() ?? Array.Empty<Delegate>();
        }
        internal void Publish(T result, UnityEngine.Object context)
        {
            foreach (Action<T> listener in snapshot)
                try { listener(result); }
                catch (Exception error) { Debug.LogWarning("移动监听失败：" + error.Message, context); }
        }
    }
}
