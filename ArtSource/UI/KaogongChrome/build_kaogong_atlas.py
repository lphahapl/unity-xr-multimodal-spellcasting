import json
import os
from collections import deque
import numpy as np
from PIL import Image


ROOT = os.path.dirname(os.path.abspath(__file__))
SLICED_DIR = os.path.join(ROOT, "Sliced")
ATLAS_OUT = os.path.abspath(
    os.path.join(ROOT, "..", "..", "..", "Assets", "Art", "UI", "Resources", "KaogongUI", "KaogongUIAtlas.png")
)
MANIFEST_OUT = os.path.join(ROOT, "KaogongUIAtlas.manifest.json")


def key_dark_exterior(rgba: np.ndarray, bg_max: int = 8, feather_hi: int = 52) -> np.ndarray:
    """Flood-fill near-black pixels ONLY from crop border so dark interior panels stay solid."""
    h, w = rgba.shape[:2]
    rgb = rgba[:, :, :3].astype(np.float32)
    lum = rgb.max(axis=2)

    visited = np.zeros((h, w), dtype=bool)
    q = deque()
    for x in range(w):
        if lum[0, x] <= bg_max:
            visited[0, x] = True
            q.append((0, x))
        if lum[h - 1, x] <= bg_max and not visited[h - 1, x]:
            visited[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(h):
        if lum[y, 0] <= bg_max and not visited[y, 0]:
            visited[y, 0] = True
            q.append((y, 0))
        if lum[y, w - 1] <= bg_max and not visited[y, w - 1]:
            visited[y, w - 1] = True
            q.append((y, w - 1))

    while q:
        cy, cx = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and lum[ny, nx] <= bg_max:
                visited[ny, nx] = True
                q.append((ny, nx))

    # Build exterior soft boundary (3 px dilation into low-lum pixels adjacent to visited)
    alpha = np.full((h, w), 255.0, dtype=np.float32)
    alpha[visited] = 0.0

    dil = visited.copy()
    for _ in range(3):
        nxt = dil.copy()
        nxt[1:, :] |= dil[:-1, :]
        nxt[:-1, :] |= dil[1:, :]
        nxt[:, 1:] |= dil[:, :-1]
        nxt[:, :-1] |= dil[:, 1:]
        dil = nxt

    fringe = dil & (~visited) & (lum < feather_hi)
    # Softly ramp alpha on outer edge pixels and un-premultiply slight black halo
    t = np.clip((lum[fringe] - bg_max) / float(max(1, feather_hi - bg_max)), 0.0, 1.0)
    alpha[fringe] = t * 255.0
    safe_a = np.maximum(t, 0.22)[:, None]
    rgb[fringe] = np.clip(rgb[fringe] / safe_a, 0.0, 255.0)

    out = rgba.copy()
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = np.clip(alpha, 0, 255).astype(np.uint8)
    return out


def key_white_background(rgba: np.ndarray, key_inner_white: bool = True) -> np.ndarray:
    """Remove solid white background (and optionally pure hollow neutral white holes inside rings)."""
    h, w = rgba.shape[:2]
    rgb = rgba[:, :, :3].astype(np.float32)
    cmin = rgb.min(axis=2)
    cmax = rgb.max(axis=2)
    chroma = cmax - cmin

    # Pixels considered pure background white
    is_bg = (cmin >= 242) & (chroma <= 10)

    visited = np.zeros((h, w), dtype=bool)
    q = deque()
    for x in range(w):
        if is_bg[0, x]:
            visited[0, x] = True
            q.append((0, x))
        if is_bg[h - 1, x] and not visited[h - 1, x]:
            visited[h - 1, x] = True
            q.append((h - 1, x))
    for y in range(h):
        if is_bg[y, 0] and not visited[y, 0]:
            visited[y, 0] = True
            q.append((y, 0))
        if is_bg[y, w - 1] and not visited[y, w - 1]:
            visited[y, w - 1] = True
            q.append((y, w - 1))

    while q:
        cy, cx = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and is_bg[ny, nx]:
                visited[ny, nx] = True
                q.append((ny, nx))

    mask = visited.copy()
    if key_inner_white:
        # Also punch out clearly neutral pure white holes inside rings/emblems
        inner_holes = (cmin >= 246) & (chroma <= 8)
        mask |= inner_holes

    # 2px boundary anti-aliasing & white de-halo unpremultiply
    dil = mask.copy()
    for _ in range(2):
        nxt = dil.copy()
        nxt[1:, :] |= dil[:-1, :]
        nxt[:-1, :] |= dil[1:, :]
        nxt[:, 1:] |= dil[:, :-1]
        nxt[:, :-1] |= dil[:, 1:]
        dil = nxt

    alpha = np.full((h, w), 255.0, dtype=np.float32)
    alpha[mask] = 0.0

    # Near-white edge fringe adjacent to transparent mask
    fringe = dil & (~mask) & (cmin >= 205) & (chroma <= 32)
    # Estimate coverage alpha: further from pure white = higher alpha
    dist_white = np.maximum(255.0 - cmin[fringe], chroma[fringe] * 1.4)
    a_est = np.clip(dist_white / 52.0, 0.0, 1.0)
    alpha[fringe] = a_est * 255.0
    # Unpremultiply RGB from white background: C_fg = (C_obs - (1 - a)*255) / a
    a_col = np.maximum(a_est, 0.15)[:, None]
    rgb[fringe] = np.clip((rgb[fringe] - (1.0 - a_est[:, None]) * 255.0) / a_col, 0.0, 255.0)

    out = rgba.copy()
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = np.clip(alpha, 0, 255).astype(np.uint8)
    return out


def tighten_alpha_crop(rgba: np.ndarray, pad: int = 2) -> np.ndarray:
    ys, xs = np.where(rgba[:, :, 3] > 6)
    if len(ys) == 0:
        return rgba
    y0 = max(0, int(ys.min()) - pad)
    y1 = min(rgba.shape[0], int(ys.max()) + 1 + pad)
    x0 = max(0, int(xs.min()) - pad)
    x1 = min(rgba.shape[1], int(xs.max()) + 1 + pad)
    return rgba[y0:y1, x0:x1]


def collapse_uniform_9slice(rgba: np.ndarray, border_lr: int, border_tb: int, keep_mid: int = 128, blend: int = 20) -> tuple[np.ndarray, tuple[int, int, int, int]]:
    """
    Preserve exact 1:1 outer bevel & corner fretwork (up to border_lr / border_tb)
    while smoothly crossfading the flat middle span down to `keep_mid` pixels wide/tall.
    """
    h, w = rgba.shape[:2]
    target_w = min(w, border_lr * 2 + keep_mid)
    target_h = min(h, border_tb * 2 + keep_mid)

    def collapse_1d(img: np.ndarray, axis: int, b: int, tsize: int) -> np.ndarray:
        orig = img.shape[axis]
        if orig <= tsize:
            return img
        mid = tsize - 2 * b
        half_mid_l = mid // 2
        half_mid_r = mid - half_mid_l
        # Left slice [0 : b + half_mid_l + blend], Right slice [orig - (b + half_mid_r + blend) : orig]
        left_end = b + half_mid_l
        right_start = orig - (b + half_mid_r)
        if axis == 1:
            left_part = img[:, :left_end, :].astype(np.float32)
            right_part = img[:, right_start:, :].astype(np.float32)
            # Crossfade `blend` pixels around seam so even subtle gradient blends invisibly
            ov = min(blend, half_mid_l, half_mid_r)
            l_src = img[:, left_end - ov:left_end, :].astype(np.float32)
            r_src = img[:, right_start - ov:right_start, :].astype(np.float32)
            w_arr = np.linspace(0.0, 1.0, ov, dtype=np.float32)[None, :, None]
            seam_l = l_src * (1.0 - w_arr * 0.5) + r_src * (w_arr * 0.5)
            l_src2 = img[:, left_end:left_end + ov, :].astype(np.float32)
            r_src2 = img[:, right_start:right_start + ov, :].astype(np.float32)
            seam_r = l_src2 * (0.5 - w_arr * 0.5) + r_src2 * (0.5 + w_arr * 0.5)
            left_part[:, -ov:, :] = seam_l
            right_part[:, :ov, :] = seam_r
            return np.concatenate([left_part, right_part], axis=1).clip(0, 255).astype(np.uint8)
        else:
            top_part = img[:left_end, :, :].astype(np.float32)
            bot_part = img[right_start:, :, :].astype(np.float32)
            ov = min(blend, half_mid_l, half_mid_r)
            t_src = img[left_end - ov:left_end, :, :].astype(np.float32)
            b_src = img[right_start - ov:right_start, :, :].astype(np.float32)
            w_arr = np.linspace(0.0, 1.0, ov, dtype=np.float32)[:, None, None]
            seam_t = t_src * (1.0 - w_arr * 0.5) + b_src * (w_arr * 0.5)
            t_src2 = img[left_end:left_end + ov, :, :].astype(np.float32)
            b_src2 = img[right_start:right_start + ov, :, :].astype(np.float32)
            seam_b = t_src2 * (0.5 - w_arr * 0.5) + b_src2 * (0.5 + w_arr * 0.5)
            top_part[-ov:, :, :] = seam_t
            bot_part[:ov, :, :] = seam_b
            return np.concatenate([top_part, bot_part], axis=0).clip(0, 255).astype(np.uint8)

    out = collapse_1d(rgba, 1, border_lr, target_w)
    out = collapse_1d(out, 0, border_tb, target_h)
    return out, (border_lr, border_tb, border_lr, border_tb)


def normalize_square_icon(rgba: np.ndarray, max_dim: int = 320, pad: int = 10) -> np.ndarray:
    tight = tighten_alpha_crop(rgba, pad=2)
    h, w = tight.shape[:2]
    side = max(h, w) + pad * 2
    sq = np.zeros((side, side, 4), dtype=np.uint8)
    y0 = (side - h) // 2
    x0 = (side - w) // 2
    sq[y0:y0 + h, x0:x0 + w] = tight
    if side > max_dim:
        im = Image.fromarray(sq, "RGBA").resize((max_dim, max_dim), Image.Resampling.LANCZOS)
        return np.array(im)
    return sq


def main():
    os.makedirs(SLICED_DIR, exist_ok=True)
    os.makedirs(os.path.dirname(ATLAS_OUT), exist_ok=True)

    frames_arr = np.array(Image.open(os.path.join(ROOT, "ui_bronze_frames_master.png")).convert("RGBA"))
    emblems_arr = np.array(Image.open(os.path.join(ROOT, "ui_bronze_emblems_master.png")).convert("RGBA"))
    jade_arr = np.array(Image.open(os.path.join(ROOT, "ui_jade_dividers_master.png")).convert("RGBA"))

    sprites = []

    # 1. Frame_Bronze_Main
    fb_raw = key_dark_exterior(frames_arr[56:768, 60:1194], bg_max=8, feather_hi=52)
    fb_tight = tighten_alpha_crop(fb_raw, pad=0)
    fb_9, fb_border = collapse_uniform_9slice(fb_tight, 102, 102, keep_mid=132)
    sprites.append(("Frame_Bronze_Main", fb_9, fb_border))

    # 2. Button_Normal & 3. Button_Hover (exact matching crop dimensions, 9-slice preserving corner bevels)
    bn_raw = key_dark_exterior(frames_arr[868:1100, 56:598], bg_max=8, feather_hi=50)
    bn_tight = tighten_alpha_crop(bn_raw, pad=0)
    bw = int(round(bn_tight.shape[1] * 0.72))
    bh = int(round(bn_tight.shape[0] * 0.72))
    bn_resized = np.array(Image.fromarray(bn_tight, "RGBA").resize((bw, bh), Image.Resampling.LANCZOS))
    sprites.append(("Button_Normal", bn_resized, (42, 34, 42, 34)))

    bhov_raw = key_dark_exterior(frames_arr[868:1100, 656:1198], bg_max=8, feather_hi=50)
    bhov_tight = tighten_alpha_crop(bhov_raw, pad=0)
    bhov_resized = np.array(Image.fromarray(bhov_tight, "RGBA").resize((bw, bh), Image.Resampling.LANCZOS))
    sprites.append(("Button_Hover", bhov_resized, (42, 34, 42, 34)))

    # 4. Frame_Jade_Plaque
    fj_raw = key_dark_exterior(jade_arr[38:626, 46:1210], bg_max=8, feather_hi=54)
    fj_tight = tighten_alpha_crop(fj_raw, pad=0)
    fj_9, fj_border = collapse_uniform_9slice(fj_tight, 102, 102, keep_mid=132)
    sprites.append(("Frame_Jade_Plaque", fj_9, fj_border))

    # 5. Gauge_Track
    gt_raw = key_dark_exterior(jade_arr[704:800, 52:1058], bg_max=8, feather_hi=46)
    gt_tight = tighten_alpha_crop(gt_raw, pad=0)
    sprites.append(("Gauge_Track", gt_tight, (18, 18, 18, 18)))

    # 6. Gauge_Fill (with bronze caps) & Gauge_Fill_Inner (cap-free for radial/horizontal Filled)
    gf_raw = key_dark_exterior(jade_arr[844:920, 52:1058], bg_max=8, feather_hi=46)
    gf_tight = tighten_alpha_crop(gf_raw, pad=0)
    sprites.append(("Gauge_Fill", gf_tight, (18, 12, 18, 12)))
    gf_inner = gf_tight[6:-6, 18:-18].copy()
    sprites.append(("Gauge_Fill_Inner", gf_inner, (0, 0, 0, 0)))

    # 7. Divider_Gold
    dv_raw = key_dark_exterior(jade_arr[1054:1124, 54:1056], bg_max=8, feather_hi=44)
    dv_tight = tighten_alpha_crop(dv_raw, pad=0)
    sprites.append(("Divider_Gold", dv_tight, (0, 0, 0, 0)))

    # 8. Gauge_Vertical_Track
    gv_raw = key_dark_exterior(jade_arr[660:1190, 1116:1200], bg_max=8, feather_hi=46)
    gv_tight = tighten_alpha_crop(gv_raw, pad=0)
    sprites.append(("Gauge_Vertical_Track", gv_tight, (16, 18, 16, 18)))

    # 9-14: Emblems, Reticle, Astrolabe Ring (all on pure white bg)
    emblem_specs = [
        ("Icon_Seals", (140, 15, 430, 515), False, 280),
        ("Icon_GlyphBrush", (520, 35, 985, 500), True, 280),
        ("Icon_VoiceBell", (1035, 15, 1460, 515), True, 280),
        ("Icon_StatusWheel", (60, 535, 505, 965), True, 280),
        ("Reticle_Fangyuan", (545, 535, 980, 965), True, 280),
        ("Ring_Astrolabe", (1005, 510, 1485, 980), True, 420),
    ]
    for name, (x0, y0, x1, y1), inner_hole, dim in emblem_specs:
        crop = key_white_background(emblems_arr[y0:y1, x0:x1], key_inner_white=inner_hole)
        sq = normalize_square_icon(crop, max_dim=dim, pad=8)
        sprites.append((name, sq, (0, 0, 0, 0)))

    # Also synthesize a tiny 32x32 soft-feathered Gradient Scrim for subtitles (Subtitle_Smoke_Scrim)
    scrim_h, scrim_w = 64, 512
    xs = np.linspace(-1.0, 1.0, scrim_w, dtype=np.float32)[None, :]
    ys = np.linspace(-1.0, 1.0, scrim_h, dtype=np.float32)[:, None]
    hx = np.clip(1.0 - np.abs(xs) ** 2.4, 0.0, 1.0)
    vy = np.clip(1.0 - (np.abs(ys) ** 2.2), 0.0, 1.0)
    scrim_a = (hx * vy * 218.0).astype(np.uint8)
    scrim = np.zeros((scrim_h, scrim_w, 4), dtype=np.uint8)
    scrim[:, :, 0] = 20
    scrim[:, :, 1] = 27
    scrim[:, :, 2] = 25
    scrim[:, :, 3] = scrim_a
    sprites.append(("Subtitle_Smoke_Scrim", scrim, (180, 20, 180, 20)))

    # Save individual sliced previews for visual review
    for name, img, border in sprites:
        Image.fromarray(img, "RGBA").save(os.path.join(SLICED_DIR, f"{name}.png"))
        print(f"Sliced {name:24s}: {img.shape[1]}x{img.shape[0]}  border={border}")

    # Pack into single 2048x1024 Atlas using skyline bottom-left packer (power-of-two, optimal Quest ASTC footprint!)
    atlas_w = 2048
    atlas_h = 1024
    pad = 12
    atlas = np.zeros((atlas_h, atlas_w, 4), dtype=np.uint8)
    ordered = sorted(sprites, key=lambda s: -(s[1].shape[0] * s[1].shape[1]))

    skyline = np.full(atlas_w, pad, dtype=np.int32)
    manifest = {"atlasWidth": atlas_w, "atlasHeight": atlas_h, "sprites": []}

    for name, img, (bl, bb, br, bt) in ordered:
        sh, sw = img.shape[:2]
        need_w = sw + pad
        best_x = -1
        best_y = atlas_h
        for x in range(pad, atlas_w - need_w + 1, 4):
            y_top = int(skyline[x:x + need_w].max())
            if y_top + sh + pad <= atlas_h and y_top < best_y:
                best_y = y_top
                best_x = x
        if best_x < 0:
            raise RuntimeError(f"Atlas overflow placing {name}")

        cx, cy = best_x, best_y
        skyline[cx:cx + need_w] = cy + sh + pad

        # Extrude 2px border bleed to prevent bilinear filtering halo
        atlas[cy:cy + sh, cx:cx + sw] = img
        for e in range(1, 3):
            atlas[cy - e, cx:cx + sw] = img[0, :]
            atlas[cy + sh - 1 + e, cx:cx + sw] = img[-1, :]
            atlas[cy:cy + sh, cx - e] = img[:, 0]
            atlas[cy:cy + sh, cx + sw - 1 + e] = img[:, -1]

        # Unity SpriteRect coordinates have Y=0 at BOTTOM
        unity_y = atlas_h - (cy + sh)
        manifest["sprites"].append({
            "name": name,
            "x": cx,
            "y": unity_y,
            "w": sw,
            "h": sh,
            "borderLeft": bl,
            "borderBottom": bb,
            "borderRight": br,
            "borderTop": bt,
        })

    Image.fromarray(atlas, "RGBA").save(ATLAS_OUT)
    with open(MANIFEST_OUT, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(f"\nPacked {len(sprites)} sprites into {ATLAS_OUT} ({atlas_w}x{atlas_h}, max y {int(skyline.max())}/{atlas_h})")


if __name__ == "__main__":
    main()
