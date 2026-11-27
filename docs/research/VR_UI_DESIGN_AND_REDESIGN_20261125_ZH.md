# VR 游戏 UI 设计理念调研与《考工律度》全套 UI 重做总案

> 核查与撰写日期：2026-11-25。状态：**准望匣、加载及画符已完成第一轮桌面验证**。[接手续作记录](../../Tools/Validation/Evidence/GeminiTakeover1126/README.md)。  
> 调研依据：公开 VR 人因工程与平台规范（Meta Horizon OS Spatial UI / Eyes & Hands Guidelines、Valve《Half-Life: Alyx》、Vertical Robot《Red Matter 2》、Fireproof《The Room VR》、Sanzaru《Asgard's Wrath 2》），结合本仓库现行源码、Prefab、Shader 与最新美术定稿逐项对照。

---

## 第一部分：VR 游戏 UI 设计理念系统调研

在 VR（尤其是一体机如 Meta Quest 3 / PICO）中，UI 不再是贴在玻璃显示器上的“平面 HUD”，而是**占据真实立体深度、与玩家身体力学（手腕、头颈、视线余光）直接耦合的空间物体**。综合行业标杆与平台官方指南，现代沉浸式 VR 游戏 UI 遵循六大核心原则：

### 1. 四象限演进：从 Non-Diegetic 走向 Diegetic（叙事内生）+ Tactile Spatial（触觉空间）
传统游戏 UI 分四类，但在高沉浸叙事/机关动作 VR 中有明确优劣排序：
- **叙事内生（Diegetic UI，首选）**：UI 本身是世界观中的真实器物，NPC 与主角在逻辑上均能看见与触碰。
  - *行业标杆*：**《Half-Life: Alyx》** 将生命值与储藏指示直接铸入重力手套手背与枪械机匣；**《Red Matter》** 将翻译器与状态表集成在宇航服腕部硬件上；**《The Room VR》** 用机械发条盒、实体按扣与铜尺承载线索与收纳，让“打开面板”本身就是拨动机关的物理体验。
- **空间型（Spatial UI，辅助）**：锚定于三维世界坐标或物理道具近旁的清晰浮层（如物品名牌、教学展开板、画符案板）。要求与纯实体道具保留清晰的**视觉可触达区分（Affordance Separation）**，避免玩家分不清该“伸手抓取”还是“食指点击”。
- **元感知（Meta UI，克制使用）**：表达角色生理状态的视野周边效果（如受击边缘暗红、防晕定目帘），严禁遮蔽中心 60° 瞄准与格挡视野。
- **非叙事死锁 HUD（Non-Diegetic Head-Locked，尽量淘汰）**：像 PC 网游那样硬锁在摄像头正前方的 2D 框、纯黑锁头加载幕布，会破坏双目深度融合、引发追踪眩晕（Cybersickness）与眼疲劳。

### 2. 空间锚定与防晕铁律（Ergonomics & Anchoring）
根据 Meta Horizon OS Spatial Design Guidelines 与 *Half-Life: Alyx* 交互复盘：
1. **禁止高频信息硬锁头部（Avoid Head-Locking）**：核心交互面板严禁直接作为 HMD 子物体紧随每一帧微小的转头而抖动。若需随行提示（如字幕、结印候选、系统通知），须采用 **水平视向死区 + 弹性滞后跟随（Lazy-Follow / Angular Deadzone ±15°–20° + SmoothDamp）**，让玩家微转头观察场景时文字保持世界静止，视线偏离过远才平滑跟回。
2. **腕部唤起 → 世界释放定锚（Spawn-on-Wrist, Lock-to-World）**：Meta 官方明确指出：手腕是天然的快速仪表盘入口（遵循人类“抬腕看表”肌肉记忆），但手腕追踪存在不可避免的手臂悬停抖动（Gorilla Arm）与骨骼微颤。因此**“小而短”的瞄一眼状态放手腕，“大而可交互”的多按钮子面板展开后必须脱离手腕锁定在舒适世界空间（约 0.55–0.75 m、平视下方 10°–20°）**，避免“追着会晃的手按按钮”。
3. **舒适视锥与臂长边界**：主要可读区落在前方水平 $\pm 30^\circ$（总舒适锥 60°）及视线略下方 $15^\circ\text{–}30^\circ$，避免仰头或歪脖；近距直触（Poke）面板放在 0.35–0.55 m 臂展舒适区，中远距列表（0.7–1.5 m）切换为射线轻捏（Ray + Pinch），避免尴尬的“够不着又近在眼前”的中间死区（Middle-Distance Dead Zone）。

