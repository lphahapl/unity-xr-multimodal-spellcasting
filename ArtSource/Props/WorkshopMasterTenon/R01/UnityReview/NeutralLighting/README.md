# 中性照明材质对照

三张图沿用原近手、侧光和实插视角，使用同一冻结网格与贴图。BaseColor 导入为 sRGB；Normal、MetallicSmoothness、AO 为线性；Normal 使用 NormalMap 导入、没有翻转绿色通道。材质颜色乘数为白色，法线、金属与粗糙度通道保留，没有发光。

原样板主灯 RGB=(1,0.94,0.82)、强度 2.1，补光偏蓝且 ambient 带色，造成较强黄绿偏色和顶面高光。此对照改为白色主灯 1.1、白色补光 0.35，以及中性灰 ambient；没有添加调色或曝光后处理。冻贴图里的轻微青灰纹理仍然保留，不把它强行改成纯白。

文件分别是 `01_NearHand_Front45_Neutral.png`、`02_SideLight_Neutral.png` 和 `03_Inserted_Front45_Neutral.png`。用于判断环境照明影响和材质后续批注，不代表玉质/艺术最终通过。
