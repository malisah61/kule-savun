"""Yürüme karesi üretimi: SD1.5 + ControlNet OpenPose (img2img) + EbSynth stil kilidi.

Her düşman için 4 karelik ön görünüş yürüme döngüsü:
  0 sağ diz kalkık, 1 basış, 2 sol diz kalkık, 3 basış (ön görünüş).
Üç çeşit çıkarılır ki karşılaştırılabilsin:
  A = sadece SD+ControlNet
  B = SD+ControlNet, ardından EbSynth ile orijinal resmin pikselleri üzerine kilitlenir
  C = sadece EbSynth (iskelet farkıyla orijinali büker, SD yok)
Kullanım: python araclar/yurume.py d_curuk_zombi d_iskelet_okcu ...
Çıktı: anim/<cesit>/<ad>.png (4 kare yan yana), anim/<cesit>/<ad>.gif, anim/ara/...
"""
import json, math, os, subprocess, sys, time
import numpy as np
from PIL import Image
from scipy import ndimage

import cv2

S = 512
EBSYNTH = os.environ.get("EBSYNTH", "ebsynth/bin/ebsynth")
adlar = sys.argv[1:]
promptlar = dict(l.strip().split("|", 1) for l in open("tasarim/promptlar.txt", encoding="utf-8") if "|" in l)
boylar = json.load(open("tasarim/boylar.json"))
for d in ["anim/A", "anim/B", "anim/C", "anim/ara"]:
    os.makedirs(d, exist_ok=True)


# ------------------------------------------------------------------ maske
def maske(img):
    a = np.asarray(img.convert("RGB")).astype(np.int32)
    kenar = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
    bgc = np.median(kenar, axis=0)
    aday = np.abs(a - bgc).max(2) < 34
    lab, n = ndimage.label(aday)
    kenar_et = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    m = ~np.isin(lab, list(kenar_et))
    m = ndimage.binary_opening(m, iterations=1)
    lab, n = ndimage.label(m)
    if n == 0:
        return m
    boy = ndimage.sum(m, lab, range(1, n + 1))
    en = int(np.argmax(boy)) + 1
    ys, xs = np.where(lab == en)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    tut = np.zeros_like(m)
    for i in range(1, n + 1):
        if boy[i - 1] < boy[en - 1] * 0.02:
            continue
        yy, xx = np.where(lab == i)
        if xx.min() >= x0 - 10 and xx.max() <= x1 + 10 and yy.min() >= y0 - 10 and yy.max() <= y1 + 10:
            tut |= lab == i
    return ndimage.binary_fill_holes(tut) & m | tut


# ------------------------------------------------------------------ openpose çizimi
LIMB = [[2, 3], [2, 6], [3, 4], [4, 5], [6, 7], [7, 8], [2, 9], [9, 10], [10, 11], [2, 12], [12, 13], [13, 14],
        [2, 1], [1, 15], [15, 17], [1, 16], [16, 18]]
RENK = [[255, 0, 0], [255, 85, 0], [255, 170, 0], [255, 255, 0], [170, 255, 0], [85, 255, 0], [0, 255, 0],
        [0, 255, 85], [0, 255, 170], [0, 255, 255], [0, 170, 255], [0, 85, 255], [0, 0, 255], [85, 0, 255],
        [170, 0, 255], [255, 0, 255], [255, 0, 170], [255, 0, 85]]


def poz_ciz(k):
    tuval = np.zeros((S, S, 3), np.uint8)
    for i in range(18):
        cv2.circle(tuval, (int(k[i][0]), int(k[i][1])), 4, RENK[i], thickness=-1)
    for i, (a, b) in enumerate(LIMB):
        p, q = k[a - 1], k[b - 1]
        ust = tuval.copy()
        mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
        uz = math.hypot(p[0] - q[0], p[1] - q[1])
        aci = math.degrees(math.atan2(p[1] - q[1], p[0] - q[0]))
        poly = cv2.ellipse2Poly((int(mx), int(my)), (int(uz / 2), 4), int(aci), 0, 360, 1)
        cv2.fillConvexPoly(ust, poly, RENK[i])
        tuval = cv2.addWeighted(tuval, 0.4, ust, 0.6, 0)
    return Image.fromarray(tuval)


