# 线性 RPG 切片验收记录

日期：2026-10-30。Unity 6000.3.10f1，Windows编辑器；当前平台配置为Android，测试运行于编辑器Play。没有在本轮打包或宣称Quest/PICO真机通过。

## 交付

- 新场景：`Assets/SealCombat/Scenes/LinearRpgTest.unity`，复用原庭院美术，独立技能/效果/玩家/三阶段配置。
- HP/MP/恢复等待/CD、盾后伤害、死亡门控、固定奖励、一次性隐藏盾购买、火球升级与版本化检查点。
- 左食指导航、回中重新准备、导航期间右手张掌闪避；真实HMD局部姿态不被修改，闪避可离开节点。
- 右手持球前推拥有发射优先权；未就绪或同一次动作发射完毕后都不会顺带前闪。
- 双手可靠低位取消、显式取消和取消口令清理待施法/未发射物，保留护盾/飞行物和已付MP/CD。
- 中文HUD、中文Inspector、电脑调试按钮、独立KWS词表与[使用说明](LINEAR_RPG_QUICKSTART_ZH.md)。

## 结果与证据

| 验收 | 结果 | 原始记录 |
|---|---:|---|
| 玩家数值和文件存档 | 15组通过 | [编辑模式报告](../Logs/linear-rpg/build-check.json) |
| 手势状态、碰撞、检查点和闪避时钟 | 28组通过 | 同上 |
| 原有技能核心匹配回归 | 47组通过 | 同上 |
| 原有效果和效果配置 | 20组通过 | [真实Play报告](../Logs/linear-rpg/play-check.json) |
| RPG真实Play联动 | 20组通过 | 同上，逐项说明含测试存档路径 |
| 中文语音配置 | 4个口令及端口校验通过 | `skill_voice.py --config Assets/SealCombat/Configuration/LinearRpg/skill-voice-config.json --check-config` |
| UI视觉检查 | 中文完整可读，未见截断 | [世界HUD预览](../Logs/linear-rpg/hud-preview.png)、[庭院预览](../Logs/linear-rpg/scene-preview.png) |

上述共130组自动检查。编辑模式的物理用例使用独立临时场景；Play检查使用唯一`linear-rpg-probe-<GUID>.json`，不覆盖正常玩家档案。

Play中的手姿由明确的测试入口注入，发射通过测试调用，目标击破使用作弊按钮；旧/新取消口令使用显式时间戳。训练红圈的预警和25点伤害由组件真实逐帧执行。测试区分模拟输入与运行接线，不能拿这些结果推算手势识别率。

## 修正与设计边界

- 编辑模式临时节点需显式模拟注册/注销；跨场景拒绝测试用PreviewScene，避免未保存临时场景相互阻挡测试。
- 构建器在切换场景后加载配置，避免切换前有效的Unity资产引用在切换后失效。已对本次未完成场景保留副本后修复接线。
- 存档先生成候选，写入成功才提交检查点；不能落地时不提交下一阶段奖励/检查点。
- 伤害先去重再扣盾；取消口令仍校验时间与输入屏障；导航、语音和最新骨骼在同一LateUpdate顺序中仲裁。
- 主控停用会结束技能/效果子系统；升级修改运行态克隆，不写回共享SO。
- 当前回调只保证内置单效果的资源记账，不是通用多效果事务。存档为单写入者；Android文件系统行为尚未验证。

## 仍需用户真机验证

节点导航和张掌闪避阈值、自然动作误触发、低位腕部视野、遮挡和恢复、头部位移舒适性，以及真实麦克风下“金刚护体”等口令的误报/漏检。购买、升级和死亡恢复目前通过电脑调试按钮操作，头显交互菜单尚未制作。

画符、多印序列、属性克制、硬直、正式敌人AI、完整剧情/商店、PICO实测、MR与多人未作为本次已完成功能。

## 备份

施工前作者文件快照：`D:\UnityProjects\NarutoVR_Backups\before-linear-rpg-20261030-182310`，包括Assets、Packages、ProjectSettings、docs和README，不包含可重新生成的Library缓存。其位置另记于`Logs/linear-rpg/backup-path.txt`。

本轮已有文件变更清单见`Logs/linear-rpg/changed-existing.json`；新文件另外包含RPG/Locomotion代码、编辑器检查、新场景、独立配置与文档。原场景/SDK版本不因新RPG接线而升级或迁移。
