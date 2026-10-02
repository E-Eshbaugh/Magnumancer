#!/usr/bin/env python3
"""
Synthesizes Magnumancer's procedural sound effects (the ones the Gamemaster gun pack
doesn't cover: footsteps, hit markers, kill dings, elemental casts, UI blips, fanfares...).

Pure standard library, deterministic (seeded), writes 16-bit mono 44.1 kHz WAVs into
Assets/Sounds/Magnumancer/. Re-run after tweaking a recipe:

    python3 Tools/Audio/synth_sfx.py            # all
    python3 Tools/Audio/synth_sfx.py heal ui_   # only names containing these

Unity picks up changed WAVs automatically (keep the .meta files so GUIDs stay stable).
"""
import hashlib
import math
import os
import random
import struct
import sys
import wave

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Sounds", "Magnumancer")
TAU = math.tau


# ---------------------------------------------------------------- building blocks

def silence(sec):
    return [0.0] * int(SR * sec)


def mix(dst, src, at=0.0, gain=1.0):
    """Adds src into dst starting at `at` seconds (dst grows if needed)."""
    o = int(at * SR)
    need = o + len(src)
    if need > len(dst):
        dst.extend([0.0] * (need - len(dst)))
    for i, v in enumerate(src):
        dst[o + i] += v * gain
    return dst


def env_ad(n, attack, decay_tau, hold=0.0):
    """Linear attack, optional hold, exponential decay (decay_tau in seconds)."""
    a = max(1, int(attack * SR))
    h = int(hold * SR)
    out = []
    for i in range(n):
        if i < a:
            out.append(i / a)
        elif i < a + h:
            out.append(1.0)
        else:
            out.append(math.exp(-(i - a - h) / (decay_tau * SR)))
    return out


def tone(freq, sec, shape="sine", attack=0.005, decay=0.2, hold=0.0, glide_to=None, vib=0.0, vib_rate=6.0, phase=0.0):
    """One oscillator note. freq may glide (exponentially) to glide_to over the note."""
    n = int(sec * SR)
    e = env_ad(n, attack, decay, hold)
    out = []
    ph = phase
    for i in range(n):
        t = i / n
        f = freq if glide_to is None else freq * (glide_to / freq) ** t
        if vib:
            f *= 1.0 + vib * math.sin(TAU * vib_rate * i / SR)
        ph += f / SR
        p = ph % 1.0
        if shape == "sine":
            s = math.sin(TAU * p)
        elif shape == "tri":
            s = 4 * abs(p - 0.5) - 1
        elif shape == "saw":
            s = 2 * p - 1
        elif shape == "square":
            s = 1.0 if p < 0.5 else -1.0
        else:
            raise ValueError(shape)
        out.append(s * e[i])
    return out


def noise(sec, rng, attack=0.002, decay=0.1, hold=0.0):
    n = int(sec * SR)
    e = env_ad(n, attack, decay, hold)
    return [(rng.random() * 2 - 1) * e[i] for i in range(n)]


def lowpass(x, cutoff, cutoff_end=None):
    """One-pole lowpass, cutoff can sweep linearly."""
    out, y = [], 0.0
    n = len(x)
    for i, v in enumerate(x):
        c = cutoff if cutoff_end is None else cutoff + (cutoff_end - cutoff) * i / max(1, n - 1)
        a = 1 - math.exp(-TAU * max(10.0, c) / SR)
        y += a * (v - y)
        out.append(y)
    return out


def highpass(x, cutoff):
    lp = lowpass(x, cutoff)
    return [a - b for a, b in zip(x, lp)]


def bandpass(x, center, q=1.0, center_end=None):
    """RBJ biquad bandpass (constant peak gain), center may sweep exponentially."""
    out = []
    x1 = x2 = y1 = y2 = 0.0
    n = len(x)
    b0 = b2 = a1 = a2 = 0.0
    for i, v in enumerate(x):
        if i % 32 == 0:
            f = center if center_end is None else center * (center_end / center) ** (i / max(1, n - 1))
            f = min(max(f, 20.0), SR * 0.45)
            w = TAU * f / SR
            alpha = math.sin(w) / (2 * q)
            a0 = 1 + alpha
            b0, b2 = alpha / a0, -alpha / a0
            a1, a2 = -2 * math.cos(w) / a0, (1 - alpha) / a0
        y = b0 * v + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, v
        y2, y1 = y1, y
        out.append(y)
    return out


