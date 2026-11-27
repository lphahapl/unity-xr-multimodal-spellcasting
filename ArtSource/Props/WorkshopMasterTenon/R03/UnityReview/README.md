# 主榫 R03 独立 Unity 样板

R03 保留方插端及底端以上 0–127 mm 的肩部几何，127 mm 以上改为减薄握段、圆润上肩和带实际凹面的拇指区。Unity 6000.3.10f1 / D3D11 首轮桌面验收通过，PID 12552，2026-11-25 18:39:14—18:43:29 +08:00，原生退出码 0。此为可批注候选，未改正式章节、生产脚本或 R01/R02。

打开 `D:/Temp/NarutoVR-T01Seal-20261124` 中 `Assets/Review/WorkshopMasterTenonR03/WorkshopMasterTenonR03_Lab.unity`，进入 Play。交付包和 `Assets/Review/WorkshopMasterTenonR03` 原文件一致。包依赖本项目现有 `NarutoVR.Game.Runtime`、`NarutoVR.Game.Editor`、URP、Input System，不是空项目即用包。

## 拿取与插接

- `1` 拿起；`2` 近手观察；`3` 松手；`4` 再拿起。
- `5` 移到原槽上方；`6` 松手并尝试插入；插入后按 `1` 重新取出。
- `R` 回展示位；持物时方向键、Page Up/Down 平移，Q/E 转向；也可用左上面板。

③取用台与④实际中控分开摆放，样板间距不表示章节距离。④复用原 TenonConsole / ConsoleSocket 几何，仅重映射独立 GUID。灰手是静态 1:1 比例参照。

**本样板通过桌面键盘、面板及合成接口输入驱动真实 GameItemBase.Take / MoveHeld / Drop 与 SnapZone。尚未绑定或验证 Meta/XR 真实抓取入口。** 主榫保留 Grip（高 231 mm）、InsertionTip、刚体和碰撞配置，未加入通用杂物注册目录。它和另一个负责人制作的章节接线切片不是同一交付。

## 尺寸、原孔和碰撞

最大外包仍为 120 × 120 × 350 mm，底端原点，Unity +Y 向上。模型 1474 / 560 / 210 三角形，单材质、2K PBR；拇指凹深约 2 mm 是低模几何。BaseColor 使用 sRGB，法线、AO、MetallicSmoothness 使用线性导入，保留正常 PBR，没有自发光玉或禁用法线。

Unity 导入网格实测：底端以上 5、50、104、105、110、120、127 mm 七个截面的边界与 R02 差异为 0；230 mm 握段截面约 104 × 74.4 mm，R02 为 120 × 120 mm。不能把“下部保持”扩大为 0–140 mm。完整测量见 `r02-r03-measured-sections.tsv`。

拿插样板采用 17 段启用的轴向外包 BoxCollider，分别按宽、深取区间外包；824 个导入 LOD0 顶点全部被覆盖，容差 20 μm。圆角和拇指凹面在盒体中被保守填平。根部另有禁用的 120 × 350 × 120 mm 量规，只供 stowEnvelope 尺寸和路径检查，始终不是物理接触体。

原槽当前网格孔宽约 153 mm，原 Body 与 Socket 共 6056 三角面。底端落座 y=0.9239401 m，上口参照 y=1.028 m，样板插深 104.06 mm，台身余量 4 mm。**120 mm 肩部不跨越 153 mm 孔口，不能作为孔口止挡；落座由 SnapZone 终点限定。** 106 mm 方端在 45° 时对角约 149.9 mm；45° 拒绝属于方位规则，不能说实际薄端必然碰壁。

原拿取、移动、松手物理落台、重拿、四个正交方位及重插均执行。完成共 5 次，无额外事件；拒绝持有、横置、30 mm 偏心、错误方位和外物；五个现有 ToolBeltSlot 按真实尺寸拒收。正确路径逐段预检和播放逐帧实际接触体/保守量规均为 0 穿透。正控制：120 mm 量规 45° 检出 8.353 mm；实际接触体过深检出 4.975 mm、偏心 50 mm 检出 26.500 mm。

## 顶扣兼容与新肩销提案

`Proposal/CurrentInstalledTopLatch.prefab` 和 `Proposal/ShoulderPinProposal.prefab` 为两套独立可视夹具，可分别拖入独立场景并在 Play 面板切换闭合/退开。不得把新肩销称为正式系统已有实现。夹具没有接敌人、清场、Take 锁或章节事件。

现行 `WorkshopTenonAssembly.cs` 实际是沿 ±X 的顶部压挡，销中心 y=356 mm、下沿 y=350 mm。两销 48 × 12 × 30 mm，闭合 X=±59.5 mm，退开 X=±117 mm；竖导座同步横移，行程保持 57.5 mm。原位几何兼容 R03，未调整正式 Assembly。闭态抬升 10 mm 的网格正控制检出约 7 mm 相交，刚体上抬被挡住。

新增肩销沿减薄轴 ±Z，属于新配合候选，未定义为原稿“左右”。48 mm 为行程轴长度、12 mm 高、30 mm 横宽。销底高 140 mm；闭合中心 `(0,146,±77.5)` mm，内缘 `|Z|=53.5` mm；真实网格该高度带最大半深 52.5 mm，初始间隙实测 1 mm。退开中心 `(0,146,±135)` mm，仍横移 57.5 mm。接触面是 127 mm 以上收拢的肩上斜面，不是原孔上的止挡。导座最高 158 mm，低于握点 231 mm。

肩销抬升 10 mm 正控制检出约 4.131 mm 相交；固定 X/Z、旋转并持续请求向上 0.06 m/s 的独立刚体，在约 1.730 mm 抬升后停止。退开后两套夹具的 401 点、400 mm 完整取出路径均无相交；独立刚体均实际取出约 419.903 mm。这里只验证竖向受限运动，未验真人扭动、载荷强度或耐久。

夹具使用从冻结 LOD0 实际网格切片生成的分段凸包，拇指凹面仍被凸包保守填平。**它与拿插样板的 17 段外包盒是不同的接触近似。普通外包盒会占用肩销的精配间隙，不能宣称原 GameItemBase Prefab 已直接接好新夹具。** 后续正式接入需要决定接触体和锁定入口，本轮不修改系统。

## 图片、视频与复现

`Images` 保留中性近手、侧背、侧光、进入端、握位近看、原槽与实插图。`12/13` 为 R03/R02 同机位正交正面对照，`14/15` 为同机位侧面厚度对照；`17/18` 为原顶扣闭/退，`19/20/21` 为新肩销闭合、受阻抬升、退开。`Insertion_ActualUnity_R03.mp4` 是实跑 75 帧，30 fps、2.5 秒，无音频。

仅在独占隔离工程空闲时运行 `Logs/workshop-master-tenon-r03-20261125/unity/run-acceptance.ps1` 可复现；它会重建 R03 Review 资源并覆盖本轮证据，使用前先复制整份输出留档。关键结果为 `result.txt`、`process-start.json`、`process-exit.json`、`timeline.tsv`、`clamp-compatibility.txt`、`collision-oracle-diagnostics.txt`、`captured-input-sha256.json`、`package-audit.json` 和 `final-media-freeze.json`。

隔离编辑器仍有 OpenXR 未初始化、SearchDatabase 索引异常等宿主日志，不称 Console 零错误。验收断言与原生退出码通过；头显抓取、人体工学及用户审美尚未通过本轮验证。
