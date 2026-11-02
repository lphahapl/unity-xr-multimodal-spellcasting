# -*- coding: utf-8 -*-
import os, sys, docx, time
from docx.shared import Pt, Inches, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import parse_xml, OxmlElement
from docx.oxml.ns import nsdecls, qn

DOCS_DIR = r"d:\UnityProjects\NarutoVR\docs"
MASTER_MD = os.path.join(DOCS_DIR, "world", "KAO_GONG_LU_DU_WORLD_SETTING_ZH.md")
MASTER_DOCX = os.path.join(DOCS_DIR, "考工律度_世界观设定与机制全案.docx")

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

def compile_master_docx():
    print("Compiling Master DOCX from:", MASTER_MD)
    with open(MASTER_MD, "r", encoding="utf-8") as f:
        master_text = f.read()
        
    doc = docx.Document()
    
    # Styles
    styles = doc.styles
    normal_style = styles['Normal']
    normal_style.font.name = 'Microsoft YaHei'
    normal_style.font.size = Pt(10)
    normal_style.font.color.rgb = RGBColor(0x33, 0x33, 0x33)
    normal_style.paragraph_format.line_spacing = 1.25
    normal_style.paragraph_format.space_after = Pt(4)
    
    # Title
    t_p = doc.add_paragraph()
    t_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    t_p.paragraph_format.space_before = Pt(12)
    t_p.paragraph_format.space_after = Pt(4)
    t_run = t_p.add_run("《考工律度》\n世界观设定与全系统需求规范全案")
    t_run.font.size = Pt(22)
    t_run.font.bold = True
    t_run.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
    
    sub_p = doc.add_paragraph()
    sub_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    sub_p.paragraph_format.space_after = Pt(18)
    sub_run = sub_p.add_run("Celestial Metrics: Master Worldbuilding Bible & System Specifications (V1.5 Revised 2026-11-02)")
    sub_run.font.size = Pt(10)
    sub_run.font.italic = True
    sub_run.font.color.rgb = RGBColor(0x66, 0x66, 0x66)
    
    lines = master_text.split('\n')
    in_table = False
    table_rows = []
    
    for line in lines:
        sline = line.strip()
        if not sline:
            continue
        if sline.startswith('# '):
            in_table = False
            h = doc.add_paragraph()
            h.paragraph_format.space_before = Pt(14)
            h.paragraph_format.space_after = Pt(6)
            run = h.add_run(sline[2:])
            run.font.size = Pt(16)
            run.font.bold = True
            run.font.color.rgb = RGBColor(0x1B, 0x36, 0x5D)
        elif sline.startswith('### '):
            in_table = False
            h = doc.add_paragraph()
            h.paragraph_format.space_before = Pt(10)
            h.paragraph_format.space_after = Pt(4)
            run = h.add_run(sline[4:])
            run.font.size = Pt(12)
            run.font.bold = True
            run.font.color.rgb = RGBColor(0x8B, 0x25, 0x00)
        elif sline.startswith('> '):
            in_table = False
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Inches(0.3)
            p.paragraph_format.space_before = Pt(2)
            p.paragraph_format.space_after = Pt(2)
            run = p.add_run(sline[2:])
            run.font.italic = True
            run.font.color.rgb = RGBColor(0x4A, 0x55, 0x68)
        elif sline.startswith('|') and sline.endswith('|'):
            if '---' in sline:
                continue
            cols = [c.strip() for c in sline.split('|')[1:-1]]
            if not in_table:
                in_table = True
                table_rows = [cols]
            else:
                table_rows.append(cols)
        else:
            if in_table:
                # Render table
                tbl = doc.add_table(rows=len(table_rows), cols=len(table_rows[0]))
                tbl.autofit = True
                for r_idx, row in enumerate(table_rows):
                    for c_idx, cell_text in enumerate(row):
                        cell = tbl.cell(r_idx, c_idx)
                        cell.text = cell_text.replace('<br>', '\n')
                        set_cell_margins(cell, 80, 80, 120, 120)
                        if r_idx == 0:
                            set_cell_shading(cell, 'EAECEE')
                            for cp in cell.paragraphs:
                                for cr in cp.runs:
                                    cr.font.bold = True
                                    cr.font.size = Pt(9.5)
                        else:
                            if r_idx % 2 == 1:
                                set_cell_shading(cell, 'F8F9FA')
                            for cp in cell.paragraphs:
                                for cr in cp.runs:
                                    cr.font.size = Pt(9)
                p_spacer = doc.add_paragraph()
                p_spacer.paragraph_format.space_after = Pt(4)
                in_table = False
                table_rows = []
            
            p = doc.add_paragraph()
            p.paragraph_format.space_after = Pt(3)
            run = p.add_run(sline)
            run.font.size = Pt(10)

    # Attempt to save
    saved = False
    for attempt in range(3):
        try:
            doc.save(MASTER_DOCX)
            print("Successfully saved master docx to:", MASTER_DOCX)
            saved = True
            break
        except Exception as e:
            print(f"Save attempt {attempt+1} failed ({e}), retrying in 1s...")
            time.sleep(1)
            
    if not saved:
        alt_docx = os.path.join(DOCS_DIR, "考工律度_世界观设定与机制全案_更新候选.docx")
        doc.save(alt_docx)
        print("Master docx is locked by another program. Saved to candidate file:", alt_docx)

compile_master_docx()
