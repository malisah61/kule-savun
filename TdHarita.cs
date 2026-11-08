using Godot;
using System;
using System.Collections.Generic;
using static TdHarita;

/// <summary>
/// Mezarlık haritası. Her bölüm başında uzun, ekranı kat kat dolaşan rastgele bir yol,
/// kule yerleri ve süsler üretilir. Durağan zemin bir SubViewport'a TEK KEZ çizilip
/// tek bir doku olarak gösterilir (telefonda akıcılık için); fener, mum, portal ve sis ayrı canlanır.
/// </summary>
public partial class TdHarita : Node2D
{
	public static Vector2[] Yol = { new(-60, 360), new(1340, 360) };
	public static readonly List<Vector2> Yerler = new();
	public static readonly List<Vector2> Fenerler = new();
	public static readonly List<Vector2> Mumlar = new();
	public const float YolGenislik = 50f;
	public static float Sol = 0, Sag = 1280;
	public static float Uzunluk { get; private set; }
	static readonly List<float> Birikim = new();

	// süsler: tür, konum, boyut, tohum
	public enum Sus { OluAgac, Cam, Mezar, Hac, Dikili, KirikMezar, Turbe, Cit, Kaya, Cali, Kafatasi, Fener, Mantar }
	public static readonly List<(Sus tur, Vector2 p, float s, int t)> Susler = new();
	public static readonly List<(Vector2 p, float r, Color c)> Lekeler = new();

	SubViewport _vp;
	HaritaCizim _cizim;
	Sprite2D _goruntu;
	HaritaCanli _canli;

	// ================================================================ YOL ÜRETİMİ
	public static void Olustur(ulong tohum, float sol, float sag)
	{
		Sol = sol; Sag = sag;
		var rng = new RandomNumberGenerator { Seed = tohum };
		var n = new List<Vector2>();
		int stil = rng.RandiRange(0, 2);
		const float ust = 160, alt = 655;
		if (stil == 1)
		{
			// dikey kıvrım: sütun sütun aşağı-yukarı
			int sutun = Mathf.Clamp(Mathf.RoundToInt((sag - sol - 240) / 210f) + 1, 5, 8);
			float y = rng.Randf() < 0.5f ? ust : alt;
			n.Add(new Vector2(sol - 60, y));
			for (int i = 0; i < sutun; i++)
			{
				float x = sol + 120 + (sag - sol - 240) * i / (sutun - 1f) + rng.RandfRange(-14, 14);
				n.Add(new Vector2(x, y));
				float hedef = y < 400 ? alt + rng.RandfRange(-20, 0) : ust + rng.RandfRange(0, 25);
				// uzun dikey kollara hafif dalga
				n.Add(new Vector2(x + rng.RandfRange(-25, 25), (y + hedef) / 2));
				y = hedef;
				n.Add(new Vector2(x, y));
			}
			n.Add(new Vector2(sag + 60, y));
		}
		else
		{
			// yatay kıvrım: 3-4 şerit, sağa-sola dönerek
			int serit = rng.RandiRange(3, 4);
			float sol2 = sol + 110, sag2 = sag - 110;
			bool sagaGit = true;
			float y0 = ust + rng.RandfRange(0, 15);
			n.Add(new Vector2(sol - 60, y0));
			for (int i = 0; i < serit; i++)
			{
				float y = ust + (alt - ust) * i / (serit - 1f) + rng.RandfRange(-12, 12);
				float bas = sagaGit ? sol2 : sag2, son = sagaGit ? sag2 : sol2;
				if (i == 0) bas = sol - 60;
				// şerit boyunca dalgalı ara noktalar (stil 2 daha dalgalı)
				int ara = stil == 2 ? 6 : 3;
				float genlik = stil == 2 ? 38 : 14;
				for (int k = 0; k <= ara; k++)
				{
					float t = k / (float)ara;
					float x = Mathf.Lerp(bas, son, t) + (k > 0 && k < ara ? rng.RandfRange(-30, 30) : 0);
					float dy = (k > 0 && k < ara) ? Mathf.Sin(t * Mathf.Pi * (stil == 2 ? 3 : 1) + i) * genlik : 0;
					n.Add(new Vector2(i == 0 && k == 0 ? sol - 60 : x, y + dy));
				}
				sagaGit = !sagaGit;
			}
			var son2 = n[^1];
			n.Add(new Vector2(sagaGit ? sol - 60 : sag + 60, son2.Y));
		}
		// Chaikin köşe kesme: dönemeçleri yuvarlatır
		for (int k = 0; k < 4; k++) n = Yumusat(n);
		Yol = n.ToArray();

		Birikim.Clear();
		Birikim.Add(0);
		for (int i = 1; i < Yol.Length; i++) Birikim.Add(Birikim[i - 1] + Yol[i - 1].DistanceTo(Yol[i]));
		Uzunluk = Birikim[^1];
		_cikis = UcBul(true); _giris = UcBul(false);

		// kule yerleri: yola 60-120 px, birbirinden en az 76 px
		var adaylar = new List<Vector2>();
		var anahtar = new Dictionary<Vector2, float>();
		for (float x = sol + 45; x < sag - 40; x += 24)
			for (float yy = 125; yy < 700; yy += 24)
			{
				var p = new Vector2(x, yy);
				if (x > sag - 250 && yy > 610) continue;      // dalga düğmesinin altı
				float u = YolaUzaklik(p);
				if (u < 60 || u > 120) continue;
				adaylar.Add(p);
				anahtar[p] = u + rng.RandfRange(0, 40);
			}
		adaylar.Sort((a, b) => anahtar[a].CompareTo(anahtar[b]));
		Yerler.Clear();
		foreach (var p in adaylar)
		{
			bool yakin = false;
			foreach (var q in Yerler) if (q.DistanceTo(p) < 76) { yakin = true; break; }
			if (!yakin) Yerler.Add(p);
			if (Yerler.Count >= 26) break;
		}
		SusleriYerlestir(rng);
	}

