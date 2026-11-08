# Kule Savunma — yol + kule tasarım verisi üretici.
# Çıktı: tasarim/oyun_verisi.json (Godot) ve tasarim/promptlar.txt (görsel üretimi)
import json, math, os, random

HASAR = ["Fiziksel", "Ates", "Buz", "Zehir", "Yildirim", "Kutsal", "Golge", "Kusatma"]
HASAR_TR = {"Fiziksel": "Fiziksel", "Ates": "Ateş", "Buz": "Buz", "Zehir": "Zehir", "Yildirim": "Yıldırım",
            "Kutsal": "Kutsal", "Golge": "Gölge", "Kusatma": "Kuşatma"}

# Zırh tipi -> hasar türüne göre çarpan (1 = normal, >1 zayıf, <1 dirençli, 0 bağışık)
ZIRH = {
    "Olumsuz":  {"Kutsal": 2.0, "Ates": 1.5, "Zehir": 0.25, "Golge": 0.5},
    "Hayalet":  {"Fiziksel": 0.25, "Kusatma": 0.25, "Kutsal": 2.0, "Yildirim": 1.5, "Golge": 0.5},
    "Canavar":  {"Fiziksel": 1.25, "Zehir": 1.5, "Golge": 0.75},
    "Zirhli":   {"Fiziksel": 0.5, "Zehir": 0.5, "Kusatma": 1.75, "Yildirim": 1.5},
    "Seytan":   {"Ates": 0.0, "Kutsal": 2.0, "Buz": 1.5, "Golge": 0.5},
    "Vampir":   {"Kutsal": 2.0, "Ates": 1.5, "Golge": 0.25, "Zehir": 0.5},
    "Bocek":    {"Zehir": 2.0, "Ates": 1.5, "Kusatma": 0.75},
    "Tas":      {"Fiziksel": 0.5, "Zehir": 0.0, "Kusatma": 2.0, "Buz": 0.5},
    "Buz":      {"Buz": 0.0, "Ates": 2.0, "Yildirim": 0.75},
    "Bataklik": {"Zehir": 0.0, "Ates": 1.75, "Buz": 1.25},
    "Kultist":  {"Fiziksel": 1.5, "Golge": 0.25, "Kutsal": 1.25},
    "Ucan":     {"Kusatma": 0.0, "Fiziksel": 1.25, "Yildirim": 1.5},
}
ZIRH_TR = {"Olumsuz": "Ölümsüz", "Hayalet": "Hayalet", "Canavar": "Canavar", "Zirhli": "Zırhlı", "Seytan": "Şeytan",
           "Vampir": "Vampir", "Bocek": "Böcek", "Tas": "Taş", "Buz": "Buz", "Bataklik": "Bataklık", "Kultist": "Kültist", "Ucan": "Uçan"}

