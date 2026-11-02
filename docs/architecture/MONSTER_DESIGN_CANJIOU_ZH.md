# 《考工律度》基础人形小怪【残机偶】模型与机制设计方案

> **文件标识**：`MONSTER_DESIGN_CANJIOU_ZH.md`  
> **版本**：`V1.0` | 2026-11-02  
> **所属模块**：战斗系统 / 怪物生态 / 3D 美术资产规范  
> **运行平台**：Meta Quest 一体机 (VR 72/90fps) / Unity 6000.3.10f1 URP  
> **关联文档**：[`docs/world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md`](../world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md) · [`docs/world/考工律度_世界观设定白皮书.md`](../world/考工律度_世界观设定白皮书.md) · [`docs/architecture/LINEAR_RPG_DESIGN_ZH.md`](LINEAR_RPG_DESIGN_ZH.md)

---

## 1. 世界观渊源与生态定位

### 1.1 前世与异化：失控的工奴
在天地大脱扣之前，**【残机偶】（Can Ji Ou）**曾是天工司量天废阁各处外殿与神道上最常见的**初阶机关傀儡**：
- **原本职责**：负责运送石料、修筑神道地砖、夯土、平整水准以及测量石坊尺寸；
- **动力来源**：体内安装高密青铜螺旋发条，通过精密擒纵机构（Escapement）平稳释放动能；
- **失度异化**：天地大钟脱扣散架后，虚空失度乱流渗透机体，头部发条调速器的宝石轴眼与擒纵摆脱位散落，阻尼彻底归零。发条失去阻尼疯狂自旋倒转，动力狂乱输出。傀儡已丧失全部工役辨识协议，将一切踏入神道领地的血肉活人（勘律使）判定为“失度杂物”，陷入永无止境的机械狂乱清扫循环。

### 1.2 动作与视听特征：老旧机械的抽搐节律
- **机械抽搐（Clockwork Stutter）**：非平滑行走的丧尸或敏捷人形刺客，它的步态带有强烈的发条齿轮顿挫感。走三步停顿半拍、关节偶尔出现几毫秒的金属高频卡壳微震；
- **声音辨识（Audio Cue）**：
  - 巡逻时：体内传出类似老式发条大钟上得过满时沉闷急促的“咔哒、咔哒、咔嚓”齿轮啮合杂音；
  - 攻击蓄力时：头部外露发条由于超负荷运转发出刺耳的**机簧高频尖啸（Spring Whine）**与齿轮打滑火星，为玩家在 VR 空间中提供极其明确的听觉预警。

---

## 2. 3D 模型资产美术规范 (Asset Specifications)

### 2.1 比例、形体与穿戴结构
残机偶采用标准人形双足（Humanoid）结构，整体比例干练冷峻，充满先秦金石工官气息：
- **身高**：1.80 米（与主角勘律使视平线相当，避免巨物仰视眩晕，适合 VR 空间内正视对决）；
- **体型包络**：肩宽约 50cm，胸厚 35cm，步幅约 60cm；
- **骨架与核心结构**：
  - **躯干骨架**：哑光深褐色**青冈栎木榫卯骨骼**，关节外露青铜转轴与实心销钉；
  - **外部甲胄**：胸部、双肩、小臂与腰胯紧密铆接冷铸**哑光青铜护甲片**（表面带少量自然氧化深绿铜锈与切削磨损刮痕）；
  - **头部（识别核心）**：青铜冷铸的椭圆无面仪容，面部仅有一只**可变光圈的青铜游标单目镜**；后脑勺部位完全镂空敞开，内部装有一枚**外露的高速自旋螺旋发条轮**，中心嵌有散发微弱红光的【熔核朱砂】能量晶核；
  - **右手武器【切石规刃】**：长约 65cm 的厚背青铜方头短砍刃，刃背刻有秦汉分寸刻度与云雷防滑纹，原为开山凿石工具；
  - **左手副手【游标卡尺爪】**：青铜滑轨卡尺机构，两根带锯齿的活动卡爪可开合咬合，原本用于测量石料外径。

### 2.2 Quest VR 性能与拓扑指标

