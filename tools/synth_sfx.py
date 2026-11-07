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


if __name__ == "__main__":
    main()
