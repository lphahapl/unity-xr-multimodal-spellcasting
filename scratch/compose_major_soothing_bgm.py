# -*- coding: utf-8 -*-
"""
《考工律度》常驻普通场景 BGM · 大调舒缓明亮版
设计原则：
1. 大调五声 / 正宫调式 (C Major Pentatonic): 消除一切哀怨阴郁，注入开朗、温润、祥和的大三和弦色彩。
2. 舒缓流动 (Soothing Flow): BPM 60，如山涧清泉流过白玉石阶，连贯的古琴慢琶音与恬静旋律，告别生硬断续的孤立单音。
3. 明亮剔透 (Bright & Luminous): 汉白玉编磬晶莹点缀、古琴清朗高位泛音，高通滤波彻底切除低频浑浊。
4. 很小声的环境底色 (Subtle Background): 峰值 -10 dBFS，RMS -24 dBFS，安详柔和，与场景声景浑然一体。
"""

import numpy as np
import scipy.signal as signal
import scipy.io.wavfile as wavfile
import os

SR = 44100
BPM = 60
BEAT = 60.0 / BPM  # 1.0 秒一拍
TOTAL_BARS = 12
BAR_DUR = 4.0      # 4/4 拍，一小节 4.0 秒
TOTAL_DUR = TOTAL_BARS * BAR_DUR  # 48.0 秒

def create_soothing_reverb(audio, sr=44100, rt60=1.6, wet=0.22):
    """温润明亮的汉白玉殿堂混响，声尾舒缓透明，切除低频泥泞。"""
    sos_hp = signal.butter(2, 220, btype='highpass', fs=sr, output='sos')
    
    delays = [int(sr * d) for d in [0.021, 0.029, 0.037, 0.043]]
    gains = [10 ** (-3 * d / rt60) for d in [0.021, 0.029, 0.037, 0.043]]
    
    combs = np.zeros_like(audio)
    for d, g in zip(delays, gains):
        b = np.zeros(d + 1)
        b[0] = 1.0
        a = np.zeros(d + 1)
        a[0] = 1.0
        a[-1] = -g * 0.82
        combs += signal.lfilter(b, a, audio)
    combs /= len(delays)
    
    # 扩散器
    ap_delays = [int(sr * 0.005), int(sr * 0.0017)]
    out = combs
    for d in ap_delays:
        b = np.zeros(d + 1)
        b[0] = -0.6
        b[-1] = 1.0
        a = np.zeros(d + 1)
        a[0] = 1.0
        a[-1] = -0.6
        out = signal.lfilter(b, a, out)
        
    out = signal.sosfilt(sos_hp, out)
    delay_r = int(sr * 0.009)
    out_r = np.roll(out, delay_r)
    
    dry = audio * 0.85
    return np.column_stack([dry + out * wet, dry + out_r * wet])

# ==========================================
# 乐器模型
# ==========================================
def synth_guqin_note(freq, duration=2.5, amp=0.18, is_harmonic=False):
    """
    先秦古琴：舒缓流动的弦音。
    is_harmonic=True: 纯净高洁徽位泛音
    is_harmonic=False: 温润散音/按音，带微弱琴体共鸣与细致走手音
    """
    t = np.linspace(0, duration, int(SR * duration), endpoint=False)
    sound = np.zeros_like(t)
    
    if is_harmonic:
        # 泛音：基频纯，泛音通透，如晶莹露珠
        partials = [(1.0, 1.0, 1.8), (2.0, 0.35, 1.2), (3.0, 0.12, 0.8)]
        for p, a, dec in partials:
            f = freq * p
            if f < SR / 2:
                env = np.exp(-t / dec) * (1.0 - np.exp(-t / 0.002))
                sound += a * np.sin(2 * np.pi * f * t) * env
        # 极轻指甲切弦声
        sound += np.random.randn(len(t)) * np.exp(-t / 0.003) * 0.03
    else:
        # 按音/散音：温润丝弦，带微弱颤音
        harmonics = [
            (1.0, 1.0, 2.2), (2.0, 0.55, 1.6), (3.0, 0.28, 1.2),
            (4.0, 0.14, 0.8), (5.0, 0.06, 0.5)
        ]
        vib = 1.0 + 0.0015 * np.sin(2 * np.pi * 4.5 * t)
        phase_acc = 2 * np.pi * freq * np.cumsum(vib) / SR
        for h, a, dec in harmonics:
            if freq * h < SR / 2:
                env = np.exp(-t / dec) * (1.0 - np.exp(-t / 0.004))
                sound += a * np.sin(phase_acc * h) * env
        # 真实丝弦走手音 (微弱摩擦)
        friction = np.random.randn(len(t)) * np.exp(-t / 0.08) * (1.0 - np.exp(-t / 0.01)) * 0.04
        sound += friction
        
    return sound * amp

