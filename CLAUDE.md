# Proje notları

- Motor: Godot 4.3 .NET (C#), renderer: GL Compatibility. Oyun 2D, yatay ekran (1280x720, expand).
- Hedef: Android arm64 APK. `main`'e her gönderimde `.github/workflows/android.yml` APK'yı derleyip `son-surum` sürümüne yükler.
- `.github/workflows/onizleme.yml` C# derler, oyunu bulutta açar; `onizleme/derleme.txt`, `onizleme/calisma.txt` ve ekran görüntülerini depoya geri yazar. Kod değişikliğinden sonra bunları kontrol et.
- Kullanıcıyla Türkçe konuş; oyundaki metinler Türkçe.
- Sprite'lar `tools/sprite_uretici/` ile KayKit 3D modellerinden üretilir (yerelde Godot + xvfb gerekir).
