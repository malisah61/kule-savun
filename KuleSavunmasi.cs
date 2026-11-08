using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Kule Savunma: yola kule dizilen, 40 bölümlük karanlık fantastik kule savunma oyunu.</summary>
public partial class KuleSavunmasi : Node2D
{
	// ------------------------------------------------------------ türler
	class Kule
	{
		public KuleTanim T;
		public int Sv = 1, Harcanan;
		public Vector2 P;
		public Node2D Kok;
		public Sprite2D Spr;
		public Label Yildiz;
		public float Bekleme;
		public float Hasar => T.Hasar * (1f + 0.55f * (Sv - 1));
		public float Menzil => T.Menzil * (1f + 0.07f * (Sv - 1));
		public float Hiz => T.AtisHizi * (1f + 0.08f * (Sv - 1));
		public int YukseltBedel => (int)(T.Maliyet * (0.6f + 0.4f * Sv));
	}

	class Dus
	{
		public DusmanTanim T;
		public Node2D Kok, Govde;
		public Sprite2D Spr;
		public ColorRect CanArka, CanOn;
		public float Can, CanMax, Hiz, Mesafe, Faz, Boy, Odul;
		public float Yavas, YavasSure, ZehirDps, ZehirSure, YanmaDps, YanmaSure, Sersem;
		public string ZehirTur = "Zehir", YanmaTur = "Ates";
		public CpuParticles2D Ates;
		public bool Olu;
		public Vector2 Merkez => Kok.Position + new Vector2(0, -Boy * 0.5f + (T.Ucan ? -26 : 0));
	}

	class Mermi
	{
		public Kule K;
		public Dus Hedef;
		public Vector2 P, Son, Bas;
		public float Gecen, Sure, Kavis;
		public Node2D Gor;
		public CpuParticles2D Iz;
	}

	// ------------------------------------------------------------ durum
	enum Ekran { Menu, Oyun, Bitti }
	Ekran _ekran = Ekran.Menu;
	int _enUst = 1;                // açılmış en yüksek bölüm
	int _bolum = 1, _dalga, _dalgaSayisi, _can, _altin;
	bool _dalgaSuruyor;
	float _dogmaSayac, _dogmaAralik;
	readonly Queue<DusmanTanim> _dogacak = new();
	readonly List<Kule> _kuleler = new();
	readonly List<Dus> _dusmanlar = new();
	readonly List<Mermi> _mermiler = new();
	readonly HashSet<string> _gorulen = new();
	List<Vector2> _yerler;
	int _seciliYer = -1;
	Kule _seciliKule;
	int _hizKat = 1;

	Node2D _dunya;
	TdHarita _harita;
	YerCizer _yerCizer;
	readonly RandomNumberGenerator _rng = new();

	// arayüz
	CanvasLayer _ui;
	Control _menu, _ust, _insaPanel, _kulePanel, _sonucPanel;
	Label _bolumYazi, _dalgaYazi, _canYazi, _altinYazi, _mesaj, _menuOzet;
	Button _dalgaButon, _hizButon;
	VBoxContainer _insaListe;
	Label _kuleBaslik, _kuleBilgi;
	Button _yukseltButon, _satButon;
	GridContainer _bolumIzgara;
	Label _sonucBaslik, _sonucMetin;
	Button _sonucDevam;
	float _mesajSure;

	readonly Dictionary<string, AudioStreamWav> _sesler = new();
	readonly List<AudioStreamPlayer> _oynaticilar = new();
	readonly Dictionary<string, float> _sesSon = new();

