# 项目目录与配置分类

当前规则（2026-11-07）：`Assets/scripts` 是项目脚本的唯一入口。不再使用 `Assets/SealCombat` 或 `Assets/Script` 外层。场景在 `Assets/Scene`；C# 命名空间和 Unity Inspector 引用沿用原值。

| 脚本目录 | 数量 | 职责 |
|---|---:|---|
| `Assets/scripts/data` | 26 | 数据模型、SO 定义、存档、配置编辑器及教程说明数据。 |
| `Assets/scripts/logic` | 154 | 技能、敌人、移动、交互、事件以及相应构建与验证。 |
| `Assets/scripts/ui` | 15 | 面板、视图、表现、反馈和 UI Inspector。 |

编辑器代码继续位于所属分组的 `Editor` 子目录。两个 Unity 教程脚本也已归入 data/ui。

项目内的 SO 放在 `Assets/ScriptableObjects`。下表按 **实际 C# 类型** 分类，所有类型文件夹以“配置”结尾。为保留同名资源，少数类带有 `默认配置`、`历史配置` 或 `迁移配置/<原场景GUID>配置` 子目录。

| 配置文件夹 | SO 类型 | 数量 |
|---|---|---:|
| [基础手形配置](../../Assets/ScriptableObjects/基础手形配置) | `BasicHandPoseSettings` | 1 |
| [敌人目录配置](../../Assets/ScriptableObjects/敌人目录配置) | `EnemyCatalog` | 1 |
| [敌人战斗配置](../../Assets/ScriptableObjects/敌人战斗配置) | `EnemyCombatDefinition` | 1 |
| [敌人定义配置](../../Assets/ScriptableObjects/敌人定义配置) | `EnemyDefinition` | 1 |
| [投射物效果配置](../../Assets/ScriptableObjects/投射物效果配置) | `ProjectileEffectDefinition` | 3 |
| [玩家定义配置](../../Assets/ScriptableObjects/玩家定义配置) | `RpgPlayerDefinition` | 1 |
| [关卡阶段配置](../../Assets/ScriptableObjects/关卡阶段配置) | `RpgStageDefinition` | 3 |
| [手印识别配置](../../Assets/ScriptableObjects/手印识别配置) | `SealProfile` | 12 |
| [护盾效果配置](../../Assets/ScriptableObjects/护盾效果配置) | `ShieldEffectDefinition` | 4 |
| [技能目录配置](../../Assets/ScriptableObjects/技能目录配置) | `SkillCatalog` | 3 |
| [技能定义配置](../../Assets/ScriptableObjects/技能定义配置) | `SkillDefinition` | 13 |
| [工程说明配置](../../Assets/ScriptableObjects/工程说明配置) | `Readme` | 1 |

44 个 SO 中含 43 个游戏配置和 1 个教程工程说明。Unity 与 SDK 管理的 XR、URP 和 Addressables 设置，以及烘焙的 NavMesh 数据，保留各自目录。新增项目配置应使用对应的创建工具；配置整理与验证共用 `ConfigurationAssetOrganizer.FolderName` 的类型命名映射。

[当前 GUID 映射](../research/20261107-recovery/layout-map.json) · [恢复结果和逐文件源码核对](../research/DISK_RECOVERY_RESULT_20261107_ZH.md)。
