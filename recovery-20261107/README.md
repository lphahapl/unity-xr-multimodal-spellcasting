# NarutoVR 未推送改动恢复包（2026-11-07）

## 这是什么

2026-11-06 旧 D 盘损坏前，本工程有一批改动**没有提交也没有 push**。本目录从 Codex 的会话日志
（`C:\Users\鲁迅\.codex\sessions\`）中逐条提取 `apply_patch` 的原始补丁文本，按时间顺序重放重建。

**这些不是人工整理的摘要，是模型当时实际写入的补丁内容。**

## 基准

| 项 | 值 |
|---|---|
| 当前仓库 HEAD | `96f0a20 手部`（2026-11-02 14:58 +0800） |
| 会话来源 | 44 个 Codex 会话，1,254 条 apply_patch |
| 重放起点 | 2026-10 会话起（含更早的未提交新建文件） |

## 目录

```
recovered-files/     265 个当前 HEAD 中不存在的文件全文（重建）
patches/             2026-11-03 起的 259 条补丁，按时间顺序编号
report3.txt          11-03 之后改动的逐文件清单 + 29 条冲突明细
combined3.diff       11-03 之后相对 HEAD 的净差异（63 文件，+8,568 / -22）
report_all.txt       全量重建清单（312 文件）
combined_all.diff    全量重建差异（2.2 MB）
manifest.json        恢复文件清单（路径 / 补丁数 / 最后修改时间）
```

## 可信度

- `Add File` 的全文是会话日志里逐字记录的内容，**可靠**。
- `Update File` 是重放结果：如果同一文件在之后被再次修改过，重建结果可能与旧磁盘的真实终态有偏差。
- 29 条冲突（见 `report3.txt` 末尾）是补丁上下文对不上的，需要人工核对，主要集中在：
  `docs/README.md`、`docs/architecture/AUDIO_VFX_SPECIFICATION_ZH.md`、`docs/skills/music-production/*`。

## 使用方式

1. 先看 `report3.txt` 的 `=== CHANGED FILES ===` 段确定涉及范围。
2. 需要某个文件的完整内容 → 直接取 `recovered-files/<原路径>`。
3. 需要按变更粒度应用 → 用 `patches/` 里的单条补丁（时间顺序即依赖顺序）。
4. **不要直接把 `combined_all.diff` 打上去**。它包含 10 月的历史改动，与当前 HEAD 大量冲突，
   只作为参照，不作为可应用补丁。

## 注意

- 本目录位于 `Assets/` 之外，Unity 不会导入，不影响编译。
- 恢复的源码没有经过编译验证，引入前需在 6000.3.10f1 中确认能通过编译。
- 目录体积约 5.4 MB；是否提交进 git 由使用者决定。