| 规范项目 | 目标参数 | 设计原由与 VR 优化边界 |
|---|---|---|
| **三角面数 (Triangle Count)** | **3,200 ~ 4,200 Tris** | 确保在 Meta Quest 3/Quest 2 上同屏出现 3~4 只时，怪物总面数控制在 1.5 万以内，维持单眼 72/90fps。 |
| **骨骼数量 (Bone Count)** | **31 根标准骨骼** | 标准 Unity Humanoid 双足骨骼（28 根）+ 头部发条自转骨（1 根）+ 左手卡尺爪张合骨（2 根）。 |
| **顶点权重限制** | **Max 4 Bones per Vertex** | 严格限制 Adreno GPU 移动端硬件上限，无额外权重开销。 |
| **材质球与 Draw Call** | **1 个 Material (Single Draw Call)** | 全身网格合并为单个材质球，避免多材质导致移动端 Draw Call 激增。 |
| **贴图分辨率与通道** | **2048×2048 (PBR 合图)** | ASTC 格式压缩。Albedo(RGB) + Normal(RGB) + Metallic/Roughness/Occlusion(合图单通道)，去光污染。 |
| **自发光通道 (Emission)** | **仅限后脑发条中心与单目镜** | 采用熔核朱砂色（`#E65100`），受击或蓄力时通过材质参数微弱呼吸发光，严禁全屏泛光。 |

---

## 3. 战斗行为树与机制设计 (Combat Mechanics)

残机偶的设计使命是**作为新手期教导并巩固玩家“VR 空间位移与施法节奏”的合格陪练**。它与玩家的核心机制（空手快推后撤、定规障格挡、荧惑火核发射）形成严丝合缝的博弈闭环。

```
                    ┌─────────────────────────┐
                    │      【巡游与索敌】      │
                    │  齿轮咔哒声，0.9m/s步进  │
                    └────────────┬────────────┘
                                 │ 距离 < 1.8 米
                                 ▼
                    ┌─────────────────────────┐
                    │   【招式：规刃疾劈】     │
                    │  发条尖啸，红带蓄力1.2s  │
                    └──────┬───────────┬──────┘
             闪避成功      │           │ 未能闪避
        (空手快推后撤0.8m) │           │ (或定规障格挡)
                           ▼           ▼
        ┌────────────────────────┐   ┌────────────────────────┐
        │   【破绽：发条卡死】   │   │   【命中：破盾/受创】   │
        │ 刀深陷石缝，瘫痪3.5秒  │   │ 造成35点伤害，后退恢复 │
        │ 后脑发条暴露，200%暴击 │   └────────────────────────┘
        └───────────┬────────────┘
                    │ 玩家完成结印并送出火核
                    ▼
        ┌────────────────────────┐
        │   【击破：发条崩散】   │
        │ 齿轮暴碎散落，机体倒地 │
        └────────────────────────┘
```

### 3.1 基础行为状态机

#### 1. 巡游与步进态 (Clockwork Advance)
- **移动速度**：平缓的 0.85 ~ 1.0 米/秒，给戴头显的玩家留出充裕的心理安全距离；
- **转向速度**：角速度限制在 90°/秒（带有步进电机的离散转角感），不会突然瞬移转向；
- **索敌范围**：以自身前方 120° 扇形、半径 8 米为视线范围；或玩家施法发声（语音口令）时直接朝向声源方向。

#### 2. 近战主招式【规刃疾劈】(Caliper Cleave) —— 检验【快推滑退】
- **触发距离**：与玩家距离 ≤ 1.6 米；
- **第 1 阶段：预警蓄力（Telegraph 1.2 秒）**：
  - 残机偶双腿锁死站定，头部发条齿轮由于超频摩擦爆发出剧烈金白火星与高频啸叫；
  - 右手高举切石规刃，地面瞬间投射一条长 1.8 米、宽 0.8 米的高对比度【应力赤红】攻击危险区；
- **第 2 阶段：下劈重斩（Strike 0.15 秒）**：
  - 规刃凶猛向下劈落，正前方判定范围内造成 **35 点物理伤害**；
  - 若玩家提前张开【定规障】，定规障吸收此伤害，表面产生剧烈金色波纹裂痕，耐久扣除 35/100；
