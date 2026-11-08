using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>assets/td/veri.json: 30 kule, 200 düşman, hasar türleri ve zırh çarpanları.</summary>
public class KuleTanim
{
	public string Id, Ad, HasarTuru, Mod;
	public float Menzil, AtisHizi, Hasar;
	public int Maliyet, Level;
	public string[] Etkili, Etkisiz;
	public Texture2D Doku;
}

public class DusmanTanim
{
	public string Id, Ad, Zirh;
	public HashSet<string> Ozel = new();
	public float Guc;
	public int Level;
	public string[] Zayif, Guclu;
	public Texture2D Doku;
	public bool Boss => Ozel.Contains("boss");
	public bool Ucan => Ozel.Contains("ucan") || Zirh == "Ucan";
}

public static class TdVeri
{
	public static readonly List<KuleTanim> Kuleler = new();
	public static readonly List<DusmanTanim> Dusmanlar = new();
	public static readonly Dictionary<string, Dictionary<string, float>> Carpan = new();
	public static readonly Dictionary<string, string> HasarTr = new(), ZirhTr = new();
	public static int LevelSayisi = 40;

	public static readonly Dictionary<string, Color> HasarRenk = new()
	{
		["Fiziksel"] = new Color(0.85f, 0.82f, 0.75f), ["Ates"] = new Color(1f, 0.5f, 0.15f), ["Buz"] = new Color(0.55f, 0.85f, 1f),
		["Zehir"] = new Color(0.45f, 0.95f, 0.3f), ["Yildirim"] = new Color(0.75f, 0.8f, 1f), ["Kutsal"] = new Color(1f, 0.92f, 0.5f),
		["Golge"] = new Color(0.65f, 0.35f, 1f), ["Kusatma"] = new Color(0.7f, 0.6f, 0.5f),
	};

	static string[] Dizi(Variant v) => v.AsGodotArray().Select(x => x.AsString()).ToArray();

	static Texture2D DokuYukle(string yol) => ResourceLoader.Exists(yol) ? GD.Load<Texture2D>(yol) : null;

	public static void Yukle()
	{
		if (Kuleler.Count > 0) return;
		var kok = Json.ParseString(FileAccess.GetFileAsString("res://assets/td/veri.json")).AsGodotDictionary();
		LevelSayisi = kok["levelSayisi"].AsInt32();
		foreach (var (k, v) in kok["hasarTr"].AsGodotDictionary()) HasarTr[k.AsString()] = v.AsString();
		foreach (var (k, v) in kok["zirhTr"].AsGodotDictionary()) ZirhTr[k.AsString()] = v.AsString();
		foreach (var (z, tablo) in kok["carpanlar"].AsGodotDictionary())
		{
			var d = new Dictionary<string, float>();
			foreach (var (h, c) in tablo.AsGodotDictionary()) d[h.AsString()] = (float)c.AsDouble();
			Carpan[z.AsString()] = d;
		}
		foreach (var kv in kok["kuleler"].AsGodotArray())
		{
			var d = kv.AsGodotDictionary();
			var t = new KuleTanim
			{
				Id = d["id"].AsString(), Ad = d["ad"].AsString(), HasarTuru = d["hasarTuru"].AsString(), Mod = d["mod"].AsString(),
				Menzil = (float)d["menzil"].AsDouble(), AtisHizi = (float)d["atisHizi"].AsDouble(), Hasar = (float)d["hasar"].AsDouble(),
				Maliyet = d["maliyet"].AsInt32(), Level = d["level"].AsInt32(), Etkili = Dizi(d["etkili"]), Etkisiz = Dizi(d["etkisiz"]),
			};
			t.Doku = DokuYukle($"res://assets/td/kule_{t.Id}.png");
			Kuleler.Add(t);
		}
		foreach (var dv in kok["dusmanlar"].AsGodotArray())
		{
			var d = dv.AsGodotDictionary();
			var t = new DusmanTanim
			{
				Id = d["id"].AsString(), Ad = d["ad"].AsString(), Zirh = d["zirh"].AsString(), Guc = (float)d["guc"].AsDouble(),
				Level = d["level"].AsInt32(), Zayif = Dizi(d["zayif"]), Guclu = Dizi(d["guclu"]),
			};
			foreach (var o in Dizi(d["ozel"])) t.Ozel.Add(o);
			t.Doku = DokuYukle($"res://assets/td/d_{t.Id}.png");
			Dusmanlar.Add(t);
		}
	}

	public static float HasarCarpani(string zirh, string hasar) =>
		Carpan.TryGetValue(zirh, out var t) && t.TryGetValue(hasar, out var c) ? c : 1f;

	/// <summary>Bölüm zorluğuna göre temel can (tüm düşmanlar bunun üstüne çarpanla).</summary>
	public static float TemelCan(int bolum) => 38f * Mathf.Pow(1f + 0.22f * (bolum - 1), 1.45f);
}
