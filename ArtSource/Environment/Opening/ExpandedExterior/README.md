# 首章窗庭远山制作源

本目录保存 2026-11-08 静态外景扩建的作者脚本、场景字段清单和实际检查证据。Unity 6000.3.10f1，正式资产在 `Assets/Art/Environment/Opening/ExpandedExterior/`。

## 交付

- `ExteriorAuthor.cs`：从扩建前首章生成十扇窗、东庭、外部台基、三座远亭、残渠、山脊及边界；新增 3026 个三角形、5 个 Renderer。四面旧侧墙只删除对应方盒的 12 片三角面，保留相接端墙。
- `ExteriorSurvey.cs`：六机位离线渲染；`ExteriorVerify.cs`：重新加载序列化候选后检查引用与碰撞。
- `scene-changes.json`：30 个原场景字段改动和新 Prefab GUID；`publication.json`：实际合入前后场景哈希及资产清单。
- 两份 `MasonryWindows` 网格保留原材质索引，Prefab 内只有静态网格与 BoxCollider。

## 再制作

脚本是离线制作配方，留在 Assets 外，不作为运行时代码。复做须先从 `Logs/exterior-expand-20261108/publication-base.unity` 或等价的扩建前版本建立独立 Unity 副本，将作者、摄影和验证脚本临时复制到该副本的 Editor 目录，修改 `ExteriorSurvey.Out` 输出目录，再调用 `ExteriorAuthor.Build`。它不会覆盖正式场景。已扩建场景不能重复运行作者脚本，否则旧墙和旧坐标的断言会失败。

原制作使用本机 `Logs/exterior-expand-20261108/scene_patch.py candidate` 只合入环境字段，逐字节检查所有既有 MonoBehaviour 和无关 YAML 块不变；Unity 重新加载候选并写出 `PASS END` 后才运行 `publish`。脚本带场景哈希检查，禁止在场景已有新修改时覆盖。

天空盒由用户在独立会话把控，本 Prefab 不指定天空材质、环境光或逐帧行为。这里的离线检查没有验证 Quest 双眼、移动舒适性或帧时间。
