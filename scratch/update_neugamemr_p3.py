# -*- coding: utf-8 -*-
import os, sys, docx
from docx.shared import Pt, RGBColor
from docx.oxml import parse_xml, OxmlElement
from docx.oxml.ns import nsdecls, qn

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

# 1. Update docs/程序需求/考工律度_程序架构与交互底层技术需求规范.docx
doc_path = r'docs/程序需求/考工律度_程序架构与交互底层技术需求规范.docx'
doc = docx.Document(doc_path)

# Update Table 0
t0 = doc.tables[0]
c0 = t0.cell(0, 0)
txt0 = c0.text
if 'NeuGameMRTools' not in txt0:
    new_txt0 = txt0.replace(
        'P3 (定目帘/缩地/Timeline青铜铎对话)',
        'P3 (定目帘/缩地/Timeline青铜铎对话/NeuGameMRTools交互工具包适配)'
    )
    c0.text = new_txt0
    for p in c0.paragraphs:
        for r in p.runs:
            r.font.name = 'Microsoft YaHei'
            r.font.size = Pt(9.5)

# Update Table 3 (Priority Matrix)
t3 = doc.tables[3]
already_added = any('NeuGameMRTools' in row.cells[1].text for row in t3.rows)
if not already_added:
    new_row = t3.add_row()
    c_prio = new_row.cells[0]
    c_name = new_row.cells[1]
    c_desc = new_row.cells[2]
    c_sys = new_row.cells[3]
    
    c_prio.text = 'P3'
    c_name.text = 'NeuGameMRTools 交互工具包适配\nNeuGameMRToolsAdapter'
    c_desc.text = '【物理道具抓取与插槽系统】：适配 NeuGameMRTools 的近距离抓取 (GrabInteractionController/GrabWizard)、远距离拾取 (DistanceGrab) 与吸附插槽 (SnapZone/SnapZoneDisplayManager)。专用于工匠道具抓取配置（如墨规笔、拓片）、准望匣铜叶滑轨插装归位、场景远距工器取回与中文检查器向导。\n• 架构边界与技术约束：必须重构其 .asmdef 消除对废弃 SDK 85 程序集的硬依赖，兼容当前 Unity 6000.3.10f1 与 Meta XR SDK 207.0.1 (com.meta.xr.sdk.interaction.ovr)；仅作道具与插槽外围扩展，严禁侵入核心施法链路。'
    c_sys.text = 'GrabInteractionController.cs\nSnapZone.cs\nLinearRpgController.cs\nExclusiveInputBlocked'
    
    set_cell_shading(c_prio, 'F8F9FA')
    set_cell_shading(c_name, 'F8F9FA')
    set_cell_shading(c_desc, 'F8F9FA')
    set_cell_shading(c_sys, 'F8F9FA')
    
    for c in [c_prio, c_name, c_desc, c_sys]:
        set_cell_margins(c, 80, 80, 120, 120)
        for p in c.paragraphs:
            for r in p.runs:
                r.font.name = 'Microsoft YaHei'
                r.font.size = Pt(9)

doc.save(doc_path)
print('Updated docs/程序需求/考工律度_程序架构与交互底层技术需求规范.docx successfully!')
