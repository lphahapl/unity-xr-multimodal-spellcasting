# 二章通路符支路接入

供二章场景负责人合入。安装基线为 `e7aeb66bbe4f23bd8ff8b209db005a47565c2e4736f6c92625302145fe785cc6`，包含通路符的隔离候选为 `586d04a981f14758d402386d24137f35728d81b338ba7d1878a80ba0f6110958`。

## 合入

1. 保留正式玩家现有的 `GlyphPenController`、`Chapter02BridgeEquipment` 和原工具带。本批次需要工程中的 [GlyphPenController](../../../../../Assets/scripts/logic/Rpg/Glyphs/GlyphPenController.cs)、[GlyphEffectDefinition](../../../../../Assets/scripts/logic/Rpg/Glyphs/GlyphEffectDefinition.cs)、[PassageGlyphEffect](../../../../../Assets/scripts/logic/Rpg/Glyphs/PassageGlyphEffect.cs) 和已有 [Passage](../../../../../Assets/Art/Glyphs/Passage.asset)、[Qianjun](../../../../../Assets/Art/Glyphs/Qianjun.asset)、[铜榫 Prefab](../../../../../Assets/Art/Glyphs/PassageBankSocket.prefab) 及其原依赖。
2. 任选一种接线。场景文件已保存且编辑器没有该场景的未保存改动时，在仓库根先执行 `git -c core.autocrlf=false apply --check Tools/Validation/Evidence/Chapter02Assembly/passage-branch-1126/scene.patch`，通过后去掉 `--check` 应用同一文件。也可以在二章场景使用菜单 **Tools → NarutoVR → 第二章断衡悬道 → 装配通路符支路与主动符谱**。菜单[安装器](../../../../../Assets/scripts/logic/Editor/WorkshopTenon/Chapter02PassageBranch.cs)修改当前场景内存，由场景负责人检查并保存。菜单重复运行会检查三个既有岸位。发现局部或其他布点时会停下并告知冲突。
3. Inspector 读回玩家主动模板应包含通路符和千钧符。石案的 `GlyphStoryPanel.template` 仍为 `SquareRule.asset`。在修复态桥面下应只有一个「符渡 · 祭坛北沿支路」，内部有检修挑台、两座望台和三个 `PassageGlyphAnchor`。各岸的 `bank` 不同。新建的走面、所有铜榫和靠近提示，都跟随原桥修复态启停。
4. 新局从石门向前，到石案按铜扣、取笔临摹方直之矩。走过修好的教学桥，在进入祭坛后向左找靠栏端的检修挑台，从旧栏端开口绕到甲岸铜榫。乙岸和丙岸各有同形铜榫。画通路符，归笔时在笔套附近松开，抬起右手选岸。选中第一岸后可更换第二岸，放低右手取消，握拳确认。桥前沿到岸后可用原探索行走方式跨过。千钧符在同一支笔的主动板上书写，成符后继续持笔指向可牵引重物。
5. 试玩采用已有配置 `maxSpan=7 m`、`reach=12 m`、`deckWidth=1.1 m`、`formationSeconds=2.4 s`。两座望台为本次支路的简模。支路沿用祭坛原栏和碰撞。近岸选面、归笔和栏端净宽仍需 Quest 佩戴实测。

## 隔离重放

在工程副本内将本目录的 `Chapter02PassageBuild.cs` 与 `Chapter02PassageReplay.cs` 放到 `Assets/scripts/logic/Editor/`。使用 Unity 6000.3.10f1，在该副本以 `-batchmode -force-d3d11 -executeMethod Chapter02PassageBuild.Run -quit` 安装并检查二次装配，再以 `-batchmode -force-d3d11 -executeMethod Chapter02PassageReplay.Run` 进入 Play。后者自行退出并把 JPG、`frames.csv`、`checks.txt` 写入副本的 `Logs/ChapterPassage-日期时间/`。两次调用均需指定 `-projectPath <副本>`。

回放从新存档进入。只替换手追关节输入源，并设置桌面眼高及朝向。原玩家 LateUpdate、接近触发器、按钮、墨规笔、笔尖判别、物品归槽、射线、握拳和移动碰撞均在 Play 中运行。编码入口为 `python encode.py <原始帧目录> <输出.mp4>`。行走镜头每三个 Unity 帧取一帧，成路镜头逐帧保留。

## 本次验收

- [二章实际 Play 录像](../../../../../docs/visuals/20261126_ch02_passage/二章通路符_石案至乙岸.mp4)为 701 帧、30 fps、23.37 秒。第 545、563、581 帧分别可见桥面 25%、50%、75%，第 600 帧记录抵岸并开放碰撞。源头与对岸接点的直线距离为 4.08 米。
- 新局经正常左手移动到石案，右手按铜扣，实体笔完成方直之矩，过修复桥后绕开旧木栏。主动板先识别千钧符、进入等待指向重物状态，再识别通路符。通路符复用原笔套，松手归笔后保持选岸。此次画千钧验证至生产牵引状态，未演示拉镇石入篮。
- 甲岸选中后可预览丙岸。放低右手，预览收起并释放输入占用。再画一次，同样先预览丙岸，然后改选乙岸并握拳。成路中 73 个采样的碰撞均关闭。左手步行落在乙岸原石面，主线仍为祭坛阶段。[状态与触发日志](checks.txt) · [逐帧坐标](frames.csv)
- 原装饰网格会挡住经过渡榫的玩家。对三个岸位的六个方向，分别以横向 −16、−8、0、+8、+16 cm 进行半径 22 cm 的胶囊扫掠。原装饰网格 30 次中有 28 次接触；候选在章节实例使用选岸 Trigger，30 次均畅通。石台、崖边原栏杆及完整桥面保留实体碰撞。[净空对照](clearance.txt)
- 单写者场景补丁仅对玩家的模板数组和修复态桥面子节点列表两处已有记录做增量，新增 78 个 Unity 序列化记录。已在临时目录从正式字节应用补丁，结果等于实际 Play 的候选散列。[文件指纹与关键帧](receipt.json) · [场景补丁](scene.patch)
