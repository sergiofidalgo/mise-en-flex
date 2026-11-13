#!/usr/bin/env python3
"""Synthesizes the placeholder kitchen sounds as WAV files (standard library only).

Run from the project root:  python3 tools/synth_sfx.py

Seeded, so the same script always writes the same files. Real recordings can replace any clip
by overwriting its file: the game only knows the clip, not where it came from.
"""
import array
import math
import os
import random
import sys
import wave

RATE = 44100
OUT = os.path.join("Assets", "_Project", "Audio", "SFX")


def silence(seconds):
    return [0.0] * int(seconds * RATE)


def noise(rng, seconds):
    return [rng.uniform(-1.0, 1.0) for _ in range(int(seconds * RATE))]


def biquad(samples, kind, freq, q=0.707):
    """RBJ cookbook filter: the standard way to get a clean low/high/band-pass."""
    w = 2 * math.pi * freq / RATE
    alpha = math.sin(w) / (2 * q)
    cos = math.cos(w)
    if kind == "low":
        b0, b1, b2 = (1 - cos) / 2, 1 - cos, (1 - cos) / 2
    elif kind == "high":
        b0, b1, b2 = (1 + cos) / 2, -(1 + cos), (1 + cos) / 2
    else:  # band
        b0, b1, b2 = alpha, 0.0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * cos, 1 - alpha
    out = []
    x1 = x2 = y1 = y2 = 0.0
    for x in samples:
        y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        out.append(y)
        x2, x1, y2, y1 = x1, x, y1, y
    return out


def decay(samples, seconds, attack=0.0):
    """Exponential fade, reaching about -60 dB after `seconds`, with an optional click-free attack."""
    rate = 6.9 / max(seconds, 1e-4)
    out = []
    for i, x in enumerate(samples):
        t = i / RATE
        rise = min(1.0, t / attack) if attack > 0 else 1.0
        out.append(x * rise * math.exp(-rate * t))
    return out


def mode(freq, seconds, length, gain=1.0, phase=0.0):
    """One damped sine: a struck object rings as a handful of these."""
    rate = 6.9 / seconds
    n = int(length * RATE)
    return [gain * math.sin(2 * math.pi * freq * i / RATE + phase) * math.exp(-rate * i / RATE) for i in range(n)]


def mix(*layers):
    out = [0.0] * max(len(layer) for layer in layers)
    for layer in layers:
        for i, x in enumerate(layer):
            out[i] += x
    return out


def scaled(samples, gain):
    return [x * gain for x in samples]


def finish(samples, peak, fade_out=0.005):
    """Normalizes to `peak` and fades the last few ms, so a clip can never end on a click."""
    top = max(abs(x) for x in samples) or 1.0
    out = [x / top * peak for x in samples]
    n = min(int(fade_out * RATE), len(out))
    for i in range(n):
        out[-1 - i] *= i / max(n, 1)
    return out


def write(name, samples):
    os.makedirs(OUT, exist_ok=True)
    pcm = array.array("h", (int(max(-1.0, min(1.0, x)) * 32767) for x in samples))
    if sys.byteorder == "big":
        pcm.byteswap()
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())


def bell(rng, base):
    # A counter bell is inharmonic: these partial ratios are what make it read as metal, not a tone.
    length = 1.5
    partials = [(1.0, 1.25, 1.0), (1.51, 0.9, 0.45), (2.42, 0.6, 0.5), (3.37, 0.35, 0.25), (4.53, 0.2, 0.12)]
    layers = [mode(base * ratio, ring, length, gain, rng.uniform(0, 6.28)) for ratio, ring, gain in partials]
    # A slight detune beats against the fundamental, giving the shimmer of a real bell.
    layers.append(mode(base * 1.004, 1.1, length, 0.5))
    # The clapper's tick.
    layers.append(scaled(decay(biquad(noise(rng, 0.02), "high", 3000), 0.012), 0.5))
    return finish(mix(*layers), 0.7)


def chop(rng, muffle, gain):
    """Blade through food into a wooden board. `muffle` closes the top end for an off-beat cut."""
    length = 0.16
    crunch = decay(biquad(noise(rng, 0.05), "band", rng.uniform(2200, 3200), 0.8), 0.03)
    click = scaled(decay(biquad(noise(rng, 0.008), "high", 2500), 0.005), 0.6)
    wobble = rng.uniform(0.94, 1.06)
    board = mix(mode(210 * wobble, 0.07, length, 1.0), mode(480 * wobble, 0.05, length, 0.6), mode(1050 * wobble, 0.03, length, 0.3))
    # The blade reaches the board a few ms after it enters the food.
    body = [0.0] * int(0.006 * RATE) + board
    sound = mix(scaled(crunch, 0.8), click, scaled(body, 0.9))
    if muffle:
        sound = biquad(sound, "low", muffle)
    return finish(sound, gain)


