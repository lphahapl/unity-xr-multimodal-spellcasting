# 通路符三岸试验场

## 在 Unity 里试玩

1. 打开 [PassageGlyphLab](../../Assets/Scene/PassageGlyphLab.unity)，进入 Play。出生点在甲岸，前方是乙岸，左前方是丙岸。看向断口，岸沿有向外伸出的石榫槽，每岸有一根镂空铜榫。近岸和对岸的铜榫都铸着通路符的门框和短横。靠近可连接的渡口，会看到「画通路符，可连两岸」。
2. 右手在上腹右侧轻捏取墨规笔。提示收起，试绘图谱展开。第一笔画出开口向下的门框，第二笔画框内的短横。抬起笔尖，停笔约 0.9 秒。
3. 辨符成功后，抬起右手可见淡金通路符纹。伸食指指向铜榫或地上嵌纹，顺着光束看落点。有效接触面会亮起圆形刻度和菱心。第一岸锁定后保留菱形金框。再指向另一岸的榫座，两岸间出现青白色预览轮廓。可指向另一个合法榫座换岸。指过岸后若要取消，用空右手张掌，将手腕保持在头部下方至少 60 厘米，持续 1 秒。归笔时可先放下手，再抬手开始选岸。
4. 确认预览连向想去的岸后，握右拳。淡金桥面从第一处接岸点开始，沿轮廓向第二处接岸点连续延长。前沿抵达对岸时，整座桥才开放碰撞。左食指指向前方即可在探索行走模式下过桥。实验场的桥保留到重载场景。正式二章会按已完成的断桥教学存档恢复可走桥面。

甲岸有两个标记，可以验证同岸配对会被拒绝。在甲岸分别选择乙岸和丙岸，可建两条路线。射线根据实际右食指指尖和指节计算，实体遮挡会截断选择。指偏、换手形和短暂失追均保留已选岸与预览。失追时手光和射线收起，重获追踪后重新伸指或张掌，再握拳确认。握拳时按当下手位重新核对遮挡、距离与连接资格。目标失效会撤掉对应预览。

## 关卡接线

- 把[通路符模板](../../Assets/Art/Glyphs/Passage.asset)加入目标玩家的 `GlyphPenController.activeTemplates`。独立场景已经配置。模板中的门框图形用于试绘，可在 `GlyphTemplate.examples` 中替换。
- 岸位根挂 [PassageGlyphAnchor](../../Assets/scripts/logic/Rpg/Glyphs/PassageGlyphAnchor.cs) 和覆盖地面嵌纹的浅 Trigger。`bank` 引用岸节点，同岸的多个落点共用它；`landing` 与可走面齐平。在其下放置[符渡石座 Prefab](../../Assets/Art/Glyphs/PassageBankSocket.prefab)，正 Z 朝断口。铜榫和石座上的 MeshCollider 也能选中所属岸位。同岸已有高标时，关闭次落点 Prefab 的「通路符铜榫」子物体即可保留低槽。中央通道留给玩家与新生桥面。
- 每组渡口放置一个 [PassageApproachHint](../../Assets/scripts/logic/Rpg/Glyphs/PassageApproachHint.cs)，绑定玩家与该组全部岸位。`approachDistance` 默认 3.25 米。玩家面向近岸、有通路符模板且范围内存在可连接的另一岸时，只显示一句提示。收笔取消后可再次提示，本组成功架桥后收起。可选的 `strokeReference` 指定取笔书写时才显示的范本。
- `landing.gameObject.layer` 取玩家 `RpgLocomotionController.floorLayers` 中的可行走层，接岸点落在有地面碰撞的边沿。确认后桥面使用第一岸的这一层。检查两端净空、坡度与左右分岔的碰撞。试验场验证的是静止岸面。
- [通路符效果配置](../../Assets/Art/Glyphs/PassageEffect.asset)中可调 `formationSeconds`。试验场取 **2.4 秒**，跨度变化时仍按这一时长从第一岸铺到第二岸。`maxSpan` 按两处接岸点的三维距离判断，试验值 7 米。手指射程 `reach` 为 12 米，桥宽 `deckWidth` 为 1.1 米。指过岸后，低位张掌取消的腕部深度与保持时间分别由 `cancelBelowHead`、`cancelHoldSeconds` 调整。正式时长、最大跨度、符形及自由搭路岸位待定。
- 正式二章开场石案直接教通路符。走到断口，按铜扣开板，取笔照范本描符；归笔后右食指先后指两岸铜榫，握拳铺桥。桥头到岸才开放碰撞并完成这一阶段。已过桥的检查点恢复完整符桥。祭坛北沿保有三岸自由配对支路。[正式断桥回放](../../Tools/Validation/Chapter02MainPassage/README.md)

