# Kule Savunma (Godot 4.3 + C#)

Karanlık fantastik, yol + kule savunma oyunu. Mezarlık yolundan gelen 222 farklı düşmanı 32 farklı kuleyle durdur.

**APK:** https://github.com/malisah61/kule-savun/releases/download/son-surum/KuleSavunma.apk

## Oynanış
- Her bölümde uzun, ekranı kat kat dolaşan rastgele bir yol (şerit, sütun ya da dalgalı) ve dolu bir mezarlık haritası.
- Yolun yanındaki boş yerlere dokun → kule kur. Kuleye dokun → menzil, bilgi, bölüm içi yetenek ağacı (Güç / Ustalık), sat.
- **▶ Dalga** ile dalgayı başlat. Yolu bitiren düşman savunmanın canını azaltır (boss 10 can).
- 40+ bölüm (sonsuz); çift bölümlerde boss. Her dalgada düşman sayısı, geliş hızı ve canı artar.
- **Hasar türü × zırh:** 8 hasar türü (Fiziksel, Ateş, Buz, Zehir, Yıldırım, Kutsal, Gölge, Kuşatma) ve 12 zırh tipi. Örn. hayaletlere fiziksel neredeyse işlemez, kutsal 2 kat vurur; kuşatma uçanlara vuramaz.
- **Düşman analizi:** her dalga sonunda savunma incelenir. Rahat geçiyorsan düşmanlar güçlenir; kulelerinin hasar türüne dirençli düşmanlar daha sık gelir. Zorluk asla düşmez.

## İlerleme
- Düşman, dalga ve bölüm **XP** ve kalıcı **para** verir; bölümü kaybetsen de kalır.
- **Oyuncu seviyesi** yükseldikçe yeni kuleler açılır ve 2 yetenek puanı gelir.
- **Kalıcı yetenek ağacı:** 32 kule × 10 küçük yükseltme (+%2 hasar, +%2 menzil, +%2 hız, %2 ucuz, zırh delme…). Para + puanla alınır.

## Dosyalar
- `KuleSavunmasi.cs` — oyun akışı, dalgalar, kuleler, düşmanlar, mermiler, arayüz, analiz
- `TdVeri.cs` — `assets/td/veri.json` yükleyici, hasar çarpanları
- `YetenekAgaci.cs` — bölüm içi Güç/Ustalık ağacı · `Ilerleme.cs` — XP, seviye, para, kalıcı ağaç, kayıt
- `TdHarita.cs` — kodla çizilen mezarlık haritası ve yol · `Efekt.cs` — parçacıklar ve sesler
- `assets/td/` — 32 kule + 222 düşman sprite'ı ve `veri.json`

## Müzik
`assets/ses/menu.ogg` (ürpertici ortam) ve `assets/ses/savas.ogg` (gerilim) `araclar/besteci.py` ile kodla bestelendi; telifsiz. Oyunda ♪ düğmesiyle açılıp kapanır.

## Görseller
Sprite'lar `sanat-uretimi` dalındaki iş akışıyla GitHub Actions'ta üretildi: Stable Diffusion XL + LCM LoRA + pixel-art-xl LoRA (CPU), ardından `araclar/temizle3.py` ile arka plan temizleme. Tasarım verisi: `tasarim/veri.py`.
