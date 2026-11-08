"""Kule Savunma için kodla bestelenen korku/gerilim müzikleri (telifsiz).
menu.ogg  : ürpertici ortam — derin uğultu, uzak çan, rüzgâr, ürkütücü yüksek ton
savas.ogg : gerilim — kalp atışı, savaş davulları, yarım ton yaylı ostinato, uyumsuz yükselişler
Her parça döngüye uygun (başı ve sonu kesintisiz birleşir).
"""
import numpy as np, subprocess, sys

SR = 32000
rng = np.random.default_rng(61)

def t_ekseni(sure): return np.arange(int(SR * sure)) / SR

def dongu_gurultu(n, renk=0.995):
    """Döngüsel (başı-sonu eşleşen) filtrelenmiş gürültü: FFT ile."""
    g = rng.standard_normal(n)
    f = np.fft.rfft(g)
    frek = np.fft.rfftfreq(n, 1 / SR)
    return f, frek

def bant_gurultu(n, alt, ust):
    f, frek = dongu_gurultu(n)
    maske = ((frek >= alt) & (frek <= ust)).astype(float)
    y = np.fft.irfft(f * maske, n)
    return y / (np.abs(y).max() + 1e-9)

def alcak_geciren(x, kesim):
    f = np.fft.rfft(x); frek = np.fft.rfftfreq(len(x), 1 / SR)
    f *= 1 / (1 + (frek / kesim) ** 4)
    return np.fft.irfft(f, len(x))

def yanki(x, gecikme=0.31, geri=0.45, adim=6):
    """Döngüsel yankı (sona taşan kısım başa sarılır — dikişsiz)."""
    y = x.copy(); d = int(gecikme * SR)
    for k in range(1, adim + 1):
        y += np.roll(x, d * k) * (geri ** k)
    return y

def zarf(n, atak, bozulma):
    t = np.arange(n) / SR
    return np.minimum(1, t / max(atak, 1e-4)) * np.exp(-t / bozulma)

def yerlestir(hedef, ses, zaman):
    i = int(zaman * SR) % len(hedef)
    son = i + len(ses)
    if son <= len(hedef): hedef[i:son] += ses
    else:
        k = len(hedef) - i
        hedef[i:] += ses[:k]; hedef[:son - len(hedef)] += ses[k:]

def can(frekans, sure, ses=0.5):
    n = int(SR * sure); t = np.arange(n) / SR
    oranlar = [1, 2.0, 2.76, 3.9, 5.4, 6.8]   # çanın uyumsuz kısmi tonları
    y = sum(np.sin(2 * np.pi * frekans * o * t) * np.exp(-t * (0.6 + i * 0.7)) / (i + 1) for i, o in enumerate(oranlar))
    return y * ses * np.minimum(1, t / 0.005)

def davul(sure=0.9, frek=55, ses=0.9):
    n = int(SR * sure); t = np.arange(n) / SR
    perde = frek * (1 + 1.6 * np.exp(-t * 18))
    faz = 2 * np.pi * np.cumsum(perde) / SR
    govde = np.sin(faz) * np.exp(-t * 4.5)
    vurus = rng.standard_normal(n) * np.exp(-t * 60) * 0.25
    return (govde + alcak_geciren(vurus, 900)) * ses

def kalp(ses=0.8):
    a = davul(0.35, 48, ses); b = davul(0.35, 44, ses * 0.75)
    y = np.zeros(int(SR * 0.6)); y[:len(a)] += a; y[int(0.19 * SR):int(0.19 * SR) + len(b)] += b
    return y

def yayli(frek, sure, ses=0.3):
    """Detune'lu testere dişi + alçak geçiren: karanlık yaylı."""
    n = int(SR * sure); t = np.arange(n) / SR
    y = np.zeros(n)
    for d in (-0.35, 0, 0.4):
        f = frek * 2 ** (d / 12)
        faz = (f * t) % 1
        y += 2 * faz - 1
    y = alcak_geciren(y / 3, frek * 4)
    return y * ses * zarf(n, 0.04, sure * 0.9)

def kaydet(x, ad):
    x = x - x.mean()
    x = np.tanh(x / (np.abs(x).max() + 1e-9) * 1.4) * 0.85
    pcm = (x * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "3", ad], input=pcm, check=True)
    print(ad, len(x) / SR, "sn")