def chop_miss(rng):
    """The knife skids: a short metal scrape with a dull knock underneath."""
    seconds = 0.2
    scrape = []
    base = noise(rng, seconds)
    start, end = rng.uniform(3200, 3800), rng.uniform(1700, 2100)
    # Sweep a resonance in short blocks: fine enough at this speed, and much simpler than per-sample.
    block = int(0.01 * RATE)
    for i in range(0, len(base), block):
        t = i / len(base)
        scrape.extend(biquad(base[i:i + block], "band", start + (end - start) * t, 5.0))
    n = len(scrape)
    scrape = [x * math.sin(math.pi * min(1.0, i / n)) ** 0.7 for i, x in enumerate(scrape)]
    knock = mode(150 * rng.uniform(0.95, 1.05), 0.06, seconds, 1.0)
    return finish(mix(scaled(scrape, 0.8), scaled(knock, 0.35)), 0.55)


def sizzle_loop(rng, seconds=6.0):
    """Hiss plus random fat pops. Every layer wraps around the end, so the loop has no seam."""
    n = int(seconds * RATE)
    # Extra lead-in lets the filters settle. The noise that would follow the loop's last sample
    # then fades out over its first samples, so the end runs straight into the start.
    fade = int(0.4 * RATE)
    hiss = biquad(biquad(noise(rng, seconds + 0.8), "high", 2800), "low", 11000)
    # A slow drift in level, in whole cycles of the loop, so it breathes without jumping at the seam.
    drift = [1.0 + 0.12 * math.sin(2 * math.pi * 2 * i / n) + 0.08 * math.sin(2 * math.pi * 5 * i / n + 1.3) for i in range(n)]
    out = []
    for i in range(n):
        x = hiss[fade + i]
        if i < fade:
            # Equal power, not linear: these two stretches of noise are unrelated, so a linear
            # blend would audibly dip in level halfway through.
            k = i / fade
            x = x * math.sqrt(k) + hiss[fade + n + i] * math.sqrt(1 - k)
        out.append(x * drift[i] * 0.3)

    pops = int(seconds * 34)
    for _ in range(pops):
        start = rng.randrange(n)
        size = rng.random() ** 3  # mostly tiny spits, the odd big one
        ms = 0.002 + 0.008 * size
        pop = decay(biquad(noise(rng, ms * 2.5), "band", rng.uniform(1800, 6500), 1.2), ms)
        gain = 0.25 + 1.5 * size
        for i, x in enumerate(pop):
            out[(start + i) % n] += x * gain
    top = max(abs(x) for x in out)
    return [x / top * 0.75 for x in out]


def sizzle_hit(rng):
    """Food meeting a hot pan: a fast rush of hiss that settles into the loop underneath."""
    seconds = 0.9
    hiss = biquad(biquad(noise(rng, seconds), "high", 2200), "low", 12000)
    body = decay(hiss, 0.8, attack=0.012)
    wet = decay(biquad(noise(rng, 0.2), "band", 900, 0.8), 0.15, attack=0.005)
    return finish(mix(body, scaled(wet, 0.5)), 0.65, fade_out=0.05)


def door_creak(rng, seconds):
    """Stick-slip friction: a hinge is a fast train of tiny clicks, each ringing the door's wood."""
    n = int(seconds * RATE)
    pulses = [0.0] * n
    start, end = rng.uniform(55, 70), rng.uniform(120, 150)
    position = 0.0
    while position < n:
        t = position / n
        # Sticks and slips unevenly, which is what separates a creak from a buzz.
        rate = (start + (end - start) * t) * rng.uniform(0.8, 1.2)
        pulses[int(position)] = rng.uniform(0.6, 1.0)
        position += RATE / rate
    wood = mix(biquad(pulses, "band", 620, 6.0), scaled(biquad(pulses, "band", 1350, 7.0), 0.8), scaled(biquad(pulses, "band", 2500, 8.0), 0.4))
    envelope = [math.sin(math.pi * min(1.0, i / n)) ** 0.6 for i in range(n)]
    return finish([x * e for x, e in zip(wood, envelope)], 0.45, fade_out=0.02)


