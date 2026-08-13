"""A minimal 8-bit RGB PNG writer, so these scripts need nothing but fontTools."""
import struct
import zlib


def write_rgba(path, width, height, is_lit, fg=(255, 255, 255)):
    """Write a one-colour image on a transparent ground."""
    on = bytes(fg) + b"\xff"
    off = b"\x00\x00\x00\x00"
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw += on if is_lit(x, y) else off
    _write(path, width, height, 6, bytes(raw))


def write_rgb(path, width, height, is_lit, fg=(255, 255, 255), bg=(0, 0, 0)):
    """Write a two-colour image.  is_lit(x, y) decides the foreground."""
    fg = bytes(fg)
    bg = bytes(bg)
    raw = bytearray()
    for y in range(height):
        raw.append(0)                    # filter type 0
        for x in range(width):
            raw += fg if is_lit(x, y) else bg
    _write(path, width, height, 2, bytes(raw))


def _write(path, width, height, colour_type, raw):
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))

    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR",
                   struct.pack(">IIBBBBB", width, height, 8, colour_type, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9))
           + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)