### 3. 加载过渡的 VR 舒适法则（Loading & Transition Comfort）
传统切场直接切黑屏 + 贴脸 `Loading 20%` 在 VR 中属于高危不适设计：
- **维持空间基准与地平线（Horizon Anchor）**：*Half-Life: Alyx* 采用 3D 立体微缩沙盘（Diorama）替代黑屏；*Red Matter* 通过气密闸门/电梯物理关合过渡；Meta Horizon OS 规范禁止突然切入无参考点的纯黑虚空（Black Void），建议在过渡空间中保留**柔和的静态地面刻度/天球微光环或世界空间锚定微构**，给内耳与前庭系统提供稳定的重力垂直基准。
- **渐进淡入淡出，禁止强制拉扯镜头**：过渡期间只可静态环绕展示器物自转或渐显进度，严禁在无玩家主动输入时强制平移或旋转相机。

### 4. 双眼光学、字号角尺度与护眼对比度（Optics, Angular Size & VAC）
- **辐辏调节冲突（VAC，Vergence-Accommodation Conflict）**：VR 镜片的物理焦平面通常固定在 1.3–2.0 m 左右；若将高密度阅读文字强行放在 0.25 m 以内长时间凝视，双眼必须剧烈内聚却要把晶状体调焦到 1.5 m，几分钟内就会诱发酸胀头痛。因此：**腕表刻格（`0.40 m`）只放极简图形/大色块，长文教学卡必须在距离头部 `0.65–0.90 m` 以上阅读**。
- **角尺度（Angular Size）与汉字笔画密度**：中文汉字比英文几何结构更密。按 Quest 3 中心约 25 PPD（Pixels Per Degree）估算：
  - 副标题/标签需占视角至少 **$0.45^\circ\text{–}0.60^\circ$（27–36 arcmin）**；
  - 正文需 **$0.65^\circ\text{–}0.85^\circ$（39–51 arcmin）**。
  - 在 Unity World-Space Canvas 中若未配 `CanvasScaler` 或 `dynamicPixelsPerUnit = 1`，动态字体会按低分辨率栅格贴图采样并被双线性模糊，**必须配合 `CanvasScaler.dynamicPixelsPerUnit ≥ 2.0`（或 TMP SDF）+ 无衬线中等字重（高字怀）**才能杜绝“糊字”。
- **杜绝纯白与纯黑（No `#FFFFFF` / No `#000000`）**：Meta 视觉规范明令：近眼透镜下纯白大底（`#FFFFFF`）引发严重眩光/光晕（Bloom/God-ray），纯黑（`#000000`）在自发光屏幕引发响应拖影（Black Smearing）。推荐亮字与浅面封顶在 `#DDD6C5`–`#E6DFCE`（温润米玉色），暗底不低于 `#14191A`–`#1A1E20`（玄石青漆）。

### 5. 空中画符与直触的触觉代偿（Expressive Drawing & Tactile Compensation）
VR 空气中没有物理桌面阻力：
- **深度视觉预判**：落笔前后存在约 1–2 cm 的 Z 轴深度盲区。玩家在空中伸笔时，必须在画板上映射一个**随笔尖 Z 轴距离动态缩放的接触准点/缩圈**，到达 `contactDistance` 时缩圈闭合+墨点溅落+微震提示，彻底消除“到底戳上板面没有”的猜疑。
- **紧凑动作盒**：绘画区控制在胸前约 `36 cm × 30 cm` 的肩肘轻松活动区内（避免“大猩猩臂”酸痛）。
- **容错视觉引导**：参考本作调研[游戏画符识别_20261122.md](游戏画符识别_20261122.md) 与 *The Room VR*，范本底稿以低饱和青玉线衬底，失败保留手绘墨迹以便直接补笔，成功时墨迹向范本金石吸附并迸发粒子，形成明确的仪式感闭环。

---