# ---------------- 30 kule ----------------
# (id, ad, hasar, mod, menzil, atis/sn, hasar, maliyet, acilma_level, prompt)
# mod: tek | alan | zincir | yavas | zehir | delici | ucan_avci | aura | yakici | sersem
KULELER = [
 ("okcu",        "Okçu Kulesi",        "Fiziksel","tek",     150,1.4,  10,  60, 1, "wooden archer watchtower with arrow slits"),
 ("tas_atar",    "Taş Atar",           "Kusatma", "alan",    140,0.5,  22,  90, 1, "medieval catapult tower with a stone basket"),
 ("ates_kazani", "Ateş Kazanı",        "Ates",    "yakici",  110,0.8,   8,  80, 2, "stone tower with a burning fire cauldron on top"),
 ("buz_kristali","Buz Kristali",       "Buz",     "yavas",   130,0.9,   6,  85, 3, "tower with a floating blue ice crystal"),
 ("kutsal_can",  "Kutsal Çan",         "Kutsal",  "alan",    120,0.6,  14, 110, 4, "white chapel bell tower with golden glow"),
 ("zehir_kazani","Zehir Kazanı",       "Zehir",   "zehir",   130,0.8,   5,  90, 5, "tower with a bubbling green poison cauldron"),
 ("tatar_yayi",  "Tatar Yayı",         "Fiziksel","delici",  190,0.7,  26, 130, 6, "ballista crossbow tower with heavy bolts"),
 ("yildirim",    "Yıldırım Kulesi",    "Yildirim","zincir",  140,0.7,  16, 140, 7, "tesla coil wizard tower crackling with lightning"),
 ("golge_obel",  "Gölge Obeliski",     "Golge",   "tek",     150,1.0,  15, 120, 8, "black obsidian obelisk with purple shadow runes"),
 ("havan",       "Havan Topu",         "Kusatma", "alan",    210,0.35, 45, 170, 9, "iron mortar cannon fortification"),
 ("sahin_yuvasi","Şahin Yuvası",       "Fiziksel","ucan_avci",170,1.6, 12, 120,10, "falconer tower with hawk nests and banners"),
 ("ejder_agzi",  "Ejder Ağzı",         "Ates",    "alan",    120,0.9,  18, 180,11, "stone dragon head statue tower breathing fire"),
 ("don_kuyusu",  "Don Kuyusu",         "Buz",     "aura",    110,1.0,   4, 150,12, "frozen well tower emitting cold mist"),
 ("ilahi_isik",  "İlahi Işık",         "Kutsal",  "delici",  200,0.6,  30, 200,13, "angel statue tower shooting a holy light beam"),
 ("orumcek_agi", "Örümcek Ağı",        "Zehir",   "yavas",   120,1.0,   6, 140,14, "spider web tower with a giant spider on top"),
 ("firtina",     "Fırtına Totemi",     "Yildirim","alan",    150,0.5,  24, 210,15, "storm totem pole with dark clouds and lightning"),
 ("kemik_topu",  "Kemik Topu",         "Kusatma", "alan",    160,0.6,  30, 190,16, "cannon made of bones and skulls"),
 ("ruh_feneri",  "Ruh Feneri",         "Golge",   "sersem",  130,0.7,  12, 200,17, "ghostly lantern tower with trapped souls"),
 ("keskin_nisan","Keskin Nişancı",     "Fiziksel","tek",     260,0.5,  60, 230,18, "tall sniper tower with a long crossbow"),
 ("lav_kuyusu",  "Lav Kuyusu",         "Ates",    "yakici",  120,0.7,  20, 240,19, "volcanic tower pouring lava"),
 ("buzul_topu",  "Buzul Topu",         "Buz",     "alan",    170,0.45, 28, 240,20, "ice cannon tower firing frost balls"),
 ("aziz_heykeli","Aziz Heykeli",        "Kutsal",  "aura",    130,1.0,  10, 260,21, "statue of a praying saint with a halo"),
 ("veba_kulesi", "Veba Kulesi",        "Zehir",   "alan",    140,0.6,  14, 250,22, "plague doctor tower releasing toxic fog"),
 ("sihir_kulesi","Arkan Kule",         "Yildirim","delici",  180,0.8,  34, 280,24, "arcane mage tower with floating runes"),
 ("kara_delik",  "Kara Delik",         "Golge",   "yavas",   150,0.5,  20, 300,26, "void portal tower swirling with darkness"),
 ("mancinik",    "Dev Mancınık",       "Kusatma", "alan",    240,0.3,  80, 320,28, "giant trebuchet fortress"),
 ("anka",        "Anka Yuvası",        "Ates",    "ucan_avci",190,1.0, 30, 330,30, "phoenix nest tower with burning feathers"),
 ("kader_carki", "Kader Çarkı",        "Yildirim","zincir",  170,0.9,  36, 360,33, "clockwork tower with a spinning golden wheel"),
 ("melek_kilici","Melek Kılıcı",       "Kutsal",  "tek",     180,1.1,  48, 400,36, "giant glowing holy sword stuck in a stone pedestal"),
 ("kiyamet",     "Kıyamet Kulesi",     "Golge",   "alan",    200,0.4, 110, 500,40, "apocalypse tower with a burning eye on top"),
]
assert len(KULELER) == 30
EKLER = json.load(open("tasarim/ekler.json", encoding="utf-8")) if os.path.exists("tasarim/ekler.json") else {"kuleler": [], "dusmanlar": []}
for k in EKLER.get("kuleler", []):
    assert k["hasarTuru"] in HASAR and k["mod"] in {"tek","alan","zincir","yavas","zehir","delici","ucan_avci","aura","yakici","sersem"}, k
    KULELER.append((k["id"], k["ad"], k["hasarTuru"], k["mod"], k["menzil"], k["atisHizi"], k["hasar"], k["maliyet"], k["level"], k["prompt"]))