def door_close(rng):
    """A soft padded thud with a little wooden rattle."""
    length = 0.3
    wobble = rng.uniform(0.92, 1.08)
    low = mix(mode(85 * wobble, 0.12, length, 1.0), mode(170 * wobble, 0.08, length, 0.5))
    wood = biquad(mode(340 * wobble, 0.05, length, 0.5), "low", 1500)
    air = scaled(decay(biquad(noise(rng, 0.04), "low", 900), 0.03), 0.3)
    rattle = [0.0] * int(0.05 * RATE) + scaled(mode(290 * wobble, 0.04, 0.1, 1.0), 0.18)
    return finish(mix(low, wood, air, rattle), 0.6, fade_out=0.02)


def alarm_beep():
    """A detector's double beep. Round-edged and slightly soft: urgent, without being painful."""
    freq = 3100
    beep_seconds, gap_seconds = 0.11, 0.07
    n = int(beep_seconds * RATE)
    edge = int(0.006 * RATE)
    beep = []
    for i in range(n):
        t = i / RATE
        rise = min(1.0, i / edge, (n - i) / edge)
        # An odd harmonic gives it a piezo buzzer's edge; a pure sine would read as a test tone.
        beep.append(rise * (math.sin(2 * math.pi * freq * t) + 0.18 * math.sin(2 * math.pi * freq * 3 * t)))
    return finish(beep + silence(gap_seconds) + beep, 0.5)


def door_chime(rng, base):
    """The little bell on a shop door: two quick, small strikes as the door moves it."""
    length = 0.7
    # Higher and shorter than the order bell, so the two can't be confused.
    partials = [(1.0, 0.45, 1.0), (1.48, 0.3, 0.4), (2.31, 0.18, 0.3)]
    def strike(gain, shift):
        return [x * gain for x in mix(*(mode(base * shift * r, ring, length, g, rng.uniform(0, 6.28)) for r, ring, g in partials))]
    second = [0.0] * int(0.085 * RATE) + strike(0.65, 1.012)
    return finish(mix(strike(1.0, 1.0), second), 0.55, fade_out=0.02)


def till(rng, wobble):
    """A till: a key's clunk, the drawer's rattle, then a small ding, like the old mechanical ones."""
    length = 0.55
    key = mix(mode(300 * wobble, 0.04, length, 1.0), scaled(decay(biquad(noise(rng, 0.01), "high", 1500), 0.008), 0.7))
    drawer = [0.0] * int(0.07 * RATE) + scaled(decay(biquad(noise(rng, 0.09), "band", 700, 1.0), 0.09, attack=0.005), 0.5)
    ring_at = [0.0] * int(0.1 * RATE)
    ding = ring_at + mix(mode(1760 * wobble, 0.42, length - 0.1, 0.8), mode(1760 * 2.38 * wobble, 0.25, length - 0.1, 0.3))
    return finish(mix(scaled(key, 0.6), drawer, ding), 0.55, fade_out=0.02)


def clack(rng):
    """Something set down on a counter: a soft, short knock with a little ceramic ring."""
    length = 0.12
    wobble = rng.uniform(0.88, 1.12)
    body = mix(mode(620 * wobble, 0.04, length, 1.0), mode(1450 * wobble, 0.03, length, 0.5), mode(2900 * wobble, 0.02, length, 0.18))
    tick = scaled(decay(biquad(noise(rng, 0.006), "band", 2500, 0.8), 0.004), 0.35)
    return finish(mix(body, tick), 0.55)


def step(rng):
    """A soft shoe on a hard floor: mostly a low, dull pat, with the sole's little scuff on top."""
    length = 0.13
    wobble = rng.uniform(0.9, 1.1)
    pat = mode(115 * wobble, 0.06, length, 1.0)
    heel = scaled(decay(biquad(noise(rng, 0.05), "low", 500 * wobble), 0.035, attack=0.002), 0.9)
    scuff = [0.0] * int(0.012 * RATE) + scaled(decay(biquad(noise(rng, 0.05), "band", 1800, 0.7), 0.04, attack=0.004), 0.12)
    return finish(biquad(mix(pat, heel, scuff), "high", 60), 0.5, fade_out=0.01)


