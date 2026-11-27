using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // An invisible player barrier at an open chasm must not occlude the opposite shore.
    [DisallowMultipleComponent]
    public sealed class PassageSightWindow : MonoBehaviour { }
}
