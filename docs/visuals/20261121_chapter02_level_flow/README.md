# 《考工律度》第二章「断衡悬道」关卡动线与核心概念图集

> **修订日期**：2026-11-21  
> **关联场景**：第二章《断衡悬道》（`Chapter02_SuspendedPath.unity`）  
> **详尽指南**：参见完整文档 [docs/architecture/第二章_断衡悬道/03_第二章关卡动线与出图指南.md](../../architecture/第二章_断衡悬道/03_第二章关卡动线与出图指南.md)

---

## 零、 本项目核心美术风格总述 (Project Art Style)

本项目严格确立**“先秦工匠物理 / 墨家朋克（Mohist Punk / Artisan Physics）”**的核心美学体系：
1. **材质基调**：
   - **青灰冷岩与玄武石基**（60%+ 场景基底）：冷硬坚实，粗凿岩体与青石基石；
   - **汉白冷玉**：度量基准、规矩门楣、阴刻水尺与度线；
   - **哑光暗沉青铜**：齿轮机械、榫卯连杆、天平悬臂与准望匣外壳，斑驳铜绿包浆，严禁浮华刺眼的镀金；
   - **青冈硬木/栎木**：梁柱框架、机偶活动木骨，榫卯受力紧密；
   - **矿物朱砂与磁墨**：机关锁止指示、墨规笔画符光墨，纯正沉着天然朱砂红；
   - **荧光冰蓝**：准望匣度力充沛辉光、地表寻路导航光点与连续路径线，澄净冷冽。
2. **天地失度异常美学**：
   - 展现天钟脱扣崩坏后的力学失常：水往高处走的逆流飞溅、脱榫悬停半空的重梁、断轴自转的天仪铜环；
   - 每一处异常均有严谨的工匠力学对比参照（如垂直铅锤、直立水尺、对应榫眼），杜绝无意义的彩色魔法光雾。
3. **Quest 3 性能边界**：
   - 单重视锥内三角形面数控制在 15 万面以内，Draw Call 控制在 180 批次以内；依靠反 Z 字形转角山体岩脊做自然遮挡剔除（Occlusion Culling）。

---

## 一、 首章出口【崖端石门】实景对照与衔接清单

玩家由第一章《初醒》（`Chapter01_Courtyard.unity`）穿过天仪阁后门走向崖端石门，第二章起点紧密承接该石门。

| 首章实拍机位（真实工程截图） | 路径链接 | 空间与构件衔接要点 |
|---|---|---|
| **崖端石门正面特写** | [35_石门立于崖端.jpg](../20261119_courtyard_scene/实景/35_石门立于崖端.jpg) | 嵌于独立“崖舌”之上，汉白玉门楣、青灰石闸门扇、暗青铜门臼轴套。门外即是向下的万丈虚空。 |
| **天仪阁后门望石门** | [17_阁后望崖端石门.jpg](../20261119_courtyard_scene/实景/17_阁后望崖端石门.jpg) | 穿出阁后木槅门，直面悬崖边孤立的石闸，展现“高崖出关”的临渊感。 |
| **站在石门前回望天仪阁** | [18_崖上回望天仪阁.jpg](../20261119_courtyard_scene/实景/18_崖上回望天仪阁.jpg) | 从石门前回身，回望高耸山巅的庭院全貌，印证**庭院地势极高**。 |
| **全院俯瞰与崖舌总览** | [36_全院与崖舌总览.jpg](../20261119_courtyard_scene/实景/36_全院与崖舌总览.jpg) | 宏观大鸟瞰，石门位于突出山脊最前端的狭窄岩舌，下方便是万丈深谷。 |

* **地面铺装过渡**：从庭院内部精磨青石方砖（1.2m 规整方格），跨过 10cm 汉白玉门槛后，立刻过渡为第二章依附绝壁向外挑出的**粗粝悬空木石栈道**；
* **动线承接**：荧光冰蓝导航路径线自石门门槛正下方无缝接续亮起，顺着陡峭下行的绝壁台阶一路向下延伸。

---

## 二、 空间拓扑：自上而下的反 Z 字形绝壁顺壁下行

