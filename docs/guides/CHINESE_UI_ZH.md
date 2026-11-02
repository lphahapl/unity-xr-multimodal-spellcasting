# 结印实验台中文显示

本次将 Quest 原生场景和 Steam Link 场景的头显文字、编辑器实验台面板、常见运行提示改为中文，并为自研组件字段补充中文分组、显示名和说明。模板规则在 Inspector 折叠时显示中文特征名，展开后分为“特征与目标范围”和“容差与手动权重”。

## 如何查看

- 编辑器面板：`Tools → NarutoVR → 结印实验台`。原来的 `Seal Lab` 菜单入口仍可用。
- 中文场景显示：打开原生 `SealLab.unity` 或 `SealLabSteamLink.unity`，进入播放模式。
- 重新应用字体与初始文字：退出播放后，运行 `Tools → NarutoVR → 界面 → 应用中文显示并检查`。
- 选择“结印实验台”对象可查看追踪来源、采样、免手录制、测试与中文字体字段；选择印模板可查看中文规则列表。

## 名称与数据

| 录制中的固定标识 | 面板中文名称 |
|---|---|
| L.Index.Curl | 左手·食指·卷曲 |
| R.Middle.Flexion | 右手·中指·屈曲 |
| L.Index.Opposition | 左手·食指·对指距离 |
| Both.WristDistance | 双手腕间距 |
| Both.PalmAngle | 双掌朝向夹角 |

面板以中文显示，鼠标悬停可查看固定英文标识。特征 ID、录制 JSON 字段名、单位编码、模板特征引用与评分原因码保留，旧录制与旧模板无需转换。Inspector 中的“固定特征标识”属于数据值，应保持与提取器一致。

Steam Link 的“关节有效性推导”表示质量标记来自运行时关节有效性，不是 Meta 原生置信度。界面汉化不改变评分权重或判定阈值。

## 中文字体

使用随包资源 `Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf`，来源为 [Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese)。保留原始字体，附带 `OFL.txt` 许可和从字体元数据提取的 `NOTICE.txt` 版权信息。完整字体约 16.4 MB；使用动态字形生成，避免依赖电脑上安装的字体。

`SealLabView` 的“头显中文字体”可手动替换。未绑定时会加载同一份随包字体；场景初始化提示也已绑定此字体。

Unity 内置组件以及第三方 SDK 自带 Inspector 的类型名、原厂字段不通过修改包缓存来汉化；本项目自研组件、模板规则与实验台面板使用中文。

## 备份与检查

汉化前的实验台代码、场景与 XR 设置保存在 `Logs/BeforeChineseUI-20261029-113135/`。更早的完整工程源文件备份位于 `D:\UnityProjects\NarutoVR_Backups\before-steamlink-20261029-105429`。

导入与场景检查报告：`Logs/chinese-ui-result.json`；既有识别、录制、回放检查报告：`Logs/seal-lab-checks.json`。两个场景的字体绑定和常用中文字形已检查通过，四类自研组件的中文 Inspector 已验证加载，49 项既有回归检查通过。实际头显中文排版仍以重新进入应用后的显示为准。
