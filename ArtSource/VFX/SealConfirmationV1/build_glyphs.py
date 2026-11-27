"""Extend the original Lv monoline lettering system; deterministic, original vector art.

This is game lettering inspired by seal-script construction, not a paleographic facsimile.
The golden shading follows D:/Temp/LvConfirm-20261121a/make.py (the original project source).
No existing Lv asset is written. Run from any directory with Python, numpy, scipy, Pillow, cv2.
"""
from pathlib import Path
import json
import re
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import gaussian_filter, distance_transform_edt, shift

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DEST = ROOT / 'Assets/Art/VFX/Skills/SealConfirmation'
PROOF = ROOT / 'Logs/t01-seal-feedback-20261124'
W, H, S = 480, 576, 3

# Original cubic/line centreline paths in the same 250 x 320 design space as Lv.
# Simplified component structure deliberately keeps the five silhouettes separate.
GLYPHS = {
    'Gui': ('规', [
        'M 35 99 C 54 97 76 97 95 99',
        'M 29 145 C 51 143 77 143 103 145',
        'M 66 49 C 68 107 67 159 59 194 C 53 225 39 251 27 267',
        'M 64 180 C 77 202 89 224 104 241',
        'M 127 193 L 127 70 C 127 57 135 50 149 50 L 192 50 C 207 50 214 58 214 73 L 214 193',
        'M 129 97 L 211 97',
        'M 129 143 L 211 143',
        'M 129 190 L 211 190',
        'M 151 193 C 153 221 141 252 116 274',
        'M 185 194 L 185 250 C 185 273 198 278 220 266 L 226 252',
    ]),
    'Ju': ('矩', [
        'M 57 48 C 53 71 41 92 30 108',
        'M 50 76 L 107 76',
        'M 28 137 L 110 137',
        'M 74 78 C 74 130 72 170 63 204 C 56 235 42 261 28 277',
        'M 67 184 C 83 205 98 232 111 252',
        'M 216 52 L 139 52 L 139 275 L 221 275',
        'M 141 112 L 211 112 L 211 204 L 141 204',
    ]),
    'Zhun': ('准', [
        'M 34 73 C 45 75 53 85 60 99',
        'M 30 232 C 44 213 55 189 64 165',
        'M 129 45 C 120 75 105 103 84 124',
        'M 109 98 L 109 273',
        'M 176 43 C 183 55 189 67 190 78',
        'M 115 100 L 224 100',
        'M 170 104 L 170 269',
        'M 112 154 L 214 154',
        'M 112 210 L 214 210',
        'M 111 271 L 225 271',
    ]),
    'Sheng': ('绳', [
        'M 69 43 C 61 69 47 91 31 109 L 73 113',
        'M 93 93 C 78 128 58 159 31 180 C 50 180 73 179 94 172',
        'M 68 183 L 68 276',
        'M 42 215 C 40 235 35 254 29 266',
        'M 91 214 C 97 231 103 245 108 257',
        'M 132 139 L 132 77 C 132 61 145 53 173 53 C 200 53 215 61 215 78 L 215 139 L 132 139',
        'M 134 97 L 212 97',
        'M 174 55 L 174 240 C 174 266 191 281 220 266 L 226 252',
        'M 132 243 L 132 182 L 215 182 L 215 243 L 132 243',
        'M 134 212 L 212 212',
    ]),
    'Heng': ('衡', [
        'M 58 48 C 53 68 41 89 24 103',
        'M 66 109 C 57 135 41 154 24 167',
        'M 52 147 L 52 275',
        'M 204 60 L 236 60',
        'M 200 127 L 240 127',
        'M 220 130 L 220 267 C 218 282 205 286 197 276',
        'M 128 40 C 117 58 103 75 89 86',
        'M 116 60 L 167 60 C 163 76 153 90 142 103',
        'M 94 108 L 181 108 L 181 194 L 94 194 L 94 108',
        'M 96 151 L 178 151',
        'M 137 110 L 137 195',
        'M 87 228 L 185 228',
        'M 137 204 C 137 239 119 265 91 280',
        'M 138 230 C 150 249 168 266 187 278',
    ]),
}

def points(path):
    tokens = re.findall(r'[MLC]|-?\d+(?:\.\d+)?', path)
    i, current, result = 0, None, []
    while i < len(tokens):
        op = tokens[i]; i += 1
        count = 6 if op == 'C' else 2
        values = np.array([float(t) for t in tokens[i:i+count]]).reshape(-1, 2); i += count
        if op == 'M':
            current = values[0]; result.append(current)
        elif op == 'L':
            result.append(values[0]); current = values[0]
        else:
            b, c, d = values
            for t in np.linspace(0, 1, 50)[1:]:
                result.append((1-t)**3*current + 3*(1-t)**2*t*b + 3*(1-t)*t*t*c + t**3*d)
            current = d
    return np.array(result)