```
【最高处·海拔起点】 区域 ①：出关门径台（背倚第一章庭院厚重石闸，居高临下俯瞰万丈深渊）
                              │
                             [蓝线] 依壁下行挑梁石阶
                              ▼
【次高层·半空断崖】 区域 ②：断隘天堑台（深谷断裂青铜渡槽天桥，【墨规笔】画直角矩下沉接驳）
                              │
                             [蓝线] 倾斜穿过渡槽天桥步道
                              ▼
【中层·深陷岩坳】   区域 ③：狭道阻截区（山腰凹陷废墟夹道，地形掩护，残机偶战斗遭遇）
                              │
                             [蓝线] 穿过废墟栈道继续向下
                              ▼
【中低层·转角平台】 区域 ④：司衡配重台（巨型双臂青铜天平悬臂，天衡力矩砝码解谜，解锁垂直降落升降台）
                              │
                             [蓝线] 乘重载升降斗沿垂直绝壁直降百米
                              ▼
【最低处·深渊渊底】 区域 ⑤：地渊决战坪（深邃渊底开阔八角石坪，环形绝壁如铁，迎战【太仪·负衡偶】，遥望巨塔）
```

---

## 三、 生成提示词与全中文逐句注解

详见完整版设计文档：[03_第二章关卡动线与出图指南.md](../../architecture/第二章_断衡悬道/03_第二章关卡动线与出图指南.md#三-ai-概念出图提示词与全中文逐句注解-prompts--chinese-annotations)。

### 提示词 1：自上而下全景俯视等轴测动线蓝图（Top-Down Isometric Descending Level Flow Blueprint）
```text
Game level design map, top-down isometric view of a fantasy cliffside fortress, Z-shaped zigzag pathway gradually descending along a gigantic vertical precipice from top to bottom. 

Top section (Highest altitude): Zone 1 mountain entrance with a closed massive ancient Chinese stone gate embedded into the cliff, transitioning from paved stone to suspended cliffside planks. 
Upper-middle section: Zone 2 deep broken chasm with a severed suspended bronze aqueduct bridge and a holographic puzzle altar. 
Middle section: Zone 3 narrow ruined rocky corridor set inside a natural cliff crevice. 
Lower-middle section: Zone 4 mechanical balance scale terrace with giant bronze balance beams and a vertical hanging elevator shaft. 
Bottom section (Lowest abyss): Zone 5 deep abyss bottom with a massive octagonal stone battle arena surrounded by sheer cliff walls. 

A glowing neon cyan-blue navigation path line winds continuously from the top stone gate, down the cliffside steps and bridges, through the elevator, all the way to the lowest arena. 
Ancient Chinese Mohist punk architecture, bronze gears, floating suspended jade rocks, upward-flowing water droplets, slate grey cliff rocks, volumetric cyan haze, clean architectural blueprint concept art, readable spatial layout, 8k resolution, Unreal Engine 5 aesthetic. --ar 16:9
```

### 提示词 2：区域 ①~② 崖门出口与下行断堑俯瞰视角（Zone 1-2 Gate Exit & Descending Chasm View）
```text
First-person VR concept art from high elevation, standing just outside a massive closed ancient Chinese stone gate on a high cliff terrace, looking downwards across a dizzying vertical gorge. 

Behind the player: the towering mountain stone gate with white jade lintel, weathered dark bronze hinges, and square slate threshold, clearly matching the courtyard exit. 
In the immediate foreground: the stone path transitions to cantilevered bronze and stone cliffside stairs winding steeply downwards. A bright glowing neon cyan-blue guide line begins at the stone gate threshold and leads downwards along the edge. 
In the middle ground below: looking down into the deep foggy abyss, a disconnected ancient bronze aqueduct bridge is suspended mid-air between severed cliffs. Next to it stands a glowing holographic cinnabar-inscribed drawing altar waiting for activation. 
Distant background: misty abyss, distant mountain silhouettes below the clouds, inverted waterfall droplets drifting upwards into the cold sky. Cinematic depth of field, atmospheric lighting, Chinese Mohist craftsmanship, epic scale, 8k resolution. --ar 16:9
```

### 提示词 3：区域 ④~⑤ 司衡配重台与直降深渊决战坪透视（Zone 4-5 Balance Scale & Abyss Descent）
```text
Cinematic concept art, wide perspective standing on a mid-level cliffside terrace of an ancient Chinese mechanical installation, looking down into a deep subterranean canyon. 

Center foreground: Zone 4 giant Mohist bronze balance beam scale mechanism with hanging counterweight plates, engraved calibration dials, and heavy bronze chains. A bright neon cyan-blue path line leads to the central calibration wheel. 
Vertical connection: an immense open-frame bronze elevator cage suspended by massive woven copper cables, descending straight down into the deep vertical abyss along a sheer rock face. 
Deep background (Abyss floor): Zone 5 far below at the bottom of the chasm, an expansive octagonal stone battle arena illuminated by dim subterranean vents and faint cyan runic light, waiting for the elevator's arrival. 
Sheer vertical cliff walls rising up into misty clouds on both sides, industrial Mohist machinery, dramatic vertical sense of scale, volumetric fog, Unreal Engine 5 render, 8k resolution. --ar 16:9
```