def synth_jade_chime(freq, duration=2.2, amp=0.15):
    """汉白玉编磬：明朗高洁的石声和音。"""
    t = np.linspace(0, duration, int(SR * duration), endpoint=False)
    modes = [
        (1.0, 1.0, 1.5), (2.0, 0.35, 1.1), (2.76, 0.18, 0.8), (4.15, 0.08, 0.5)
    ]
    sound = np.zeros_like(t)
    for r, a, dec in modes:
        f = freq * r
        if f < SR / 2:
            env = np.exp(-t / dec) * (1.0 - np.exp(-t / 0.0015))
            sound += a * np.sin(2 * np.pi * f * t) * env
    mallet = np.random.randn(len(t)) * np.exp(-t / 0.003) * 0.04
    sound += mallet
    return sound * amp

def synth_warm_ambient_bed(duration, sr=44100, amp=0.08):
    """
    极弱、温暖舒缓的先秦空气与大调五声和音垫底。
    纯净大三度色彩，彻底切除 200Hz 以下沉闷轰鸣。
    """
    t = np.linspace(0, duration, int(sr * duration), endpoint=False)
    # C大调温润和音 (C4: 261.6Hz, E4: 329.6Hz, G4: 392.0Hz)
    chord_freqs = [261.63, 329.63, 392.00]
    pad = np.zeros_like(t)
    for i, f in enumerate(chord_freqs):
        # 缓慢波动的温和正弦和声
        lfo = 0.8 + 0.2 * np.sin(2 * np.pi * 0.05 * t + i * 1.5)
        pad += np.sin(2 * np.pi * f * t) * lfo * 0.3
    
    # 极柔和滤波
    sos_bp = signal.butter(2, [240, 1200], btype='bandpass', fs=sr, output='sos')
    pad_filtered = signal.sosfilt(sos_bp, pad)
    
    # 头尾 3 秒平滑淡入淡出（确保无缝循环）
    fade_len = int(sr * 3.0)
    fade_in = np.linspace(0, 1, fade_len)
    fade_out = np.linspace(1, 0, fade_len)
    pad_filtered[:fade_len] *= fade_in
    pad_filtered[-fade_len:] *= fade_out
    return pad_filtered * amp