- **核心破解操作**：
  - **空手快推后退**：玩家双手不持火核，右手在胸前快速向前平推，身形借刚性空气激波向后平滑后退 0.8 米（耗时 0.25 秒），正好完全脱离 1.8 米劈砍范围，刀尖贴鼻尖砸空！

#### 3. 核心机制弱点【发条卡死 / 齿轮咬死】(Overheat Jam) —— 施法反击大窗口
- **触发条件**：规刃劈空砍入石板地面，或者击中定规障被刚性反冲弹开；
- **机体状态**：
  - 沉重的青铜刃尖深陷青灰石板缝隙，发条反作用力导致机体剧烈抽搐前倾；
  - **整整瘫痪硬直 3.5 秒**！头部外露发条喷涌出一缕白色泄压水汽，停转锁死；
  - 此时机偶后脑与后颈处的【熔核朱砂发条核】完全暴露在玩家视线正中，并带有呼吸式高亮红光诱导；
- **反击大循环**：
  - 玩家在 3.5 秒充裕硬直时间内，从容进行【定势 ➔ 结印 ➔ 印成 ➔ 运法】（一套熟练操作约 1.5~2.0 秒）；
  - 头显准星牢牢锁定其后脑发条核（视向定轨），挥手解缚送出荧惑火核；
  - 火核击中发条核判定为**暴击（Critical Hit, 200% 伤害）**，发条彻底暴散崩飞，残机偶解体散架！

#### 4. 远程变种机制【卡尺掷爪】(Caliper Projectile) —— 针对远距离拉扯
- **触发条件**：若玩家处于 3.5 ~ 7.0 米距离持续拉扯超过 4 秒；
- **行为**：残机偶左手卡尺爪弹出，向玩家水平直线飞出（弹道速度 6.0 米/秒，尾部连着青铜软链）；
- **应对**：弹道速度适中，玩家可通过左食指走位避开，或张开【定规障】将其反弹砸回，反弹还会导致机偶自身踉跄后退 1 秒。

---

## 4. 动画状态机与剪辑表 (Animation Clips)

所有动画均强调“机械力学惯性”与“无肌肉缓冲感”：

| 动画剪辑名 | 动画长度 | 循环/单次 | 动作要领与力学特征 |
|---|---|---|---|
| `Anim_Idle_Stutter` | 1.8s | 循环 | 原地微弱呼吸站姿，头部每隔 0.6 秒有规律地轻微抽动 5 度，发条常态旋转。 |
| `Anim_Walk_Clockwork` | 1.2s | 循环 | 双腿离散机械迈步，脚掌砸地声清脆，躯干伴随左右微摆，机械发条咔哒作响。 |
| `Anim_Attack_Cleave_Windup` | 1.2s | 单次 | 双手持刃高举过头，后背发条剧震，身体微微向后仰起蓄能（预警阶段）。 |
| `Anim_Attack_Cleave_Hit` | 0.2s | 单次 | 规刃如千斤断头台骤然砸地，双臂直挺挺砸向地面。 |
| `Anim_Attack_Cleave_Jam` | 3.5s | 单次 | 刃卡在石板里拔不出，双臂拼命拉扯但机体抖动发热，头顶冒白气（弱点硬直暴露）。 |
| `Anim_Hurt_Front` | 0.4s | 单次 | 正面受击，机体机械后挫，齿轮摩擦出火星。 |
| `Anim_Hurt_Critical_Head` | 0.6s | 单次 | 后脑核心被击中，发条轮猛烈倒转，金属碎渣飞溅。 |
| `Anim_Death_Disassemble` | 1.5s | 单次 | 全身发条彻底脱扣，木质榫卯骨架崩脱，青铜甲片哗啦散落一地。 |

---

## 5. 概念图绘制与8面正交建模提示词全案 (8-View Turnaround Prompts)

供后续 AI 概念美术或 3D 建模师进行模型资产设计参考，完整 8 面详细英文提示词已独立归档至：
[docs/visuals/20261102_canjiou_enemy_schematic/01_canjiou_8views_prompts.txt](../visuals/20261102_canjiou_enemy_schematic/01_canjiou_8views_prompts.txt)

