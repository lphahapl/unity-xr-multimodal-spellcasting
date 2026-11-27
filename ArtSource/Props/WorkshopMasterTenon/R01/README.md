# 工坊主榫 R01 · 独立审美初稿

本目录仅为可批注的模型初稿，未推广到正式场景。③工坊取用座和④中控插座是两个位置；当前只制作主榫本体，不复制台身或插座。原设图来自V2.1的中控参考；依据父代理读到的P57，将“阴刻”文字替换为不含文字的回折云雷纹和度量刻线。

## 尺寸与导出

- 完整外廓：120 × 120 × 350 mm，插入底端中心原点。Blender +Z竖直，FBX/GLB导出映射Unity +Y竖直。
- 下端6mm内有导入倒角；顶部轻倒角，铜箍为连续同网格的嵌入式材质分区，不形成外挂铜肩或加宽握头。
- 现有方孔名义146mm时，居中静态单侧理论余量13mm；真实孔壁、插深和抓取/插入玩法由独立Unity测试另验，本目录不把名义尺寸推导当真实插孔通过。
- `MasterTenon_LOD0.fbx`：720三角；`MasterTenon_LODs.fbx`：720 / 256 / 96三角三档；`MasterTenon_R01.glb`为可独立查看的LOD0与内嵌PBR贴图。
- 每档1网格、1材质、1 UV。以上为本件临时预算结果，未声明项目通用标准。

## 高模与贴图

`MasterTenon_R01.blend`保存高模和三档低模。高模为343,616三角的真实浅凹云雷纹、下端校准刻度和插入接触边磨损；低模轮廓网格保留倒角，雕刻细节由真实Cycles selected-to-active高到低投射取得。

`Textures/`中全套2K PNG：BaseColor、Normal、AO、Roughness、Metallic、ORM、MetallicSmoothness。BaseColor为sRGB，其余为线性Non-Color；Normal为切线空间OpenGL +Y。ORM为R=AO/G=Roughness/B=Metallic；Unity MetallicSmoothness为R=Metallic、A=1−Roughness。

原高模材质采用青白玉云絮色差、浅刻槽积色、暗哑旧铜。游戏材质是单套不透明PBR表面表现；高模源材质的轻微次表面散射没有变成自定义Quest玉石着色器，不以离线效果冒称运行时通透效果。纹样为原创回折云雷纹设计解释，不是具体出土器物的考据复刻。

烘焙参数：Cycles、2K、16px扩边、3mm cage extrusion、8mm最大射线距离。BaseColor/Roughness/Metallic用源通道发光投射避免带入照明；Normal/AO为对应烘焙模式。首轮高模发现0.014m位置因浮点数产生一条重合零面积环，清掉重合环后高模QA通过；有效外表面位置与已经烘焙的贴图未改变。重建脚本已对环高度舍入以避免重复。

## 制作和检查

```powershell
& 'D:/Blender/Blender 5.2/blender.exe' -b --factory-startup --python-exit-code 1 --python 'ArtSource/Props/WorkshopMasterTenon/R01/build_tenon.py'
& 'D:/Blender/Blender 5.2/blender.exe' -b --factory-startup --python-exit-code 1 --python 'ArtSource/Props/WorkshopMasterTenon/R01/verify_exports.py'
```

Blender5.2.2。高模和三档低模均通过blender-asset-qa，源网格无边界、非流形、反法线、浮件、z-fighting或UV重叠。FBX和GLB在空场景冷开回读，LOD0均720三角、120×120×350mm、原点0。FBX无告警；GLB有62个位置相同顶点，属于导出UV/切线分割，已接受，源网格无重复顶点。具体数值在`model-report.json`和专属Logs/model。

`Previews/01–10`为真实材质渲染：正45、背45、正/右/背、插入端刻度、玉侧光、铜近景、相同机位高低模。`QA_material/QA_wire`为技能生成的四视图。技能Wireframe修改器在细长三角形尖角产生延长线，补充`11_CleanWireFront/12_CleanWire45`以原网格边曲线显示精确拓扑；这些线不属于模型几何。

待验：用户审美、头显近手材质、正式Unity材质光照、真实拿取/入槽操作、其他握姿遮挡。文件不含清场解锁表现或正式关卡逻辑。
