# 二章石案主断桥 · 通路符

正式场景为 [Chapter02_SuspendedPath.unity](../../../Assets/Scene/Chapter02_SuspendedPath.unity)，二章石案的青铜压面已接到章节玩家的原按钮。石案与两句旁白的回放录于场景 `3ed6f9c6fb768192a8b78fab125c3d78cd750dfef638e09a3c67f8e69782cd85`。后续在主编辑器将画板的序列化范本也指向通路符，浮光经两处岸口中心引导过桥，正式重开 SHA-256 为 `ce338bb531b220345ad43673dc4e5aad8bf655ed274f7d72adec445c3732cc83`。[正式场景增量与重开](../Evidence/Chapter02Assembly/bronze-gold-1126/passage/formal-readback.txt)。石案教学通过场景原来引用的 [CH02_BRIDGE_DRAW 阶段资产](../../../Assets/ScriptableObjects/Chapter02Workshop/CH02_BRIDGE_DRAW.asset)更新。它直接引用通路符模板和铜榫 Prefab，配置两处接岸点。进入玩家场景时，阶段将同一模板交给石案面板。按键和画板沿用已有对象，章节照常写原检查点。

## 试玩

1. 从新局走到二章第一个断口的石案。若直接在 Unity 中开场，在章节玩家的 LinearRpgController 取消勾选「直接开场时读取默认存档」即可从入口体验。已过桥的存档会恢复完整的淡金符桥。
2. 按石案上的实体青铜按钮打开画板。从腰间取墨规笔，在范本上先画门框，再画框内横线。认符后右手出现金光，可以归笔选岸。
3. 笔收回腰间后，右食指依次指向近岸、对岸的铜榫。出现青白轮廓后握拳。有预览时放低右手可取消，石案仍然可用。
4. 等淡金桥面铺到对岸，沿桥中间向前走，即可到祭坛。在本场景两岸相距 4.32 米，桥宽 1.10 米，形成时间 2.4 秒。

已撤下试玩用的两句合成旁白及其临时台词。首桥现可全程静音操作，正式配音沿用项目原旁白系统，文本由作者制作。

## 现场记录

Unity 6000.3.10f1，Windows D3D11，在隔离工程加载同一份正式场景及项目代码。使用模拟的逐帧手部关节，走正常移动、指尖按压、取笔、笔尖识别、归笔、食指射线和握拳入口。已回读青铜压面、两岸去旧地线后的网格、金色浮光引导组件，保留场景原有的手工改动。

- 旧版录像中按按钮打开通路符范本，曾播放当时的两段临时配音。画完符时仍在石案阶段，近岸防坠阻挡有效。成功取消并重新取笔画符后再次选中两岸。
- 73 张增长采样中，桥面碰撞均关闭且防坠阻挡有效。第 269 张输出帧（从 0 起）进度为 50%。到岸后中点射线击中符桥自己的行走碰撞体，角色经原探索移动走到对岸。原宽石桥的网格和碰撞已关闭。关卡推进到祭坛。
- 随后重新加载正式场景和上一段写下的二章存档，玩家恢复在祭坛阶段，断口有且仅有一座完成的符桥，石案教学不会重开。另起独立 Unity 进程，保持场景序列化的默认读档选择，冷启动同样恢复祭坛与唯一可走符桥。[新进程读数](cold.txt)
- 本次二章新档同时读到四份独立瞄准资产。按项目现有技能权限，雷光剑、龙火之术、飞炎术可用，火球术需已有解锁。Quest 手势和双目观感待上机。
- [逐项检查](checks.txt) · [逐帧位置与碰撞](frames.csv) · [主断桥静音画面回放，419 帧](../../../docs/visuals/20261126_ch02_passage/石案主断桥_通路符过桥.mp4) · [桥面延伸一半时的原始画面](../../../docs/visuals/20261126_ch02_passage/石案主断桥_成路一半.jpg)

原回放脚本的日志标签 `square` 代表石案镜头。其原有第一行说明源自旧支路夹具，当前断言实际验证出生时岸标可见、笔未开放、实体桥未接通。首次 Unity 回放出现编辑器搜索索引的启动异常，游戏输入、成桥、跨岸和读档的断言均执行至 PASS END。

## 重放

准备映射到独立存档目录的项目副本，将该副本 Player Settings 的 Company Name 设为 `ChapterPassageFormalQA`，复制正式场景与最新 Assets。在仓库根运行 `python Tools/Validation/Chapter02MainPassage/prepare.py <副本目录>`，它复用既有的双手追踪录像夹具。使用 Unity 的 `-batchmode -force-d3d11 -projectPath <副本目录> -executeMethod Chapter02MainPassageReplay.Run`，结果在副本 `Logs/MainPassage-日期时间`。命令自行退出。新进程冷启动复查使用相同副本和刚写入的存档，把 [Chapter02PassageCold.cs](Chapter02PassageCold.cs) 放入副本的 `Assets/scripts/logic/Editor/`，再执行 `-executeMethod Chapter02PassageCold.Run`。
