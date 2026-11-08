"""Gelişmiş arka plan temizleme:
- arka plan rengini kenarlardan bulur (beyaz da olsa gri de olsa)
- kenara değen arka planı + yerdeki gölgeyi siler
- sadece arka planla aynı renkteki iç boşlukları siler (beyaz kürk/hayalet korunur)
- ana karakteri tutar (en büyük parça + ona yakın büyüklükteki parçalar)
- piksel bloklarına küçültür, 40 renge indirir
kullanım: temizle3.py giris.png cikis.png hedef_boy
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

src, dst, target = sys.argv[1], sys.argv[2], int(sys.argv[3])
a = np.asarray(Image.open(src).convert('RGB')).astype(np.int32)
H, W, _ = a.shape
kenar = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
bgc = np.median(kenar, axis=0)
uzak = np.abs(a - bgc).max(2)
lum = a.mean(2)
bg_lum = bgc.mean()
notr = (a.max(2) - a.min(2)) < 18

# kenara değen arka plan
aday = uzak < 32
lab, n = ndimage.label(aday)
kenar_et = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
bg = np.isin(lab, list(kenar_et))
# kenara değen bembeyaz zemin/ışık (arka plan griyse bile)
beyaz = (a.min(2) > 232) & notr if False else (a.min(2) > 232) & ((a.max(2) - a.min(2)) < 18)
labw, _ = ndimage.label(beyaz)
kw = set(np.unique(np.concatenate([labw[0], labw[-1], labw[:, 0], labw[:, -1]]))) - {0}
bg |= np.isin(labw, list(kw))
# yerdeki gri gölge (alt %35, nötr, arka plandan biraz koyu) — arka plana bağlıysa
yy = np.arange(H)[:, None] * np.ones((1, W))
golge = notr & (bg_lum - lum < 70) & (bg_lum - lum > -10) & (yy > H * 0.65)
lab2, _ = ndimage.label(golge | bg)
bagli = set(np.unique(lab2[bg])) - {0}
bg |= np.isin(lab2, list(bagli)) & golge
# iç boşluklar: sadece arka planla neredeyse aynı renk ve büyükse
ic = (uzak < 12) & ~bg
lab3, n3 = ndimage.label(ic)
if n3:
    boy = ndimage.sum(np.ones_like(lab3), lab3, range(1, n3 + 1))
    buyuk = [i + 1 for i, s in enumerate(boy) if s > 2500]
    bg |= np.isin(lab3, buyuk)
bg = ndimage.binary_dilation(bg, iterations=2)
fg = ~bg
# ana karakter: en büyük bileşen + en az %35'i kadar büyük olanlar
lab4, n4 = ndimage.label(fg, structure=np.ones((3, 3)))
if n4 > 1:
    boy = ndimage.sum(np.ones_like(lab4), lab4, range(1, n4 + 1))
    ana = int(np.argmax(boy)) + 1
    ay, ax = np.where(lab4 == ana)
    y0, y1, x0, x1 = ay.min(), ay.max(), ax.min(), ax.max()
    py, px = (y1 - y0) * 0.08, (x1 - x0) * 0.08
    merk = ndimage.center_of_mass(np.ones_like(lab4), lab4, range(1, n4 + 1))
    tut = [i + 1 for i, (cy, cx) in enumerate(merk)
           if i + 1 == ana or (y0 - py <= cy <= y1 + py and x0 - px <= cx <= x1 + px and boy[i] > 30)]
    fg = np.isin(lab4, tut)
ys, xs = np.where(fg)
fh = ys.max() - ys.min() + 1
f = max(3, round(fh / target))
h2, w2 = H // f, W // f
fgb = fg[:h2 * f, :w2 * f].reshape(h2, f, w2, f)
cb = a[:h2 * f, :w2 * f].reshape(h2, f, w2, f, 3)
cnt = fgb.sum((1, 3))
col = (cb * fgb[..., None]).sum((1, 3)) / np.maximum(cnt, 1)[..., None]
alpha = cnt > f * f * 0.5
# tek başına kalan pikselleri temizle
komsu = ndimage.convolve(alpha.astype(int), np.ones((3, 3)), mode='constant') - alpha
alpha &= komsu >= 2
out = np.zeros((h2, w2, 4), np.uint8)
out[..., :3] = col.clip(0, 255)
out[..., 3] = alpha * 255
im = Image.fromarray(out, 'RGBA')
q = im.convert('RGB').quantize(colors=40, method=Image.MEDIANCUT).convert('RGBA')
q.putalpha(im.getchannel('A'))
q = q.crop(q.getbbox())
q.save(dst)
print(dst, q.size, 'f', f, 'bg', bgc.astype(int).tolist())
