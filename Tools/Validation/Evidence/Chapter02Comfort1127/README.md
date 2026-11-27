# 二章移动、瞄准与素纱屏风验收

[正式二章](../../../../Assets/Scene/Chapter02_SuspendedPath.unity)进场时已经学会衡印。左手结印后消耗原定度力开盾。铜橛在工具带窄鞘，竖握、锥尖触地后松手展开。旧档已收纳、落地或插着的铜橛从原状态恢复。补发时如果窄鞘已占，先找空着且装得下的通用口。玩家可以先在[全道具场](../../../../Assets/Samples/ToolBeltItems/AllItemsLab.unity)正前方五台的中间试插。

四面素纱屏边长 3 米、高 2.3 米，展开持续约 0.48 秒，场有效期 12 秒。透明 URP Shader 用顶点位移驱动轻纱摆动和命中波，片元着色驱动织纹与沿缝边移动的暖光。来自外部的投射物穿入前被拦截，内部向外射击和人物通行均可。击中屏外的弹幕不会借溅射伤到屏内。[二章固定机位动画](StakeChapter-curtain.mp4) · [展开后](StakeChapter-open.jpg)

| 项目 | 桌面实测 |
| --- | --- |
| 章节入口 | [直开](entry-fresh-override.txt)、[旧档](entry-legacy.txt)、[从第一章经过传送门](entry-travel.txt)均可主动开盾，初始铜橛只出现一根。 |
| 伤害及敌方瞄准 | 二章敌方直接伤害乘 0.65，玩家基础攻击伤害乘 1.5，弱点固定额外伤害与削韧另算。射手跟随站立、下蹲的玩家瞄躯干，地面保留落点预警。可用闪避避开已锁定的炮线。[倍率](damage.txt) · [射手实射](ranged.txt) |
| 移动与辅助开关 | 探索、战斗分别设置，均默认左食指走、行走中握拳快跑。战斗两种模式都可以用右手快推闪避。辅助瞄准可在准望匣关闭，偏好独立保留。[腕匣与移动](movement.json) |
| 术法轻辅助 | 四套瞄准 SO 分别控制。默认捕捉半角 2.5°、最大修正 1.5°、强度 0.7。原本瞄中时保留玩家方向。检验头和发射点的遮挡，发射按同一准星解算。龙火跟随手印方向，镜头不会被拉向敌人。[四术实测](aim.txt) |
| 铜橛 | [样板弹道检查](stake-lab.txt) · [正式场景初发、占口、插地、回鞘和读档](stake-chapter.txt) |

屏风的材质参照西汉马王堆曲裾素纱单衣，采用游戏中的素色纱面、深色窄缘和受击动效。[馆方展览及工艺说明](https://www.artmuseumonline.org/art/art/visitGuide/xwgg/2026/07/04/2c906d9b9e0127fe019f3bdb331225cb.html) · [素纱实物照片](https://www.artmuseumonline.org/art/upload/image/2026/07/07/52027eb933db48878212eb52b86214ae.jpeg)

辅助瞄准参照已发售 VR 游戏。《Pistol Whip》默认开启，可用 Deadeye 关闭；《地平线：山之呼唤》支持视线辅助射箭。本项目根据玩家原有瞄准方向作小幅修正，无需眼动追踪。[Cloudhead Games](https://www.cloudheadgames.com/post/how-to-play-pistol-whip-everything-you-ve-ever-wanted-to-know) · [PlayStation 官方支持](https://www.playstation.com/en-nz/support/games/horizon-call-of-the-mountain/)

Unity 6000.3.10f1，主要验证在关闭 XR 的独立桌面进程执行。Quest／PICO 的双眼观感、远景纱纹、手势手感与帧时待实机确认。

龙火与飞炎按最新正式效果资产另外做了 38 项桌面回归，覆盖飞炎 16 米实际命中与撞墙、龙火 1.848 米持续命中与被墙截断。[对应效果资产散列](aim-current-profiles.json)  · [插地冷启动](stake-planted-cold.txt) · [归鞘冷启动](stake-stowed-cold.txt)

[Android Vulkan 验证](shader-vulkan.txt)从 Unity 两组立体变体各自的合并程序中提取顶点与片元模块，四个模块均通过 spirv-val。动画为游戏内材质的固定机位采样，3.05 秒、20 fps，第 1.65 秒触发一次命中涟漪以供观察；敌火核的实际拦截另见样板弹道记录。