## 第二部分：本项目《考工律度》风格提炼与现行 UI 审计

### 1. 本项目确立的视觉DNA（来源：主设定 + 最新审定成果）
综合 [世界观白皮书](../world/考工律度_世界观设定白皮书.md)、[术法状态铜纹母图](../../ArtSource/UI/SkillStatus/术法状态铜纹母图.png)、[清圭 C4 定稿](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR01/Reference/C4_ThreeView_DesignGuide.png) 与 11 月 25 日 T-01/T-25/T-28 用户审定：
- **核心美学命题**：**“金石肃穆、刚性几何、重器沉静、去光污染（Anti-Pollution）”**，拒斥修仙五彩光斑与现代赛博霓虹。
- **最新审批工艺语言**：
  1. **器物结构**：先秦青铜叠边、云雷回纹角饰（如铜纹母图护盾边框与 C4 上肩云纹）、窄回纹环带；
  2. **材质质感**：温润半透汉白玉/青灰石留白 ＋ 古铜磨旧拉丝 ＋ 局部浅绿铜锈（Patina）＋ 细金线错金镶嵌；
  3. **交互点睛**：朱砂大漆填色（`#C83E1D` / `#E65100`）、石青/青玉珐琅（`#3FA88E`）、鎏金游标刻度线（`#D4A84E`）；
  4. **文字与纹样**：正文采用稳重黑体/宋体保证 VR 可读性；术印显形、章徽底纹、边角印信采用**可溯源的小篆印纹**与**先秦几何规矩刻线（方、圆、垂线、游标尺格）**。

### 2. 现行 UI 各模块逐项静态代码审计（问题清单）