def gain(x, g):
    return [v * g for v in x]


def drive(x, amount):
    """Soft saturation for grit/warmth."""
    k = max(1e-6, amount)
    norm = math.tanh(k)
    return [math.tanh(v * k) / norm for v in x]


def echo(x, delay, feedback, taps=4):
    out = list(x) + [0.0] * int(delay * SR * taps)
    d = int(delay * SR)
    g = 1.0
    for t in range(1, taps + 1):
        g *= feedback
        for i, v in enumerate(x):
            out[i + d * t] += v * g
    return out


def bell(freq, sec, rng=None, decay=0.6, bright=1.0):
    """Inharmonic struck-metal partials."""
    parts = [(1.0, 1.0), (2.0, 0.6), (2.76, 0.45 * bright), (5.4, 0.25 * bright), (8.93, 0.12 * bright)]
    out = silence(sec)
    for ratio, amp in parts:
        mix(out, tone(freq * ratio, sec, attack=0.002, decay=decay / math.sqrt(ratio)), 0, amp)
    return out


def note(name):
    """'C5' -> Hz (sharps only, e.g. 'F#4')."""
    names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
    pitch, octave = name[:-1], int(name[-1])
    semis = names.index(pitch) + 12 * (octave + 1) - 69
    return 440.0 * 2 ** (semis / 12)


def brass(freq, sec, attack=0.04, decay=0.6, hold=0.0, bright=2400):
    """Saw stack through a lowpass: a warm horn."""
    out = silence(sec)
    for det in (-0.004, 0.0, 0.005):
        mix(out, tone(freq * (1 + det), sec, "saw", attack, decay, hold), 0, 0.33)
    return lowpass(out, bright)


def finish(x, peak=0.89, fade=0.012):
    m = max(1e-9, max(abs(v) for v in x))
    x = [v * peak / m for v in x]
    if fade > 0:
        # trim trailing near-silence, then fade out whatever is left so nothing clicks
        end = len(x)
        while end > SR // 50 and abs(x[end - 1]) < 1e-4:
            end -= 1
        x = x[:end]
        f = min(int(fade * SR), len(x))
        for i in range(f):
            x[-1 - i] *= i / f
    return x


def write(name, x, peak=0.89, fade=0.012):
    x = finish(x, peak, fade)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in x))
    write_meta(path, name)
    print(f"  {name}.wav  {len(x) / SR:.2f}s")


def guid_for(name):
    """Stable GUID from the clip name, so regenerating never breaks references."""
    return hashlib.md5(("magnumancer-sfx/" + name).encode()).hexdigest()


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: {compression}
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_meta(path, name):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    # PCM for tiny rapid-fire clips (no decode cost), Vorbis for the rest
    compression = 0 if os.path.getsize(path) < 40000 else 1
    with open(meta, "w") as f:
        f.write(META.format(guid=guid_for(name), compression=compression))
    folder = OUT.rstrip("/") + ".meta"
    if not os.path.exists(folder):
        with open(folder, "w") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for("folder"))


# ---------------------------------------------------------------- recipes

RECIPES = {}


def recipe(fn):
    RECIPES[fn.__name__] = fn
    return fn


@recipe
def footsteps():
    for k in range(6):
        rng = random.Random(100 + k)
        x = silence(0.16)
        thump = tone(70 + rng.uniform(-10, 25), 0.12, attack=0.002, decay=0.025, glide_to=45)
        grit = bandpass(noise(0.12, rng, 0.001, 0.02), rng.uniform(900, 1700), 0.9)
        scuff = lowpass(noise(0.14, rng, 0.01, 0.04), 3200)
        mix(x, thump, 0, 0.9)
        mix(x, grit, 0.002, 0.7)
        mix(x, scuff, 0.02, 0.18)
        write(f"sfx_footstep_{k + 1:02d}", x, peak=0.7)


@recipe
def landings():
    for k in range(2):
        rng = random.Random(200 + k)
        x = silence(0.4)
        mix(x, tone(62 + 10 * k, 0.35, attack=0.002, decay=0.07, glide_to=38), 0, 1.0)
        mix(x, lowpass(noise(0.35, rng, 0.001, 0.06), 1800, 400), 0, 0.8)
        mix(x, bandpass(noise(0.3, rng, 0.02, 0.08), 2500, 0.7), 0.03, 0.15)   # dust settling
        write(f"sfx_land_{k + 1:02d}", x)