def poz(kutu, yon, kare, cx=None):
    """Ön görünüş yürüme iskeleti (sprite'lar öne bakıyor).
    kare: -1 duruş, 0 sağ diz kalkık, 1 basış, 2 sol diz kalkık, 3 basış.
    Openpose'ta kişinin sağı resmin solunda."""
    x0, y0, x1, y1 = kutu
    h = y1 - y0
    cx = (x0 + x1) / 2 if cx is None else cx
    kalca = y0 + 0.56 * h
    L = 0.42 * h
    hw, sw = 0.065 * h, 0.11 * h

    def bacak(hx, kalk, yan):
        if not kalk:
            return (hx, kalca), (hx, kalca + L / 2), (hx, kalca + L)
        # diz öne gelir: ön görünüşte uyluk kısalır, diz biraz dışa, ayak 0.11h yerden kalkar
        return (hx, kalca), (hx + yan * 0.02 * h, kalca + 0.17 * h), (hx + yan * 0.005 * h, kalca + 0.31 * h)

    hR, dR, bR = bacak(cx - hw, kare == 0, -1)
    hL, dL, bL = bacak(cx + hw, kare == 2, 1)
    boyun = (cx, y0 + 0.26 * h)
    burun = (cx, y0 + 0.14 * h)
    gozR, gozL = (cx - 0.03 * h, y0 + 0.12 * h), (cx + 0.03 * h, y0 + 0.12 * h)
    kulR, kulL = (cx - 0.065 * h, y0 + 0.13 * h), (cx + 0.065 * h, y0 + 0.13 * h)

    def kol(ox, yan, one):
        om = (ox, y0 + 0.29 * h)
        if one:   # kalkan bacağın karşı kolu öne sallanır: kısalır, el yükselir
            return om, (ox + yan * 0.015 * h, om[1] + 0.11 * h), (ox - yan * 0.01 * h, om[1] + 0.19 * h)
        return om, (ox + yan * 0.025 * h, om[1] + 0.13 * h), (ox + yan * 0.025 * h, om[1] + 0.25 * h)

    omR, eR, wR = kol(cx - sw, -1, kare == 2)
    omL, eL, wL = kol(cx + sw, 1, kare == 0)
    return [burun, boyun, omR, eR, wR, omL, eL, wL, hR, dR, bR, hL, dL, bL, gozR, gozL, kulR, kulL]


# ------------------------------------------------------------------ küçültme + palet kilidi
def paket(kareler, ad, cesit):
    """Ortak kutu ve ortak ölçekle küçültür (kare kare boy oynamasın), paleti orijinale kilitler."""
    hedef = boylar.get(ad, 84)
    mlar = [maske(k) for k in kareler]
    ys = np.concatenate([np.where(m)[0] for m in mlar]); xs = np.concatenate([np.where(m)[1] for m in mlar])
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    olc = hedef / (y1 - y0)
    W = max(1, round((x1 - x0) * olc)); H = hedef
    pal_im = Image.open(f"sprite/{ad}.png").convert("RGBA")
    pa = np.asarray(pal_im).reshape(-1, 4)
    pal = np.unique(pa[pa[:, 3] > 128][:, :3], axis=0).astype(np.int32)
    cikis = []
    for k, m in zip(kareler, mlar):
        rgba = np.dstack([np.asarray(k.convert("RGB")), (m * 255).astype(np.uint8)])
        im = Image.fromarray(rgba, "RGBA").crop((x0, y0, x1, y1)).resize((W, H), Image.BOX)
        a = np.asarray(im).astype(np.int32)
        alfa = a[:, :, 3] > 140
        rgb = a[:, :, :3]
        if len(pal):
            d = ((rgb[:, :, None, :] - pal[None, None]) ** 2).sum(3)
            rgb = pal[d.argmin(2)]
        o = np.zeros((H, W, 4), np.uint8); o[:, :, :3] = rgb; o[:, :, 3] = alfa * 255
        cikis.append(Image.fromarray(o, "RGBA"))
    serit = Image.new("RGBA", (W * len(cikis), H))
    for i, c in enumerate(cikis):
        serit.paste(c, (i * W, 0))
    serit.save(f"anim/{cesit}/{ad}.png")
    gif = []
    for c in cikis:
        z = Image.new("RGBA", (W * 4 + 40, H * 4 + 20), (28, 24, 30, 255))
        z.alpha_composite(c.resize((W * 4, H * 4), Image.NEAREST), (20, 10))
        gif.append(z.convert("RGB"))
    gif[0].save(f"anim/{cesit}/{ad}.gif", save_all=True, append_images=gif[1:], duration=150, loop=0)