| 模块 | 当前实现文件/Prefab | 实查发现的视觉与 VR 体验缺陷 |
|---|---|---|
| **准望匣 UI** | [WristMenuView.cs](../../Assets/scripts/ui/Rpg/WristMenuView.cs)<br>[Zhunwang_ButtonSplit.prefab](../../Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.prefab) | 1. **风格严重割裂**：`cyanColor = (0.22, 0.88, 0.95)` 与 `violetColor = (0.61, 0.45, 0.92)` 仍沿用早期**赛博霓虹电青/紫**！`_launcher` 按钮、`_pointer` 射线、`_reticle` 准星、`_decoration` 同心环均呈科幻全息感，与手腕上的青铜实物准望匣格格不入。<br>2. **无纹理纯色块拼接**：`AddPlate` 和 `AddButton` 全用 `sprite=null` 的纯色矩形 `Image` + `1.5px Outline`，缺少青铜叠边、云雷角饰、汉白玉/玄漆内衬与错金分隔线。<br>3. **缺少 `CanvasScaler` 超采样**：`NewCanvas` 与 `Zhunwang_ButtonSplit` 读数画布均未配 `CanvasScaler`，`dynamicPixelsPerUnit` 默认 1.0，近看腕部文字边缘易虚化。<br>4. **信息层级平面化**：按钮 `progress` 只是底部 `4px` 青线，缺少天工器物应有的游标卡槽与亮金按压感；`RightHandStatus`（快览）无统一衬底框直接散排格子。 |
| **场景加载** | [SceneLoadingOverlay.prefab](../../Assets/Prefabs/SceneLoadingOverlay.prefab)<br>[SceneLoadingBackdrop.prefab](../../Assets/Prefabs/SceneLoadingBackdrop.prefab)<br>[SceneLoadingProgressBar.prefab](../../Assets/Prefabs/SceneLoadingProgressBar.prefab)<br>[SceneLoadingOverlay.cs](../../Assets/scripts/logic/Environment/SceneLoadingOverlay.cs) | 1. **2D 锁头纯色遮罩**：`Canvas` 为 `RenderMode = ScreenSpaceCamera (PlaneDistance = 1.5)`，全屏贴脸硬锁头部。`Backdrop` 仅一块 `#0F1719` 纯色板，无空间地平线、无空气感，转头时像一块黑板贴在鼻前。<br>2. **原型级进度条**：进度条是用 `sprite=null` 灰底 + Unity 内置默认 `UISprite` (`fileID: 10905`) 拼接的 `360×7px` 细条，配平白 `25pt`“正在加载场景 0%”，没有任何《考工律度》浑天仪/测度尺/方圆界视觉叙事。<br>3. **切场硬跳隐患**：`Show()` 与销毁直接显隐，缺少柔和渐变包络，在帧率波动期切入新场景容易体感突兀。 |
| **墨规画符** | [GlyphBoardView.cs](../../Assets/scripts/logic/Rpg/Glyphs/GlyphBoardView.cs)<br>[GlyphStrokeGraphic.cs](../../Assets/scripts/logic/Rpg/Glyphs/GlyphStrokeGraphic.cs)<br>[GlyphBoard.cs](../../Assets/scripts/logic/Rpg/Glyphs/GlyphBoard.cs) | 1. **灰盒纯色板**：仅由三块无贴图直角纯色 `Image`（青黑外框、灰绿书写面、两个灰绿纯色方块“撤销/清空”）组成。<br>2. **按钮零视觉交互反馈**：`GlyphBoard.Sample` 虽然响应了 `button == 0/1` 触碰，但 `GlyphBoardView` 未保存按钮对象，无悬停高亮、无下沉反馈、无边界光提示。<br>3. **空中落笔深度盲区**：笔尖进入 `releaseDistance (19mm)` 但未到达 `contactDistance (9mm)` 时板面没有任何“预备落笔准点/缩圈”提示，玩家很难凭空判断还差几毫米触板。<br>4. **画板辅助刻度缺失**：缺乏古工匠墨斗/规矩盘的纵横浅暗界格线与外圈定度刻度，范本（`Guide`）与朱砂墨迹（`Ink`）缺乏金石压印质感。 |
| **结印候选卡** | [SealHintCard.prefab](../../Assets/Prefabs/UI/SealHintCard.prefab)<br>[SealHintPanel.prefab](../../Assets/Resources/UI/SealHintPanel.prefab)<br>[SealSequenceHintView.cs](../../Assets/scripts/ui/Skills/SealSequenceHintView.cs) | 1. **死锁头部角落**：`SealSequenceHintView` 将 `panel` 直接 `SetParent(head)` 在 `(0.61, 0.35, 1.1)`，转头瞄准时卡片始终硬贴视野右上角。<br>2. **无贴图色块结构**：`SealHintCard.prefab` 仅由纯色 `Brass`、`墨青底`、`印图区衬板` 三个无精灵方框套叠构成，进度条也是无精灵纯色 `Filled` 条。 |
| **旁白字幕** | [OpeningSubtitlePresenter.cs](../../Assets/scripts/ui/Rpg/OpeningSubtitlePresenter.cs) | 1. **无底衬裸字**：白色裸字直接 `SetParent(head)` 在 `(0, -0.23, 1.1)`，遇到首章汉白玉台阶、天仪白光或二章亮雪远峰时反差急剧下降。<br>2. **完全硬锁头部**：尚未使用团队已调研的“死区/阈值惰性跟随”，轻微转头看悬梁或低头装铜叶时文字会强行拽着视线移动。 |
| **敌人名牌与地脉提示** | [EnemyPoiseBar.cs](../../Assets/scripts/ui/Enemies/EnemyPoiseBar.cs)<br>[RpgInteractionFeedback.cs](../../Assets/scripts/ui/Rpg/RpgInteractionFeedback.cs) | 1. `EnemyPoiseBar` 表现为通用端游直角红橙黑条，无青铜/玄铁边饰、无刻度节段感。<br>2. `RpgInteractionFeedback` 默认 `pathColor = (.05, .85, 1)` 仍为科幻荧光青，与 `OpeningNavigationMarks` 已做好的白石/铜测标风格不一致。 |

---

## 第三部分：《考工律度》全套 UI 美术规范（Design System）

为保证后续全部模块在 Unity（Mobile Vulkan + PC Forward+）下高度统一且轻量合批，制定统一 **「天工金石 UI 设计系统（Kaogong Craft UI System）」**：

### 1. 统一护眼金石色板（已规避纯白 `#FFFFFF` 与纯黑 `#000000`）