# ---------------- 200 düşman ----------------
# aile: (zırh, varsayılan özellikler, [(ad, prompt), ...])
AILELER = [
 ("Olumsuz", [], [
  ("Çürük Zombi","rotting green zombie in torn rags"),("Köylü Zombi","zombie peasant with a pitchfork"),
  ("Kemik Piyade","skeleton soldier with a rusty sword"),("İskelet Okçu","skeleton archer with a bow"),
  ("Kafatası Fırlatan","skeleton throwing skulls"),("Mezar Kazıcı","undead gravedigger with a shovel"),
  ("Mumya","bandaged mummy"),("Kemik Kalkan","skeleton with a large wooden shield"),
  ("Zombi Kasap","fat zombie butcher with a cleaver"),("Gul","pale ghoul crawling with long claws"),
  ("Kemik Şövalye","skeleton knight in black armor"),("Lanetli Rahip","undead priest in a torn robe"),
  ("Leş Yiyici","hunched corpse eater ghoul"),("Kemik Dev","giant skeleton made of many bones"),
  ("İskelet Atlı","skeleton riding a skeletal horse"),("Zombi Dev","huge stitched zombie brute"),
  ("Mumya Kral","mummy pharaoh with a golden mask"),("Kemik Ejder","small skeletal dragon walking"),
  ("Kafasız Asker","headless undead soldier holding his head"),("Veba Zombisi","plague zombie with boils"),
 ]),
 ("Hayalet", [], [
  ("Gezgin Ruh","pale wandering ghost"),("Feryatçı","banshee screaming ghost woman"),
  ("Fener Hayaleti","ghost carrying a lantern"),("Zincirli Ruh","ghost wrapped in chains"),
  ("Gölge Çocuk","creepy ghost child"),("Hortlak","hooded wraith with glowing eyes"),
  ("Boğulmuş Ruh","drowned ghost dripping water"),("Hayalet Şövalye","ghost knight with a spectral sword"),
  ("Kötü Ruh","red angry poltergeist spirit"),("Ruh Toplayıcı","ghost reaper with a scythe"),
  ("Ayna Hayaleti","ghost emerging from a broken mirror"),("Hayalet Gelin","ghost bride in a torn veil"),
  ("Fısıltı","shadowy whispering specter"),("Hayalet Kaptan","ghost pirate captain"),
  ("Ruh Fırtınası","swirling storm of ghost faces"),("Hortlak Lordu","wraith lord with a crown"),
  ("Kukla Ruh","haunted porcelain doll ghost"),("Mum Hayaleti","ghost made of melting candle wax"),
 ]),
 ("Canavar", ["hizli"], [
  ("Kara Kurt","black wolf with red eyes"),("Kurt Adam","hulking werewolf"),
  ("Dev Fare","giant plague rat"),("Kuduz Köpek","rabid hound"),
  ("Yaban Domuzu","demonic wild boar with tusks"),("Ayı Canavarı","undead bear monster"),
  ("Hırlayan","goblin-like gremlin creature"),("Mağara Trolü","cave troll with a club"),
  ("Boynuzlu Canavar","horned beast walking on two legs"),("Kertenkele Adam","lizardman warrior"),
  ("Dev Örümcek","giant black spider"),("Kurt Sürü Lideri","giant alpha wolf with scars"),
  ("Yeti","white furred yeti"),("Kemirici","ratman with daggers"),
  ("Chupakabra","chupacabra creature with spines"),("Mantikor","manticore with scorpion tail"),
  ("Kara Panter","shadow panther"),("Wendigo","wendigo with antlers and skull face"),
 ]),
 ("Zirhli", ["kalkan"], [
  ("Kara Muhafız","dark guard in heavy armor"),("Paslı Şövalye","rusty armored knight"),
  ("Kalkan Taşıyıcı","soldier with a tower shield"),("Zırhlı Gulyabani","armored ghoul brute"),
  ("Kara Paladin","dark paladin with a mace"),("Demir Bekçi","iron sentinel armor"),
  ("Boş Zırh","living empty suit of armor"),("Ork Savaşçı","orc warrior in spiked armor"),
  ("Ork Şef","orc chieftain with a big axe"),("Ölüm Şövalyesi","death knight with a glowing sword"),
  ("Cellat","executioner with a giant axe and hood"),("Gladyatör","undead gladiator"),
  ("Kara Lord","dark lord in spiked black armor"),("Zırhlı Kurt","wolf in iron armor"),
  ("Kuşatma Devi","giant ogre with a siege hammer"),("Demir Golem","iron golem"),
 ]),
 ("Seytan", [], [
  ("İmp","small red imp"),("Ateş İblisi","fire demon"),("Boynuzlu Şeytan","horned devil with a trident"),
  ("Cehennem Köpeği","hellhound with flaming mane"),("Sukkubus","winged succubus demon"),
  ("Lav İblisi","lava demon"),("Kemirgen İblis","demonic rat imp"),("Zincirli İblis","demon wrapped in chains"),
  ("Kan İblisi","blood demon"),("Çok Gözlü","demon with many eyes"),("Kül İblisi","ash demon"),
  ("İşkenceci","demon torturer with hooks"),("Kara Keçi","goat headed demon baphomet style"),
  ("Cehennem Şövalyesi","hell knight with burning armor"),("Kırmızı Dev","giant red demon"),
  ("Balrog","huge fire demon with a whip"),
 ]),
 ("Vampir", ["iyilesir"], [
  ("Vampir Yarasa","giant vampire bat"),("Genç Vampir","young pale vampire"),("Vampir Kontes","vampire countess in a red dress"),
  ("Kan Emici","feral vampire creature"),("Vampir Asilzade","vampire noble with a cane"),
  ("Strigoi","hunched strigoi vampire"),("Gece Avcısı","vampire hunter turned vampire"),
  ("Kan Rahibesi","vampire blood priestess"),("Vampir Muhafız","vampire guard with a halberd"),
  ("Nosferatu","bald nosferatu vampire with long fingers"),("Vampir Gelin","vampire bride"),
  ("Kan Lordu","vampire lord with a long cape"),
  ("Vampir Uşak","hunchback vampire servant"),("Kan Şövalyesi","vampire knight in crimson armor"),
 ]),
 ("Bocek", ["kalabalik"], [
  ("Dev Sinek","giant fly"),("Leş Böceği","carrion beetle"),("Kırkayak","giant centipede"),
  ("Akrep","giant scorpion"),("Kanlı Sivrisinek","giant blood mosquito"),("Sürü Karıncası","giant soldier ant"),
  ("Larva","giant maggot larva"),("Peygamber Devesi","giant praying mantis"),("Kene","giant tick"),
  ("Böcek Kraliçe","insect queen"),("Zırhlı Böcek","armored beetle"),("Yumurta Taşıyıcı","bug carrying eggs"),
  ("Ateş Böceği","glowing fire beetle"),("Ağ Örücü","web spinning spider"),("Kurt Böceği","giant worm"),("Eşek Arısı","giant hornet"),
 ]),
 ("Tas", ["yavas"], [
  ("Taş Gargoyl","stone gargoyle"),("Kaya Golem","rock golem"),("Mezar Taşı Canlısı","living tombstone monster"),
  ("Heykel Bekçi","animated stone statue guardian"),("Kristal Golem","crystal golem"),("Kum Golemi","sand golem"),
  ("Obsidyen Dev","obsidian giant"),("Kemik Golem","bone golem"),("Et Golemi","flesh golem"),
  ("Dev Gargoyl","huge gargoyle"),("Toprak Elementali","earth elemental"),("Anıt Dev","colossal stone titan"),
 ]),
 ("Buz", [], [
  ("Buz Zombisi","frozen zombie"),("Kar Hortlağı","snow wraith"),("Buz Kurdu","ice wolf"),
  ("Don Devi","frost giant"),("Buz Elementali","ice elemental"),("Buz Cadısı","ice witch"),
  ("Donmuş Şövalye","frozen knight"),("Buz Örümceği","ice spider"),("Buzul Trol","glacier troll"),
  ("Kış Lordu","winter lord with an ice crown"),
  ("Buz Gulü","frost ghoul"),("Kar Adam","evil snowman monster"),("Buz Golemi","ice golem"),("Kar Leoparı","snow leopard beast"),
 ]),
 ("Bataklik", ["iyilesir"], [
  ("Bataklık Zombisi","swamp zombie covered in moss"),("Kurbağa Adam","frog man warrior"),
  ("Balçık","green slime blob"),("Bataklık Cadısı","swamp hag witch"),("Sülük","giant leech"),
  ("Yosun Canavarı","moss monster"),("Timsah Adam","crocodile man"),("Çürük Ent","rotten tree monster"),
  ("Mantar Adam","fungus mushroom man"),("Hidra","small swamp hydra"),("Zehirli Balçık","toxic purple slime"),
  ("Bataklık Kralı","swamp king monster with a crown of roots"),
 ]),
 ("Kultist", [], [
  ("Kültist","hooded cultist with a dagger"),("Kara Rahip","dark priest"),("Kemik Büyücüsü","necromancer"),
  ("Cadı","old witch with a broom"),("Kurban Taşıyıcı","cultist carrying a candle"),("Maskeli Tarikatçı","masked cultist"),
  ("Lanet Okuyucu","curse caster with a book"),("Ölüm Büyücüsü","death wizard with a skull staff"),
  ("Cadı Kazanı Ustası","witch with a cauldron"),("Kan Büyücüsü","blood mage"),
  ("Tarikat Lideri","cult leader with a goat mask"),("Liç","lich with a glowing crown"),
 ]),
 ("Ucan", ["ucan"], [
  ("Yarasa Sürüsü","swarm of bats"),("Kuzgun","giant raven"),("Harpi","harpy"),
  ("Uçan Kafatası","flaming flying skull"),("Uçan Göz","floating demonic eyeball"),("Akbaba Canavar","undead vulture"),
  ("Kanatlı İblis","winged demon"),("Wyvern","wyvern"),("Gece Kanadı","shadow wing creature"),
  ("Kemik Kartal","skeletal eagle"),("Hayalet Kuş","ghost bird"),("Taş Kanat","flying stone gargoyle"),
 ]),
]

