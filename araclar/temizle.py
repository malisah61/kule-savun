import sys, numpy as np
from PIL import Image
from scipy import ndimage
src, dst, target = sys.argv[1], sys.argv[2], int(sys.argv[3])
a = np.asarray(Image.open(src).convert('RGB')).astype(np.int32)
H, W, _ = a.shape
mn, mx = a.min(2), a.max(2)
cand = (mn > 190) & (mx - mn < 45)                 # whitish
lab, _ = ndimage.label(cand)
edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
bg = np.isin(lab, list(edge))
# enclosed white holes bigger than a few blocks
sizes = ndimage.sum(np.ones_like(lab), lab, range(lab.max() + 1))
big = np.where(sizes > 400)[0]; bg |= np.isin(lab, big) & (mn > 225)
bg = ndimage.binary_dilation(bg, iterations=2)     # eat anti-aliased fringe
fg = ~bg
ys, xs = np.where(fg); fh = ys.max() - ys.min()
f = max(3, round(fh / target))
h2, w2 = H // f, W // f
fgb = fg[:h2*f, :w2*f].reshape(h2, f, w2, f)
cb = a[:h2*f, :w2*f].reshape(h2, f, w2, f, 3)
cnt = fgb.sum((1, 3))
col = (cb * fgb[..., None]).sum((1, 3)) / np.maximum(cnt, 1)[..., None]
alpha = (cnt > f * f * 0.5)
alpha = ndimage.binary_opening(alpha, structure=np.ones((2, 2))) | (alpha & ndimage.binary_erosion(alpha))
alpha = alpha | (cnt > f * f * 0.5) & ndimage.binary_dilation(ndimage.binary_opening(alpha, np.ones((2,2))))
out = np.zeros((h2, w2, 4), np.uint8); out[..., :3] = col.clip(0, 255); out[..., 3] = alpha * 255
im = Image.fromarray(out, 'RGBA')
q = im.convert('RGB').quantize(colors=40, method=Image.MEDIANCUT).convert('RGBA')
q.putalpha(im.getchannel('A')); q = q.crop(q.getbbox()); q.save(dst); print(dst, q.size, 'factor', f)
