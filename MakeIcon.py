import math
import struct
import zlib
from pathlib import Path


def inside_round_rect(x, y, left, top, width, height, radius):
    dx = max(left + radius - x, x - (left + width - radius), 0)
    dy = max(top + radius - y, y - (top + height - radius), 0)
    return dx * dx + dy * dy <= radius * radius


def paint(x, y):
    if not inside_round_rect(x, y, 3, 3, 58, 58, 14):
        return (0, 0, 0, 0)
    if not inside_round_rect(x, y, 3, 3, 58, 53, 14):
        return (14, 28, 62, 255)

    if inside_round_rect(x, y, 46.4, 11.8, 7.3, 7.3, 1.5):
        return (255, 177, 97, 255)

    snake = (
        inside_round_rect(x, y, 12, 16, 9, 32, 4.5)
        or inside_round_rect(x, y, 12, 39, 31, 9, 4.5)
        or inside_round_rect(x, y, 34, 25, 9, 23, 4.5)
        or inside_round_rect(x, y, 33.6, 22, 19, 13, 4.5)
    )
    if snake:
        if (x - 47.4) ** 2 + (y - 27) ** 2 <= 2.05 ** 2:
            return (24, 48, 73, 255)
        return (132, 235, 172, 255)
    return (35, 65, 128, 255)


def render(size, samples):
    pixels = bytearray()
    scale = 64 / (size * samples)
    total = samples * samples
    for y in range(size):
        for x in range(size):
            red = green = blue = covered = 0
            for sy in range(samples):
                for sx in range(samples):
                    r, g, b, a = paint((x * samples + sx + 0.5) * scale, (y * samples + sy + 0.5) * scale)
                    if a:
                        red += r
                        green += g
                        blue += b
                        covered += 1
            if covered:
                pixels.extend((round(red / covered), round(green / covered), round(blue / covered),
                               round(255 * covered / total)))
            else:
                pixels.extend((0, 0, 0, 0))
    return pixels


def png(size, rgba):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    stride = size * 4
    rows = b"".join(b"\0" + bytes(rgba[start:start + stride]) for start in range(0, len(rgba), stride))
    return (b"\x89PNG\r\n\x1a\n" +
            chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)) +
            chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b""))


def icon_frame(size, rgba):
    # Small icon frames use 32-bit DIBs with an aligned transparency mask.
    colors = bytearray()
    mask_stride = math.ceil(size / 32) * 4
    mask = bytearray(mask_stride * size)
    for row, y in enumerate(range(size - 1, -1, -1)):
        for x in range(size):
            offset = (y * size + x) * 4
            r, g, b, a = rgba[offset:offset + 4]
            colors.extend((b, g, r, a))
            if a == 0:
                mask[row * mask_stride + x // 8] |= 128 >> (x % 8)
    return (struct.pack("<IiiHHIIiiII", 40, size, 2 * size, 1, 32, 0, len(colors), 0, 0, 0, 0)
            + colors + mask)


def main():
    folder = Path(__file__).resolve().parent
    sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
    directory = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
    offset = 6 + 16 * len(sizes)
    frames = []
    for size in sizes:
        rgba = render(size, 4 if size <= 64 else 2)
        data = icon_frame(size, rgba) if size <= 48 else png(size, rgba)
        directory.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        frames.append(data)
        offset += len(data)
        if size == 256:
            (folder / "TypingIcon.png").write_bytes(data)
    (folder / "TypingIcon.ico").write_bytes(directory + b"".join(frames))


if __name__ == "__main__":
    main()
