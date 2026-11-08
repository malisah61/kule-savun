using Godot;
using System;
using System.Collections.Generic;

/// <summary>Bir yetenek ağacı düğümü: kuleye çarpan veya özel yetenek ekler.</summary>
public class YetenekDugum
{
	public string Ad, Aciklama, Ozel;
	public float Hasar, Menzil, Hiz, ZirhDelme;
	public int Bedel;
}

/// <summary>
/// Her kulenin iki dalı vardır: Güç (0) ve Ustalık (1), her biri 3 kademe.
/// Bir dal 3'e kadar çıkabilir; iki dal birden 2'yi geçemez (seçim yaptırır).
/// 3. kademe kulenin saldırı biçimine özel bir yetenek açar.
/// </summary>
public static class YetenekAgaci
{
	public static readonly string[] DalAd = { "Güç", "Ustalık" };

	static readonly Dictionary<string, (string, string)> GucAd = new()
	{
		["Fiziksel"] = ("Keskin Uçlar", "Çelik Uçlar"), ["Ates"] = ("Kızgın Kor", "Ejder Ateşi"),
		["Buz"] = ("Kara Buz", "Kutup Soğuğu"), ["Zehir"] = ("Asit", "Kara Veba"),
		["Yildirim"] = ("Yüksek Gerilim", "Fırtına Kalbi"), ["Kutsal"] = ("Kutsanmış", "İlahi Öfke"),
		["Golge"] = ("Karanlık Öz", "Boşluğun Gücü"), ["Kusatma"] = ("Ağır Gülle", "Yıkım"),
	};

	// mod -> (Güç dalı 3. kademe, Ustalık dalı 3. kademe): (kod, ad, açıklama)
	static readonly Dictionary<string, ((string, string, string), (string, string, string))> Ozel = new()
	{
		["tek"] = (("kritik", "Kritik Vuruş", "%25 şansla 3 kat hasar"), ("cift", "Çift Atış", "Aynı anda 2 hedefe atar")),
		["alan"] = (("buyuk_alan", "Büyük Patlama", "Patlama alanı +%60"), ("sarsinti", "Sarsıntı", "Patlama düşmanları sersemletir")),
		["zincir"] = (("asiri_yuk", "Aşırı Yük", "Zincir hasarı azalmaz"), ("ag", "Fırtına Ağı", "+4 hedefe sıçrar")),
		["yavas"] = (("donduran", "Donduran", "Yavaşlatma %70'e çıkar"), ("kirilgan", "Kırılganlık", "Yavaşlayan düşman tüm hasardan +%30 alır")),
		["zehir"] = (("olumcul", "Ölümcül Zehir", "Zehir hasarı 2 kat"), ("salgin", "Salgın", "Zehirli düşman ölünce zehir yayılır")),
		["delici"] = (("yakici_isin", "Dev Avcısı", "Bosslara 2 kat hasar"), ("genis_isin", "Geniş Işın", "Işın 2 kat geniş, menzil +%25")),
		["ucan_avci"] = (("avci", "Gök Avcısı", "Uçanlara 3 kat hasar"), ("suru", "Sürü Saldırısı", "Aynı anda 3 hedefe atar")),
		["aura"] = (("yogun_aura", "Yoğun Aura", "Aura hasarı 2 kat"), ("genis_aura", "Geniş Aura", "Menzil +%40 ve yavaşlatır")),
		["yakici"] = (("cehennem", "Cehennem", "Yanma 2 kat ve daha geniş"), ("kul", "Kül Patlaması", "Yanan düşman ölünce patlar")),
		["sersem"] = (("uzun_sersem", "Derin Kâbus", "Sersemletme süresi 2 kat"), ("ruh_hasadi", "Ruh Hasadı", "Öldürdüklerinden +%60 altın")),
	};

	public static List<YetenekDugum>[] Olustur(KuleTanim t)
	{
		var (g1, g2) = GucAd.TryGetValue(t.HasarTuru, out var g) ? g : ("Güç I", "Güç II");
		var (oa, ob) = Ozel[t.Mod];
		int B(float c) => Mathf.RoundToInt(t.Maliyet * c / 5f) * 5;
		var guc = new List<YetenekDugum>
		{
			new() { Ad = g1, Aciklama = "+%35 hasar", Hasar = 0.35f, Bedel = B(0.55f) },
			new() { Ad = g2, Aciklama = "+%45 hasar, +%10 menzil", Hasar = 0.45f, Menzil = 0.1f, Bedel = B(0.95f) },
			new() { Ad = oa.Item2, Aciklama = oa.Item3, Ozel = oa.Item1, Hasar = 0.25f, Bedel = B(1.7f) },
		};
		var usta = new List<YetenekDugum>
		{
			new() { Ad = "Hızlı Mekanizma", Aciklama = "+%25 atış hızı", Hiz = 0.25f, Bedel = B(0.5f) },
			new() { Ad = "Zırh Delici", Aciklama = "Dirençleri yarıya indirir, +%15 menzil", ZirhDelme = 0.5f, Menzil = 0.15f, Bedel = B(0.9f) },
			new() { Ad = ob.Item2, Aciklama = ob.Item3, Ozel = ob.Item1, Hiz = 0.15f, Bedel = B(1.6f) },
		};
		if (ob.Item1 == "genis_isin") usta[2].Menzil = 0.25f;
		if (ob.Item1 == "genis_aura") usta[2].Menzil = 0.4f;
		return new[] { guc, usta };
	}

	/// <summary>Bu dalın sonraki kademesi alınabilir mi? (bedel hariç)</summary>
	public static bool Alinabilir(int[] dal, int i) =>
		dal[i] < 3 && !(dal[i] + 1 >= 2 && dal[1 - i] >= 2);
}