def fridge_open(rng, wobble):
    """A fridge door unsticking: the seal's soft pop, a bottle clinking on the door, a breath of cold air."""
    length = 0.5
    seal = mix(mode(95 * wobble, 0.05, length, 1.0), scaled(decay(biquad(noise(rng, 0.03), "low", 700), 0.02, attack=0.002), 0.8))
    clink = [0.0] * int(0.06 * RATE) + scaled(mix(mode(2650 * wobble, 0.12, 0.3, 1.0), mode(2650 * 2.7 * wobble, 0.07, 0.3, 0.4)), 0.12)
    air = biquad(noise(rng, 0.42), "band", 1100 * wobble, 0.6)
    n = len(air)
    # Swells as the door swings wide, then thins out.
    air = [x * math.sin(math.pi * i / n) ** 1.5 for i, x in enumerate(air)]
    whoosh = [0.0] * int(0.03 * RATE) + scaled(air, 0.35)
    return finish(mix(seal, clink, whoosh), 0.6, fade_out=0.03)


def fridge_close(rng, wobble):
    """A fridge door swinging shut. The thump sits 0.26 s in, as the door meets the frame:
    FridgeDoorView raises Closed when the 0.3 s swing starts, not when it ends."""
    hit = int(0.26 * RATE)
    swing = biquad(noise(rng, 0.26), "band", 800 * wobble, 0.7)
    n = len(swing)
    swing = [x * (i / n) ** 2 * 0.12 for i, x in enumerate(swing)]
    # A padded seal: deeper and duller than the kitchen door, with the shelves' glass answering.
    thump = mix(mode(75 * wobble, 0.1, 0.3, 1.0), mode(150 * wobble, 0.06, 0.3, 0.4),
                scaled(decay(biquad(noise(rng, 0.03), "low", 600), 0.025), 0.5))
    rattle = [0.0] * int(0.02 * RATE) + scaled(mix(mode(2400 * wobble, 0.1, 0.25, 1.0), mode(3300 * wobble, 0.07, 0.25, 0.6)), 0.08)
    body = [0.0] * hit + mix(thump, rattle)
    return finish(mix(swing, body), 0.6, fade_out=0.02)


def lift_ding(rng, base):
    """An elevator's arrival bell: one round, soft-edged ding with a long, warm ring. Lower and
    purer than the order bell (a struck chime bar, nearly harmonic), so the two never get confused."""
    length = 2.2
    partials = [(1.0, 1.9, 1.0), (2.0, 1.1, 0.28), (3.0, 0.6, 0.1), (4.16, 0.35, 0.05)]
    layers = [mode(base * ratio, ring, length, gain, rng.uniform(0, 6.28)) for ratio, ring, gain in partials]
    layers.append(mode(base * 1.003, 1.6, length, 0.35))
    # A felt hammer: a dull, short knock rather than a metal tick.
    layers.append(scaled(decay(biquad(noise(rng, 0.02), "low", 1200), 0.01), 0.25))
    return finish(mix(*layers), 0.8, fade_out=0.05)


def lift_doors(rng):
    """Elevator doors sliding: a smooth rolling hush swelling and fading, a low motor hum under it,
    and a soft bump where the panels stop."""
    seconds = 0.65
    hush = biquad(noise(rng, seconds), "band", 700, 0.5)
    n = len(hush)
    hush = [x * math.sin(math.pi * i / n) ** 1.2 * 0.5 for i, x in enumerate(hush)]
    hum = [math.sin(2 * math.pi * 92 * i / RATE) * 0.25 * math.sin(math.pi * i / n) for i in range(n)]
    bump = [0.0] * int((seconds - 0.05) * RATE) + scaled(mode(110, 0.08, 0.25, 1.0), 0.35)
    return finish(mix(hush, hum, bump), 0.45, fade_out=0.03)


def tone(freq, seconds, ring, gain=1.0, harmonics=((1, 1.0),), attack=0.004):
    """A struck or plucked note: a few harmonics with a soft attack and a ring-out."""
    n = int(seconds * RATE)
    rate = 6.9 / ring
    edge = max(1, int(attack * RATE))
    return [gain * min(1.0, i / edge) * math.exp(-rate * i / RATE)
            * sum(g * math.sin(2 * math.pi * freq * h * i / RATE) for h, g in harmonics) for i in range(n)]


def at(seconds, samples):
    return [0.0] * int(seconds * RATE) + samples


MARIMBA = ((1, 1.0), (4, 0.25), (9.9, 0.06))
BELLISH = ((1, 1.0), (2.76, 0.35), (5.4, 0.12))


def ui_move(rng):
    # A soft wooden tick: heard every step through a menu, so it has to be small.
    return finish(mix(tone(1320 * rng.uniform(0.98, 1.02), 0.06, 0.035, 1.0, MARIMBA)), 0.45)


