import os
import docx
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

def set_cell_background(cell, fill_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    tcPr.append(parse_xml(f'<w:shd {nsdecls("w")} w:fill="{fill_hex}"/>'))

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def create_document():
    doc = Document()
    
    # Page Margins
    for section in doc.sections:
        section.top_margin = Inches(0.8)
        section.bottom_margin = Inches(0.8)
        section.left_margin = Inches(0.8)
        section.right_margin = Inches(0.8)
        
    # Styles
    normal_style = doc.styles['Normal']
    normal_style.font.name = 'Microsoft YaHei'
    normal_style.font.size = Pt(10.5)
    normal_style.font.color.rgb = RGBColor(0x33, 0x33, 0x33)
    normal_style._element.rPr.rFonts.set(qn('w:eastAsia'), 'Microsoft YaHei')
    
    # Title
    title = doc.add_paragraph()
    title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_title = title.add_run("《考工律度》第二章 · 断衡悬道\n关卡动线与场景视觉出图指南")
    r_title.font.name = 'Microsoft YaHei'
    r_title._element.rPr.rFonts.set(qn('w:eastAsia'), 'Microsoft YaHei')
    r_title.font.size = Pt(22)
    r_title.font.bold = True
    r_title.font.color.rgb = RGBColor(0x1A, 0x36, 0x5D) # Dark Navy
    
    subtitle = doc.add_paragraph()
    subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_sub = subtitle.add_run("版本：V2.0_20261121 | 关联场景：Chapter01_Courtyard ➔ Chapter02_SuspendedPath\n规范核心：先秦工匠物理体系 · 首章崖端石门实景衔接 · 顺壁向下反Z字形下行 · AI出图全中文注解")
    r_sub.font.size = Pt(9.5)
    r_sub.font.italic = True
    r_sub.font.color.rgb = RGBColor(0x66, 0x66, 0x66)
    
    doc.add_paragraph().paragraph_format.space_after = Pt(12)

    # Helper for adding headings
    def add_h1(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(16)
        p.paragraph_format.space_after = Pt(6)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.font.name = 'Microsoft YaHei'
        run.font.size = Pt(15)
        run.font.bold = True
        run.font.color.rgb = RGBColor(0x1A, 0x36, 0x5D)
        run._element.rPr.rFonts.set(qn('w:eastAsia'), 'Microsoft YaHei')
        return p

    def add_h2(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.font.name = 'Microsoft YaHei'
        run.font.size = Pt(12.5)
        run.font.bold = True
        run.font.color.rgb = RGBColor(0x2B, 0x6C, 0xB0)
        run._element.rPr.rFonts.set(qn('w:eastAsia'), 'Microsoft YaHei')
        return p

    def add_p(text, bold_prefix="", italic=False, color=None):
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.line_spacing = 1.15
        if bold_prefix:
            r_b = p.add_run(bold_prefix)
            r_b.font.bold = True
            r_b.font.color.rgb = RGBColor(0x1A, 0x20, 0x2C)
        r = p.add_run(text)
        if italic:
            r.font.italic = True
        if color:
            r.font.color.rgb = color
        return p

    def add_prompt_box(title_text, prompt_en, annotations):
        # Container table for prompt
        table = doc.add_table(rows=1, cols=1)
        table.alignment = WD_TABLE_ALIGNMENT.CENTER
        table.autofit = False
        table.columns[0].width = Inches(6.8)
        cell = table.rows[0].cells[0]
        set_cell_background(cell, "F7FAFC")
        set_cell_margins(cell, top=140, bottom=140, left=180, right=180)
        
        # Cell border
        tcPr = cell._tc.get_or_add_tcPr()
        tcBorders = parse_xml(f'<w:tcBorders {nsdecls("w")}><w:left w:val="single" w:sz="24" w:space="0" w:color="3182CE"/><w:top w:val="single" w:sz="4" w:space="0" w:color="E2E8F0"/><w:right w:val="single" w:sz="4" w:space="0" w:color="E2E8F0"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="E2E8F0"/></w:tcBorders>')
        tcPr.append(tcBorders)
        
        p = cell.paragraphs[0]
        p.paragraph_format.space_after = Pt(4)
        r_head = p.add_run(title_text)
        r_head.font.bold = True
        r_head.font.size = Pt(11)
        r_head.font.color.rgb = RGBColor(0x2B, 0x6C, 0xB0)
        
        p_en = cell.add_paragraph()
        p_en.paragraph_format.space_after = Pt(8)
        p_en.paragraph_format.line_spacing = 1.1
        r_en = p_en.add_run("【英文提示词 / English Prompt】\n" + prompt_en)
        r_en.font.name = 'Consolas'
        r_en.font.size = Pt(9.0)
        r_en.font.color.rgb = RGBColor(0x2D, 0x37, 0x48)
        
        p_an = cell.add_paragraph()
        p_an.paragraph_format.space_after = Pt(2)
        r_an_title = p_an.add_run("【全中文逐句结构化注解与设计释义】")
        r_an_title.font.bold = True
        r_an_title.font.size = Pt(10)
        r_an_title.font.color.rgb = RGBColor(0xC5, 0x30, 0x30)
        
        for k, v in annotations:
            p_item = cell.add_paragraph()
            p_item.paragraph_format.space_after = Pt(3)
            p_item.paragraph_format.line_spacing = 1.15
            r_k = p_item.add_run(f"• {k}：\n  ")
            r_k.font.bold = True
            r_k.font.size = Pt(9.5)
            r_k.font.color.rgb = RGBColor(0x2C, 0x52, 0x82)
            r_v = p_item.add_run(v)
            r_v.font.size = Pt(9.5)
            r_v.font.color.rgb = RGBColor(0x4A, 0x55, 0x68)
            
        doc.add_paragraph().paragraph_format.space_after = Pt(8)

    # ==================== 第零节：本项目核心美术风格规范 ====================
    add_h1("第零节：本项目核心美术风格规范 (Project Art Style Specification)")
    add_p("《考工律度》的美术基调既非传统的道家修仙或修真玄幻，亦非西式的蒸汽朋克或废土魔幻，而是严肃、沉郁、克制且严密的“先秦工匠力学与墨家机巧工程体系（Mohist Punk / Artisan Physics）”。整体视觉贯彻三大核心基石：")
    
    add_h2("1. 严谨厚重的六大材质与色彩体系")
    add_p("粗粝坚韧的天然千仞绝壁峭壁、带有明显人工凿痕的石阶梯与平整稳固的青石地面。传递深邃冷峻、险峻巍峨的苍凉压迫感。", bold_prefix="① 青灰冷岩与玄武石基（占比 60% 以上基底）：")
    add_p("象征天地正统法则与工程度量标尺。严格用于重要建筑门楣、量水水尺、日晷刻度圈与机关校准榫头。质地细腻、微透冷润光泽，表面阴刻精致小篆与度量线。", bold_prefix="② 汉白冷玉（度量法则与规矩门楣）：")
    add_p("用于机械传动齿轮、轴承、榫卯销锁、大型天平悬臂与主角核心法具【准望匣】外壳。通体带有数百年岁月风化形成的青绿铜锈（Patina）与暗沉包浆，严禁使用任何浮华刺眼的工业抛光金或镀金！", bold_prefix="③ 暗沉哑光青铜（机巧机械与法器构件）：")
    add_p("纹理紧密的古老实木，用于承重主梁、悬崖挑梁木龙骨以及残机偶的活动木骨架。表面呈桐油防护的熟木质感，在机械受力关节处与青铜紧密咬合抱合。", bold_prefix="④ 青冈硬木与古栎木（承重构架与机偶木骨）：")
    add_p("用于古代机关锁止指示、机械过载警戒标识，以及主角使用【墨规笔】画出的自发光几何符纹。色彩为沉着高饱和度的天然辰砂红，醒目内敛，极具古代神秘工业感。", bold_prefix="⑤ 矿物朱砂与磁墨（警戒锁止与墨规笔画符）：")
    add_p("用于准望匣度力充盈辉光、地表寻路导航光点与连续路径线。如极地夜空般的清冷澄净冷光，在幽暗的灰石与悬崖间提供清晰、宁静且极高辨识度的视觉牵引。", bold_prefix="⑥ 荧光冰蓝（度力充沛与地表导航路径线）：")

    add_h2("2. 天地失度的异常视觉哲学（Artisan Anomaly）")
    add_p("游戏所展现的“大脱扣”灾变，本质是太古天钟崩坏后的物理力学秩序失常。所有画面中的超现实异象，必须通过严密的工程参照物反衬异常：")
    add_p("量水道中的水脱离石槽，顺着石阶逆流爬升撞向屋顶大梁。画面必须完整绘制低处的敞口跌水槽源头、向上漂移的水珠以及高处梁底的湿痕水花，并由立于渠边的垂直直立水尺标出真实水位高差对照。", bold_prefix="• 逆流之水必有源头与参照：")
    add_p("巨大的承重横梁脱离榫座悬浮在半空。画面必须清晰露出梁端完好的榫头公头、柱头上对应的空榫眼母槽，以及悬梁在下层墙壁上投下的真实物理阴影。", bold_prefix="• 脱榫之梁必露榫头与空槽：")
    add_p("测候天仪的青铜铜环悬空倾斜自转。画面必须在旁边立有垂直铅锤以标定真正的“绝对垂直线”，反衬出天仪力场失控的物理扭曲感。", bold_prefix="• 断轴天仪必有垂直铅锤对照：")
    add_p("严禁使用满屏无逻辑的七彩魔法光斑、虚空能量漩涡或乱飞的无关浮石来敷衍失度，必须让玩家一眼看懂是具体哪处机关失去了力学平衡。", bold_prefix="• 拒绝廉价魔法光效：")

    add_h2("3. Quest 3 移动端 VR 性能与舒适性边界")
    add_p("单重视锥内几何三角形面数控制在 15 万面以内，Draw Call 控制在 180 批次以内。利用反 Z 字形绝壁转角的巨大山体凸出岩脊天然切断视线，实现零性能浪费的遮挡剔除（Occlusion Culling）。在悬崖绝壁边沿高速行进或转向时，准望匣自动开启【定目帘】（Comfort Vignette），以柔和外周暗角收束视野，确保玩家在百米高空绝不产生生理眩晕。", bold_prefix="• 性能与视界收束：")

    # ==================== 第一节：首章出口【崖端石门】实景截图与衔接规范 ====================
    add_h1("第一节：首章出口【崖端石门】实景截图与空间衔接规范")
    add_p("第二章《断衡悬道》的起点，必须与第一章《初醒》（Chapter01_Courtyard.unity）尾声的【崖端石门】在空间位置、建筑形制与材质纹理上达到 100% 绝对一致。以下为 Unity 工程中运行实景截图：")

    # Image 1: 35_石门立于崖端.jpg
    img_gate_path = r"d:\UnityProjects\NarutoVR\docs\visuals\20261119_courtyard_scene\实景\35_石门立于崖端.jpg"
    if os.path.exists(img_gate_path):
        doc.add_picture(img_gate_path, width=Inches(6.2))
        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r_cap = p_cap.add_run("图 1：第一章终点【崖端石门】正面实拍特写（立于突出山体的独立崖舌前端，门外即是万丈深谷）")
        r_cap.font.size = Pt(9.0)
        r_cap.font.italic = True
        r_cap.font.color.rgb = RGBColor(0x66, 0x66, 0x66)
    
    # Image 2: 17_阁后望崖端石门.jpg
    img_back_path = r"d:\UnityProjects\NarutoVR\docs\visuals\20261119_courtyard_scene\实景\17_阁后望崖端石门.jpg"
    if os.path.exists(img_back_path):
        doc.add_picture(img_back_path, width=Inches(6.2))
        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r_cap = p_cap.add_run("图 2：穿出天仪阁木门望向崖端石门（石砌通道直通悬崖孤门，两侧地势骤然落入虚空）")
        r_cap.font.size = Pt(9.0)
        r_cap.font.italic = True
        r_cap.font.color.rgb = RGBColor(0x66, 0x66, 0x66)

    # Image 3: 18_崖上回望天仪阁.jpg & 36_全院与崖舌总览.jpg (side by side in table)
    img_lookback = r"d:\UnityProjects\NarutoVR\docs\visuals\20261119_courtyard_scene\实景\18_崖上回望天仪阁.jpg"
    img_overview = r"d:\UnityProjects\NarutoVR\docs\visuals\20261119_courtyard_scene\实景\36_全院与崖舌总览.jpg"
    if os.path.exists(img_lookback) and os.path.exists(img_overview):
        t_pair = doc.add_table(rows=1, cols=2)
        t_pair.alignment = WD_TABLE_ALIGNMENT.CENTER
        t_pair.columns[0].width = Inches(3.2)
        t_pair.columns[1].width = Inches(3.2)
        c0 = t_pair.rows[0].cells[0]
        c1 = t_pair.rows[0].cells[1]
        c0.paragraphs[0].add_run().add_picture(img_lookback, width=Inches(3.1))
        p0 = c0.add_paragraph()
        p0.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r0 = p0.add_run("图 3：站在石门前回望山间校院与天仪阁\n（清晰印证庭院地处全山脉最高峰）")
        r0.font.size = Pt(8.5)
        r0.font.italic = True
        
        c1.paragraphs[0].add_run().add_picture(img_overview, width=Inches(3.1))
        p1 = c1.add_paragraph()
        p1.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r1 = p1.add_run("图 4：全院与崖舌全景俯瞰鸟瞰\n（石门位于突出山脊最前沿，后方均为深谷）")
        r1.font.size = Pt(8.5)
        r1.font.italic = True

    add_h2("衔接关键特征与设计契约清单")
    add_p("石门框采用整料青灰玄武岩巨石凿制，沉实稳重；横向门楣上整块镶嵌汉白冷玉，表面阴刻先秦云雷纹与工矩度量方圆铭文；立柱两侧嵌入加固青铜轴套与门臼，泛出深绿氧化铜斑。", bold_prefix="1. 门框与门楣形制：")
    add_p("双扇对掩的厚重青灰石闸（厚度达 30cm），表面整饬带凹槽线，正中配有风化暗沉青铜铺首门环与重型铜质横向门闩构件。门扇紧闭，深深嵌在山壁之中。", bold_prefix="2. 门扇与铜件细节：")
    add_p("门内（第一章庭院侧）为平整规矩的 1.2 米青石方砖；门下为高出地面 10cm 的整条汉白玉门槛石；踏出门槛（第二章侧）立即变为嵌在悬崖岩体上的粗粝挑梁厚石板与外挑木栈龙骨，地面边缘带有清晰的人工斧凿裂痕，直临万丈虚空！", bold_prefix="3. 门槛与地面材质过渡（核心要点）：")
    add_p("第一章引导光点引至石门门槛；玩家踏过门槛进入第二章后，身后的厚重石闸轰鸣封闭；脚下门槛正下方无缝接续亮起明亮的荧光冰蓝导航路径线（Glowing Cyan-Blue Path Line），顺着悬崖边缘一路陡降向下！", bold_prefix="4. 荧光冰蓝动线接续：")

    # ==================== 第二节：第二章宏观空间走势：反 Z 字形顺壁下行 ====================
    add_h1("第二节：第二章宏观空间走势：反 Z 字形顺壁下行")
    add_p("基于第一章庭院坐落于全山脉最高峰（测候台高程约 +120m）的事实，第二章《断衡悬道》的整张地图宏观走势为自高向低顺壁向下盘旋下行（反 Z 字形阶梯折返），从海拔最高的山巅石门一路下探至海拔最低的地渊谷底（垂直落差达 200 米）：")

    # Topo Table
    t_topo = doc.add_table(rows=6, cols=4)
    t_topo.alignment = WD_TABLE_ALIGNMENT.CENTER
    for cell in t_topo.rows[0].cells:
        set_cell_background(cell, "2B6CB0")
    t_topo.columns[0].width = Inches(1.3)
    t_topo.columns[1].width = Inches(1.1)
    t_topo.columns[2].width = Inches(2.8)
    t_topo.columns[3].width = Inches(1.6)
    
    headers = ["区域编号与名称", "海拔垂直层级", "核心玩法交互与场景机制", "动线标记 [荧光冰蓝线]"]
    for i, h in enumerate(headers):
        p = t_topo.rows[0].cells[i].paragraphs[0]
        r = p.add_run(h)
        r.font.bold = True
        r.font.size = Pt(9.5)
        r.font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)
        
    topo_data = [
        ("区域 ①【出关门径台】\n(Zone 1 Gate Exit)", "最高处\n(海拔起点\n约 +120m)", "背倚第一章紧闭的雄伟石门，站在万丈高崖边沿的小石坪，居高临下俯瞰整个下行深渊与远山废墟。安全适应区与观澜点。", "蓝线自石门门槛延伸而出，顺着悬崖边挑梁石阶向下盘旋。"),
        ("区域 ②【断隘天堑台】\n(Zone 2 Broken Chasm)", "次高层\n(半空断崖\n约 +80m)", "挑梁栈道中断。面前横亘脱扣卡死、倾斜跨越深谷的巨型青铜渡槽天桥。拔出【墨规笔】在全息画板画 90° 直角矩，机关咬合，天桥轰鸣下沉接驳！", "蓝线引至画符台边缘；解开后蓝线穿过渡槽倾斜向下延伸。"),
        ("区域 ③【狭道阻截区】\n(Zone 3 Ruined Corridor)", "中层\n(山坳废墟\n约 +40m)", "穿过渡槽进入山体天然裂隙与落石废墟夹道。遭遇 3 具残机偶工奴伏击。运用胸前快推后退闪避并释放火核轰击核心。清场后破损闸门开启。", "战斗中蓝线暂时隐去，清场后重新亮起，穿过废墟栈道引向大转角。"),
        ("区域 ④【司衡配重台】\n(Zone 4 Balance Scale)", "中低层\n(转角露台\n约 0m)", "开阔转角大露台。架设巨型双臂青铜天平悬臂与砝码盘。玩家搬运或操控砝码配平两侧力矩，卡在半空的垂直升降笼轰鸣降下接驳！", "蓝线引向中央称重调校转轮；配平后蓝线延伸进入升降笼内部。"),
        ("区域 ⑤【地渊决战坪】\n(Zone 5 Abyss Arena)", "最低处\n(深渊谷底\n约 -80m)", "乘升降笼顺千仞光滑绝壁直降百米深入地渊谷底。开阔八角巨石祭坛，四周如环形铁壁。仰望可见极高处的石门与栈道。迎战首领【太仪·负衡偶】，战后重立平衡，遥望巨塔！", "步出升降笼踏入祭坛中心，决战打响；战毕蓝线指向前方裂谷尽头。")
    ]
    
    for row_idx, data in enumerate(topo_data, start=1):
        row = t_topo.rows[row_idx]
        bg = "FFFFFF" if row_idx % 2 != 0 else "F7FAFC"
        for col_idx, text in enumerate(data):
            cell = row.cells[col_idx]
            set_cell_background(cell, bg)
            set_cell_margins(cell, top=80, bottom=80, left=100, right=100)
            p = cell.paragraphs[0]
            p.paragraph_format.line_spacing = 1.15
            p.paragraph_format.space_after = Pt(2)
            r = p.add_run(text)
            r.font.size = Pt(8.5)

    doc.add_paragraph().paragraph_format.space_after = Pt(12)

    # ==================== 第三节：AI 概念出图提示词与全中文逐句注解 ====================
    add_h1("第三节：AI 概念出图提示词与全中文逐句结构化注解")
    add_p("以下提示词可直接复制粘贴至 Midjourney、Stable Diffusion、ChatGPT / DALL-E 3、Kling 或其他主流生图大模型中使用。每条提示词均严格按照下行地势与首章石门衔接标准构建，并配备详尽的中文段落解析。")

    # Prompt 1
    p1_en = (
        "Game level design map, top-down isometric view of a fantasy cliffside fortress, Z-shaped zigzag pathway gradually descending along a gigantic vertical precipice from top to bottom. \n\n"
        "Top section (Highest altitude): Zone 1 mountain entrance with a closed massive ancient Chinese stone gate embedded into the cliff, transitioning from paved stone to suspended cliffside planks. \n"
        "Upper-middle section: Zone 2 deep broken chasm with a severed suspended bronze aqueduct bridge and a holographic puzzle altar. \n"
        "Middle section: Zone 3 narrow ruined rocky corridor set inside a natural cliff crevice. \n"
        "Lower-middle section: Zone 4 mechanical balance scale terrace with giant bronze balance beams and a vertical hanging elevator shaft. \n"
        "Bottom section (Lowest abyss): Zone 5 deep abyss bottom with a massive octagonal stone battle arena surrounded by sheer cliff walls. \n\n"
        "A glowing neon cyan-blue navigation path line winds continuously from the top stone gate, down the cliffside steps and bridges, through the elevator, all the way to the lowest arena. \n"
        "Ancient Chinese Mohist punk architecture, bronze gears, floating suspended jade rocks, upward-flowing water droplets, slate grey cliff rocks, volumetric cyan haze, clean architectural blueprint concept art, readable spatial layout, 8k resolution, Unreal Engine 5 aesthetic. --ar 16:9"
    )
    p1_an = [
        ("构图主体与下行拓扑", "游戏关卡设计图，俯视等轴测视角，奇幻悬崖机巧要塞。之字形阶梯通道从画面顶部（最高处）向画面底部（最低处）逐渐顺着千仞绝壁向下下行。"),
        ("顶部（最高海拔·区域①）", "山巅入口，一扇紧闭的厚重中国古典石闸深深嵌入绝壁岩体，地面由平整铺石过渡为向外挑空的绝壁悬空木石栈道。（严格衔接首章出口石门实景）。"),
        ("次高层（区域②）", "深渊断裂天堑，半空中横亘脱扣断开卡死的巨型青铜复道渡槽天桥与发光全息画板祭坛（墨规笔画符操作台）。"),
        ("中层（区域③）", "嵌在天然崖壁裂隙与碎石废墟中的狭窄防卫走廊（残机偶遭遇战区）。"),
        ("中低层（区域④）", "机械天平配重高台，架设巨型青铜天平悬臂与垂直悬吊升降井架（力矩解谜与垂直升降梯）。"),
        ("谷底最低深渊（区域⑤）", "深邃地渊底部，一座被千仞垂直铁壁环绕的巨型八角石坪决战祭坛。"),
        ("荧光冰蓝动线全景贯穿", "一条明亮发光的荧光冰蓝色导航动线，自顶部石门起，顺着崖壁台阶、天桥、穿过垂直升降梯，一路盘旋向下延伸至最底部的决战坪。"),
        ("材质风格与渲染参数", "中国古代墨家机巧工程风格，青铜齿轮，浮空悬石，逆流向上的失度水滴，青灰色冷硬岩石，蓝绿体积雾，清晰可读的建筑蓝图概念图，8K 超清，UE5 渲染质感，16:9 宽屏。")
    ]
    add_prompt_box("提示词 1：自上而下全景俯视等轴测动线蓝图 (Top-Down Isometric Flow Blueprint)", p1_en, p1_an)

    # Prompt 2
    p2_en = (
        "First-person VR concept art from high elevation, standing just outside a massive closed ancient Chinese stone gate on a high cliff terrace, looking downwards across a dizzying vertical gorge. \n\n"
        "Behind the player: the towering mountain stone gate with white jade lintel, weathered dark bronze hinges, and square slate threshold, clearly matching the courtyard exit. \n"
        "In the immediate foreground: the stone path transitions to cantilevered bronze and stone cliffside stairs winding steeply downwards. A bright glowing neon cyan-blue guide line begins at the stone gate threshold and leads downwards along the edge. \n"
        "In the middle ground below: looking down into the deep foggy abyss, a disconnected ancient bronze aqueduct bridge is suspended mid-air between severed cliffs. Next to it stands a glowing holographic cinnabar-inscribed drawing altar waiting for activation. \n"
        "Distant background: misty abyss, distant mountain silhouettes below the clouds, inverted waterfall droplets drifting upwards into the cold sky. Cinematic depth of field, atmospheric lighting, Chinese Mohist craftsmanship, epic scale, 8k resolution. --ar 16:9"
    )
    p2_an = [
        ("视点与沉浸情境", "第一人称 VR 概念图，处于极高海拔。玩家刚站在高崖外侧的小石坪上，背靠紧闭的厚重山体石闸，居高临下俯视令人眩晕的万丈纵深峡谷。"),
        ("玩家身后（石门衔接验证）", "巍峨高耸的山壁石闸，汉白玉门楣、风化暗青铜门轴与门臼、整齐平整的青石门槛——外貌材质与第一章校规庭终点石门（实景截图35号）完全吻合，让人一眼看出正是从该门迈步而出。"),
        ("脚下前景（地面与动线）", "平整的石路过渡为外挑式青铜加固的悬崖栈道与陡峭向下的石阶。一条明亮发光的荧光冰蓝引导线自石门门槛正下方延伸而出，顺着悬崖边缘一路陡降向下。"),
        ("下方中景（断桥解谜台）", "俯视浓雾弥漫的深渊，半空中横亘着因大脱扣断裂卡死的大型青铜渡槽天桥；天桥旁立着发光的朱砂磁墨全息画板祭坛，等待玩家下去激活。"),
        ("远景与失度环境氛围", "雾气蒸腾的深渊谷底，云海之下若隐若现的远山轮廓，倒流的水滴反向飘向冷冽的天空。电影级景深，氛围光照，墨家工巧质感，宏大高差落差，8K 分辨率。")
    ]
    add_prompt_box("提示词 2：区域 ①~② 崖门出口与下行断堑俯瞰视角 (Zone 1-2 Gate Exit & Descent View)", p2_en, p2_an)

    # Prompt 3
    p3_en = (
        "Cinematic concept art, wide perspective standing on a mid-level cliffside terrace of an ancient Chinese mechanical installation, looking down into a deep subterranean canyon. \n\n"
        "Center foreground: Zone 4 giant Mohist bronze balance beam scale mechanism with hanging counterweight plates, engraved calibration dials, and heavy bronze chains. A bright neon cyan-blue path line leads to the central calibration wheel. \n"
        "Vertical connection: an immense open-frame bronze elevator cage suspended by massive woven copper cables, descending straight down into the deep vertical abyss along a sheer rock face. \n"
        "Deep background (Abyss floor): Zone 5 far below at the bottom of the chasm, an expansive octagonal stone battle arena illuminated by dim subterranean vents and faint cyan runic light, waiting for the elevator's arrival. \n"
        "Sheer vertical cliff walls rising up into misty clouds on both sides, industrial Mohist machinery, dramatic vertical sense of scale, volumetric fog, Unreal Engine 5 render, 8k resolution. --ar 16:9"
    )
    p3_an = [
        ("构图视角与场面设计", "电影级概念设计图，广角透视。立于中国古代工匠机械设施的中层悬崖露台上，向下俯视幽深的地底大峡谷。"),
        ("前景中心（区域④机械天平）", "区域 ④ 巨型墨家青铜天平悬臂机构，带有悬垂的巨大砝码铜盘、阴刻刻度盘与粗壮青铜铁索。荧光冰蓝动线引向中央称重调校转轮。"),
        ("纵向垂直联通（升降机）", "一台由巨型绞织青铜缆绳悬吊的敞开式青铜升降吊笼，贴着万仞光滑绝壁笔直垂直向下延伸进无底深渊。"),
        ("深谷深处远景（区域⑤决战坪）", "远在深渊底部的区域 ⑤，展现一座开阔的八角形青石决战祭坛，受地脉微光与青冷符文微光映照，等待升降机平稳降落接驳。"),
        ("周围环境与工业宏大感", "两侧千仞绝壁笔直耸入云雾，古法机械工业感，极致的纵向高差尺度，体积雾效，UE5 渲染质感，8K 分辨率。")
    ]
    add_prompt_box("提示词 3：区域 ④~⑤ 司衡配重台与直降深渊决战坪透视 (Zone 4-5 Balance Scale & Descent)", p3_en, p3_an)

    # ==================== 第四节：概念图视觉参考附录 ====================
    add_h1("第四节：概念图视觉参考附录 (Visual Concepts Archive)")
    add_p("以下为上一轮生成的概念设计参考图件（已妥善归档至 docs/visuals/20261121_chapter02_level_flow/）：")

    concepts = [
        (r"d:\UnityProjects\NarutoVR\docs\visuals\20261121_chapter02_level_flow\chapter2_level_flow_blueprint.jpg", "图 5：宏观等轴测关卡动线概念总览（展现 5 大区域与荧光蓝线走势）"),
        (r"d:\UnityProjects\NarutoVR\docs\visuals\20261121_chapter02_level_flow\chapter2_zone2_glyph_puzzle.jpg", "图 6：区域 ② 断隘天堑与墨规笔画符第一人称视角（断裂天桥与半透明画板）"),
        (r"d:\UnityProjects\NarutoVR\docs\visuals\20261121_chapter02_level_flow\chapter2_zone4_balance_scale.jpg", "图 7：区域 ④ 司衡配重台机械天平与升降浮岛（青铜悬臂与力矩平衡构件）")
    ]
    for c_path, c_caption in concepts:
        if os.path.exists(c_path):
            doc.add_picture(c_path, width=Inches(6.0))
            p_cap = doc.add_paragraph()
            p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
            r_cap = p_cap.add_run(c_caption)
            r_cap.font.size = Pt(9.0)
            r_cap.font.italic = True
            r_cap.font.color.rgb = RGBColor(0x66, 0x66, 0x66)

    # Save documents
    out_path1 = r"d:\UnityProjects\NarutoVR\docs\architecture\第二章_断衡悬道\考工律度_第二章关卡动线与出图指南.docx"
    out_path2 = r"d:\UnityProjects\NarutoVR\docs\visuals\20261121_chapter02_level_flow\考工律度_第二章关卡动线与出图指南.docx"
    
    os.makedirs(os.path.dirname(out_path1), exist_ok=True)
    os.makedirs(os.path.dirname(out_path2), exist_ok=True)
    
    doc.save(out_path1)
    doc.save(out_path2)
    print("Document successfully created and saved to:")
    print("1.", out_path1)
    print("2.", out_path2)

if __name__ == "__main__":
    create_document()