# Bosslar (20) — her 2 levelde bir
BOSSLAR = [
 ("Olumsuz","Kemik Kral","giant skeleton king on a bone throne"),("Canavar","Ay Kurt","giant werewolf howling"),
 ("Hayalet","Hayalet Kraliçe","ghost queen with a spectral crown"),("Zirhli","Kara Komutan","dark armored warlord"),
 ("Bocek","Böcek Ana","giant insect broodmother"),("Bataklik","Bataklık Hidrası","three headed swamp hydra"),
 ("Kultist","Kara Başrahip","dark high priest with tentacles"),("Tas","Taş Titan","colossal stone titan"),
 ("Vampir","Vampir Lordu","vampire lord with long white hair and red cape"),("Buz","Buz Ejderi","ice dragon"),
 ("Seytan","Cehennem Lordu","demon lord with huge horns"),("Ucan","Kara Ejder","black dragon"),
 ("Olumsuz","Liç Kral","lich king with a frozen crown"),("Canavar","Kimera","chimera beast"),
 ("Hayalet","Ölüm Meleği","angel of death with black wings"),("Zirhli","Kara Şövalye Lordu","black knight lord on a horse"),
 ("Seytan","İblis Prensi","demon prince with flaming sword"),("Tas","Golem Kral","golem king"),
 ("Kultist","Eski Tanrı","eldritch old god with tentacles"),("Seytan","Kıyamet Canavarı","apocalypse beast"),
]

