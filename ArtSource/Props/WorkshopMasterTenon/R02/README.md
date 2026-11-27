# 工坊校准主榫 R02

R02沿用已核主榫用途与插入底端原点，独立保存，R01模型、贴图及批注不覆盖。父代理已接受本轮中性45°和侧光画面进入最终检查；这是待用户批注版本，并非审美定稿。模型/贴图已冻结，详见[冻结哈希](../../../../Logs/workshop-master-tenon-r02-20261125/model/candidate-ready.json)。本模型代理未改Unity、正式Assets、共享页或交接文件。

## 从 R01 到 R02

- 玉料改为物理坐标下等向多尺度云絮和抛磨面，去掉沿长轴拉伸的连续竖流痕。保留温白玉底色和克制细微色差。
- 两排孤立方圈改为环绕上段的窄连续云雷纹带。贯穿准线连接10mm节拍的长短刻度，准线独立加宽/加深并保留微小槽色差，使中性45°下能读成完整标尺。
- 两道深铜箍移除，仅在插入端保留约1.5mm暗铜细嵌饰。主视觉仍由玉料、纹带和刻度构成。
- 重做短方插端、肩线、握持段与柔化顶冠。父复核首轮指出的分层高光通过单调C1截面曲线、低模对应解析法线和完整重新烘焙处理；保留原灯光进行对照。

## 尺寸与配合边界

完整外廓120×120×350mm；Blender +Z、Unity +Y竖直。底端中心为原点，原插接座标不变。

插端最大106×106mm，至z=105mm；肩部过渡z=105–127mm；握持段最大120mm，顶冠保持在该外廓内。以旧入座底面y=0.923940m计算，肩起点y=1.028940m，较父代理给出的孔口y=1.028m高0.94mm。肩是视觉转折，不能跨过更宽方孔形成机械止挡。

**原名义方孔约146mm，父代理本轮测得现有网格方孔约153mm。** `model-report.json` 的兼容字段名 `nominal_socket_metres: 0.153` 实际表示本轮采用的网格测量值，不能读成原稿给出153mm。该孔未由模型代理缩改；本目录没有独立证明真实孔壁和入槽接触，相关实测由父代理与Unity代理完成。

逐高度宽度、圆角半径及建议内包分段碰撞盒见[model-report.json](model-report.json)。建议盒仅为独立测试起点，不能替代真实抓握/碰撞验收；分段顺序为插端、肩、握体、顶冠。

## 文件与技术检查

| 文件/对象 | 内容 |
|---|---|
| `MasterTenon_R02.blend` | 可编辑高模、三档低模、源材质和中性灯光 |
| `MasterTenon_LOD0.fbx` | 952三角，1材质、1 UV，120×120×350mm |
| `MasterTenon_LODs.fbx` | LOD0/1/2为952/320/112三角，统一底端原点 |
| `MasterTenon_R02.glb` | LOD0和内嵌PBR贴图，独立预览用 |
| `Textures/` | 2K BaseColor、Normal、AO、Roughness、Metallic、ORM、MetallicSmoothness |
| `Previews/01–10` | 45°、侧背、三视、插接肩线/刻度、侧光、端嵌饰及同机位高低模真实渲染 |
| `Previews/QA_*`、`11/12_CleanWire*` | 技能四视图与不带Wireframe修改器尖角延长线的精确边线预览 |

高模361,856三角，刻线、纹带与接触边为真实几何凹刻；通过Cycles selected-to-active完整重烘焙2K贴图，cage extrusion 2mm、最大射线6mm、16px扩边。低模三角化和解析法线在烘焙前固定。BaseColor为sRGB，其余线性；Normal为OpenGL +Y切线空间。ORM=AO/Roughness/Metallic，Unity MetallicSmoothness的R=Metallic、A=1−Roughness。

高模和三档低模QA均通过、无错误或告警。FBX与GLB空场景冷回读均952三角、原点0、尺寸一致；FBX无告警，GLB有72个同位置顶点，为UV/切线拆分，接受并记录。检查日志在[专属模型日志](../../../../Logs/workshop-master-tenon-r02-20261125/model)。

```powershell
& 'D:/Blender/Blender 5.2/blender.exe' -b --factory-startup --python-exit-code 1 --python 'ArtSource/Props/WorkshopMasterTenon/R02/build_tenon_r02.py'
& 'D:/Blender/Blender 5.2/blender.exe' -b --factory-startup --python-exit-code 1 --python 'ArtSource/Props/WorkshopMasterTenon/R02/verify_exports.py'
```

## A1 参考的使用范围

A1用于玉料、窄纹带、长短刻度及端部层次的方向参考，不从透视图像素倒算物理尺寸。R02插端按本轮105mm长和106mm方截面实施；A1中的底座、双销和机械锁定没有并入主榫。双销/清场解锁为父会话另行协调的方案。

本版游戏材质是单套不透明PBR，源高模的轻微次表面散射不代表Quest运行时真实玉石透光。用户审美、头显近手观看、真实拿取/插接及正式场景材质环境尚需各自验收。