@recipe
def double_jump():
    rng = random.Random(300)
    x = silence(0.4)
    mix(x, bandpass(noise(0.3, rng, 0.005, 0.08), 600, 1.2, 3500), 0, 0.9)          # puff
    mix(x, tone(900, 0.32, "sine", 0.01, 0.1, glide_to=2200, vib=0.02, vib_rate=30), 0.01, 0.35)  # shimmer
    write("sfx_double_jump", x, peak=0.75)


@recipe
def hitmarker():
    rng = random.Random(400)
    x = silence(0.08)
    mix(x, tone(2900, 0.06, "sine", 0.0005, 0.012), 0, 0.8)
    mix(x, tone(4350, 0.05, "sine", 0.0005, 0.008), 0, 0.4)
    mix(x, highpass(noise(0.02, rng, 0.0002, 0.003), 3000), 0, 0.5)
    write("sfx_hitmarker", x, peak=0.8)


@recipe
def kill_confirm():
    x = silence(0.9)
    mix(x, bell(1318.5, 0.8, decay=0.45, bright=0.7), 0, 0.6)
    mix(x, bell(1975.5, 0.8, decay=0.4, bright=0.5), 0.07, 0.55)
    mix(x, tone(130, 0.15, attack=0.002, decay=0.04, glide_to=60), 0, 0.7)     # punch underneath
    write("sfx_kill_confirm", x)


@recipe
def death_soul():
    rng = random.Random(500)
    x = silence(1.1)
    mix(x, tone(880, 0.9, "tri", 0.01, 0.35, hold=0.2, glide_to=110, vib=0.04, vib_rate=11), 0.02, 0.5)
    mix(x, tone(660, 0.9, "sine", 0.01, 0.3, hold=0.2, glide_to=82, vib=0.03, vib_rate=7), 0.05, 0.35)
    mix(x, bandpass(noise(1.0, rng, 0.05, 0.3), 1800, 1.5, 300), 0, 0.6)       # soul whoosh leaving
    write("sfx_death_soul", echo(x, 0.11, 0.3, 3))


@recipe
def eliminated():
    x = silence(3.2)
    mix(x, bell(98, 3.0, decay=1.6, bright=0.9), 0, 1.0)
    mix(x, bell(146.8, 2.6, decay=1.2, bright=0.6), 0.01, 0.4)
    mix(x, tone(49, 1.5, attack=0.003, decay=0.5), 0, 0.6)
    write("sfx_eliminated", x)


@recipe
def respawn():
    rng = random.Random(600)
    x = silence(1.1)
    for i, n in enumerate(["C5", "E5", "G5", "C6", "E6"]):
        mix(x, tone(note(n), 0.6, "tri", 0.005, 0.25), i * 0.06, 0.32)
        mix(x, tone(note(n) * 2, 0.4, "sine", 0.005, 0.12), i * 0.06, 0.12)
    mix(x, highpass(noise(0.9, rng, 0.2, 0.25), 5000), 0, 0.25)                  # sparkle
    mix(x, tone(130.8, 0.8, "sine", 0.08, 0.3), 0, 0.4)
    write("sfx_respawn", echo(x, 0.09, 0.25, 3))


@recipe
def heal():
    x = silence(1.0)
    for i, n in enumerate(["G5", "B5", "D6", "G6"]):
        mix(x, tone(note(n), 0.7, "sine", 0.01, 0.3), i * 0.07, 0.35)
        mix(x, tone(note(n) * 3, 0.3, "sine", 0.005, 0.06), i * 0.07, 0.06)
    write("sfx_heal", echo(x, 0.12, 0.3, 3), peak=0.7)


@recipe
def ability_ready():
    x = silence(0.35)
    mix(x, tone(note("E6"), 0.2, "tri", 0.002, 0.06), 0, 0.5)
    mix(x, tone(note("B6"), 0.3, "tri", 0.002, 0.1), 0.07, 0.5)
    mix(x, tone(note("B6") * 2, 0.2, "sine", 0.002, 0.05), 0.07, 0.15)
    write("sfx_ability_ready", x, peak=0.6)