def ui_confirm():
    return finish(mix(tone(880, 0.25, 0.16, 1.0, MARIMBA), at(0.06, tone(1320, 0.22, 0.16, 0.9, MARIMBA))), 0.5)


def ui_back():
    return finish(mix(tone(990, 0.2, 0.12, 0.9, MARIMBA), at(0.055, tone(660, 0.2, 0.12, 0.8, MARIMBA))), 0.45)


def deny(rng):
    """Nothing happened: a muted double knock, low and short, so it never reads as an error alarm."""
    knock = lambda f: mix(mode(f, 0.05, 0.1, 1.0), scaled(decay(biquad(noise(rng, 0.01), "low", 900), 0.008), 0.4))
    f = 230 * rng.uniform(0.96, 1.04)
    return finish(mix(knock(f), at(0.085, knock(f * 0.94))), 0.45)


def pickup(rng):
    """Something lifted: a quick, soft brush of air and cloth. Quieter than putting down."""
    body = biquad(noise(rng, 0.09), "band", 1400 * rng.uniform(0.85, 1.15), 0.7)
    n = len(body)
    body = [x * math.sin(math.pi * i / n) ** 2 for i, x in enumerate(body)]
    return finish(mix(body, scaled(mode(420 * rng.uniform(0.9, 1.1), 0.03, 0.09, 1.0), 0.3)), 0.4, fade_out=0.01)


def heat_click(rng):
    """A hob knob's detent: a tiny, dry plastic click."""
    return finish(mix(decay(biquad(noise(rng, 0.012), "band", 3800 * rng.uniform(0.9, 1.1), 1.5), 0.006),
                      scaled(mode(1900, 0.01, 0.03, 1.0), 0.4)), 0.4)


def gas_ignite(rng):
    """Igniter ticks, then the gas catching with a soft whump of flame."""
    ticks = mix(*(at(t, decay(biquad(noise(rng, 0.006), "high", 3000), 0.003)) for t in (0.0, 0.07, 0.14)))
    roar = biquad(noise(rng, 0.5), "low", 900)
    n = len(roar)
    roar = [x * min(1.0, i / (0.03 * RATE)) * math.exp(-4.5 * i / n) for i, x in enumerate(roar)]
    whump = mode(70, 0.12, 0.3, 1.0)
    return finish(mix(scaled(ticks, 0.5), at(0.16, mix(scaled(roar, 0.9), scaled(whump, 0.6)))), 0.55, fade_out=0.05)


def gas_off(rng):
    """The flame going out: a click and a short soft puff."""
    puff = decay(biquad(noise(rng, 0.12), "low", 700), 0.08, attack=0.004)
    return finish(mix(heat_click(rng), at(0.01, scaled(puff, 0.6))), 0.4, fade_out=0.02)


def fire_whoosh(rng):
    """A pan going up: a rush of flame that swells fast and falls back to a roar."""
    rush = biquad(noise(rng, 1.2), "low", 1600)
    n = len(rush)
    rush = [x * min(1.0, i / (0.08 * RATE)) * (0.35 + 0.65 * math.exp(-3.0 * i / n)) for i, x in enumerate(rush)]
    return finish(mix(rush, scaled(mode(55, 0.4, 1.2, 1.0), 0.5)), 0.7, fade_out=0.25)


def fire_loop(rng, seconds=5.0):
    """A pan fire's roar and crackle, looping without a seam (same trick as the sizzle)."""
    n = int(seconds * RATE)
    fade = int(0.4 * RATE)
    roar = biquad(biquad(noise(rng, seconds + 0.8), "low", 1200), "high", 80)
    out = []
    for i in range(n):
        x = roar[fade + i]
        if i < fade:
            k = i / fade
            x = x * math.sqrt(k) + roar[fade + n + i] * math.sqrt(1 - k)
        out.append(x * (1.0 + 0.2 * math.sin(2 * math.pi * 3 * i / n)) * 0.5)
    for _ in range(int(seconds * 18)):
        start = rng.randrange(n)
        size = rng.random() ** 2
        pop = decay(biquad(noise(rng, 0.03), "band", rng.uniform(900, 3500), 1.0), 0.004 + 0.012 * size)
        for i, x in enumerate(pop):
            out[(start + i) % n] += x * (0.4 + 1.6 * size)
    top = max(abs(x) for x in out)
    return [x / top * 0.75 for x in out]