	static List<Vector2> Yumusat(List<Vector2> p)
	{
		var y = new List<Vector2> { p[0] };
		for (int i = 0; i < p.Count - 1; i++)
		{
			y.Add(p[i].Lerp(p[i + 1], 0.25f));
			y.Add(p[i].Lerp(p[i + 1], 0.75f));
		}
		y.Add(p[^1]);
		return y;
	}

	static void SusleriYerlestir(RandomNumberGenerator rng)
	{
		Susler.Clear(); Lekeler.Clear(); Fenerler.Clear(); Mumlar.Clear();
		var dolu = new List<(Vector2 p, float r)>();
		foreach (var y in Yerler) dolu.Add((y, 38));
		bool Bos(Vector2 p, float r, float yolPayi)
		{
			if (p.X < Sol + 8 || p.X > Sag - 8 || p.Y < 70 || p.Y > 712) return false;
			if (YolaUzaklik(p) < YolGenislik * 0.5f + yolPayi) return false;
			foreach (var (q, qr) in dolu) if (q.DistanceTo(p) < r + qr) return false;
			return true;
		}
		void Ekle(Sus tur, Vector2 p, float s, float r) { Susler.Add((tur, p, s, rng.RandiRange(0, 9999))); dolu.Add((p, r)); }

		// zemin lekeleri (yosun, toprak)
		for (int i = 0; i < 70; i++)
		{
			var p = new Vector2(rng.RandfRange(Sol, Sag), rng.RandfRange(60, 720));
			if (YolaUzaklik(p) < YolGenislik) continue;
			float r = rng.RandfRange(14, 46);
			var c = rng.Randf() < 0.6f ? new Color(0.14f, 0.19f, 0.12f, 0.55f) : new Color(0.17f, 0.14f, 0.11f, 0.5f);
			Lekeler.Add((p, r, c));
		}
		// türbeler (büyük boşluklara)
		for (int i = 0; i < 40 && Susler.Count < 3; i++)
		{
			var p = new Vector2(rng.RandfRange(Sol + 60, Sag - 60), rng.RandfRange(120, 690));
			if (Bos(p, 52, 50)) Ekle(Sus.Turbe, p, rng.RandfRange(0.9f, 1.2f), 52);
		}
		// mezar kümeleri (çit + taşlar + fener)
		for (int i = 0; i < 60; i++)
		{
			var m = new Vector2(rng.RandfRange(Sol + 40, Sag - 40), rng.RandfRange(110, 700));
			if (!Bos(m, 30, 20)) continue;
			int adet = rng.RandiRange(2, 5);
			for (int j = 0; j < adet; j++)
			{
				var p = m + new Vector2(j * 26 - adet * 13 + rng.RandfRange(-4, 4), rng.RandfRange(-6, 6));
				if (!Bos(p, 13, 12)) continue;
				var tur = (Sus)(int)Sus.Mezar + rng.RandiRange(0, 3);
				Ekle(tur, p, rng.RandfRange(0.8f, 1.15f), 13);
				if (rng.Randf() < 0.18f) Mumlar.Add(p + new Vector2(rng.RandfRange(-8, 8), 4));
			}
			if (rng.Randf() < 0.35f && Bos(m + new Vector2(0, 26), 14, 10)) Ekle(Sus.Cit, m + new Vector2(0, 26), adet * 26 + 20, 14);
		}
		// fenerler
		for (int i = 0; i < 40 && Fenerler.Count < 7; i++)
		{
			var p = new Vector2(rng.RandfRange(Sol + 30, Sag - 30), rng.RandfRange(110, 700));
			if (YolaUzaklik(p) > 90 || !Bos(p, 14, 14)) continue;
			Ekle(Sus.Fener, p, 1f, 14);
			Fenerler.Add(p + new Vector2(0, -32));
		}
		// ağaçlar, kayalar, çalılar, kafatası, mantar ile doldur
		for (int i = 0; i < 700; i++)
		{
			var p = new Vector2(rng.RandfRange(Sol + 6, Sag - 6), rng.RandfRange(80, 712));
			float z = rng.Randf();
			Sus tur; float r, s;
			if (z < 0.22f) { tur = Sus.OluAgac; s = rng.RandfRange(0.8f, 1.4f); r = 22 * s; }
			else if (z < 0.36f) { tur = Sus.Cam; s = rng.RandfRange(0.8f, 1.3f); r = 18 * s; }
			else if (z < 0.55f) { tur = Sus.Kaya; s = rng.RandfRange(0.6f, 1.4f); r = 12 * s; }
			else if (z < 0.78f) { tur = Sus.Cali; s = rng.RandfRange(0.7f, 1.3f); r = 12 * s; }
			else if (z < 0.88f) { tur = Sus.Kafatasi; s = rng.RandfRange(0.8f, 1.2f); r = 9; }
			else { tur = Sus.Mantar; s = rng.RandfRange(0.8f, 1.2f); r = 8; }
			if (Bos(p, r, 8)) Ekle(tur, p, s, r);
		}
		Susler.Sort((a, b) => a.p.Y.CompareTo(b.p.Y));
	}

