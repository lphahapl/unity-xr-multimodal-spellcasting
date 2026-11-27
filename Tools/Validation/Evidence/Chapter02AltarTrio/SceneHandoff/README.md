# ②祭坛三敌接线

场景负责人将 [安装器](Chapter02AltarTrioInstaller.cs) 临时放到 Unity 工程的 `Assets/Editor`，打开 `Assets/Scene/Chapter02_SuspendedPath.unity`，执行菜单 **Tools → NarutoVR → 第二章断衡悬道 → 补齐祭坛三敌**。审阅场景差异后保存。批处理可在编辑模式调用 `public static bool Chapter02AltarTrioInstaller.Apply(Scene scene)`。

原祭坛房间已经用 `stone-carrier` 绑定背石起始位。该工具在同一房间补齐普通近战 `canjiou` 和远程掷火 `zhihuocanji`，沿用原敌人目录、地面导航和接敌触发盒。新增点会朝向接敌线中心。三者均使用普通场景敌人登记。场景作者已安排的合法点位、名字、顺序和朝向均保留。

| 角色 | e7aeb66b 场景副本中的出生位置，世界米 | 世界 Y 轴朝向 |
| --- | --- | ---: |
| 背石工奴，原位 | (−9.1214, 80.0000, 35.0000) | 90° |
| 普通近战，候选试摆 | (−12.1214, 80.0833, 38.0000) | 45° |
| 掷火残机，候选试摆 | (−5.1214, 80.0833, 33.0000) | 333.435° |

两处试摆选在原接敌线坐标系下的 (3, 0, −3) 与 (8, 0, 4)。安装前会采样地面，并检查从工奴位置到两点都存在完整路径。工坊 F1/F2 继续使用各自原先的房间配置。

| 执行结果 | 行为 |
| --- | --- |
| 返回 `true` | 本次补齐了缺少的登记；整次操作可撤销。 |
| 返回 `false` | 原工奴与两个战斗敌人均已正确登记，现有编成和场景内容保持原值。 |
| 抛出 `InvalidOperationException` | 发现跨区锚点、重复角色、错误召唤标志、原工奴丢失或地面不可达等冲突；先检查已有房间引用。 |

## 编辑态复验

Unity 6000.3.10f1 已分别对 e7aeb66b、a5842a97 两个正式场景快照的独立副本各运行 13 项检查。一次 Undo 将房间还原成单工奴，Redo 恢复两名敌人的点位和朝向。保存重开后再次运行返回 `false`，场景保持干净，磁盘字节相同。探针另外调换了登记顺序、重命名、自定义点位和朝向，并试了重复 ID、误设召唤、跨房间引用与部分预装。工奴的篮、巡逻点和祭坛外各房间的敌人、范围与服务引用均已核对。[首轮结果](edit-results.txt) · [第二次按正式场景更新复验](edit-results-a5842a97.txt)

将[编辑态探针](Chapter02AltarTrioEditChecks.cs)和安装器放到同一独立工程的 `Assets/Editor`。输入使用尚为单工奴的场景快照，探针会在副本保存一次并重开，随后用内存中的编辑验证幂等、手配布局和冲突保护。运行参数如下。

```text
-batchmode -nographics -projectPath <独立工程> -executeMethod Chapter02AltarTrioEditChecks.Run -altarDisposable <同一独立工程> -altarResults <结果文件>
```