## 隔离重放

将 [PassageGlyphPlayChecks.cs](../../Tools/Validation/PassageGlyphPlayChecks.cs) 复制到独立工程副本的 `Assets/scripts/logic/Editor/`。以 Unity 6000.3.10f1 执行 `-batchmode -nographics -projectPath <副本> -executeMethod PassageGlyphPlayChecks.Run -logFile <日志>`。结果写入副本的 `Logs/PassageGlyph.txt`。回归向玩家输入入口送入合成手部观测，覆盖取笔、实际笔尖采样、停笔辨符、选面、握拳和探索行走。

本次桌面回放通过甲→乙、甲→丙及抵达乙岸后的乙→丙配对。无桥时玩家停在甲岸边缘，成桥后沿同一路径连续行走抵达乙岸。真机输入手感与正式关卡布点待验。[逐项实跑记录](../../Tools/Validation/Evidence/PassageGlyph.txt)

## 右手符纹与选岸射线

[断口识路与过桥实录](../visuals/20261125_dodge_glyph_play/通路符_断口识路与过桥.mp4) 从取笔前的[玩家视角](../visuals/20261125_dodge_glyph_play/通路符_断口玩家视角.jpg)开始。2.40 秒切到桥座近景，7.70 秒锁定甲岸，9.23 秒预览通路，10.50 秒握拳，12.93 秒接岸开放通行，20.60 秒已到对岸。[本次逐帧读数](../visuals/20261125_dodge_glyph_play/通路符_断口识路与过桥.frames.csv)

日常布点可直接复用符渡石座 Prefab。若需重建试验场的石座网格，将[生成工具](../../Tools/Validation/BuildPassageBankMarkers.cs)放入隔离工程的 Editor 程序集，执行 `-batchmode -executeMethod BuildPassageBankMarkers.Run -quit`。重新启动该副本后再录制，以便从磁盘重载网格、材质及场景实例。

[观看手光、射线和落点近景](../visuals/20261125_dodge_glyph_play/通路符_手光与射线实录.mp4)。本次桌面 Play 回放在 2.70 秒辨符成功，3.90 秒锁定甲岸，5.43 秒显示乙岸命中近景，6.70 秒握拳。右手符纹短暂扩亮再稳定下来。门框与短横取自本试验场的通路符笔形，腕部到指根决定符纹位置。掌心和手背的符面随右手翻转，单面剔除使两面各自朝外。

射线从右食指实际指尖出发，窄亮芯上有向远处移动的淡金光点。指向可用岸标时，落点符纹沿实际命中面贴放。持续指向已选的第一岸时，光束仍停在该岸。扫向空处或遇实体遮挡时，落点纹熄灭；继续持笔可以重新瞄准。握拳后手光、射线与落点退场，转入下方的成路过程。[手部与落点材质](../../Assets/Art/Glyphs/PassageHandSigil.mat) · [射线材质](../../Assets/Art/Glyphs/PassageCueGold.mat) · [逐帧读数](../visuals/20261125_dodge_glyph_play/通路符_手光与射线实录.frames.csv)

## 半透明淡金符面

[淡金流光版试玩](../visuals/20261125_dodge_glyph_play/通路符_半透明淡金流光实录.mp4)使用与下方初版录像相同的机位。辨符后的手部光圈、右食指射线与第一岸菱形有淡金微光，第二岸预览保留青白色空心轮廓。握拳后，光带沿桥面向对岸移动，依次照亮菱纹和边缘刻度。接通后仍能透过桥端看到岸上的石纹。

