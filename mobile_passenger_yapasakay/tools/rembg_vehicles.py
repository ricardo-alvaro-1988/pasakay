"""Cut vehicle JPGs to clean transparent PNGs with rembg + fringe cleanup."""
from pathlib import Path

from PIL import Image, ImageFilter
from rembg import remove

src_dir = Path(__file__).resolve().parents[1] / "assets" / "vehicles"
names = [
    "motorcycle",
    "tricycle",
    "tuktuk",
    "mpv",
    "van",
    "pickup_l300",
    "pickup_cargo",
]


def clean_fringe(im: Image.Image) -> Image.Image:
    """Remove white/black halos left by JPEG + rembg on soft edges."""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    # Pass 1: clear near-white / near-black fringe when semi-transparent
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if a < 40:
                px[x, y] = (0, 0, 0, 0)
                continue
            # White halo
            if a < 250 and r >= 220 and g >= 220 and b >= 220:
                px[x, y] = (0, 0, 0, 0)
                continue
            # Black studio leftover
            if a < 250 and r <= 28 and g <= 28 and b <= 28:
                px[x, y] = (0, 0, 0, 0)
                continue

    # Pass 2: despill — color soft edges from nearest opaque pixels
    src = im.copy()
    spx = src.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = spx[x, y]
            if a == 0 or a >= 250:
                continue
            rs = gs = bs = n = 0
            for dy in (-2, -1, 0, 1, 2):
                for dx in (-2, -1, 0, 1, 2):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h:
                        rr, gg, bb, aa = spx[nx, ny]
                        if aa >= 245 and not (rr >= 220 and gg >= 220 and bb >= 220):
                            rs += rr
                            gs += gg
                            bs += bb
                            n += 1
            if n > 0:
                px[x, y] = (rs // n, gs // n, bs // n, a)

    # Slight alpha smooth so edges aren't stair-steppy at UI size
    a = im.split()[3].filter(ImageFilter.GaussianBlur(radius=0.6))
    im.putalpha(a)
    return im


def trim_transparent(im: Image.Image, pad: int = 16) -> Image.Image:
    bbox = im.split()[3].getbbox()
    if not bbox:
        return im
    l, t, r, b = bbox
    l = max(0, l - pad)
    t = max(0, t - pad)
    r = min(im.width, r + pad)
    b = min(im.height, b + pad)
    return im.crop((l, t, r, b))


def main() -> None:
    for name in names:
        jpg = src_dir / f"{name}.jpg"
        png = src_dir / f"{name}.png"
        if not jpg.exists():
            print(f"skip {name}: no jpg")
            continue
        print(f"rembg {name}...")
        src = Image.open(jpg).convert("RGBA")
        cut = remove(src)
        cut = clean_fringe(cut)
        cut = trim_transparent(cut)
        target_w = 1536
        if cut.width != target_w:
            ratio = target_w / cut.width
            cut = cut.resize((target_w, max(1, int(cut.height * ratio))), Image.Resampling.LANCZOS)
            cut = clean_fringe(cut)
        cut.save(png, "PNG", optimize=True)
        print(f"  -> {png.name} {cut.size}")
    print("done")


if __name__ == "__main__":
    main()
