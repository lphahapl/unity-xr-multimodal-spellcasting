# 主榫 R02 独立 Unity 样板

R02 为可批注的近手精修候选。Unity 6000.3.10f1 / D3D11 首轮桌面回归通过，进程 40132 原生退出码 0。未修改正式章节、生产运行时代码、R01 样板或其提案。

打开 `D:/Temp/NarutoVR-T01Seal-20261124` 中的 `Assets/Review/WorkshopMasterTenonR02/WorkshopMasterTenonR02_Lab.unity` 并 Play。交付的 `WorkshopMasterTenonR02_UnityReview.unitypackage` 与 `Assets/Review/WorkshopMasterTenonR02` 是同一份资源。包需要本项目现有 `NarutoVR.Game.Runtime` / `NarutoVR.Game.Editor`、URP 和 Input System；不是空白 Unity 工程。

## 操作

- `1` 拿起；`2` 移到近手检查位；`3` 松手；`4` 再拿起。
- `5` 移到原槽上方，`6` 松手并尝试插入。完成后 `1` 可以重新取出。
- `R` 回独立展示位。持物时方向键、Page Up / Page Down 平移，Q / E 绕竖轴旋转；也可用左上面板。

③取用位和④中控是独立示例台，间距不表示章节距离。③展示座为尺寸代理，④复用正式 TenonConsole / ConsoleSocket 原几何；参考资源只重映射独立 GUID。

**当前只由键盘、面板和合成接口输入驱动真实 GameItemBase.Take / MoveHeld / Drop。尚未绑定 Meta/XR 抓取事件，真实头显抓取入口尚未接入或验证。** 灰色静态手模仅作 1:1 比例参照。Prefab 保留 Grip（底端上方 231 mm）、InsertionTip、刚体和碰撞数据供后续系统接入。

## 几何与碰撞的区别

可视主榫最大 120 × 120 × 350 mm，插端主体宽 106 mm，到底端上方 105 mm；肩线在 105–127 mm 过渡。952 / 320 / 112 三角形，单材质，2K PBR。

实际游戏接触使用 10 段启用的 BoxCollider：按冻结截面各高度区间取轴向外包宽，相邻等宽区间合并。551 个 Unity 导入 LOD0 顶点均被覆盖，浮点容差 20 μm。盒体在圆角外侧略保守，不声称精确复制曲面。

另有一个禁用的 120 × 350 × 120 mm 全包 BoxCollider，只供 GameItemBase.stowEnvelope 尺寸测量与独立保守路径检查。它始终禁用，**不是实际接触体**。落台、松手与插接全程检查十段接触体仍启用。

原槽当前网格轴向孔宽约 153 mm，原 Body + Socket LOD0 共 6056 个三角面。样板底端落座 y=0.9239401 m，上口参照 y=1.028 m，插深约 104.06 mm；距台身最高面保留 4 mm。此为样板装配值，不是新定稿章节数值。120 mm 肩部也小于 153 mm 孔，**肩不是跨孔物理挡停，落座由 SnapZone 终点限定**。

106 mm 方插端的 45° 对角约 149.9 mm，可小于当前孔宽。45° 拒绝来自方位/准线规则，不宣称实际薄端必碰壁。检查分别记录实际十段接触体和 120 mm 保守量规；两者不混写。

## 本次结果

原 GameItemBase 拿取、移动、释放后物理落台、再次拿取；原 SnapZone 的所有权、插值和完成事件被实际执行。四个正交方位加一次重插共五次完成；重复请求不重发；途中不能抢取；完成后可重新拿出。主榫不进通用物品注册目录，现有五个 ToolBeltSlot 按实际尺寸拒收；专用槽拒绝外物、持物、横置、30 mm 偏位及不合方位输入。

正确路径逐段预检和播放逐帧均通过：实际接触体与保守量规最大检测穿透均为 0。正控制分别为：

- 实际 R02 45° 在该座位检测为 0，符合薄端可通过的事实。
- 120 mm 量规 45° 检出约 8.353 mm 干涉，仅属于量规。
- 实际接触体过深检出约 4.975 mm，偏心 50 mm 检出约 26.500 mm。

## 查看成果

`Images` 包含中性近手正45°、侧背、侧光、进入端近看、握位/进入端远看、原槽与坐实图。`02_NearHand_Front45_1to1.png` 和 `02B_R01_Reference_SameCamera_Neutral.png` 是相同机位、相同光照的 R02/R01 对照。

`Insertion_ActualUnity_R02.mp4` 由实际回归 75 帧生成，30 fps、2.5 秒，无音频。没有重复 R01 的双销提案，未接清场或章节系统。艺术定稿、真人握持和 Quest 舒适度仍待后续验收。

证据位于 `D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-r02-20261125/unity/`：`result.txt`、进程起止/退出码、`timeline.tsv`、`collision-oracle-diagnostics.txt`、`physical-envelope-coverage.txt`、`captured-input-sha256.json`、`package-audit.json`、`final-media-freeze.json`。R01 的 183 项基线文件保持不变，捕获用 45 项输入完成核对。
