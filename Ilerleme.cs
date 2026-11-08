using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Kalıcı yükseltme düğümü (kule başına 10 tane, her biri küçük bir artış).</summary>
public class KaliciDugum
{
	public string Ad;
	public float Hasar, Menzil, Hiz, Indirim, ZirhDelme;
	public int Para, Puan = 1;
}

/// <summary>
/// Kalıcı ilerleme: XP → oyuncu seviyesi (kuleleri açar, yetenek puanı verir),
/// para → kalıcı yetenek ağacı. Bölüm kaybedilse de kazanılan XP ve para kalır.
/// </summary>
public static class Ilerleme
{
	const string Dosya = "user://td_kayit.json";
	public const int PuanPerSeviye = 2;
	public const int MaxSeviye = 60;

	public static int EnUstBolum = 1;
	public static int Xp, Para, HarcananPuan;
	public static readonly Dictionary<string, int> Kademe = new();

	// ------------------------------------------------------------ seviye
	/// <summary>n. seviyeden n+1'e geçmek için gereken XP.</summary>
	public static int SeviyeXp(int n) => 60 + 30 * (n - 1) + 4 * (n - 1) * (n - 1);

	public static int Seviye
	{
		get
		{
			int sv = 1, kalan = Xp;
			while (sv < MaxSeviye && kalan >= SeviyeXp(sv)) { kalan -= SeviyeXp(sv); sv++; }
			return sv;
		}
	}

	/// <summary>Mevcut seviyede biriken XP ve bir sonraki seviyeye gereken.</summary>
	public static (int ilerleme, int gereken) SeviyeDurumu()
	{
		int sv = 1, kalan = Xp;
		while (sv < MaxSeviye && kalan >= SeviyeXp(sv)) { kalan -= SeviyeXp(sv); sv++; }
		return (kalan, SeviyeXp(sv));
	}

	public static int ToplamPuan => PuanPerSeviye * Seviye;
	public static int Puan => ToplamPuan - HarcananPuan;

	public static bool KuleAcik(KuleTanim t) => t.Level <= Seviye;

	// ------------------------------------------------------------ yetenek ağacı (10 düğüm / kule)
	static readonly (string ad, float h, float m, float hz, float ind, float z)[] Desen =
	{
		("Keskinlik", 0.02f, 0, 0, 0, 0), ("Görüş", 0, 0.02f, 0, 0, 0), ("Çeviklik", 0, 0, 0.02f, 0, 0),
		("Sertlik", 0.02f, 0, 0, 0, 0), ("Tutumluluk", 0, 0, 0, 0.02f, 0), ("Kudret", 0.03f, 0, 0, 0, 0),
		("Seri El", 0, 0, 0.02f, 0, 0), ("Delicilik", 0, 0, 0, 0, 0.03f), ("Kartal Gözü", 0, 0.02f, 0, 0, 0),
		("Ustalık", 0.04f, 0, 0.02f, 0, 0),
	};

	public static List<KaliciDugum> Dugumler(KuleTanim t)
	{
		var l = new List<KaliciDugum>();
		for (int i = 0; i < Desen.Length; i++)
		{
			var d = Desen[i];
			l.Add(new KaliciDugum
			{
				Ad = d.ad, Hasar = d.h, Menzil = d.m, Hiz = d.hz, Indirim = d.ind, ZirhDelme = d.z,
				Para = Mathf.RoundToInt((15 + 12 * i) * (1f + t.Level * 0.08f) / 5f) * 5,
				Puan = i >= 7 ? 2 : 1,
			});
		}
		return l;
	}

	public static string Aciklama(KaliciDugum d)
	{
		var p = new List<string>();
		if (d.Hasar > 0) p.Add($"+%{d.Hasar * 100:0} hasar");
		if (d.Menzil > 0) p.Add($"+%{d.Menzil * 100:0} menzil");
		if (d.Hiz > 0) p.Add($"+%{d.Hiz * 100:0} atış hızı");
		if (d.Indirim > 0) p.Add($"%{d.Indirim * 100:0} ucuz");
		if (d.ZirhDelme > 0) p.Add($"+%{d.ZirhDelme * 100:0} zırh delme");
		return string.Join(", ", p);
	}

	public static int KuleKademe(string id) => Kademe.TryGetValue(id, out var k) ? k : 0;

	/// <summary>Kulenin kalıcı ağaçtan aldığı toplam bonus.</summary>
	public static (float hasar, float menzil, float hiz, float indirim, float zirh) Bonus(KuleTanim t)
	{
		float h = 0, m = 0, hz = 0, ind = 0, z = 0;
		var l = Dugumler(t);
		for (int i = 0; i < KuleKademe(t.Id); i++) { h += l[i].Hasar; m += l[i].Menzil; hz += l[i].Hiz; ind += l[i].Indirim; z += l[i].ZirhDelme; }
		return (h, m, hz, ind, z);
	}

	public static int Maliyet(KuleTanim t) => Mathf.RoundToInt(t.Maliyet * (1f - Bonus(t).indirim));

	/// <summary>Sıradaki düğümü alır; alınamıyorsa nedenini döndürür.</summary>
	public static string DugumAl(KuleTanim t)
	{
		int k = KuleKademe(t.Id);
		if (!KuleAcik(t)) return $"Oyuncu seviyesi {t.Level} gerekli";
		if (k >= 10) return "Tamamlandı";
		var d = Dugumler(t)[k];
		if (Puan < d.Puan) return "Yetenek puanı yetmiyor";
		if (Para < d.Para) return "Para yetmiyor";
		Para -= d.Para;
		HarcananPuan += d.Puan;
		Kademe[t.Id] = k + 1;
		Kaydet();
		return null;
	}

	// ------------------------------------------------------------ kayıt
	public static void Yukle()
	{
		if (!FileAccess.FileExists(Dosya)) return;
		var d = Json.ParseString(FileAccess.GetFileAsString(Dosya)).AsGodotDictionary();
		if (d.ContainsKey("enUst")) EnUstBolum = Math.Max(1, d["enUst"].AsInt32());
		if (d.ContainsKey("xp")) Xp = d["xp"].AsInt32();
		if (d.ContainsKey("para")) Para = d["para"].AsInt32();
		if (d.ContainsKey("harcananPuan")) HarcananPuan = d["harcananPuan"].AsInt32();
		if (d.ContainsKey("kademe"))
			foreach (var (k, v) in d["kademe"].AsGodotDictionary()) Kademe[k.AsString()] = v.AsInt32();
	}

	public static void Kaydet()
	{
		var kad = new Godot.Collections.Dictionary();
		foreach (var (k, v) in Kademe) kad[k] = v;
		var d = new Godot.Collections.Dictionary
		{
			["enUst"] = EnUstBolum, ["xp"] = Xp, ["para"] = Para, ["harcananPuan"] = HarcananPuan, ["kademe"] = kad
		};
		using var f = FileAccess.Open(Dosya, FileAccess.ModeFlags.Write);
		f?.StoreString(Json.Stringify(d));
	}
}
