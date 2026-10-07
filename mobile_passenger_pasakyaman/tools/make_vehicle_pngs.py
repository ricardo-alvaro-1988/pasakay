from pathlib import Path
from collections import deque
from PIL import Image

src_dir = Path(__file__).resolve().parents[1] / "assets" / "vehicles"
names = [
    "motorcycle",
    "tricycle",
    "tuktuk",
    "sedan",
    "mpv",
    "suv",
    "van",
    "pickup_l300",
    "pickup_cargo",
]
HARD = 28
SOFT = 55


def is_bg_hard(r: int, g: int, b: int) -> bool:
    return r <= HARD and g <= HARD and b <= HARD


def is_bg_soft(r: int, g: int, b: int) -> bool:
    return r <= SOFT and g <= SOFT and b <= SOFT


def convert(path: Path, out: Path) -> None:
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    px = im.load()
    visited = [[False] * h for _ in range(w)]
    q: deque[tuple[int, int]] = deque()

    def try_seed(x: int, y: int) -> None:
        r, g, b, _a = px[x, y]
        if is_bg_soft(r, g, b) and not visited[x][y]:
            q.append((x, y))
            visited[x][y] = True

    for x in range(w):
        try_seed(x, 0)
        try_seed(x, h - 1)
    for y in range(h):
        try_seed(0, y)
        try_seed(w - 1, y)

    while q:
        x, y = q.popleft()
        r, g, b, _a = px[x, y]
        if is_bg_hard(r, g, b):
            px[x, y] = (0, 0, 0, 0)
        elif is_bg_soft(r, g, b):
            lum = (r + g + b) / 3.0
            alpha = int(max(0, min(255, (lum - HARD) / (SOFT - HARD) * 255)))
            px[x, y] = (r, g, b, alpha)
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
                rr, gg, bb, _aa = px[nx, ny]
                visited[nx][ny] = True
                if is_bg_soft(rr, gg, bb):
                    q.append((nx, ny))

    im.save(out, "PNG")
    print(f"{path.name} -> {out.name} ({w}x{h})")


def main() -> None:
    for name in names:
        jpg = src_dir / f"{name}.jpg"
        png = src_dir / f"{name}.png"
        # Prefer source PNGs that already have alpha (e.g. sedan/suv).
        if png.exists():
            try:
                existing = Image.open(png)
                if existing.mode in ("RGBA", "LA") or "transparency" in existing.info:
                    a = existing.convert("RGBA").split()[3]
                    if any(v == 0 for v in a.getdata()):
                        print(f"skip {png.name} (already transparent)")
                        continue
            except Exception:
                pass
        if not jpg.exists():
            print(f"MISSING {jpg}")
            continue
        convert(jpg, png)
    print("done")


if __name__ == "__main__":
    main()