def fire_out(rng):
    """The extinguisher winning: a long hiss that tails away, with a last little crackle."""
    hiss = biquad(noise(rng, 1.3), "high", 2400)
    n = len(hiss)
    hiss = [x * min(1.0, i / (0.02 * RATE)) * math.exp(-2.6 * i / n) for i, x in enumerate(hiss)]
    return finish(mix(hiss, at(0.5, scaled(decay(biquad(noise(rng, 0.02), "band", 1800, 1.0), 0.01), 0.5))), 0.55, fade_out=0.2)


def stroke(rng, centre, seconds, q, gain=1.0):
    """One sweep of something across a surface: band-passed noise that rises and falls."""
    body = biquad(noise(rng, seconds), "band", centre * rng.uniform(0.85, 1.15), q)
    n = len(body)
    return scaled([x * math.sin(math.pi * i / n) ** 1.5 for i, x in enumerate(body)], gain)


def scrub(rng):
    return finish(stroke(rng, 2600, 0.22, 1.2), 0.45, fade_out=0.01)


def mop(rng):
    # Wetter and lower than the cloth: a slosh across the floor.
    return finish(mix(stroke(rng, 700, 0.35, 0.8), stroke(rng, 2200, 0.3, 1.5, 0.3)), 0.45, fade_out=0.02)


def spray(rng):
    return finish(stroke(rng, 4200, 0.3, 0.5), 0.45, fade_out=0.02)


def clean_done():
    """Clean: a small bright sparkle, three quick high notes."""
    notes = [(2093, 0.0), (2637, 0.06), (3136, 0.12)]
    return finish(mix(*(at(t, tone(f, 0.35, 0.25, 0.8, BELLISH)) for f, t in notes)), 0.4, fade_out=0.05)


def wash(rng):
    """Washing up: water swirling round a plate, with a scrub in it."""
    return finish(mix(stroke(rng, 900, 0.4, 0.6), stroke(rng, 2400, 0.25, 1.4, 0.35)), 0.4, fade_out=0.03)


def splash(rng):
    """A plate going into the sink's water."""
    plop = mode(260 * rng.uniform(0.9, 1.1), 0.06, 0.4, 1.0)
    water = decay(biquad(noise(rng, 0.35), "band", 1500, 0.6), 0.25, attack=0.004)
    clink = at(0.02, scaled(tone(2900 * rng.uniform(0.95, 1.05), 0.2, 0.12, 1.0, BELLISH), 0.15))
    return finish(mix(scaled(plop, 0.5), water, clink), 0.5, fade_out=0.05)


def splat(rng):
    """A spill landing on the floor: wet and soft."""
    wet = decay(biquad(noise(rng, 0.18), "low", 1100 * rng.uniform(0.8, 1.2)), 0.1, attack=0.002)
    return finish(mix(wet, scaled(mode(140, 0.05, 0.18, 1.0), 0.5)), 0.4, fade_out=0.02)


def bin_thunk(rng):
    """Something dropped into the bin: a dull thud into a hollow plastic body."""
    body = mix(mode(160 * rng.uniform(0.9, 1.1), 0.12, 0.3, 1.0), mode(390 * rng.uniform(0.9, 1.1), 0.07, 0.3, 0.4))
    rustle = scaled(decay(biquad(noise(rng, 0.08), "band", 3000, 0.8), 0.05), 0.25)
    return finish(mix(body, rustle), 0.5, fade_out=0.03)


def bag(rng):
    """The bin bag pulled out: plastic rustling for half a second."""
    out = []
    for _ in range(14):
        out = mix(out or [0.0], at(rng.uniform(0, 0.45), scaled(stroke(rng, rng.uniform(2500, 5000), 0.06, 1.5), rng.uniform(0.4, 1.0))))
    return finish(out, 0.4, fade_out=0.03)


def knife(rng):
    """A knife drawn from (or slid into) the rack: a short bright metal shing."""
    ring = tone(4200 * rng.uniform(0.96, 1.04), 0.35, 0.25, 1.0, ((1, 1.0), (1.47, 0.5), (2.09, 0.3)))
    scrape = stroke(rng, 6000, 0.12, 2.0, 0.8)
    return finish(mix(scrape, at(0.05, scaled(ring, 0.5))), 0.4, fade_out=0.04)


def serve(notes):
    """A plate landing at the table, then a little phrase that says how it went."""
    plate = mix(mode(700, 0.04, 0.1, 1.0), mode(1600, 0.03, 0.1, 0.4))
    phrase = mix(*(at(0.08 + t, tone(f, 0.4, 0.3, 0.8, MARIMBA)) for f, t in notes))
    return finish(mix(scaled(plate, 0.6), phrase), 0.5, fade_out=0.05)


