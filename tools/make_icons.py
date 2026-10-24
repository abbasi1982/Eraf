"""Generates the ERAF app icon set (Assets/*.png and AppIcon.ico) from one drawing.

Usage: python tools/make_icons.py
Needs Pillow. Output goes to src/Eraf.Windows/Assets. The design: a rounded indigo-to-teal tile holding a photo
card (sun and mountains) with a white voice-note pill (sound bars) overlapping its lower edge, i.e. one photo + one voice.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'src', 'Eraf.Windows', 'Assets')
S = 2048  # drawing canvas; everything below is in 0..1 units of this


def px(v):
    return int(round(v * S))


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def gradient(size, c0, c1, c2=None):
    """Diagonal gradient (top-left to bottom-right)."""
    g = Image.new('RGB', (size, size))
    d = ImageDraw.Draw(g)
    for i in range(size * 2):
        t = i / (size * 2 - 1)
        col = lerp(c0, c1, t) if c2 is None else (lerp(c0, c1, t * 2) if t < 0.5 else lerp(c1, c2, (t - 0.5) * 2))
        d.line([(i, 0), (0, i)], fill=col, width=2)
    return g


def rounded_mask(box, radius):
    m = Image.new('L', (S, S), 0)
    ImageDraw.Draw(m).rounded_rectangle([px(v) for v in box], radius=px(radius), fill=255)
    return m


def shadow(mask, dy, blur, alpha):
    sh = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    solid = Image.new('RGBA', (S, S), (10, 14, 60, alpha))
    sh.paste(solid, (0, px(dy)), mask)
    return sh.filter(ImageFilter.GaussianBlur(px(blur)))


def draw_icon():
    """Returns the 2048x2048 RGBA icon (tile with a transparent margin of 0.04)."""
    canvas = Image.new('RGBA', (S, S), (0, 0, 0, 0))

    # Tile
    tile_box = (0.04, 0.04, 0.96, 0.96)
    tile_mask = rounded_mask(tile_box, 0.215)
    bg = gradient(S, (88, 80, 236), (60, 130, 240), (20, 184, 200)).convert('RGBA')
    canvas.paste(bg, (0, 0), tile_mask)
    # soft top-left light
    glow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([px(-0.2), px(-0.35), px(0.75), px(0.45)], fill=(255, 255, 255, 60))
    glow = glow.filter(ImageFilter.GaussianBlur(px(0.08)))
    glow.putalpha(Image.eval(Image.composite(glow.getchannel('A'), Image.new('L', (S, S), 0), tile_mask), lambda v: v))
    canvas.alpha_composite(glow)

    # Photo card (slightly up and left of centre)
    card_box = (0.19, 0.20, 0.81, 0.66)
    card_mask = rounded_mask(card_box, 0.07)
    canvas.alpha_composite(shadow(card_mask, 0.025, 0.025, 110))
    card = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    card.paste(Image.new('RGBA', (S, S), (255, 255, 255, 255)), (0, 0), card_mask)
    canvas.alpha_composite(card)

    # Picture inside the card
    pic_box = (0.225, 0.235, 0.775, 0.625)
    pic_mask = rounded_mask(pic_box, 0.045)
    sky = gradient(S, (176, 214, 255), (222, 240, 255)).convert('RGBA')
    pic = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    pic.paste(sky, (0, 0), pic_mask)
    d = ImageDraw.Draw(pic)
    d.ellipse([px(0.60), px(0.265), px(0.71), px(0.375)], fill=(255, 196, 66, 255))  # sun
    far = [(px(0.225), px(0.625)), (px(0.225), px(0.52)), (px(0.33), px(0.39)), (px(0.46), px(0.54)), (px(0.52), px(0.625))]
    near = [(px(0.40), px(0.625)), (px(0.56), px(0.42)), (px(0.775), px(0.625))]
    d.polygon(far, fill=(120, 130, 235, 255))
    d.polygon(near, fill=(70, 80, 200, 255))
    clipped = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    clipped.paste(pic, (0, 0), pic_mask)
    canvas.alpha_composite(clipped)

    # Voice pill overlapping the card's lower edge
    pill_box = (0.27, 0.58, 0.83, 0.80)
    pill_mask = rounded_mask(pill_box, 0.11)
    canvas.alpha_composite(shadow(pill_mask, 0.03, 0.03, 130))
    pill = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    pill.paste(Image.new('RGBA', (S, S), (255, 255, 255, 255)), (0, 0), pill_mask)
    canvas.alpha_composite(pill)
    d = ImageDraw.Draw(canvas)
    heights = [0.03, 0.055, 0.082, 0.055, 0.068, 0.04, 0.022]
    x0, step, w = 0.33, 0.0685, 0.030
    cy = 0.69
    cols = [(88, 80, 236), (74, 100, 238), (60, 130, 240), (45, 150, 230), (30, 168, 215), (20, 180, 205), (20, 184, 200)]
    for i, h in enumerate(heights):
        x = x0 + i * step
        d.rounded_rectangle([px(x), px(cy - h), px(x + w), px(cy + h)], radius=px(w / 2), fill=cols[i] + (255,))
    return canvas


def fit(img, size, pad=0.0):
    """Resize the drawing to size x size, cropping the transparent margin (pad is extra margin as a fraction)."""
    crop = img.crop((px(0.04), px(0.04), px(0.96), px(0.96)))
    inner = int(round(size * (1 - 2 * pad)))
    r = crop.resize((inner, inner), Image.LANCZOS)
    out = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    out.alpha_composite(r, ((size - inner) // 2, (size - inner) // 2))
    return out


def centered(img, w, h, icon_h_frac, bg=None):
    icon = fit(img, int(h * icon_h_frac))
    out = Image.new('RGBA', (w, h), bg or (0, 0, 0, 0))
    out.alpha_composite(icon, ((w - icon.width) // 2, (h - icon.height) // 2))
    return out


def main():
    icon = draw_icon()
    save = lambda im, name: im.save(os.path.join(OUT, name))
    save(fit(icon, 88), 'Square44x44Logo.scale-200.png')
    save(fit(icon, 24), 'Square44x44Logo.targetsize-24_altform-unplated.png')
    save(fit(icon, 48), 'Square44x44Logo.targetsize-48_altform-lightunplated.png')
    save(fit(icon, 300, pad=0.04), 'Square150x150Logo.scale-200.png')
    save(centered(icon, 620, 300, 0.80), 'Wide310x150Logo.scale-200.png')
    save(fit(icon, 96), 'LockScreenLogo.scale-200.png')
    save(fit(icon, 100), 'StoreLogo.png')
    splash_bg = gradient(1240, (22, 24, 58), (34, 32, 92)).crop((0, 0, 1240, 600)).convert('RGBA')
    splash = splash_bg.copy()
    ic = fit(icon, 360)
    splash.alpha_composite(ic, ((1240 - 360) // 2, (600 - 360) // 2))
    save(splash, 'SplashScreen.scale-200.png')
    sizes = [16, 24, 32, 48, 64, 128, 256]
    fit(icon, 256).save(os.path.join(OUT, 'AppIcon.ico'), sizes=[(s, s) for s in sizes])
    # Preview sheet for review (not shipped)
    sheet = Image.new('RGBA', (1100, 560), (245, 246, 250, 255))
    sheet.alpha_composite(fit(icon, 480), (20, 40))
    x = 540
    for s in (128, 64, 48, 32, 24):
        sheet.alpha_composite(fit(icon, s), (x, 60))
        x += s + 20
    sheet.alpha_composite(centered(icon, 620, 300, 0.80, (28, 30, 70, 255)).resize((310, 150)), (540, 260))
    sheet.save(os.path.join(HERE, 'icon_preview.png'))
    print('ok')


if __name__ == '__main__':
    main()
