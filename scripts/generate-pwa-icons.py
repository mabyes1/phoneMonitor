from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "src" / "PhoneMonitor.Host" / "wwwroot" / "icons"


def load_font(size):
    candidates = [
        Path("C:/Windows/Fonts/segoeuib.ttf"),
        Path("C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arialbd.ttf"),
    ]
    for path in candidates:
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def draw_icon(size, maskable=False):
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    pad = 0 if maskable else round(size * 0.055)
    radius = round(size * (0.19 if maskable else 0.16))
    draw.rounded_rectangle(
        [pad, pad, size - pad, size - pad],
        radius=radius,
        fill=(17, 24, 32, 255),
    )

    inner = round(size * 0.12)
    draw.rounded_rectangle(
        [inner, inner, size - inner, size - inner],
        radius=round(size * 0.09),
        outline=(143, 209, 255, 210),
        width=max(3, round(size * 0.018)),
        fill=(26, 38, 48, 245),
    )

    screen = [
        round(size * 0.25),
        round(size * 0.23),
        round(size * 0.75),
        round(size * 0.57),
    ]
    draw.rounded_rectangle(
        screen,
        radius=round(size * 0.035),
        fill=(9, 13, 18, 255),
        outline=(112, 217, 139, 210),
        width=max(2, round(size * 0.012)),
    )

    for index, color in enumerate([(112, 217, 139, 255), (102, 197, 232, 245), (255, 210, 122, 235)]):
        y = round(size * (0.31 + index * 0.082))
        x1 = round(size * 0.31)
        x2 = round(size * (0.69 - index * 0.075))
        draw.rounded_rectangle(
            [x1, y, x2, y + max(3, round(size * 0.028))],
            radius=round(size * 0.014),
            fill=color,
        )

    font = load_font(round(size * 0.19))
    text = "PM"
    bbox = draw.textbbox((0, 0), text, font=font)
    text_w = bbox[2] - bbox[0]
    text_h = bbox[3] - bbox[1]
    draw.text(
        ((size - text_w) / 2, round(size * 0.64) - text_h / 2),
        text,
        font=font,
        fill=(245, 247, 251, 255),
    )

    home_y = round(size * 0.82)
    draw.rounded_rectangle(
        [round(size * 0.43), home_y, round(size * 0.57), home_y + max(3, round(size * 0.018))],
        radius=round(size * 0.012),
        fill=(185, 196, 208, 180),
    )
    return canvas


def save_icon(name, size, maskable=False):
    icon = draw_icon(size, maskable=maskable)
    icon.save(OUT / name)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    save_icon("icon-192.png", 192)
    save_icon("icon-512.png", 512)
    save_icon("maskable-512.png", 512, maskable=True)
    save_icon("apple-touch-icon.png", 180)


if __name__ == "__main__":
    main()