def walkout():
    """A party giving up: two low notes falling, deliberately flat and a little sad."""
    return finish(mix(tone(392, 0.45, 0.35, 1.0, MARIMBA), at(0.16, tone(311, 0.55, 0.45, 1.0, MARIMBA))), 0.5, fade_out=0.08)


def furniture(rng):
    """Furniture set down: a heavy wooden thump."""
    return finish(mix(mode(90 * rng.uniform(0.9, 1.1), 0.15, 0.35, 1.0), mode(210 * rng.uniform(0.9, 1.1), 0.08, 0.35, 0.5),
                      scaled(decay(biquad(noise(rng, 0.04), "low", 1200), 0.03), 0.6)), 0.55, fade_out=0.03)


def ticket(rng):
    """A new order: the little chatter of a ticket printer, then the paper tearing off."""
    chatter = mix(*(at(i * 0.022, decay(biquad(noise(rng, 0.01), "band", 3400, 2.0), 0.006)) for i in range(9)))
    tear = at(0.24, stroke(rng, 5200, 0.07, 1.0, 0.8))
    return finish(mix(chatter, tear), 0.4, fade_out=0.02)


def count_tick():
    return finish(tone(1046, 0.22, 0.15, 1.0, BELLISH), 0.45, fade_out=0.04)


def doors_open():
    """Service begins: a bright rising arpeggio, like a shop opening its doors."""
    notes = [(523, 0.0), (659, 0.08), (784, 0.16), (1046, 0.24)]
    return finish(mix(*(at(t, tone(f, 0.7, 0.55, 0.8, BELLISH)) for f, t in notes)), 0.55, fade_out=0.1)


def service_over():
    """Service ends: the same notes falling, softer, a full stop."""
    notes = [(1046, 0.0), (784, 0.1), (659, 0.2), (523, 0.32)]
    return finish(mix(*(at(t, tone(f, 0.8, 0.6, 0.8, BELLISH)) for f, t in notes)), 0.5, fade_out=0.12)


def scooter(rng, arriving):
    """A scooter's two-stroke putter, slowing to a stop or revving away."""
    seconds = 1.2
    n = int(seconds * RATE)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        rate = (55 - 30 * t) if arriving else (30 + 45 * t)
        phase += rate / RATE
        pulse = 1.0 if (phase % 1.0) < 0.15 else 0.0
        out.append(pulse * (rng.uniform(0.7, 1.0)))
    body = biquad(biquad(out, "low", 900), "high", 70)
    env = [(1 - t) ** 0.5 if arriving else min(1.0, t * 4) * (1 - t) ** 1.5 for t in (i / n for i in range(n))]
    return finish([x * e for x, e in zip(body, env)], 0.45, fade_out=0.1)


def phone_ring():
    """The boss's mobile: two short trilling bursts, a classic ring, not a melody."""
    def burst():
        n = int(0.4 * RATE)
        return [math.sin(2 * math.pi * 1400 * i / RATE) * (0.6 + 0.4 * math.sin(2 * math.pi * 18 * i / RATE))
                * min(1.0, i / 200, (n - i) / 200) for i in range(n)]
    return finish(mix(burst(), at(0.55, burst())), 0.45)


def join_pop():
    """A chef joining: a cheerful upward pop."""
    return finish(mix(tone(660, 0.25, 0.18, 1.0, MARIMBA), at(0.07, tone(990, 0.3, 0.22, 1.0, MARIMBA))), 0.5, fade_out=0.04)


def fanfare():
    """A restaurant bought: a short, proud brass-ish flourish."""
    brass = ((1, 1.0), (2, 0.6), (3, 0.4), (4, 0.25), (5, 0.12))
    notes = [(392, 0.0, 0.18), (523, 0.15, 0.18), (659, 0.3, 0.18), (784, 0.45, 0.6)]
    return finish(mix(*(at(t, tone(f, d + 0.3, d + 0.2, 0.8, brass, attack=0.02)) for f, t, d in notes)), 0.55, fade_out=0.12)