@recipe
def casts():
    # Frost: crystalline pings over a cold hiss
    rng = random.Random(700)
    x = silence(0.9)
    mix(x, bandpass(noise(0.7, rng, 0.02, 0.2), 6000, 2.0, 3000), 0, 0.5)
    for i in range(14):
        f = rng.uniform(2500, 7000)
        mix(x, tone(f, 0.25, "sine", 0.001, rng.uniform(0.03, 0.09)), rng.uniform(0, 0.4), rng.uniform(0.15, 0.35))
    mix(x, tone(1400, 0.4, "sine", 0.002, 0.15, glide_to=2800), 0, 0.2)
    write("sfx_cast_frost", x, peak=0.8)

    # Water: a splash (noise dropping in pitch) + bubbles
    rng = random.Random(701)
    x = silence(0.9)
    mix(x, lowpass(noise(0.6, rng, 0.003, 0.15), 6000, 500), 0, 0.9)
    mix(x, bandpass(noise(0.5, rng, 0.003, 0.12), 1200, 1.0, 300), 0, 0.6)
    for i in range(9):
        f = rng.uniform(250, 600)
        mix(x, tone(f, 0.08, "sine", 0.002, 0.03, glide_to=f * 2.5), 0.05 + rng.uniform(0, 0.45), 0.35)
    write("sfx_cast_water", x, peak=0.8)

    # Void: a dark detuned swell that sucks inward
    rng = random.Random(702)
    x = silence(1.0)
    swell = silence(0.8)
    for f in (55, 58.3, 82.4, 110.7):
        mix(swell, tone(f, 0.8, "saw", 0.5, 0.12), 0, 0.3)
    swell = lowpass(swell, 300, 1800)
    mix(x, swell, 0, 1.0)
    mix(x, bandpass(noise(0.8, rng, 0.55, 0.08), 400, 1.5, 3000), 0, 0.5)     # reverse whoosh
    mix(x, tone(220, 0.35, "sine", 0.002, 0.12, glide_to=40), 0.55, 0.8)     # the drop
    write("sfx_cast_void", drive(x, 1.6), peak=0.85)

    # Nature: woody knock + a pentatonic chime and leafy rustle
    rng = random.Random(703)
    x = silence(0.9)
    mix(x, bandpass(tone(180, 0.15, "tri", 0.001, 0.03), 900, 2.0), 0, 2.0)
    for i, n in enumerate(["D5", "E5", "A5", "B5"]):
        mix(x, tone(note(n), 0.5, "sine", 0.004, 0.18), 0.03 + i * 0.05, 0.25)
    mix(x, bandpass(noise(0.6, rng, 0.05, 0.12), 3500, 0.8), 0.02, 0.35)
    write("sfx_cast_nature", x, peak=0.75)

    # Earth: a heavy stone crack and rumble
    rng = random.Random(704)
    x = silence(1.0)
    mix(x, tone(55, 0.9, "sine", 0.004, 0.25, glide_to=35), 0, 1.0)
    mix(x, lowpass(noise(0.9, rng, 0.002, 0.25), 900, 150), 0, 1.0)
    for i in range(8):
        mix(x, bandpass(noise(0.05, rng, 0.0005, 0.012), rng.uniform(1500, 3500), 1.0), rng.uniform(0, 0.3), 0.5)
    write("sfx_cast_earth", drive(x, 1.5))

    # Arcane (no element): a bright upward zap
    rng = random.Random(705)
    x = silence(0.6)
    mix(x, tone(300, 0.45, "square", 0.003, 0.15, glide_to=1500), 0, 0.2)
    mix(x, tone(600, 0.45, "sine", 0.003, 0.15, glide_to=3000), 0, 0.4)
    mix(x, bandpass(noise(0.4, rng, 0.003, 0.1), 2000, 1.0, 6000), 0, 0.3)
    write("sfx_cast_arcane", lowpass(x, 7000), peak=0.75)


@recipe
def blink():
    rng = random.Random(800)
    x = silence(0.35)
    mix(x, tone(2400, 0.12, "sine", 0.001, 0.04, glide_to=300), 0, 0.6)
    mix(x, tone(300, 0.15, "sine", 0.001, 0.05, glide_to=2600), 0.08, 0.6)
    mix(x, bandpass(noise(0.25, rng, 0.002, 0.06), 3000, 1.0, 800), 0, 0.35)
    write("sfx_blink", x, peak=0.75)


