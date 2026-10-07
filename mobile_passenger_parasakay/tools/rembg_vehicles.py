"""Cut vehicle JPGs to transparent PNGs with padding so UI never clips the art."""
from pathlib import Path

from PIL import Image
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


def cutout(src: Image.Image) -> Image.Image:
    # Extra black border so rembg does not chew helmets / tires at the frame edge.
    border = 96
    canvas = Image.new("RGBA", (src.width + border * 2, src.height + border * 2), (0, 0, 0, 255))
    canvas.paste(src.convert("RGBA"), (border, border))
    cut = remove(canvas).convert("RGBA")
    px = cut.load()
    w, h = cut.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 16:
                px[x, y] = (0, 0, 0, 0)
            elif a < 200 and r <= 35 and g <= 35 and b <= 35:
                px[x, y] = (0, 0, 0, 0)

    solid = cut.split()[3].point(lambda v: 255 if v >= 128 else 0)
    bbox = solid.getbbox()
    if not bbox:
        return cut
    cropped = cut.crop(bbox)
    cw, ch = cropped.size
    mx, my = max(32, int(cw * 0.18)), max(32, int(ch * 0.18))
    final = Image.new("RGBA", (cw + mx * 2, ch + my * 2), (0, 0, 0, 0))
    final.paste(cropped, (mx, my), cropped)
    return final


def main() -> None:
    web_dir.mkdir(parents=True, exist_ok=True)
    for name in names:
        jpg = src_dir / f"{name}.jpg"
        png = src_dir / f"{name}.png"
        if jpg.exists():
            print(f"rembg {name}...")
            out = cutout(Image.open(jpg))
        elif png.exists():
            print(f"repad {name}...")
            # Already transparent — just ensure solid margins.
            im = Image.open(png).convert("RGBA")
            solid = im.split()[3].point(lambda v: 255 if v >= 128 else 0)
            bbox = solid.getbbox()
            if not bbox:
                continue
            cropped = im.crop(bbox)
            cw, ch = cropped.size
            mx, my = max(32, int(cw * 0.18)), max(32, int(ch * 0.18))
            out = Image.new("RGBA", (cw + mx * 2, ch + my * 2), (0, 0, 0, 0))
            out.paste(cropped, (mx, my), cropped)
        else:
            print(f"skip {name}")
            continue

        target_w = 1536
        if out.width != target_w:
            out = out.resize(
                (target_w, max(1, int(out.height * target_w / out.width))),
                Image.Resampling.LANCZOS,
            )
        out.save(src_dir / f"{name}.png", "PNG", optimize=True)
        out.save(web_dir / f"{name}.png", "PNG", optimize=True)
        print(f"  -> {name}.png {out.size}")
    print("done")


if __name__ == "__main__":
    main()
