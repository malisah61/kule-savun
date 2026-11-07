using Godot;
using System;
using System.Collections.Generic;

/// <summary>sprites.json'u okuyup her karakter için SpriteFrames kurar.</summary>
public static class SpriteKutuphane
{
	class Bilgi
	{
		public SpriteFrames Kareler;
		public Vector2 Ofset;
		public Dictionary<string, float> Sure = new();
	}

	static readonly Dictionary<string, Bilgi> Hepsi = new();
	static readonly HashSet<string> Dongulu = new() { "bekle", "yuru" };

	public static void Yukle()
	{
		if (Hepsi.Count > 0) return;
		var metin = FileAccess.GetFileAsString("res://assets/sprites/sprites.json");
		var kok = Json.ParseString(metin).AsGodotDictionary();
		foreach (var anahtar in kok.Keys)
		{
			string ad = anahtar.AsString();
			var d = kok[anahtar].AsGodotDictionary();
			int w = d["w"].AsInt32(), h = d["h"].AsInt32();
			var doku = GD.Load<Texture2D>($"res://assets/sprites/{ad}.png");
			var b = new Bilgi { Kareler = new SpriteFrames() };
			b.Ofset = new Vector2(w * 0.5f - (float)d["ayak_x"].AsDouble(), h * 0.5f - (float)d["ayak_y"].AsDouble());
			var animler = d["anim"].AsGodotDictionary();
			foreach (var ak in animler.Keys)
			{
				string an = ak.AsString();
				var a = animler[ak].AsGodotDictionary();
				int satir = a["satir"].AsInt32(), kare = a["kare"].AsInt32();
				float sure = (float)a["sure"].AsDouble();
				b.Kareler.AddAnimation(an);
				b.Kareler.SetAnimationLoop(an, Dongulu.Contains(an));
				b.Kareler.SetAnimationSpeed(an, kare / Mathf.Max(0.1f, sure));
				for (int i = 0; i < kare; i++)
					b.Kareler.AddFrame(an, new AtlasTexture { Atlas = doku, Region = new Rect2(i * w, satir * h, w, h) });
				b.Sure[an] = sure;
			}
			if (b.Kareler.HasAnimation("default"))
				b.Kareler.RemoveAnimation("default");
			Hepsi[ad] = b;
		}
	}

	/// <summary>Ayakları node konumunda duran animasyonlu sprite.</summary>
	public static AnimatedSprite2D Olustur(string ad, float olcek, bool solaBak)
	{
		var b = Hepsi[ad];
		var s = new AnimatedSprite2D { SpriteFrames = b.Kareler, Offset = b.Ofset, Scale = Vector2.One * olcek, FlipH = solaBak };
		if (solaBak) s.Offset = new Vector2(-b.Ofset.X, b.Ofset.Y);
		return s;
	}

	public static float Sure(string ad, string anim) =>
		Hepsi.TryGetValue(ad, out var b) && b.Sure.TryGetValue(anim, out var s) ? s : 1f;
}

public enum DusmanDurum { Dogus, Yuru, Saldiri, Olu }

public class DusmanTuru
{
	public string Ad, Sprite;
	public float Can, Hiz, Hasar, Olcek, SaldiriAralik;
	public int Altin;
	public bool Uzak;

	public static readonly Dictionary<string, DusmanTuru> Liste = new()
	{
		["iskelet"] = new DusmanTuru { Ad = "İskelet", Sprite = "iskelet", Can = 30, Hiz = 55, Hasar = 5, Olcek = 0.66f, Altin = 4, SaldiriAralik = 1.3f },
		["haydut"] = new DusmanTuru { Ad = "Haydut", Sprite = "haydut", Can = 22, Hiz = 115, Hasar = 4, Olcek = 0.64f, Altin = 5, SaldiriAralik = 1.0f },
		["savasci"] = new DusmanTuru { Ad = "Savaşçı", Sprite = "savasci", Can = 95, Hiz = 40, Hasar = 11, Olcek = 0.72f, Altin = 9, SaldiriAralik = 1.6f },
		["kara_buyucu"] = new DusmanTuru { Ad = "Kara Büyücü", Sprite = "kara_buyucu", Can = 40, Hiz = 48, Hasar = 8, Olcek = 0.66f, Altin = 8, SaldiriAralik = 2.4f, Uzak = true },
		["dev"] = new DusmanTuru { Ad = "Dev İskelet", Sprite = "savasci", Can = 650, Hiz = 30, Hasar = 28, Olcek = 1.25f, Altin = 120, SaldiriAralik = 2.0f },
	};
}

public class Dusman
{
	public DusmanTuru Tur;
	public Node2D Kok;
	public AnimatedSprite2D Spr;
	public ColorRect CanCubugu;
	public ColorRect CanArka;
	public DusmanDurum Durum = DusmanDurum.Dogus;
	public float Can, CanMax, Hiz, Hasar;
	public float Bekleme;       // sonraki saldırıya kalan
	public float Vurus = -1f;   // saldırı animasyonunda darbenin geleceği an
	public float DurmaX;        // nerede durup saldıracağı
	public float Sersem;
	public float Yanma, YanmaHasar;
	public CpuParticles2D Ates;
	public float OlumSure;
	public float Boy;
	public bool Dev;

	public Vector2 Konum => Kok.Position;
	public bool Canli => Durum != DusmanDurum.Olu;
	public Vector2 Merkez => Kok.Position + new Vector2(0, -Boy * 0.5f);
}

public class Kahraman
{
	public string Tur, Ad, BeceriAd;
	public int Kat;             // 0 = yerde (şövalye), 1..3 = kale katı
	public AnimatedSprite2D Spr;
	public Node2D Kok;
	public int Seviye = 1;
	public bool Acik;
	public float Bekleme;
	public float BeceriBekleme;
	public float BeceriSure;
	public float Aralik;
	public float Can, CanMax;
	public bool Olu;
	public Button Buton;
	public bool Mesgul;          // animasyon bitene dek idle'a dönmesin
}

public class Mermi
{
	public string Tur;           // ok, ates, balta, mor, meteor, yagmur
	public Node2D Dugum;
	public Vector2 Bas, Son;
	public float Sure, Gecen, Kavis;
	public float Hasar, Alan;
	public Dusman Hedef;
	public CpuParticles2D Iz;
	public bool Donen;
}