	// ================================================================ BAŞLANGIÇ
	public override void _Ready()
	{
		_rng.Randomize();
		TdVeri.Yukle();
		KayitYukle();
		foreach (var s in new[] { "ok", "ates", "patlama", "vurus", "altin", "buyu", "boru", "kale", "kalkan" }) _sesler[s] = Efekt.SesUret(s);
		for (int i = 0; i < 8; i++) { var p = new AudioStreamPlayer { VolumeDb = -8 }; AddChild(p); _oynaticilar.Add(p); }

		_harita = new TdHarita { ZIndex = -10 };
		AddChild(_harita);
		_dunya = new Node2D { YSortEnabled = true };
		AddChild(_dunya);
		_yerler = TdHarita.KuleYerleri();
		_yerCizer = new YerCizer { Oyun = this, ZIndex = -5 };
		AddChild(_yerCizer);

		ArayuzKur();
		MenuyuGoster();

		foreach (var arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--onizleme="))
				_ = OnizlemeCek(arg.Substring("--onizleme=".Length));
	}

	void Ses(string ad)
	{
		if (!_sesler.ContainsKey(ad)) return;
		float simdi = Time.GetTicksMsec() / 1000f;
		if (_sesSon.TryGetValue(ad, out var son) && simdi - son < 0.07f) return;
		_sesSon[ad] = simdi;
		var p = _oynaticilar.FirstOrDefault(o => !o.Playing) ?? _oynaticilar[0];
		p.Stream = _sesler[ad];
		p.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		p.Play();
	}

	// ================================================================ KAYIT
	void KayitYukle()
	{
		if (!FileAccess.FileExists("user://td_kayit.json")) return;
		var d = Json.ParseString(FileAccess.GetFileAsString("user://td_kayit.json")).AsGodotDictionary();
		if (d.ContainsKey("enUst")) _enUst = Math.Max(1, d["enUst"].AsInt32());
	}

	void KayitYaz()
	{
		using var f = FileAccess.Open("user://td_kayit.json", FileAccess.ModeFlags.Write);
		f?.StoreString(Json.Stringify(new Godot.Collections.Dictionary { ["enUst"] = _enUst }));
	}

	IEnumerable<KuleTanim> AcikKuleler => TdVeri.Kuleler.Where(k => k.Level <= _enUst);

	// ================================================================ BÖLÜM AKIŞI
	void BolumBaslat(int bolum)
	{
		Temizle();
		_bolum = bolum;
		_dalga = 0;
		_dalgaSayisi = Math.Min(13, 5 + bolum / 5);
		_can = 20;
		_altin = 160 + bolum * 25;
		_dalgaSuruyor = false;
		_ekran = Ekran.Oyun;
		_menu.Visible = false;
		_sonucPanel.Visible = false;
		_ust.Visible = true;
		_dalgaButon.Visible = true;
		_dalgaButon.Text = "▶ Dalga 1";
		_hizKat = 1; Engine.TimeScale = 1; _hizButon.Text = "Hız x1";
		ArayuzGuncelle();
		var yeni = TdVeri.Kuleler.Where(k => k.Level == bolum).Select(k => k.Ad).ToList();
		Mesaj(yeni.Count > 0 ? $"Bölüm {bolum}\nYeni kule: {string.Join(", ", yeni)}" : $"Bölüm {bolum}", new Color(1f, 0.9f, 0.55f), 3f);
	}

	void Temizle()
	{
		foreach (var k in _kuleler) k.Kok.QueueFree();
		foreach (var d in _dusmanlar) d.Kok.QueueFree();
		foreach (var m in _mermiler) { m.Gor?.QueueFree(); }
		_kuleler.Clear(); _dusmanlar.Clear(); _mermiler.Clear(); _dogacak.Clear(); _gorulen.Clear();
		PanelleriKapat();
	}

	void DalgaBaslat()
	{
		if (_dalgaSuruyor || _ekran != Ekran.Oyun || _dalga >= _dalgaSayisi) return;
		_dalga++;
		_dalgaSuruyor = true;
		_dalgaButon.Visible = false;
		var havuz = TdVeri.Dusmanlar.Where(d => !d.Boss && d.Level <= _bolum).ToList();
		int adet = 5 + _bolum / 2 + _dalga * 2;
		for (int i = 0; i < adet; i++)
		{
			// yeni açılan düşmanlar daha sık gelir
			float toplam = havuz.Sum(Agirlik);
			float r = _rng.Randf() * toplam;
			var sec = havuz[^1];
			foreach (var d in havuz) { r -= Agirlik(d); if (r <= 0) { sec = d; break; } }
			int tekrar = sec.Ozel.Contains("kalabalik") ? 3 : 1;
			for (int j = 0; j < tekrar; j++) _dogacak.Enqueue(sec);
		}
		if (_dalga == _dalgaSayisi)
		{
			var boss = TdVeri.Dusmanlar.FirstOrDefault(d => d.Boss && d.Level == _bolum)
				?? (_bolum % 5 == 0 ? TdVeri.Dusmanlar.Where(d => d.Boss && d.Level < _bolum).OrderBy(_ => _rng.Randf()).FirstOrDefault() : null);
			if (boss != null) _dogacak.Enqueue(boss);
		}
		_dogmaAralik = Mathf.Max(0.32f, 0.95f - _bolum * 0.013f);
		_dogmaSayac = 0.3f;
		Ses("boru");
		Mesaj($"Dalga {_dalga}/{_dalgaSayisi}", new Color(1f, 0.55f, 0.4f), 1.5f);
		ArayuzGuncelle();
	}

	float Agirlik(DusmanTanim d) => 1f + (d.Level >= _bolum - 2 ? 3f : 0f);

	void DusmanDogur(DusmanTanim t)
	{
		var d = new Dus { T = t };
		float can = TdVeri.TemelCan(_bolum) * (0.8f + 0.6f * t.Guc);
		float hiz = 46f;
		if (t.Ozel.Contains("hizli")) { hiz = 70f; can *= 0.8f; }
		if (t.Ozel.Contains("yavas")) { hiz = 30f; can *= 1.8f; }
		if (t.Ozel.Contains("kalkan")) can *= 1.3f;
		if (t.Ozel.Contains("kalabalik")) { hiz = 54f; can *= 0.45f; }
		if (t.Ucan) { hiz = 58f; can *= 0.85f; }
		if (t.Zirh == "Hayalet") hiz = 50f;
		if (t.Boss) { hiz = 27f; can *= 16f; }
		d.Can = d.CanMax = can;
		d.Hiz = hiz * _rng.RandfRange(0.92f, 1.08f);
		d.Odul = t.Boss ? 25 + _bolum * 4 : (2 + _bolum * 0.35f) * (t.Ozel.Contains("kalabalik") ? 0.5f : 1f);
		d.Boy = t.Boss ? 96f : t.Ozel.Contains("kalabalik") ? 40f : 56f;
		d.Faz = _rng.RandfRange(0, 6);

		d.Kok = new Node2D();
		var golge = new Sprite2D { Texture = Efekt.Yumusak, Modulate = new Color(0, 0, 0, 0.45f), Scale = new Vector2(d.Boy / 90f, d.Boy / 300f) };
		d.Kok.AddChild(golge);
		d.Govde = new Node2D { Position = new Vector2(0, t.Ucan ? -26 : 0) };
		d.Kok.AddChild(d.Govde);
		var doku = t.Doku ?? (Texture2D)Efekt.Yumusak;
		d.Spr = new Sprite2D { Texture = doku };
		float olcek = d.Boy / doku.GetHeight();
		d.Spr.Scale = Vector2.One * olcek;
		d.Spr.Offset = new Vector2(0, -doku.GetHeight() * 0.5f);
		d.Spr.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		if (t.Doku == null) d.Spr.Modulate = ZirhRenk(t.Zirh);
		d.Govde.AddChild(d.Spr);
		float gen = t.Boss ? 70 : 36;
		d.CanArka = new ColorRect { Color = new Color(0, 0, 0, 0.7f), Size = new Vector2(gen + 2, 5), Position = new Vector2(-gen / 2 - 1, -d.Boy - 10) };
		d.CanOn = new ColorRect { Color = t.Boss ? new Color(0.9f, 0.2f, 0.25f) : new Color(0.35f, 0.9f, 0.35f), Size = new Vector2(gen, 3), Position = new Vector2(-gen / 2, -d.Boy - 9) };
		d.Govde.AddChild(d.CanArka); d.Govde.AddChild(d.CanOn);
		d.Kok.Position = TdHarita.Konum(0, out _);
		_dunya.AddChild(d.Kok);
		_dusmanlar.Add(d);
		if (t.Ozel.Contains("kalabalik")) d.Mesafe = -_rng.RandfRange(0, 20);
		if (_gorulen.Add(t.Id))
		{
			string zayif = t.Zayif.Length > 0 ? "Zayıf: " + string.Join(", ", t.Zayif) : "";
			string guclu = t.Guclu.Length > 0 ? "  Güçlü: " + string.Join(", ", t.Guclu) : "";
			if (t.Boss || t.Level == _bolum) Mesaj($"{(t.Boss ? "BOSS: " : "Yeni düşman: ")}{t.Ad}\n{zayif}{guclu}", t.Boss ? new Color(1f, 0.35f, 0.3f) : new Color(0.8f, 0.9f, 1f), 3f);
			if (t.Boss) Ses("kale");
		}
	}

	static Color ZirhRenk(string z) => z switch
	{
		"Olumsuz" => new Color(0.8f, 0.78f, 0.65f), "Hayalet" => new Color(0.7f, 0.85f, 1f, 0.8f), "Canavar" => new Color(0.55f, 0.4f, 0.3f),
		"Zirhli" => new Color(0.5f, 0.5f, 0.55f), "Seytan" => new Color(0.9f, 0.25f, 0.2f), "Vampir" => new Color(0.6f, 0.1f, 0.2f),
		"Bocek" => new Color(0.4f, 0.5f, 0.2f), "Tas" => new Color(0.55f, 0.52f, 0.5f), "Buz" => new Color(0.6f, 0.85f, 1f),
		"Bataklik" => new Color(0.3f, 0.55f, 0.3f), "Kultist" => new Color(0.45f, 0.25f, 0.55f), _ => new Color(0.3f, 0.3f, 0.35f),
	};

	// ================================================================ GÜNCELLEME
	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (_mesajSure > 0) { _mesajSure -= dt / (float)Engine.TimeScale; if (_mesajSure <= 0) _mesaj.Visible = false; }
		if (_ekran != Ekran.Oyun) return;

		if (_dalgaSuruyor && _dogacak.Count > 0)
		{
			_dogmaSayac -= dt;
			if (_dogmaSayac <= 0)
			{
				var t = _dogacak.Dequeue();
				DusmanDogur(t);
				_dogmaSayac = t.Ozel.Contains("kalabalik") && _dogacak.Count > 0 && _dogacak.Peek() == t ? 0.18f : _dogmaAralik;
			}
		}
		DusmanlariGuncelle(dt);
		KuleleriGuncelle(dt);
		MermileriGuncelle(dt);

		if (_dalgaSuruyor && _dogacak.Count == 0 && _dusmanlar.Count == 0)
		{
			_dalgaSuruyor = false;
			if (_dalga >= _dalgaSayisi) { BolumKazanildi(); return; }
			int bonus = 20 + _bolum * 3;
			_altin += bonus;
			Efekt.Yazi(_dunya, new Vector2(640, 110), $"+{bonus} altın", new Color(1f, 0.85f, 0.3f), 30);
			_dalgaButon.Text = $"▶ Dalga {_dalga + 1}";
			_dalgaButon.Visible = true;
			ArayuzGuncelle();
		}
	}

