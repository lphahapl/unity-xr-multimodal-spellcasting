# 汉白玉校准主榫 · 清圭 R01

2026-11-25，用户依据 C4 明确授权：“就这样开始建模 先做三视图 要求建模细致 有玉石光泽，透光 青铜和金线有质感”。本目录为新方向独立制作；旧 R01–R03 和概念图全部保留。

## 本轮依据与尺寸

- [获准 C4 图](../BronzeConceptC4/Tenon_C4.png)，SHA256 `1255d86f12df547e3d03501dc3ba843c5ebdfd7876fadb1c22ca85ece9a040cd`。
- [造型三视参考](Reference/C4_ThreeView_DesignGuide.png)是原生图片工具根据 C4 编辑的造型稿，侧面底端比例不能当量规。准确三视另由同一实际模型导出。
- [制作契约](design-contract.json)：全长 350 mm，最大外廓不超过 120 mm；原方插端 106×106×105 mm、底端中心原点和现有插接方向逻辑保持。握体按新稿重建，顶扣和肩销须重新测量。
- 温白玉的细微云絮、抛磨曲面和厚薄透光，暗铜的氧化与接触磨痕，金线的嵌槽与金属反光分别制作。清圭仅是造型方案名，不改正式物品名称。

## 制作范围与阶段

2026-11-26 首版实模、烘焙、三视及独立 Unity 桌面验证完成。C4 外观和开始建模已获用户授权；本次实际模型仍可批注，尚未推广到正式章节。

- [同尺度实际三视](Model/Previews/11_ThreeView_SameScale.png)：同一 LOD0 的正、右、背，非 AI 参考图。高模 1,207,404 三角形；LOD0 / 1 / 2 为 4,916 / 1,800 / 600，单材质，2K 贴图。九张交付图含原始与不同打包格式，并非同时采样九图。
- [可编辑源与模型说明](Model/README.md)，[Blender 源](Model/QingGuiR01_Source.blend)，[游戏 FBX](Model/QingGui_LODs.fbx)，[普通 PBR GLB](Model/QingGuiR01.glb)。真实通孔、金槽、铜脊层次与圆融肩线已制作；源网格与 FBX 冷导入检查通过。
- 外廓约 111×106×350 mm；230 mm 高处握位宽约 85 mm、厚约 46 mm。保留方插端与原点，原 141 个保护顶点最大偏差约 1.013e-9 m。新顶扣、肩部双销及③取用台适配另验。
- [Unity 近手正面](UnityReview/FacingReview/04_C4_Front45_NearHand.png)、[④原孔插到位](UnityReview/FacingReview/07_C4_Front45_SeatedPose.png)、[Unity 样板与证据](UnityReview/README.md)。正面是源 Blender −Y / 导入 Unity +Z，右侧是源 +X / Unity −X；不得把背面或照明方向误标为正面。

| 目录 | 用途 |
|---|---|
| `Reference/` | 原生造型三视与实际提示词 |
| `Model/` | 可编辑 Blender 源、模型、纹理、实模视图与模型检查 |
| `UnityReview/` | 独立 Unity 样板、实时材质及验证证据 |
| [本轮日志](../../../../Logs/workshop-master-tenon-qinggui-r01-20261125/) | 外部事实核查、父代理复核及阶段记录 |

运行时方案为不透明 URP PBR 加厚度控制的背光近似，不等同 Cycles 的体积/次表面光传输。Thickness 按真实网格对向射线测量，线性通道 0..1 对应 0..120 mm；JadeMask 的玉为 1、铜金为 0。关灯、金属及外物遮挡必须单独检查，不能靠自发光呈现玉。

Unity 6000.3.10f1 / URP 17.3.0 / D3D11 独立工程对本版最终冻结网格实跑：四向插入与取回重插共五次成功，错误角度、偏置及持握未到位拒绝；15 个实际接触段覆盖 2,848 个导入顶点，逐帧最大原槽穿入为 0，另测 120 mm 保守量规也为 0。原槽六个深度的双轴实际射线净宽约 153 mm；这是当前网格实测值，不覆盖最早约 146 mm 的估计。握持锚点按 Unity 实际截面改至 `(1.49998,230,-0.20707) mm`，单独 Take / MoveHeld 复核通过。

最终模型受光时有克制的暖色透光增量，关闭主灯或外物遮挡时增量为 0；实际全金属像素最大残余约 2.98e-6，属于浮点近零，不写为数学全零。2 / 8 / 25 mm 控制样本随厚度增加响应递减。保留投射/接收阴影，未使用 emission。Android Vulkan shader 编译通过，但未构建或运行 Quest APK。Thickness 的 6 个未命中保守回退和超过 120 mm 的斜向光程截断见 Model 报告；2K 法线的极近采样限制仍存在。

[审稿页](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r01.html)并列实际模型、Unity、Cycles 与原 C4；[旋转模型](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-qinggui-r01)只显示普通 PBR。

本轮不替换正式章节场景，③取用台由独立任务制作，④中控接口沿用。旧 R03 与正式工坊的历史桌面通过结果不作为本模型验收。本次测试使用桌面/API 合成输入，独立样板尚未接通 Meta/XR 抓取事件；Quest 透光观感、真人握持与性能仍需真机测试。
