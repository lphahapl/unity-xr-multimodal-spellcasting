# 沈商五口下胸工具带

[原设定稿](../../../docs/visuals/沈商_五口下胸工具带_定稿.jpg) · [工具带和补给规范](../../../docs/world/沈商_五口工具带与补给道具设定规范.md) · [Unity 装配 Prefab](../../../Assets/Art/Props/ShenShangToolBelt/ShenShangToolBelt.prefab) · [六件装配转模与批注](http://127.0.0.1:4190/rig/?collection=shenshang-toolbelt&model=toolbelt-assembled-v1)

六个混元 V3.1 原件共 299,994 三角面。Unity 装配两档分别为 13,668 / 5,156 三角面。每件各有 FBX、URP 材质和三张 PBR 贴图。底带与软袋用 2K，其他四件用 1K。材质遮罩的 R、G、A 分别用于金属度、环境遮蔽、光滑度，法线为 OpenGL 切线法线。粗布经纬和折痕可查看[底带近景](Verification/BeltBody-fabric.jpg)和[袋口近景](Verification/SoftPouch-look.jpg)。

| 部件 | LOD0 / LOD1 三角面 | 外廓宽 × 高 × 厚（mm） | 两档共用净宽 × 厚 × 深（mm） | 袋口锚点局部 XYZ（mm） | 验证 |
| --- | ---: | ---: | ---: | ---: | --- |
| 下胸底带 | 3,300 / 1,100 | 350 × 249 × 217 | — | — | [QA](Verification/BeltBody-QA.json) |
| 斜皮笔套 | 1,718 / 768 | 36 × 116 × 26 | 按实笔试插 55 深 | (-0.0, 116.0, -0.0) | [QA](Verification/PenSleeve-QA.json) |
| 窄平套 | 1,720 / 712 | 35 × 90 × 17 | 17 × 6 × 56 | (-0.0, 90.0, -0.0) | [QA](Verification/FlatSleeve-QA.json) |
| 中央软布袋 | 2,988 / 1,186 | 98 × 88 × 48 | 47 × 17 × 68 | (-0.0, 88.0, -0.0) | [QA](Verification/SoftPouch-QA.json) |
| 方形木骨布匣 | 2,200 / 760 | 78 × 92 × 44 | 38 × 19 × 80 | (-0.0, 92.0, -0.0) | [QA](Verification/WoodCase-QA.json) |
| 窄长筒套 | 1,742 / 630 | 28 × 92 × 33 | 13 × 16 × 57 | (-0.0, 92.0, -0.0) | [QA](Verification/TallSleeve-QA.json) |

袋口净空按中心对齐的刚性盒沿局部 −Y 插入测量，预留 1 mm 底隙。窄平套近档有一处近顶点提示，中央布袋远档有一处内部薄边贴合提示，详见对应 QA。笔套用于墨规笔，另四口按实物尺寸与姿态选用。图中位置用于默认陈列。斜笔套在装配中有 11° 倾角，锚点随之转动。墨规笔两档实物网格最小确认深度为 55 mm，建议止位 52 mm，预留取笔空间。[实笔试插](Verification/PenSleeve-T21Fit.json) · [Unity 回读](Verification/Unity-ToolBelt.txt)

混元回件、可编辑 Blender 文件和裁图分别在 [Models](Models) 与 [References](References)。[回件散列与三角面](Verification/HunyuanJobs.json)采用获选下载文件标识。资产列表刷新延迟期间，底带和木骨匣出现重复生成，各选一件入库。

[装配材质预览](Verification/Assembly-Studio.png) · [工作台正视](Verification/Assembly-View.png) · [旋转后画面](Verification/Assembly-Orbit.png) · [工作台读取与批注验证](Verification/Workbench-Verification.json)

Unity 6.3 实际导入、装配 Prefab 回读、两档材质和挂点检查通过。角色上身、佩戴形变、运行时取放、Quest / Pico 帧率尚待实测。


## V2 返工与实笔入口

[总装旋转与批注](http://127.0.0.1:4190/rig/?collection=shenshang-toolbelt&model=toolbelt-assembled-v2) · [实际 R04 四面试穿](V2/WearReview.jpg) · [内外布面和承托](V2/BodyReview.jpg) · [V2 Unity Prefab](../../../Assets/Art/Props/ShenShangToolBelt/V2/ShenShangToolBeltV2.prefab)

底带按冷态 R04 衣面放在下胸到上腹。外布、内衬及皮边构成贴身曲面，五口由各自的缝合挂环承托。V2 试稿移除旧版连续硬木尺。六件共 14,420 / 6,444 三角面，R04 角色与工具带仍是独立资产。

| 部件 | 本轮结果 |
| --- | --- |
| 底带与总装 | 与 R04 衣、袖、盘扣、腰绳及手臂静态三角对检无相交，五袋对检无相交。[精确拟合](V2/FitAssembly.json) |
| 斜皮笔套 | 高度 116 → 80 mm，外宽约 34 mm，四种 LOD 配对在轴线上插到 52 mm，握点露出 56 mm。[取放图](V2/PenSleeve/PenSleeve-V2-Review.jpg) |
| 窄平套 | 外宽 35 → 42.4 mm，双档净空 24.3 × 6 × 56 mm。[宽度及槽口](V2/FlatSleeve/FlatSleeve-V2-Review.jpg) |
| 中央软袋 | 厚度 48.05 → 30.94 mm，原孔边收成连续弧底。46 × 17 mm 通道净深至少 81.9 mm；实际干粮外袋沿轴无穿插。[弧底和袋面](V2/SoftPouch/SoftPouch-V2-Review.jpg) |
| 木骨匣与墨规笔 | 用户已通过 V1，原模型与材质保留。木匣中同一笔可在 52 mm 止位取放。[原件回读](V2/Approved-Unity.txt) · [木匣试插](V2/WoodCase-MoGuiPen-fit.json) |

[生产运行时入口](../../../Assets/Art/Props/ShenShangToolBelt/ChapterToolBelt.prefab) 使用已通过的实笔和木匣。储物和书写两项权限齐备才可取实体笔、启用画符。隔离桌面验证了未开放、拿笔书写、近还原口、换木匣和远放落地。正式二章已含统一 RPG 玩家和中段路线。正式场景尚未挂本组工具带、实笔控制器和画符板。 [接线交接](../../../docs/architecture/第二章_断衡悬道/07_工具带与实笔画符接线.md)。头显和角色动态贴合未测试。