| 语义层级 | 名称 | Hex（sRGB） | 用途与规范 |
|---|---|---|---|
| **主底板** | 玄漆玄石底 | `#141918` (94% Alpha) | 启动器、教学卡、画板基座、候选卡内底（替掉原赛博深蓝黑 `#060A10`） |
| **次级衬底** | 墨松石青衬 | `#1D2927` (96% Alpha) | 卡片图文槽、书写区、按钮常态底、状态格底 |
| **浅色留白** | 汉白温玉面 | `#E3DDD0` / `#D6CEBA` | 玉圭型标签、高光主标题、小篆阳刻底托（绝不用纯白） |
| **主结构框** | 哑光老青铜 | `#B8924C` | 主框叠边、云雷回纹框线、分隔游标尺线 |
| **高光错金** | 鎏金错金线 | `#E5BC63` | 选中边框、游标进度条、错金角线、成符金闪 |
| **确认/激发** | 熔核朱砂红 | `#D94E22` / `#E65100` | 朱砂墨迹、危险/损耗态、按压确认瞬间余光 |
| **灵息/就绪** | 青玉松石绿 | `#45B897` | 替换原赛博电青！用于就绪状态、度力充盈、指引导引光晕、落笔范本 |
| **次级文本** | 素缣灰金字 | `#AFA692` | 说明文本、单位、冷却数值、次级提示 |
| **预警/破韧** | 应力赭紫/赤 | `#E0483E` / `#9E5FD4` | 敌机破韧瘫痪态、受击边界晕 |

### 2. 共用可合批资产体系（Zero-DrawCall-Explosion）
全套 UI 不再依赖 `sprite=null` 的平直方块，而是构建**一张高复用的 1024×1024 九宫格（9-Slice）UI 纹样图集（UI Chrome Atlas）** + **2 套 URP 单通道立体（SPI）兼容着色器**：
1. **图集切片清单**（严格遵循 `术法状态铜纹母图.png` 与 `清圭 C4` 笔触风格）：
   - `Frame_Bronze_NineSlice`：外层古铜斜切倒角 + 内层细金错金线 + 四角极简先秦回纹（保留中心干净留白，支持任意比例面板拉伸不模糊角饰）。
   - `Button_Tablet_NineSlice`：铜圭状内凹按压按键底图（带微弱内阴影斜坡）。
   - `Rule_Grid_Tile`：墨规画板与天仪刻度使用的极淡纵横度尺暗纹与中心十字准标。
   - `Gauge_Slot_Bezel`：敌人血韧条 / 进度槽专用的段式铜箍刻格框。
   - `Subtitle_Smoke_Gradient`：两端渐隐的墨烟水平条纹底衬（解决生硬白边或无衬底问题）。
   - `Compass_Astrolabe_Ring`：加载空间与准望匣背饰共用的 **规·矩·律·准·绳** 游标刻盘环形切图。
2. **字体超采样策略**：
   - 所有世界空间 `Canvas` 强制挂载 `CanvasScaler`，设为 **`dynamicPixelsPerUnit = 2.0f~2.5f`**；
   - 继续统一绑定随包 [NotoSansCJKsc-Regular.otf](../../Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf)（保持 0 额外字体包体膨胀与完整生僻字覆盖）；术印/标题章纹使用独立小篆矢量切片，避免动态字体在斜角处拉锯齿。

---

## 第四部分：核心模块重做方案

### 模块一：准望匣 UI 重做（`WristMenuView` / `ZhunwangWristDisplay` / `SkillStatusRows`）
> **核心理念**：把“科幻全息面板”变成“从青铜准望匣核心投影/展开的**天工游标铜简与铜叶拓版**”，形成 **物理匣身 → 近身铜简入口 → 世界定锚教学竹铜卷** 的三层一体感。

1. **物理读数层（`ZhunwangWristDisplay` · 匣面）**：
   - 保留已验证的实例化 [WristGaugeInlay.shader](../../Assets/Art/VFX/Shaders/WristGaugeInlay.shader)（1 次绘制 20 格），在 `Zhunwang_ButtonSplit.prefab` 读数画布补挂 `CanvasScaler`（`dynamicPixelsPerUnit = 3.0f`），将「倒下提示 · 按朱砂扣恢复检查点」设为朱砂暗金微边框标签；气血/度力两侧补上极小的篆纹刻标「血」「度」。
