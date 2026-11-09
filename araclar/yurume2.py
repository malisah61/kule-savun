"""Yürüme kareleri, 2. yöntem: iskelete göre gerçek bükme (MLS) + EbSynth onarımı.

1. Orijinal resmin üstüne ön görünüş iskeleti oturtulur (duruş).
2. Her kare için iskelet hareket ettirilir (bir ayak öne/aşağı basar, öbürü kalkar,
   kollar ters sallanır) ve resim Moving Least Squares ile kemiklere göre bükülür.
   Hareket buradan gelir, garanti görünür.
3. EbSynth bükülmüş kareyi orijinalin kendi parçalarıyla yeniden örer (gerilen
   pikselleri onarır), sonra paleti orijinal sprite'a kilitlenir.
Çıktı: anim2/W (sadece bükme), anim2/E (bükme + EbSynth) — <ad>.png şerit + .gif
Kullanım: python araclar/yurume2.py [d_ad ...]  (ad verilmezse: insan biçimli, karesi olmayan tüm düşmanlar)
"""
import json, os, re, subprocess, sys, time
import numpy as np
from PIL import Image
from scipy import ndimage

S = 512
EBSYNTH = os.environ.get("EBSYNTH", "ebsynth/bin/ebsynth")
boylar = json.load(open("tasarim/boylar.json"))
for d in ["anim2/W", "anim2/E", "anim2/ara"]:
    os.makedirs(d, exist_ok=True)


def arka_renk(a):
    kenar = np.concatenate([a[:6].reshape(-1, 3), a[-6:].reshape(-1, 3), a[:, :6].reshape(-1, 3), a[:, -6:].reshape(-1, 3)])
    return np.median(kenar, axis=0)


def maske(img):
    """Arka plan: kenara değen + içerideki büyük arka plan renkli boşluklar (bacak arası)."""
    a = np.asarray(img.convert("RGB")).astype(np.int32)
    bgc = arka_renk(a)
    aday = np.abs(a - bgc).max(2) < 34
    lab, n = ndimage.label(aday)
    kenar_et = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(kenar_et))
    boy = ndimage.sum(aday, lab, range(1, n + 1))
    for i in range(1, n + 1):
        if i not in kenar_et and boy[i - 1] > 150:
            bg |= lab == i
    m = ~bg
    # yerdeki gölge: alt banttaki renksiz gri pikseller (oyun kendi gölgesini çizer)
    ys_, xs_ = np.where(m)
    if len(ys_):
        alt0 = ys_.max() - 0.09 * (ys_.max() - ys_.min())
        doy = a.max(2) - a.min(2)
        golge = (doy < 14) & (np.arange(a.shape[0])[:, None] > alt0)
        m &= ~golge
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
    return tut


YASAK = (r"\b(flying|bats?|wings?|winged|ghosts?|ghostly|spirits?|wraiths?|phantoms?|slimes?|ooze|blob|spiders?|"
         r"scorpions?|worms?|serpents?|snakes?|beetles?|insects?|crows?|ravens?|vultures?|owls?|harpy|harpies|griffin|"
         r"gryphon|birds?|eagles?|swarm|hounds?|wolf|wolves|werewolf|rats?|eyeballs?|tentacles?|jellyfish|centipedes?|"
         r"horses?|rider|mounted|dragons?|wyverns?|drakes?|hydra|crawler|larva|maggots?|floating|hovering|clouds?|storm|"
         r"orbs?|wisps?|flies|moths?|wasps?|leech|toads?|frogs?|crabs?|boar|bears?|cats?|dogs?|spectres?|specters?|"
         r"banshees?|shades?|mammoth|deer|stag|elk|fish|squid|octopus|cerberus|chimera|mantis)\b")


