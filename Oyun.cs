using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Kule Savunma: Grow Castle tarzı 2D kale savunma oyunu.</summary>
public partial class Oyun : Node2D
{
	// ------------------------------------------------------------ sabitler
	const float Yer = 630f;
	const float KaleX = 150f;
	const float KaleOlcek = 0.62f;
	const float U = 72f * KaleOlcek;          // kale modelinde 1 birim = U piksel
	const float KahramanOlcek = 0.62f;
	static readonly string[] KuleSirasi = { "okcu", "buyucu", "barbar" };

	// ------------------------------------------------------------ durum
	enum Evre { Hazirlik, Savas }
	Evre _evre = Evre.Hazirlik;
	int _altin = 60;
	int _dalga = 1;
	int _kaleSv = 1, _okSv = 1;
	float _kaleCan, _kaleCanMax;
	float _genislik = 1280f;

	readonly List<Dusman> _dusmanlar = new();
	readonly List<Mermi> _mermiler = new();
	readonly Dictionary<string, Kahraman> _kahraman = new();
	readonly List<(float t, Action a)> _zamanli = new();
	readonly Queue<string> _dogacak = new();
	float _dogmaSayac;
	float _bitisSayac = -1f;

	// dokunma
	bool _basili;
	Vector2 _parmak;
	float _atisSayac;

	// sahne
	Node2D _dunya;
	Node2D _kaleDugum;
	Arkaplan _arka;
	Camera2D _kamera;
	float _sarsinti;
	readonly RandomNumberGenerator _rng = new();
	Texture2D _okDoku, _baltaDoku;

	// arayüz
	Label _altinYazi, _dalgaYazi, _mesaj, _ipucu, _kaleYazi;
	ProgressBar _kaleBar;
	Button _savasButon, _hizButon;
	Panel _panel;
	VBoxContainer _gelistirmeKutu;
	HBoxContainer _beceriKutu;
	float _mesajSure;

	// ses
	readonly Dictionary<string, AudioStreamWav> _sesler = new();
	readonly List<AudioStreamPlayer> _sesOynaticilar = new();

	// ================================================================ BAŞLANGIÇ