@recipe
def ui():
    x = silence(0.05)
    mix(x, tone(1900, 0.04, "tri", 0.0005, 0.008), 0, 1.0)
    mix(x, tone(3800, 0.02, "sine", 0.0005, 0.004), 0, 0.3)
    write("sfx_ui_move", x, peak=0.55)

    x = silence(0.25)
    mix(x, tone(note("A5"), 0.08, "square", 0.001, 0.03), 0, 0.25)
    mix(x, tone(note("E6"), 0.2, "square", 0.001, 0.06), 0.06, 0.25)
    write("sfx_ui_confirm", lowpass(x, 5000), peak=0.6)

    x = silence(0.25)
    mix(x, tone(note("E5"), 0.08, "square", 0.001, 0.03), 0, 0.25)
    mix(x, tone(note("A4"), 0.2, "square", 0.001, 0.06), 0.06, 0.25)
    write("sfx_ui_back", lowpass(x, 4000), peak=0.55)

    x = silence(0.8)
    for i, n in enumerate(["C5", "E5", "G5"]):
        mix(x, tone(note(n), 0.5, "tri", 0.002, 0.2), i * 0.05, 0.4)
    mix(x, bell(note("C6"), 0.6, decay=0.3, bright=0.5), 0.15, 0.25)
    write("sfx_ui_ready", x, peak=0.65)

    x = silence(0.18)
    mix(x, tone(note("C4"), 0.15, "square", 0.001, 0.04), 0, 0.3)
    mix(x, tone(note("C#4"), 0.15, "square", 0.001, 0.04), 0, 0.3)
    write("sfx_ui_error", lowpass(x, 2500), peak=0.55)

    x = silence(0.4)
    mix(x, tone(note("G4"), 0.3, "tri", 0.002, 0.1), 0, 0.5)
    mix(x, tone(note("D5"), 0.3, "tri", 0.002, 0.1), 0.04, 0.4)
    write("sfx_ui_pause", x, peak=0.55)

    x = silence(0.4)
    mix(x, tone(note("D5"), 0.3, "tri", 0.002, 0.1), 0, 0.4)
    mix(x, tone(note("G4"), 0.3, "tri", 0.002, 0.1), 0.04, 0.5)
    write("sfx_ui_unpause", x, peak=0.55)


@recipe
def match_start():
    rng = random.Random(900)
    x = silence(2.6)
    mix(x, bell(73.4, 2.5, decay=1.2, bright=1.2), 0, 0.9)                     # gong
    mix(x, lowpass(noise(2.0, rng, 0.01, 0.6), 1200, 300), 0, 0.35)
    for n in ("D3", "A3", "D4"):
        mix(x, brass(note(n), 1.6, attack=0.06, decay=0.6, hold=0.5, bright=2000), 0.12, 0.4)
    write("sfx_match_start", drive(x, 1.2))


@recipe
def victory():
    x = silence(3.4)
    seq = [("C4", 0.0, 0.18), ("C4", 0.14, 0.18), ("C4", 0.28, 0.18), ("C4", 0.42, 0.4),
           ("G#3", 0.85, 0.4), ("A#3", 1.25, 0.4), ("C4", 1.65, 0.3), ("A#3", 1.88, 0.12), ("C4", 2.0, 1.3)]
    for n, at, hold in seq:
        f = note(n) * 2
        mix(x, brass(f, hold + 0.6, attack=0.02, decay=0.25, hold=hold, bright=3500), at, 0.5)
        mix(x, brass(f * 1.5, hold + 0.6, attack=0.02, decay=0.25, hold=hold, bright=3000), at, 0.2)
    mix(x, bell(note("C6"), 1.4, decay=0.7), 2.0, 0.25)
    write("sfx_victory", echo(x, 0.16, 0.22, 3))


@recipe
def defeat():
    x = silence(3.0)
    for n, at in (("G3", 0.0), ("F#3", 0.45), ("F3", 0.9), ("E3", 1.35)):
        hold = 0.3 if n != "E3" else 1.0
        mix(x, brass(note(n), hold + 0.8, attack=0.03, decay=0.35, hold=hold, bright=1600), at, 0.6)
    write("sfx_defeat", echo(x, 0.18, 0.2, 3))


@recipe
def heartbeat():
    x = silence(0.9)
    for at, g in ((0.0, 1.0), (0.22, 0.75)):
        mix(x, tone(55, 0.2, "sine", 0.004, 0.05, glide_to=40), at, g)
        mix(x, lowpass(tone(110, 0.1, "tri", 0.002, 0.02), 300), at, 0.4 * g)
    write("sfx_heartbeat", x)