	void DusmanlariGuncelle(float dt)
	{
		for (int i = _dusmanlar.Count - 1; i >= 0; i--)
		{
			var d = _dusmanlar[i];
			if (d.Olu) continue;
			// durum etkileri
			if (d.ZehirSure > 0) { d.ZehirSure -= dt; HasarVer(d, d.ZehirDps * dt, d.ZehirTur, false); if (d.Olu) continue; }
			if (d.YanmaSure > 0)
			{
				d.YanmaSure -= dt; HasarVer(d, d.YanmaDps * dt, d.YanmaTur, false); if (d.Olu) continue;
				if (d.YanmaSure <= 0 && d.Ates != null) { Efekt.Sondur(d.Ates); d.Ates = null; }
			}
			if (d.T.Ozel.Contains("iyilesir")) d.Can = Mathf.Min(d.CanMax, d.Can + d.CanMax * 0.015f * dt);
			if (d.YavasSure > 0) d.YavasSure -= dt; else d.Yavas = 0;
			float hiz = d.Hiz * (1f - d.Yavas);
			if (d.Sersem > 0) { d.Sersem -= dt; hiz = 0; }
			d.Mesafe += hiz * dt;
			d.Faz += dt * hiz / 46f;
			var p = TdHarita.Konum(Mathf.Max(0, d.Mesafe), out var yon);
			d.Kok.Position = p;
			if (Mathf.Abs(yon.X) > 0.1f) d.Spr.FlipH = yon.X < 0;
			// yürüme / süzülme
			bool suzulur = d.T.Ucan || d.T.Zirh == "Hayalet";
			d.Spr.Rotation = suzulur ? Mathf.Sin(d.Faz * 3) * 0.05f : Mathf.Sin(d.Faz * 8) * 0.07f;
			d.Spr.Position = new Vector2(0, suzulur ? Mathf.Sin(d.Faz * 2.5f) * 4 : -Mathf.Abs(Mathf.Sin(d.Faz * 8)) * 3);
			if (d.T.Zirh == "Hayalet") d.Spr.SelfModulate = new Color(1, 1, 1, 0.65f + 0.2f * Mathf.Sin(d.Faz * 2));
			var renk = Colors.White;
			if (d.Yavas > 0) renk = new Color(0.65f, 0.85f, 1.25f);
			else if (d.ZehirSure > 0) renk = new Color(0.75f, 1.2f, 0.6f);
			if (d.Sersem > 0) renk = new Color(1.2f, 1.2f, 0.6f);
			if (d.T.Doku != null && d.Spr.Modulate.R <= 1.3f) d.Spr.Modulate = renk;
			d.CanOn.Size = new Vector2((d.T.Boss ? 70 : 36) * Mathf.Clamp(d.Can / d.CanMax, 0, 1), 3);
			if (d.Ates != null) d.Ates.Position = d.Merkez;

			if (d.Mesafe >= TdHarita.Uzunluk)
			{
				int kayip = d.T.Boss ? 10 : 1;
				_can -= kayip;
				Ses("kale");
				Efekt.Yazi(_dunya, new Vector2(1220, 450), $"-{kayip} ♥", new Color(1f, 0.3f, 0.3f), 30);
				DusmaniSil(d, false);
				ArayuzGuncelle();
				if (_can <= 0) { Kaybedildi(); return; }
			}
		}
	}

	void HasarVer(Dus d, float miktar, string tur, bool vurusEfekti, Kule k = null)
	{
		if (d.Olu) return;
		float c = TdVeri.HasarCarpani(d.T.Zirh, tur);
		float h = miktar * c;
		if (d.T.Ozel.Contains("kalkan") && tur == "Fiziksel") h *= 0.7f;
		d.Can -= h;
		if (vurusEfekti)
		{
			if (c == 0f) Efekt.Yazi(_dunya, d.Merkez, "BAĞIŞIK", new Color(0.7f, 0.7f, 0.75f), 16);
			else if (c >= 1.5f && _rng.Randf() < 0.35f) Efekt.Yazi(_dunya, d.Merkez, ((int)h).ToString() + "!", new Color(1f, 0.85f, 0.2f), 20);
			if (d.T.Doku != null)
			{
				d.Spr.Modulate = new Color(2.2f, 2.2f, 2.2f);
				var tw = d.Spr.CreateTween();
				tw.TweenProperty(d.Spr, "modulate", Colors.White, 0.12f);
			}
		}
		if (d.Can <= 0) Oldur(d);
	}

	void Oldur(Dus d)
	{
		if (d.Olu) return;
		_altin += Mathf.Max(1, Mathf.RoundToInt(d.Odul));
		var m = d.Merkez;
		switch (d.T.Zirh)
		{
			case "Hayalet": case "Kultist": case "Golge": Efekt.MorPatlama(_dunya, m, d.T.Boss ? 2f : 0.7f); break;
			case "Seytan": case "Buz": Efekt.Patlama(_dunya, m, d.T.Boss ? 1.6f : 0.55f); break;
			case "Bocek": case "Bataklik":
				Efekt.Yayici(_dunya, m, new[] { new Color(0.5f, 0.9f, 0.3f, 1f), new Color(0.2f, 0.4f, 0.1f, 0f) }, 16, 0.6f, 80, 180, 0.2f, 0.4f, true, false, 400f);
				break;
			default: Efekt.Kemik(_dunya, m); break;
		}
		if (d.T.Boss) { Ses("patlama"); Mesaj($"{d.T.Ad} yenildi!", new Color(0.6f, 1f, 0.6f), 2f); }
		else Ses("vurus");
		DusmaniSil(d, true);
		ArayuzGuncelle();
	}

	void DusmaniSil(Dus d, bool animasyon)
	{
		d.Olu = true;
		_dusmanlar.Remove(d);
		if (d.Ates != null) { Efekt.Sondur(d.Ates); d.Ates = null; }
		if (!animasyon) { d.Kok.QueueFree(); return; }
		d.CanArka.Visible = d.CanOn.Visible = false;
		var tw = d.Kok.CreateTween().SetParallel(true);
		tw.TweenProperty(d.Spr, "scale", new Vector2(d.Spr.Scale.X * 1.3f, 0.01f), 0.35f).SetEase(Tween.EaseType.In);
		tw.TweenProperty(d.Kok, "modulate:a", 0f, 0.35f);
		tw.Chain().TweenCallback(Callable.From(d.Kok.QueueFree));
	}

