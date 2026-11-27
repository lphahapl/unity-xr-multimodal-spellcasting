# T-28 青铜概念 B1 / B2

2026-11-25，GPT-6 / Codex Desktop。用户在已展示馆藏候选后明确“你继续，给我设计稿”，看过第一组后要求“主榫在设计一版”。这两次要求授权出概念候选，不代表纹样或美术定稿。

- [主榫 B2](Tenon_B2.png)：双夔合抱、窄玉芯对准线、圆肩收腰。当前新增候选。
- [主榫 B1](../BronzeConceptB1/Tenon_B1.png)：单鸟纹、玉质拇指落点；保留比较。
- [③取用台 B1](../BronzeConceptB1/Pedestal_B1.png)：方彝式分层、兽面前饰和低位双销提案；与主榫分开批注。
- [工作台图稿对照](http://127.0.0.1:4190/plans/workshop-master-tenon-bronze-concepts.html)。
- [完整提示词](prompts.json)：内置 `image_gen.imagegen`；B1初稿、针对标注的修订、取用台B1和主榫B2共四次实际生成。未调用外部生图模型。

## 参考与设计边界

馆藏参考为[大都会商方彝 1974.268.2a,b](https://www.metmuseum.org/art/collection/search/44776)、[大都会西周方鼎 1988.20.2](https://www.metmuseum.org/art/collection/search/44513)、[克利夫兰方卣 1963.103](https://www.clevelandart.org/art/1963.103)。此前已核馆藏页及 Public Domain / CC0 标记，原始下载记录在 `../BronzeReference/`。生成图是现代游戏道具适配，不是馆藏照片或考古复原。

主榫设计目标仍是约350mm长、最大120mm包络，方插端沿用既有106×106×105mm及底端定位作为后续建模约束。③原稿展示座为850×850×650mm。概念图多视图比例、侧厚、玉芯连续性和浮雕高度仍需建模统一，不能按像素取尺寸。B1初稿的侧厚尺寸指引有误，已保留为 `../BronzeConceptB1/Tenon_B1_initial.png` 历史草稿，不上架；当前B1去除了数字尺寸线。

④中控为另一处已有资产，保持接口。③取用台小图只解释低位双销动作，主榫形状是示意，未证明与B1/B2配合。闭合面、销位、握持净空和退开路径均须在选稿后建模复测。新稿没有继承R03的原孔或锁止通过结论。

R01/R02/R03资产保留。R03旧方向的Blender和独立Unity工程验证已完成，详见 `../R03/README.md` 和 `../R03/UnityReview/README.md`；本次没有运行Unity、Blender、头显，也没有改正式模型、场景或材质。青铜比例、纹样和新台座均待用户批注，主世界设定及专业需求未提升为已定稿。