@recipe
def coins():
    x = silence(0.45)
    mix(x, tone(note("B5"), 0.08, "square", 0.001, 0.05, hold=0.05), 0, 0.25)
    mix(x, tone(note("E6"), 0.35, "square", 0.001, 0.12, hold=0.04), 0.07, 0.25)
    write("sfx_points", lowpass(x, 6000), peak=0.55)

    x = silence(0.9)
    for i, n in enumerate(["E6", "G6", "B6", "E7"]):
        mix(x, bell(note(n), 0.5, decay=0.25, bright=0.6), i * 0.045, 0.35)
    mix(x, tone(note("E5"), 0.12, "square", 0.001, 0.04), 0, 0.15)
    write("sfx_purchase", lowpass(x, 9000), peak=0.7)


@recipe
def pickups():
    rng = random.Random(1000)
    x = silence(0.6)
    for i, n in enumerate(["G5", "C6", "E6", "G6"]):
        mix(x, tone(note(n), 0.25, "tri", 0.002, 0.08), i * 0.04, 0.35)
    mix(x, highpass(noise(0.3, rng, 0.05, 0.08), 6000), 0, 0.2)
    write("sfx_pickup", x, peak=0.7)

    rng = random.Random(1001)
    x = silence(1.6)
    for i, n in enumerate(["C5", "E5", "G5", "B5", "D6", "G6"]):
        mix(x, tone(note(n), 0.9, "tri", 0.003, 0.35), i * 0.055, 0.28)
    mix(x, bell(note("G6"), 1.2, decay=0.6, bright=0.6), 0.33, 0.3)
    mix(x, highpass(noise(1.0, rng, 0.25, 0.3), 6000), 0, 0.2)
    write("sfx_pickup_rare", echo(x, 0.1, 0.3, 3), peak=0.75)

    rng = random.Random(1002)
    x = silence(1.6)
    mix(x, tone(300, 1.4, "sine", 1.0, 0.15, glide_to=1800, vib=0.01, vib_rate=9), 0, 0.5)
    mix(x, bandpass(noise(1.4, rng, 1.0, 0.12), 400, 2.0, 4000), 0, 0.6)
    write("sfx_drop_incoming", x, peak=0.7)


@recipe
def impacts():
    for k in range(3):
        rng = random.Random(1100 + k)
        x = silence(0.12)
        mix(x, bandpass(noise(0.1, rng, 0.0005, 0.015), rng.uniform(700, 1400), 1.2), 0, 1.0)
        mix(x, tone(rng.uniform(150, 220), 0.08, "sine", 0.0005, 0.015, glide_to=90), 0, 0.6)
        write(f"sfx_bullet_impact_{k + 1:02d}", x, peak=0.7)
    for k in range(2):
        rng = random.Random(1200 + k)
        x = silence(0.4)
        f = rng.uniform(2600, 3400)
        mix(x, tone(f, 0.32, "sine", 0.001, 0.12, glide_to=f * 0.55), 0, 0.5)
        mix(x, tone(f * 1.51, 0.25, "sine", 0.001, 0.08, glide_to=f * 0.8), 0, 0.2)
        mix(x, bandpass(noise(0.06, rng, 0.0005, 0.01), 1500, 1.0), 0, 0.6)
        write(f"sfx_ricochet_{k + 1:02d}", x, peak=0.55)


@recipe
def shield():
    x = silence(0.7)
    mix(x, bell(1250, 0.6, decay=0.25, bright=1.2), 0, 0.7)
    mix(x, tone(2500, 0.3, "sine", 0.001, 0.06, vib=0.02, vib_rate=40), 0, 0.2)
    write("sfx_shield_block", x, peak=0.7)

    rng = random.Random(1300)
    x = silence(0.8)
    for i in range(10):
        mix(x, bell(rng.uniform(1800, 4200), 0.35, decay=0.12, bright=1.0), rng.uniform(0, 0.15), 0.25)
    mix(x, highpass(noise(0.4, rng, 0.001, 0.1), 2500), 0, 0.5)
    write("sfx_shield_break", x, peak=0.8)

    x = silence(0.9)
    mix(x, tone(220, 0.8, "tri", 0.1, 0.3, glide_to=660, vib=0.03, vib_rate=14), 0, 0.5)
    mix(x, tone(330, 0.8, "sine", 0.1, 0.3, glide_to=990), 0, 0.3)
    write("sfx_shield_up", x, peak=0.6)