	public override void _Ready()
	{
		_rng.Randomize();
		SpriteKutuphane.Yukle();
		_okDoku = GD.Load<Texture2D>("res://assets/kale/ok.png");
		_baltaDoku = GD.Load<Texture2D>("res://assets/kale/balta.png");
		KahramanlariTanimla();
		KayitYukle();
		SesleriKur();
		DunyaKur();
		ArayuzKur();
		KaleKur(false);
		HazirligaGec(false);
		Mesaj("Kuleyi iskeletlerden koru!", new Color(1f, 0.9f, 0.5f), 3f);

		foreach (var arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--onizleme="))
				_ = OnizlemeCek(arg.Substring("--onizleme=".Length));
	}

	void KahramanlariTanimla()
	{
		_kahraman["okcu"] = new Kahraman { Tur = "okcu", Ad = "Okçu", BeceriAd = "Ok Yağmuru", BeceriSure = 12f, Aralik = 1.0f, Acik = true };
		_kahraman["buyucu"] = new Kahraman { Tur = "buyucu", Ad = "Büyücü", BeceriAd = "Meteor", BeceriSure = 18f, Aralik = 2.2f };
		_kahraman["barbar"] = new Kahraman { Tur = "barbar", Ad = "Barbar", BeceriAd = "Deprem", BeceriSure = 15f, Aralik = 1.8f };
		_kahraman["sovalye"] = new Kahraman { Tur = "sovalye", Ad = "Şövalye", BeceriAd = "Kalkan Darbesi", BeceriSure = 10f, Aralik = 1.1f };
	}

	static int AcmaBedeli(string tur) => tur switch { "buyucu" => 150, "sovalye" => 220, "barbar" => 350, _ => 0 };
	static float TemelHasar(string tur) => tur switch { "okcu" => 14f, "buyucu" => 20f, "barbar" => 42f, "sovalye" => 24f, _ => 10f };
	static int TemelBedel(string tur) => tur switch { "okcu" => 40, "buyucu" => 70, "barbar" => 90, "sovalye" => 60, _ => 50 };

	float KahramanHasar(Kahraman k) => TemelHasar(k.Tur) * (1f + 0.35f * (k.Seviye - 1));
	int YukseltmeBedeli(Kahraman k) => (int)(TemelBedel(k.Tur) * Mathf.Pow(1.5f, k.Seviye - 1));
	float KaleCanMax() => 220f * (1f + 0.3f * (_kaleSv - 1));
	int KaleBedel() => (int)(40 * Mathf.Pow(1.5f, _kaleSv - 1));
	float OkHasar() => 9f * (1f + 0.3f * (_okSv - 1));
	int OkBedel() => (int)(30 * Mathf.Pow(1.45f, _okSv - 1));
	float SovalyeCanMax(Kahraman k) => 130f * (1f + 0.4f * (k.Seviye - 1));

	void DunyaKur()
	{
		_kamera = new Camera2D { AnchorMode = Camera2D.AnchorModeEnum.FixedTopLeft, Position = Vector2.Zero };
		AddChild(_kamera);
		_kamera.MakeCurrent();
		_arka = new Arkaplan { Yer = Yer, ZIndex = -10 };
		AddChild(_arka);
		_dunya = new Node2D();
		AddChild(_dunya);
		_kaleDugum = new Node2D();
		_dunya.AddChild(_kaleDugum);

		foreach (var k in _kahraman.Values)
		{
			k.Kok = new Node2D { ZIndex = k.Tur == "sovalye" ? 6 : 2 };
			k.Spr = SpriteKutuphane.Olustur(k.Tur, KahramanOlcek * (k.Tur == "sovalye" ? 1.1f : 1f), false);
			k.Kok.AddChild(k.Spr);
			k.Spr.Play("bekle");
			k.Spr.Frame = _rng.RandiRange(0, 5);
			_dunya.AddChild(k.Kok);
		}
		var sov = _kahraman["sovalye"];
		sov.Kok.Position = new Vector2(KaleX + 125f, Yer + 4f);
		sov.CanMax = sov.Can = SovalyeCanMax(sov);
	}

	// ================================================================ KALE (3D'den çizilmiş parçalar)

	Sprite2D KaleParca(string ad, Vector2 koken, float dunyaY, int z)
	{
		var s = new Sprite2D
		{
			Texture = GD.Load<Texture2D>($"res://assets/kale/{ad}.png"),
			Centered = false,
			Offset = -koken,
			Scale = Vector2.One * KaleOlcek,
			Position = new Vector2(KaleX, Yer - dunyaY * U),
			ZIndex = z
		};
		_kaleDugum.AddChild(s);
		return s;
	}

	int KuleKatSayisi() => KuleSirasi.Count(t => _kahraman[t].Acik);
	float KatY(int kat) => 2.0f + (kat - 1) * 1.8f;
	Vector2 KaleOnu => new Vector2(KaleX + 2.0f * U, Yer);
	Vector2 KaleOkcuNoktasi => new Vector2(KaleX - 4f, Yer - (KatY(Math.Max(1, KuleKatSayisi()) + 1) + 1.3f) * U);

	void KaleKur(bool animasyon)
	{
		foreach (var c in _kaleDugum.GetChildren())
			c.QueueFree();
		int n = Math.Max(1, KuleKatSayisi());
		KaleParca("zemin", new Vector2(158f, 162f), 0f, 0);
		int kat = 0;
		foreach (var t in KuleSirasi)
		{
			var k = _kahraman[t];
			k.Kok.Visible = k.Acik;
			if (!k.Acik) continue;
			kat++;
			k.Kat = kat;
			float y = KatY(kat);
			var parca = KaleParca("kat", new Vector2(151f, 140.4f), y, 0);
			KaleParca("korkuluk", new Vector2(151f, 140.4f), y, 3);
			k.Kok.Position = new Vector2(KaleX + 10f, Yer - (y + 0.2f) * U);
			if (animasyon && kat == n)
			{
				parca.Scale = new Vector2(KaleOlcek, 0.05f);
				parca.CreateTween().TweenProperty(parca, "scale", Vector2.One * KaleOlcek, 0.6f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				Efekt.Toz(_dunya, new Vector2(KaleX, Yer - y * U), 2.5f);
				Efekt.Toz(_dunya, new Vector2(KaleX + 60, Yer - y * U), 2f);
			}
		}
		var cati = KaleParca("cati", new Vector2(144f, 306f), KatY(n + 1), 1);
		if (animasyon)
		{
			cati.Position += new Vector2(0, -60);
			cati.CreateTween().TweenProperty(cati, "position:y", Yer - KatY(n + 1) * U, 0.6f)
				.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		}
		// Kapı meşaleleri
		foreach (var x in new[] { -1.15f, 0.35f })
		{
			var mesale = new ColorRect { Color = new Color(0.3f, 0.2f, 0.12f), Size = new Vector2(6, 20), Position = new Vector2(KaleX + x * U - 3, Yer - 1.25f * U), ZIndex = 1 };
			_kaleDugum.AddChild(mesale);
			var ates = Efekt.Ates(_kaleDugum, new Vector2(KaleX + x * U, Yer - 1.3f * U), 0.55f, 18);
			ates.ZIndex = 2;
		}
		var sov = _kahraman["sovalye"];
		sov.Kok.Visible = sov.Acik;
	}

	// ================================================================ AKIŞ

	void HazirligaGec(bool kaybetti)
	{
		_evre = Evre.Hazirlik;
		Engine.TimeScale = 1.0;
		_hizButon.Text = "Hız x1";
		_kaleCanMax = KaleCanMax();
		_kaleCan = _kaleCanMax;
		var sov = _kahraman["sovalye"];
		sov.CanMax = SovalyeCanMax(sov);
		sov.Can = sov.CanMax;
		if (sov.Olu)
		{
			sov.Olu = false;
			sov.Spr.Play("bekle");
		}
		foreach (var k in _kahraman.Values)
			k.BeceriBekleme = 0f;
		_savasButon.Visible = true;
		_panel.Visible = true;
		_beceriKutu.Visible = false;
		_ipucu.Visible = false;
		ArayuzGuncelle();
	}

	void SavasaBasla()
	{
		if (_evre == Evre.Savas) return;
		_evre = Evre.Savas;
		_savasButon.Visible = false;
		_panel.Visible = false;
		_beceriKutu.Visible = true;
		_ipucu.Visible = _dalga <= 3;
		_dogacak.Clear();
		foreach (var t in DalgaListesi(_dalga))
			_dogacak.Enqueue(t);
		_dogmaSayac = 0.5f;
		_bitisSayac = -1f;
		Oynat("boru", -6f);
		Mesaj($"Dalga {_dalga}", new Color(1f, 0.85f, 0.4f), 1.6f);
		ArayuzGuncelle();
	}

	List<string> DalgaListesi(int w)
	{
		var l = new List<string>();
		int adet = 5 + w * 2;
		for (int i = 0; i < adet; i++)
		{
			float r = _rng.Randf();
			if (w >= 6 && r < 0.12f) l.Add("kara_buyucu");
			else if (w >= 4 && r < 0.3f) l.Add("savasci");
			else if (w >= 2 && r < 0.48f) l.Add("haydut");
			else l.Add("iskelet");
		}
		if (w % 5 == 0) l.Add("dev");
		return l;
	}

	void Zafer()
	{
		int odul = 15 + _dalga * 6;
		_altin += odul;
		Mesaj($"Dalga {_dalga} geçildi!  +{odul} altın", new Color(0.6f, 1f, 0.5f), 2.5f);
		Oynat("altin");
		foreach (var k in _kahraman.Values)
			if (k.Acik && !k.Olu) { k.Spr.Play("sevinc"); k.Mesgul = true; }
		_dalga++;
		KayitYaz();
		HazirligaGec(false);
	}

	void Yenilgi()
	{
		Mesaj("Kale düştü! Güçlen ve tekrar dene.", new Color(1f, 0.45f, 0.4f), 3f);
		Oynat("patlama");
		Sars(25f);
		foreach (var e in _dusmanlar)
		{
			Efekt.Sondur(e.Ates);
			var kok = e.Kok;
			var tw = kok.CreateTween();
			tw.TweenProperty(kok, "modulate:a", 0f, 0.6f);
			tw.TweenCallback(Callable.From(kok.QueueFree));
		}
		_dusmanlar.Clear();
		foreach (var m in _mermiler)
		{
			Efekt.Sondur(m.Iz);
			m.Dugum.QueueFree();
		}
		_mermiler.Clear();
		_dogacak.Clear();
		foreach (var k in _kahraman.Values)
			if (k.Acik) { k.Spr.Play("darbe"); k.Mesgul = true; }
		KayitYaz();
		HazirligaGec(true);
	}

	// ================================================================ HER KARE

	public override void _Process(double delta)
	{
		float d = (float)delta;
		_genislik = GetViewport().GetVisibleRect().Size.X;
		_arka.Genislik = _genislik;

		// zamanlanmış işler
		if (_zamanli.Count > 0)
		{
			var hazir = new List<Action>();
			for (int i = _zamanli.Count - 1; i >= 0; i--)
			{
				var z = _zamanli[i];
				z.t -= d;
				if (z.t <= 0) { hazir.Add(z.a); _zamanli.RemoveAt(i); }
				else _zamanli[i] = z;
			}
			foreach (var a in hazir) a();
		}

		// kamera sarsıntısı
		_sarsinti = Mathf.MoveToward(_sarsinti, 0f, d * 40f);
		_kamera.Offset = _sarsinti > 0.1f ? new Vector2(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1)) * _sarsinti : Vector2.Zero;

		if (_evre == Evre.Savas)
		{
			// düşman doğur
			if (_dogacak.Count > 0)
			{
				_dogmaSayac -= d;
				if (_dogmaSayac <= 0)
				{
					DusmanDogur(_dogacak.Dequeue());
					_dogmaSayac = Mathf.Max(0.45f, 1.25f - _dalga * 0.04f) * _rng.RandfRange(0.6f, 1.3f);
				}
			}
			// kale okçusu (dokunma)
			_atisSayac -= d;
			if (_basili && _atisSayac <= 0)
			{
				var hedef = DokunulanDusman(_parmak);
				if (hedef != null)
				{
					_atisSayac = 0.24f;
					Firlat("ok", KaleOkcuNoktasi, hedef, Vector2.Zero, 0f, 60f, OkHasar(), 0f);
				}
			}
			foreach (var k in _kahraman.Values)
				k.BeceriBekleme = Mathf.Max(0f, k.BeceriBekleme - d);
		}

		foreach (var k in _kahraman.Values)
			KahramanGuncelle(k, d);
		for (int i = _dusmanlar.Count - 1; i >= 0; i--)
			DusmanGuncelle(_dusmanlar[i], d);
		for (int i = _mermiler.Count - 1; i >= 0; i--)
			MermiGuncelle(_mermiler[i], d);

		if (_evre == Evre.Savas && _dogacak.Count == 0 && !_dusmanlar.Any(e => e.Canli))
		{
			if (_bitisSayac < 0) _bitisSayac = 1.2f;
			_bitisSayac -= d;
			if (_bitisSayac <= 0) Zafer();
		}

		if (_mesajSure > 0)
		{
			_mesajSure -= d / (float)Engine.TimeScale;
			_mesaj.Modulate = new Color(1, 1, 1, Mathf.Clamp(_mesajSure * 2f, 0f, 1f));
		}
		BeceriButonlariGuncelle();
		_kaleBar.MaxValue = _kaleCanMax;
		_kaleBar.Value = _kaleCan;
		_kaleYazi.Text = $"Kale  {Mathf.CeilToInt(_kaleCan)} / {Mathf.CeilToInt(_kaleCanMax)}";
	}

	void Zamanla(float sn, Action a) => _zamanli.Add((sn, a));

	void Sars(float miktar) => _sarsinti = Mathf.Max(_sarsinti, miktar);

	// ================================================================ DÜŞMANLAR

	void DusmanDogur(string tur)
	{
		var t = DusmanTuru.Liste[tur];
		float k = 1f + 0.22f * (_dalga - 1);
		var e = new Dusman
		{
			Tur = t,
			Dev = tur == "dev",
			CanMax = t.Can * k,
			Hiz = t.Hiz * _rng.RandfRange(0.9f, 1.1f),
			Hasar = t.Hasar * (1f + 0.1f * (_dalga - 1)),
			Boy = 95f * t.Olcek,
		};
		e.Can = e.CanMax;
		float y = Yer + _rng.RandfRange(-5f, 12f);
		float x = _genislik - _rng.RandfRange(70f, 300f);
		e.Kok = new Node2D { Position = new Vector2(x, y), ZIndex = 7 + (int)(y - Yer + 6) };
		e.Spr = SpriteKutuphane.Olustur(t.Sprite, t.Olcek, true);
		e.Kok.AddChild(e.Spr);
		if (e.Dev)
		{
			e.Spr.Modulate = new Color(1f, 0.72f, 0.72f);
			var aura = Efekt.Yayici(e.Kok, new Vector2(0, -e.Boy * 0.4f), Efekt.MorRenk, 16, 0.9f, 10f, 40f, 0.6f, 1.2f, false, true, -40f, 180f, e.Boy * 0.3f);
			aura.LocalCoords = true;
			aura.ShowBehindParent = true;
			Mesaj("Dev İskelet geliyor!", new Color(1f, 0.5f, 0.9f), 2f);
		}
		e.CanArka = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Size = new Vector2(e.Dev ? 80 : 42, 7), Visible = false };
		e.CanArka.Position = new Vector2(-e.CanArka.Size.X / 2, -e.Boy - 14);
		e.CanCubugu = new ColorRect { Color = new Color(0.9f, 0.15f, 0.12f), Size = new Vector2(e.CanArka.Size.X - 2, 5), Position = new Vector2(1, 1) };
		e.CanArka.AddChild(e.CanCubugu);
		e.Kok.AddChild(e.CanArka);
		e.DurmaX = t.Uzak ? _rng.RandfRange(620f, 840f) : _rng.RandfRange(0f, 34f);
		e.Spr.Play("dogus");
		e.Bekleme = 0.3f;
		_dunya.AddChild(e.Kok);
		Efekt.Toz(_dunya, new Vector2(x, y), e.Dev ? 2.2f : 1.1f);
		_dusmanlar.Add(e);
	}

