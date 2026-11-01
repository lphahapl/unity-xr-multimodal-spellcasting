# NeuGameMRTools 初步源码核对

核对日期：2026-10-28。来源：[用户指定仓库](https://cnb.cool/NeuGameMR/NeuGameMRTools)。

本地临时克隆：`Temp/ToolkitReview/NeuGameMRTools`；分支 `master`；提交 `6e5c58c905ec0ed1931b31c30eb517ddec9e1572`。使用单分支浅克隆，未导入 Assets 或 Packages，未执行工具集脚本，未验证运行效果。Temp 可能被 Unity 清理，不适合作为长期存档。

## 对结印项目的用途

| 模块 | 源码依据（相对克隆根目录） | 可用于本项目的场景（评估） |
| --- | --- | --- |
| 近距离抓取 | `Runtime/Components/GrabInteractionController.cs`、`Editor/Windows/GrabWizard.cs` | 苦无、卷轴等道具的抓取配置 |
| 远距离抓取 | `Runtime/Components/DistanceGrabInteractionController.cs`、`Editor/Windows/DistanceGrabWizard.cs` | 远程取回道具，仍需验证生成配置 |
| 吸附插槽 | `Runtime/Components/SnapZone.cs` | 道具归位、训练台插槽；本质是物体触发与位置/旋转插值 |
| 分组显示 | `Runtime/Components/SnapZoneDisplayManager.cs` | 按组呈现训练道具；当前代码围绕插槽完成事件运行 |
| 计数器 | `Runtime/Components/Counter.cs` | 统计训练完成次数；不检查印的身份、顺序或时限，不能替代结印连招状态机 |
| 中文编辑器 | `Editor/Editors`、`Editor/Windows` | 可借鉴中文分组、组件检查、配置向导及 Undo 操作 |

已枚举并读取相关源码；当前克隆没有骨骼快照录制、FingerShapes 特征提取、加权印模板或手势分类训练模块。它对道具交互有帮助，对当前 SealLab 识别核心没有直接替代作用。

## 兼容性发现

1. `Runtime/NeuGameMRTools.Runtime.asmdef` 引用了 `Oculus.Interaction.Grab` 和 `Oculus.Interaction.HandGrab`。当前项目 Library/PackageCache 中不存在这两个名称的程序集；它们作为 C# 命名空间存在不能证明同名程序集存在。直接导入前需修正引用并实际编译。
2. README 安装示例使用 SDK 85 和 `com.meta.xr.sdk.ovr`，而本项目 manifest 是 SDK 207.0.1，OVR 适配包为 `com.meta.xr.sdk.interaction.ovr`。文档另有 v68+ 描述，不能据此认定已验证 SDK 207；不应照抄 manifest 或降级现有环境。
3. 根目录没有 `package.json`，当前结构不能作为完整 UPM 包直接 Add package from Git URL。可先做独立适配副本，再按目录导入或补成包。
4. `GrabWizard.Create()` 同时创建手部与控制器抓取组件；设备选择变量没有用于该创建分支。远抓向导的模式选择主要用于对象命名和全息网格分支，未见三种模式完整行为配置。向导生成结果需要逐项检查，不能只按 README 宣称接受。
5. `README.md` 许可证段落声明 MIT，根目录未见独立 LICENSE 文件；正式分发时需保留并核对作者提供的完整许可信息。

## 建议与验证范围

先继续完成真实结印采集与混淆验证。开发道具交互时，再按需适配抓取和插槽模块。此次仅完成 Git 拉取、工作区干净检查、README/源码/程序集引用静态核对，没有导入、编译或 Quest 真机测试。

外部 Antigravity/Gemini 3.8 Flash High 子任务 `e41b454a-a4ef-4940-959c-c8f8f16fe6d4` 超时，没有可验收交付；已确认进程停止并释放范围。父代理通过 Git 和本地源码读取完成本次核对，未切换其他外部代理。