	public static Vector2 Konum(float m, out Vector2 yon)
	{
		m = Mathf.Clamp(m, 0, Uzunluk);
		int lo = 1, hi = Yol.Length - 1;
		while (lo < hi) { int mid = (lo + hi) / 2; if (Birikim[mid] < m) lo = mid + 1; else hi = mid; }
		yon = (Yol[lo] - Yol[lo - 1]).Normalized();
		return Yol[lo - 1] + yon * (m - Birikim[lo - 1]);
	}

	public static float YolaUzaklik(Vector2 p)
	{
		float en = float.MaxValue;
		for (int i = 1; i < Yol.Length; i++)
		{
			var a = Yol[i - 1]; var b = Yol[i];
			var t = Mathf.Clamp((p - a).Dot(b - a) / (b - a).LengthSquared(), 0, 1);
			en = Mathf.Min(en, p.DistanceSquaredTo(a + (b - a) * t));
		}
		return Mathf.Sqrt(en);
	}

	static Vector2 _cikis, _giris;
	/// <summary>Yolun ekrandaki son noktası (kapı ve kaybedilen can yazısı için).</summary>
	public static Vector2 Cikis => _cikis;
	public static Vector2 Giris => _giris;

	static Vector2 UcBul(bool son)
	{
		for (float m = son ? Uzunluk : 0; son ? m > 0 : m < Uzunluk; m += son ? -10 : 10)
		{
			var p = Konum(m, out _);
			if (p.X > Sol + 30 && p.X < Sag - 30) return p;
		}
		return son ? Yol[^1] : Yol[0];
	}

