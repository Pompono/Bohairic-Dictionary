"""Recreate the Windows icon with Python and Pillow: python scripts/create-icon.py."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root = Path(__file__).resolve().parent.parent
assets = root / 'src/Bohairic.Desktop/Assets'
image = Image.new('RGBA', (1024, 1024))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((32, 32, 992, 992), radius=210, fill='#174D50')
draw.rounded_rectangle((205, 165, 829, 845), radius=65, fill='#0D3437')
draw.rounded_rectangle((179, 139, 805, 817), radius=60, fill='#F4EFE2')
draw.rounded_rectangle((179, 139, 303, 817), radius=45, fill='#C88863')
draw.rectangle((259, 139, 307, 817), fill='#C88863')
draw.line((336, 743, 748, 743), fill='#C8C2B4', width=14)
font = ImageFont.truetype(str(assets / 'FreeSerifAvvaShenouda.ttf'), 480)
glyph = 'ⲃ'
bounds = draw.textbbox((0,0), glyph, font=font)
x = 541 - (bounds[2] - bounds[0]) / 2 - bounds[0]
y = 449 - (bounds[3] - bounds[1]) / 2 - bounds[1]
draw.text((x,y), glyph, font=font, fill='#174D50')
image.resize((256,256), Image.Resampling.LANCZOS).save(assets / 'BohairicDictionary.png')
image.save(assets / 'BohairicDictionary.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
