# 共享工作空间

安装 Node.js 22 或更新版本后，从本目录双击 **Open.cmd**，或者在本目录运行 `node server.mjs`。Windows 启动器优先打开 Chrome。服务器只监听本机，默认端口为 4190。所需的 Three.js 0.180.0 模块、示例 FBX 与材质已随文件夹提供，可以离线启动。

| 入口 | 双击文件 | 浏览器地址 | 记录目录 |
|---|---|---|---|
| 工作空间首页 | Open.cmd | http://127.0.0.1:4190/ | workspace.json |
| 骨骼与动画工作台 | rig/Open.cmd | http://127.0.0.1:4190/rig/ | config/rig/ |
| 策划案工作台 | plans/Open.cmd | http://127.0.0.1:4190/plans/ | config/plans/ |
| 场景原型工作台 | scenes/Open.cmd | http://127.0.0.1:4190/scenes/ | config/scenes/ |
| 配置与检索 | reader/Open.cmd | http://127.0.0.1:4190/reader/ | 读取以上三个目录 |

服务运行期间，双击任一上述目录的 index.html 可跳到对应页面。程序随目录移动。每台设备用本机同步到的副本启动。使用自定义端口时设置 `RIG_WORKBENCH_PORT` 并使用 Open.cmd 或对应的 HTTP 地址。旧版 4186 的 `/?model=shen` 链接会跳到新工作台的沈商版本。

## 给通过 Google Drive 协作的云端 Gemini

云端会话可以在共享目录的 `inbox/` 新建一份普通 JSON。执行机上的工作台会自动接收命令，处理配置或渲染预览，再将回执和图片写入 `results/`。先用 `read` 读取当前版本，后续修改带上该版本号。完整版示例覆盖试炼间触发区、沈商左腕权重和残机偶动画帧。Chrome 中的 Gemini 也可通过 [Gemini-Receive.cmd](Gemini-Receive.cmd) 投递带指定编号的回答。[命令格式与操作示例](inbox/README.md)

## 给能够操作本机 Chrome 的会话