	// ------------------------------------------------------------ kuleler
	void KuleleriGuncelle(float dt)
	{
		foreach (var k in _kuleler)
		{
			k.Bekleme -= dt;
			if (k.Bekleme > 0) continue;
			if (k.T.Mod == "aura")
			{
				var icinde = _dusmanlar.Where(d => d.Kok.Position.DistanceTo(k.P) <= k.Menzil).ToList();
				if (icinde.Count == 0) continue;
				k.Bekleme = 1f / k.Hiz;
				Efekt.Parlama(_dunya, k.P + new Vector2(0, -20), new Color(TdVeri.HasarRenk[k.T.HasarTuru], 0.5f), k.Menzil / 22f, 0.5f);
				foreach (var d in icinde)
				{
					HasarVer(d, k.Hasar, k.T.HasarTuru, false, k);
					if (!d.Olu && k.T.HasarTuru == "Buz" && TdVeri.HasarCarpani(d.T.Zirh, "Buz") > 0) { d.Yavas = Mathf.Max(d.Yavas, 0.35f); d.YavasSure = 1.2f; }
				}
				KuleTepki(k);
				continue;
			}
			var hedef = HedefSec(k);
			if (hedef == null) continue;
			k.Bekleme = 1f / k.Hiz;
			KuleTepki(k);
			var cikis = k.P + new Vector2(0, -52);
			switch (k.T.Mod)
			{
				case "zincir": Zincir(k, hedef, cikis); break;
				case "delici": Isin(k, hedef, cikis); break;
				default: MermiAt(k, hedef, cikis); break;
			}
		}
	}

	Dus HedefSec(Kule k)
	{
		Dus en = null;
		float enIyi = float.MinValue;
		foreach (var d in _dusmanlar)
		{
			if (d.Olu || d.Kok.Position.DistanceTo(k.P) > k.Menzil) continue;
			if (k.T.HasarTuru == "Kusatma" && d.T.Ucan) continue;
			float puan = d.Mesafe + (k.T.Mod == "ucan_avci" && d.T.Ucan ? 5000 : 0);
			if (puan > enIyi) { enIyi = puan; en = d; }
		}
		return en;
	}

	void KuleTepki(Kule k)
	{
		var tw = k.Spr.CreateTween();
		var o = k.Spr.Scale;
		tw.TweenProperty(k.Spr, "scale", new Vector2(o.X * 1.08f, o.Y * 0.92f), 0.06f);
		tw.TweenProperty(k.Spr, "scale", o, 0.12f);
		string s = k.T.HasarTuru switch { "Ates" => "ates", "Kusatma" => "patlama", "Fiziksel" => "ok", "Kutsal" => "kalkan", _ => "buyu" };
		if (k.T.HasarTuru != "Kusatma" || _rng.Randf() < 0.5f) Ses(s);
	}

	void MermiAt(Kule k, Dus hedef, Vector2 cikis)
	{
		var renk = TdVeri.HasarRenk[k.T.HasarTuru];
		var gor = new Node2D { Position = cikis, ZIndex = 20 };
		var parilti = new Sprite2D { Texture = Efekt.Parilti, Modulate = renk, Scale = Vector2.One * (k.T.Mod == "alan" ? 0.42f : 0.28f), Material = Efekt.Toplama };
		gor.AddChild(parilti);
		if (k.T.HasarTuru == "Fiziksel")
		{
			var ok = new Line2D { Points = new[] { new Vector2(-9, 0), new Vector2(9, 0) }, Width = 2.5f, DefaultColor = new Color(0.85f, 0.75f, 0.55f) };
			gor.AddChild(ok);
			parilti.Scale = Vector2.One * 0.12f;
		}
		AddChild(gor);
		var m = new Mermi { K = k, Hedef = hedef, Bas = cikis, P = cikis, Son = hedef.Merkez, Gor = gor };
		bool lob = k.T.HasarTuru == "Kusatma" || k.T.Mod == "alan";
		float uzak = cikis.DistanceTo(m.Son);
		m.Sure = Mathf.Clamp(uzak / (lob ? 330f : 560f), 0.12f, 0.8f);
		m.Kavis = lob ? Mathf.Min(120f, uzak * 0.45f) : 0f;
		if (k.T.HasarTuru == "Ates") m.Iz = Efekt.Ates(gor, Vector2.Zero, 0.45f, 12);
		_mermiler.Add(m);
	}

	void MermileriGuncelle(float dt)
	{
		for (int i = _mermiler.Count - 1; i >= 0; i--)
		{
			var m = _mermiler[i];
			m.Gecen += dt;
			if (m.Hedef != null && !m.Hedef.Olu) m.Son = m.Hedef.Merkez;
			float t = Mathf.Clamp(m.Gecen / m.Sure, 0, 1);
			var yeni = m.Bas.Lerp(m.Son, t) + new Vector2(0, -m.Kavis * 4 * t * (1 - t));
			if (m.Gor.GetChildCount() > 1 && (yeni - m.P).LengthSquared() > 0.01f) m.Gor.Rotation = (yeni - m.P).Angle();
			m.P = yeni;
			m.Gor.Position = yeni;
			if (t < 1) continue;
			_mermiler.RemoveAt(i);
			if (m.Iz != null) Efekt.Sondur(m.Iz);
			m.Gor.QueueFree();
			Isabet(m.K, m.Hedef, m.Son);
		}
	}

	void Isabet(Kule k, Dus hedef, Vector2 nokta)
	{
		string tur = k.T.HasarTuru;
		var renk = TdVeri.HasarRenk[tur];
		switch (k.T.Mod)
		{
			case "alan":
				float yaricap = 48f + k.Sv * 4;
				if (tur == "Ates" || tur == "Kusatma") Efekt.Patlama(_dunya, nokta, 0.5f);
				else if (tur == "Golge") Efekt.MorPatlama(_dunya, nokta, 0.7f);
				else Efekt.Parlama(_dunya, nokta, new Color(renk, 0.8f), yaricap / 14f, 0.35f);
				foreach (var d in _dusmanlar.ToList())
					if (d.Kok.Position.DistanceTo(nokta) <= yaricap + 10 && !(tur == "Kusatma" && d.T.Ucan))
					{
						HasarVer(d, k.Hasar, tur, d == hedef, k);
						if (!d.Olu && tur == "Buz") Yavaslat(d, 0.35f, 1.5f);
						if (!d.Olu && tur == "Zehir") Zehirle(d, k.Hasar * 0.4f, 3f, tur);
					}
				return;
			case "yakici":
				Efekt.Patlama(_dunya, nokta, 0.3f);
				foreach (var d in _dusmanlar.ToList())
					if (d.Kok.Position.DistanceTo(nokta) <= 40)
					{
						HasarVer(d, k.Hasar, tur, d == hedef, k);
						if (!d.Olu && TdVeri.HasarCarpani(d.T.Zirh, tur) > 0)
						{
							d.YanmaDps = Mathf.Max(d.YanmaDps * (d.YanmaSure > 0 ? 1 : 0), k.Hasar * 0.7f);
							d.YanmaSure = 3f; d.YanmaTur = tur;
							d.Ates ??= Efekt.Ates(_dunya, d.Merkez, 0.4f, 10);
						}
					}
				return;
		}
		if (hedef == null || hedef.Olu) return;
		Efekt.Kivilcim(_dunya, nokta, renk);
		float carp = k.T.Mod == "ucan_avci" && hedef.T.Ucan ? 2f : 1f;
		HasarVer(hedef, k.Hasar * carp, tur, true, k);
		if (hedef.Olu) return;
		switch (k.T.Mod)
		{
			case "yavas": Yavaslat(hedef, tur == "Buz" ? 0.45f : 0.5f, 2f); if (tur == "Zehir") Zehirle(hedef, k.Hasar * 0.6f, 3f, tur); break;
			case "zehir": Zehirle(hedef, k.Hasar * 0.9f, 4f, tur); break;
			case "sersem": hedef.Sersem = hedef.T.Boss ? 0.15f : 0.55f; Efekt.Parlama(_dunya, hedef.Merkez, new Color(0.7f, 0.4f, 1f, 0.7f), 1.4f, 0.3f); break;
		}
	}