	float HedefX(Dusman e)
	{
		if (e.Tur.Uzak) return e.DurmaX;
		var sov = _kahraman["sovalye"];
		if (sov.Acik && !sov.Olu && e.Kok.Position.X > sov.Kok.Position.X)
			return sov.Kok.Position.X + 40f + e.DurmaX + (e.Dev ? 25f : 0f);
		return KaleOnu.X + 8f + e.DurmaX + (e.Dev ? 25f : 0f);
	}

	void DusmanGuncelle(Dusman e, float d)
	{
		if (e.Durum == DusmanDurum.Olu)
		{
			e.OlumSure += d;
			if (e.OlumSure > 1.6f)
				e.Kok.Modulate = new Color(1, 1, 1, Mathf.Clamp(1f - (e.OlumSure - 1.6f) * 1.5f, 0f, 1f));
			if (e.OlumSure > 2.4f)
			{
				e.Kok.QueueFree();
				_dusmanlar.Remove(e);
			}
			return;
		}
		if (e.Yanma > 0)
		{
			e.Yanma -= d;
			HasarVer(e, e.YanmaHasar * d, Colors.Orange, false);
			if (e.Yanma <= 0 || !e.Canli)
			{
				Efekt.Sondur(e.Ates);
				e.Ates = null;
			}
			if (!e.Canli) return;
		}
		if (e.Durum == DusmanDurum.Dogus)
		{
			if (!e.Spr.IsPlaying())
			{
				e.Durum = DusmanDurum.Yuru;
				e.Spr.Play("yuru");
			}
			return;
		}
		if (e.Sersem > 0)
		{
			e.Sersem -= d;
			if (e.Sersem <= 0 && e.Durum == DusmanDurum.Yuru) e.Spr.Play("yuru");
			return;
		}
		if (e.Vurus >= 0)
		{
			e.Vurus -= d;
			if (e.Vurus < 0) DarbeUygula(e);
		}
		bool darbeAnim = e.Spr.Animation == "darbe" && e.Spr.IsPlaying();
		float hx = HedefX(e);
		if (e.Durum == DusmanDurum.Yuru)
		{
			if (darbeAnim) return;
			if (e.Kok.Position.X > hx)
			{
				e.Kok.Position += new Vector2(-e.Hiz * d, 0);
				if (e.Spr.Animation != "yuru" || !e.Spr.IsPlaying()) e.Spr.Play("yuru");
			}
			else
			{
				e.Durum = DusmanDurum.Saldiri;
				e.Bekleme = 0.15f;
			}
		}
		else if (e.Durum == DusmanDurum.Saldiri)
		{
			if (e.Kok.Position.X > hx + 35f && e.Vurus < 0)
			{
				e.Durum = DusmanDurum.Yuru;
				e.Spr.Play("yuru");
				return;
			}
			e.Bekleme -= d;
			if (e.Bekleme <= 0)
			{
				e.Spr.Play("saldiri");
				e.Spr.Frame = 0;
				e.Vurus = SpriteKutuphane.Sure(e.Tur.Sprite, "saldiri") * 0.45f;
				e.Bekleme = e.Tur.SaldiriAralik;
			}
		}
	}