def slug(s):
    tr = str.maketrans("çğıöşüÇĞİÖŞÜ ", "cgiosuCGIOSU_")
    return "".join(ch for ch in s.translate(tr).lower() if ch.isalnum() or ch == "_")

dusmanlar = []
for zirh, ozel, liste in AILELER:
    for i, (ad, pr) in enumerate(liste):
        dusmanlar.append({"ad": ad, "zirh": zirh, "ozel": list(ozel), "prompt": pr, "guc": i / max(1, len(liste) - 1)})
assert len(dusmanlar) == 180, len(dusmanlar)
for zirh, ad, pr in BOSSLAR:
    dusmanlar.append({"ad": ad, "zirh": zirh, "ozel": ["boss"] + (["ucan"] if zirh == "Ucan" else []), "prompt": pr, "guc": 1.0})
assert len(dusmanlar) == 200

# açılma leveli: aileler sırayla açılır; her ailenin içindeki güç arttıkça daha geç gelir
random.seed(7)
LEVEL_SAYISI = 40
aile_baslangic = {"Olumsuz": 1, "Canavar": 2, "Hayalet": 4, "Bocek": 5, "Zirhli": 7, "Ucan": 8, "Bataklik": 10,
                  "Kultist": 12, "Vampir": 14, "Tas": 16, "Buz": 18, "Seytan": 21}
normal = [d for d in dusmanlar if "boss" not in d["ozel"]]
for d in normal:
    b = aile_baslangic[d["zirh"]]
    d["level"] = min(LEVEL_SAYISI, b + round(d["guc"] * (LEVEL_SAYISI - 4 - b)))
