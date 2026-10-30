# -*- coding: utf-8 -*-
import os
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

def build_workbook():
    wb = openpyxl.Workbook()
    wb.remove(wb.active)  # remove default sheet

    # 12 Seals (Earthly Branches / 十二地支印)
    seals = ['子', '丑', '寅', '卯', '辰', '巳', '午', '未', '申', '酉', '戌', '亥']
    seals_en = ['Rat', 'Ox', 'Tiger', 'Rabbit', 'Dragon', 'Snake', 'Horse', 'Ram', 'Monkey', 'Rooster', 'Dog', 'Boar']

    fingers = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
    finger_names = {'Thumb': '拇指', 'Index': '食指', 'Middle': '中指', 'Ring': '无名指', 'Pinky': '小指'}
    shapes = ['Curl', 'Flexion', 'Abduction', 'Opposition']
    shape_names = {'Curl': '卷曲', 'Flexion': '屈曲', 'Abduction': '张开', 'Opposition': '对指距离'}

    # 45 features in exact inspector order
    features = []
    # Left hand (18 features)
    for f_idx, f in enumerate(fingers):
        for k_idx, s in enumerate(shapes):
            if f_idx == 4 and s == 'Abduction': continue
            if f_idx == 0 and s == 'Opposition': continue
            fid = f'L.{f}.{s}'
            unit = 'm (米)' if s == 'Opposition' else 'deg (度)'
            def_tol = 0.015 if s == 'Opposition' else 15
            cname = f'左手·{finger_names[f]}·{shape_names[s]}'
            cat = f'左手手指姿态'
            features.append((cat, cname, fid, unit, def_tol))

    # Right hand (18 features)
    for f_idx, f in enumerate(fingers):
        for k_idx, s in enumerate(shapes):
            if f_idx == 4 and s == 'Abduction': continue
            if f_idx == 0 and s == 'Opposition': continue
            fid = f'R.{f}.{s}'
            unit = 'm (米)' if s == 'Opposition' else 'deg (度)'
            def_tol = 0.015 if s == 'Opposition' else 15
            cname = f'右手·{finger_names[f]}·{shape_names[s]}'
            cat = f'右手手指姿态'
            features.append((cat, cname, fid, unit, def_tol))

    # Both hands (9 features)
    pairs = [
        ('双手相对空间几何', '双手腕间距', 'Both.WristDistance', 'm (米)', 0.015),
        ('双手相对空间几何', '双掌朝向夹角', 'Both.PalmAngle', 'deg (度)', 15),
        ('双手相对空间几何', '双食指尖间距', 'Both.IndexTipDistance', 'm (米)', 0.015),
        ('双手相对空间几何', '双中指尖间距', 'Both.MiddleTipDistance', 'm (米)', 0.015),
        ('双手相对空间几何', '双拇指尖间距', 'Both.ThumbTipDistance', 'm (米)', 0.015),
        ('双手相对空间几何', '双食指方向夹角', 'Both.IndexDirectionAngle', 'deg (度)', 15),
        ('双手相对空间几何', '右腕相对左腕·横向偏移', 'Both.RightWristInLeft.X', 'm (米)', 0.015),
        ('双手相对空间几何', '右腕相对左腕·纵向偏移', 'Both.RightWristInLeft.Y', 'm (米)', 0.015),
        ('双手相对空间几何', '右腕相对左腕·前后偏移', 'Both.RightWristInLeft.Z', 'm (米)', 0.015),
    ]
    features.extend(pairs)

    # Styles
    font_family = 'Microsoft YaHei'
    title_font = Font(name=font_family, size=14, bold=True, color='FFFFFF')
    subtitle_font = Font(name=font_family, size=10, italic=True, color='4A5568')
    sec_font = Font(name=font_family, size=11, bold=True, color='FFFFFF')
    header_font = Font(name=font_family, size=11, bold=True, color='FFFFFF')
    subhead_font = Font(name=font_family, size=10, bold=True, color='2D3748')
    bold_cell_font = Font(name=font_family, size=10, bold=True, color='1A202C')
    cell_font = Font(name=font_family, size=10, color='2D3748')

    fill_title = PatternFill(start_color='1A365D', end_color='1A365D', fill_type='solid') # Navy
    fill_sec = PatternFill(start_color='2B6CB0', end_color='2B6CB0', fill_type='solid')   # Slate blue
    fill_header = PatternFill(start_color='2C5282', end_color='2C5282', fill_type='solid') # Blue
    fill_subhead = PatternFill(start_color='EDF2F7', end_color='EDF2F7', fill_type='solid') # Light gray
    fill_lh = PatternFill(start_color='EBF8FF', end_color='EBF8FF', fill_type='solid')      # Pale blue
    fill_rh = PatternFill(start_color='F0FFF4', end_color='F0FFF4', fill_type='solid')      # Pale green
    fill_both = PatternFill(start_color='FAF5FF', end_color='FAF5FF', fill_type='solid')    # Pale purple
    fill_zebra = PatternFill(start_color='F7FAFC', end_color='F7FAFC', fill_type='solid')   # Zebra

    thin_border_side = Side(border_style='thin', color='CBD5E0')
    border_all = Border(left=thin_border_side, right=thin_border_side, top=thin_border_side, bottom=thin_border_side)
    thick_bottom = Border(bottom=Side(border_style='medium', color='718096'), left=thin_border_side, right=thin_border_side, top=thin_border_side)

    align_center = Alignment(horizontal='center', vertical='center', wrap_text=True)
    align_left = Alignment(horizontal='left', vertical='center')
    align_right = Alignment(horizontal='right', vertical='center')

    # =========================================================================
    # SHEET 1: 12印横坐标（纯12列矩阵式 - 各个配置字段展开为行）
    # =========================================================================
    ws1 = wb.create_sheet(title='十二印配置矩阵(12列)')
    ws1.views.sheetView[0].showGridLines = True

    # Title & Notice
    ws1.merge_cells('A1:Q1')
    ws1['A1'] = 'NarutoVR - 十二印手势识别配置表 (12印横坐标 / 非采集配置字段)'
    ws1['A1'].font = title_font
    ws1['A1'].fill = fill_title
    ws1['A1'].alignment = Alignment(horizontal='center', vertical='center')
    ws1.row_dimensions[1].height = 36

    ws1.merge_cells('A2:Q2')
    ws1['A2'] = '【说明】1. 本表仅录入“权重(Weight)”、“容差(Tolerance)”、“是否必须(Required)”及全局判别参数等配置字段；2. 手指目标角度与空间距离区间([Min, Max])属于实时采集/标定数值，不在此表中手动录入；3. 权重为0表示该印不校验此特征。'
    ws1['A2'].font = subtitle_font
    ws1['A2'].alignment = Alignment(horizontal='left', vertical='center', indent=1)
    ws1.row_dimensions[2].height = 24

    # Headers at Row 3
    headers1 = ['分类', '特征名称', '固定特征ID', '单位', '配置属性'] + [f'{s}印\n({en})' for s, en in zip(seals, seals_en)]
    for col_idx, h in enumerate(headers1, 1):
        cell = ws1.cell(row=3, column=col_idx, value=h)
        cell.font = header_font
        cell.fill = fill_header
        cell.alignment = align_center
        cell.border = border_all
    ws1.row_dimensions[3].height = 30

    cur_row = 4

    # Section 1: 全局判定配置 (SealProfile 全局参数)
    ws1.merge_cells(f'A{cur_row}:Q{cur_row}')
    sec1 = ws1.cell(row=cur_row, column=1, value='【一、印级全局判别配置参数】')
    sec1.font = sec_font
    sec1.fill = fill_sec
    sec1.alignment = Alignment(horizontal='left', vertical='center', indent=1)
    ws1.row_dimensions[cur_row].height = 24
    cur_row += 1

    globals_info = [
        ('全局判别条件', '加权匹配得分阈值', 'threshold', '0~1比例', '阈值(Threshold)', 0.8),
        ('全局判别条件', '有效特征覆盖率下限', 'minimumCoverage', '0~1比例', '覆盖率(Coverage)', 0.8),
        ('全局判别条件', '连续达标保持时间', 'holdSeconds', '秒 (s)', '保持秒数(HoldSec)', 0.07),
        ('全局判别条件', '允许低置信特征', 'allowLowConfidence', '布尔值', '允许低置信(LowConf)', '否 (False)'),
        ('全局判别条件', '已确认模板启用识别', 'calibrated', '布尔值', '启用识别(Calibrated)', '否 (草稿)'),
    ]

    for cat, name, fid, unit, prop, def_val in globals_info:
        ws1.cell(row=cur_row, column=1, value=cat).alignment = align_center
        ws1.cell(row=cur_row, column=2, value=name).alignment = align_left
        ws1.cell(row=cur_row, column=3, value=fid).alignment = align_left
        ws1.cell(row=cur_row, column=4, value=unit).alignment = align_center
        ws1.cell(row=cur_row, column=5, value=prop).alignment = align_center
        for c in range(1, 6):
            ws1.cell(row=cur_row, column=c).font = bold_cell_font
            ws1.cell(row=cur_row, column=c).border = border_all
            ws1.cell(row=cur_row, column=c).fill = fill_subhead
        
        for c_idx in range(6, 18):
            c_cell = ws1.cell(row=cur_row, column=c_idx, value=def_val)
            c_cell.font = cell_font
            c_cell.alignment = align_center
            c_cell.border = border_all
            c_cell.fill = fill_subhead
        ws1.row_dimensions[cur_row].height = 20
        cur_row += 1

    # Section 2: 45项特征规则配置
    ws1.merge_cells(f'A{cur_row}:Q{cur_row}')
    sec2 = ws1.cell(row=cur_row, column=1, value='【二、45项特征规则配置字段（非采集字段：权重、容差、是否必须）】')
    sec2.font = sec_font
    sec2.fill = fill_sec
    sec2.alignment = Alignment(horizontal='left', vertical='center', indent=1)
    ws1.row_dimensions[cur_row].height = 24
    cur_row += 1

    for feat_idx, (cat, cname, fid, unit, def_tol) in enumerate(features):
        row_start = cur_row
        cat_fill = fill_lh if '左手' in cat else (fill_rh if '右手' in cat else fill_both)
        
        # 3 rows per feature: 权重, 容差, 必须
        props = [
            ('权重 (Weight)', 1, '评分权重（0为禁用，>0表示参与评分比重）'),
            ('容差 (Tolerance)', def_tol, f'区间外线性扣分软容差（默认 {def_tol} {unit}）'),
            ('是否必须 (Required)', '否', '若为“是”，该特征丢失或被遮挡时直接一票否决')
        ]

        for p_idx, (p_name, p_def, p_tip) in enumerate(props):
            ws1.cell(row=cur_row, column=1, value=cat).alignment = align_center
            ws1.cell(row=cur_row, column=2, value=cname).alignment = align_left
            ws1.cell(row=cur_row, column=3, value=fid).alignment = align_left
            ws1.cell(row=cur_row, column=4, value=unit).alignment = align_center
            
            p_cell = ws1.cell(row=cur_row, column=5, value=p_name)
            p_cell.alignment = align_center
            p_cell.font = bold_cell_font

            # Style metadata cells
            for c in range(1, 5):
                cell = ws1.cell(row=cur_row, column=c)
                cell.font = cell_font
                cell.fill = cat_fill
                cell.border = border_all
            p_cell.fill = fill_zebra if feat_idx % 2 == 1 else PatternFill(fill_type=None)
            p_cell.border = border_all

            # Fill 12 seal columns with defaults
            for seal_col in range(6, 18):
                val_cell = ws1.cell(row=cur_row, column=seal_col, value=p_def)
                val_cell.font = cell_font
                val_cell.alignment = align_center
                val_cell.border = border_all
                if feat_idx % 2 == 1:
                    val_cell.fill = fill_zebra

            ws1.row_dimensions[cur_row].height = 19
            cur_row += 1

        # Merge vertical metadata columns for the 3 rows
        ws1.merge_cells(start_row=row_start, start_column=1, end_row=cur_row-1, end_column=1)
        ws1.merge_cells(start_row=row_start, start_column=2, end_row=cur_row-1, end_column=2)
        ws1.merge_cells(start_row=row_start, start_column=3, end_row=cur_row-1, end_column=3)
        ws1.merge_cells(start_row=row_start, start_column=4, end_row=cur_row-1, end_column=4)

    # Freeze panes at E4 (freeze metadata columns & headers)
    ws1.freeze_panes = 'F4'

    # Auto-adjust column widths
    ws1.column_dimensions['A'].width = 18
    ws1.column_dimensions['B'].width = 24
    ws1.column_dimensions['C'].width = 26
    ws1.column_dimensions['D'].width = 12
    ws1.column_dimensions['E'].width = 20
    for c in range(6, 18):
        ws1.column_dimensions[get_column_letter(c)].width = 13


    # =========================================================================
    # SHEET 2: 12印横向展开宽表（45行特征，每印展开[权重|容差|必须]3小列）
    # =========================================================================
    ws2 = wb.create_sheet(title='十二印展开宽表(分组视图)')
    ws2.views.sheetView[0].showGridLines = True

    # Title
    ws2.merge_cells('A1:AO1')
    ws2['A1'] = 'NarutoVR - 十二印手势识别配置宽表 (45特征纵坐标 × 12印36维配置横坐标)'
    ws2['A1'].font = title_font
    ws2['A1'].fill = fill_title
    ws2['A1'].alignment = Alignment(horizontal='center', vertical='center')
    ws2.row_dimensions[1].height = 36

    # Row 2 & 3: Double header
    ws2.merge_cells('A2:A3'); ws2['A2'] = '序号'
    ws2.merge_cells('B2:B3'); ws2['B2'] = '分类'
    ws2.merge_cells('C2:C3'); ws2['C2'] = '特征中文名称'
    ws2.merge_cells('D2:D3'); ws2['D2'] = '固定特征ID'
    ws2.merge_cells('E2:E3'); ws2['E2'] = '单位'
    ws2.merge_cells('F2:F3'); ws2['F2'] = '默认容差'

    for c in ['A', 'B', 'C', 'D', 'E', 'F']:
        cell = ws2[f'{c}2']
        cell.font = header_font
        cell.fill = fill_header
        cell.alignment = align_center
        cell.border = border_all
        ws2[f'{c}3'].border = border_all

    sub_labels = ['权重', '容差', '必须?']
    for s_idx, (s, en) in enumerate(zip(seals, seals_en)):
        start_c = 7 + s_idx * 3
        end_c = start_c + 2
        ws2.merge_cells(start_row=2, start_column=start_c, end_row=2, end_column=end_c)
        s_cell = ws2.cell(row=2, column=start_c, value=f'{s}印 ({en})')
        s_cell.font = header_font
        s_cell.fill = fill_sec if s_idx % 2 == 0 else fill_header
        s_cell.alignment = align_center
        s_cell.border = border_all
        ws2.cell(row=2, column=start_c+1).border = border_all
        ws2.cell(row=2, column=start_c+2).border = border_all

        for sub_i, sub_name in enumerate(sub_labels):
            sub_col = start_c + sub_i
            scell = ws2.cell(row=3, column=sub_col, value=sub_name)
            scell.font = subhead_font
            scell.fill = fill_subhead
            scell.alignment = align_center
            scell.border = border_all

    ws2.row_dimensions[2].height = 22
    ws2.row_dimensions[3].height = 20

    for i, (cat, cname, fid, unit, def_tol) in enumerate(features, 1):
        r = 3 + i
        cat_fill = fill_lh if '左手' in cat else (fill_rh if '右手' in cat else fill_both)
        
        ws2.cell(row=r, column=1, value=i).alignment = align_center
        ws2.cell(row=r, column=2, value=cat).alignment = align_center
        ws2.cell(row=r, column=3, value=cname).alignment = align_left
        ws2.cell(row=r, column=4, value=fid).alignment = align_left
        ws2.cell(row=r, column=5, value=unit).alignment = align_center
        ws2.cell(row=r, column=6, value=def_tol).alignment = align_center

        for c in range(1, 7):
            cell = ws2.cell(row=r, column=c)
            cell.font = cell_font
            cell.fill = cat_fill
            cell.border = border_all

        for s_idx in range(12):
            start_c = 7 + s_idx * 3
            # Weight default 1
            w_cell = ws2.cell(row=r, column=start_c, value=1)
            w_cell.alignment = align_center
            w_cell.font = cell_font
            w_cell.border = border_all

            # Tol default def_tol
            t_cell = ws2.cell(row=r, column=start_c+1, value=def_tol)
            t_cell.alignment = align_center
            t_cell.font = cell_font
            t_cell.border = border_all

            # Required default 否
            req_cell = ws2.cell(row=r, column=start_c+2, value='否')
            req_cell.alignment = align_center
            req_cell.font = cell_font
            req_cell.border = border_all

            if i % 2 == 0:
                w_cell.fill = fill_zebra
                t_cell.fill = fill_zebra
                req_cell.fill = fill_zebra

        ws2.row_dimensions[r].height = 19

    ws2.freeze_panes = 'G4'

    ws2.column_dimensions['A'].width = 6
    ws2.column_dimensions['B'].width = 16
    ws2.column_dimensions['C'].width = 24
    ws2.column_dimensions['D'].width = 25
    ws2.column_dimensions['E'].width = 10
    ws2.column_dimensions['F'].width = 10
    for c in range(7, 43):
        ws2.column_dimensions[get_column_letter(c)].width = 8.5

    # =========================================================================
    # SHEET 3: 字段字典与设计规范说明
    # =========================================================================
    ws3 = wb.create_sheet(title='配置字典与设计规范说明')
    ws3.views.sheetView[0].showGridLines = True

    ws3.merge_cells('A1:F1')
    ws3['A1'] = 'NarutoVR - 十二印手势识别特征字典与参数设计规范'
    ws3['A1'].font = title_font
    ws3['A1'].fill = fill_title
    ws3['A1'].alignment = Alignment(horizontal='center', vertical='center')
    ws3.row_dimensions[1].height = 36

    doc_rows = [
        ('1. 为什么“角度和距离数值”不需要在配置表手动录入？', 
         '在结印识别系统中，每一个印对应的手指弯曲角度、掌心朝向、指尖间距等存在极大的个体手型差异。\n'
         '系统采用“录制样本 -> 百分位数自动提取区间”的标定机制（SealTemplateFromRecording）。\n'
         '当玩家录制一个印时，算法自动采集高置信帧计算 10%~90% 分位数得出 [Min, Max] 目标区间。\n'
         '因此，距离和角度属于【实时采集/标定数值】，无需在 Excel 手工填写真实距离度数。'),

        ('2. Excel 配置表中的核心非采集参数是什么？',
         '本 Excel 专用于策划和算法工程师调参，主要配置以下核心字段：\n'
         '• 权重 (weight)：该特征在判别时的重要程度（0 表示禁用，>0 表示权重分母比重）。\n'
         '• 容差 (tolerance)：超出 [min, max] 区间后，线性扣分降至 0 的软缓冲范围（角度默认 15°，距离默认 0.015 米/1.5 厘米）。\n'
         '• 是否必须 (required)：一票否决权。若为“是”，一旦该特征被遮挡丢失（Missing），直接拒绝识别此印。\n'
         '• 阈值 (threshold)：综合得分及格线，加权匹配度达到多少确认及格（默认 0.8 / 80%）。\n'
         '• 覆盖率 (minimumCoverage)：允许被遮挡的特征上限，有效特征占总权重的比例必须大于该值（默认 0.8）。\n'
         '• 保持时间 (holdSeconds)：单帧达标后需持续保持的时间，避免换印过程中的瞬态误触发（默认 0.07 秒）。'),

        ('3. 45 项特征构成明细与物理意义',
         '• 左手手指姿态 (18项)：5指卷曲(Curl 0~180°)、5指屈曲(Flexion 0~180°)、4指张开(Abduction 0~90°)、4指对指距离(Opposition 0~0.2m)。\n'
         '• 右手手指姿态 (18项)：同左手镜像，提供右手对应各关节姿态指标。\n'
         '• 双手相对空间几何 (9项)：双手腕间距(WristDistance)、双掌法线朝向夹角(PalmAngle 0~180°)、双食指尖间距、双中指尖间距、双拇指尖间距、双食指方向夹角(IndexDirectionAngle)、右腕相对于左腕坐标系的3轴空间偏移(X横向/Y纵向/Z前后)。\n'
         '注：Inspector 面板中显示的“Max”实际是 Oculus SDK 中枚举与 Pinky(小指) 共享值的别名，本质即为小指。'),

        ('4. 典型印的特征权重配置指导原则',
         '• 寅印 (虎印)：双手中指与食指直立并拢，其余手指内扣互握。\n'
         '  -> 核心必须特征：双手食指中指的 Curl（必须靠近 0° 直立）、双手食指尖间距（必须靠近 0m）。\n'
         '• 巳印 (蛇印)：双手十指交叉完全紧扣。\n'
         '  -> 核心特征：双手腕间距极近、双掌朝向夹角靠近 180° 或贴合，各指弯曲度权重极高。\n'
         '• 未印 (羊印)：双手食指与拇指伸展相贴，其余手指握紧。\n'
         '  -> 核心特征：双食指与双拇指 TipDistance 权重拉满，无关手指权重可适度调低。')
    ]

    r3 = 3
    for title, desc in doc_rows:
        ws3.merge_cells(f'A{r3}:F{r3}')
        t_cell = ws3.cell(row=r3, column=1, value=title)
        t_cell.font = sec_font
        t_cell.fill = fill_sec
        t_cell.alignment = Alignment(horizontal='left', vertical='center', indent=1)
        ws3.row_dimensions[r3].height = 24
        r3 += 1

        ws3.merge_cells(f'A{r3}:F{r3}')
        d_cell = ws3.cell(row=r3, column=1, value=desc)
        d_cell.font = cell_font
        d_cell.alignment = Alignment(horizontal='left', vertical='top', wrap_text=True)
        d_cell.border = border_all
        line_count = desc.count('\n') + 2
        ws3.row_dimensions[r3].height = line_count * 18
        r3 += 2

    ws3.column_dimensions['A'].width = 120

    # Save to files
    out_dir = r'd:\UnityProjects\NarutoVR\docs'
    os.makedirs(out_dir, exist_ok=True)
    out_path1 = os.path.join(out_dir, 'NarutoVR_12Seals_FeatureConfig.xlsx')
    out_path2 = r'd:\UnityProjects\NarutoVR\NarutoVR_12Seals_FeatureConfig.xlsx'

    wb.save(out_path1)
    wb.save(out_path2)
    print(f'Successfully generated {out_path1}')
    print(f'Successfully generated {out_path2}')

if __name__ == '__main__':
    build_workbook()
