# 给云端 Gemini 的工作台命令

在这个目录新建一个 UTF-8 **普通 JSON 文件**，如 `review-room-001.json`。文件名去掉后缀须等于顶级 `id`。每个新意图用新的唯一 ID，推荐 UUID。ID 可以含英文字母、数字、短横线和下划线，最长 96 位。每份命令只操作一条记录。云端只需能在 Drive 新建文件。

执行机每两秒检查一次，等文件连续两次稳定后处理。Drive 的上传和下载另需时间。回执位于上一级的 `results/<命令id>.json`。按回执判断是否完成。步骤有依赖时，先等上一步回执再提交下一步。同 ID 同内容的重新投递会复用原回执。修改已执行命令的内容会得到单独的冲突回执。保留收件和回执直至各端同步完成。

## 试炼间示例

先新建 `read-room-001.json`。

```json
{"id":"read-room-001","op":"read","kind":"scenes","target":"trial-room"}
```

从 `results/read-room-001.json` 读取 `doc` 和 64 位十六进制 `revision`。确认当前布局后新建 `move-trigger-001.json`，把下面的版本占位符替换为读到的原值。位置字段使用绝对坐标。

```json
{"id":"move-trigger-001","op":"scene.set","kind":"scenes","target":"trial-room","baseRevision":"替换为 read 回执中的 revision","objectId":"trigger","fields":{"x":5}}
```

回执 `status: "ok"` 会带上保存后的 `object` 和新 `revision`。需要看画面时新建独立的预览命令，把 `baseRevision` 设为刚保存的新版本。

```json
{"id":"see-room-001","op":"preview","kind":"scenes","target":"trial-room","view":"top","baseRevision":"替换为 move 回执中的 revision"}
```

成功回执的 `image` 是相对共享目录的 PNG 路径。等该图片也同步后打开查看。若收到 `status: "conflict"`，用新 ID 重新 read，核对用户改动后再提交。错误回执含 `code` 与 `error`。

## 可用命令

`kind` 取 `rig`、`plans` 或 `scenes`。`target` 为集合、策划案或场景的记录 ID。

| op | 用途 | 附加字段 |
|---|---|---|
| read | 读取完整记录及版本 | 无 |
| create | 创建新记录 | doc，完整配置，doc.id 等于 target |
| replace | 更新整个已有记录，适合策划正文、模型节点和版本清单 | baseRevision、doc |
| delete | 删除目标配置，独立素材保留 | baseRevision |
| scene.set | 更新白盒内单个物体 | baseRevision、objectId、fields。可指定 x、y、z、w、h、d、rotation、visible、name、type、shape |
| rig.note | 给某个模型版本追加批注 | baseRevision、versionId、text |
| preview | 渲染当前实际记录并返回 PNG | 可选 baseRevision。场景用 view 选 perspective 或 top。角色须填 versionId，可加 bone 查看权重热图，或 clip 与 seconds 取动画帧 |

角色预览示例：

```json
{"id":"shen-wrist-001","op":"preview","kind":"rig","target":"shen","versionId":"shen","bone":"LeftHand"}
```

角色预览回执的 `details` 含原始骨名、片段 ID、权重检查结果。选骨时还会返回影响顶点数与最大权重。先省略 bone、clip 可以取得该版本的清单。残机偶的待机片段示例：

```json
{"id":"puppet-idle-001","op":"preview","kind":"rig","target":"sample","versionId":"sample","clip":"file:idle:0","seconds":0.72}
```

## 执行机

### 从本机 Chrome 中的 Gemini 对话投递

网页版 Gemini 无法直接新建普通 JSON 时，可由本机接收指定对话里的 JSON 回答。使用已经开启远程调试的本机 Chrome（端口 9222），打开需要的 Gemini 对话。双击 [Gemini-Receive.cmd](../Gemini-Receive.cmd) 并粘贴对话链接，或在共享目录运行 `node gemini-inbox.mjs "该对话的完整 https 链接"`，保持这个终端打开。只收一次可在末尾加 `--once`。关闭终端、关闭该标签或导航到其他对话会结束接收。

用户提示第一行单独写 `工作台投递 命令ID 操作 类型 目标ID`。下一行写实际需求，请 Gemini 回复一个完整的 JSON 代码块。接收器只检查指定对话中最近一轮用户首行及其配对回答，核对命令 ID、操作、类型和目标，再将命令写到本机 inbox。重复编号不能覆盖已有文件。示例首行为 `工作台投递 my-first-read read scenes trial-room`。若需接着修改，可将上一步的 JSON 回执作为附件交给 Gemini，要求它使用其中的 revision 作为新命令的 baseRevision。图片回执同样可以作为附件交给该会话。

### 启用 Drive 文件收件

只指定一台同步工作空间的电脑收件。给共享目录写权限的协作者可以发出上表中的命令，包括删除记录。工作台使用同一套记录校验与并发写入检查。收件器按操作名分派，不运行文件里提供的脚本或系统命令。

在执行机的共享目录运行 `node commands.mjs --enable`，重启该目录的 Node 工作台服务后生效。平时按原方式打开工作台即可，本机服务运行期间关掉浏览器标签仍可收件。可从 [本机收件状态](http://127.0.0.1:4190/api/receiver) 核对 enabled。关闭使用 `node commands.mjs --disable` 后重启服务。此设置和恢复日志只保存在该电脑的用户数据目录，绑定工作空间的实际路径与服务端口。换电脑时先停止原执行机、等回执同步完成，再启用新机。

截图由执行机上独立的无头 Chrome 实例绘制，无需登录账号。自定义 Chrome 安装位置可设置环境变量 `WORKSPACE_CHROME`。请求上限 8 MiB。场景和模型素材需事先同步到执行机。

Windows 上已经运行旧版 4190 服务时，可以在启用后执行 `node commands.mjs --attach` 热接入。此时原服务的 /api/receiver 会返回 404，可投递 read 并在 results 查看带时间的回执。下次正常启动新版服务时由内置收件器接管。