打开 [配置与检索](http://127.0.0.1:4190/reader/) 可按工作台和文本检索。标题、策划正文、场景坐标、角色版本及批注都是可读取的网页文字。选择「打开工作台」可定位记录；使用「编辑 JSON」或对应工作台的表单可保存修改。需要机器可读的完整数据时，打开 [完整 JSON](http://127.0.0.1:4190/api/context)。场景页把可选节点、局部位置、旋转和缩放放在可读写的表单中。白盒另带俯视图和元素表。骨骼的逐关节顶点计数和权重异常计数在骨骼页面的检查面板。

例如可让 Gemini 在你已打开的本机 Chrome 标签页中执行“在场景原型里找到试炼间，把触发区沿 X 轴挪一米，保存，再通过配置与检索回读坐标”。只能访问 Drive 文件的会话可直接读取下列 UTF-8 JSON。实际写入取决于该会话对文件或浏览器的权限。Drive 网页预览、Gemini 云端请求和协作者自己电脑的 localhost 各有不同的访问范围。

## 骨骼与动画

打开[沈商人物集合](http://127.0.0.1:4190/rig/?collection=shen)默认显示 **08 · 自然神态 I（修眼角前）**。在顶部「版本」切换头部稿、整身稿、绑定版本和玩家左右手；地址中的 `model` 固定当前版本。各历史存档的实际内容见[沈商玩家模型指南](../docs/guides/SHEN_SHANG_PLAYER_MODEL_ZH.md#工作台存档)。

查看[沈商整身比例修订](http://127.0.0.1:4190/rig/?collection=shen&model=shen-proportions)或[上一次眼皮与下颌修订](http://127.0.0.1:4190/rig/?collection=shen&model=shen-eyelids-jaw)，点「全身三视图」可对照正面、左侧与背面，点图可打开原尺寸。版本可通过 `turnaround` 指定 `files` 中的一张图片。

打开[残机偶](http://127.0.0.1:4190/rig/?model=sample)，在「动画预览」选择「待机 / mixamo.com」，点「播放」。拖动时间轴可检查任意时刻的姿态，取消循环可停在末帧。「退出动画」恢复进入播放前的手动姿态。骨骼标记与权重热图跟随当前姿态。

导入新模型时选择新建集合或已有角色集合。切换版本后，动画和批注跟随版本切换。内嵌片段会自动列出。用「导入匹配骨架的动画」选择含动作的 FBX、GLB 或 glTF，有外部贴图或 BIN 时同次选入。外部片段按骨名和父链绑定，源和目标都有蒙皮绑定矩阵的关节还会核对局部绑定长度、轴向和缩放。不匹配会显示具体骨名或轨道。[上一版沈商](http://127.0.0.1:4190/rig/?model=shen)也支持同骨架动画。只有骨架时可选择「单骨转动测试（合成）」检查当前关节。

沈商集合包含玩家左右手。打开任一侧后可切换 V2 与 V3，点「版本批注」查看该版的审阅结果。配置里的 `slotAlbedos` 按原模型材质槽名引用同版本附件的文件名，例如 `{"HandBody":"LeftHandAlbedo.png","ForearmEnd":"LeftCuffAlbedo.png"}`。V2 与 V3 共用同侧的原贴图，工作台预览底色；Unity 材质负责铜纹高光和袖口渐隐。[沈商左手 V3](http://127.0.0.1:4190/rig/?collection=shen&model=hand-left-v3) · [沈商右手 V3](http://127.0.0.1:4190/rig/?collection=shen&model=hand-right-v3)

## 两种 3D 场景

- 打开[工坊场景模型](http://127.0.0.1:4190/scenes/?id=workshop)。在「3D 场景模型」中可导入 FBX、GLB 或 glTF，点选模型或从「所选节点」选择层级中的对象。用移动、旋转、缩放手柄或数值调整局部变换。切换「可见」可隐藏该节点及子树。工坊源文件是合并网格，独立移动门、墙等部件需要源文件保留各自的节点。
- 打开[试炼间白盒](http://127.0.0.1:4190/scenes/?id=trial-room)。在「3D 白盒」中添加立方体、球体或圆柱。可在视口中拖动、缩放、旋转、删除，或展开「俯视布局与场地尺寸」摆放入口、角色和触发区。三维坐标用米，Y 向上，旋转输入用度。
- 「保存到文件」将当前模式及编辑结果写入场景 JSON，刷新后可继续。切换模式会保留同一记录内两种模式的数据。「导出当前 GLB」下载当前可见场景的几何、材质和节点变换，可再导入查看。源 FBX 与导出的 GLB 分开保存。

## 手工维护文件

角色集合可用 `defaultVersion` 指定默认显示的版本 ID。访问只带 `collection` 的链接时使用这个值；链接带有 `model` 时直接打开对应版本。沈商当前的值是 `shen-archive-08`。

每个角色集合、策划案或场景原型占一个 `config/<种类>/<id>.json`。可复制现有记录创建，修改文件内容更新，按名称或正文检索，移走或删除文件撤下。文件名与顶级 id 须一致。ID 用字母、数字、下划线和短横线，以字母或数字开头，最多 96 位。名称、正文、版本标签与批注可写中文。工作台点击「重新读取文件」或刷新页面会读到本地已到达的改动。无效 JSON 在「配置与检索」列出文件名与原因，修复后重新读取。

- **骨骼与动画**。每条记录含 id、name、versions。每版有独立 id、label、filename、created、files 和 notes。created 为 Unix 毫秒时间戳。files 是相对 assets/ 的「目录/文件名」清单，须包含与 filename 同名的模型。可手工把附件放入 `assets/my-model-v2/` 后引用它们。FBX 和 glTF 的外置贴图与 BIN 要一并提供，同一版内附件名唯一。可选 material 指定 files 内的 color、normal 和 0–1 之间的 metalness。可选 `animations` 数组按版本存放动画。每项有 id、label、filename、files 与按源文件顺序排列的 clips 名称。可参照 [残机偶记录](config/rig/sample.json)。批注形如 `{"id":"review-1","text":"复核左腕","created":1795291200000}`。删改版本和批注可直接编辑对应数组，也可用网页的「编辑 JSON」。删除版本后独立附件保留，确认没有其他版本引用后可手工清理。
- **策划案**。id、name、status、summary、body 都可查阅，正文支持多行纯文本。可用 `/plans/?id=awakening` 定位首章样例。
- **场景原型**。id、name、summary、mode 标识记录。白盒模式 `whitebox` 使用 width、depth 和 objects。对象的 x、z 为旋转前平面占地起点，y 为底面高度，w、h、d 为尺寸。shape 可用 box、sphere、cylinder，rotation 按 XYZ 存储度数，visible 控制显示。视口手柄的位置在物体中心。模型模式 `model` 使用 model.filename、files 和 nodes。nodes 以 `root`、`root/0` 等原始子节点索引路径存储覆盖项，每项包含 position、rotation、scale、visible。导入替换源模型后会重建路径映射。可对照[试炼间白盒配置](config/scenes/trial-room.json)和[工坊模型配置](config/scenes/workshop.json)。
- **首页跳转**。编辑 `workspace.json` 的 name、links；每个跳转项包含 title、description 和站内 path。可将 `rig/?collection=shen&model=shen` 等深链接加入首页，或在其他文件夹放置指向这些本机地址的快捷方式。

## 共享与并发

在 Google Drive 桌面版选择同步整个 SharedWorkSpace，包括 config、assets、vendor 和页面。本应用不需要 Google 密钥。网页写入本地配置，Drive 客户端负责把变化送到其他电脑。[Google Drive 桌面同步说明](https://support.google.com/drive/answer/10838124?hl=zh-Hans)

多人可同时编辑不同记录。网页用内容哈希检查同一文件的旧版本。若提示文件已经变化，先保留自己的草稿，重新读取后再合并。同一文件在两台尚未同步的电脑离线编辑时，仍需处理 Drive 的冲突副本。先等附件同步到本机再打开新模型。原有浏览器库在原 Chrome 配置和原地址的「配置与检索 → 旧浏览器资料」可迁入；同一版本与批注可重复执行迁入，旧 IndexedDB 保留。

## 本机 HTTP 接口

- `GET /api/context` 返回 workspace 以及 rig、plans、scenes 的 entries、errors。只读原文件位于 `/config/<种类>/<id>.json`。
- `GET /api/<种类>` 列举记录；`GET /api/<种类>/<id>` 返回 doc、revision，响应 ETag 为带双引号的 revision。
- 新建使用 `PUT /api/<种类>/<id>` 及 `If-None-Match: *`。更新使用同一 PUT 及上次读取的 `If-Match: "<revision>"`。请求体是记录本身，Content-Type 为 application/json。删除使用 DELETE 和相同的 If-Match。旧版本返回 412，重名创建返回 409，无效数据返回 422。
- `POST /api/assets?name=<URL 编码的附件名>` 接收 application/octet-stream，返回可放进 files 的相对路径。内容相同且同名的文件可以复用。单个附件上限为 512 MiB。
- 浏览器写操作来自工作台自身的页面。可操作本机 Chrome 的会话使用这些页面即可，无需在 Gemini 网站中跨来源调用 localhost。

