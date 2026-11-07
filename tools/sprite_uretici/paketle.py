import json, glob, os
from PIL import Image
KUC = 0.5          # 320 -> 160
AYAK = (160, 296.5)  # y=0 dünya noktasının piksel yeri (320'lik karede)
meta_all = json.load(open('cikti/meta_hepsi.json'))
out = '/home/claude/kale-savunma/assets/sprites'
os.makedirs(out, exist_ok=True)
toplam = {}
for ad, animler in meta_all.items():
    kareler = {}
    bbox = None
    for an, b in animler.items():
        lst = []
        for i in range(b['kare']):
            im = Image.open(f'cikti/{ad}/{an}_{i:02d}.png').convert('RGBA')
            lst.append(im)
            bb = im.getbbox()
            if bb:
                bbox = bb if bbox is None else (min(bbox[0],bb[0]),min(bbox[1],bb[1]),max(bbox[2],bb[2]),max(bbox[3],bb[3]))
        kareler[an] = lst
    x0,y0,x1,y1 = bbox; x0-=2; y0-=2; x1+=2; y1+=2
    w = int((x1-x0)*KUC+0.999); h = int((y1-y0)*KUC+0.999)
    sut = max(len(v) for v in kareler.values())
    sheet = Image.new('RGBA', (w*sut, h*len(kareler)), (0,0,0,0))
    satirlar = {}
    for r,(an,lst) in enumerate(kareler.items()):
        for c,im in enumerate(lst):
            k = im.crop((x0,y0,x0+int(w/KUC),y0+int(h/KUC))).resize((w,h), Image.LANCZOS)
            sheet.alpha_composite(k, (c*w, r*h))
        satirlar[an] = {"satir": r, "kare": len(lst), "sure": round(animler[an]['sure'],3)}
    sheet.save(f'{out}/{ad}.png', optimize=True)
    toplam[ad] = {"w": w, "h": h, "ayak_x": round((AYAK[0]-x0)*KUC,1), "ayak_y": round((AYAK[1]-y0)*KUC,1), "anim": satirlar}
    print(ad, sheet.size, os.path.getsize(f'{out}/{ad}.png')//1024, 'KB')
json.dump(toplam, open(f'{out}/sprites.json','w'), indent=1, ensure_ascii=False)