	void DarbeUygula(Dusman e)
	{
		if (!e.Canli) return;
		if (e.Tur.Uzak)
		{
			var bas = e.Kok.Position + new Vector2(-28, -e.Boy * 0.7f);
			var son = new Vector2(KaleOnu.X - 20f, Yer - _rng.RandfRange(1.5f, 4.5f) * U);
			Firlat("mor", bas, null, son, 0.9f, 80f, e.Hasar, 0f);
			Oynat("buyu", -10f);
			return;
		}
		var sov = _kahraman["sovalye"];
		var vurusNokta = e.Kok.Position + new Vector2(-30, -e.Boy * 0.5f);
		if (sov.Acik && !sov.Olu && e.Kok.Position.X < sov.Kok.Position.X + 110f && e.Kok.Position.X > sov.Kok.Position.X)
			SovalyeHasar(e.Hasar);
		else
			KaleHasar(e.Hasar);
		Efekt.Kivilcim(_dunya, vurusNokta, new Color(1f, 0.85f, 0.5f));
		Oynat("vurus", -8f);
	}

	void KaleHasar(float h)
	{
		if (_evre != Evre.Savas) return;
		_kaleCan -= h;
		Sars(4f);
		Oynat("kale", -6f);
		_kaleDugum.Modulate = new Color(1f, 0.7f, 0.7f);
		_kaleDugum.CreateTween().TweenProperty(_kaleDugum, "modulate", Colors.White, 0.25f);
		if (_kaleCan <= 0)
		{
			_kaleCan = 0;
			Yenilgi();
		}
	}

	void SovalyeHasar(float h)
	{
		var sov = _kahraman["sovalye"];
		sov.Can -= h;
		Efekt.Yazi(_dunya, sov.Kok.Position + new Vector2(0, -80), $"-{Mathf.CeilToInt(h)}", new Color(1f, 0.4f, 0.4f), 22);
		if (sov.Can <= 0)
		{
			sov.Olu = true;
			sov.Spr.Play("olum");
			Mesaj("Şövalye düştü!", new Color(1f, 0.5f, 0.4f), 1.5f);
		}
		else if (!sov.Mesgul)
		{
			sov.Spr.Play("darbe");
			sov.Mesgul = true;
		}
	}

	void HasarVer(Dusman e, float miktar, Color renk, bool yazi = true)
	{
		if (!e.Canli) return;
		e.Can -= miktar;
		e.CanArka.Visible = true;
		e.CanCubugu.Size = new Vector2((e.CanArka.Size.X - 2) * Mathf.Clamp(e.Can / e.CanMax, 0f, 1f), 5);
		if (yazi)
			Efekt.Yazi(_dunya, e.Kok.Position + new Vector2(_rng.RandfRange(-12, 12), -e.Boy - 20), Mathf.CeilToInt(miktar).ToString(), renk, 24);
		if (e.Can <= 0)
		{
			Oldur(e);
			return;
		}
		if (yazi && !e.Dev && e.Durum == DusmanDurum.Yuru && _rng.Randf() < 0.3f)
			e.Spr.Play("darbe");
	}

	void Oldur(Dusman e)
	{
		e.Durum = DusmanDurum.Olu;
		e.Vurus = -1f;
		e.Spr.Play("olum");
		e.CanArka.Visible = false;
		Efekt.Sondur(e.Ates);
		e.Ates = null;
		int altin = Mathf.RoundToInt(e.Tur.Altin * (1f + 0.06f * (_dalga - 1)));
		_altin += altin;
		Efekt.Yazi(_dunya, e.Kok.Position + new Vector2(0, -e.Boy - 45), $"+{altin}", new Color(1f, 0.85f, 0.2f), 28);
		Efekt.Kemik(_dunya, e.Merkez);
		Oynat("altin", -12f);
		ArayuzGuncelle();
	}

	void Yak(Dusman e, float sure, float dps)
	{
		if (!e.Canli) return;
		e.Yanma = Mathf.Max(e.Yanma, sure);
		e.YanmaHasar = Mathf.Max(e.YanmaHasar, dps);
		if (e.Ates == null || !IsInstanceValid(e.Ates))
		{
			e.Ates = Efekt.Ates(e.Kok, new Vector2(0, -e.Boy * 0.45f), e.Dev ? 1.4f : 0.8f, 20);
			e.Ates.ZIndex = 1;
		}
	}

	Dusman OndekiDusman()
	{
		Dusman en = null;
		foreach (var e in _dusmanlar)
		{
			if (!e.Canli || e.Durum == DusmanDurum.Dogus || e.Kok.Position.X > _genislik - 20) continue;
			if (en == null || e.Kok.Position.X < en.Kok.Position.X) en = e;
		}
		return en;
	}

	Dusman DokunulanDusman(Vector2 p)
	{
		Dusman en = null;
		float enIyi = 150f;
		foreach (var e in _dusmanlar)
		{
			if (!e.Canli) continue;
			float m = Mathf.Abs(e.Kok.Position.X - p.X) + Mathf.Abs(e.Merkez.Y - p.Y) * 0.25f;
			if (m < enIyi) { enIyi = m; en = e; }
		}
		return en;
	}

	IEnumerable<Dusman> Yakindakiler(Vector2 p, float r) =>
		_dusmanlar.Where(e => e.Canli && Mathf.Abs(e.Kok.Position.X - p.X) < r && Mathf.Abs(e.Merkez.Y - p.Y) < r + e.Boy * 0.5f).ToList();

	// ================================================================ KAHRAMANLAR

