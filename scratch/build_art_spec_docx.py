# -*- coding: utf-8 -*-
import os, sys, docx
from docx.shared import Pt, Inches, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import parse_xml, OxmlElement
from docx.oxml.ns import nsdecls, qn

ART_DOCX = r"docs/美术需求/考工律度_美术资产与视效制作需求规范.docx"

def set_cell_margins(cell, top=80, bottom=80, left=120, right=120):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def set_cell_shading(cell, color_hex):
    shading = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{color_hex}"/>')
    cell._tc.get_or_add_tcPr().append(shading)

def generate_art_spec_docx():
    doc = docx.Document()
    
    # Base Style
    normal = doc.styles['Normal']
    normal.font.name = 'Microsoft YaHei'
    normal.font.size = Pt(10)
    normal.font.color.rgb = RGBColor(0x33, 0x33, 0x33)
    normal.paragraph_format.line_spacing = 1.25
    normal.paragraph_format.space_after = Pt(4)
    
    # Title
    t_p = doc.add_paragraph()
    t_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    t_p.paragraph_format.space_before = Pt(14)
    t_p.paragraph_format.space_after = Pt(4)
    r_title = t_p.add_run("《考工律度》XR 多模态施法项目\n美术资产制作与视效渲染需求规范书")
    r_title.font.size = Pt(20)
    r_title.font.bold = True
    r_title.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    
    sub_p = doc.add_paragraph()
    sub_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    sub_p.paragraph_format.space_after = Pt(16)
    r_sub = sub_p.add_run("Art Asset & VFX Production Specification (Target: Meta Quest Standalone / PCVR - Revised 2026-11-02)")
    r_sub.font.size = Pt(9.5)
    r_sub.font.italic = True
    r_sub.font.color.rgb = RGBColor(0x66, 0x66, 0x66)
    
    # Summary Box
    t_sum = doc.add_table(rows=1, cols=1)
    t_sum.autofit = True
    c_sum = t_sum.cell(0, 0)
    set_cell_shading(c_sum, 'F0F4F8')
    set_cell_margins(c_sum, 100, 100, 140, 140)
    p_sum = c_sum.paragraphs[0]
    p_sum.add_run("【美术工程核心准则与技术对齐】\n").bold = True
    p_sum.add_run(
        "• 目标受众：3D角色/场景美术师、TA（技术美术）、VFX特效师、UI设计师、VR交互程序\n"
        "• 硬件基线：Meta Quest 独立一体机优先（Snapdragon XR2 / URP 移动管线），目标 72/90fps，Draw Calls < 120，场景同屏面数 < 15 万面\n"
        "• 美术哲学：金石肃穆与结构秩序（汉白玉、青灰玄石、冷铸青铜、煅烧朱砂），严禁高饱和度杂乱粒子与日式/修仙俗套\n"
        "• 最新技术映射：全面对齐【空手快推后撤】气浪反冲视效、【视向定轨与送势解缚】直线恒速 9m/s 火核、【太仪·负衡偶】3s长条红带预警与10s石缝卡死过热弱点呈现"
    )
    for cr in p_sum.runs:
        cr.font.size = Pt(9)
        cr.font.name = 'Microsoft YaHei'
        
    doc.add_paragraph().paragraph_format.space_after = Pt(6)
    
    # Section 1
    h1 = doc.add_paragraph()
    h1.paragraph_format.space_before = Pt(12)
    h1.paragraph_format.space_after = Pt(4)
    r1 = h1.add_run("一、世界材质语言与 PBR 色板约定")
    r1.font.size = Pt(14)
    r1.font.bold = True
    r1.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    
    p = doc.add_paragraph(
        "本工程采用 Unity URP Lit 材质标准。针对 Quest 移动端 GPU 填充率与带宽限制，全局严格采用哑光、微漫反射与局部自发光结合的材质策略，"
        "杜绝大面积光滑镜面反射与强高光刺眼光斑，构建厚重肃穆的中式古典工匠世界："
    )
    
    palette_data = [
        ["材质名称", "HEX色值", "Metallic", "Roughness", "应用规范与质感描述"],
        ["凿面青灰岩", "#2A3036", "0.05", "0.85", "占场景 70% 墙面地面，凿刻纹理，漫反射平稳，无刺目光泽，模拟先秦汉代巨石工程"],
        ["汉白冷玉", "#DFE4E8", "0.10", "0.35", "核心神道、祭坛立柱，具有温润次表面感与轻微边缘透光，代表天地未失度前的规矩秩序"],
        ["哑光青铜", "#D4A042", "0.85", "0.30", "准望匣、墨规笔、基标方砖、场景铜铎，冷铸金属感，边缘带氧化绿斑磨损贴图"],
        ["熔核朱砂", "#E65100", "0.00", "0.20", "火核内核、按扣晶石、弱点核心，Emission = 2.0，局部点睛发光，严禁大面积满屏泛光"],
        ["失度空洞", "#241429", "0.00", "0.90", "空间折断处吸光材质，暗紫近黑雾效，造成重力崩解与虚空断层感"],
        ["应力赤红", "#FF1744", "0.20", "0.40", "首领抡砸预警长带（Telegraph）、机体破损应力缝隙，带低频正弦波呼吸脉冲"]
    ]
    
    t_pal = doc.add_table(rows=len(palette_data), cols=len(palette_data[0]))
    t_pal.autofit = True
    for r_idx, row in enumerate(palette_data):
        for c_idx, val in enumerate(row):
            cell = t_pal.cell(r_idx, c_idx)
            cell.text = val
            set_cell_margins(cell, 60, 60, 100, 100)
            if r_idx == 0:
                set_cell_shading(cell, 'EAECEE')
                for cp in cell.paragraphs:
                    for cr in cp.runs:
                        cr.font.bold = True
                        cr.font.size = Pt(9)
                        cr.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
            else:
                if r_idx % 2 == 1:
                    set_cell_shading(cell, 'F8F9FA')
                for cp in cell.paragraphs:
                    for cr in cp.runs:
                        cr.font.size = Pt(8.5)
                        
    doc.add_paragraph().paragraph_format.space_after = Pt(6)

    # Section 2
    h2 = doc.add_paragraph()
    h2.paragraph_format.space_before = Pt(12)
    h2.paragraph_format.space_after = Pt(4)
    r2 = h2.add_run("二、第一人称资产制作需求")
    r2.font.size = Pt(14)
    r2.font.bold = True
    r2.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    
    doc.add_paragraph("1. 勘律使第一人称手臂 (First-Person Arms)：\n"
                      "• 三角面预算：双臂总三角面严格控制在 2500~3500 面以内。\n"
                      "• 造型特征：深灰粗糙织布缠手带，小臂紧缚麻绳，食指与拇指裸露并带有冷铸青铜指环（强化指骨相规的几何感与对准精度）。\n"
                      "• 追踪友好性：手腕处严禁设计悬垂飘荡的碎布条或宽大袖口，避免遮挡 Meta Inside-Out 摄像头的骨骼追踪光流。\n"
                      "• 骨骼权重：严格匹配 OpenXR / Meta Hand Tracking 骨骼层级，关节大角度弯折时无破面与穿帮。")
                      
    doc.add_paragraph("2. 准望匣（左腕机械仪规）与铜叶：\n"
                      "• 准望匣本体：长八角青铜机壳，表面嵌有精细的齿轮微轴与水平刻度游标。外侧设一颗朱砂圆形按扣（按扣下陷行程 2mm）。\n"
                      "• 三扇展开铜叶：呈折扇状展开骨骼，铜叶采用低多边形浮雕结合法线贴图；文字与图标采用粗线条阳刻，避免小楷细字造成纱窗模糊。\n"
                      "• 交互高亮规范：指尖靠近/悬停（0~5cm）仅产生外缘呼吸微光，严禁产生遮挡主视野的巨型弹窗；捏下确认时按扣下陷并伴随青铜卡扣脆鸣。")

    doc.add_paragraph("3. 墨规笔 (P2 扩展)：\n"
                      "• 外观：玄铜细长笔杆，通体蚀刻寸分刻度，笔尾内装微型发条储墨盒。\n"
                      "• 抓取点与碰撞体：设置标准 GrabInteraction 锚点，笔尖支持微碰撞反馈，落笔拖曳出刚性朱砂金线。")

    # Section 3
    h3 = doc.add_paragraph()
    h3.paragraph_format.space_before = Pt(12)
    h3.paragraph_format.space_after = Pt(4)
    r3 = h3.add_run("三、法术特效与交互视觉反馈 (VFX & Interaction Presentation)")
    r3.font.size = Pt(14)
    r3.font.bold = True
    r3.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    
    doc.add_paragraph("1. 【荧惑火核】：视向定轨与送势解缚视效\n"
                      "• 造型构架：采用几何网格旋转而非纯粒子发射。外围三道细金同心铜环（反向自转），内部包裹一颗熔核朱砂小球（半径严格限制在 8cm 以内，距掌心 5cm 悬浮，不遮挡正面视平线）。\n"
                      "• 瞄准预测线：抬腕持核时，自手中火核引出一条微细淡金色准线直达头显前方目标点，端点生成微小光斑；放下手腕隐去预测线。\n"
                      "• 脱手发射：手势动作（松手/前送/保持）只作为解缚触发器。解缚瞬间铜环脱扣，火核以恒速 9m/s 笔直扫掠飞出，拖曳笔直朱砂金线尾痕，撞墙产生刚性碎裂火星。严禁制作随机弯曲或根据手速变动的轨迹。")

    doc.add_paragraph("2. 【定规障】：玄铜护盾与障破反馈\n"
                      "• 材质规范：正方几何玄铜半透明网格 Shader（单层 Alpha，菲涅尔边缘光）。\n"
                      "• 受击与碎裂：受击点产生金色同心水波涟漪；耐久度低于 30% 时显示边缘龟裂贴图；耐久耗尽瞬间化为金色金属粉尘消散（障破）。")

    doc.add_paragraph("3. 战斗【空手快推后撤】避险视效：\n"
                      "• 触发表现：右手在胸前猛推空气瞬间，掌心前爆发一圈直径约 0.3 米的透明刚性反冲空气激波环。\n"
                      "• 地面轨迹：脚底青石地砖浮现一道微弱的朱砂回退折痕，身形平滑后退 0.8 米，落地后折痕光晕迅速隐去。严禁制作刀光剑影或侧身平切特效。")

    doc.add_paragraph("4. 探索【连续平滑行走】地脉视效：\n"
                      "• 视向铺展：左食指持续伸直时，视线指向的平整地面上，方圆界青铜基标光晕随视线平滑延伸。\n"
                      "• 场景地面要求：首片长廊必须为连续、无缝隙断层的平整石板神道，严禁在探索段出现无预警的断裂深渊阻断脚步。")

    doc.add_paragraph("5. 【定目帘 (Comfort Vignette)】护目暗圈：\n"
                      "• 视觉表现：全屏相机圆形羽化遮罩 Shader，边缘微带暗金规矩纹理。位移剧震时垂落遮蔽外周刺目光流（Aperture 0.45~0.6），落稳后 0.2 秒平滑回弹收回。")

    # Section 4
    h4 = doc.add_paragraph()
    h4.paragraph_format.space_before = Pt(12)
    h4.paragraph_format.space_after = Pt(4)
    r4 = h4.add_run("四、敌人与特色首领【太仪·负衡偶】美术制作标准")
    r4.font.size = Pt(14)
    r4.font.bold = True
    r4.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    
    doc.add_paragraph("1. 基础小怪【残机偶】：\n"
                      "• 造型特征：高约 1.8 米，青冈栎木骨架包覆哑光青铜甲片。头颅发条破损自旋，动作带有机械抽搐与节律感。\n"
                      "• 攻击动作：左手卡尺爪飞掷、右手青铜规刃挥砍。攻击前摇带有明显的机簧发条尖锐蓄力声与关节微震，给玩家充足的快推后撤预警。")

    doc.add_paragraph("2. 特色首领【太仪·负衡偶】：\n"
                      "• 巨像外形：高 2.8 米，右臂一体化连接一杆长达 4 米的青铜天平大秤杆，秤尾悬挂数千斤重的八棱玄石秤砣；胸腔为三层逆转青铜浑天仪。\n"
                      "• 砸地大招危险区（Telegraph Zone）：大秤抡起蓄力 3.0 秒，地面投射出长 5 米、宽 1.5 米的高对比度【应力赤红】长条危险带。场地设计必须确保玩家身后留有 >1.5 米的安全纵深，避免快推滑退被背墙截停。\n"
                      "• 瘫痪弱点期（9~10 秒大窗口）：巨秤轰然砸穿石缝拔不出来，首领被杠杆反冲力带翻在地；机体各关节喷涌浓白过热蒸汽，胸腔浑天仪狂暴翻转，中心露出猩红发光的【浑天仪核心】（自发光 Emission=3.0），视觉诱导玩家甩核轰击。")

    # Section 5
    h5 = doc.add_paragraph()
    h5.paragraph_format.space_before = Pt(12)
    h5.paragraph_format.space_after = Pt(4)
    r5 = h5.add_run("五、VR / Quest 移动渲染管线性能预算与检查清单")
    r5.font.size = Pt(14)
    r5.font.bold = True
    r5.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)

    budget_data = [
        ["指标维度", "Quest 独立一体机硬指标", "PCVR 串流指标", "TA 审查与优化策略"],
        ["同屏三角面", "< 150,000 面", "< 500,000 面", "第一人称手臂 <3500 面，场景模块 LOD0/LOD1 严格削面"],
        ["Draw Calls", "< 120 calls", "< 400 calls", "采用 Atlas 纹理合批，MaterialPropertyBlock 亲和 SRP Batcher"],
        ["纹理格式", "ASTC 4x4 / 6x6", "BC7 / DXT5", "移动端贴图分辨率上限 2048x2048，法线贴图禁用无压缩 RGBA"],
        ["Overdraw (透明重叠)", "层数 < 2.0x 屏幕", "层数 < 3.5x 屏幕", "禁止大面积粒子云；火核、护盾均采用网格几何体控制透光面积"],
        ["后处理开销", "仅开启极简 Bloom / Vignette", "全功能 URP Post-processing", "禁止移动端开启 SSAO、景深与运动模糊，避免剧烈眩晕"]
    ]
    
    t_bud = doc.add_table(rows=len(budget_data), cols=len(budget_data[0]))
    t_bud.autofit = True
    for r_idx, row in enumerate(budget_data):
        for c_idx, val in enumerate(row):
            cell = t_bud.cell(r_idx, c_idx)
            cell.text = val
            set_cell_margins(cell, 60, 60, 100, 100)
            if r_idx == 0:
                set_cell_shading(cell, 'EAECEE')
                for cp in cell.paragraphs:
                    for cr in cp.runs:
                        cr.font.bold = True
                        cr.font.size = Pt(9)
                        cr.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
            else:
                if r_idx % 2 == 1:
                    set_cell_shading(cell, 'F8F9FA')
                for cp in cell.paragraphs:
                    for cr in cp.runs:
                        cr.font.size = Pt(8.5)

    doc.save(ART_DOCX)
    print("Successfully generated art spec docx to:", ART_DOCX)

generate_art_spec_docx()
