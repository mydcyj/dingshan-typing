"""Render the 100 BPM, two-bar Snake loop as mono PCM16."""

from array import array
from math import exp, sin, tau
from pathlib import Path
from random import Random
import sys
import wave


SAMPLE_RATE = 24000
BPM = 100
STEP_SECONDS = 60 / BPM / 4
STEP_FRAMES = round(SAMPLE_RATE * STEP_SECONDS)

# F major. MIDI A4 is 69, distinct from the A3 (57) in the second bar.
MELODY = (53, 53, 69, 69, 53, 53, 50, 50) * 2 + (48, 48, 53, 53, 48, 48, 57, 57) * 2


def render():
    pcm = array("h")
    noise = Random(171)

    for step, note in enumerate(MELODY):
        frequency = 440 * 2 ** ((note - 69) / 12)
        accent = 1.0 if step % 2 == 0 else 0.85
        kick = step % 16 in (0, 7, 8)
        snare = step % 8 == 4
        hat = step % 2 == 0 or step in (15, 31)
        low_noise = 0.0

        for sample in range(STEP_FRAMES):
            t = sample / SAMPLE_RATE
            remaining = (STEP_FRAMES - 1 - sample) / SAMPLE_RATE
            edge = min(1, t / 0.004, remaining / 0.016)
            phase = tau * frequency * t
            partials = sin(phase) + exp(-t * 17) * (0.34 * sin(2 * phase) + 0.15 * sin(3 * phase))
            lead = 0.25 * accent * partials * (0.56 + 0.44 * exp(-t * 14))

            drum = 0.0
            if kick:
                # Exponential pitch sweep with phase continuous across the attack.
                drum = 0.24 * sin(tau * (45 * t + 2.1 * (1 - exp(-38 * t)))) * exp(-25 * t)
            if snare or hat:
                white_noise = noise.uniform(-1.0, 1.0)
                low_noise += 0.44 * (white_noise - low_noise)
                fizz = white_noise - low_noise
                if snare:
                    drum += 0.14 * exp(-30 * t) * (fizz + 0.38 * sin(tau * 174 * t))
                if hat:
                    drum += (0.042 if step % 4 == 2 else 0.026) * exp(-65 * t) * fizz

            pcm.append(round((lead + drum) * edge * 32767))

    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


if __name__ == "__main__":
    with wave.open(str(Path(__file__).with_name("SnakeMusic.wav")), "wb") as output:
        output.setparams((1, 2, SAMPLE_RATE, 0, "NONE", "not compressed"))
        output.writeframes(render())
