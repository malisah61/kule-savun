# Kule Savunma (Godot 4.3 + C#)

Grow Castle tarzı 2D kale savunma oyunu. Kuleni büyüt, kahramanlarını yerleştir, iskelet dalgalarını durdur.

**APK:** https://github.com/malisah61/kule-savun/releases/download/son-surum/KuleSavunma.apk

## Oynanış
- **SAVAŞ!** ile dalga başlar. Düşmanlara dokun: kale okçusu ateş eder.
- Kahramanlar kendiliğinden saldırır; alttaki düğmelerle özel güçlerini kullan:
  - Okçu: **Ok Yağmuru** · Büyücü: **Meteor** (ateş, yanma) · Barbar: **Deprem** (sersemletir) · Şövalye: **Kalkan Darbesi** (geri iter)
- Dalgalar arasında altınla kaleyi, kale okçusunu ve kahramanları geliştir. Yeni kahraman açınca kuleye kat eklenir.
- Her 5. dalgada Dev İskelet gelir.

## Dosyalar
- `Oyun.cs` — oyun akışı, düşmanlar, kahramanlar, mermiler, arayüz
- `Birimler.cs` — sprite yükleyici ve birim sınıfları
- `Efekt.cs` — ateş/büyü/patlama parçacıkları, kodla üretilen sesler
- `Arkaplan.cs`, `Halka.cs` — kodla çizilen arka plan ve halka efekti
- `assets/sprites/` — karakter sprite sayfaları (3D modellerden çekildi)
- `assets/kale/` — kale parçaları, ok, balta (3D'den çizildi)
- `tools/sprite_uretici/` — 3D modelleri 2D sprite'a çeviren Godot betikleri

## Emeği geçenler (CC0)
- Karakterler: KayKit Adventurers ve KayKit Skeletons — Kay Lousberg (kaylousberg.com)