	// ================================================================ ÇİZİM
	public override void _Ready()
	{
		_vp = new SubViewport { TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Once, Disable3D = true };
		AddChild(_vp);
		_cizim = new HaritaCizim();
		_vp.AddChild(_cizim);
		_goruntu = new Sprite2D { Centered = false };
		AddChild(_goruntu);
		_canli = new HaritaCanli();
		AddChild(_canli);
		Yenile(1);
	}

	/// <summary>Zemini yeni yola göre bir kez çizer.</summary>
	public void Yenile(ulong tohum)
	{
		if (_vp == null) return;
		_vp.Size = new Vector2I(Mathf.CeilToInt(Sag - Sol), 720);
		_cizim.Position = new Vector2(-Sol, 0);
		_cizim.QueueRedraw();
		_vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		_goruntu.Texture = _vp.GetTexture();
		_goruntu.Position = new Vector2(Sol, 0);
	}

}

	/// <summary>SubViewport içinde bir kez çizilen durağan harita.</summary>
public partial class HaritaCizim : Node2D
	{
		static readonly Color Koyu = new(0.05f, 0.04f, 0.06f);
		public override void _Draw()
		{
			DrawRect(new Rect2(Sol - 20, 0, Sag - Sol + 40, 740), new Color(0.1f, 0.12f, 0.09f));
			foreach (var (lp, lr, lc) in Lekeler) DrawCircle(lp, lr, lc);
			var rng = new RandomNumberGenerator { Seed = 5 };
			for (int i = 0; i < 500; i++)
			{
				var p = new Vector2(rng.RandfRange(Sol, Sag), rng.RandfRange(56, 720));
				DrawLine(p, p + new Vector2(rng.RandfRange(-2, 2), -rng.RandfRange(3, 7)), new Color(0.16f, 0.22f, 0.13f), 1.5f);
			}
			// yol: kenar, toprak, taşlar, iz
			var kenar = new Color(0.1f, 0.08f, 0.06f);
			var toprak = new Color(0.29f, 0.22f, 0.16f);
			for (int i = 1; i < Yol.Length; i++) { DrawLine(Yol[i - 1], Yol[i], kenar, YolGenislik + 12); DrawCircle(Yol[i - 1], (YolGenislik + 12) / 2, kenar); }
			for (int i = 1; i < Yol.Length; i++) { DrawLine(Yol[i - 1], Yol[i], toprak, YolGenislik); DrawCircle(Yol[i - 1], YolGenislik / 2, toprak); }
			for (float m = 0; m < Uzunluk; m += 17)
			{
				var p = Konum(m, out var yon);
				var dik = new Vector2(-yon.Y, yon.X);
				float h = Mathf.Sin(m * 0.37f) * 0.5f + 0.5f;
				DrawCircle(p + dik * (YolGenislik * 0.5f + 1) , 2.5f + h * 1.5f, new Color(0.36f, 0.33f, 0.3f));
				DrawCircle(p - dik * (YolGenislik * 0.5f + 1), 2.5f + (1 - h) * 1.5f, new Color(0.33f, 0.3f, 0.28f));
				if ((int)(m / 17) % 3 == 0) DrawCircle(p + dik * Mathf.Sin(m) * 12, 1.6f, new Color(0.22f, 0.17f, 0.12f));
			}
			for (float m = 0; m < Uzunluk; m += 30)
				DrawLine(Konum(m, out _), Konum(m + 12, out _), new Color(0.24f, 0.18f, 0.13f), 3);
			foreach (var (tur, p, s, t) in Susler) Ciz(tur, p, s, t);
			// çıkış kapısı
			var kapi = Cikis;
			DrawRect(new Rect2(kapi.X - 26, kapi.Y - 56, 52, 56), new Color(0.22f, 0.18f, 0.14f));
			DrawRect(new Rect2(kapi.X - 18, kapi.Y - 46, 36, 46), new Color(0.06f, 0.04f, 0.04f));
			DrawRect(new Rect2(kapi.X - 30, kapi.Y - 62, 60, 8), new Color(0.3f, 0.26f, 0.22f));
			// kenar karartma
			for (int i = 0; i < 6; i++)
			{
				var k = new Color(0, 0, 0, 0.07f);
				DrawRect(new Rect2(Sol, 56, 14 + i * 12, 664), k);
				DrawRect(new Rect2(Sag - 14 - i * 12, 56, 14 + i * 12, 664), k);
				DrawRect(new Rect2(Sol, 720 - 10 - i * 9, Sag - Sol, 10 + i * 9), k);
			}
		}

		void Ciz(Sus tur, Vector2 p, float s, int t)
		{
			var rng = new RandomNumberGenerator { Seed = (ulong)t };
			var tas = new Color(0.33f, 0.32f, 0.38f);
			var tasK = new Color(0.22f, 0.21f, 0.27f);
			var tasA = new Color(0.45f, 0.44f, 0.5f);
			DrawCircle(p + new Vector2(2, 2), 10 * s, new Color(0, 0, 0, 0.25f));
			switch (tur)
			{
				case Sus.OluAgac:
					Dal(p, -Mathf.Pi / 2 + rng.RandfRange(-0.15f, 0.15f), 34 * s, 5.5f * s, 4, rng);
					break;
				case Sus.Cam:
					DrawRect(new Rect2(p.X - 2 * s, p.Y - 8 * s, 4 * s, 8 * s), new Color(0.15f, 0.1f, 0.07f));
					for (int k = 0; k < 3; k++)
					{
						float w = (16 - k * 4) * s, y = p.Y - 6 * s - k * 11 * s;
						var c = k % 2 == 0 ? new Color(0.08f, 0.16f, 0.13f) : new Color(0.1f, 0.19f, 0.15f);
						DrawColoredPolygon(new[] { new Vector2(p.X - w, y), new Vector2(p.X + w, y), new Vector2(p.X, y - 18 * s) }, c);
					}
					break;
				case Sus.Mezar:
					DrawRect(new Rect2(p.X - 8 * s, p.Y - 18 * s, 16 * s, 18 * s), tas);
					DrawCircle(new Vector2(p.X, p.Y - 18 * s), 8 * s, tas);
					DrawRect(new Rect2(p.X + 4 * s, p.Y - 20 * s, 4 * s, 20 * s), tasK);
					DrawRect(new Rect2(p.X - 5 * s, p.Y - 14 * s, 8 * s, 1.5f * s), tasK);
					DrawRect(new Rect2(p.X - 5 * s, p.Y - 10 * s, 6 * s, 1.5f * s), tasK);
					DrawRect(new Rect2(p.X - 9 * s, p.Y - 2, 18 * s, 3), new Color(0.12f, 0.17f, 0.1f));
					break;
				case Sus.Hac:
					DrawRect(new Rect2(p.X - 2 * s, p.Y - 26 * s, 4 * s, 26 * s), tas);
					DrawRect(new Rect2(p.X - 9 * s, p.Y - 20 * s, 18 * s, 4 * s), tas);
					DrawRect(new Rect2(p.X + 1 * s, p.Y - 26 * s, 1.5f * s, 26 * s), tasK);
					break;
				case Sus.Dikili:
					DrawColoredPolygon(new[] { new Vector2(p.X - 6 * s, p.Y), new Vector2(p.X + 6 * s, p.Y), new Vector2(p.X + 4 * s, p.Y - 28 * s), new Vector2(p.X, p.Y - 34 * s), new Vector2(p.X - 4 * s, p.Y - 28 * s) }, tas);
					DrawColoredPolygon(new[] { new Vector2(p.X + 1 * s, p.Y), new Vector2(p.X + 6 * s, p.Y), new Vector2(p.X + 4 * s, p.Y - 28 * s), new Vector2(p.X, p.Y - 34 * s) }, tasK);
					break;
				case Sus.KirikMezar:
					DrawColoredPolygon(new[] { new Vector2(p.X - 8 * s, p.Y), new Vector2(p.X + 8 * s, p.Y), new Vector2(p.X + 8 * s, p.Y - 10 * s), new Vector2(p.X + 2 * s, p.Y - 16 * s), new Vector2(p.X - 3 * s, p.Y - 11 * s), new Vector2(p.X - 8 * s, p.Y - 15 * s) }, tas);
					DrawColoredPolygon(new[] { new Vector2(p.X + 10 * s, p.Y), new Vector2(p.X + 18 * s, p.Y - 2 * s), new Vector2(p.X + 15 * s, p.Y - 7 * s) }, tasK);
					break;
				case Sus.Turbe:
					float w2 = 34 * s, h2 = 34 * s;
					DrawRect(new Rect2(p.X - w2, p.Y - h2, w2 * 2, h2), tas);
					DrawRect(new Rect2(p.X + w2 * 0.55f, p.Y - h2, w2 * 0.45f, h2), tasK);
					DrawColoredPolygon(new[] { new Vector2(p.X - w2 - 6, p.Y - h2), new Vector2(p.X + w2 + 6, p.Y - h2), new Vector2(p.X, p.Y - h2 - 26 * s) }, tasK);
					DrawRect(new Rect2(p.X - 9 * s, p.Y - 22 * s, 18 * s, 22 * s), Koyu);
					DrawCircle(new Vector2(p.X, p.Y - 22 * s), 9 * s, Koyu);
					foreach (float cx in new[] { -w2 + 5, w2 - 9 }) DrawRect(new Rect2(p.X + cx, p.Y - h2, 4 * s, h2), tasA);
					DrawRect(new Rect2(p.X - 1.5f, p.Y - h2 - 40 * s, 3, 14 * s), tasA);
					DrawRect(new Rect2(p.X - 6 * s, p.Y - h2 - 35 * s, 12 * s, 3), tasA);
					break;
				case Sus.Cit:
					float gen = s;
					var demir = new Color(0.07f, 0.06f, 0.08f);
					DrawLine(p + new Vector2(-gen / 2, -14), p + new Vector2(gen / 2, -14), demir, 2);
					DrawLine(p + new Vector2(-gen / 2, -5), p + new Vector2(gen / 2, -5), demir, 2);
					for (float x = -gen / 2; x <= gen / 2; x += 7)
					{
						DrawLine(p + new Vector2(x, 0), p + new Vector2(x, -20), demir, 2);
						DrawColoredPolygon(new[] { p + new Vector2(x - 2, -20), p + new Vector2(x + 2, -20), p + new Vector2(x, -25) }, demir);
					}
					break;
				case Sus.Kaya:
					var kn = new Vector2[7];
					for (int k = 0; k < 7; k++) { float a = k / 7f * Mathf.Tau; kn[k] = p + new Vector2(Mathf.Cos(a) * 11 * s * rng.RandfRange(0.7f, 1.1f), Mathf.Sin(a) * 7 * s * rng.RandfRange(0.7f, 1.1f) - 4 * s); }
					DrawColoredPolygon(kn, new Color(0.27f, 0.27f, 0.29f));
					DrawCircle(p + new Vector2(-3 * s, -7 * s), 3 * s, new Color(0.36f, 0.36f, 0.38f));
					break;
				case Sus.Cali:
					for (int k = 0; k < 5; k++)
						DrawCircle(p + new Vector2(rng.RandfRange(-9, 9) * s, rng.RandfRange(-10, 0) * s), rng.RandfRange(4, 7) * s,
							k % 2 == 0 ? new Color(0.09f, 0.15f, 0.09f) : new Color(0.12f, 0.19f, 0.11f));
					break;
				case Sus.Kafatasi:
					var kemik = new Color(0.78f, 0.75f, 0.65f);
					for (int k = 0; k < rng.RandiRange(1, 3); k++)
					{
						var q = p + new Vector2(k * 7 - 6, -k * 2);
						DrawCircle(q, 4.5f * s, kemik);
						DrawCircle(q + new Vector2(-1.6f, -0.5f), 1.1f, Koyu);
						DrawCircle(q + new Vector2(1.6f, -0.5f), 1.1f, Koyu);
					}
					DrawLine(p + new Vector2(-9, 4), p + new Vector2(8, 2), kemik, 2);
					break;
				case Sus.Mantar:
					for (int k = 0; k < 3; k++)
					{
						var q = p + new Vector2(k * 6 - 6, rng.RandfRange(-2, 2));
						DrawRect(new Rect2(q.X - 1, q.Y - 5, 2, 5), new Color(0.75f, 0.72f, 0.62f));
						DrawCircle(q + new Vector2(0, -6), 3.5f, k == 1 ? new Color(0.55f, 0.12f, 0.16f) : new Color(0.4f, 0.55f, 0.2f));
					}
					break;
				case Sus.Fener:
					DrawRect(new Rect2(p.X - 1.5f, p.Y - 30, 3, 30), new Color(0.08f, 0.07f, 0.09f));
					DrawRect(new Rect2(p.X - 5, p.Y - 38, 10, 9), new Color(0.12f, 0.1f, 0.1f));
					DrawRect(new Rect2(p.X - 3, p.Y - 36, 6, 5), new Color(1f, 0.7f, 0.3f));
					break;
			}
		}

		void Dal(Vector2 bas, float aci, float boy, float kalin, int derinlik, RandomNumberGenerator rng)
		{
			var son = bas + new Vector2(Mathf.Cos(aci), Mathf.Sin(aci)) * boy;
			DrawLine(bas, son, Koyu, kalin);
			if (derinlik <= 0 || kalin < 1) return;
			int kol = rng.RandiRange(2, 3);
			for (int i = 0; i < kol; i++)
				Dal(bas.Lerp(son, rng.RandfRange(0.55f, 1f)), aci + rng.RandfRange(-0.8f, 0.8f), boy * rng.RandfRange(0.5f, 0.72f), kalin * 0.62f, derinlik - 1, rng);
		}
	}

