using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Mezarlık haritası. Her bölüm başında yol ve kule yerleri rastgele üretilir.
/// Zemin bir kez çizilir (performans); portal ve sis ayrı küçük bir düğümde canlanır.
/// </summary>
public partial class TdHarita : Node2D
{
	public static Vector2[] Yol = { new(-60, 360), new(1340, 360) };
	public static readonly List<Vector2> Yerler = new();
	public const float YolGenislik = 54f;
	public static float Sol = 0, Sag = 1280;
	public static float Uzunluk { get; private set; }
	static readonly List<float> Birikim = new();

	readonly List<(Vector2 p, float r, Color c)> _benekler = new();
	readonly List<(Vector2 p, int tur)> _susler = new();
	HaritaCanli _canli;

	// ------------------------------------------------------------ üretim
	/// <summary>Görünen genişliğe (sol..sağ) yayılan, kıvrımlı rastgele bir yol ve kule yerleri üretir.</summary>
	public static void Olustur(ulong tohum, float sol, float sag)
	{
		Sol = sol; Sag = sag;
		var rng = new RandomNumberGenerator { Seed = tohum };
		var noktalar = new List<Vector2>();
		int donus = rng.RandiRange(3, 5);
		float y = rng.RandfRange(190, 560);
		noktalar.Add(new Vector2(sol - 60, y));
		for (int i = 1; i <= donus; i++)
		{
			float x = sol + (sag - sol) * i / (donus + 1f) + rng.RandfRange(-40, 40);
			noktalar.Add(new Vector2(x, y));
			float yeniY;
			int deneme = 0;
			do { yeniY = rng.RandfRange(165, i == donus ? 560 : 640); deneme++; }
			while (Mathf.Abs(yeniY - y) < 160 && deneme < 30);
			y = yeniY;
			noktalar.Add(new Vector2(x, y));
		}
		noktalar.Add(new Vector2(sag + 60, y));
		Yol = noktalar.ToArray();

		Birikim.Clear();
		Birikim.Add(0);
		for (int i = 1; i < Yol.Length; i++) Birikim.Add(Birikim[i - 1] + Yol[i - 1].DistanceTo(Yol[i]));
		Uzunluk = Birikim[^1];

		// kule yerleri: yola 62-125 px uzaklıkta, birbirinden en az 78 px aralıklı
		var adaylar = new List<Vector2>();
		for (float x = sol + 45; x < sag - 40; x += 26)
			for (float yy = 135; yy < 695; yy += 26)
			{
				var p = new Vector2(x, yy);
				if (x > sag - 250 && yy > 610) continue;      // dalga düğmesinin altı
				float u = YolaUzaklik(p);
				if (u >= 64 && u <= 125) adaylar.Add(p);
			}
		// yola yakın olanlar önce (daha kullanışlı), biraz rastgelelikle
		var anahtar = new Dictionary<Vector2, float>();
		foreach (var p in adaylar) anahtar[p] = YolaUzaklik(p) + rng.RandfRange(0, 45);
		adaylar.Sort((a, b) => anahtar[a].CompareTo(anahtar[b]));
		Yerler.Clear();
		foreach (var p in adaylar)
		{
			bool yakin = false;
			foreach (var q in Yerler) if (q.DistanceTo(p) < 78) { yakin = true; break; }
			if (!yakin) Yerler.Add(p);
			if (Yerler.Count >= 20) break;
		}
	}

	public static Vector2 Konum(float m, out Vector2 yon)
	{
		m = Mathf.Clamp(m, 0, Uzunluk);
		for (int i = 1; i < Yol.Length; i++)
		{
			if (m <= Birikim[i])
			{
				yon = (Yol[i] - Yol[i - 1]).Normalized();
				return Yol[i - 1] + yon * (m - Birikim[i - 1]);
			}
		}
		yon = Vector2.Right;
		return Yol[^1];
	}

	public static float YolaUzaklik(Vector2 p)
	{
		float en = float.MaxValue;
		for (int i = 1; i < Yol.Length; i++)
		{
			var a = Yol[i - 1]; var b = Yol[i];
			var t = Mathf.Clamp((p - a).Dot(b - a) / (b - a).LengthSquared(), 0, 1);
			en = Mathf.Min(en, p.DistanceTo(a + (b - a) * t));
		}
		return en;
	}

	/// <summary>Bitişe yakın görünen nokta (kaybedilen can yazısı için).</summary>
	public static Vector2 Cikis => new(Sag - 40, Yol[^1].Y - 40);

	// ------------------------------------------------------------ çizim
	public override void _Ready()
	{
		_canli = new HaritaCanli();
		AddChild(_canli);
		Yenile(1);
	}