def ebsynth(stil, rehberler, cikis):
    kom = [EBSYNTH, "-style", stil]
    for kaynak, hedef, w in rehberler:
        kom += ["-guide", kaynak, hedef, "-weight", str(w)]
    kom += ["-output", cikis, "-patchsize", "5", "-uniformity", "3500"]
    r = subprocess.run(kom, capture_output=True, text=True)
    if r.returncode != 0:
        print("ebsynth hata:", r.stderr[-300:], flush=True)


# ------------------------------------------------------------------ ana akış
import torch
from diffusers import ControlNetModel, LCMScheduler, StableDiffusionControlNetImg2ImgPipeline

torch.set_num_threads(os.cpu_count())
t0 = time.time()
cn = ControlNetModel.from_pretrained("lllyasviel/control_v11p_sd15_openpose", torch_dtype=torch.float32)
pipe = StableDiffusionControlNetImg2ImgPipeline.from_pretrained(
    "stable-diffusion-v1-5/stable-diffusion-v1-5", controlnet=cn, torch_dtype=torch.float32,
    safety_checker=None, requires_safety_checker=False)
pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
pipe.load_lora_weights("latent-consistency/lcm-lora-sdv1-5")
print(f"model hazir {time.time()-t0:.0f}s", flush=True)
neg = "blurry, 3d render, realistic photo, multiple characters, text, watermark, cropped, extra limbs"

for ad in adlar:
    t = time.time()
    kaynak = Image.open(f"ham_jpg/{ad}.jpg").convert("RGB")
    # kare tuvale ortala
    kw, kh = kaynak.size
    tuval = Image.new("RGB", (max(kw, kh),) * 2, kaynak.getpixel((2, 2)))
    tuval.paste(kaynak, ((max(kw, kh) - kw) // 2, (max(kw, kh) - kh) // 2))
    orj = tuval.resize((S, S), Image.LANCZOS)
    m = maske(orj)
    ys, xs = np.where(m)
    kutu = (xs.min(), ys.min(), xs.max(), ys.max())
    # yüz yönü: üst %30'un kütle merkezi tüm gövdeye göre hangi yanda
    ust = ys < kutu[1] + 0.3 * (kutu[3] - kutu[1])
    yon = 1 if xs[ust].mean() >= xs.mean() else -1
    alt = ys > kutu[1] + 0.65 * (kutu[3] - kutu[1])
    gcx = float(np.median(xs[alt]))
    orj.save(f"anim/ara/{ad}_orj.png")
    poz_ciz(poz(kutu, yon, -1, gcx)).save(f"anim/ara/{ad}_poz_n.png")
    prompt = promptlar.get(ad, "pixel art monster, full body, side view, simple plain white background")
    prompt = prompt.replace("side view", "front view, walking, one knee raised mid step")
    A, B, C = [], [], []
    for k in range(4):
        pz = poz_ciz(poz(kutu, yon, k, gcx))
        pz.save(f"anim/ara/{ad}_poz{k}.png")
        img = pipe(prompt=prompt, negative_prompt=neg, image=orj, control_image=pz, strength=0.62,
                   num_inference_steps=8, guidance_scale=1.5, controlnet_conditioning_scale=1.1,
                   generator=torch.Generator().manual_seed(777)).images[0]
        img.save(f"anim/ara/{ad}_sd{k}.png")
        A.append(img)
        ebsynth(f"anim/ara/{ad}_orj.png", [(f"anim/ara/{ad}_orj.png", f"anim/ara/{ad}_sd{k}.png", 1.5),
                                          (f"anim/ara/{ad}_poz_n.png", f"anim/ara/{ad}_poz{k}.png", 1.0)],
                f"anim/ara/{ad}_eb{k}.png")
        ebsynth(f"anim/ara/{ad}_orj.png", [(f"anim/ara/{ad}_poz_n.png", f"anim/ara/{ad}_poz{k}.png", 2.0),
                                          (f"anim/ara/{ad}_orj.png", f"anim/ara/{ad}_orj.png", 0.4)],
                f"anim/ara/{ad}_ebc{k}.png")
        B.append(Image.open(f"anim/ara/{ad}_eb{k}.png") if os.path.exists(f"anim/ara/{ad}_eb{k}.png") else img)
        C.append(Image.open(f"anim/ara/{ad}_ebc{k}.png") if os.path.exists(f"anim/ara/{ad}_ebc{k}.png") else orj)
        print(f"  {ad} kare {k} {time.time()-t:.0f}s", flush=True)
    for ces, kl in (("A", A), ("B", B), ("C", C)):
        try:
            paket(kl, ad, ces)
        except Exception as e:
            print("paket hata", ad, ces, e, flush=True)
    print(f"OK {ad} yon={yon} {time.time()-t:.0f}s", flush=True)