	void KahramanGuncelle(Kahraman k, float d)
	{
		if (!k.Acik) return;
		if (k.Olu) return;
		if (!k.Spr.IsPlaying())
		{
			k.Mesgul = false;
			k.Spr.Play("bekle");
		}
		if (_evre != Evre.Savas) return;
		k.Bekleme -= d;
		if (k.Bekleme > 0 || k.Mesgul) return;

		if (k.Tur == "sovalye")
		{
			Dusman hedef = null;
			foreach (var e in _dusmanlar)
				if (e.Canli && e.Durum != DusmanDurum.Dogus && e.Kok.Position.X - k.Kok.Position.X < 105f && e.Kok.Position.X > k.Kok.Position.X - 20f)
					if (hedef == null || e.Kok.Position.X < hedef.Kok.Position.X) hedef = e;
			if (hedef == null) return;
			k.Bekleme = k.Aralik;
			k.Spr.Play("saldiri");
			k.Mesgul = true;
			Zamanla(SpriteKutuphane.Sure("sovalye", "saldiri") * 0.42f, () =>
			{
				if (!hedef.Canli || k.Olu) return;
				HasarVer(hedef, KahramanHasar(k), Colors.White);
				Efekt.Kivilcim(_dunya, hedef.Merkez + new Vector2(-15, 0), new Color(0.8f, 0.9f, 1f));
				Oynat("vurus", -6f);
			});
			return;
		}

		var t = OndekiDusman();
		if (t == null) return;
		k.Bekleme = k.Aralik;
		k.Spr.Play("saldiri");
		k.Mesgul = true;
		float an = k.Tur switch { "okcu" => 0.4f, "buyucu" => 0.5f, _ => 0.45f };
		Zamanla(SpriteKutuphane.Sure(k.Tur, "saldiri") * an, () =>
		{
			var hedef = t.Canli ? t : OndekiDusman();
			if (hedef == null) return;
			var el = k.Kok.Position + new Vector2(24, -40);
			switch (k.Tur)
			{
				case "okcu":
					Firlat("ok", el, hedef, Vector2.Zero, 0f, 40f, KahramanHasar(k), 0f);
					break;
				case "buyucu":
					Firlat("ates", el + new Vector2(10, -10), hedef, Vector2.Zero, 0f, 120f, KahramanHasar(k), 78f);
					Oynat("ates", -8f);
					break;
				case "barbar":
					Firlat("balta", el, hedef, Vector2.Zero, 0f, 140f, KahramanHasar(k), 0f);
					break;
			}
		});
	}

