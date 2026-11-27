# 2026-11-26 Gemini 续作验收

Gemini 本机项目记录显示两项任务在模型额度 429 时中断。接手时已有准望匣与加载界面代码、共用瘫痪与起立 FBX 及两份 Animator 接线。本次继续现有实现并完成双路桌面验证。

## 界面

- 原加载检查在销毁预览相机后才读取 Canvas 模式。现用真正的 SceneLoadingOverlay.Show 绑定相机，在绑定仍有效时断言模式和进度。隔离工程使用真正的 SceneTravelGate 进门，观测旧相机、新场景相机接力和蒙版收起。
- 准望匣启动器加高原标题字框，实测生成中文网格。画符板复用原九宫格图集，加入铜框、温玉匾、度尺、落点与按压反馈。画符局部坐标、采点和辨符规则沿用。
- Unity 6000.3.10f1，KaogongUIRedesignChecks.RunBatch 出口 0，共 286 项。GlyphPlayChecks.Run 出口 0，覆盖真实 GlyphLab 的取笔、千钧、剧情方矩、吸附与提示。见 [UI 检查](UI-Report.json)、[四联实拍](Kaogong-UI-Comparison.jpg)、[传送门实跑](Loading-Play.txt)、[画符实跑](Glyph-Play.txt)。

## 敌人

- 残机偶和掷火残机共用 CanJiOu_DazedSeated 与 Mixamo 81 帧动作 CanJiOu_StandUp。原始 FBX 仍是下载文件。StandUp 的根 Y 基于脚部，现行 Animator 继续关闭 Root Motion。
- 修正前起身最低脚骨分别约 -0.65 m 与 -0.72 m。修正后为 +0.034 m 与 -0.010 m。合成时间步 50 ms，单独测自双方预制体，直接读取脚骨。两只从削韧触发到恢复为约 6.85 s。起身时保持零韧性和行动锁，死亡中断后不回待机。
- EnemyStandUpChecks.Run 出口 0，43 项专项通过。EnemyAnimationContinuityChecks.Run 出口 0，52 项连续性通过。两套敌人一并复跑原火核格挡和敌人导航回归。见 [原脚高](Enemy-StandUp-BeforeFeet.txt)、[修正后](Enemy-StandUp.txt)、[原关卡动作连续性](Enemy-Continuity.txt)。

已核对 [正式源文件 SHA-256](Input-SHA256.json)。桌面跑合成手或短程试验镜头，Quest 佩戴可读性、双眼一致性以及武器和鞋底真实网格近看待实机核对。