2. **近腕启动器（`_launcher`，唤起时浮于匣侧上方）**：
   - 彻底剔除电青/霓虹紫！改用 **「玄漆底 + 青铜回纹九宫框 + 四枚错金铜叶卡槽按钮」**：
     - 4 个功能键改为竖向或微弧形错金圭牌（带对应的铜纹图标轮廓剪影：印诀、墨规笔、言灵铎、术法总览），底部「切换探索移动」改为一条细长的**推拉游标拨杆样式**。
     - 悬停进度条：从“4px 蓝条”改为**贯穿圭牌底部的鎏金游标刻尺填充槽**，悬停渐进填充、满格轻震+清脆铜簧音确认。
   - **指向射线与准星（`_pointer` / `_reticle`）**：
     - 射线颜色从亮青/金改为**“墨金细丝”（微弱暖铜金 `#D4A84E`，透明度 0.65）**，按下瞬间变为**“熔核朱砂点睛”（`#E65100`）**；
     - 准星不再是单调的平面十字圆环，而是一枚**微型同心方圆规印**：外圆（规）+ 内转 45° 小方框（矩），触碰按钮时方圆咬合缩紧。
3. **独立展开面板（`_panel`，世界固定，可捏住上沿铜梁拖动）**：
   - 整体采用“先秦错金铜版卷轴”构图（如 `清圭 C4` 的留白哲学）：顶部横贯一条古铜拖动把手（「捏住拖动」+「跟随/固定」卡榫开关），中段 `铜叶教学卡`（`_lessonSurface`）保留高清四点各向异性采样的手印照片材质（`TeachingSealPhoto.shader`），左右两侧改用青铜细线框区分「习得铜印菱标」「触发方式」「度力与息期」「口令词条」。
   - **右手掌心快览（`_rightGlance`）**：增加紧凑型青铜微框衬底（避免文字/图标直接飘在空中混乱背景前），展示 3~4 枚 `术法状态铜纹母图` 徽记，冷却时徽记转为暗铜、外沿一圈鎏金弧线随冷却顺时针闭合。

---

### 模块二：场景加载模块重做（`SceneLoadingOverlay` / `AsyncSceneLoader` / `SceneTravelGate`）
> **核心理念**：废除“贴脸 2D 灰黑塑料盒”，打造符合 VR 舒适规范与《考工律度》世界观的 **「方圆界 · 天仪静枢」过渡空间**。

1. **告别贴脸硬锁黑板**：
   - 将 `SceneLoadingOverlay` 从“贴在镜头前 1.5 米随头硬锁的 2D `ScreenSpaceCamera` 全屏方块”改造为 **World-Space 静枢过渡穹室（随加载开始一瞬对齐玩家当前站位与地平线，之后固定于世界坐标！）**。
   - **VR 舒适性核心**：玩家进入传送门后，原场景通过 Shader / 渐暗穹罩柔和暗隐（0.25s 平滑 Fade，而非瞬切黑帧），玩家仍脚踏一套**微微发光的青铜地面方圆界基标同心线**（给出稳定的水平地平线与下方空间锚，彻底消除前庭失重感与黑屏压迫感！）。
2. **器物级进度载体（浑天仪刻盘进度环）**：
   - 正前方舒适阅读位（距头约 `1.8 m`、视线下移 `10°`）悬浮一只极简低面的**虚空青铜天仪环组（双层反向慢转细铜环 + 中央五度游标弧）**：
     - 取消 Unity 内置 `UISprite` 细长条！进度填充驱动环形外圈上的**金石刻槽圆弧（360° 游标刻盘填满）**，圆环随进度渐亮并逐格点亮「规·矩·律·准·绳」小篆方位点；
     - 下方展示目标章节古朴印章题名（如 **「第二章 · 断衡悬道」** + 一行简短天工箴言，如 *“天地失度，以尺规正之”*，配精细百分比小数标尺）。
   - 加载完成且 `allowSceneActivation = true` 后，新场景先在后方就绪，静枢铜环顺时针合槽发出一声轻磐余音并向四周化散（0.3s Fade Out），视野平滑展开新世界。
   - **Quest 性能护栏**：整个过渡穹室使用纯 Unlit Instanced 线框/贴花片 + 1 张共享图集（`< 3000` 三角面，`< 8` Draw Calls），即使在场景切换卡顿帧也能维持稳定体感。