# ==========================================
# 乐曲编配：大调舒缓流淌
# ==========================================
def compose_bgm():
    total_samples = int(SR * TOTAL_DUR)
    mix = np.zeros(total_samples, dtype=np.float32)
    
    # 音高定义 (C Major Pentatonic)
    C3 = 130.81
    G3 = 196.00
    C4 = 261.63
    D4 = 293.66
    E4 = 329.63
    G4 = 392.00
    A4 = 440.00
    C5 = 523.25
    D5 = 587.33
    E5 = 659.26
    G5 = 783.99
    A5 = 880.00
    C6 = 1046.50
    
    # 1. 温暖舒缓的环境和音铺底
    print("Generating soothing ambient bed...")
    mix += synth_warm_ambient_bed(TOTAL_DUR, SR, amp=0.06)
    
    # 2. 乐曲事件序列 (BPM 60, 每小节 4.0s)
    # 风格：连贯舒缓的慢琶音流动 + 明亮玉磬点缀 + 空灵古琴泛音
    events = [
        # === 第 1-2 小节：晨曦初沐 · C 大调和润舒展 (0.0s ~ 8.0s) ===
        # 0.0s: 玉磬 C5 清澈点缀
        (0.2, 'jade', C5, 0.16),
        # 古琴连贯流水琶音 (C4 -> E4 -> G4 -> C5)
        (0.8, 'qin', C4, 0.14, False),
        (1.8, 'qin', E4, 0.13, False),
        (2.8, 'qin', G4, 0.13, False),
        (4.0, 'qin', C5, 0.15, True),  # 泛音 C5
        (5.2, 'qin', G4, 0.12, False),
        (6.4, 'jade', E5, 0.15),       # 玉磬 E5
        (7.2, 'qin', A4, 0.12, False),

        # === 第 3-4 小节：规矩安宁 · G / C 调和 (8.0s ~ 16.0s) ===
        (8.4, 'qin', G4, 0.13, False),
        (9.4, 'qin', D4, 0.12, False),
        (10.6, 'qin', G4, 0.14, True), # 泛音 G4
        (12.0, 'jade', G5, 0.16),      # 玉磬 G5 高洁明亮
        (12.8, 'qin', E4, 0.13, False),
        (14.0, 'qin', C4, 0.12, False),
        (15.0, 'qin', D4, 0.13, False),

        # === 第 5-6 小节：春水融融 · 舒缓明朗上扬 (16.0s ~ 24.0s) ===
        (16.4, 'jade', C5, 0.16),
        (17.2, 'qin', E4, 0.13, False),
        (18.2, 'qin', G4, 0.14, False),
        (19.4, 'qin', A4, 0.14, False),
        (20.4, 'qin', C5, 0.16, True), # 泛音 C5
        (21.6, 'qin', D5, 0.15, True), # 泛音 D5
        (22.8, 'jade', E5, 0.15),
        (23.6, 'qin', C5, 0.13, False),

        # === 第 7-8 小节：天工从容 · 恬淡闲适 (24.0s ~ 32.0s) ===
        (24.4, 'qin', G4, 0.14, False),
        (25.6, 'qin', E4, 0.13, False),
        (26.8, 'qin', G4, 0.14, True), # 泛音 G4
        (28.0, 'jade', C5, 0.15),
        (29.0, 'qin', A4, 0.13, False),
        (30.2, 'qin', G4, 0.12, False),
        (31.2, 'qin', E4, 0.13, False),

        # === 第 9-10 小节：高山流水 · 晶莹泛音点染 (32.0s ~ 40.0s) ===
        (32.4, 'jade', G5, 0.16),      # 玉磬 G5
        (33.2, 'qin', C5, 0.16, True), # 泛音 C5
        (34.4, 'qin', G4, 0.13, False),
        (35.6, 'qin', E5, 0.15, True), # 泛音 E5
        (36.8, 'jade', D5, 0.15),
        (37.6, 'qin', C5, 0.14, False),
        (38.8, 'qin', A4, 0.13, False),

        # === 第 11-12 小节：静水深流 · 温润归拢留白 (40.0s ~ 48.0s) ===
        (40.4, 'qin', G4, 0.14, False),
        (41.6, 'qin', E4, 0.13, False),
        (42.8, 'jade', C5, 0.15),      # 玉磬 C5
        (43.8, 'qin', C4, 0.14, False),
        (45.0, 'qin', G4, 0.14, True), # 泛音 G4
        # 46.0s ~ 48.0s 自然余韵留白，无缝循环衔接回 0.0s
    ]
    
    print("Synthesizing notes and jade chimes...")
    for ev in events:
        start_t = ev[0]
        inst = ev[1]
        freq = ev[2]
        amp = ev[3]
        
        if inst == 'qin':
            is_harm = ev[4] if len(ev) > 4 else False
            s = synth_guqin_note(freq, duration=2.5, amp=amp, is_harmonic=is_harm)
        elif inst == 'jade':
            s = synth_jade_chime(freq, duration=2.2, amp=amp)
        else:
            continue
            
        start_idx = int(start_t * SR)
        end_idx = min(start_idx + len(s), total_samples)
        valid_len = end_idx - start_idx
        mix[start_idx:end_idx] += s[:valid_len]
        
    print("Applying soothing pavilion stereo reverb...")
    stereo = create_soothing_reverb(mix, sr=SR, rt60=1.6, wet=0.20)
    
    # 3. 按照“很小声的 BGM”要求进行动态控制：
    # 峰值控制在 -10.5 dBFS (约 0.30)，整体柔和清澈，完全不压迫
    max_val = np.max(np.abs(stereo))
    if max_val > 0:
        target_peak = 0.30  # -10.45 dBFS
        stereo = stereo * (target_peak / max_val)
        
    # 头尾做 15ms 汉宁窗微淡化，实现真正的 100% 零爆音无缝平滑循环
    loop_fade = int(SR * 0.015)
    fade_in = 0.5 * (1 - np.cos(np.pi * np.arange(loop_fade) / loop_fade))
    fade_out = 0.5 * (1 + np.cos(np.pi * np.arange(loop_fade) / loop_fade))
    stereo[:loop_fade, 0] *= fade_in
    stereo[:loop_fade, 1] *= fade_in
    stereo[-loop_fade:, 0] *= fade_out
    stereo[-loop_fade:, 1] *= fade_out
    
    stereo_int16 = np.int16(np.clip(stereo, -1.0, 1.0) * 32767)
    
    # 输出路径
    out_paths = [
        r"d:\UnityProjects\NarutoVR\docs\audio\kaogong_bgm_soothing_major.wav",
        r"d:\UnityProjects\NarutoVR\Assets\SealCombat\Audio\kaogong_bgm_soothing_major.wav",
        r"C:\Users\鲁迅\.gemini\antigravity\brain\3081f072-31dc-4fc6-8b99-e4d4a469452b\kaogong_bgm_soothing_major.wav"
    ]
    
    for path in out_paths:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        wavfile.write(path, SR, stereo_int16)
        print("Exported to:", path, f"({len(stereo_int16)/SR:.1f}s, {os.path.getsize(path)} bytes)")

if __name__ == '__main__':
    compose_bgm()