def main():
    rng = random.Random(20261107)
    write("SFX_Bell_01", bell(rng, 2350))
    write("SFX_Bell_02", bell(rng, 2390))
    for i in range(1, 5):
        write(f"SFX_Chop_Clean_{i:02}", chop(rng, muffle=None, gain=0.8))
    for i in range(1, 4):
        write(f"SFX_Chop_Off_{i:02}", chop(rng, muffle=1500, gain=0.55))
    for i in range(1, 3):
        write(f"SFX_Chop_Miss_{i:02}", chop_miss(rng))
    write("SFX_Sizzle_Loop", sizzle_loop(rng))
    write("SFX_Sizzle_Hit_01", sizzle_hit(rng))
    write("SFX_Sizzle_Hit_02", sizzle_hit(rng))
    write("SFX_Door_Open_01", door_creak(rng, 0.32))
    write("SFX_Door_Open_02", door_creak(rng, 0.26))
    write("SFX_Door_Close_01", door_close(rng))
    write("SFX_Door_Close_02", door_close(rng))
    # New clips go last, so the random numbers behind the ones above never change.
    write("SFX_Alarm_01", alarm_beep())
    write("SFX_Chime_01", door_chime(rng, 3350))
    write("SFX_Chime_02", door_chime(rng, 3180))
    write("SFX_Till_01", till(rng, 1.0))
    write("SFX_Till_02", till(rng, 0.97))
    for i in range(1, 5):
        write(f"SFX_Clack_{i:02}", clack(rng))
    for i in range(1, 6):
        write(f"SFX_Step_{i:02}", step(rng))
    write("SFX_Fridge_Open_01", fridge_open(rng, 1.0))
    write("SFX_Fridge_Open_02", fridge_open(rng, 0.95))
    write("SFX_Fridge_Close_01", fridge_close(rng, 1.0))
    write("SFX_Fridge_Close_02", fridge_close(rng, 1.05))
    write("SFX_Lift_Ding_01", lift_ding(rng, 1318.5))
    write("SFX_Lift_Doors_01", lift_doors(rng))
    # Everything below came with the 2026-11-13 pass over the whole game; still appended last.
    for i in range(1, 3):
        write(f"SFX_UI_Move_{i:02}", ui_move(rng))
    write("SFX_UI_Confirm_01", ui_confirm())
    write("SFX_UI_Back_01", ui_back())
    for i in range(1, 3):
        write(f"SFX_Deny_{i:02}", deny(rng))
    for i in range(1, 4):
        write(f"SFX_Pickup_{i:02}", pickup(rng))
    for i in range(1, 4):
        write(f"SFX_Heat_Click_{i:02}", heat_click(rng))
    for i in range(1, 3):
        write(f"SFX_Gas_Ignite_{i:02}", gas_ignite(rng))
    write("SFX_Gas_Off_01", gas_off(rng))
    write("SFX_Fire_Whoosh_01", fire_whoosh(rng))
    write("SFX_Fire_Loop", fire_loop(rng))
    write("SFX_Fire_Out_01", fire_out(rng))
    for i in range(1, 4):
        write(f"SFX_Scrub_{i:02}", scrub(rng))
    for i in range(1, 3):
        write(f"SFX_Mop_{i:02}", mop(rng))
    for i in range(1, 3):
        write(f"SFX_Spray_{i:02}", spray(rng))
    write("SFX_Clean_Done_01", clean_done())
    for i in range(1, 4):
        write(f"SFX_Wash_{i:02}", wash(rng))
    for i in range(1, 3):
        write(f"SFX_Splash_{i:02}", splash(rng))
    for i in range(1, 3):
        write(f"SFX_Splat_{i:02}", splat(rng))
    for i in range(1, 3):
        write(f"SFX_Bin_{i:02}", bin_thunk(rng))
    write("SFX_Bag_01", bag(rng))
    for i in range(1, 3):
        write(f"SFX_Knife_{i:02}", knife(rng))
    write("SFX_Serve_Good_01", serve([(784, 0.0), (1046, 0.09), (1318, 0.18)]))
    write("SFX_Serve_Ok_01", serve([(784, 0.0), (880, 0.1)]))
    write("SFX_Serve_Poor_01", serve([(523, 0.0), (466, 0.12)]))
    write("SFX_Walkout_01", walkout())
    for i in range(1, 3):
        write(f"SFX_Furniture_{i:02}", furniture(rng))
    for i in range(1, 3):
        write(f"SFX_Ticket_{i:02}", ticket(rng))
    write("SFX_Count_01", count_tick())
    write("SFX_Open_01", doors_open())
    write("SFX_Close_01", service_over())
    write("SFX_Scooter_In_01", scooter(rng, arriving=True))
    write("SFX_Scooter_Out_01", scooter(rng, arriving=False))
    write("SFX_Phone_01", phone_ring())
    write("SFX_Join_01", join_pop())
    write("SFX_Fanfare_01", fanfare())


if __name__ == "__main__":
    main()