---

### 模块三：墨规画符模块重做（`GlyphBoardView` / `GlyphStrokeGraphic` / `GlyphBoard`）
> **核心理念**：把画符从“一块绿灰方片上画折线”重构为**“勘律使以朱砂磁墨在先秦青铜墨规拓板上补全天地几何”**的匠人实操台。

1. **「青铜墨规拓版」板面重构**：
   - 保持 `GlyphBoard` 精确的物理尺寸（书写区 `360 × 300 mm`）与 `$P` 采样坐标系完全不变，确保所有现存回归测试和剧情机关零破坏；
   - **结构升级**：
     - 外框改为**九宫格青铜叠边 + 云雷角榫 + 顶栏「汉白玉符名匾」**（显示“千钧符 / 方直之矩”及当前提示状态）；
     - 书写面内部嵌入一张半透明极细**「考工九宫度尺暗格」**（中心纵横墨斗十字线 + 四分度短刻线 + 外围方圆规距虚线），大幅提升玩家在空间中对齐直角与闭合梯形的几何参照直觉！
2. **「零深度盲区」落笔准星与笔触质感**：
   - 在 `GlyphBoardView` 新增**笔尖深度投影环（Nib Proximity Reticle）**：
     - 当墨规笔尖进入板前 `0.06 m` 预告区时，画板对应坐标上浮现一圈随距离收缩的**石青游标细环**（距离越近环越收拢、颜色由石青过渡至鎏金）；
     - 一旦穿过 `contactDistance (9mm)` 真正落笔，环瞬时收拢为一粒**朱砂墨滴溅点**，配合轻微笔触音，解决玩家在空气中“不知笔尖距板面还有多远”的通病。
   - **笔画视觉升级**：在 `GlyphStrokeGraphic` 或其搭配材质中，给朱砂墨迹（`#E65100` → `#FF7A38`）加上微弱的金石笔锋亮芯与轻微外缘晕染感，停笔判错时保留墨迹并将缺失边缘在范本（`Guide`）上做一次温和的青玉呼吸脉冲引导提示；成符瞬间沿吸附轨迹滑入范本并触发由中心向外扩散的**方圆金线回响波纹**。
3. **实体感触碰按钮（撤销 / 清空）**：
   - 改造底部两个无交互状态的死板方块为**带青铜按槽的铜印块**（「退一笔 · 撤销」「拭净板 · 清空」）；
   - 将 `GlyphBoard.Sample` 中的悬停（笔尖距离 ≤ `releaseDistance`）与按下（`depth ≤ contactDistance`）状态实时透传给 `GlyphBoardView`，触发**按钮金边点亮 + 视觉微下沉 2 mm**，给足触碰反馈。

---

### 模块四：战斗与引导周边 UI 统一化改造（闭环整体观感）
为防止只有三大模块换新而其余浮层仍突兀，建议同步统一以下 4 个高频组件：

| 周边模块 | 现行缺陷 | 重做方案 |
|---|---|---|
| **结印候选卡**<br>(`SealHintCard` / `SealHintPanel`) | 1. 直接 `SetParent(head)` 硬锁右上角；<br>2. 纯色三框拼接。 | 1. **锚定升级**：改为相对头部水平偏航 `±22°` 死区惰性跟随（或随身体朝向在偏右上方 `1.1 m` 处平滑阻尼跟随），消除强行贴脸追视疲劳；<br>2. **美术升级**：套用共用 `Frame_Bronze_NineSlice`，左侧印名配小篆微型底章，右区以「温玉留白衬底 + 细金隔栏」承托 `Teaching/Seals` 手势图，「稳住释放」蓄力条改为**鎏金榫卯合槽进度条**。 |
| **旁白字幕**<br>(`OpeningSubtitlePresenter`) | 1. 硬锁头部前下方；<br>2. 无底衬，亮背景看不清。 | 1. **渐隐墨烟条衬**：添加两端羽化的轻量玄漆半透明渐变带（中心 ~72% 不透明 `#141918`，两端完全消隐，无矩形硬边）；<br>2. **护眼双色排印**：说话人「申屠：」使用鎏金 `#DFBA66`，正文使用柔温玉白 `#E5DFD0`；<br>3. **惰性随动**：水平转角阈值 $\pm 18^\circ$ 内世界稳定、超限平滑跟回，兼顾低头操作双盘与远望不跑偏。 |
| **敌人血条与韧性条**<br>(`EnemyPoiseBar`) | 直角纯黑红橙调试条，缺乏机巧/金石风。 | 升级为**「玄铁测标游标尺」**：共享图集铜箍边框 + 分段格（五分/十分微刻线）；普通状态暗朱红血量 + 琥珀机簧韧性；**破韧瘫痪期**整条切换为玄紫急促脉冲闪耀，一眼辨识 3.5s/10s 暴击窗口。 |
| **地面导引与投掷准线**<br>(`RpgInteractionFeedback`) | `pathColor` 仍为赛博亮青 `(0.05, 0.85, 1)`。 | 统一收束进项目色板：探索引导路径设为**青玉温光 `#45B897`**，驻留蓄力与选点确认变为**错金鎏金 `#E5BC63`**，火核瞄准准星改为**朱砂微金准线 `#E06224`**（与 `OpeningNavigationMarks` 的测标材质完全融为一体）。 |

