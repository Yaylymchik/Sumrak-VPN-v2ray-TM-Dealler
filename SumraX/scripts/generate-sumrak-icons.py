"""Generate SumraX icon sizes from Desktop\\SumrakVPN_icon_source.png."""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

SRC = Path(r"C:\Users\Programming\Desktop\SumrakVPN_icon_source.png")
ASSETS = Path(__file__).resolve().parents[1] / "src" / "SumraX.App" / "Assets"
SIZES = [16, 32, 48, 64, 128, 256]


def main() -> int:
    if not SRC.is_file():
        print(f"Missing logo: {SRC}", file=sys.stderr)
        return 1

    ASSETS.mkdir(parents=True, exist_ok=True)
    base = Image.open(SRC).convert("RGBA")
    ico_images: list[Image.Image] = []

    for size in SIZES:
        im = base.resize((size, size), Image.Resampling.LANCZOS)
        ico_images.append(im)
        name = "sumrax-icon.png" if size == 256 else f"sumrax-icon-{size}.png"
        im.save(ASSETS / name, format="PNG")
        print(f"wrote {name}")

    logo = base.resize((512, 512), Image.Resampling.LANCZOS)
    logo.save(ASSETS / "sumrax-logo.png", format="PNG")
    logo.save(ASSETS / "sumrak-mascot-source.png", format="PNG")

    ico_images[-1].save(
        ASSETS / "sumrax.ico",
        format="ICO",
        sizes=[(im.width, im.height) for im in ico_images],
    )
    print("wrote sumrax.ico")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())