/// <summary>Haritanın canlı kısmı: portal, titreyen fener ve mum ışıkları, sis. Saniyede 20 kez çizilir.</summary>
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
		var giris = TdHarita.Giris;
		DrawCircle(giris, 40, new Color(0.25f, 0.9f, 0.3f, 0.1f + 0.08f * n));
		DrawCircle(giris, 26, new Color(0.15f, 0.6f, 0.2f, 0.22f));
		DrawArc(giris, 30 + 4 * n, _t * 2, _t * 2 + 4, 16, new Color(0.5f, 1f, 0.5f, 0.35f), 2);
		int i = 0;
		foreach (var f in TdHarita.Fenerler)
		{
			float tit = 0.75f + 0.25f * Mathf.Sin(_t * 7 + i * 2.1f) * Mathf.Sin(_t * 3.3f + i);
			DrawCircle(f, 26 * tit, new Color(1f, 0.6f, 0.2f, 0.08f));
			DrawCircle(f, 12 * tit, new Color(1f, 0.7f, 0.3f, 0.16f));
			i++;
		}
		foreach (var m in TdHarita.Mumlar)
		{
			float tit = 0.7f + 0.3f * Mathf.Sin(_t * 9 + m.X);
			DrawRect(new Rect2(m.X - 1, m.Y - 6, 2, 6), new Color(0.85f, 0.82f, 0.7f));
			DrawCircle(m + new Vector2(0, -8), 2 * tit, new Color(1f, 0.8f, 0.4f));
			DrawCircle(m + new Vector2(0, -8), 9 * tit, new Color(1f, 0.6f, 0.2f, 0.1f));
		}
		var c = TdHarita.Cikis;
		DrawCircle(c + new Vector2(0, -24), 16, new Color(0.8f, 0.15f, 0.1f, 0.12f + 0.08f * n));
		float gen = TdHarita.Sag - TdHarita.Sol;
		for (int k = 0; k < 7; k++)
		{
			float x = TdHarita.Sol + Mathf.PosMod(_t * (10 + k * 4) + k * 260, gen + 260) - 130;
			DrawRect(new Rect2(x, 110 + k * 88, 190, 12), new Color(0.7f, 0.9f, 0.75f, 0.035f));
			DrawRect(new Rect2(x + 30, 104 + k * 88, 120, 8), new Color(0.7f, 0.9f, 0.75f, 0.03f));
		}
	}
}