	void BeceriKullan(Kahraman k)
	{
		if (_evre != Evre.Savas || !k.Acik || k.Olu || k.BeceriBekleme > 0) return;
		k.BeceriBekleme = k.BeceriSure;
		k.Spr.Play("beceri");
		k.Mesgul = true;
		float h = KahramanHasar(k);
		var on = OndekiDusman();
		float onX = on != null ? on.Kok.Position.X : _genislik * 0.6f;
		switch (k.Tur)
		{
			case "okcu":
				Oynat("ok");
				for (int i = 0; i < 18; i++)
				{
					float gecikme = 0.3f + i * 0.06f;
					Zamanla(gecikme, () =>
					{
						var son = new Vector2(onX + _rng.RandfRange(-40f, 300f), Yer + _rng.RandfRange(-14f, 6f));
						Firlat("yagmur", son + new Vector2(-230f, -760f), null, son, 0.5f, 0f, h * 1.2f, 46f);
					});
				}
				break;
			case "buyucu":
			{
				var hedefler = _dusmanlar.Where(q => q.Canli && q.Durum != DusmanDurum.Dogus).OrderBy(q => q.Kok.Position.X).ToList();
				Oynat("buyu");
				for (int i = 0; i < 3; i++)
				{
					int j = i;
					Zamanla(0.5f + i * 0.4f, () =>
					{
						Vector2 son;
						if (j < hedefler.Count && hedefler[j].Canli) son = new Vector2(hedefler[j].Kok.Position.X - hedefler[j].Hiz * 0.8f, Yer - 10);
						else son = new Vector2(onX + j * 140f, Yer - 10);
						Firlat("meteor", son + new Vector2(-420f, -800f), null, son, 0.8f, 0f, h * 3.2f, 125f);
						Oynat("ates", -4f);
					});
				}
				break;
			}
			case "barbar":
				Zamanla(SpriteKutuphane.Sure("barbar", "beceri") * 0.72f, () =>
				{
					Oynat("deprem");
					Sars(24f);
					Halka.Olustur(_dunya, new Vector2(KaleOnu.X, Yer), new Color(0.85f, 0.65f, 0.35f), 900f, 0.9f, 0.12f);
					for (float xx = KaleOnu.X; xx < _genislik; xx += 85f)
					{
						float gx = xx;
						Zamanla((gx - KaleOnu.X) / 1400f, () => Efekt.Toz(_dunya, new Vector2(gx, Yer + 4), 1.6f));
					}
					foreach (var e in _dusmanlar.Where(q => q.Canli).ToList())
					{
						HasarVer(e, h * 1.5f, new Color(1f, 0.8f, 0.4f));
						if (!e.Canli) continue;
						e.Sersem = 2.0f;
						e.Vurus = -1f;
						if (e.Durum != DusmanDurum.Dogus) e.Spr.Play("darbe");
					}
				});
				break;
			case "sovalye":
				Zamanla(SpriteKutuphane.Sure("sovalye", "beceri") * 0.4f, () =>
				{
					Oynat("kalkan");
					var merkez = k.Kok.Position + new Vector2(30, -30);
					Halka.Olustur(_dunya, merkez, new Color(0.55f, 0.85f, 1f), 260f, 0.5f);
					Efekt.Parlama(_dunya, merkez, new Color(0.5f, 0.8f, 1f), 3f, 0.35f);
					foreach (var e in _dusmanlar.Where(q => q.Canli && q.Kok.Position.X - k.Kok.Position.X < 260f).ToList())
					{
						HasarVer(e, h * 1.5f, new Color(0.6f, 0.9f, 1f));
						if (!e.Canli) continue;
						e.Sersem = 1.6f;
						e.Vurus = -1f;
						if (e.Durum == DusmanDurum.Saldiri) e.Durum = DusmanDurum.Yuru;
						e.Spr.Play("darbe");
						float yeniX = Mathf.Min(e.Kok.Position.X + (e.Dev ? 60f : 170f), _genislik - 30f);
						e.Kok.CreateTween().TweenProperty(e.Kok, "position:x", yeniX, 0.35f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
					}
				});
				break;
		}
	}

	// ================================================================ MERMİLER

	void Firlat(string tur, Vector2 bas, Dusman hedef, Vector2 son, float sure, float kavis, float hasar, float alan)
	{
		float hiz = tur switch { "ok" => 1000f, "ates" => 620f, "balta" => 680f, _ => 600f };
		if (hedef != null)
		{
			float mesafe = bas.DistanceTo(hedef.Merkez);
			sure = Mathf.Clamp(mesafe / hiz, 0.18f, 1.4f);
			float hareket = hedef.Durum == DusmanDurum.Yuru && hedef.Sersem <= 0 ? -hedef.Hiz * sure : 0f;
			son = hedef.Merkez + new Vector2(hareket, 0);
		}
		var m = new Mermi { Tur = tur, Bas = bas, Son = son, Sure = sure, Kavis = kavis, Hasar = hasar, Alan = alan, Hedef = hedef };
		m.Dugum = new Node2D { Position = bas, ZIndex = 20 };
		switch (tur)
		{
			case "ok":
			case "yagmur":
				m.Dugum.AddChild(new Sprite2D { Texture = _okDoku, FlipH = true, Scale = Vector2.One * 0.6f });
				break;
			case "balta":
				m.Dugum.AddChild(new Sprite2D { Texture = _baltaDoku, Scale = Vector2.One * 0.42f });
				m.Donen = true;
				break;
			case "ates":
				m.Dugum.AddChild(new Sprite2D { Texture = Efekt.Parilti, Modulate = new Color(1f, 0.55f, 0.15f), Scale = Vector2.One * 0.9f, Material = Efekt.Toplama });
				m.Dugum.AddChild(new Sprite2D { Texture = Efekt.Parilti, Modulate = new Color(1f, 1f, 0.8f), Scale = Vector2.One * 0.4f, Material = Efekt.Toplama });
				m.Iz = Efekt.Ates(m.Dugum, Vector2.Zero, 0.9f, 30);
				break;
			case "meteor":
				m.Dugum.AddChild(new Sprite2D { Texture = Efekt.Parilti, Modulate = new Color(1f, 0.45f, 0.1f), Scale = Vector2.One * 2.2f, Material = Efekt.Toplama });
				m.Dugum.AddChild(new Sprite2D { Texture = Efekt.Parilti, Modulate = new Color(1f, 1f, 0.75f), Scale = Vector2.One * 1.0f, Material = Efekt.Toplama });
				m.Iz = Efekt.Yayici(m.Dugum, Vector2.Zero, Efekt.AtesRenk, 60, 0.8f, 20f, 90f, 1.0f, 2.2f, false, true, -60f, 40f, 16f);
				break;
			case "mor":
				m.Dugum.AddChild(new Sprite2D { Texture = Efekt.Parilti, Modulate = new Color(0.75f, 0.3f, 1f), Scale = Vector2.One * 0.8f, Material = Efekt.Toplama });
				m.Iz = Efekt.Yayici(m.Dugum, Vector2.Zero, Efekt.MorRenk, 26, 0.45f, 10f, 40f, 0.3f, 0.7f, false, true, 0f, 180f, 4f);
				break;
		}
		_dunya.AddChild(m.Dugum);
		_mermiler.Add(m);
		if (tur == "ok") Oynat("ok", -10f);
		if (tur == "balta") Oynat("ok", -6f);
	}

	void MermiGuncelle(Mermi m, float d)
	{
		m.Gecen += d;
		float u = Mathf.Min(1f, m.Gecen / Mathf.Max(0.01f, m.Sure));
		var onceki = m.Dugum.Position;
		var p = m.Bas.Lerp(m.Son, u) + new Vector2(0, -m.Kavis * 4f * u * (1f - u));
		m.Dugum.Position = p;
		if (m.Donen) m.Dugum.Rotation += d * 16f;
		else if ((p - onceki).LengthSquared() > 0.01f) m.Dugum.Rotation = (p - onceki).Angle();
		if (u >= 1f)
		{
			Carp(m, p);
			Efekt.Sondur(m.Iz);
			m.Dugum.QueueFree();
			_mermiler.Remove(m);
		}
	}

	void Carp(Mermi m, Vector2 p)
	{
		switch (m.Tur)
		{
			case "ok":
			case "balta":
			{
				Dusman hedef = m.Hedef != null && m.Hedef.Canli && m.Hedef.Merkez.DistanceTo(p) < 75f ? m.Hedef : Yakindakiler(p, 50f).FirstOrDefault();
				if (hedef != null)
				{
					HasarVer(hedef, m.Hasar, m.Tur == "balta" ? new Color(1f, 0.75f, 0.4f) : Colors.White);
					Efekt.Kivilcim(_dunya, p, m.Tur == "balta" ? new Color(1f, 0.6f, 0.3f) : new Color(1f, 0.95f, 0.6f));
					if (m.Tur == "balta") { Oynat("vurus", -4f); Sars(3f); }
				}
				break;
			}
			case "yagmur":
			{
				var yakin = Yakindakiler(p + new Vector2(0, -30), m.Alan).ToList();
				foreach (var e in yakin) HasarVer(e, m.Hasar, new Color(1f, 1f, 0.7f));
				if (yakin.Count == 0) Efekt.Toz(_dunya, p, 0.4f);
				else Efekt.Kivilcim(_dunya, p + new Vector2(0, -20), new Color(1f, 0.95f, 0.6f));
				break;
			}
			case "ates":
			{
				Efekt.Patlama(_dunya, p, 0.75f);
				Oynat("patlama", -12f);
				foreach (var e in Yakindakiler(p, m.Alan))
				{
					HasarVer(e, m.Hasar, new Color(1f, 0.6f, 0.2f));
					Yak(e, 3f, m.Hasar * 0.25f);
				}
				break;
			}
			case "meteor":
			{
				Efekt.Patlama(_dunya, p, 1.9f);
				Halka.Olustur(_dunya, p, new Color(1f, 0.6f, 0.2f), m.Alan * 1.4f, 0.45f, 0.25f);
				Oynat("patlama", -2f);
				Sars(16f);
				foreach (var e in Yakindakiler(p + new Vector2(0, -30), m.Alan))
				{
					HasarVer(e, m.Hasar, new Color(1f, 0.5f, 0.1f));
					Yak(e, 4f, m.Hasar * 0.15f);
				}
				break;
			}
			case "mor":
				Efekt.MorPatlama(_dunya, p);
				KaleHasar(m.Hasar);
				break;
		}
	}

	// ================================================================ DOKUNMA

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventScreenTouch t)
		{
			_basili = t.Pressed;
			_parmak = t.Position;
			if (t.Pressed) _atisSayac = Mathf.Min(_atisSayac, 0f);
		}
		else if (ev is InputEventScreenDrag dr)
		{
			_parmak = dr.Position;
		}
	}

	// ================================================================ ARAYÜZ

	void ArayuzKur()
	{
		var katman = new CanvasLayer();
		AddChild(katman);
		var kok = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		kok.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		kok.Theme = TemaOlustur();
		katman.AddChild(kok);

		var solUst = new VBoxContainer { Position = new Vector2(18, 12) };
		_altinYazi = YaziOlustur(30, new Color(1f, 0.85f, 0.25f));
		_dalgaYazi = YaziOlustur(24, Colors.White);
		solUst.AddChild(_altinYazi);
		solUst.AddChild(_dalgaYazi);
		kok.AddChild(solUst);

		_kaleBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(380, 30) };
		_kaleBar.AnchorLeft = _kaleBar.AnchorRight = 0.5f;
		_kaleBar.OffsetLeft = -190; _kaleBar.OffsetRight = 190; _kaleBar.OffsetTop = 16; _kaleBar.OffsetBottom = 46;
		_kaleBar.AddThemeStyleboxOverride("background", Kutu(new Color(0.1f, 0.06f, 0.05f, 0.85f), new Color(0.85f, 0.65f, 0.25f), 3, 8));
		_kaleBar.AddThemeStyleboxOverride("fill", Kutu(new Color(0.75f, 0.15f, 0.12f), new Color(0, 0, 0, 0), 0, 6));
		kok.AddChild(_kaleBar);
		_kaleYazi = YaziOlustur(20, Colors.White);
		_kaleYazi.HorizontalAlignment = HorizontalAlignment.Center;
		_kaleYazi.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_kaleBar.AddChild(_kaleYazi);

		_hizButon = new Button { Text = "Hız x1", CustomMinimumSize = new Vector2(130, 54) };
		_hizButon.AnchorLeft = _hizButon.AnchorRight = 1f;
		_hizButon.OffsetLeft = -146; _hizButon.OffsetRight = -16; _hizButon.OffsetTop = 12; _hizButon.OffsetBottom = 66;
		_hizButon.Pressed += () =>
		{
			Engine.TimeScale = Engine.TimeScale > 1.5 ? 1.0 : 2.0;
			_hizButon.Text = Engine.TimeScale > 1.5 ? "Hız x2" : "Hız x1";
		};
		kok.AddChild(_hizButon);

		// Geliştirme paneli
		_panel = new Panel();
		_panel.AnchorLeft = _panel.AnchorRight = 1f;
		_panel.AnchorTop = 0f; _panel.AnchorBottom = 1f;
		_panel.OffsetLeft = -430; _panel.OffsetRight = -14; _panel.OffsetTop = 78; _panel.OffsetBottom = -120;
		kok.AddChild(_panel);
		var kaydir = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		kaydir.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		kaydir.OffsetLeft = 12; kaydir.OffsetTop = 10; kaydir.OffsetRight = -12; kaydir.OffsetBottom = -10;
		_panel.AddChild(kaydir);
		_gelistirmeKutu = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_gelistirmeKutu.AddThemeConstantOverride("separation", 8);
		kaydir.AddChild(_gelistirmeKutu);

		_savasButon = new Button { Text = "SAVAŞ!", CustomMinimumSize = new Vector2(280, 84) };
		_savasButon.AddThemeFontSizeOverride("font_size", 40);
		_savasButon.AnchorLeft = _savasButon.AnchorRight = 0.5f;
		_savasButon.AnchorTop = _savasButon.AnchorBottom = 1f;
		_savasButon.OffsetLeft = -140 - 120; _savasButon.OffsetRight = 140 - 120; _savasButon.OffsetTop = -104; _savasButon.OffsetBottom = -20;
		_savasButon.AddThemeStyleboxOverride("normal", Kutu(new Color(0.65f, 0.12f, 0.08f), new Color(1f, 0.8f, 0.3f), 4, 14));
		_savasButon.AddThemeStyleboxOverride("hover", Kutu(new Color(0.78f, 0.18f, 0.1f), new Color(1f, 0.85f, 0.4f), 4, 14));
		_savasButon.AddThemeStyleboxOverride("pressed", Kutu(new Color(0.5f, 0.08f, 0.05f), new Color(1f, 0.8f, 0.3f), 4, 14));
		_savasButon.Pressed += SavasaBasla;
		kok.AddChild(_savasButon);

		_beceriKutu = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		_beceriKutu.AnchorLeft = 0f; _beceriKutu.AnchorRight = 1f; _beceriKutu.AnchorTop = _beceriKutu.AnchorBottom = 1f;
		_beceriKutu.OffsetLeft = 300; _beceriKutu.OffsetRight = -14; _beceriKutu.OffsetTop = -86; _beceriKutu.OffsetBottom = -10;
		_beceriKutu.AddThemeConstantOverride("separation", 10);
		kok.AddChild(_beceriKutu);
		foreach (var k in new[] { "okcu", "buyucu", "barbar", "sovalye" }.Select(t => _kahraman[t]))
		{
			var kh = k;
			kh.Buton = new Button { CustomMinimumSize = new Vector2(170, 72), FocusMode = Control.FocusModeEnum.None };
			kh.Buton.AddThemeFontSizeOverride("font_size", 19);
			kh.Buton.Pressed += () => BeceriKullan(kh);
			_beceriKutu.AddChild(kh.Buton);
		}

		_mesaj = YaziOlustur(42, Colors.White);
		_mesaj.HorizontalAlignment = HorizontalAlignment.Center;
		_mesaj.AnchorLeft = 0; _mesaj.AnchorRight = 1; _mesaj.OffsetTop = 120; _mesaj.OffsetBottom = 180;
		_mesaj.AddThemeConstantOverride("outline_size", 10);
		kok.AddChild(_mesaj);

		_ipucu = YaziOlustur(20, new Color(1f, 1f, 0.85f));
		_ipucu.Text = "İpucu: düşmanlara dokun, kale okçusu ateş etsin!";
		_ipucu.AnchorTop = _ipucu.AnchorBottom = 1f;
		_ipucu.OffsetLeft = 16; _ipucu.OffsetTop = -40; _ipucu.OffsetBottom = -10; _ipucu.OffsetRight = 600;
		kok.AddChild(_ipucu);
	}

	Label YaziOlustur(int boyut, Color renk)
	{
		var l = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", boyut);
		l.AddThemeColorOverride("font_color", renk);
		return l;
	}

	static StyleBoxFlat Kutu(Color ic, Color kenar, int kalinlik, int kose)
	{
		var s = new StyleBoxFlat { BgColor = ic, BorderColor = kenar };
		s.SetBorderWidthAll(kalinlik);
		s.SetCornerRadiusAll(kose);
		s.ContentMarginLeft = s.ContentMarginRight = 12;
		s.ContentMarginTop = s.ContentMarginBottom = 6;
		return s;
	}

	Theme TemaOlustur()
	{
		var t = new Theme { DefaultFontSize = 22 };
		var altin = new Color(0.85f, 0.65f, 0.25f);
		t.SetStylebox("normal", "Button", Kutu(new Color(0.3f, 0.19f, 0.1f, 0.95f), altin, 3, 10));
		t.SetStylebox("hover", "Button", Kutu(new Color(0.4f, 0.26f, 0.13f, 0.95f), altin.Lightened(0.2f), 3, 10));
		t.SetStylebox("pressed", "Button", Kutu(new Color(0.2f, 0.12f, 0.06f, 0.95f), altin, 3, 10));
		t.SetStylebox("disabled", "Button", Kutu(new Color(0.22f, 0.2f, 0.18f, 0.85f), new Color(0.45f, 0.42f, 0.38f), 3, 10));
		t.SetStylebox("focus", "Button", new StyleBoxEmpty());
		t.SetStylebox("panel", "Panel", Kutu(new Color(0.1f, 0.07f, 0.05f, 0.88f), altin, 3, 14));
		t.SetColor("font_color", "Button", new Color(1f, 0.95f, 0.85f));
		t.SetColor("font_disabled_color", "Button", new Color(0.65f, 0.62f, 0.58f));
		t.SetColor("font_outline_color", "Button", new Color(0.08f, 0.04f, 0.02f));
		t.SetConstant("outline_size", "Button", 5);
		t.SetColor("font_outline_color", "Label", new Color(0.08f, 0.04f, 0.02f));
		t.SetConstant("outline_size", "Label", 7);
		return t;
	}

	void ArayuzGuncelle()
	{
		_altinYazi.Text = $"Altın: {_altin}";
		_dalgaYazi.Text = $"Dalga {_dalga}";
		if (_evre != Evre.Hazirlik) return;

		foreach (var c in _gelistirmeKutu.GetChildren())
			c.QueueFree();
		var baslik = YaziOlustur(26, new Color(1f, 0.85f, 0.4f));
		baslik.Text = "Geliştirmeler";
		baslik.HorizontalAlignment = HorizontalAlignment.Center;
		_gelistirmeKutu.AddChild(baslik);

		GelistirmeButonu($"Kale Duvarı  Sv.{_kaleSv}\nCan {Mathf.RoundToInt(KaleCanMax())} → yükselt: {KaleBedel()} altın", KaleBedel(), () =>
		{
			_kaleSv++;
			_kaleCanMax = KaleCanMax();
			_kaleCan = _kaleCanMax;
		});
		GelistirmeButonu($"Kale Okçusu  Sv.{_okSv}\nDokunma hasarı {Mathf.RoundToInt(OkHasar())} → yükselt: {OkBedel()} altın", OkBedel(), () => _okSv++);
		foreach (var t in new[] { "okcu", "buyucu", "barbar", "sovalye" })
		{
			var k = _kahraman[t];
			if (k.Acik)
				GelistirmeButonu($"{k.Ad}  Sv.{k.Seviye}\nHasar {Mathf.RoundToInt(KahramanHasar(k))} → yükselt: {YukseltmeBedeli(k)} altın", YukseltmeBedeli(k), () =>
				{
					k.Seviye++;
					if (k.Tur == "sovalye") k.Can = k.CanMax = SovalyeCanMax(k);
					Efekt.Parlama(_dunya, k.Kok.Position + new Vector2(0, -40), new Color(1f, 0.9f, 0.4f), 2f, 0.4f);
					k.Spr.Play("sevinc");
					k.Mesgul = true;
				});
			else
				GelistirmeButonu($"{k.Ad} kilitli ({k.BeceriAd})\nAç: {AcmaBedeli(t)} altın", AcmaBedeli(t), () =>
				{
					k.Acik = true;
					KaleKur(true);
					Oynat("kale");
					Mesaj($"{k.Ad} kuleye katıldı!", new Color(0.6f, 1f, 0.6f), 2f);
					k.Spr.Play("sevinc");
					k.Mesgul = true;
				});
		}
	}

	void GelistirmeButonu(string metin, int bedel, Action islem)
	{
		var b = new Button { Text = metin, CustomMinimumSize = new Vector2(0, 66), Disabled = _altin < bedel, FocusMode = Control.FocusModeEnum.None };
		b.AddThemeFontSizeOverride("font_size", 18);
		b.Pressed += () =>
		{
			if (_altin < bedel) return;
			_altin -= bedel;
			islem();
			Oynat("altin");
			KayitYaz();
			ArayuzGuncelle();
		};
		_gelistirmeKutu.AddChild(b);
	}

	void BeceriButonlariGuncelle()
	{
		_altinYazi.Text = $"Altın: {_altin}";
		foreach (var k in _kahraman.Values)
		{
			if (k.Buton == null) continue;
			k.Buton.Visible = k.Acik;
			if (!k.Acik) continue;
			bool hazir = k.BeceriBekleme <= 0 && !k.Olu;
			k.Buton.Text = k.Olu ? $"{k.BeceriAd}\nDüştü" : hazir ? $"{k.BeceriAd}\nHAZIR" : $"{k.BeceriAd}\n{Mathf.CeilToInt(k.BeceriBekleme)} sn";
			k.Buton.Disabled = !hazir || _evre != Evre.Savas;
		}
	}

	void Mesaj(string metin, Color renk, float sure)
	{
		_mesaj.Text = metin;
		_mesaj.AddThemeColorOverride("font_color", renk);
		_mesaj.Modulate = Colors.White;
		_mesajSure = sure;
	}

	// ================================================================ SES

	void SesleriKur()
	{
		foreach (var ad in new[] { "ok", "ates", "patlama", "vurus", "altin", "kale", "buyu", "deprem", "boru", "kalkan" })
			_sesler[ad] = Efekt.SesUret(ad);
		for (int i = 0; i < 10; i++)
		{
			var o = new AudioStreamPlayer();
			AddChild(o);
			_sesOynaticilar.Add(o);
		}
	}

	void Oynat(string ad, float db = 0f)
	{
		if (!_sesler.TryGetValue(ad, out var akis)) return;
		var o = _sesOynaticilar.FirstOrDefault(x => !x.Playing) ?? _sesOynaticilar[0];
		o.Stream = akis;
		o.VolumeDb = db;
		o.PitchScale = _rng.RandfRange(0.92f, 1.08f);
		o.Play();
	}

	// ================================================================ KAYIT

	void KayitYukle()
	{
		var cf = new ConfigFile();
		if (cf.Load("user://kayit.cfg") != Error.Ok) return;
		_altin = (int)cf.GetValue("oyun", "altin", 60);
		_dalga = (int)cf.GetValue("oyun", "dalga", 1);
		_kaleSv = (int)cf.GetValue("oyun", "kale", 1);
		_okSv = (int)cf.GetValue("oyun", "okcu_kale", 1);
		foreach (var k in _kahraman.Values)
		{
			k.Seviye = (int)cf.GetValue("kahraman", k.Tur + "_sv", 1);
			k.Acik = (bool)cf.GetValue("kahraman", k.Tur + "_acik", k.Tur == "okcu");
		}
	}

	void KayitYaz()
	{
		var cf = new ConfigFile();
		cf.SetValue("oyun", "altin", _altin);
		cf.SetValue("oyun", "dalga", _dalga);
		cf.SetValue("oyun", "kale", _kaleSv);
		cf.SetValue("oyun", "okcu_kale", _okSv);
		foreach (var k in _kahraman.Values)
		{
			cf.SetValue("kahraman", k.Tur + "_sv", k.Seviye);
			cf.SetValue("kahraman", k.Tur + "_acik", k.Acik);
		}
		cf.Save("user://kayit.cfg");
	}

	// ================================================================ ÖNİZLEME (bulutta ekran görüntüsü)

	async System.Threading.Tasks.Task OnizlemeCek(string klasor)
	{
		DirAccess.MakeDirRecursiveAbsolute(klasor);
		_altin = 5000;
		_dalga = 5;
		foreach (var k in _kahraman.Values) { k.Acik = true; k.Seviye = 3; }
		KaleKur(true);
		HazirligaGec(false);
		await Bekle(1.5f);
		await Kare(klasor + "/1_hazirlik.png");
		SavasaBasla();
		await Bekle(5.5f);
		await Kare(klasor + "/2_savas.png");
		BeceriKullan(_kahraman["buyucu"]);
		BeceriKullan(_kahraman["okcu"]);
		await Bekle(1.25f);
		await Kare(klasor + "/3_meteor.png");
		BeceriKullan(_kahraman["barbar"]);
		BeceriKullan(_kahraman["sovalye"]);
		await Bekle(0.75f);
		await Kare(klasor + "/4_deprem.png");
		await Bekle(6f);
		await Kare(klasor + "/5_savas_sonra.png");
		GD.Print("ONIZLEME bitti");
		GetTree().Quit();
	}

	async System.Threading.Tasks.Task Bekle(float sn)
	{
		await ToSignal(GetTree().CreateTimer(sn), SceneTreeTimer.SignalName.Timeout);
	}

	async System.Threading.Tasks.Task Kare(string yol)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(yol);
		GD.Print("ONIZLEME kaydedildi: ", yol);
	}
}
