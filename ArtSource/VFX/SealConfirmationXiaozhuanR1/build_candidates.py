"""Reproduce research-only typesetting from an unmodified, attributed font.

No Unity writes, no synthetic/edited glyph strokes, no fallback fonts for seals.
"""
from pathlib import Path
from datetime import datetime, timezone, timedelta
import hashlib
import json
import shutil
from xml.sax.saxutils import escape

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
LOG = ROOT / 'Logs/seal-confirmation-xiaozhuan-r1-20261125'
FONT = OUT / 'Source/chongxi_seal.otf'
ASSETS = ROOT / 'Assets/Art/VFX/Skills/SealConfirmation'
CHARS = [('Lv', '律', '鸣律'), ('Gui', '規', '合规'), ('Ju', '矩', '交矩'),
         ('Zhun', '準', '平准'), ('Sheng', '繩', '张绳'), ('Heng', '衡', '镇衡'),
         ('Ju_ShuowenJu', '巨', '《说文》本字'), ('Ju_ShuowenVariant', '榘', '《说文》重文')]
CREDIT = '崇羲篆體 — 王心怡、謝清俊、莊德明、季旭昇 / 中央研究院小學堂'
LICENSE = 'CC BY-ND 3.0 TW or later; https://xiaoxue.iis.sinica.edu.tw/chongxi/copyright.htm'