	void Yavaslat(Dus d, float oran, float sure)
	{
		if (TdVeri.HasarCarpani(d.T.Zirh, "Buz") == 0 && oran > 0) return;
		d.Yavas = Mathf.Max(d.Yavas, d.T.Boss ? oran * 0.5f : oran);
		d.YavasSure = sure;
	}

	void Zehirle(Dus d, float dps, float sure, string tur)
	{
		d.ZehirDps = Mathf.Max(d.ZehirSure > 0 ? d.ZehirDps : 0, dps);
		d.ZehirSure = sure; d.ZehirTur = tur;
	}

	void Zincir(Kule k, Dus ilk, Vector2 cikis)
	{
		var vurulan = new List<Dus> { ilk };
		var son = ilk;
		for (int i = 0; i < 3 + k.Sv / 2; i++)
		{
			var sonraki = _dusmanlar.Where(d => !d.Olu && !vurulan.Contains(d) && d.Kok.Position.DistanceTo(son.Kok.Position) < 95)
				.OrderBy(d => d.Kok.Position.DistanceTo(son.Kok.Position)).FirstOrDefault();
			if (sonraki == null) break;
			vurulan.Add(sonraki); son = sonraki;
		}
		var noktalar = new List<Vector2> { cikis };
		noktalar.AddRange(vurulan.Select(d => d.Merkez));
		Simsek(noktalar, TdVeri.HasarRenk[k.T.HasarTuru]);
		float h = k.Hasar;
		foreach (var d in vurulan) { HasarVer(d, h, k.T.HasarTuru, true, k); h *= 0.75f; }
	}

	void Simsek(List<Vector2> noktalar, Color renk)
	{
		var l = new Line2D { Width = 3, DefaultColor = new Color(renk.R, renk.G, renk.B, 1), ZIndex = 25, Material = Efekt.Toplama };
		for (int i = 0; i < noktalar.Count; i++)
		{
			l.AddPoint(noktalar[i]);
			if (i < noktalar.Count - 1)
				for (int j = 1; j < 4; j++)
				{
					var p = noktalar[i].Lerp(noktalar[i + 1], j / 4f);
					l.AddPoint(p + new Vector2(_rng.RandfRange(-9, 9), _rng.RandfRange(-9, 9)));
				}
		}
		AddChild(l);
		var tw = l.CreateTween();
		tw.TweenProperty(l, "modulate:a", 0f, 0.18f);
		tw.TweenCallback(Callable.From(l.QueueFree));
	}

	void Isin(Kule k, Dus hedef, Vector2 cikis)
	{
		var yon = (hedef.Merkez - cikis).Normalized();
		var bitis = cikis + yon * k.Menzil * 1.1f;
		var renk = TdVeri.HasarRenk[k.T.HasarTuru];
		var l = new Line2D { Points = new[] { cikis, bitis }, Width = 7, DefaultColor = renk, ZIndex = 25, Material = Efekt.Toplama };
		AddChild(l);
		var tw = l.CreateTween().SetParallel(true);
		tw.TweenProperty(l, "width", 1f, 0.22f);
		tw.TweenProperty(l, "modulate:a", 0f, 0.22f);
		tw.Chain().TweenCallback(Callable.From(l.QueueFree));
		foreach (var d in _dusmanlar.ToList())
		{
			var c = d.Merkez;
			float t = Mathf.Clamp((c - cikis).Dot(bitis - cikis) / (bitis - cikis).LengthSquared(), 0, 1);
			if (c.DistanceTo(cikis + (bitis - cikis) * t) < 18) HasarVer(d, k.Hasar, k.T.HasarTuru, true, k);
		}
	}

