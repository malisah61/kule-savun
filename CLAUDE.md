# Proje notları

- Motor: Godot 4.3 .NET (C#), renderer: GL Compatibility. Oyun 2D, yatay ekran (1280x720, expand).
- Hedef: Android arm64 APK. `main`'e her gönderimde `.github/workflows/android.yml` APK'yı derleyip `son-surum` sürümüne yükler.
- `.github/workflows/onizleme.yml` C# derler, oyunu bulutta açar; `onizleme/derleme.txt`, `onizleme/calisma.txt` ve ekran görüntülerini depoya geri yazar. Kod değişikliğinden sonra bunları kontrol et.
- Kullanıcıyla Türkçe konuş; oyundaki metinler Türkçe.
- Oyun yol + kule savunması (KuleSavunmasi.cs). Eski Grow Castle kodu kaldırıldı; `assets/sprites`, `assets/kale`, `tools/` eski sürümden kalma, kullanılmıyor.
- Kule/düşman verisi `assets/td/veri.json` (kaynağı `sanat-uretimi` dalında `tasarim/veri.py`); sprite'lar `assets/td/kule_<id>.png`, `d_<id>.png`.
- Yeni görsel: `sanat-uretimi` dalında `tasarim/promptlar.txt`'e satır ekle → `sanat.yml` bulutta üretir (CPU, görsel başına ~6-12 dk), `sprite/` klasörüne yazar.
- Kayıt: `user://td_kayit.json` (enUst, xp, para, harcananPuan, kademe).
- Müzik: `assets/ses/` menu, savas1-3, boss (CC0, OpenGameArt; kaynaklar `assets/ses/KREDILER.md`, indirme `.github/workflows/muzik.yml`). Her bölüm sırayla bir savaş parçası çalar, boss çıkınca boss.ogg. `araclar/besteci.py` sentez parçayı üretir (savas3).
- Haritalar bölüm numarasından sabit tohumla üretilir (`BolumTohum`): aynı bölüm hep aynı yol.
- Düşman hareketi: `assets/td/yuru_<id>.png` (4 karelik yürüme şeridi, sanat dalında `araclar/yurume2.py` + `yurume.yml` üretir) ve `assets/td/hareket.json` (tip: kare, adim, kanat, suzul, yuzen, surun, zipla, bocek, dortayak, cuppe; `araclar/hareket.py`). Oyunda `HareketUygula`.