涵盖 8 个标准三维建模参考视角：
1. **正视图 (Front View)**：中性 A-Pose，展示胸腹青铜甲片与青冈栎木榫卯骨架、黄铜游标单目镜；
2. **背视图 (Back View)**：展示核心弱点——完全镂空的后脑勺、外露高速螺旋主发条与微弱自发光熔核朱砂晶核；
3. **左侧视图 (Left Profile)**：90° 侧视，展现左臂游标卡尺爪滑动导轨与齿状咬合爪；
4. **右侧视图 (Right Profile)**：90° 侧视，展现右臂切石规刃的厚背剖面与刃背分寸刻度；
5. **前左45°视图 (Front-Left 3/4 Oblique)**：展现胸甲立体厚度与卡尺爪机械转轴；
6. **前右45°视图 (Front-Right 3/4 Oblique)**：展现切石规刃立体持握与右肩甲包覆；
7. **后左45°视图 (Back-Left 3/4 Oblique)**：展现后脑弱点发条腔体的立体纵深与发光能量核；
8. **顶视图 (Top-Down Plan View)**：垂直正俯视，展现头部发条腔、双肩青铜护肩弧度与武器水平包络。

```text
Title: 《考工律度》先秦工官机械傀儡【残机偶】3D建模标准参考图
Style: Production 3D game character concept turnaround sheet, stylized ancient Chinese mechanical automaton, Qin-Han dynasty工官 aesthetic, realistic PBR materials, studio diffuse lighting.

Subject Details:
- A humanoid ancient Chinese mechanical clockwork automaton called "残机偶" (Deranged Clockwork Automaton). Height: 1.80m, balanced athletic bipedal proportions.
- Skeleton & Framework: Sturdy dark oiled oak timber (青冈栎木) mortise-and-tenon armature, visible bronze pivot pins and joint hinges.
- Armor & Plating: Cold-cast matte patinated ancient bronze armor plates (chest cuirass, shoulder guards, forearm bracers, waist tassets) with subtle verdigris patina and industrial chisel scrapes. NO excessive ornaments, purely utilitarian ancient engineering aesthetic.
- Head & Weak Point: Cylindrical matte bronze faceless mask with a single central brass vernier aperture monocle (caliper eye). The back of the head/occiput is completely openwork, revealing an exposed, rapidly spinning clockwork spiral spring mechanism with a faintly glowing cinnabar orange core (#E65100).
- Right Weapon: A 65cm heavy-backed squared-tip bronze cutting ruler-blade (切石规刃) with engraved caliper measurement marks along the spine.
- Left Arm: A bronze mechanical vernier caliper claw (游标卡尺爪) with dual interlocking teeth.
- Background: Pure solid neutral medium grey (#808080), flat, zero shadows, no floor.
- Views: Clean orthographic front view, side profile, and rear view showing back-head spring weak point clearly.
- STRICT NEGATIVE: NO text, NO labels, NO annotations, NO arrows, NO dimension numbers, NO watermarks, NO steampunk brass pipes, NO cyberpunk glowing neon lights.
```

---

## 6. 与现有代码的接线与集成方案

在当前工程 `Assets/SealCombat/Runtime/Rpg/` 体系中，残机偶的落地无须推翻既有架构，而是作为 `RpgTrainingHazard` 的完全进化版：

1. **替换目标**：
   - 将当前关卡中仅有红圈代码逻辑的 `RpgTrainingHazard.cs`，升级为承载实际 3D 网格、碰撞盒（Collider）与弱点判定区域（Weakpoint Box）的 `RpgEnemyController.cs`；
2. **伤害与反冲逻辑接入**：
   - 攻击判定帧调用现有的 `RpgPlayerState.TakeDamage(damage, sourceId)`；
   - 玩家受击后通过既有的 `SkillEffects.AbsorbDamage()` 计算定规障耐久扣减与障破碎裂；
3. **后撤避险判定**：
   - 结合现有的 `RpgLocomotionController.Encounter.cs`，玩家在蓄力 1.2 秒内执行快推滑退（`ExecuteQuickRetreat()`），即可自动离开有效碰撞范围；
4. **火核弹道命中与受创**：
   - 怪物身上的后脑弱点碰撞体挂载 `IProjectileReceiver`，当 `SkillProjectile`（火核）以 9m/s 碰撞时，检测命中点是否在后脑方位，若为后方则触发 200% 暴击并触发 `Anim_Death_Disassemble`。