@recipe
def debris():
    for k in range(2):
        rng = random.Random(1400 + k)
        x = silence(1.0)
        mix(x, lowpass(noise(0.7, rng, 0.002, 0.15), 1500, 300), 0, 0.9)
        mix(x, tone(70, 0.4, "sine", 0.002, 0.08, glide_to=45), 0, 0.8)
        for i in range(22):
            at = rng.uniform(0.02, 0.6) ** 1.5
            mix(x, bandpass(noise(0.05, rng, 0.0005, rng.uniform(0.005, 0.02)), rng.uniform(800, 4000), 1.5), at, rng.uniform(0.3, 0.8))
        write(f"sfx_debris_{k + 1:02d}", x)
    for k in range(3):
        rng = random.Random(1500 + k)
        x = silence(0.15)
        mix(x, bandpass(noise(0.12, rng, 0.0005, 0.02), rng.uniform(400, 900), 1.0), 0, 1.0)
        mix(x, tone(rng.uniform(110, 160), 0.1, "sine", 0.001, 0.025), 0, 0.5)
        write(f"sfx_prop_hit_{k + 1:02d}", x, peak=0.65)


@recipe
def wave_start():
    rng = random.Random(1600)
    x = silence(3.0)
    for i, at in enumerate((0.0, 0.32, 0.64)):
        mix(x, tone(60, 0.5, "sine", 0.002, 0.12, glide_to=42), at, 1.0)
        mix(x, lowpass(noise(0.3, rng, 0.001, 0.05), 800), at, 0.6)
    for n in ("D2", "A2", "D3", "F3"):
        mix(x, brass(note(n), 2.0, attack=0.25, decay=0.6, hold=0.8, bright=1400), 0.9, 0.35)
    write("sfx_wave_start", drive(x, 1.3))


@recipe
def gale_horn():
    rng = random.Random(1700)
    x = silence(1.3)
    for n in ("A2", "E3", "A3"):
        mix(x, brass(note(n), 1.1, attack=0.03, decay=0.35, hold=0.35, bright=2200), 0, 0.5)
    mix(x, bandpass(noise(1.2, rng, 0.05, 0.4), 500, 0.8, 2500), 0, 0.7)
    write("sfx_gale_horn", drive(x, 1.4))


@recipe
def minigun_spin():
    # a seamless loop: rotor whine + motor hum (period-aligned so it loops cleanly)
    sec = 1.0
    n = int(SR * sec)
    x = []
    for i in range(n):
        t = i / SR
        x.append(0.4 * math.sin(TAU * 60 * t) + 0.25 * math.sin(TAU * 120 * t)
                 + 0.15 * math.sin(TAU * 480 * t) * (0.6 + 0.4 * math.sin(TAU * 20 * t))
                 + 0.08 * math.sin(TAU * 1440 * t))
    write("sfx_minigun_spin_loop", x, peak=0.6, fade=0)


@recipe
def stun():
    x = silence(0.7)
    for i in range(3):
        f = note("A6") if i % 2 == 0 else note("E6")
        mix(x, tone(f, 0.18, "sine", 0.003, 0.05, vib=0.06, vib_rate=25), i * 0.13, 0.4)
    write("sfx_stun", x, peak=0.55)


@recipe
def throw():
    rng = random.Random(1800)
    x = silence(0.35)
    mix(x, bandpass(noise(0.3, rng, 0.06, 0.05), 700, 1.5, 2500), 0, 1.0)
    write("sfx_throw", x, peak=0.6)


@recipe
def crown():
    x = silence(1.6)
    for i, n in enumerate(["G4", "C5", "E5", "G5"]):
        mix(x, brass(note(n), 0.9 if i == 3 else 0.35, attack=0.015, decay=0.25, hold=0.6 if i == 3 else 0.1, bright=3200), i * 0.11, 0.5)
    mix(x, bell(note("G6"), 1.0, decay=0.5), 0.33, 0.3)
    write("sfx_crown", x, peak=0.75)


def main():
    filters = sys.argv[1:]
    print(f"writing to {os.path.normpath(OUT)}")
    for name, fn in RECIPES.items():
        if filters and not any(f in name for f in filters):
            continue
        fn()


if __name__ == "__main__":
    main()
