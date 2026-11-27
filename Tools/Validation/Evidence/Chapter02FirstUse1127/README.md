# 二章传送与首次出场

Unity 6000.3.10f1，Windows 桌面独立工程，XR 和渲染关闭。下列时间只反映该机器上的 CPU 与文件操作。头显串流录屏没有打点记录当时的成功传送。这批数据也不含 Quest 原生 Vulkan 首绘。

- 两个原版进程共 36 次普通选点传送，移动输入最慢 0.585 ms，期间未换场或写档。石案阶段触发帧的同步保存分别用了 20.62、10.72 ms。[第一轮](teleport-base.json) · [第二轮](teleport-base2.json)
- 另用 5 个新 Editor Play 进程测正式 Prefab，敌人数据使用其中两轮带有效 NavMesh 的记录。资源引用已取到后，普通残机偶首次创建并激活 91–105 ms，掷火残机 73–148 ms，豪火球手持视觉 3.9–9.8 ms，龙火待吹火苗与喷流均低于 1 ms。首次激活会执行 Awake/OnEnable；禁用状态下提前克隆不会覆盖全部代价。普通命中爆散已经在 Awake 预建 6 份。[逐项耗时](analysis.csv) · [试验条件及汇总](summary.json)
- 优先在 Android Development Build 上对照首次和第二次敌人出现、豪火球施放，以及跨阶段和同阶段传送。若要预热敌人，选择章节确实使用的类型，提前完成实际初始化并隔离 AI、战斗和声音。整章资源不宜在同一帧激活。

[Unity 6.3 目标平台性能采集](https://docs.unity3d.com/6000.3/Documentation/Manual/profiler-profiling-applications.html) · [Unity 6.3 首次使用着色器](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-loading.html)

庭院进入二章是另一条路径。一次分段回放中，异步加载调用用 0.94 ms 返回句柄，首次创建过渡蒙版用了 492.48 ms；在副本提前建好同一蒙版后，进门取用耗时 0.37 ms。两次均走正式进门事件，后者为隔离实验。普通选点和石案自动存档无需这个蒙版。[跨章冷启动](courtyard-cold.json) · [蒙版提前准备](courtyard-prepared.json)
