# 墨规笔朱砂毛笔验收

2026-11-26 在 Unity 6000.3.10f1 隔离工程运行。桌面画面使用正式画板和材质渲染，笔尖由合成轨迹驱动。对照板从上到下是慢直线、快直线、减速折笔后快速扫出。

- [整板对照](GlyphBrush-Board.png) · [笔锋与飞白细节](GlyphBrush-Detail.png)
- [11.8 秒 Unity 桌面录屏](GlyphBrush-Desktop.mp4) · [录制参数与校验](GlyphBrush-Desktop.capture.json)。画面依次演示接近落笔、慢笔、快笔、减速折笔与快速收锋，结尾为笔迹近景。Unity Play Mode 逐帧录制，由合成笔尖轨迹驱动。
- GlyphLab 正常剧情入口的[书写](GlyphLab-Story.png)与[金线确认](GlyphLab-Gold.png)
- [实测输出](Checks.txt) · [新版 UI 报告](UI-Report.json) · [输入散列](Input-SHA256.json)
- [GitHub 源码调研](../../../../docs/research/VR_GLYPH_BRUSH_GITHUB_20261126_ZH.md)

在同样路径上，用网格实际量得慢笔 4.88 毫米、快笔 2.66 毫米，笔墨通道的值分别为 235 和 120。减速折笔在接缝处量得 7.29 毫米。回放同时检查急收与快速扫出的不同收锋，覆盖重描、交叉、撤销和清空。实际 GlyphLab 的主动千钧、失追、移动抢占、剧情方矩与金线确认均通过。

画板渲染使用 Direct3D 11。Android Vulkan 离线分别编译立体实例化和多视图，两个组合程序都生成了字节码。Quest 双眼显示、帧时间和裸手书写手感待佩戴实测。桌面回放运行时未连接 OpenXR 头显。
