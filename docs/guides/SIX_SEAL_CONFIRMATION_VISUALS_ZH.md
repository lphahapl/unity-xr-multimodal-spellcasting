# 六印成印字影：小篆与验证入口

2026-11-26。按用户“成印特效字影必须是小篆”修正原自绘篆意字。五印声音继续采用 V3.1，律印保留原铜鸣；此轮只改字影。

## 字形与来源

六字使用同一份崇羲篆体的原轮廓：律、規、矩、準、繩、衡。正式印名与配置仍为律、规、矩、准、绳、衡；繁体本字只用于成印字影。矩使用字库所收的矩字篆体转写，不换成巨，也不声称为《说文》“矩”独立字头摹本。

[逐字字源与许可记录](../../ArtSource/VFX/SealConfirmationXiaozhuanR1/README.md)包括矩／巨／榘关系、字体文件实际哈希、CNS 参照未采用原因及官方 PDF 未取得的边界。[官方许可](https://xiaoxue.iis.sinica.edu.tw/chongxi/copyright.htm)为 CC BY-ND 3.0 TW or later。采用原字库普通排字、等比缩放与透明背景，未增删或加粗笔画；字体镜像未完成官方整包哈希比对，不作这一保证。

游戏用 PNG 为 480×576。暖金 RGB 246/220/167 来自原律印实心笔画代表色；alpha 与原字体白版逐像素相同。白字版、原轮廓 SVG、旧版字影及暖金版分别留存，供复查。保留作者署名和来源；未来修改笔画不属于本轮普通排字方案。

## 实际画面与边界

- [六印 Unity 实际实例同屏](../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/SixSealActualInstances.png)：上排规、矩、衡，下排律、准、绳。同屏图用于比较，六个实际 Prefab 同脉冲年龄，板上声音静音。
- [衡印左腕与原腕匣](../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/Heng/compare_with_case.png)：Unity 合成腕姿态、原左锚与实际腕匣；不能当头显跟踪或所有手势遮挡已验。
- [旧版／小篆与小尺寸对照](../../ArtSource/VFX/SealConfirmationXiaozhuanR1/Comparison/SixSeal_Old_vs_ChongXi.png)：96、64、48 像素静态排字，仍须 Quest 双眼观感检查。
- [实际播放结果](../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/result.txt)：独立 Unity 6000.3.10f1 / D3D11，用原 matcher → ForwardSeal → PlaySealConfirmation → SealGlyphPulse 播放链。

规、矩、律、衡各稳定保持确认一次、有效退出再进入确认一次，退出不额外成印。准、绳仍为未标定，识别事件为零，仅直接预览原模板。六印原声的 AudioRenderer 实际混音均非零；不重做音源或重复先前压力测试。历史手帧回放属于旧样本回归，合成特征和腕姿态不等于真人识别。

此轮部署只覆盖六个原 `Glyph.png`，保留 `.meta`、Prefab、声音、手别、大小、配置与去重。最终部署与受保护文件的前后哈希以[本轮证据目录](../../Logs/seal-confirmation-xiaozhuan-r1-20261125/)的部署记录为准。正式场景未由本轮打开或保存，Quest 实机观感未测。
