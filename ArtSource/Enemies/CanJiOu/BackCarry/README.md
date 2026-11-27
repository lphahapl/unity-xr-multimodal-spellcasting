# 残机偶背石慢走

用 Blender 5.2.2 打开 `CanJiOu_BackCarryWalk.blend` 可以直接编辑骨架关键帧和镇石轨迹。动作每秒 30 帧，第 0 帧和第 48 帧接环。循环 1.60 秒走两步，对应巡逻速度 0.54 米/秒。地面相对承重脚每帧前进 18 毫米。

`CanJiOu_BackCarryWalk.glb` 为旋转审阅版，包含角色和独立镇石。选择唯一动作可同时播放两者。`CanJiOu_BackCarryWalk.mp4` 为左右同步的侧面与后侧演示。游戏动画 FBX 位于 `Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_BackCarryWalk.fbx`，其中只有原骨架与步态。角色和镇石的审阅材质沿用现有模型与基色贴图。

## 重建

在项目的独立副本根目录执行以下 PowerShell 命令。生成脚本会写入该副本的 Blender 源、FBX、GLB 及镇石轨迹 CSV。输入包括现有 `Assets/Art/Enemies/CanJiOu/CanJiOu_LOD0.fbx`、`BallastStoneA4/T25-A4-Unity.fbx`、原角色及镇石纹理。其余 Python 参数均为输出路径。

```powershell
$backCarry = 'ArtSource/Enemies/CanJiOu/BackCarry'
$blender = 'D:/Blender/Blender 5.2/blender.exe'
$plates = Join-Path $env:TEMP 'CanJiOu-BackCarry-Plates'
$movie = Join-Path $env:TEMP 'CanJiOu-BackCarry-Movie'
& $blender -b -t 3 --factory-startup --python-exit-code 1 -P "$backCarry/build_backwalk.py"
& $blender -b -t 3 "$backCarry/CanJiOu_BackCarryWalk.blend" --python-exit-code 1 -P "$backCarry/render_backwalk.py" -- $plates
python "$backCarry/encode_backwalk.py" $plates $movie
& $blender -b -t 2 "$backCarry/CanJiOu_BackCarryWalk.blend" --python-exit-code 1 -P "$backCarry/export_unity_deltas.py" -- "$movie/SourceDeltas.csv"
```

视频编码使用 Python 的 Pillow、imageio-ffmpeg，以及系统中文字体微软雅黑。其他机器可用 `--font` 指向能显示中文的 TrueType 或 OpenType 字体。镇石 CSV 的相位为 0 至 1，坐标是 Unity 角色根空间中的米，四元数顺序为 x、y、z、w。骨姿差值 CSV 的第 −1 帧是绑定姿势；同一角色的其余 49 帧包含每根骨的根空间位置及相对绑定姿势的旋转差值，可用于游戏 Avatar 重采样。
