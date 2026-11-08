import sys
from PIL import Image, ImageOps
from collections import deque
src, dst, target = sys.argv[1], sys.argv[2], int(sys.argv[3])
im = Image.open(src).convert('RGB')
# find character height in full res (non-white pixels)
g = ImageOps.invert(im.convert('L')).point(lambda v: 255 if v > 45 else 0)
bb = g.getbbox(); fh = bb[3] - bb[1]
factor = max(3, round(fh / target))
small = im.resize((im.width // factor, im.height // factor), Image.BOX)
sw, sh = small.size; px = small.load()
def bgish(c, y): return (min(c) > 215 and max(c) - min(c) < 30) or (y > 0.7 * sh and min(c) > 140 and max(c) - min(c) < 22)
mask = [[False] * sw for _ in range(sh)]
q = deque([(x, y) for x in range(sw) for y in (0, sh - 1)] + [(x, y) for y in range(sh) for x in (0, sw - 1)])
while q:
    x, y = q.popleft()
    if not (0 <= x < sw and 0 <= y < sh) or mask[y][x] or not bgish(px[x, y], y): continue
    mask[y][x] = True; q.extend([(x+1, y), (x-1, y), (x, y+1), (x, y-1)])
# enclosed holes of background colour (e.g. gap between arm and body)
seen = [[False] * sw for _ in range(sh)]
for y0 in range(sh):
    for x0 in range(sw):
        c = px[x0, y0]
        if mask[y0][x0] or seen[y0][x0] or not (min(c) > 222 and max(c) - min(c) < 20): continue
        comp, q2 = [], deque([(x0, y0)])
        while q2:
            x, y = q2.popleft()
            if not (0 <= x < sw and 0 <= y < sh) or seen[y][x] or mask[y][x]: continue
            c = px[x, y]
            if not (min(c) > 205 and max(c) - min(c) < 25): continue
            seen[y][x] = True; comp.append((x, y)); q2.extend([(x+1, y), (x-1, y), (x, y+1), (x, y-1)])
        if len(comp) >= 3:
            for x, y in comp: mask[y][x] = True
# peel light fringe next to background (2 passes)
for _ in range(2):
    add = []
    for y in range(sh):
        for x in range(sw):
            if mask[y][x]: continue
            c = px[x, y]
            if min(c) > 165 and any(0 <= x+dx < sw and 0 <= y+dy < sh and mask[y+dy][x+dx] for dx, dy in ((1,0),(-1,0),(0,1),(0,-1))):
                add.append((x, y))
    for x, y in add: mask[y][x] = True
out = small.quantize(colors=32, method=Image.FASTOCTREE).convert('RGBA'); op = out.load()
for y in range(sh):
    for x in range(sw):
        if mask[y][x]: op[x, y] = (0, 0, 0, 0)
out = out.crop(out.getbbox()); out.save(dst); print(dst, out.size, 'factor', factor)
