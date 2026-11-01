import os
import numpy as np
import matplotlib.pyplot as plt
from PIL import Image, ImageDraw, ImageFont, ImageFilter

def create_vr_vignette_diagram():
    os.makedirs(r"d:\UnityProjects\NarutoVR\docs\visuals", exist_ok=True)
    os.makedirs(r"C:\Users\鲁迅\.gemini\antigravity\brain\3081f072-31dc-4fc6-8b99-e4d4a469452b", exist_ok=True)
    
    # Dimensions
    w, h = 1600, 900
    
    # Set up matplotlib figure
    plt.rcParams['font.sans-serif'] = ['SimHei', 'Microsoft YaHei', 'Arial Unicode MS', 'DejaVu Sans']
    plt.rcParams['axes.unicode_minus'] = False
    
    fig = plt.figure(figsize=(16, 9), facecolor='#0D1117')
    
    # Title & Header
    plt.suptitle("《考工律度》VR 动态视野舒适度遮罩 (Dynamic Comfort Vignette / 隧道视野机制)", 
                 fontsize=22, fontweight='bold', color='#E6EDF3', y=0.96)
    
    # Subplots layout: 2 VR viewports on top, 1 timing curve and 1 principle breakdown on bottom
    gs = fig.add_gridspec(2, 2, height_ratios=[1.3, 0.7], hspace=0.28, wspace=0.15,
                           top=0.90, bottom=0.08, left=0.06, right=0.94)
    
    ax1 = fig.add_subplot(gs[0, 0])
    ax2 = fig.add_subplot(gs[0, 1])
    ax3 = fig.add_subplot(gs[1, 0])
    ax4 = fig.add_subplot(gs[1, 1])
    
    # ----------------- Viewport 1: Normal View -----------------
    ax1.set_title("【常规状态】静止或慢速移动 (FOV: 110° 全开，遮罩孔径 Aperture = 1.0)", 
                  fontsize=14, color='#58A6FF', pad=10, fontweight='bold')
    # Generate background: ancient stone hall perspective
    res_x, res_y = 400, 300
    y_coords, x_coords = np.mgrid[0:res_y, 0:res_x]
    
    # Simulated 3D corridor lines
    scene_bg = np.zeros((res_y, res_x, 3), dtype=np.float32)
    center_x, center_y = res_x // 2, res_y // 2
    
    # Ceiling & Floor gradient
    floor_mask = y_coords > center_y
    ceil_mask = y_coords < center_y
    scene_bg[floor_mask] = [0.12, 0.14, 0.18]
    scene_bg[ceil_mask] = [0.08, 0.10, 0.14]
    
    # Stone pillars / wall depth perspective
    for r in range(center_y, res_y, 25):
        scene_bg[r:r+2, :] += [0.05, 0.08, 0.12]
    # Center bronze celestial altar glow
    dist_from_c = np.sqrt((x_coords - center_x)**2 + (y_coords - center_y)**2)
    glow = np.clip(1.0 - dist_from_c / 120.0, 0, 1)
    scene_bg[:, :, 0] += glow * 0.45  # Golden bronze glow
    scene_bg[:, :, 1] += glow * 0.35
    scene_bg[:, :, 2] += glow * 0.15
    
    # Edge full field of view (slight natural vignette of VR lens)
    lens_vignette = np.clip(1.0 - (dist_from_c / 220.0)**4, 0.2, 1.0)
    for c in range(3):
        scene_bg[:, :, c] *= lens_vignette
        
    ax1.imshow(scene_bg)
    ax1.axis('off')
    # Add VR binocular overlay outline
    circ1 = plt.Circle((center_x, center_y), 130, color='#30363D', fill=False, lw=2, linestyle='--')
    ax1.add_patch(circ1)
    ax1.text(center_x, center_y + 110, "边缘光流充斥外周神经 -> 静止时无刺激，舒适稳定", 
             color='#7EE787', fontsize=11, ha='center', bbox=dict(boxstyle='round,pad=0.3', facecolor='#161B22', edgecolor='#238636', alpha=0.9))

    # ----------------- Viewport 2: Vignette Active View -----------------
    ax2.set_title("【高速平切 / 瞬移瞬间】遮罩收缩生效 (隧道视野，孔径 Aperture = 0.45)", 
                  fontsize=14, color='#FFA657', pad=10, fontweight='bold')
    
    # Rapid motion scene has strong peripheral motion blur and dark feathered vignette
    dash_scene = np.copy(scene_bg)
    # Add radial motion streaks in background
    angles = np.arctan2(y_coords - center_y, x_coords - center_x)
    streaks = (np.sin(angles * 24) * 0.5 + 0.5) * (dist_from_c / 200.0) * 0.25
    dash_scene[:, :, 0] += streaks * 0.3
    dash_scene[:, :, 1] += streaks * 0.2
    
    # Apply Dynamic Vignette (Feathered aperture at r=65)
    aperture_radius = 65.0
    feather_width = 35.0
    vignette_factor = 1.0 - np.clip((dist_from_c - aperture_radius) / feather_width, 0.0, 1.0)
    # Smooth step (Hermite)
    vignette_factor = vignette_factor * vignette_factor * (3.0 - 2.0 * vignette_factor)
    
    for c in range(3):
        dash_scene[:, :, c] *= vignette_factor
        
    ax2.imshow(dash_scene)
    ax2.axis('off')
    
    # Overlay indicators
    circ_center = plt.Circle((center_x, center_y), aperture_radius, color='#58A6FF', fill=False, lw=1.5, linestyle=':')
    circ_outer = plt.Circle((center_x, center_y), aperture_radius + feather_width, color='#FA7970', fill=False, lw=1.5, linestyle=':')
    ax2.add_patch(circ_center)
    ax2.add_patch(circ_outer)
    
    ax2.text(center_x, center_y + 110, "遮罩收缩羽化：全黑阻断外周视觉流 -> 彻底消除前庭晕眩", 
             color='#FFA657', fontsize=11, ha='center', bbox=dict(boxstyle='round,pad=0.3', facecolor='#161B22', edgecolor='#D29922', alpha=0.9))
    ax2.text(center_x, center_y - 10, "中心保留清晰观察窗 (50%)", color='#E6EDF3', fontsize=10, ha='center', weight='bold')

    # ----------------- Subplot 3: Timing & Curve (0.05s In, 0.15s Out) -----------------
    ax3.set_facecolor('#161B22')
    for spine in ax3.spines.values():
        spine.set_color('#30363D')
    ax3.grid(True, color='#21262D', linestyle='--', linewidth=0.7)
    
    time_pts = np.linspace(0, 0.45, 450)
    # Velocity spike at t=0.05, duration 0.15s
    # Aperture response curve: 1.0 -> drops to 0.45 -> recovers to 1.0
    aperture_vals = np.ones_like(time_pts)
    for i, t in enumerate(time_pts):
        if t < 0.04:
            aperture_vals[i] = 1.0
        elif t < 0.09: # 0.05s rapid drop
            norm_t = (t - 0.04) / 0.05
            aperture_vals[i] = 1.0 - 0.55 * (1.0 - np.cos(norm_t * np.pi / 2))
        elif t < 0.22: # holding during 0.8m dash
            aperture_vals[i] = 0.45
        elif t < 0.38: # 0.16s smooth recovery
            norm_t = (t - 0.22) / 0.16
            aperture_vals[i] = 0.45 + 0.55 * (0.5 - 0.5 * np.cos(norm_t * np.pi))
        else:
            aperture_vals[i] = 1.0
            
    ax3.plot(time_pts * 1000, aperture_vals, color='#58A6FF', lw=2.5, label='遮罩可视孔径比例 (Aperture Ratio)')
    ax3.axhline(0.45, color='#FFA657', linestyle='--', alpha=0.7, label='极限闪避收缩阈值 (0.45)')
    ax3.axvline(90, color='#7EE787', linestyle=':', alpha=0.6)
    ax3.axvline(220, color='#FA7970', linestyle=':', alpha=0.6)
    
    ax3.set_title("动态时序触发曲线 (Comfort Vignette Animation Profile)", fontsize=13, color='#E6EDF3', fontweight='bold')
    ax3.set_xlabel("动作发生时间 / 毫秒 (ms)", fontsize=10, color='#8B949E')
    ax3.set_ylabel("可视孔径比例 (1.0 = 全开)", fontsize=10, color='#8B949E')
    ax3.tick_params(colors='#8B949E')
    ax3.legend(loc='lower right', facecolor='#0D1117', edgecolor='#30363D', labelcolor='#E6EDF3', fontsize=9)
    
    ax3.annotate("手势平切触发\n0.05s 急速收缩", xy=(65, 0.72), xytext=(85, 0.82),
                 arrowprops=dict(facecolor='#7EE787', shrink=0.05, width=1, headwidth=6),
                 color='#7EE787', fontsize=9, fontweight='bold')
    ax3.annotate("位移完成\n0.16s 平滑回弹", xy=(300, 0.75), xytext=(270, 0.58),
                 arrowprops=dict(facecolor='#58A6FF', shrink=0.05, width=1, headwidth=6),
                 color='#58A6FF', fontsize=9, fontweight='bold')

    # ----------------- Subplot 4: Technical Specs & Medical Principle -----------------
    ax4.set_facecolor('#161B22')
    ax4.axis('off')
    
    spec_text = (
        "【VR 晕动症医学成因与工程原理】\n"
        "● 视觉前庭冲突 (Vestibular Mismatch)：\n"
        "  玩家在真实物理空间处于静止，但虚拟世界中发生 0.8m 急速平切或瞬移。\n"
        "  人眼【视网膜外周区域】对高角速度光流极为敏感，大脑因感官不一致引发晕眩。\n\n"
        "● 动态遮罩核心机制 (Dynamic Tunneling)：\n"
        "  1. 动静感知切除：在角速度或线速度瞬间剧增时，0.05s 内遮蔽外周 50% 视野。\n"
        "  2. 软边缘羽化 (Smooth Feathering)：采用 Hermite 三次方平滑过渡，消除生硬黑圈违和感。\n"
        "  3. 沉浸式律度包覆：暗圈边缘融入《考工律度》微弱金青色太虚刻度暗纹，使之成为法术护体感知。\n"
        "  4. 自由选项：在手腕《天工罗盘》UI 中提供：[强力]、[适度 (推荐)]、[关闭] 三档调节。"
    )
    ax4.text(0.04, 0.95, spec_text, transform=ax4.transAxes, fontsize=10.5, color='#C9D1D9',
             va='top', ha='left', fontfamily=['Microsoft YaHei', 'SimHei'], linespacing=1.45,
             bbox=dict(boxstyle='round,pad=0.6', facecolor='#0D1117', edgecolor='#30363D'))

    # Save to workspace and brain
    p1 = r"d:\UnityProjects\NarutoVR\docs\visuals\comfort_vignette_diagram.png"
    p2 = r"C:\Users\鲁迅\.gemini\antigravity\brain\3081f072-31dc-4fc6-8b99-e4d4a469452b\comfort_vignette_diagram.png"
    
    plt.savefig(p1, dpi=200, bbox_inches='tight')
    plt.savefig(p2, dpi=200, bbox_inches='tight')
    plt.close()
    print(f"Saved diagram successfully:\n{p1}\n{p2}")

if __name__ == '__main__':
    create_vr_vignette_diagram()