	// ------------------------------------------------------------ kule kurma
	void KuleKur(int yer, KuleTanim t)
	{
		if (_altin < t.Maliyet || _kuleler.Any(k => k.P == _yerler[yer])) return;
		_altin -= t.Maliyet;
		var k = new Kule { T = t, P = _yerler[yer], Harcanan = t.Maliyet };
		k.Kok = new Node2D { Position = k.P };
		var golge = new Sprite2D { Texture = Efekt.Yumusak, Modulate = new Color(0, 0, 0, 0.5f), Scale = new Vector2(1.0f, 0.32f), Position = new Vector2(0, 4) };
		k.Kok.AddChild(golge);
		var doku = t.Doku ?? (Texture2D)Efekt.Yumusak;
		k.Spr = new Sprite2D { Texture = doku, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
		float boy = 74f;
		k.Spr.Scale = Vector2.One * (boy / doku.GetHeight());
		k.Spr.Offset = new Vector2(0, -doku.GetHeight() * 0.5f);
		k.Spr.Position = new Vector2(0, 8);
		if (t.Doku == null) k.Spr.Modulate = TdVeri.HasarRenk[t.HasarTuru];
		k.Kok.AddChild(k.Spr);
		k.Yildiz = new Label { Position = new Vector2(-30, 8), Size = new Vector2(60, 20), HorizontalAlignment = HorizontalAlignment.Center };
		k.Yildiz.AddThemeFontSizeOverride("font_size", 13);
		k.Yildiz.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		k.Yildiz.AddThemeColorOverride("font_outline_color", Colors.Black);
		k.Yildiz.AddThemeConstantOverride("outline_size", 4);
		k.Kok.AddChild(k.Yildiz);
		YildizGuncelle(k);
		_dunya.AddChild(k.Kok);
		_kuleler.Add(k);
		k.Spr.Scale *= new Vector2(1, 0.1f);
		k.Spr.CreateTween().TweenProperty(k.Spr, "scale", Vector2.One * (boy / doku.GetHeight()), 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		Efekt.Toz(_dunya, k.P, 1.2f);
		Ses("kalkan");
		PanelleriKapat();
		ArayuzGuncelle();
	}

	void YildizGuncelle(Kule k) => k.Yildiz.Text = new string('★', k.Sv);

	void Yukselt(Kule k)
	{
		if (k.Sv >= 5 || _altin < k.YukseltBedel) return;
		_altin -= k.YukseltBedel;
		k.Harcanan += k.YukseltBedel;
		k.Sv++;
		YildizGuncelle(k);
		Efekt.Parlama(_dunya, k.P + new Vector2(0, -30), new Color(1f, 0.85f, 0.3f, 0.8f), 3f, 0.4f);
		Ses("altin");
		KulePaneliAc(k);
		ArayuzGuncelle();
	}

	void Sat(Kule k)
	{
		_altin += (int)(k.Harcanan * 0.6f);
		_kuleler.Remove(k);
		Efekt.Toz(_dunya, k.P, 1.4f);
		k.Kok.QueueFree();
		Ses("altin");
		PanelleriKapat();
		ArayuzGuncelle();
	}

	// ------------------------------------------------------------ dokunma
	public override void _UnhandledInput(InputEvent e)
	{
		if (_ekran != Ekran.Oyun) return;
		Vector2 p;
		if (e is InputEventScreenTouch st && st.Pressed) p = st.Position;
		else return;
		p = GetGlobalTransform().AffineInverse() * GetViewport().GetCanvasTransform().AffineInverse() * p;
		int en = -1; float enU = 40f;
		for (int i = 0; i < _yerler.Count; i++)
		{
			float u = _yerler[i].DistanceTo(p + new Vector2(0, 20));
			if (u < enU) { enU = u; en = i; }
		}
		if (en < 0) { PanelleriKapat(); return; }
		var kule = _kuleler.FirstOrDefault(k => k.P == _yerler[en]);
		if (kule != null) KulePaneliAc(kule);
		else InsaPaneliAc(en);
	}

	public int SeciliYer => _seciliYer;
	public Vector2? SeciliKuleKonum => _seciliKule?.P;
	public List<Vector2> Yerler => _yerler;
	public bool DoluMu(Vector2 p) => _kuleler.Any(k => k.P == p);
	public bool OyunEkrani => _ekran == Ekran.Oyun;
	public float SeciliMenzil => _seciliKule?.Menzil ?? 0;

	// ================================================================ SONUÇ
	void BolumKazanildi()
	{
		_ekran = Ekran.Bitti;
		Engine.TimeScale = 1;
		int onceki = _enUst;
		if (_bolum + 1 > _enUst) _enUst = Math.Min(TdVeri.LevelSayisi, _bolum + 1);
		KayitYaz();
		var yeniKule = TdVeri.Kuleler.Where(k => k.Level > onceki && k.Level <= _enUst).Select(k => k.Ad).ToList();
		var yeniDus = TdVeri.Dusmanlar.Count(d => d.Level > onceki && d.Level <= _enUst);
		string metin = $"Can: {_can}/20   Altın: {_altin}";
		if (yeniKule.Count > 0) metin += $"\n\nAçılan kule: {string.Join(", ", yeniKule)}";
		if (yeniDus > 0) metin += $"\nYeni düşmanlar yolda: {yeniDus}";
		if (_bolum >= TdVeri.LevelSayisi) metin += "\n\nTüm bölümleri bitirdin!";
		SonucGoster("ZAFER!", metin, _bolum < TdVeri.LevelSayisi ? $"Bölüm {_bolum + 1} ▶" : "Menü", true);
		Ses("altin");
	}

	void Kaybedildi()
	{
		_ekran = Ekran.Bitti;
		Engine.TimeScale = 1;
		SonucGoster("SAVUNMA DÜŞTÜ", $"Dalga {_dalga}/{_dalgaSayisi}\nKulelerini düşmanların zayıf yönlerine göre seç.", "Tekrar Dene", false);
	}

	void SonucGoster(string baslik, string metin, string buton, bool kazandi)
	{
		_sonucBaslik.Text = baslik;
		_sonucBaslik.AddThemeColorOverride("font_color", kazandi ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.35f, 0.3f));
		_sonucMetin.Text = metin;
		_sonucDevam.Text = buton;
		_sonucDevam.SetMeta("kazandi", kazandi);
		_sonucPanel.Visible = true;
		_dalgaButon.Visible = false;
	}

	// ================================================================ ARAYÜZ
	static StyleBoxFlat Kutu(Color renk, int kose = 10, Color? kenar = null)
	{
		var s = new StyleBoxFlat { BgColor = renk };
		s.SetCornerRadiusAll(kose);
		s.SetContentMarginAll(10);
		if (kenar != null) { s.BorderColor = kenar.Value; s.SetBorderWidthAll(2); }
		return s;
	}

	static Label Yazi(string metin, int boyut, Color renk)
	{
		var l = new Label { Text = metin };
		l.AddThemeFontSizeOverride("font_size", boyut);
		l.AddThemeColorOverride("font_color", renk);
		l.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.05f));
		l.AddThemeConstantOverride("outline_size", 5);
		return l;
	}

	static Button Dugme(string metin, int boyut = 22)
	{
		var b = new Button { Text = metin, FocusMode = Control.FocusModeEnum.None };
		b.AddThemeFontSizeOverride("font_size", boyut);
		b.AddThemeStyleboxOverride("normal", Kutu(new Color(0.18f, 0.12f, 0.24f), 8, new Color(0.4f, 0.3f, 0.5f)));
		b.AddThemeStyleboxOverride("hover", Kutu(new Color(0.26f, 0.17f, 0.34f), 8, new Color(0.55f, 0.4f, 0.65f)));
		b.AddThemeStyleboxOverride("pressed", Kutu(new Color(0.12f, 0.3f, 0.15f), 8, new Color(0.4f, 0.9f, 0.4f)));
		b.AddThemeStyleboxOverride("disabled", Kutu(new Color(0.12f, 0.11f, 0.13f), 8, new Color(0.22f, 0.2f, 0.24f)));
		return b;
	}

	void ArayuzKur()
	{
		_ui = new CanvasLayer();
		AddChild(_ui);

		// üst çubuk
		_ust = new PanelContainer { Position = Vector2.Zero, Size = new Vector2(1280, 56) };
		_ust.AddThemeStyleboxOverride("panel", Kutu(new Color(0.05f, 0.03f, 0.07f, 0.88f), 0));
		var satir = new HBoxContainer();
		satir.AddThemeConstantOverride("separation", 26);
		_ust.AddChild(satir);
		_bolumYazi = Yazi("", 22, new Color(0.85f, 0.8f, 1f));
		_dalgaYazi = Yazi("", 22, new Color(1f, 0.6f, 0.45f));
		_canYazi = Yazi("", 22, new Color(1f, 0.4f, 0.45f));
		_altinYazi = Yazi("", 22, new Color(1f, 0.85f, 0.3f));
		foreach (var l in new[] { _bolumYazi, _dalgaYazi, _canYazi, _altinYazi }) satir.AddChild(l);
		satir.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		_hizButon = Dugme("Hız x1", 18);
		_hizButon.Pressed += () =>
		{
			_hizKat = _hizKat % 3 + 1;
			Engine.TimeScale = _hizKat;
			_hizButon.Text = $"Hız x{_hizKat}";
		};
		var menuButon = Dugme("Menü", 18);
		menuButon.Pressed += MenuyuGoster;
		satir.AddChild(_hizButon);
		satir.AddChild(menuButon);
		_ui.AddChild(_ust);

		_dalgaButon = Dugme("▶ Dalga 1", 28);
		_dalgaButon.Position = new Vector2(1060, 640);
		_dalgaButon.Size = new Vector2(200, 64);
		_dalgaButon.Pressed += DalgaBaslat;
		_ui.AddChild(_dalgaButon);

		_mesaj = Yazi("", 34, Colors.White);
		_mesaj.HorizontalAlignment = HorizontalAlignment.Center;
		_mesaj.Position = new Vector2(240, 270);
		_mesaj.Size = new Vector2(800, 140);
		_mesaj.MouseFilter = Control.MouseFilterEnum.Ignore;
		_mesaj.Visible = false;
		_ui.AddChild(_mesaj);

		// kule inşa paneli
		_insaPanel = new PanelContainer { Position = new Vector2(880, 66), Size = new Vector2(390, 640) };
		_insaPanel.AddThemeStyleboxOverride("panel", Kutu(new Color(0.07f, 0.05f, 0.1f, 0.95f), 12, new Color(0.35f, 0.25f, 0.45f)));
		var insaKutu = new VBoxContainer();
		insaKutu.AddChild(Yazi("Kule Kur", 24, new Color(1f, 0.85f, 0.4f)));
		var kaydir = new ScrollContainer { CustomMinimumSize = new Vector2(370, 570), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_insaListe = new VBoxContainer();
		_insaListe.AddThemeConstantOverride("separation", 6);
		kaydir.AddChild(_insaListe);
		insaKutu.AddChild(kaydir);
		_insaPanel.AddChild(insaKutu);
		_insaPanel.Visible = false;
		_ui.AddChild(_insaPanel);

		// seçili kule paneli
		_kulePanel = new PanelContainer { Position = new Vector2(880, 66), Size = new Vector2(390, 320) };
		_kulePanel.AddThemeStyleboxOverride("panel", Kutu(new Color(0.07f, 0.05f, 0.1f, 0.95f), 12, new Color(0.35f, 0.25f, 0.45f)));
		var kk = new VBoxContainer();
		kk.AddThemeConstantOverride("separation", 8);
		_kuleBaslik = Yazi("", 24, new Color(1f, 0.85f, 0.4f));
		_kuleBilgi = Yazi("", 17, new Color(0.88f, 0.86f, 0.95f));
		_kuleBilgi.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_kuleBilgi.CustomMinimumSize = new Vector2(360, 0);
		var butonlar = new HBoxContainer();
		_yukseltButon = Dugme("Yükselt", 20);
		_yukseltButon.Pressed += () => { if (_seciliKule != null) Yukselt(_seciliKule); };
		_satButon = Dugme("Sat", 20);
		_satButon.Pressed += () => { if (_seciliKule != null) Sat(_seciliKule); };
		_yukseltButon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		butonlar.AddChild(_yukseltButon); butonlar.AddChild(_satButon);
		kk.AddChild(_kuleBaslik); kk.AddChild(_kuleBilgi); kk.AddChild(butonlar);
		_kulePanel.AddChild(kk);
		_kulePanel.Visible = false;
		_ui.AddChild(_kulePanel);

		// sonuç paneli
		_sonucPanel = new PanelContainer { Position = new Vector2(340, 170), Size = new Vector2(600, 360) };
		_sonucPanel.AddThemeStyleboxOverride("panel", Kutu(new Color(0.06f, 0.04f, 0.09f, 0.96f), 16, new Color(0.5f, 0.35f, 0.6f)));
		var sk = new VBoxContainer();
		sk.AddThemeConstantOverride("separation", 14);
		_sonucBaslik = Yazi("", 44, Colors.White);
		_sonucBaslik.HorizontalAlignment = HorizontalAlignment.Center;
		_sonucMetin = Yazi("", 20, new Color(0.9f, 0.88f, 0.95f));
		_sonucMetin.HorizontalAlignment = HorizontalAlignment.Center;
		_sonucMetin.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_sonucMetin.CustomMinimumSize = new Vector2(560, 150);
		var sb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		sb.AddThemeConstantOverride("separation", 20);
		_sonucDevam = Dugme("", 26);
		_sonucDevam.CustomMinimumSize = new Vector2(220, 60);
		_sonucDevam.Pressed += () =>
		{
			bool kazandi = _sonucDevam.GetMeta("kazandi").AsBool();
			if (kazandi && _bolum < TdVeri.LevelSayisi) BolumBaslat(_bolum + 1);
			else if (kazandi) MenuyuGoster();
			else BolumBaslat(_bolum);
		};
		var sm = Dugme("Menü", 26);
		sm.CustomMinimumSize = new Vector2(160, 60);
		sm.Pressed += MenuyuGoster;
		sb.AddChild(_sonucDevam); sb.AddChild(sm);
		sk.AddChild(_sonucBaslik); sk.AddChild(_sonucMetin); sk.AddChild(sb);
		_sonucPanel.AddChild(sk);
		_sonucPanel.Visible = false;
		_ui.AddChild(_sonucPanel);

		// ana menü
		_menu = new PanelContainer { Position = Vector2.Zero, Size = new Vector2(1280, 720) };
		_menu.AddThemeStyleboxOverride("panel", Kutu(new Color(0.04f, 0.03f, 0.06f, 0.93f), 0));
		var mk = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		mk.AddThemeConstantOverride("separation", 14);
		var baslik = Yazi("KULE SAVUNMASI", 56, new Color(0.55f, 1f, 0.45f));
		baslik.HorizontalAlignment = HorizontalAlignment.Center;
		var alt = Yazi("Mezarlık yolunu koru · 30 kule · 200 düşman · 40 bölüm", 20, new Color(0.75f, 0.7f, 0.9f));
		alt.HorizontalAlignment = HorizontalAlignment.Center;
		_menuOzet = Yazi("", 18, new Color(1f, 0.85f, 0.4f));
		_menuOzet.HorizontalAlignment = HorizontalAlignment.Center;
		var orta = new CenterContainer();
		_bolumIzgara = new GridContainer { Columns = 10 };
		_bolumIzgara.AddThemeConstantOverride("h_separation", 10);
		_bolumIzgara.AddThemeConstantOverride("v_separation", 10);
		orta.AddChild(_bolumIzgara);
		mk.AddChild(baslik); mk.AddChild(alt); mk.AddChild(_menuOzet); mk.AddChild(orta);
		_menu.AddChild(mk);
		_ui.AddChild(_menu);
	}

	void MenuyuGoster()
	{
		Temizle();
		_ekran = Ekran.Menu;
		Engine.TimeScale = 1;
		_menu.Visible = true;
		_ust.Visible = false;
		_dalgaButon.Visible = false;
		_sonucPanel.Visible = false;
		foreach (var c in _bolumIzgara.GetChildren()) c.QueueFree();
		for (int i = 1; i <= TdVeri.LevelSayisi; i++)
		{
			int b = i;
			bool boss = TdVeri.Dusmanlar.Any(d => d.Boss && d.Level == i);
			var dg = Dugme(i <= _enUst ? (boss ? $"{i}☠" : $"{i}") : "🔒", 22);
			dg.CustomMinimumSize = new Vector2(96, 64);
			dg.Disabled = i > _enUst;
			dg.Pressed += () => BolumBaslat(b);
			_bolumIzgara.AddChild(dg);
		}
		int acikKule = AcikKuleler.Count();
		int acikDus = TdVeri.Dusmanlar.Count(d => d.Level <= _enUst);
		_menuOzet.Text = $"Açık kuleler: {acikKule}/{TdVeri.Kuleler.Count}   ·   Tanışılan düşmanlar: {acikDus}/{TdVeri.Dusmanlar.Count}";
	}

	void ArayuzGuncelle()
	{
		_bolumYazi.Text = $"Bölüm {_bolum}";
		_dalgaYazi.Text = $"Dalga {_dalga}/{_dalgaSayisi}";
		_canYazi.Text = $"♥ {_can}";
		_altinYazi.Text = $"◎ {_altin}";
		if (_kulePanel.Visible && _seciliKule != null) KuleBilgiYaz(_seciliKule);
		if (_insaPanel.Visible) InsaListesiYaz();
	}

	void PanelleriKapat()
	{
		_insaPanel.Visible = false;
		_kulePanel.Visible = false;
		_seciliYer = -1;
		_seciliKule = null;
	}

	void InsaPaneliAc(int yer)
	{
		PanelleriKapat();
		_seciliYer = yer;
		_insaPanel.Position = new Vector2(_yerler[yer].X > 640 ? 10 : 880, 66);
		InsaListesiYaz();
		_insaPanel.Visible = true;
	}

	void InsaListesiYaz()
	{
		foreach (var c in _insaListe.GetChildren()) c.QueueFree();
		foreach (var t in TdVeri.Kuleler)
		{
			bool acik = t.Level <= _enUst;
			string satir1 = acik ? $"{t.Ad}   ◎{t.Maliyet}" : $"🔒 {t.Ad}  (Bölüm {t.Level})";
			string satir2 = $"{TdVeri.HasarTr[t.HasarTuru]} · {ModAd(t.Mod)}";
			if (acik && t.Etkili.Length > 0) satir2 += $"\nEtkili: {string.Join(", ", t.Etkili)}";
			var b = Dugme(satir1 + "\n" + satir2, 16);
			b.Alignment = HorizontalAlignment.Left;
			b.CustomMinimumSize = new Vector2(350, 0);
			b.Disabled = !acik || _altin < t.Maliyet;
			if (acik) b.AddThemeColorOverride("font_color", TdVeri.HasarRenk[t.HasarTuru].Lightened(0.3f));
			if (t.Doku != null && acik) { b.Icon = t.Doku; b.ExpandIcon = false; b.AddThemeConstantOverride("icon_max_width", 44); }
			var tt = t;
			b.Pressed += () => { if (_seciliYer >= 0) KuleKur(_seciliYer, tt); };
			_insaListe.AddChild(b);
		}
	}

	static string ModAd(string mod) => mod switch
	{
		"tek" => "tek hedef", "alan" => "alan hasarı", "zincir" => "zincirleme", "yavas" => "yavaşlatır", "zehir" => "zehirler",
		"delici" => "delip geçer", "ucan_avci" => "uçanlara x2", "aura" => "çevresine sürekli", "yakici" => "yakar", "sersem" => "sersemletir", _ => mod
	};

	void KulePaneliAc(Kule k)
	{
		PanelleriKapat();
		_seciliKule = k;
		_kulePanel.Position = new Vector2(k.P.X > 640 ? 10 : 880, 66);
		KuleBilgiYaz(k);
		_kulePanel.Visible = true;
	}

	void KuleBilgiYaz(Kule k)
	{
		_kuleBaslik.Text = $"{k.T.Ad}  {new string('★', k.Sv)}";
		var t = k.T;
		string bilgi = $"Hasar: {k.Hasar:0} {TdVeri.HasarTr[t.HasarTuru]}  ·  {ModAd(t.Mod)}\nMenzil: {k.Menzil:0}   Atış/sn: {k.Hiz:0.0}";
		if (t.Etkili.Length > 0) bilgi += $"\nEtkili: {string.Join(", ", t.Etkili)}";
		if (t.Etkisiz.Length > 0) bilgi += $"\nZayıf kaldığı: {string.Join(", ", t.Etkisiz)}";
		_kuleBilgi.Text = bilgi;
		_yukseltButon.Text = k.Sv >= 5 ? "En üst seviye" : $"Yükselt ◎{k.YukseltBedel}";
		_yukseltButon.Disabled = k.Sv >= 5 || _altin < k.YukseltBedel;
		_satButon.Text = $"Sat +{(int)(k.Harcanan * 0.6f)}";
	}

	void Mesaj(string metin, Color renk, float sure)
	{
		_mesaj.Text = metin;
		_mesaj.AddThemeColorOverride("font_color", renk);
		_mesaj.Visible = true;
		_mesajSure = sure;
	}

	// ================================================================ ÖNİZLEME (bulutta ekran görüntüsü)
	async System.Threading.Tasks.Task OnizlemeCek(string klasor)
	{
		DirAccess.MakeDirRecursiveAbsolute(klasor);
		_enUst = 14;
		MenuyuGoster();
		await Bekle(0.8f);
		await Kare(klasor + "/1_menu.png");
		BolumBaslat(12);
		_altin = 99999;
		var acik = AcikKuleler.ToList();
		for (int i = 0; i < _yerler.Count; i++)
		{
			KuleKur(i, acik[(i * 5) % acik.Count]);
			var k = _kuleler[^1];
			for (int s = 0; s < i % 4; s++) Yukselt(k);
		}
		PanelleriKapat();
		await Bekle(1f);
		await Kare(klasor + "/2_harita.png");
		for (int d = 0; d < 3; d++)
		{
			DalgaBaslat();
			Engine.TimeScale = 2;
			await Bekle(d == 0 ? 6f : 4f);
		}
		await Kare(klasor + "/3_savas.png");
		KulePaneliAc(_kuleler[2]);
		await Bekle(0.4f);
		await Kare(klasor + "/4_kule_panel.png");
		Sat(_kuleler[0]);
		InsaPaneliAc(0);
		await Bekle(0.4f);
		await Kare(klasor + "/5_insa.png");
		PanelleriKapat();
		var boss = TdVeri.Dusmanlar.FirstOrDefault(d => d.Boss && d.Level == _bolum);
		if (boss != null && _ekran == Ekran.Oyun) { DusmanDogur(boss); _dusmanlar[^1].Mesafe = 700; }
		Engine.TimeScale = 1;
		await Bekle(2.5f);
		await Kare(klasor + "/6_boss.png");
		GD.Print($"ONIZLEME bölüm={_bolum} dalga={_dalga}/{_dalgaSayisi} can={_can} düşman={_dusmanlar.Count} kule={_kuleler.Count} doku_kule={TdVeri.Kuleler.Count(k => k.Doku != null)} doku_dusman={TdVeri.Dusmanlar.Count(d => d.Doku != null)}");
		GD.Print("ONIZLEME bitti");
		GetTree().Quit();
	}

	async System.Threading.Tasks.Task Bekle(float sn) =>
		await ToSignal(GetTree().CreateTimer(sn, true, false, true), SceneTreeTimer.SignalName.Timeout);

	async System.Threading.Tasks.Task Kare(string yol)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(yol);
		GD.Print("ONIZLEME kaydedildi: ", yol);
	}
}