bosslar = [d for d in dusmanlar if "boss" in d["ozel"]]
for i, d in enumerate(bosslar): d["level"] = 2 * (i + 1)
for e in EKLER.get("dusmanlar", []):
    assert e["zirh"] in ZIRH, e
    dusmanlar.append({"ad": e["ad"], "zirh": e["zirh"], "ozel": list(e.get("ozel", [])), "prompt": e["prompt"],
                      "guc": float(e.get("guc", 0.5)), "level": int(e["level"])})

ad_say = {}
for d in dusmanlar:
    s = slug(d["ad"]); ad_say[s] = ad_say.get(s, 0) + 1
    d["id"] = s if ad_say[s] == 1 else f"{s}_{ad_say[s]}"
    lv = d["level"]; boss = "boss" in d["ozel"]
    can = 30 * (1.16 ** (lv - 1)) * (0.8 + 0.6 * d["guc"])
    hiz = 42
    if "hizli" in d["ozel"]: hiz = 62
    if "yavas" in d["ozel"]: hiz = 28; can *= 1.6
    if "kalkan" in d["ozel"]: can *= 1.3
    if "kalabalik" in d["ozel"]: can *= 0.55; hiz = 50
    if "ucan" in d["ozel"]: hiz = 55; can *= 0.8
    if d["zirh"] == "Hayalet": hiz = 48
    if boss: can *= 14; hiz = 26
    d["can"] = round(can); d["hiz"] = hiz
    d["odul"] = max(3, round(4 + lv * 0.9 * (6 if boss else 1)))
    d["hasar"] = 10 if boss else (2 if d["guc"] > .6 else 1)  # savunma canından düşer

# güçlü / zayıf açıklaması
def carpan(zirh, h): return ZIRH[zirh].get(h, 1.0)
for d in dusmanlar:
    d["zayif"] = [HASAR_TR[h] for h in HASAR if carpan(d["zirh"], h) > 1.0]
    d["guclu"] = [HASAR_TR[h] for h in HASAR if carpan(d["zirh"], h) < 1.0]

kuleler = []
for (kid, ad, hasar, mod, menzil, hiz, dmg, mal, lv, pr) in KULELER:
    etkili = [ZIRH_TR[z] for z in ZIRH if ZIRH[z].get(hasar, 1.0) > 1.0]
    etkisiz = [ZIRH_TR[z] for z in ZIRH if ZIRH[z].get(hasar, 1.0) < 1.0]
    kuleler.append({"id": kid, "ad": ad, "hasarTuru": hasar, "mod": mod, "menzil": menzil, "atisHizi": hiz, "hasar": dmg,
                    "maliyet": mal, "level": lv, "prompt": pr, "etkili": etkili, "etkisiz": etkisiz})

veri = {"hasarTurleri": HASAR, "hasarTr": HASAR_TR, "zirhTr": ZIRH_TR, "carpanlar": ZIRH, "levelSayisi": LEVEL_SAYISI,
        "kuleler": kuleler, "dusmanlar": dusmanlar}
os.makedirs("tasarim", exist_ok=True)
json.dump(veri, open("tasarim/oyun_verisi.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)

with open("tasarim/promptlar.txt", "w", encoding="utf-8") as f:
    for k in kuleler:
        f.write(f"kule_{k['id']}|pixel art, {k['prompt']}, dark fantasy game defense tower building, whole building visible, front view, simple plain white background\n")
    for d in dusmanlar:
        extra = "huge boss monster, " if "boss" in d["ozel"] else ""
        f.write(f"d_{d['id']}|pixel art, {extra}{d['prompt']}, dark fantasy horror game enemy, full body, side view, simple plain white background\n")
boylar = {}
for k in kuleler: boylar["kule_" + k["id"]] = 100
for d in dusmanlar: boylar["d_" + d["id"]] = 120 if "boss" in d["ozel"] else (70 if "kalabalik" in d["ozel"] else 84)
json.dump(boylar, open("tasarim/boylar.json", "w"), indent=0)
print(len(kuleler), "kule,", len(dusmanlar), "düşman")
from collections import Counter
print(Counter(d["level"] for d in dusmanlar if "boss" not in d["ozel"]).most_common(5))
print("level başına yeni düşman:", sorted(Counter(d["level"] for d in dusmanlar).items())[:12])