桥的可走面与两岸齐平，可见符面沿桥面法线抬高 12 毫米。选面线中心高于岸标顶部 20 毫米。重叠的通路按单层透明符面叠色。桥面材质的 `Film opacity` 控制底色浓淡，`Shimmer strength` 控制亮纹起伏，`Light travel` 控制光带沿桥移动的速度。当前试验值分别为 0.26、0.75、0.84 米/秒。[桥面材质](../../Assets/Art/Glyphs/PassageGold.mat) · [手光与选面材质](../../Assets/Art/Glyphs/PassageCueGold.mat)

有图形设备的副本可用 `-batchmode -force-d3d11 -executeMethod PassageGlyphPlayChecks.Run` 复查。此次桥中明暗底色经符面后仍保留约 38% 的绿色通道对比。甲乙各三个低视角均读到符面。视频 6.40 秒握拳、7.60 秒半程、8.83 秒开放碰撞、16.50 秒站上乙岸，共 517 帧，全部取自同一次桌面回放。Quest 双目观感待实机复核。[逐帧状态](../visuals/20261125_dodge_glyph_play/通路符_半透明淡金流光实录.frames.csv) · [录制设置与源文件哈希](../visuals/20261125_dodge_glyph_play/通路符_半透明淡金流光实录.capture.json)

Android Vulkan 可另执行 `-batchmode -quit -executeMethod PassageGlyphPlayChecks.CompileAndroidVariants`。桥面与提示光在立体实例化和多视图两种关键字下均已生成顶点与片元的组合程序。[Unity 回归和编译结果](../../Tools/Validation/Evidence/PassageGlyph.txt)

## 初版录像与重录步骤

[通路符选面与成路实录](../visuals/20261125_dodge_glyph_play/通路符_选面与成路实录.mp4)长 17.23 秒。镜头从符板切到两岸全景，再切到玩家的过桥视角。右上小窗显示同一帧驱动输入的 Meta 右手，字幕中的甲岸和乙岸对应实际 `landing`。

| 录像时间 | 视频帧（从 0 起） | Play 中观测到的状态 |
|---|---:|---|
| 2.70 秒 | 81 | 通路符识别成功一次 |
| 3.53 秒 | 106 | 右食指锁定甲岸 |
| 5.13 秒 | 154 | 乙岸进入预览，两岸间出现空心轮廓 |
| 6.40 秒 | 192 | 握拳确认，桥面进度 0 |
| 7.00 / 7.60 / 8.20 秒 | 210 / 228 / 246 | 前沿到达 25% / 50% / 75%，桥面碰撞均关闭 |
| 8.83 秒 | 265 | 前沿接上乙岸，碰撞首次开启 |
| 16.50 秒 | 495 | 玩家已走至乙岸地面 |

本次实测两岸间距 3.90 米。甲岸坐标为 `(120, 0, 121.8)`，乙岸为 `(120, 0, 125.7)`。第 193–264 帧连续 72 个 Unity 帧可见桥面延伸，确认后即使右手离开，已确认的桥继续生成。第 264 帧 CSV 的六位小数已舍入至 1，程序仍处于成路状态，下一帧才开放碰撞。第 105 帧切镜时旧画板短暂出现洋红色，成片仅此帧的画面采用同一镜头第 106 帧，逐帧 CSV 保留全部原始读数。[逐帧读数](../visuals/20261125_dodge_glyph_play/通路符_选面与成路实录.frames.csv) · [录制参数、源文件哈希及画面替换记录](../visuals/20261125_dodge_glyph_play/通路符_选面与成路实录.capture.json)

重录时，将[录制入口](../../Tools/Validation/PassageGlyphPlayCapture.cs)放入隔离工程的 `Assets/scripts/logic/Editor/`，在有图形设备的 Unity 6000.3.10f1 中执行 `-batchmode -force-d3d11 -executeMethod PassageGlyphPlayCapture.Run`。日志中的 `PASSAGE_FILM_DIR` 指向此轮逐帧图像与 CSV。运行 `python Tools/Validation/encode_passage_glyph_play.py <逐帧目录> <输出.mp4>` 可编码和校验。录制器逐帧回放取笔与画符输入，使用官方 OpenXR 手模姿势经玩家手形判定执行选面、握拳。行走由玩家探索移动控制器执行。录制时替代了桌面无头显的输入轮询，成路用正式组件的 Unity Update。