---

## 第五部分：Quest 3 / PC 双平台工程与性能约束

结合前序 [全局 Shader 调研](GLOBAL_SHADER_VR_20261125_ZH.md) 已锁定的工程基线（Unity 6000.3.10f1, URP 17.3.0, OpenXR SPI, Android Vulkan + MSAA 4×）：
1. **单通道立体渲染（SPI / Multiview）硬要求**：所有新增 UI / 加载过渡 / 画板特效 Shader 必须声明 `UNITY_VERTEX_INPUT_INSTANCE_ID` 与 `UNITY_VERTEX_OUTPUT_STEREO` / `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`。
2. **合批与带宽纪律**：
   - 将青铜边框、按钮底、暗格、进度槽全部打入**单张 UI Sprite Atlas**，使同一面板的数十个装饰元素可合入同一材质批次（Control Draw Calls）；
   - 加载静枢与悬浮卡严格禁止全屏后处理（Bloom/Blur/Opaque Texture），半透明叠加层控制在 $\le 2$ 层，保障 Quest 3 72/90 fps 帧预算（单 UI 系统额外 GPU 开销控制在 $\le 0.25\text{ ms}$ 内）。
3. **业务契约零破坏边界**：
   - 准望匣重做**严守 `WristMenuController` 现有的 `Targets` / `WristUiTarget`、双模输入仲裁（Poke + Ray+Pinch）及 `ZhunwangWristDisplay` 对外接口**，只升级表现层 `WristMenuView`、3D 读数 Prefab 与材质贴图；
   - 加载重做**严守 `AsyncSceneLoader` 与 `SceneLoadingOverlay.Show(view, load)` / `Finished` 状态契约**，确保 `SceneTravelGate` 跨章跳转无需修改调用方式；
   - 画符重做**严守 `GlyphBoard` 坐标域（`HalfWidth=0.18, HalfHeight=0.15`）与 `$P` 识别采样逻辑**，确保 `GlyphLab` 与二章全部 30+ 项已有检查继续全过。

---

## 第六部分：实施路线图与待确认决策点

建议按优先级分 3 个批次落地：
- **阶段 A（基础视觉系统与资源）**：制作统一「天工金石 UI 图集（1024² 九宫格框、回纹角饰、度尺格线、浑仪刻盘、烟墨字幕底）」、补齐各 World-Space Canvas `CanvasScaler` 超采样设置。
- **阶段 B（核心三大模块重做）**：
  1. 重做 **准望匣 UI**（去赛博青紫、接入错金框/游标按钮/墨金准星/快览底衬）；
  2. 重做 **场景加载**（世界空间「天仪静枢」过渡穹室 + 浑仪环形进度）；
  3. 重做 **墨规画符**（青铜拓版 + 九宫格暗线 + 深度预告落笔圈 + 按压微沉按钮）。
- **阶段 C（周边体验与人体工学统一）**：升级 `SealHintCard` 与 `OpeningSubtitlePresenter` 的死区惰性跟随、改造 `EnemyPoiseBar` 与 `RpgInteractionFeedback` 配色，并在 Quest 3 / 桌面验证可读性与回归用例。