def render(paths, seed):
    mask = np.zeros((H*S, W*S), np.uint8)
    for path in paths:
        pts = np.rint((points(path) * 1.82 + np.array((10, 1))) * S).astype(np.int32)
        thick = round(6.4 * 1.82 * S)
        cv2.polylines(mask, [pts], False, 255, thick, cv2.LINE_AA)
        for p in (pts[0], pts[-1]):
            cv2.circle(mask, tuple(p), thick//2, 255, -1, cv2.LINE_AA)
    mask = cv2.resize(mask, (W,H), interpolation=cv2.INTER_AREA)/255.
    yy, xx = np.mgrid[:H,:W]
    noise = gaussian_filter(np.random.default_rng(seed).normal(size=(H,W)), 1.2)
    rgba = np.zeros((H,W,4))
    bell = lambda x: np.exp(-x*x/2)
    def over(rgb, alpha):
        a = np.clip(alpha,0,1)[...,None]
        rgba[:,:,:3] = np.asarray(rgb)*a + rgba[:,:,:3]*(1-a)
        rgba[:,:,3:] = a + rgba[:,:,3:]*(1-a)
    outside, inside = distance_transform_edt(mask<.5), distance_transform_edt(mask>.5)
    over([1.,.57,.14], .060*bell(outside/12)+.14*bell(outside/4.4))
    over([.38,.17,.052], .69*bell(outside/2.1))
    facet = np.clip(.52+.28*np.sin((xx*.37+yy)/53)+.20*np.sin((yy-xx*.6)/21)+noise*.065,0,1)
    low, high = np.array([.73,.40,.12]), np.array([1.,.89,.60])
    metal = low[None,None,:]+(high-low)[None,None,:]*facet[:,:,None]
    edgelight = np.clip(mask-shift(mask,(1.6,1.4),order=1),0,1)
    core = np.clip(inside/5.,0,1)*(.55+.32*bell((yy+xx*.24-226)/110))
    metal = metal*(1-core[:,:,None])+np.array([1.,.925,.73])*core[:,:,None]
    metal = metal*(1-edgelight[:,:,None])+np.array([1.,.97,.86])*edgelight[:,:,None]
    over(metal,mask*.99)
    out = np.zeros_like(rgba); out[:,:,3] = rgba[:,:,3]
    out[:,:,:3] = np.divide(rgba[:,:,:3],rgba[:,:,3:],out=np.zeros_like(rgba[:,:,:3]),where=rgba[:,:,3:]>.0001)
    return Image.fromarray(np.uint8(np.clip(out,0,1)*255+.5), 'RGBA')

def build():
    HERE.mkdir(parents=True,exist_ok=True); DEST.mkdir(parents=True,exist_ok=True); PROOF.mkdir(parents=True,exist_ok=True)
    for index,(key,(char,paths)) in enumerate(GLYPHS.items()):
        body='\n'.join(f'<path d="{p}"/>' for p in paths)
        (HERE/f'{key}.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 250 320"><title>{char} — original game lettering V1</title><g fill="none" stroke="#ffebba" stroke-width="6.4" stroke-linecap="round" stroke-linejoin="round">{body}</g></svg>',encoding='utf-8')
        render(paths,731+index).save(DEST/f'{key}Glyph.png')
    # Review board uses actual output textures, including untouched Lv reference.
    order=[('Gui','规'),('Ju','矩'),('Lv','律 · 原样板'),('Zhun','准'),('Sheng','绳'),('Heng','衡')]
    board=Image.new('RGB',(1440,770),(20,25,29)); d=ImageDraw.Draw(board)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
    d.text((30,18),'T-01 / 成印字影 V1  ·  实际导入贴图',font=font,fill='#e8d5aa')
    for i,(key,char) in enumerate(order):
        x=i*240
        d.text((x+44,74),char,font=font,fill='#f3e6cb')
        im=Image.open(DEST/f'{key}Glyph.png').convert('RGBA')
        board.paste(im.resize((200,240),Image.Resampling.LANCZOS),(x+20,123),im.resize((200,240),Image.Resampling.LANCZOS))
        for j,size in enumerate([96,64,48]):
            y=408+j*103; w=round(size*W/H); thumb=im.resize((w,size),Image.Resampling.LANCZOS)
            board.paste(thumb,(x+120-w//2,y),thumb)
            d.text((x+18,y+size//2-8),str(size),font=small,fill='#9eaaae')
        d.text((x+36,733), '待真人采样' if key in ['Zhun','Sheng'] else ('双手' if key!='Heng' else '左手'),font=small,fill='#b4bfc2')
    board.save(PROOF/'Glyph_Readability.png')
    (HERE/'glyph-design.json').write_text(json.dumps({k:{'character':v[0],'paths':v[1]} for k,v in GLYPHS.items()},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Built five original glyph textures and vector sources; Lv untouched.')

if __name__=='__main__': build()