# ---------------------------------------------------------------- MENÜ (ortam)
def menu():
    sure = 64.0; n = int(SR * sure); t = t_ekseni(sure)
    y = np.zeros(n)
    # derin uğultu: A1 + bozuk beşli, yavaş dalgalanma (döngüye tam oturan LFO)
    for f, s in ((55, 0.35), (55 * 1.498, 0.18), (27.5, 0.25), (58.3, 0.12)):
        lfo = 0.7 + 0.3 * np.sin(2 * np.pi * t * (2 / sure) + f)
        y += np.sin(2 * np.pi * f * t + 0.3 * np.sin(2 * np.pi * t * (1 / sure))) * s * lfo
    # rüzgâr
    ruzgar = bant_gurultu(n, 200, 900) * (0.5 + 0.5 * np.sin(2 * np.pi * t * (3 / sure)) ** 2)
    y += ruzgar * 0.12
    # ürkütücü yüksek ton (cam sesi), yavaş kayan
    for k in range(4):
        bas = k * 16 + 4
        n2 = int(SR * 9); tt = np.arange(n2) / SR
        f0 = [1046, 987, 1108, 932][k]
        ton = np.sin(2 * np.pi * np.cumsum(f0 * (1 + 0.03 * np.sin(tt * 0.7))) / SR) * np.sin(np.pi * tt / 9) ** 2 * 0.05
        yerlestir(y, ton, bas)
    # uzak çanlar
    for k, (zaman, f) in enumerate(((2, 110), (18, 98), (34, 110), (50, 82.4))):
        yerlestir(y, can(f, 7, 0.35), zaman)
    return yanki(y, 0.43, 0.4, 5)

# ---------------------------------------------------------------- SAVAŞ (gerilim)
def savas():
    bpm = 96; vurus = 60 / bpm; olcu = vurus * 4
    olcu_sayisi = 24; sure = olcu * olcu_sayisi
    n = int(SR * sure); t = t_ekseni(sure)
    y = np.zeros(n)
    # alt uğultu
    y += np.sin(2 * np.pi * 41.2 * t) * 0.25 + np.sin(2 * np.pi * 43.65 * t) * 0.12
    # kalp atışı her ölçünün başı ve 3. vuruşu
    for o in range(olcu_sayisi):
        yerlestir(y, kalp(0.55), o * olcu)
        yerlestir(y, kalp(0.45), o * olcu + 2 * vurus)
    # savaş davulları (8. ölçüden sonra yoğunlaşır)
    desen = [0, 1.5, 2, 3.5]
    for o in range(olcu_sayisi):
        if o % 8 < 2: continue
        for d in desen:
            yerlestir(y, davul(0.8, 62 if d in (0, 2) else 75, 0.55), o * olcu + d * vurus)
        if o % 4 == 3:
            for k in range(4): yerlestir(y, davul(0.4, 90, 0.35), o * olcu + (3 + k * 0.25) * vurus)
    # yaylı ostinato: E2-F2 yarım ton gerilimi, sekizlik
    notalar = [82.4, 87.3, 82.4, 82.4, 87.3, 82.4, 77.8, 82.4]
    for o in range(olcu_sayisi):
        if o % 8 >= 6: continue
        for k, f in enumerate(notalar):
            yerlestir(y, yayli(f * 2, vurus * 0.55, 0.16), o * olcu + k * vurus / 2)
    # uyumsuz küme yükselişleri (her 8 ölçüde)
    for o in range(5, olcu_sayisi, 8):
        n2 = int(SR * olcu * 2); tt = np.arange(n2) / SR
        kume = sum(np.sin(2 * np.pi * f * tt) for f in (164.8, 174.6, 233.1, 246.9)) / 4
        kume *= (tt / tt[-1]) ** 2 * 0.22
        yerlestir(y, kume, o * olcu)
    # yüksek gerilim teli
    y += np.sin(2 * np.pi * 659.3 * t) * 0.025 * (0.5 + 0.5 * np.sin(2 * np.pi * t / (olcu * 4)))
    return yanki(y, vurus * 0.75, 0.3, 4)

hedef = sys.argv[1] if len(sys.argv) > 1 else "."
kaydet(menu(), f"{hedef}/menu.ogg")
kaydet(savas(), f"{hedef}/savas.ogg")
