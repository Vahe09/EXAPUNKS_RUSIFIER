from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


PAGE_HEADER = "===== PAGE "


def load_pages(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    pages: list[str] = []
    current: list[str] = []
    for line in text.splitlines():
        if line.startswith(PAGE_HEADER):
            if current:
                pages.append("\n".join(current).strip())
                current = []
            continue
        current.append(line)
    if current:
        pages.append("\n".join(current).strip())
    return pages


def wrap_text(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    wrapped: list[str] = []
    for paragraph in text.split("\n"):
        paragraph = paragraph.strip()
        if not paragraph:
            wrapped.append("")
            continue
        words = paragraph.split()
        line = words[0]
        for word in words[1:]:
            probe = f"{line} {word}"
            if draw.textlength(probe, font=font) <= max_width:
                line = probe
            else:
                wrapped.append(line)
                line = word
        wrapped.append(line)
    return wrapped


def get_font(size: int) -> ImageFont.FreeTypeFont:
    candidates = [
        Path("C:/Windows/Fonts/arial.ttf"),
        Path("C:/Windows/Fonts/Arial.ttf"),
        Path("C:/Windows/Fonts/tahoma.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def render_page(text: str, title: str, page_number: int, width: int = 1654, height: int = 2339) -> Image.Image:
    page = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(page)

    title_font = get_font(40)
    body_font = get_font(28)
    footer_font = get_font(24)

    margin_x = 110
    y = 90
    draw.text((margin_x, y), title, fill="black", font=title_font)
    y += 90

    lines = wrap_text(draw, text, body_font, width - margin_x * 2)
    line_height = 38
    for line in lines:
        if y > height - 120:
            break
        draw.text((margin_x, y), line, fill="black", font=body_font)
        y += line_height

    footer = f"{page_number}"
    footer_w = draw.textlength(footer, font=footer_font)
    draw.text((width - margin_x - footer_w, height - 70), footer, fill="black", font=footer_font)
    return page


def build_pdf(src_txt: Path, out_pdf: Path, title: str) -> None:
    pages = load_pages(src_txt)
    rendered = [render_page(text=page_text, title=title, page_number=index + 1) for index, page_text in enumerate(pages)]
    out_pdf.parent.mkdir(parents=True, exist_ok=True)
    first, *rest = rendered
    first.save(out_pdf, save_all=True, append_images=rest, resolution=150.0)


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("src_txt")
    parser.add_argument("out_pdf")
    parser.add_argument("title")
    args = parser.parse_args()

    build_pdf(Path(args.src_txt), Path(args.out_pdf), args.title)


if __name__ == "__main__":
    main()