/// <summary>Boş kule yerlerini ve seçili kulenin menzilini çizer.</summary>
public partial class YerCizer : Node2D
{
	public KuleSavunmasi Oyun;
	float _t;
	public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }
	public override void _Draw()
	{
		if (Oyun == null || !Oyun.OyunEkrani) return;
		if (Oyun.SeciliKuleKonum is Vector2 kp)
		{
			DrawCircle(kp, Oyun.SeciliMenzil, new Color(1f, 0.85f, 0.3f, 0.07f));
			DrawArc(kp, Oyun.SeciliMenzil, 0, Mathf.Tau, 64, new Color(1f, 0.85f, 0.3f, 0.6f), 2);
		}
		for (int i = 0; i < Oyun.Yerler.Count; i++)
		{
			var p = Oyun.Yerler[i];
			if (Oyun.DoluMu(p)) continue;
			bool secili = Oyun.SeciliYer == i;
			float n = 0.5f + 0.5f * Mathf.Sin(_t * 3 + i);
			DrawCircle(p, 26, new Color(0.08f, 0.06f, 0.05f, 0.85f));
			DrawArc(p, 26, 0, Mathf.Tau, 32, secili ? new Color(1f, 0.85f, 0.3f) : new Color(0.55f, 0.9f, 0.45f, 0.35f + 0.3f * n), secili ? 4 : 2);
			DrawLine(p + new Vector2(-8, 0), p + new Vector2(8, 0), new Color(0.6f, 0.9f, 0.5f, 0.6f), 3);
			DrawLine(p + new Vector2(0, -8), p + new Vector2(0, 8), new Color(0.6f, 0.9f, 0.5f, 0.6f), 3);
		}
	}
}
