"""Her düşmana hareket tipi atar → hareket.json (oyun assets/td/hareket.json olarak kullanır).

kare     : gerçek 4 karelik yürüme şeridi var (anim2/E)
kanat    : kanatlı uçan (kanat çırpar, havada salınır)
suzul    : hayalet/ruh/küre (süzülür, kenarları dalgalanır, saydamlık nabzı)
surun    : balçık/solucan/yılan/dokunaç (esneyip büzülerek ilerler)
zipla    : kurbağa benzeri (sıçrayarak)
bocek    : örümcek/akrep/böcek (hızlı kısa adımlar, titreşim)
dortayak : kurt/ayı/fare/geyik (dörtnala: öne-arkaya sallanma, ön/arka bacak sırayla)
yuzen    : balık/mürekkep balığı (havada yüzer, gövde dalgalanır)
cuppe    : cüppeli/elbiseli (kayarak, etek dalgalanır)
adim     : diğer iki ayaklılar (shader ile sırayla ayak kaldırma)
Kullanım: python araclar/hareket.py  → hareket.json
"""
import json, os, re

pr = dict(l.strip().split("|", 1) for l in open("tasarim/promptlar.txt", encoding="utf-8") if "|" in l)
veri = json.load(open("tasarim/oyun_verisi.json"))


def var(p, kelimeler):
    return re.search(r"\b(" + kelimeler + r")\b", p) is not None


KURALLAR = [
    ("suzul", r"ghosts?|ghostly|spirits?|wraiths?|phantoms?|spectres?|specters?|spectral|banshees?|wisps?|shades?|"
              r"floating|hovering|orbs?|eyeballs?|eyes?|clouds?|storm|souls?|apparition|will-o|lantern ghost|mist|smoke"),
    ("kanat", r"wings?|winged|bats?|flying|birds?|crows?|ravens?|vultures?|owls?|harpy|harpies|moths?|wasps?|flies|fly|"
              r"dragons?|wyverns?|gargoyles? flying|griffin|gryphon"),
    ("zipla", r"frogs?|toads?"),
    ("yuzen", r"fish|squid|octopus|eel|anglerfish"),
    ("surun", r"slimes?|ooze|blob|worms?|serpents?|snakes?|leech|maggots?|larva|tentacles?|jelly|jellyfish|slug|mimic|"
              r"amoeba|pudding|hydra"),
    ("bocek", r"spiders?|scorpions?|beetles?|insects?|centipedes?|crabs?|mantis|ants?|ticks?|swarm|locusts?|roach|cockroach"),
    ("dortayak", r"wolf|wolves|werewolf|hounds?|dogs?|cats?|bears?|boar|rats?|deer|stag|elk|mammoth|horses?|rider|mounted|"
                 r"cerberus|chimera|tiger|lion|panther|goat|ram|bull|hyena|beast|fox|jackal|lizard|crocodile|salamander|turtle|tortoise"),
    ("cuppe", r"robed?|robes|cloak|cloaked|dress|gown|cultist|witch|priest|priestess|monk|nun|bride|mage|wizard|"
              r"sorcerer|sorceress|necromancer|hooded|veil|shroud|reaper"),
]


def tip(d):
    ad = "d_" + d["id"]
    if os.path.exists(f"anim2/E/{ad}.png"):
        return "kare"
    p = (pr.get(ad, "") + " " + d.get("prompt", "")).lower()
    ozel = d.get("ozel") or []
    if d.get("zirh") == "Hayalet":
        return "suzul"
    for t, k in KURALLAR:
        if var(p, k):
            return t
    if "ucan" in ozel or d.get("zirh") == "Ucan":
        return "kanat"
    if d.get("zirh") == "Bocek":
        return "bocek"
    if d.get("zirh") == "Bataklik" and var(p, "swamp thing|mud|bog"):
        return "surun"
    return "adim"


sonuc = {d["id"]: tip(d) for d in veri["dusmanlar"]}
json.dump(sonuc, open("hareket.json", "w"), ensure_ascii=False, indent=0, sort_keys=True)
from collections import Counter
print(Counter(sonuc.values()))
