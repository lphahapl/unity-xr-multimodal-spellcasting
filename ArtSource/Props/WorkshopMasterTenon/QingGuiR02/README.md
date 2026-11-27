# 汉白玉校准主榫 · 清圭 R02B

本轮当前交付为 **RevisionB**。依据用户“修过渡、金丝在表面、玉石要透明光泽”及工作台铜玉像一体、铜面不平的批注完成。原 R02 与 R01 冻结稿、全部历史批注保持。

- 铜玉过渡：实际约 0.5 mm 薄铜包边、窄界与细接缝，收掉孔下铜尾宽幅淡入；铜面平顺，保留克制细纹。
- 金丝：独立实体圆丝、收束端头、贴面接触；Unity 使用金属 PBR 与几何高光过滤。
- 玉料：实测厚度驱动的局部半透、浅层密度与抛光反射，金铜保持不透明。它是实时近似，不是物理体积光追。

[审稿、同光对比与实拍](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r02.html) · [旋转模型](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-qinggui-r02b)

[模型源文件与已知限制](Model/RevisionB/README.md) · [模型冻结清单](Model/RevisionB/frozen-manifest.json) · [Unity 场景、Prefab、材质及验证](UnityReview/README.md) · [独立导入包](UnityReview/RevisionB/QingGuiR02B_UnityReview.unitypackage)

源 LOD 为 15,834 / 6,650 / 3,444 三角。局部玉层重复提交 Body，实际前向为 24,672 / 8,650 / 4,144，尚不计阴影和额外相机。独立管线启用透明接收阴影；直接放入当前生产 Mobile Renderer 不能宣称效果一致。

Unity 桌面实际 API 拿放、原孔四向与重插、单根三档 LOD、材质透射及外挡控制、Android Vulkan 着色器编译已通过。正式章节、③取用台/双销、Meta/XR 抓取入口与 Quest 双眼/性能仍需独立接入和实测；本轮没有替换它们。实际观感待用户批注。

全量文件/哈希、用户原图和技术证据见 [本轮日志](../../../../Logs/workshop-master-tenon-qinggui-r02-20261126/)。三份模型交火交接由父代理追加记录，本轮未改六印声音或字影。
