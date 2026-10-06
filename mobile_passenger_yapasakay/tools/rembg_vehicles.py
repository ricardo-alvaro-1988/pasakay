"""Cut vehicle JPGs to clean transparent PNGs with rembg + fringe cleanup + padding."""
from pathlib import Path

from PIL import Image, ImageFilter
from rembg import remove

src_dir = Path(__file__).resolve().parents[1] / "assets" / "vehicles"
web_dir = Path(__file__).resolve().parents[3] / "web" / "customer" / "public" / "vehicles"
names = [
    "motorcycle",
    "tricycle",
    "tuktuk",
    "mpv",
    "van",
    "pickup_l300",
    "pickup_cargo",
    "sedan",
    "suv",
]


def clean_fringe(im: Image.Image) -> Image.Image:
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if a < 40:
                px[x, y] = (0, 0, 0, 0)
                continue
            if a < 250 and r >= 220 and g >= 220 and b >= 220:
                px[x, y] = (0, 0, 0, 0)
                continue
            if a < 250 and r <= 28 and g <= 28 and b <= 28:
                px[x, y] = (0, 0, 0, 0)
                continue

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

    a = im.split()[3].filter(ImageFilter.GaussianBlur(radius=0.6))
    im.putalpha(a)
    return im


def pad_subject(im: Image.Image, pad_frac: float = 0.12) -> Image.Image:
    """Keep full subject with transparent margin so UI boxes never clip wheels/helmets."""
    bbox = im.split()[3].getbbox()
    if not bbox:
        return im
    cropped = im.crop(bbox)
    w, h = cropped.size
    pad_x = max(24, int(w * pad_frac))
    pad_y = max(24, int(h * pad_frac))
    canvas = Image.new("RGBA", (w + pad_x * 2, h + pad_y * 2), (0, 0, 0, 0))
    canvas.paste(cropped, (pad_x, pad_y), cropped)
    return canvas


def main() -> None:
    web_dir.mkdir(parents=True, exist_ok=True)
    for name in names:
        jpg = src_dir / f"{name}.jpg"
        png_src = src_dir / f"{name}.png"
        # Prefer jpg for cutout; sedan/suv may only have clean png already.
        if jpg.exists():
            print(f"rembg {name}...")
            src = Image.open(jpg).convert("RGBA")
            cut = remove(src)
            cut = clean_fringe(cut)
            cut = pad_subject(cut, 0.14)
        elif png_src.exists():
            print(f"repad {name}...")
            cut = pad_subject(Image.open(png_src).convert("RGBA"), 0.14)
        else:
            print(f"skip {name}: no source")
            continue

        target_w = 1536
        if cut.width != target_w:
            ratio = target_w / cut.width
            cut = cut.resize((target_w, max(1, int(cut.height * ratio))), Image.Resampling.LANCZOS)

        out = src_dir / f"{name}.png"
        cut.save(out, "PNG", optimize=True)
        cut.save(web_dir / f"{name}.png", "PNG", optimize=True)
        print(f"  -> {out.name} {cut.size}")
    print("done")


if __name__ == "__main__":
    main()
