using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed class UnityRpgSaveJson : IRpgSaveJson
    {
        public static readonly UnityRpgSaveJson Instance = new();
        UnityRpgSaveJson() { }
        public string Serialize<T>(T value) => JsonUtility.ToJson(value, true);
        public T Deserialize<T>(string json) => JsonUtility.FromJson<T>(json);
    }
}