def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def json_out(p, value):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def label(draw, xy, value, size=24, fill=(45, 42, 35)):
    draw.text(xy, value, font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', size), fill=fill)


def place_ink(board, png, rect, color=(30, 29, 25), alpha=1.0):
    x, y, w, h = rect
    src = Image.open(png).convert('RGBA')
    src.thumbnail((w, h), Image.Resampling.LANCZOS)
    mask = src.getchannel('A')
    if alpha != 1:
        mask = mask.point(lambda v: round(v * alpha))
    if color is not None:
        src = Image.new('RGBA', src.size, (*color, 255))
    src.putalpha(mask)
    board.paste(src, (x + (w-src.width)//2, y + (h-src.height)//2), src)


def main():
    for folder in ['Candidates/ChongXi/PNG', 'Candidates/ChongXi/SVG', 'Comparison', 'OriginalSnapshots']:
        (OUT/folder).mkdir(parents=True, exist_ok=True)
    LOG.mkdir(parents=True, exist_ok=True)
    protected = [*ASSETS.glob('*'), *(ROOT/'Assets/ScriptableObjects/手印识别配置').glob('*印.asset')]
    baseline = {str(p.relative_to(ROOT)).replace('\\', '/'): digest(p) for p in protected if p.is_file()}
    baseline_path = LOG/'protected-assets-before.json'
    if not baseline_path.exists():
        json_out(baseline_path, baseline)
    font = TTFont(FONT)
    cmap = font.getBestCmap()
    glyphs = font.getGlyphSet()
    pil_font = ImageFont.truetype(str(FONT), 3072)
    rows = []
    for ident, char, meaning in CHARS:
        assert ord(char) in cmap, (char, 'missing: no fallback permitted')
        glyph_name = cmap[ord(char)]
        g = glyphs[glyph_name]
        bounds = BoundsPen(glyphs)
        g.draw(bounds)
        x0,y0,x1,y1 = bounds.bounds
        s = min(400/(x1-x0), 480/(y1-y0))
        tx = 240-s*(x0+x1)/2
        ty = 288+s*(y0+y1)/2
        pen = SVGPathPen(glyphs)
        g.draw(pen)
        commands = pen.getCommands()
        metadata = {'character': char, 'codepoint': f'U+{ord(char):04X}', 'glyph': glyph_name,
                    'fontSha256': digest(FONT), 'credit': CREDIT, 'license': LICENSE,
                    'outlineModification': 'none; uniform scale, y-axis inversion and translation only',
                    'status': 'research candidate; no Unity deployment'}
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="480" height="576" viewBox="0 0 480 576">\n'
               f'<title>{escape(char)} — {escape(CREDIT)}</title>\n'
               f'<metadata>{escape(json.dumps(metadata,ensure_ascii=False))}</metadata>\n'
               f'<g transform="matrix({s:.12f} 0 0 {-s:.12f} {tx:.12f} {ty:.12f})">'
               f'<path fill="white" d="{commands}"/></g>\n</svg>\n')
        svg_path = OUT/f'Candidates/ChongXi/SVG/{ident}Glyph.svg'
        svg_path.write_text(svg, encoding='utf-8')
        # Ordinary unmodified font typesetting. Only uniform placement and raster antialiasing.
        bb = pil_font.getbbox(char)
        raster = Image.new('L', (bb[2]-bb[0]+8, bb[3]-bb[1]+8))
        ImageDraw.Draw(raster).text((4-bb[0],4-bb[1]),char,font=pil_font,fill=255)
        raster = raster.crop(raster.getbbox())
        scale = min(400/raster.width,480/raster.height)
        raster = raster.resize((round(raster.width*scale),round(raster.height*scale)),Image.Resampling.LANCZOS)
        mask = Image.new('L',(480,576))
        mask.paste(raster,((480-raster.width)//2,(576-raster.height)//2))
        png = Image.new('RGBA',(480,576),(255,255,255,0))
        png.putalpha(mask)
        png_path = OUT/f'Candidates/ChongXi/PNG/{ident}Glyph.png'
        png.save(png_path)
        a=np.asarray(mask)
        rows.append({**metadata,'id':ident,'meaning':meaning,'fontBounds':list(bounds.bounds),
                     'svgTransform':[s,0,0,-s,tx,ty],'outlineSha256':hashlib.sha256(commands.encode()).hexdigest(),
                     'png':str(png_path.relative_to(OUT)).replace('\\','/'),'pngSha256':digest(png_path),
                     'svg':str(svg_path.relative_to(OUT)).replace('\\','/'),'svgSha256':digest(svg_path),
                     'alphaBox':list(mask.getbbox()),'alphaNonzero':int(np.count_nonzero(a)),
                     'edgeAlphaZero':bool(not(a[0].any() or a[-1].any() or a[:,0].any() or a[:,-1].any()))})
        if ident in [v[0] for v in CHARS[:6]]:
            snapshot = OUT/f'OriginalSnapshots/{ident}Glyph.png'
            if not snapshot.exists():
                shutil.copy2(ASSETS/f'{ident}Glyph.png', snapshot)
    # Large six-character original/candidate proof. Modern labels are explicitly labels, not candidates.
    board=Image.new('RGB',(1740,1180),(244,240,229));d=ImageDraw.Draw(board)
    label(d,(30,18),'六印小篆研究候选：原字影 / 崇羲原轮廓排字',34)
    label(d,(30,68),'正式印名不变；規／準／繩使用本字；矩为该字库转写字，不冒称《说文》独立字头。',22)
    for i,(ident,char,meaning) in enumerate(CHARS[:6]):
        x=25+i*285
        label(d,(x+85,115),f'{char}  {meaning}',24)
        place_ink(board,OUT/f'OriginalSnapshots/{ident}Glyph.png',(x,160,270,330),None)
        place_ink(board,OUT/f'Candidates/ChongXi/PNG/{ident}Glyph.png',(x,565,270,340))
        for j,h in enumerate([96,64,48]):
            place_ink(board,OUT/f'Candidates/ChongXi/PNG/{ident}Glyph.png',(x+12+j*86,955,80,h))
            label(d,(x+16+j*86,1060),str(h)+'px',16)
    label(d,(30,515),'上：当前正式 Assets 原字影（历史原创篆意）',22)
    label(d,(30,915),'下：同一字库排字；末行按 96 / 64 / 48 像素高缩放，静态图不等于头显验收。',22)
    label(d,(30,1108),CREDIT,21)
    label(d,(30,1142),'CC BY-ND 3.0 TW or later · 字库原样；未增删、拉扁、加粗笔画 · 来源/授权边界见 README',19)
    board.save(OUT/'Comparison/SixSeal_Old_vs_ChongXi.png')
    single=Image.new('RGB',(1740,700),(244,240,229));d=ImageDraw.Draw(single)
    label(d,(28,18),'六印｜崇羲篆体原轮廓候选（未接入 Unity）',34)
    for i,(ident,char,meaning) in enumerate(CHARS[:6]):
        x=25+i*285
        label(d,(x+95,80),char,32)
        place_ink(single,OUT/f'Candidates/ChongXi/PNG/{ident}Glyph.png',(x,145,270,390))
        label(d,(x+75,560),meaning,25)
    label(d,(30,625),CREDIT,21)
    label(d,(30,662),'同一原字库 · 正式印名仍为 律／规／矩／准／绳／衡 · 矩/巨/榘见专页 · CC BY-ND 3.0 TW or later',19)
    single.save(OUT/'Comparison/SixSeal_ChongXi_FirstLook.png')
    ju=Image.new('RGB',(1500,960),(244,240,229));d=ImageDraw.Draw(ju)
    label(d,(30,20),'矩印不改名｜矩、巨、榘三者对照',36)
    for i,(ident,char,heading,note) in enumerate([
        ('Ju','矩','矩 U+77E9｜当前印名对应','崇羲字库收录的转写篆体；非《说文》独立字头'),
        ('Ju_ShuowenJu','巨','巨 U+5DE8｜《说文》本字','古义是画方工具；规巨即规矩；不是以“大”释印'),
        ('Ju_ShuowenVariant','榘','榘 U+6998｜《说文》重文','巨的或体，从木、矢；可作考据旁证，未改印名')]):
        x=30+i*490
        label(d,(x,92),heading,24)
        place_ink(ju,OUT/f'Candidates/ChongXi/PNG/{ident}Glyph.png',(x+35,150,400,500))
        label(d,(x,690),note[:20],21)
        label(d,(x,724),note[20:],21)
    label(d,(30,800),'字义依据：香港中文大学《汉语多功能字库》“矩”“巨”条；字形：崇羲篆体原字库，未拼字。',22)
    label(d,(30,840),'《说文》巨条同时解释规矩义与榘重文；矩篆体候选与原刻/原本字头证据需区别。',22)
    label(d,(30,885),CREDIT,21)
    label(d,(30,923),'CC BY-ND 3.0 TW or later · 三者只作清晰候选与字源比较；没有静默替换正式“矩印”。',20)
    ju.save(OUT/'Comparison/Ju_JuRadical_JuVariant.png')
    after={str(p.relative_to(ROOT)).replace('\\','/'):digest(p) for p in protected if p.is_file()}
    preserved=baseline==after
    assert preserved
    manifest={'generatedAt':datetime.now(timezone(timedelta(hours=8))).isoformat(),'status':'research candidates for parent review',
              'font':{'file':'Source/chongxi_seal.otf','sha256':digest(FONT),'bytes':FONT.stat().st_size,
                      'officialPublisher':'https://xiaoxue.iis.sinica.edu.tw/chongxi/',
                      'officialDownload':'https://xiaoxue.iis.sinica.edu.tw/chongxi/files/chongxi_seal.zip',
                      'downloadedFrom':'https://recatools.com/assets/fonts/chongxi-seal/chongxi_seal.otf',
                      'officialByteComparisonPerformed':False,'credit':CREDIT,'license':LICENSE},
              'glyphs':rows,'protectedAssetsUnchanged':preserved,'unityRun':False,'headsetRun':False}
    json_out(OUT/'candidate-manifest.json',manifest)
    json_out(LOG/'candidate-checks.json',{'fontSha256':digest(FONT),'glyphCount':len(rows),'allEightCmapPresent':True,
        'pngsAll480x576':True,'noFallback':True,'allEdgesTransparent':all(r['edgeAlphaZero'] for r in rows),
        'protectedFilesChecked':len(after),'beforeEqualsAfter':preserved,'unityRun':False,'headsetRun':False,
        'generatedAt':manifest['generatedAt']})
    print(json.dumps({'candidates':len(rows),'fontSha256':digest(FONT),'protected':len(after),'unchanged':preserved},ensure_ascii=False))


if __name__=='__main__':
    main()
