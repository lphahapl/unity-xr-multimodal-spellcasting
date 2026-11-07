from pathlib import Path
import sys
from PIL import Image, ImageChops, ImageDraw

root = Path(sys.argv[1])
replacements = Path(sys.argv[2]) if len(sys.argv) > 2 else root
names = ["01_正面", "02_左前45度", "03_右前45度", "04_左面",
         "05_右面", "06_背面", "07_顶面", "08_底面"]
expected_sizes = [(1030,286),(744,286),(744,286),(22,286),
                  (22,286),(1030,286),(1030,22),(1030,22)]
assert sorted(f.stem for f in root.glob("*.png")) == names
preview = Image.new("RGB", (1600, 850), "#ededed")
label = ImageDraw.Draw(preview)
for i, name in enumerate(names):
    directory = replacements if i in (1, 2, 3, 4) else root
    with Image.open(directory / f"{name}.png") as file:
        assert file.format == "PNG" and file.size == (1200, 1200)
        img = file.convert("RGB")
        background = Image.new("RGB", img.size, img.getpixel((0, 0)))
        diff = ImageChops.difference(img, background).convert("L")
        left, top, right, bottom = diff.point(lambda p: 255 if p > 14 else 0).getbbox()
        assert 25 < left < right < 1175 and 25 < top < bottom < 1175
        width, height = right-left, bottom-top
        expected_width, expected_height = expected_sizes[i]
        assert abs(width - expected_width) <= 3 and abs(height - expected_height) <= 3, name
        print(f"{name}: {right-left} x {bottom-top} pixels, full silhouette")
        img.thumbnail((396, 396), Image.Resampling.LANCZOS)
        x, y = (i % 4) * 400, (i // 4) * 425
        preview.paste(img, (x + 2, y + 2))
        label.text((x + 193, y + 402), name[:2], fill="#222222")
preview.save(Path(__file__).parent / "qa_preview.jpg", quality=93)
