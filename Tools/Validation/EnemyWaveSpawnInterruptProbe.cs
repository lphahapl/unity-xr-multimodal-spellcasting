using System;
using UnityEngine;

/// <summary>在池中已知小怪的下一次激活时投递命中，覆盖召唤已暂存前一只的同步重入。</summary>
public sealed class EnemyWaveSpawnInterruptProbe : MonoBehaviour
{
    public Action NextEnable;

    void OnEnable()
    {
        var callback = NextEnable;
        NextEnable = null;
        callback?.Invoke();
    }
}
