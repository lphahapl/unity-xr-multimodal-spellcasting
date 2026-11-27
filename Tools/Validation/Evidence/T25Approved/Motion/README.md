# T-25 · 运行时搬运持姿验收

Unity 6000.3.10f1 在隔离工程 Play 中通过正式敌人目录生成 `stone-carrier`。工奴带出的镇石包含正式 A4 FBX 的 8 块网格、21,456 个三角面。可见模型相对于石根的位置、旋转和缩放均为单位变换。工奴使用原搬运剪辑、原 Humanoid Avatar 和正式 Room Prefab 的右肩旋转覆盖，根运动关闭。[输入文件散列](tested-inputs.json) · [实际 Play 断言](play-checks.txt)

## 运行行为

工奴独占基础层 `Base Layer.StoneCarry` 时，`StoneCarrier.LateUpdate` 从 Animator 读取当前状态相位，按配置的 87 个采样对石根局部位置作线性插值、对朝向作球面插值。普通残机偶继续走原状态。工奴巡逻停顿和房间暂停会停住搬运动作，恢复后相位继续；外部把 Animator 改为半速，手和石头都会随之减速。[逐帧相位和误差](runtime-frames.csv)

原受击状态接管后，持姿停止写石根；过渡结束，工奴恢复携带原石。死亡立刻脱石，此后的 Animator 更新不会改写落石。对象池复用同一工奴时会装配新石并从第 0 帧开始。销毁工奴会清理当前背载的石头，已经掉在场上的那块继续存在。[生命周期测试](play-checks.txt)

## 87 帧实测

在旋转 37°、离开世界原点的工奴根下，以 30 Hz 推进正式 Animator，起点只定位一次。每步在动画求值后调用同一个运行时持姿回调，回读石根、双腕和 CPU 烘焙蒙皮。测量范围为整段原剪辑的第 0–86 帧，含循环终点。下面误差均为该次回读与批准样本之间的最大值。[石根局部位姿](runtime-poses.csv) · [散列及逐骨比较](readback-comparison.json)

| 指标 | 最大误差 |
| --- | ---: |
| 归一化播放相位 | 0.0000003391 圈 |
| 石根位置 | 0.000505 mm |
| 石根旋转 | 0°（Unity `Quaternion.Angle` 的报告精度） |
| 石根缩放 | 87 帧均为 (1, 1, 1) |
| 任一手腕位置 | 0.001301 mm |
| 任一手腕朝向 | 双精度归一化四元数复算小于 0.0002° |

本次 Editor Play 以 Android 为活动目标、Mobile 质量预设、TwoBones 全局权重运行，蒙皮渲染器配置为 Auto。与历史棚拍的全权重求值比较，最大顶点差为 **38.5195 mm**，位于第 51 帧、609 号髋腿三骨权重顶点。用真实骨矩阵和逆绑定矩阵重算该帧后，保留前两权重的结果与生产烘焙最大差 0.00061 mm，保留所有权重的结果与棚拍最大差 0.00216 mm。两个显式渲染器骨数探针与默认导出的顶点散列相同。PC 质量的独立渲染和 Quest 真机画面仍待验证。[蒙皮诊断](play-checks.txt)

用同一实例的 461,187 个顶点样本、4,058 个身体三角、石根实测位姿及批准的 A4 实物求距，87 帧内均未检测到三角相交或闭合体包容。全身最近 **3.7748 mm**（第 6 帧），右手长件最近 **39.2853 mm**（第 55 帧），左右手掌距石最大分别 **8.4053 mm / 9.3989 mm**。这些数字适用于独占搬运状态中采样的 87 帧，受击过渡仅做动作与持物归属验证。[同批净空与输入散列](../RuntimeClearance/default/summary.json)

## 复现

将本目录中的探针复制至独立工程的 `Assets/Editor/StoneCarryPoseChecks.cs`。工程需要同版正式资源、脚本、导航和 Addressables 配置。证据根目录需要保留 `Logs/CH02-carry-pose-20261125` 中的原始顶点、骨骼和 manifest，以及 `Logs/CH02-ballast-A4-20261125/motion/final/candidate.csv`。用 Unity 运行下列参数，等待 `A4_MOTION_PASS`。[验证探针](StoneCarryPoseChecks.cs)

```text
-projectPath <独立工程> -batchmode -nographics -executeMethod StoneCarryPoseChecks.Run -evidenceRoot <证据根目录> -motionOutput <本次输出目录> -logFile <本次日志文件>
```

正式实例的全量日志为 `Logs/CH02-T25-A4-production/motion/play-production.log`，原始数据在同级 `production/`。顶点为小端 float32，维度为 [87, 5301, 3]，单位米、坐标系为 Unity 角色根。索引为小端 int32，维度为 [4058, 3]。25 根骨骼逐帧保存根空间位置与旋转，维度为 [87, 25, 7]。编辑器启动时的 SearchDatabase 索引异常以及 Package Manager 联网警告均出现在测试进入 Play 之前；该次 Play 成功完成全部断言。
