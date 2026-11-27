# 二章干粮正式发布

2026-11-26 04:07（北京时间），在 D 盘主 Unity 编辑器写入二章并重新打开。正式场景 SHA-256 为 `5fde465963731f04617d87206389f89f14391f10b2455e8aaf5fbd00b1e94e0f`。写入前后场景均为已保存状态，原场景 `.meta` 的 SHA-256 保持一致。[正式场景](../../../../../../../Assets/Scene/Chapter02_SuspendedPath.unity) · [写前检查](main-inspect.txt) · [正式回读](main-publish.txt)

玩家开局的中央软袋为空。关卡确定领取时机后，可从 Inspector 的事件绑定 `ChapterRationSupply.GrantFromReward()`，或从脚本调用 `TryGrant()` 并读取是否成功。重复领取和中央口被占用时均保留现有物品。正式回读确认 `SoftPouch` 属于原有五口工具带，嘴区挂在 `CenterEyeAnchor` 下，官方左右手仍指向各自的骨骼输入。

| 接收方独立复验 | 结果 |
|---|---|
| 干粮新档 | 36 项通过。左右手可交换拿袋和取饼；四块饼各恢复 15 气血，保持捏合不会重复结算。最后吸入口中时丢失追踪可取消。持笔拿袋、持袋拿笔都保持原物占用。[测试记录](receiver-ration.txt) |
| 干粮冷读 | 4 项通过。独立 Unity 进程读回保存时的两块饼、气血和中央槽位。无干粮字段的旧档与新档默认空袋口。[测试记录](receiver-ration.txt) |
| 既有断桥与实笔 | 27 项通过。原笔握取和入套、解锁绘符、断桥碰撞修复、阶段和读档均通过。另核对了官方手模与同侧绑定。[断桥回归](receiver-bridge.txt) · [官方双手](official-hands.txt) |

接收方以交付清单逐一比对全部 15 项源码和元数据。正式基线共 806 个序列化对象，804 个保持原样，仅改动玩家的组件列表与头显预制实例的子对象记录，新增 9 段、删除 0 段。发布前版本为 `cf4b60b2`。三次 Unity 进程均返回 0，手部观测使用桌面模拟；Quest 实际嘴区高度、捏取距离和遮挡手感待头显验证。[接收清单和差异](receipt.json) · [来源清单](../manifest.json) · [发布前场景](../../../combined-1126/Chapter02_SuspendedPath.candidate.unity)