	/// <summary>Süsleri yeni yola göre yerleştirip zemini bir kez yeniden çizer.</summary>
	public void Yenile(ulong tohum)
	{
		var rng = new RandomNumberGenerator { Seed = tohum * 31 + 7 };
		_benekler.Clear(); _susler.Clear();
		for (int i = 0; i < 300; i++)
		{
			var p = new Vector2(rng.RandfRange(Sol, Sag), rng.RandfRange(56, 720));
			if (YolaUzaklik(p) < YolGenislik * 0.6f) continue;
			_benekler.Add((p, rng.RandfRange(2, 7), rng.Randf() < 0.5f ? new Color(0.13f, 0.17f, 0.11f) : new Color(0.09f, 0.12f, 0.09f)));
		}
		for (int i = 0; i < 60; i++)
		{
			var p = new Vector2(rng.RandfRange(Sol + 20, Sag - 20), rng.RandfRange(90, 705));
			if (YolaUzaklik(p) < YolGenislik * 0.5f + 20f) continue;
			bool yakin = false;
			foreach (var y in Yerler) if (y.DistanceTo(p) < 50) yakin = true;
			if (!yakin) _susler.Add((p, rng.RandiRange(0, 3)));
		}
		QueueRedraw();
		_canli?.QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Sol - 20, 0, Sag - Sol + 40, 740), new Color(0.11f, 0.13f, 0.1f));
		foreach (var (p, r, c) in _benekler) DrawCircle(p, r, c);
		var kenar = new Color(0.12f, 0.09f, 0.07f);
		var toprak = new Color(0.3f, 0.23f, 0.17f);
		for (int i = 1; i < Yol.Length; i++)
		{
			DrawLine(Yol[i - 1], Yol[i], kenar, YolGenislik + 10);
			DrawCircle(Yol[i - 1], (YolGenislik + 10) / 2, kenar);
		}
		for (int i = 1; i < Yol.Length; i++)
		{
			DrawLine(Yol[i - 1], Yol[i], toprak, YolGenislik);
			DrawCircle(Yol[i - 1], YolGenislik / 2, toprak);
		}
		for (int i = 1; i < Yol.Length; i++)
			DrawDashedLine(Yol[i - 1], Yol[i], new Color(0.24f, 0.18f, 0.13f), 3, 14);
		foreach (var (p, tur) in _susler)
		{
			switch (tur)
			{
				case 0:
					DrawRect(new Rect2(p.X - 9, p.Y - 20, 18, 22), new Color(0.32f, 0.31f, 0.38f));
					DrawCircle(new Vector2(p.X, p.Y - 20), 9, new Color(0.32f, 0.31f, 0.38f));
					DrawRect(new Rect2(p.X - 5, p.Y - 16, 10, 2), new Color(0.2f, 0.19f, 0.25f));
					break;
				case 1:
					DrawRect(new Rect2(p.X - 2, p.Y - 26, 4, 28), new Color(0.3f, 0.29f, 0.35f));
					DrawRect(new Rect2(p.X - 9, p.Y - 20, 18, 4), new Color(0.3f, 0.29f, 0.35f));
					break;
				case 2:
					DrawLine(p, p + new Vector2(0, -38), new Color(0.06f, 0.05f, 0.07f), 4);
					DrawLine(p + new Vector2(0, -24), p + new Vector2(-14, -36), new Color(0.06f, 0.05f, 0.07f), 2);
					DrawLine(p + new Vector2(0, -30), p + new Vector2(12, -44), new Color(0.06f, 0.05f, 0.07f), 2);
					break;
				default:
					DrawLine(p + new Vector2(-7, 0), p + new Vector2(7, -3), new Color(0.75f, 0.72f, 0.62f), 3);
					DrawCircle(p + new Vector2(-8, 1), 2.5f, new Color(0.75f, 0.72f, 0.62f));
					DrawCircle(p + new Vector2(8, -3), 2.5f, new Color(0.75f, 0.72f, 0.62f));
					break;
			}
		}
		// çıkış kapısı
		var c = Cikis + new Vector2(0, 40);
		DrawRect(new Rect2(c.X - 12, c.Y - 50, 52, 100), new Color(0.2f, 0.16f, 0.12f));
		DrawRect(new Rect2(c.X - 4, c.Y - 38, 44, 88), new Color(0.07f, 0.05f, 0.05f));
	}
}

/// <summary>Haritanın canlı kısmı: giriş portalı, kapı meşalesi, sis. Saniyede 20 kez çizilir.</summary>
public partial class HaritaCanli : Node2D
{
	float _t, _sayac;
	public override void _Process(double delta)
	{
		_t += (float)delta;
		_sayac -= (float)delta;
		if (_sayac <= 0) { _sayac = 0.05f; QueueRedraw(); }
	}

	public override void _Draw()
	{
		float n = 0.5f + 0.5f * Mathf.Sin(_t * 3);
		var giris = new Vector2(TdHarita.Sol + 18, TdHarita.Yol[0].Y);
		DrawCircle(giris, 34, new Color(0.25f, 0.9f, 0.3f, 0.12f + 0.08f * n));
		DrawCircle(giris, 22, new Color(0.15f, 0.6f, 0.2f, 0.25f));
		var c = TdHarita.Cikis + new Vector2(0, 40);
		DrawCircle(new Vector2(c.X - 8, c.Y - 50), 8, new Color(1f, 0.6f, 0.2f, 0.5f + 0.3f * n));
		float gen = TdHarita.Sag - TdHarita.Sol;
		for (int i = 0; i < 6; i++)
		{
			float x = TdHarita.Sol + Mathf.PosMod(_t * (12 + i * 5) + i * 230, gen + 220) - 110;
			DrawRect(new Rect2(x, 120 + i * 105, 160, 10), new Color(0.7f, 0.9f, 0.75f, 0.04f));
		}
	}
}
