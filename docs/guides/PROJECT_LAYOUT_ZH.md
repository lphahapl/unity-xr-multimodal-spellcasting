# 项目目录与配置分类

目录规则（脚本与 SO 数量为 2026-11-07 的恢复快照）：`Assets/scripts` 是项目脚本的唯一入口。不再使用 `Assets/SealCombat` 或 `Assets/Script` 外层。场景在 `Assets/Scene`；C# 命名空间和 Unity Inspector 引用沿用原值。

| 脚本目录 | 数量 | 职责 |
|---|---:|---|
| `Assets/scripts/data` | 26 | 数据模型、SO 定义、存档、配置编辑器及教程说明数据。 |
| `Assets/scripts/logic` | 154 | 技能、敌人、移动、交互、事件以及相应构建与验证。 |
| `Assets/scripts/ui` | 15 | 面板、视图、表现、反馈和 UI Inspector。 |

本轮将纯玩家/章节及状态规则放入 `Assets/scripts/Core`，将 v2 DTO、映射、读写与重试放入 `Assets/scripts/Persistence`。Core 和 Persistence 均可独立于 Unity 引擎编译。运行时代码由 `NarutoVR.Game.Runtime` 引用这两个程序集。三个 Editor 目录共同编译成 `NarutoVR.Game.Editor`，仅随编辑器运行。

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

## 新建模型的源文件与 Unity 交付

新模型先制作高模，再制作保留轮廓、可用于实时渲染的低模。Blender 工程保留两者及可重烘的对应关系，放在 `ArtSource`；混元生成的高模也进入这一步。给低模展开 UV、固定导出三角化，从高模烘焙底色、切线空间法线与材质需要的遮蔽或金属度等通道。在 `Assets/Art` 交付低模 FBX、烘焙贴图和 Unity 材质。多处重复出现的器物使用同一网格与材质。用回读的尺寸、三角数、UV、Unity 法线导入设置和实机画面验证交付。[室内规靶与壁灯样例](../architecture/开场教学与首枚铜叶/08_试炼室制作与交互.md)


## 导入素材与正式特效（2026-11-08）

`Assets/Imported` 存放第三方待选素材：`EpicToonFX`、`MasterMagicFX` 和 `BattleSoundLibrary`。仅保留 Epic Toon FX 1.71，避免旧版本重复 GUID；应用随包 URP 材质补丁。音效库仅导入 WAV，不引入演示工具。源包仍保留在 `D:/BaiduNetdiskDownload`。导入库不代表已选用，不自动替换正式技能和场景。

`Assets/Art/VFX` 汇集当前工程已使用的特效：

| 目录 | 内容 |
|---|---|
| `Skills/Prefabs` | 火球、护盾、命中爆散、地面阵法 |
| `Skills/Materials`、`Skills/Meshes` | 上述效果的材质和烘焙网格 |
| `ThunderSword` | 雷光剑、雷光波、落雷及配套材质和网格 |
| `Shaders` | 技能能量、护盾短闪、受击边缘和腕匣刻度着色器 |
| `Resources` | 护盾短闪、受击边缘材质；保留 Resources.Load 的原资源名 |

移动资源保留原 `.meta` 和 GUID；技能生成工具输出位置同步到新目录。项目运行逻辑脚本继续放在 `Assets/scripts`，第三方随包依赖脚本保留在导入库内部。正式效果的场景、SO 引用按 GUID 维持；新导入效果的实际视觉、风格和头显性能需挑选后验收。


## 技能音频与测试清理（2026-11-08）

新配置位于 `Assets/ScriptableObjects/技能音频配置`（`SkillAudioProfile`）；音频数据类在 `Assets/scripts/data/Skills/Audio`，播放器在 `Assets/scripts/logic/Skills/Audio`，雷光剑正式 WAV 在 `Assets/Audio/ThunderSword`。

移除 `SampleScene`、`EmptyStreamProbe`、`SkyboxPreview`、已生成的 `SnapToolTest` 四个场景及对应 meta。天空改用独立编辑器预览窗口；吸附验证会按需重建空场景。移除七个一次性脚本：`DiskRecoveryChecks`、`ProjectLayoutOnce`、`YicunShanheV9AudioProbe`、`EmptyStreamProbe`、`StreamHandLines`、`StreamCpuProfile`、`DebugTextComparison`，并清除其调度入口。逐项原因和可恢复备份在 `Logs/skill-audio-refactor-cleanup-20261108/cleanup-plan.json` 与 `removed-backup/`。

保留首章、P1、SealLab 以及仍被生成器、回归检查和技能迁移工具使用的训练场景。`SkillInputController`/`SkillTestView` 是实际运行组件；`LinearRpgTest` 是当前回归夹具。`BgmLoopVerificationEmpty` 仍被当前 v11 音乐验收使用，不能作为闲置场景删除。
