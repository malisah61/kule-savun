# Gece Günlüğü

## 2026-11-09
**Eklenen düşmanlar (10):**
| Ad | Zırh | Seviye | Özel |
|---|---|---|---|
| Tabut Taşıyıcı | Ölümsüz | 8 | kalkan |
| Sırtlan Adam | Canavar | 14 | hızlı |
| Dev Sümüklü | Böcek | 21 | yavaş |
| Kan Cerrahı | Vampir | 27 | iyileşir |
| Kemik Vatozu | Uçan | 36 | uçan |
| Vitray Bekçi | Taş | 47 | kalkan |
| Sayfa Hayaleti | Hayalet | 47 | hızlı |
| İblis Davulcu | Şeytan | 47 | hızlı |
| Ayaz Cellatı | Buz | 48 | – |
| Çamur Ahtapotu | Bataklık | 48 | iyileşir |

**Boss:** Mezar Fili (Ölümsüz, bölüm 46)

**Kule:** Çürük Mantar — Zehir, zehir, menzil 165, atış 0.7/sn, hasar 22, maliyet 340, oyuncu Sv 34

**Yürüme karesi alanlar (5):** Ayaz Cellatı, İblis Davulcu, Kan Cerrahı, Sırtlan Adam, Mezar Fili. Hepsi kontrol edildi, kopuk/bozuk yok; silinen şerit yok. (Tabut Taşıyıcı ve Vitray Bekçi insan biçimli testine takılmadı → shader yürüyüşü.) Toplam 97 yürüme şeridi.

**Kalite kontrol:**
- Sırtlan Adam: ilk görsel ayıya benziyordu → prompt netleştirilip yeniden üretildi (sırtlan/çakal başlı); koltuk altındaki beyaz arka plan artıkları silindi.
- Demir Salyangoz: ilk görsel yeşil tırtıl/sümüklü böcek, ikinci gri biçimsiz taş yığını → ilk görsel kullanıldı, ad "Dev Sümüklü" yapıldı (id dev_sumuklu), prompt görsele uyduruldu.
- Mezar Fili: arka bacaklar arasındaki açık renkli arka plan artığı silindi. Çamur Ahtapotu: kenarlardaki beyaz benekler silindi.
- Çıkarılan yok. Not: yurume.yml'in workflow_run tetiği varsayılan dal dışında çalışmadığı için elle başlatıldı; ikinci sanat çalıştırmasının "topla" adımı çizilecek görsel olmadığından ('ham' klasörü yok) hata verdi, zararsız.
- Toplam: 33 kule, 233 düşman.

## 2026-11-08 (ikinci parti)
**Eklenen düşmanlar (10):**
| Ad | Zırh | Seviye | Özel |
|---|---|---|---|
| Kafes Taşıyıcı | Ölümsüz | 7 | – |
| Böcek Rahip | Böcek | 12 | – |
| Kök Dişli | Bataklık | 19 | yavaş |
| Ayna Şövalye | Zırhlı | 28 | kalkan |
| Kan Kuğusu | Uçan | 35 | uçan |
| Saat Hortlağı | Hayalet | 45 | hızlı |
| Tuz Golemi | Taş | 45 | yavaş |
| Demirci İblis | Şeytan | 45 | kalkan |
| Buzul Mamutu | Buz | 46 | yavaş |
| Gölge Terzi | Kültist | 46 | hızlı |

**Boss:** Yüz Maske Kahini (Kültist, bölüm 44)

**Kule:** Ruh Zinciri — Gölge, zincir, menzil 160, atış 0.8/sn, hasar 32, maliyet 320, oyuncu Sv 31

**Kalite kontrol:**
- Kafes Taşıyıcı: ilk görsel karakter değil, içi beyaz boşluklu dolap/kafesti → prompt netleştirildi, yeniden üretildi (iskelet gardiyan).
- Böcek Rahip: ilk görsel beyaz cüppeli sıradan rahipti, koltuk altında delikler → yeniden üretildi (koyu, böcekli cüppe).
- Gölge Terzi: ilk görsel terziye benzemiyordu (sopalı ninja) → yeniden üretildi.
- Yüz Maske Kahini: ilk görselde omuzlarda iki ayrı yeşil yaratık vardı → yeniden üretildi (tek, mor cüppeli kâhin).
- Çıkarılan yok. Not: ad "Kâhini" yerine "Kahini" yazıldı ki dosya adı ASCII olsun.

## 2026-11-08
**Eklenen düşmanlar (10):**
| Ad | Zırh | Seviye | Özel |
|---|---|---|---|
| Lanetli Korkuluk | Ölümsüz | 6 | – |
| Kafatası Yengeci | Böcek | 9 | kalkan |
| Fener Balığı Adam | Bataklık | 17 | iyileşir |
| Kuklacı | Kültist | 24 | – |
| Çan Kafalı | Zırhlı | 33 | kalkan |
| Yüzsüz | Hayalet | 41 | hızlı |
| Diken Geyik | Canavar | 41 | hızlı |
| Lav Kaplumbağası | Şeytan | 42 | yavaş |
| Kırağı Güvesi | Buz | 43 | uçan |
| Sis Kontu | Vampir | 44 | iyileşir |

**Boss:** Mezar Leviatanı (Bataklık, bölüm 42)

**Kule:** Buz Mızrağı — Buz, delici, menzil 190, atış 0.6/sn, hasar 36, maliyet 300, oyuncu Sv 25

**Kalite kontrol:**
- Lanetli Korkuluk: ilk görsel çiftçiye benziyordu → prompt netleştirilip yeniden üretildi; bacak arasındaki beyaz arka plan artığı elle silindi.
- Kafatası Yengeci: iki üretimde de kabukta delik vardı; yeniden üretilen daha kötüydü → ilk görsel, kabuktaki kapalı delik kemik rengiyle doldurularak kullanıldı.
- Mezar Leviatanı: yeniden üretilen görselde kopuk ikinci parça vardı → ilk görsel geri alındı.
- Çıkarılan karakter yok. Toplam: 31 kule, 211 düşman.
