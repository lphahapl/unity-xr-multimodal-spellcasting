# 赭朱墨迹与纤维纸写面

[同轨迹对照视频](GlyphBrush-Material-AB.mp4) · [末帧截图](GlyphBrush-Material-AB.jpg) · [「永」字笔迹近景](Glyph-Calligraphy.png)

左侧为[上一版录屏](../GlyphBrush1126/GlyphBrush-Desktop.mp4)，右侧为新版 Unity 画面。两版输入相同的三笔和时间戳。视频依次展示慢笔、快笔、减速折笔和加速收锋。「永」字由五笔模拟轨迹驱动同一个画板实时生成。

新版用局部笔画 Shader 表现浓墨核心、随笔程变化的宽窄和毛边、附在纸面上的浅渗边。干笔留出不连续的笔毫间隙，实际下面的纸面透出来。纸面是一张自制 512×512 的低对比纤维纸，范本和落点的颜色随写面调整。

Unity 6000.3.10f1 的 GlyphLab 和新版 UI 回归通过，剧情入口的[书写](GlyphLab-Story.png)和[金闪](GlyphLab-Gold.png)均有实拍。Android Vulkan 立体实例化和多视图离线编译成功。视频为 11.8 秒、354 帧，逐帧解码通过。Quest 裸手和双眼显示待实测。

[录制参数和散列](GlyphBrush-Material-AB.capture.json) · [检查输出](Checks.txt) · [正式输入散列](Input-SHA256.json) · [UI 报告](UI-Report.json)
