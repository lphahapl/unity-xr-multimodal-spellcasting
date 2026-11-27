清圭 R03 Revision A · Unity 首样交付（2026-11-26）

状态：实际 Unity 首拍与必要快检完成，供父代理和用户批注；不是最终优化成品。
实际输入：Model/RevisionA，FBX 3052e95f06ac67cfd37a6106e06f7a472b74cbae882f0ff0247268234f2170d3。
GLB 6b7f8b1c1c52e04d77dd8ec29839ccb272e965490137c140e0c0d41b494723bf。
Thickness a0642df21fc9aec326f7d386bab04d91f49fa50203f45f189aaa286f481ce582。
18 条金线；JadeCore / BronzeFrame / GoldWire 各独立闭合，闭合来自模型 QA，不由 Renderer 数推断。
导入实读 11,196 + 21,328 + 7,500 = 40,024 三角、3 个不透明 Renderer/材质。无重复玉层；ShadowCaster/深度等额外 pass 不包含在前向图元数中。当前无最终 LOD、Quest 或双眼性能验收。

可看文件：revision-a-first-look-01/ 下 01—10、4 秒 R03_FixedLight_Turn.mp4；11 是实际原槽几何落座。media-contract.json 逐项记 SHA、bytes、相机/灯/输入。
07/08/09 是本次同一帧 B 左、R03 A 右，并非不同曝光拼图。01—09 使用普通二章灯/天空/曝光快照；10 仅改主灯方向用于逆光，不能当作本体背面。06 对准实际 local Y315mm 孔下铜帽接玉端，旧首15线版本错位腰部图不复用。
4 秒视频为 120 帧、30fps 固定光和机位，物件 yaw180±22°；这是影片编码参数，不是头显运行帧率。

材质：当前首样纯色基础玉/铜/金，不宣称已完成最终微表面纹理。玉使用 Opaque PBR + clearcoat、真实测厚吸收/浅散射和细密内部色近似；不采样屏幕背景，无 alpha 透图、无 emission。吸收(38,50,70)/m 等为美术拟合，非本件实测常数。Thickness R×0.12m，1,930,932 逐 UV 像素 BVH 射线，5 个三角法向备用、2 个 miss 取保守120mm、251,393 长路径截断；不是无误差的体积散射/折射。
普通 Mobile RP1024阴影/50m/1cascade/bias1,1保留，三个本物 ShadowCaster 对 signed bias 各限 depth0.75mm/normal0.50mm；接收器与外挡保留。阴影单变量控制证明原大三角来自阴影偏移，不能归功于 A 法线。低分辨率原管线不保证解析0.1mm接触影。
原场景光为强度1.65、色(1,.88,.74)、shadowStrength.68，原Trilight和Gorge全景反射；无摄影软箱、无曝光提亮。离线实捕128立方天空反射，运行需要既有环境反射支持；不声称移动端每帧实时probe已配置。此样板复制光照设置，未复制全章地形遮挡、完整场景或真实头部姿态。

验证：Unity6000.3.10f1、RTX4050Laptop、D3D11；PID66404原生exit0，14:56:21—15:00:52+08。
玉面黑白背景差0；真实测厚背光增量最大.5686275；真实URP/Lit外挡后增量0；主灯关增量0；直接光/环境/反射全关黑背景无自发光。指标来自同相机可见玉深度遮罩，不将金铜像素算玉。
实际原槽6056三角，6层射线孔宽153mm，四向×25步120mm保守量规插入路径交0；过深正控7.9999mm，120mm量规45°正控8.3528mm。样板落座bottomY=.9239401，插深104.06mm、台身底间隙4mm。106mm实际方端45°对角149.9mm可进入153孔，方位拒绝应是玩法准线规则；肩不是跨孔实体止挡。此轮几何位置验证不等于 GameItemBase / SnapZone / 真实 XR 抓取接线，当前独立Prefab是视觉资源。
原生log保留无头环境XR、编辑器SearchDatabase索引异常、Package Manager警告；不宣称全日志零异常。当前采图/探针无失败、所测shader未见编译错误。未在本轮做 Android/Vulkan 编译或 Quest 真人实测。

可复现源：SourceSnapshot/Assets/Review/QingGuiR03/ 全部源/材质/独立Prefab/新FBX/厚度图与原meta。
运行时视觉Prefab：Generated/QingGuiR03_FirstLook.prefab（原点/旋转为建模根，摄影仅转实例）。依赖 URP17.3 和本目录 shader/Generated/Model/RevisionA。Editor 样板与场景还引用隔离工程既有 R02 B 对照、ReferenceConsole；这份源码快照不是包含全旧版依赖的自足 unitypackage。
复跑入口 Editor/QingGuiR03FirstLook.Run；Logs/.../capture-input.json 配置实际模型与 run 名。必须在独占 D:/Temp/NarutoVR-QingGuiR01-20261125 中启动，并给新 run 名，不覆盖这次图。原槽运行时不可读网格由编辑器预读真实6056三角缓存（Generated/OriginalConsoleTriangles.bytes），不修改原模型 meta 或伪造孔。完整命令、实际执行源和 native exit 在 Logs/.../revision-a-first-look-01/。
不安装到主工程 Assets，不替换正式章节，不覆 R01/R02/B，不宣称本首样可以直接进入游戏。