def kare_tuval(ad):
    kaynak = Image.open(f"ham_jpg/{ad}.jpg").convert("RGB")
    kw, kh = kaynak.size
    tuval = Image.new("RGB", (max(kw, kh),) * 2, kaynak.getpixel((2, 2)))
    tuval.paste(kaynak, ((max(kw, kh) - kw) // 2, (max(kw, kh) - kh) // 2))
    return tuval.resize((S, S), Image.LANCZOS)


def insansi(ad, prompt):
    """İki bacak üstünde duran, uçmayan/süzülmeyen karakter mi? (kelime + alttaki bacak arası boşluk)"""
    if re.search(YASAK, prompt.lower()):
        return False
    m = maske(kare_tuval(ad))
    ys, xs = np.where(m)
    if len(ys) < 500:
        return False
    h, w = ys.max() - ys.min(), xs.max() - xs.min()
    if h < w:
        return False
    iyi = n = 0
    for y in range(int(ys.max() - 0.18 * h), int(ys.max() - 0.03 * h)):
        x = np.where(m[y])[0]; n += 1
        if len(x) >= 2 and (np.diff(x) > max(3, 0.03 * w)).any():
            iyi += 1
    return iyi >= n * 0.35


# ------------------------------------------------------------------ iskelet
def iskelet(kutu, cx, kare):
    """Ön görünüş. Döndürür: {ad: (x,y)}. kare -1/1/3 duruş; 0 sağ ayak basar sol kalkar; 2 tersi."""
    x0, y0, x1, y1 = kutu
    h = y1 - y0
    kalca = y0 + 0.56 * h
    L = 0.42 * h
    hw, sw = 0.07 * h, 0.12 * h
    j = {"bas": (cx, y0 + 0.08 * h), "cene": (cx, y0 + 0.2 * h), "boyun": (cx, y0 + 0.26 * h),
         "bel": (cx, y0 + 0.45 * h), "kalca": (cx, kalca)}
    for yan, ad in ((-1, "R"), (1, "L")):
        hx = cx + yan * hw
        j["h" + ad] = (hx, kalca)
        j["d" + ad] = (hx, kalca + L / 2)
        j["a" + ad] = (hx, kalca + L)
        om = (cx + yan * sw, y0 + 0.29 * h)
        j["o" + ad] = om
        j["e" + ad] = (om[0] + yan * 0.025 * h, om[1] + 0.13 * h)
        j["w" + ad] = (om[0] + yan * 0.025 * h, om[1] + 0.25 * h)
    if kare in (0, 2):
        bas_, kalk = ("R", "L") if kare == 0 else ("L", "R")
        yk = 1 if kalk == "L" else -1
        # basan ayak öne (kameraya) gelir: biraz aşağı
        hx = j["h" + bas_][0]
        j["d" + bas_] = (hx, kalca + L / 2 + 0.012 * h)
        j["a" + bas_] = (hx, kalca + L + 0.025 * h)
        # kalkan bacak: diz bükülür, ayak yerden ~%12 kalkar
        hx = j["h" + kalk][0]
        j["d" + kalk] = (hx + yk * 0.015 * h, kalca + 0.165 * h)
        j["a" + kalk] = (hx + yk * 0.005 * h, kalca + 0.30 * h)
        # kalça kalkan tarafa hafif yükselir
        j["h" + kalk] = (hx, kalca - 0.012 * h)
        # kollar ters: basan bacak tarafındaki kol geri (el yukarı), öbürü öne (el aşağı/içe)
        for ad, s_ in ((bas_, -1), (kalk, 1)):
            om = j["o" + ad]
            yan = -1 if ad == "R" else 1
            j["e" + ad] = (om[0] + yan * 0.02 * h, om[1] + 0.13 * h + s_ * 0.012 * h)
            j["w" + ad] = (om[0] + yan * (0.025 - s_ * 0.02) * h, om[1] + 0.25 * h + s_ * 0.035 * h)
        # gövde basış anında 1-2 px çöker
        for ad in ("bas", "cene", "boyun", "oR", "oL"):
            j[ad] = (j[ad][0], j[ad][1] + 0.008 * h)
    return j


KEMIK = [("bas", "cene"), ("cene", "boyun"), ("boyun", "bel"), ("bel", "kalca"), ("oR", "oL"),
         ("oR", "eR"), ("eR", "wR"), ("oL", "eL"), ("eL", "wL"), ("kalca", "hR"), ("kalca", "hL"),
         ("hR", "dR"), ("dR", "aR"), ("hL", "dL"), ("dL", "aL")]


def kontrol(j):
    pts = []
    for a, b in KEMIK:
        p, q = np.array(j[a]), np.array(j[b])
        for t in np.linspace(0, 1, 6):
            pts.append(p + (q - p) * t)
    # köşeler sabit: arka plan kıpırdamasın
    for c in [(0, 0), (S - 1, 0), (0, S - 1), (S - 1, S - 1), (S / 2, 0), (S / 2, S - 1), (0, S / 2), (S - 1, S / 2)]:
        pts.append(np.array(c, float))
    return np.array(pts)


def mls_geri(P, Q, alfa=1.5):
    """Hedef kontrol noktaları P, kaynak Q. Her çıkış pikseli için kaynaktaki konumu (afin MLS)."""
    yy, xx = np.mgrid[0:S, 0:S]
    V = np.stack([xx.ravel(), yy.ravel()], 1).astype(np.float64)
    out = np.empty_like(V)
    for b in range(0, len(V), 16384):
        v = V[b:b + 16384]
        d2 = ((P[None] - v[:, None]) ** 2).sum(2)
        w = 1.0 / (d2 ** alfa + 1e-6)
        sw = w.sum(1, keepdims=True)
        ps = (w @ P) / sw
        qs = (w @ Q) / sw
        ph = P[None] - ps[:, None]
        qh = Q[None] - qs[:, None]
        A = np.einsum("nk,nki,nkj->nij", w, ph, ph)
        B = np.einsum("nk,nki,nkj->nij", w, ph, qh)
        A[:, 0, 0] += 1e-6; A[:, 1, 1] += 1e-6
        M = np.linalg.solve(A, B)
        out[b:b + 16384] = np.einsum("ni,nij->nj", v - ps, M) + qs
    return out.reshape(S, S, 2)


def bük(rgba, harita):
    import cv2
    mx = harita[..., 0].astype(np.float32); my = harita[..., 1].astype(np.float32)
    rgb = cv2.remap(rgba[..., :3], mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
    al = cv2.remap(rgba[..., 3], mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
    return np.dstack([rgb, al])


def ebsynth(stil, rehberler, cikis):
    kom = [EBSYNTH, "-style", stil]
    for kaynak, hedef, w in rehberler:
        kom += ["-guide", kaynak, hedef, "-weight", str(w)]
    kom += ["-output", cikis, "-patchsize", "5", "-uniformity", "3000"]
    r = subprocess.run(kom, capture_output=True, text=True)
    if r.returncode != 0:
        print("ebsynth hata:", r.stderr[-300:], flush=True)
    return r.returncode == 0


def paket(kareler, ad, klasor):
    """kareler: RGBA 512 dizileri. Ortak kutu + ortak ölçek, palet orijinal sprite'a kilitli."""
    hedef = boylar.get(ad, 84)
    ys = np.concatenate([np.where(k[..., 3] > 127)[0] for k in kareler])
    xs = np.concatenate([np.where(k[..., 3] > 127)[1] for k in kareler])
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    olc = hedef / (y1 - y0)
    W, H = max(1, round((x1 - x0) * olc)), hedef
    pa = np.asarray(Image.open(f"sprite/{ad}.png").convert("RGBA")).reshape(-1, 4)
    pal = np.unique(pa[pa[:, 3] > 128][:, :3], axis=0).astype(np.int32)
    cikis = []
    for k in kareler:
        im = Image.fromarray(k.astype(np.uint8), "RGBA").crop((x0, y0, x1, y1)).resize((W, H), Image.BOX)
        a = np.asarray(im).astype(np.int32)
        rgb = a[..., :3]
        if len(pal):
            rgb = pal[((rgb[:, :, None, :] - pal[None, None]) ** 2).sum(3).argmin(2)]
        o = np.zeros((H, W, 4), np.uint8)
        o[..., :3] = rgb; o[..., 3] = (a[..., 3] > 140) * 255
        cikis.append(Image.fromarray(o, "RGBA"))
    serit = Image.new("RGBA", (W * len(cikis), H))
    for i, c in enumerate(cikis):
        serit.paste(c, (i * W, 0))
    serit.save(f"anim2/{klasor}/{ad}.png")
    gif = []
    for c in cikis:
        z = Image.new("RGBA", (W * 4 + 40, H * 4 + 20), (28, 24, 30, 255))
        z.alpha_composite(c.resize((W * 4, H * 4), Image.NEAREST), (20, 10))
        gif.append(z.convert("RGB"))
    gif[0].save(f"anim2/{klasor}/{ad}.gif", save_all=True, append_images=gif[1:], duration=160, loop=0)


def isle(ad, eb=True):
    t = time.time()
    orj = kare_tuval(ad)
    m = maske(orj)
    a = np.asarray(orj).astype(np.uint8)
    bgc = arka_renk(a.astype(np.int32)).astype(np.uint8)
    rgba = np.dstack([a, (m * 255).astype(np.uint8)])
    ys, xs = np.where(m)
    kutu = (xs.min(), ys.min(), xs.max(), ys.max())
    alt = ys > kutu[1] + 0.65 * (kutu[3] - kutu[1])
    cx = float(np.median(xs[alt]))
    Q = kontrol(iskelet(kutu, cx, -1))
    # EbSynth için arka planı düz renk yap
    duz = np.where(m[..., None], a, bgc).astype(np.uint8)
    Image.fromarray(duz).save(f"anim2/ara/{ad}_orj.png")
    W_, E_ = [], []
    for k in range(4):
        P = kontrol(iskelet(kutu, cx, k))
        b = bük(rgba, mls_geri(P, Q)) if k in (0, 2) else rgba.copy()
        W_.append(b)
        duzb = np.where((b[..., 3] > 127)[..., None], b[..., :3], bgc).astype(np.uint8)
        Image.fromarray(duzb).save(f"anim2/ara/{ad}_w{k}.png")
        if eb and k in (0, 2) and ebsynth(f"anim2/ara/{ad}_orj.png",
                                         [(f"anim2/ara/{ad}_orj.png", f"anim2/ara/{ad}_w{k}.png", 2.0)],
                                         f"anim2/ara/{ad}_e{k}.png"):
            e = np.asarray(Image.open(f"anim2/ara/{ad}_e{k}.png").convert("RGB"))
            E_.append(np.dstack([e, b[..., 3]]))
        else:
            E_.append(b)
    paket(W_, ad, "W")
    paket(E_, ad, "E")
    print(f"OK {ad} {time.time()-t:.0f}s", flush=True)


if __name__ == "__main__":
    eb = os.path.exists(EBSYNTH)
    adlar = sys.argv[1:]
    if not adlar:
        pr = dict(l.strip().split("|", 1) for l in open("tasarim/promptlar.txt", encoding="utf-8") if "|" in l)
        aday = sorted(a for a in pr if a.startswith("d_") and os.path.exists(f"ham_jpg/{a}.jpg") and os.path.exists(f"sprite/{a}.png"))
        yeniden = os.environ.get("YENIDEN") == "1"
        adlar = [a for a in aday if (yeniden or not os.path.exists(f"anim2/E/{a}.png")) and insansi(a, pr[a])]
        print(f"{len(adlar)} düşman karelendirilecek (aday {len(aday)})", flush=True)
    for ad in adlar:
        try:
            isle(ad, eb)
        except Exception as e:
            print("HATA", ad, e, flush=True)
