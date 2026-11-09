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
		public int Harcanan;
		public int[] Dal = new int[2];
		public List<YetenekDugum>[] Agac;
		public readonly HashSet<string> Ozel = new();
		public float HasarC = 1, MenzilC = 1, HizC = 1, ZirhDelme;
		public Vector2 P;
		public Node2D Kok;
		public Sprite2D Spr;
		public Label Yildiz;
		public float Bekleme;
		public float Hasar => T.Hasar * HasarC;
		public float Menzil => T.Menzil * MenzilC;
		public float Hiz => T.AtisHizi * HizC;
		public int Kademe => Dal[0] + Dal[1];
		public bool Var(string o) => Ozel.Contains(o);
		public float Dps => Hasar * Hiz * (T.Mod is "alan" or "zincir" or "aura" or "delici" ? 2.2f : 1f) * (Var("kritik") ? 1.5f : 1f) * (Var("cift") ? 2f : 1f) * (Var("suru") ? 3f : 1f);
	}

	class Dus
	{
		public DusmanTanim T;
		public Node2D Kok, Govde;
		public Sprite2D Spr;
		public ShaderMaterial Mat;
		public ColorRect CanArka, CanOn;
		public float Can, CanMax, Hiz, Mesafe, Faz, Boy, Odul;
		public float Yavas, YavasSure, ZehirDps, ZehirSure, YanmaDps, YanmaSure, Sersem, KirilganSure;
		public bool Salgin, KulPatlar;
		public string ZehirTur = "Zehir", YanmaTur = "Ates";
		public CpuParticles2D Ates;
		public bool Olu;
		public Vector2 Merkez => Kok.Position + new Vector2(0, -Boy * 0.5f + (T.Ucan ? -26 : 0));
	}

	// ================================================================ YÜRÜME
	// Sprite'lar tek kare; bacaklar parça shader'ıyla oynatılır: kalçanın altı
	// sol/sağ ikiye bölünüp adım fazına göre açılıp kapanır, öndeki ayak hafif kalkar.
	const string YurumeKod = @"shader_type canvas_item;
uniform float sol_kalk = 0.0;
uniform float sag_kalk = 0.0;
uniform float kalca = 0.62;
uniform float orta = 0.5;
uniform float kanat = 0.0;
uniform float faz = 0.0;
void fragment() {
	vec2 uv = UV;
	// kalçadan ayağa doğru artan etki: kalkan bacak kısalır, ayak yerden kesilir
	float t = clamp((uv.y - kalca) / (1.0 - kalca), 0.0, 1.0);
	float kalk = uv.x < orta ? sol_kalk : sag_kalk;
	uv.y += kalk * t;
	// kanat çırpma (uçanlar)
	float k = clamp((kalca - uv.y) / kalca, 0.0, 1.0);
	uv.x = orta + (uv.x - orta) / (1.0 + kanat * sin(faz) * k);
	uv = (floor(uv / TEXTURE_PIXEL_SIZE) + 0.5) * TEXTURE_PIXEL_SIZE;
	vec4 c = texture(TEXTURE, uv);
	if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) c.a = 0.0;
	COLOR = c * COLOR;
}";
	static Shader _yurume;
	static readonly Dictionary<Texture2D, Vector2> _bacak = new();
	static readonly StringName FazAd = "faz", SolAd = "sol_kalk", SagAd = "sag_kalk", KanatAd = "kanat";

	/// <summary>Sprite'ın ayak hizasındaki dolu piksellerin ortasını (orta) ve kalça yüksekliğini bulur.</summary>
	static Vector2 BacakBul(Texture2D doku)
	{
		if (_bacak.TryGetValue(doku, out var v)) return v;
		v = new Vector2(0.5f, 0.62f);
		var im = doku.GetImage();
		if (im != null)
		{
			if (im.IsCompressed()) im.Decompress();
			int w = im.GetWidth(), h = im.GetHeight();
			int ust = h, alt = 0;
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x += 2)
					if (im.GetPixel(x, y).A > 0.5f) { ust = Math.Min(ust, y); alt = Math.Max(alt, y); }
			if (alt > ust)
			{
				float toplam = 0; int n = 0;
				int bas = alt - Math.Max(2, (alt - ust) / 6);
				for (int y = bas; y <= alt; y++)
					for (int x = 0; x < w; x++)
						if (im.GetPixel(x, y).A > 0.5f) { toplam += x; n++; }
				float orta = n > 0 ? (toplam / n + 0.5f) / w : 0.5f;
				float kalca = (ust + (alt - ust) * 0.64f) / h;
				v = new Vector2(Mathf.Clamp(orta, 0.3f, 0.7f), kalca);
			}
		}
		_bacak[doku] = v;
		return v;
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
	int _bolumXp, _bolumPara;      // bu bölümde kazanılan (kaybedilse de kalır)
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
	// uyarlanabilir zorluk: her dalga sonunda savunma analiz edilir
	float _uyum = 1f;
	int _kacanBuDalga, _olumSayisi;
	float _olumMesafeToplam;
	readonly Dictionary<string, float> _karsiAgirlik = new();
	Label _tehditYazi;

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
	Button _satButon;
	readonly Button[,] _agacButon = new Button[2, 3];
	GridContainer _bolumIzgara;
	Label _sonucBaslik, _sonucMetin;
	Button _sonucDevam;
	float _mesajSure;
	// kalıcı yetenek ağacı ekranı
	Control _agacEkrani;
	VBoxContainer _agacListe;
	GridContainer _agacDugumler;
	Label _agacBaslik, _agacBilgi, _agacUst;
	KuleTanim _agacSecili;
	HBoxContainer _devamKutu;

	readonly Dictionary<string, AudioStreamWav> _sesler = new();
	readonly List<AudioStreamPlayer> _oynaticilar = new();
	readonly Dictionary<string, float> _sesSon = new();
	// müzik: menüde ürpertici ortam, savaşta gerilim (kodla bestelendi, assets/ses)
	AudioStreamPlayer _muzik;
	readonly Dictionary<string, AudioStream> _muzikler = new();
	string _calan;
	Button _muzikDugme, _muzikDugmeMenu;
	const float MuzikDb = -9f;

	// ================================================================ BAŞLANGIÇ
	public override void _Ready()
	{
		_rng.Randomize();
		TdVeri.Yukle();
		Ilerleme.Yukle();
		foreach (var s in new[] { "ok", "ates", "patlama", "vurus", "altin", "buyu", "boru", "kale", "kalkan" }) _sesler[s] = Efekt.SesUret(s);
		for (int i = 0; i < 8; i++) { var p = new AudioStreamPlayer { VolumeDb = -8 }; AddChild(p); _oynaticilar.Add(p); }
		_muzik = new AudioStreamPlayer { VolumeDb = MuzikDb };
		AddChild(_muzik);
		foreach (var ad in new[] { "menu", "savas" })
		{
			var m = GD.Load<AudioStreamOggVorbis>($"res://assets/ses/{ad}.ogg");
			if (m != null) { m.Loop = true; _muzikler[ad] = m; }
		}

		// kamera haritanın ortasında; geniş telefonlarda harita yanlara doğru uzar
		var kamera = new Camera2D { Position = new Vector2(640, 360) };
		AddChild(kamera);
		kamera.MakeCurrent();
		HaritaUret(BolumTohum(Math.Max(1, Ilerleme.EnUstBolum)));
		_harita = new TdHarita { ZIndex = -10 };
		AddChild(_harita);
		_dunya = new Node2D { YSortEnabled = true };
		AddChild(_dunya);
		_yerler = TdHarita.Yerler;
		_yerCizer = new YerCizer { Oyun = this, ZIndex = -5 };
		AddChild(_yerCizer);

		ArayuzKur();
		EkranAyarla();
		GetViewport().SizeChanged += EkranAyarla;
		MenuyuGoster();

		foreach (var arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--onizleme="))
				_ = OnizlemeCek(arg.Substring("--onizleme=".Length));
	}

	void MuzikCal(string ad)
	{
		if (_calan == ad && _muzik.Playing) return;
		_calan = ad;
		if (!Ilerleme.MuzikAcik || !_muzikler.TryGetValue(ad, out var akis)) { _muzik.Stop(); return; }
		var tw = _muzik.CreateTween();
		if (_muzik.Playing) tw.TweenProperty(_muzik, "volume_db", -40f, 0.6f);
		tw.TweenCallback(Callable.From(() => { _muzik.Stream = akis; _muzik.VolumeDb = -40f; _muzik.Play(); }));
		tw.TweenProperty(_muzik, "volume_db", MuzikDb, 1.2f);
	}

	void MuzikDegistir()
	{
		Ilerleme.MuzikAcik = !Ilerleme.MuzikAcik;
		Ilerleme.Kaydet();
		string c = _calan; _calan = null;
		if (Ilerleme.MuzikAcik) MuzikCal(c ?? "menu"); else { _muzik.Stop(); _calan = c; }
		MuzikDugmeGuncelle();
	}

	void MuzikDugmeGuncelle()
	{
		string m = Ilerleme.MuzikAcik ? "♪ Açık" : "♪ Kapalı";
		if (_muzikDugme != null) _muzikDugme.Text = m;
		if (_muzikDugmeMenu != null) _muzikDugmeMenu.Text = "Müzik: " + m;
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
	float EkranGen => GetViewportRect().Size.X;

	/// <summary>Bölüm numarasından sabit harita tohumu: tekrar oynayınca/menüden dönünce yol değişmez.</summary>
	static ulong BolumTohum(int bolum) => (ulong)bolum * 2654435761UL + 0x5EED_1234UL;

	void HaritaUret(ulong tohum)
	{
		float g = Mathf.Max(1280f, EkranGen);
		// bölüm ilerledikçe 10 → 18 kule yeri; geniş ekranda yol uzadığı için +2
		int yer = Mathf.Clamp(10 + Math.Max(_bolum, 1) / 5, 10, 18) + (g > 1450 ? 2 : 0);
		TdHarita.Olustur(tohum, 640 - g / 2, 640 + g / 2, yer);
	}

	/// <summary>Arayüzü ekranın gerçek genişliğine yerleştirir (20:9 telefonlar dahil).</summary>
	void EkranAyarla()
	{
		float g = Mathf.Max(1280f, EkranGen);
		_ust.Size = new Vector2(g, 56);
		_menu.Size = new Vector2(g, 720);
		_agacEkrani.Size = new Vector2(g, 720);
		_dalgaButon.Position = new Vector2(g - 220, 640);
		_mesaj.Position = new Vector2((g - 1000) / 2, 250);
		_sonucPanel.Position = new Vector2((g - 600) / 2, 170);
		if (_ekran == Ekran.Menu && Mathf.Abs(TdHarita.Sag - TdHarita.Sol - g) > 1)
		{
			ulong t = BolumTohum(_bolum > 0 ? _bolum : Math.Max(1, Ilerleme.EnUstBolum));
			HaritaUret(t);
			_harita?.Yenile(t);
		}
	}

	/// <summary>Panel, seçilen noktanın karşı tarafında açılır.</summary>
	float PanelX(Vector2 dunya, float gen)
	{
		float g = Mathf.Max(1280f, EkranGen);
		return dunya.X > 640 ? 10 : g - gen - 10;
	}

	IEnumerable<KuleTanim> AcikKuleler => TdVeri.Kuleler.Where(Ilerleme.KuleAcik);

	/// <summary>XP ve kalıcı para kazanımı; seviye atlarsa yeni kuleleri duyurur.</summary>
	void Kazan(int xp, int para)
	{
		int once = Ilerleme.Seviye;
		Ilerleme.Xp += xp; Ilerleme.Para += para;
		_bolumXp += xp; _bolumPara += para;
		int sonra = Ilerleme.Seviye;
		if (sonra > once)
		{
			var yeni = TdVeri.Kuleler.Where(k => k.Level > once && k.Level <= sonra).Select(k => k.Ad).ToList();
			Mesaj($"SEVİYE {sonra}!  +{Ilerleme.PuanPerSeviye * (sonra - once)} yetenek puanı" + (yeni.Count > 0 ? $"\nYeni kule açıldı: {string.Join(", ", yeni)}" : ""),
				new Color(0.6f, 1f, 0.95f), 3.2f);
			Ses("altin");
			Ilerleme.Kaydet();
			if (_insaPanel.Visible) InsaListesiYaz();
		}
		ArayuzGuncelle();
	}

	// ================================================================ BÖLÜM AKIŞI
	void BolumBaslat(int bolum)
	{
		Temizle();
		_bolum = bolum;
		_dalga = 0;
		ulong tohum = BolumTohum(bolum);   // aynı bölüm her zaman aynı harita
		HaritaUret(tohum);
		_harita.Yenile(tohum);
		_dalgaSayisi = Math.Min(13, 5 + bolum / 5);
		_can = 20;
		_altin = 160 + bolum * 25;
		_dalgaSuruyor = false;
		_uyum = 1f;
		_karsiAgirlik.Clear();
		_ekran = Ekran.Oyun;
		_menu.Visible = false;
		_sonucPanel.Visible = false;
		_ust.Visible = true;
		_dalgaButon.Visible = true;
		_dalgaButon.Text = "▶ Dalga 1";
		_hizKat = 1; Engine.TimeScale = 1; _hizButon.Text = "Hız x1";
		ArayuzGuncelle();
		_bolumXp = 0; _bolumPara = 0;
		MuzikCal("savas");
		Mesaj($"Bölüm {bolum}", new Color(1f, 0.9f, 0.55f), 2f);
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
		_kacanBuDalga = 0; _olumSayisi = 0; _olumMesafeToplam = 0;
		KarsiSecimHesapla();
		var havuz = TdVeri.Dusmanlar.Where(d => !d.Boss && d.Level <= _bolum).ToList();
		// her dalga daha kalabalık; savunma güçlüyse daha da kalabalık
		int adet = Mathf.RoundToInt((5 + _bolum * 0.5f + _dalga * 2.5f) * Mathf.Lerp(1f, _uyum, 0.5f));
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
			foreach (var boss in BolumBosslari(_bolum)) _dogacak.Enqueue(boss);
		}
		// her dalgada düşmanlar daha sık gelir
		_dogmaAralik = Mathf.Max(0.22f, (0.95f - _bolum * 0.012f) * (1f - 0.06f * (_dalga - 1)) / Mathf.Sqrt(_uyum));
		_dogmaSayac = 0.3f;
		Ses("boru");
		Mesaj($"Dalga {_dalga}/{_dalgaSayisi}", new Color(1f, 0.55f, 0.4f), 1.5f);
		ArayuzGuncelle();
	}

	/// <summary>Çift bölümlerde boss; içerik bitince bosslar sırayla döner, her 10 bölümde iki boss.</summary>
	List<DusmanTanim> BolumBosslari(int bolum)
	{
		var l = new List<DusmanTanim>();
		var tum = TdVeri.Dusmanlar.Where(d => d.Boss).OrderBy(d => d.Level).ToList();
		if (tum.Count == 0) return l;
		var tam = tum.FirstOrDefault(d => d.Level == bolum);
		if (tam != null) l.Add(tam);
		else if (bolum % 2 == 0 && bolum > tum[^1].Level) l.Add(tum[(bolum / 2) % tum.Count]);
		if (bolum > tum[^1].Level && bolum % 10 == 0) l.Add(tum[(bolum / 2 + 7) % tum.Count]);
		return l;
	}

	float Agirlik(DusmanTanim d) =>
		(1f + (d.Level >= _bolum - 2 ? 3f : 0f)) * (_karsiAgirlik.TryGetValue(d.Zirh, out var k) ? k : 1f);

	/// <summary>Kulelerin hasar türü dağılımına bakar; bu karışıma dirençli zırhlar daha sık seçilir.</summary>
	void KarsiSecimHesapla()
	{
		_karsiAgirlik.Clear();
		var pay = new Dictionary<string, float>();
		float top = 0;
		foreach (var k in _kuleler) { pay[k.T.HasarTuru] = pay.GetValueOrDefault(k.T.HasarTuru) + k.Dps; top += k.Dps; }
		if (top <= 0) return;
		foreach (var z in TdVeri.Carpan.Keys)
		{
			float etki = 0;
			foreach (var (h, v) in pay) etki += v / top * TdVeri.HasarCarpani(z, h);
			// etki 1'den düşükse (savunma bu zırha karşı zayıfsa) o zırh daha sık gelir
			_karsiAgirlik[z] = Mathf.Clamp(1f + 1.2f * (1f - etki) * Mathf.Clamp(_uyum - 0.6f, 0.2f, 1.2f), 0.5f, 2.5f);
		}
	}

	float DalgaCanCarpani => 1f + 0.1f * (_dalga - 1);

	/// <summary>
	/// Dalga sonu analizi: düşmanlar yolun neresinde ölüyor, kaç tanesi kaçtı, kulelerin toplam gücü ne?
	/// Savunma rahatsa düşmanlar güçlenir/çoğalır/hızlanır; zorlanıyorsa biraz geri çekilir.
	/// </summary>
	void DalgaAnalizi()
	{
		float onceki = _uyum;
		float ort = _olumSayisi > 0 ? _olumMesafeToplam / _olumSayisi : 1f;
		// zorluk sadece artar: savunma rahatsa düşmanlar güçlenir, zorlanıyorsa sabit kalır
		if (_kacanBuDalga == 0 && ort < 0.3f) _uyum *= 1.08f;
		else if (_kacanBuDalga == 0 && ort < 0.5f) _uyum *= 1.04f;
		// kulelere yapılan yatırım: saniyede verilebilecek hasar / bir sonraki dalganın gerektirdiği hasar
		float guc = _kuleler.Sum(k => k.Dps);
		int sonrakiAdet = Mathf.RoundToInt(5 + _bolum * 0.5f + (_dalga + 1) * 2.5f);
		float gereken = TdVeri.TemelCan(_bolum) * (1f + 0.1f * _dalga) * _uyum * sonrakiAdet
			/ (sonrakiAdet * _dogmaAralik + TdHarita.Uzunluk / 46f);
		float oran = gereken > 0 ? guc / gereken : 1f;
		if (_kacanBuDalga == 0 && oran > 2.5f) _uyum *= 1.03f;
		_uyum = Mathf.Clamp(_uyum, 1f, 3f);
		KarsiSecimHesapla();

		// zorluk sessizce ayarlanır; oyuncuya mesaj gösterilmez
		ArayuzGuncelle();
	}

	void DusmanDogur(DusmanTanim t)
	{
		var d = new Dus { T = t };
		float can = TdVeri.TemelCan(_bolum) * (0.8f + 0.6f * t.Guc) * DalgaCanCarpani * _uyum;
		float hiz = 46f;
		if (t.Ozel.Contains("hizli")) { hiz = 70f; can *= 0.8f; }
		if (t.Ozel.Contains("yavas")) { hiz = 30f; can *= 1.8f; }
		if (t.Ozel.Contains("kalkan")) can *= 1.3f;
		if (t.Ozel.Contains("kalabalik")) { hiz = 54f; can *= 0.45f; }
		if (t.Ucan) { hiz = 58f; can *= 0.85f; }
		if (t.Zirh == "Hayalet") hiz = 50f;
		if (t.Boss) { hiz = 27f; can *= 16f; }
		// Kingdom Rush tarzı aile rolleri: kalabalık/zayıf, tank/yavaş, hızlı, sürü...
		var (rc, rh) = Rol(t.Zirh);
		can *= rc; hiz *= rh;
		// uzun yollarda yürüyüş süresi makul kalsın
		hiz *= Mathf.Clamp(TdHarita.Uzunluk / 3400f, 1f, 1.6f);
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
		bool kareli = t.Yuru != null && !t.Ucan && t.Zirh != "Hayalet";
		if (kareli)
		{
			// gerçek yürüme kareleri: 4 kare yan yana
			d.Spr.Texture = t.Yuru;
			d.Spr.Hframes = 4;
			d.Spr.Frame = 1;
			float yo = d.Boy / t.Yuru.GetHeight();
			d.Spr.Scale = Vector2.One * yo;
			d.Spr.Offset = new Vector2(0, -t.Yuru.GetHeight() * 0.5f);
		}
		else if (t.Doku != null)
		{
			_yurume ??= new Shader { Code = YurumeKod };
			var b = BacakBul(t.Doku);
			d.Mat = new ShaderMaterial { Shader = _yurume };
			d.Mat.SetShaderParameter("orta", b.X);
			d.Mat.SetShaderParameter("kalca", b.Y);
			d.Spr.Material = d.Mat;
		}
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

	/// <summary>Zırh tipine göre (can, hız) çarpanı.</summary>
	static (float, float) Rol(string z) => z switch
	{
		"Olumsuz" => (1.0f, 0.95f),   // piyade: ortalama, kalabalık gelir
		"Hayalet" => (0.8f, 1.1f),    // fiziksele dirençli, hızlıca süzülür
		"Canavar" => (0.9f, 1.35f),   // hızlı koşucu
		"Zirhli" => (1.7f, 0.75f),    // tank: yavaş, dayanıklı
		"Seytan" => (1.25f, 1.0f),
		"Vampir" => (1.1f, 1.05f),    // kendini iyileştirir
		"Bocek" => (0.55f, 1.2f),     // sürü: zayıf ama çok
		"Tas" => (2.1f, 0.6f),        // dev tank
		"Buz" => (1.2f, 0.9f),
		"Bataklik" => (1.1f, 0.85f),
		"Kultist" => (0.85f, 1.0f),
		"Ucan" => (0.8f, 1.15f),      // sadece bazı kuleler vurur
		_ => (1f, 1f),
	};

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
			DalgaAnalizi();
			Kazan(6 + _bolum, 3 + _bolum / 3);
			Ilerleme.Kaydet();
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
			if (d.KirilganSure > 0) d.KirilganSure -= dt;
			float hiz = d.Hiz * (1f - d.Yavas);
			if (d.Sersem > 0) { d.Sersem -= dt; hiz = 0; }
			d.Mesafe += hiz * dt;
			d.Faz += dt * hiz / 46f;
			var p = TdHarita.Konum(Mathf.Max(0, d.Mesafe), out var yon);
			d.Kok.Position = p;
			if (Mathf.Abs(yon.X) > 0.1f) d.Spr.FlipH = yon.X < 0;
			// yürüme / süzülme
			bool suzulur = d.T.Ucan || d.T.Zirh == "Hayalet";
			// klasik 4 karelik yürüyüş: sol ayak kalkık → duruş → sağ ayak kalkık → duruş
			// (yumuşak sinüs yerine kare kare; sallanma yok)
			int kare = ((int)Mathf.Floor(d.Faz * 8 / (Mathf.Pi / 2)) % 4 + 4) % 4;
			if (suzulur)
			{
				d.Spr.Rotation = 0;
				d.Spr.Position = new Vector2(0, Mathf.Sin(d.Faz * 2.5f) * 5);
			}
			else
			{
				bool adimda = kare == 0 || kare == 2;
				d.Spr.Rotation = (d.Spr.FlipH ? -1f : 1f) * 0.04f;     // yürüme yönüne hafif eğik
				if (d.Spr.Hframes == 4)
				{
					d.Spr.Frame = hiz > 0 ? kare : 1;
					d.Spr.Position = new Vector2(0, adimda ? 0f : -1.5f); // kareler zaten basışta çöküyor
				}
				else
					d.Spr.Position = new Vector2(0, adimda ? -2f : 0f);   // adımda gövde 2 px yükselir
			}
			if (d.Mat != null)
			{
				float g = suzulur || hiz <= 0 ? 0f : (!d.T.Boss && d.T.Ozel.Contains("kalabalik") ? 0.09f : 0.12f);
				d.Mat.SetShaderParameter(SolAd, kare == 0 ? g : 0f);
				d.Mat.SetShaderParameter(SagAd, kare == 2 ? g : 0f);
				d.Mat.SetShaderParameter(KanatAd, d.T.Ucan ? 0.12f : 0f);
				d.Mat.SetShaderParameter(FazAd, d.Faz * 3f);
			}
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
				_kacanBuDalga += kayip;
				Ses("kale");
				Efekt.Yazi(_dunya, TdHarita.Cikis, $"-{kayip} ♥", new Color(1f, 0.3f, 0.3f), 30);
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
		if (k != null && c < 1f) c = Mathf.Lerp(c, 1f, k.ZirhDelme);
		float h = miktar * c;
		if (d.T.Ozel.Contains("kalkan") && tur == "Fiziksel" && (k == null || k.ZirhDelme <= 0)) h *= 0.7f;
		if (d.KirilganSure > 0) h *= 1.3f;
		if (k != null && k.Var("yakici_isin") && d.T.Boss) h *= 2f;
		bool kritik = vurusEfekti && k != null && k.Var("kritik") && _rng.Randf() < 0.25f;
		if (kritik) { h *= 3f; Efekt.Yazi(_dunya, d.Merkez + new Vector2(0, -14), "KRİTİK", new Color(1f, 0.4f, 0.2f), 18); }
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
		if (d.Can <= 0) Oldur(d, k);
	}

	void Oldur(Dus d, Kule katil = null)
	{
		if (d.Olu) return;
		_altin += Mathf.Max(1, Mathf.RoundToInt(d.Odul * (katil != null && katil.Var("ruh_hasadi") ? 1.6f : 1f)));
		Kazan(d.T.Boss ? 30 + _bolum * 3 : 1 + _bolum / 4, d.T.Boss ? 15 + _bolum : (_rng.Randf() < 0.35f ? 1 : 0));
		_olumSayisi++;
		_olumMesafeToplam += Mathf.Clamp(d.Mesafe / TdHarita.Uzunluk, 0, 1);
		var m = d.Merkez;
		// Salgın: zehir çevreye yayılır · Kül Patlaması: yanan düşman patlar
		if (d.Salgin && d.ZehirSure > 0)
		{
			Efekt.Yayici(_dunya, m, new[] { new Color(0.5f, 1f, 0.3f, 0.8f), new Color(0.2f, 0.5f, 0.1f, 0f) }, 18, 0.7f, 40, 110, 0.5f, 1f, true, false, 0f);
			foreach (var e in _dusmanlar.Where(e => e != d && !e.Olu && e.Kok.Position.DistanceTo(d.Kok.Position) < 75))
			{ Zehirle(e, d.ZehirDps, 4f, d.ZehirTur); e.Salgin = true; }
		}
		if (d.KulPatlar && d.YanmaSure > 0)
		{
			Efekt.Patlama(_dunya, m, 0.6f);
			foreach (var e in _dusmanlar.Where(e => e != d && !e.Olu && e.Kok.Position.DistanceTo(d.Kok.Position) < 60).ToList())
				HasarVer(e, d.YanmaDps * 2.5f, d.YanmaTur, true);
		}
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
					if (!d.Olu && (k.T.HasarTuru == "Buz" || k.Var("genis_aura"))) Yavaslat(d, k.T.HasarTuru == "Buz" ? 0.35f : 0.25f, 1.2f);
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
				default:
					MermiAt(k, hedef, cikis);
					int ek = k.Var("cift") ? 1 : k.Var("suru") ? 2 : 0;
					foreach (var d2 in _dusmanlar.Where(d => d != hedef && !d.Olu && d.Kok.Position.DistanceTo(k.P) <= k.Menzil
						&& !(k.T.HasarTuru == "Kusatma" && d.T.Ucan)).OrderByDescending(d => d.Mesafe).Take(ek).ToList())
						MermiAt(k, d2, cikis);
					break;
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
				float yaricap = (48f + k.Kademe * 4) * (k.Var("buyuk_alan") ? 1.6f : 1f);
				if (tur == "Ates" || tur == "Kusatma") Efekt.Patlama(_dunya, nokta, 0.5f);
				else if (tur == "Golge") Efekt.MorPatlama(_dunya, nokta, 0.7f);
				else Efekt.Parlama(_dunya, nokta, new Color(renk, 0.8f), yaricap / 14f, 0.35f);
				foreach (var d in _dusmanlar.ToList())
					if (d.Kok.Position.DistanceTo(nokta) <= yaricap + 10 && !(tur == "Kusatma" && d.T.Ucan))
					{
						HasarVer(d, k.Hasar, tur, d == hedef, k);
						if (!d.Olu && tur == "Buz") Yavaslat(d, 0.35f, 1.5f);
						if (!d.Olu && tur == "Zehir") Zehirle(d, k.Hasar * 0.4f, 3f, tur);
						if (!d.Olu && k.Var("sarsinti")) d.Sersem = Mathf.Max(d.Sersem, d.T.Boss ? 0.1f : 0.35f);
					}
				return;
			case "yakici":
				bool cehennem = k.Var("cehennem");
				Efekt.Patlama(_dunya, nokta, cehennem ? 0.5f : 0.3f);
				foreach (var d in _dusmanlar.ToList())
					if (d.Kok.Position.DistanceTo(nokta) <= (cehennem ? 62 : 40))
					{
						HasarVer(d, k.Hasar, tur, d == hedef, k);
						if (!d.Olu && TdVeri.HasarCarpani(d.T.Zirh, tur) > 0)
						{
							d.YanmaDps = Mathf.Max(d.YanmaDps * (d.YanmaSure > 0 ? 1 : 0), k.Hasar * (cehennem ? 1.4f : 0.7f));
							d.YanmaSure = 3f; d.YanmaTur = tur;
							if (k.Var("kul")) d.KulPatlar = true;
							d.Ates ??= Efekt.Ates(_dunya, d.Merkez, 0.4f, 10);
						}
					}
				return;
		}
		if (hedef == null || hedef.Olu) return;
		Efekt.Kivilcim(_dunya, nokta, renk);
		float carp = k.T.Mod == "ucan_avci" && hedef.T.Ucan ? (k.Var("avci") ? 3f : 2f) : 1f;
		HasarVer(hedef, k.Hasar * carp, tur, true, k);
		if (hedef.Olu) return;
		switch (k.T.Mod)
		{
			case "yavas":
				Yavaslat(hedef, k.Var("donduran") ? 0.7f : tur == "Buz" ? 0.45f : 0.5f, 2f);
				if (k.Var("kirilgan")) hedef.KirilganSure = 2f;
				if (tur == "Zehir") Zehirle(hedef, k.Hasar * 0.6f, 3f, tur);
				break;
			case "zehir":
				Zehirle(hedef, k.Hasar * 0.9f * (k.Var("olumcul") ? 2f : 1f), 4f, tur);
				if (k.Var("salgin")) hedef.Salgin = true;
				break;
			case "sersem":
				hedef.Sersem = (hedef.T.Boss ? 0.15f : 0.55f) * (k.Var("uzun_sersem") ? 2f : 1f);
				Efekt.Parlama(_dunya, hedef.Merkez, new Color(0.7f, 0.4f, 1f, 0.7f), 1.4f, 0.3f);
				break;
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
		for (int i = 0; i < 3 + k.Kademe / 2 + (k.Var("ag") ? 4 : 0); i++)
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
		foreach (var d in vurulan) { HasarVer(d, h, k.T.HasarTuru, true, k); if (!k.Var("asiri_yuk")) h *= 0.75f; }
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
		bool genis = k.Var("genis_isin");
		var l = new Line2D { Points = new[] { cikis, bitis }, Width = genis ? 14 : 7, DefaultColor = renk, ZIndex = 25, Material = Efekt.Toplama };
		AddChild(l);
		var tw = l.CreateTween().SetParallel(true);
		tw.TweenProperty(l, "width", 1f, 0.22f);
		tw.TweenProperty(l, "modulate:a", 0f, 0.22f);
		tw.Chain().TweenCallback(Callable.From(l.QueueFree));
		foreach (var d in _dusmanlar.ToList())
		{
			var c = d.Merkez;
			float t = Mathf.Clamp((c - cikis).Dot(bitis - cikis) / (bitis - cikis).LengthSquared(), 0, 1);
			if (c.DistanceTo(cikis + (bitis - cikis) * t) < (genis ? 34 : 18)) HasarVer(d, k.Hasar, k.T.HasarTuru, true, k);
		}
	}

	// ------------------------------------------------------------ kule kurma
	void KuleKur(int yer, KuleTanim t)
	{
		int bedel = Ilerleme.Maliyet(t);
		if (!Ilerleme.KuleAcik(t) || _altin < bedel || _kuleler.Any(k => k.P == _yerler[yer])) return;
		_altin -= bedel;
		var k = new Kule { T = t, P = _yerler[yer], Harcanan = bedel, Agac = YetenekAgaci.Olustur(t) };
		var bon = Ilerleme.Bonus(t);
		k.HasarC += bon.hasar; k.MenzilC += bon.menzil; k.HizC += bon.hiz; k.ZirhDelme = bon.zirh;
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

	void YildizGuncelle(Kule k)
	{
		k.Yildiz.Text = new string('★', k.Kademe);
		k.Yildiz.AddThemeColorOverride("font_color", k.Dal[0] >= k.Dal[1] ? new Color(1f, 0.7f, 0.3f) : new Color(0.55f, 0.85f, 1f));
	}

	/// <summary>Yetenek ağacında bir dalın sonraki kademesini alır.</summary>
	void DalAl(Kule k, int dal)
	{
		if (!YetenekAgaci.Alinabilir(k.Dal, dal)) return;
		var n = k.Agac[dal][k.Dal[dal]];
		if (_altin < n.Bedel) return;
		_altin -= n.Bedel;
		k.Harcanan += n.Bedel;
		k.Dal[dal]++;
		k.HasarC += n.Hasar; k.MenzilC += n.Menzil; k.HizC += n.Hiz;
		k.ZirhDelme = Mathf.Max(k.ZirhDelme, n.ZirhDelme);
		if (n.Ozel != null)
		{
			k.Ozel.Add(n.Ozel);
			if (n.Ozel is "yogun_aura") k.HasarC += 0.75f;
			Mesaj($"{k.T.Ad}: {n.Ad}!\n{n.Aciklama}", new Color(1f, 0.85f, 0.35f), 2.2f);
		}
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
		int onceki = Ilerleme.EnUstBolum;
		Kazan(40 + 10 * _bolum, 25 + 6 * _bolum);
		if (_bolum + 1 > Ilerleme.EnUstBolum) Ilerleme.EnUstBolum = _bolum + 1;
		Ilerleme.Kaydet();
		var yeniDus = TdVeri.Dusmanlar.Count(d => d.Level > onceki && d.Level <= Ilerleme.EnUstBolum);
		string metin = $"Kazanılan: +{_bolumXp} XP   +{_bolumPara} para\nOyuncu seviyesi {Ilerleme.Seviye}   ·   Yetenek puanı {Ilerleme.Puan}";
		if (yeniDus > 0) metin += $"\n\nSıradaki bölümde yeni düşmanlar: {yeniDus}";
		SonucGoster("ZAFER!", metin, $"Bölüm {_bolum + 1} ▶", true);
		Ses("altin");
	}

	void Kaybedildi()
	{
		_ekran = Ekran.Bitti;
		Engine.TimeScale = 1;
		Ilerleme.Kaydet();
		SonucGoster("SAVUNMA DÜŞTÜ", $"Dalga {_dalga}/{_dalgaSayisi}\nKazandıkların sende kaldı: +{_bolumXp} XP   +{_bolumPara} para\n\nYetenek Ağacı'nda kulelerini güçlendir, sonra tekrar dene.", "Tekrar Dene", false);
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

	static StyleBoxFlat _sNormal, _sHover, _sBasili, _sKapali, _sSecili;
	static Button Dugme(string metin, int boyut = 22)
	{
		_sNormal ??= Kutu(new Color(0.18f, 0.12f, 0.24f), 8, new Color(0.4f, 0.3f, 0.5f));
		_sHover ??= Kutu(new Color(0.26f, 0.17f, 0.34f), 8, new Color(0.55f, 0.4f, 0.65f));
		_sBasili ??= Kutu(new Color(0.12f, 0.3f, 0.15f), 8, new Color(0.4f, 0.9f, 0.4f));
		_sKapali ??= Kutu(new Color(0.12f, 0.11f, 0.13f), 8, new Color(0.22f, 0.2f, 0.24f));
		var b = new Button { Text = metin, FocusMode = Control.FocusModeEnum.None };
		b.AddThemeFontSizeOverride("font_size", boyut);
		b.AddThemeStyleboxOverride("normal", _sNormal);
		b.AddThemeStyleboxOverride("hover", _sHover);
		b.AddThemeStyleboxOverride("pressed", _sBasili);
		b.AddThemeStyleboxOverride("disabled", _sKapali);
		return b;
	}

	static void MetinAyarla(Button b, string m) { if (b.Text != m) b.Text = m; }

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
		_tehditYazi = Yazi("", 20, new Color(0.95f, 0.5f, 0.9f));
		foreach (var l in new[] { _bolumYazi, _dalgaYazi, _canYazi, _altinYazi, _tehditYazi }) satir.AddChild(l);
		satir.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		_hizButon = Dugme("Hız x1", 18);
		_hizButon.Pressed += () =>
		{
			_hizKat = _hizKat % 3 + 1;
			Engine.TimeScale = _hizKat;
			_hizButon.Text = $"Hız x{_hizKat}";
		};
		_muzikDugme = Dugme("♪", 18);
		_muzikDugme.Pressed += MuzikDegistir;
		satir.AddChild(_muzikDugme);
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
		_mesaj.Position = new Vector2(140, 250);
		_mesaj.Size = new Vector2(1000, 180);
		_mesaj.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_mesaj.AddThemeFontSizeOverride("font_size", 30);
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
		_kulePanel = new PanelContainer { Position = new Vector2(860, 66), Size = new Vector2(410, 560) };
		_kulePanel.AddThemeStyleboxOverride("panel", Kutu(new Color(0.07f, 0.05f, 0.1f, 0.95f), 12, new Color(0.35f, 0.25f, 0.45f)));
		var kk = new VBoxContainer();
		kk.AddThemeConstantOverride("separation", 8);
		_kuleBaslik = Yazi("", 24, new Color(1f, 0.85f, 0.4f));
		_kuleBilgi = Yazi("", 17, new Color(0.88f, 0.86f, 0.95f));
		_kuleBilgi.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_kuleBilgi.CustomMinimumSize = new Vector2(360, 0);
		// yetenek ağacı: iki sütun (Güç / Ustalık), her biri 3 kademe
		var agac = new HBoxContainer();
		agac.AddThemeConstantOverride("separation", 8);
		for (int dal = 0; dal < 2; dal++)
		{
			var sutun = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			sutun.AddThemeConstantOverride("separation", 6);
			var dalBaslik = Yazi(YetenekAgaci.DalAd[dal], 18, dal == 0 ? new Color(1f, 0.65f, 0.3f) : new Color(0.55f, 0.85f, 1f));
			dalBaslik.HorizontalAlignment = HorizontalAlignment.Center;
			sutun.AddChild(dalBaslik);
			for (int kd = 0; kd < 3; kd++)
			{
				var b = Dugme("", 14);
				b.CustomMinimumSize = new Vector2(188, 74);
				b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				int d0 = dal;
				b.Pressed += () => { if (_seciliKule != null) DalAl(_seciliKule, d0); };
				_agacButon[dal, kd] = b;
				sutun.AddChild(b);
			}
			agac.AddChild(sutun);
		}
		var kural = Yazi("Bir dal 3. kademeye çıkabilir, diğeri en fazla 1'de kalır.", 13, new Color(0.65f, 0.6f, 0.75f));
		kural.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		kural.CustomMinimumSize = new Vector2(380, 0);
		_satButon = Dugme("Sat", 18);
		_satButon.Pressed += () => { if (_seciliKule != null) Sat(_seciliKule); };
		kk.AddChild(_kuleBaslik); kk.AddChild(_kuleBilgi); kk.AddChild(agac); kk.AddChild(kural); kk.AddChild(_satButon);
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
			if (kazandi) BolumBaslat(_bolum + 1);
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
		var alt = Yazi($"Mezarlık yolunu koru · {TdVeri.Kuleler.Count} kule · {TdVeri.Dusmanlar.Count} düşman · sonsuz bölüm", 20, new Color(0.75f, 0.7f, 0.9f));
		alt.HorizontalAlignment = HorizontalAlignment.Center;
		_menuOzet = Yazi("", 18, new Color(1f, 0.85f, 0.4f));
		_menuOzet.HorizontalAlignment = HorizontalAlignment.Center;
		var orta = new CenterContainer();
		_bolumIzgara = new GridContainer { Columns = 10 };
		_bolumIzgara.AddThemeConstantOverride("h_separation", 10);
		_bolumIzgara.AddThemeConstantOverride("v_separation", 10);
		orta.AddChild(_bolumIzgara);
		var agacButon = Dugme("Yetenek Ağacı  (kalıcı yükseltmeler)", 24);
		agacButon.CustomMinimumSize = new Vector2(460, 58);
		agacButon.Pressed += () => AgacEkraniniAc(_agacSecili ?? TdVeri.Kuleler[0]);
		var ustDugmeler = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		ustDugmeler.AddThemeConstantOverride("separation", 16);
		_devamKutu = new HBoxContainer();
		_muzikDugmeMenu = Dugme("Müzik", 20);
		_muzikDugmeMenu.CustomMinimumSize = new Vector2(0, 58);
		_muzikDugmeMenu.Pressed += MuzikDegistir;
		ustDugmeler.AddChild(_devamKutu); ustDugmeler.AddChild(agacButon); ustDugmeler.AddChild(_muzikDugmeMenu);
		mk.AddChild(baslik); mk.AddChild(alt); mk.AddChild(_menuOzet); mk.AddChild(ustDugmeler); mk.AddChild(orta);
		_menu.AddChild(mk);
		_ui.AddChild(_menu);
		AgacEkraniKur();
	}

	void AgacEkraniKur()
	{
		_agacEkrani = new PanelContainer { Position = Vector2.Zero, Size = new Vector2(1280, 720) };
		_agacEkrani.AddThemeStyleboxOverride("panel", Kutu(new Color(0.04f, 0.03f, 0.06f, 0.98f), 0));
		var dikey = new VBoxContainer();
		dikey.AddThemeConstantOverride("separation", 10);
		var ust = new HBoxContainer();
		ust.AddThemeConstantOverride("separation", 20);
		var geri = Dugme("◀ Menü", 20);
		geri.Pressed += MenuyuGoster;
		_agacUst = Yazi("", 22, new Color(0.6f, 1f, 0.9f));
		ust.AddChild(geri); ust.AddChild(_agacUst);
		var yatay = new HBoxContainer();
		yatay.AddThemeConstantOverride("separation", 16);
		var kaydir = new ScrollContainer { CustomMinimumSize = new Vector2(330, 630), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_agacListe = new VBoxContainer();
		_agacListe.AddThemeConstantOverride("separation", 4);
		kaydir.AddChild(_agacListe);
		var sag = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		sag.AddThemeConstantOverride("separation", 12);
		_agacBaslik = Yazi("", 32, new Color(1f, 0.85f, 0.4f));
		_agacBilgi = Yazi("", 18, new Color(0.88f, 0.86f, 0.95f));
		_agacBilgi.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_agacBilgi.CustomMinimumSize = new Vector2(860, 0);
		_agacDugumler = new GridContainer { Columns = 5 };
		_agacDugumler.AddThemeConstantOverride("h_separation", 10);
		_agacDugumler.AddThemeConstantOverride("v_separation", 10);
		var not = Yazi("Her yükseltme küçük ama kalıcı. Bölümü kaybetsen de XP ve para sende kalır: geri dön, güçlen, tekrar dene.", 15, new Color(0.65f, 0.6f, 0.75f));
		not.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		not.CustomMinimumSize = new Vector2(860, 0);
		sag.AddChild(_agacBaslik); sag.AddChild(_agacBilgi); sag.AddChild(_agacDugumler); sag.AddChild(not);
		yatay.AddChild(kaydir); yatay.AddChild(sag);
		dikey.AddChild(ust); dikey.AddChild(yatay);
		_agacEkrani.AddChild(dikey);
		_agacEkrani.Visible = false;
		_ui.AddChild(_agacEkrani);
	}

	void AgacEkraniniAc(KuleTanim t)
	{
		_agacSecili = t;
		_agacEkrani.Visible = true;
		_menu.Visible = false;
		var (ilr, ger) = Ilerleme.SeviyeDurumu();
		_agacUst.Text = $"Oyuncu Sv {Ilerleme.Seviye}  ({ilr}/{ger} XP)     Yetenek puanı: {Ilerleme.Puan}     Para: {Ilerleme.Para}";
		foreach (var c in _agacListe.GetChildren()) c.QueueFree();
		foreach (var k in TdVeri.Kuleler)
		{
			bool acik = Ilerleme.KuleAcik(k);
			int kd = Ilerleme.KuleKademe(k.Id);
			var b = Dugme(acik ? $"{k.Ad}   {kd}/10" : $"🔒 {k.Ad}  (Sv {k.Level})", 16);
			b.Alignment = HorizontalAlignment.Left;
			b.CustomMinimumSize = new Vector2(310, 40);
			if (k == t) b.AddThemeStyleboxOverride("normal", _sSecili ??= Kutu(new Color(0.3f, 0.2f, 0.12f), 8, new Color(1f, 0.8f, 0.35f)));
			if (acik) b.AddThemeColorOverride("font_color", kd >= 10 ? new Color(0.5f, 1f, 0.55f) : TdVeri.HasarRenk[k.HasarTuru].Lightened(0.3f));
			var kk = k;
			b.Pressed += () => AgacEkraniniAc(kk);
			_agacListe.AddChild(b);
		}
		var bon = Ilerleme.Bonus(t);
		_agacBaslik.Text = $"{t.Ad}   {Ilerleme.KuleKademe(t.Id)}/10";
		_agacBilgi.Text = $"{TdVeri.HasarTr[t.HasarTuru]} · {ModAd(t.Mod)}   ·   Açılış: oyuncu Sv {t.Level}\n" +
			$"Toplam kalıcı bonus: +%{bon.hasar * 100:0} hasar, +%{bon.menzil * 100:0} menzil, +%{bon.hiz * 100:0} atış hızı, %{bon.indirim * 100:0} ucuz, +%{bon.zirh * 100:0} zırh delme" +
			(t.Etkili.Length > 0 ? $"\nEtkili: {string.Join(", ", t.Etkili)}" : "");
		foreach (var c in _agacDugumler.GetChildren()) c.QueueFree();
		var dug = Ilerleme.Dugumler(t);
		int mevcut = Ilerleme.KuleKademe(t.Id);
		for (int i = 0; i < dug.Count; i++)
		{
			var d = dug[i];
			bool alindi = i < mevcut, sirada = i == mevcut;
			var b = Dugme(alindi ? $"✓ {i + 1}. {d.Ad}\n{Ilerleme.Aciklama(d)}"
				: $"{i + 1}. {d.Ad}\n{Ilerleme.Aciklama(d)}\n◎{d.Para} · {d.Puan} puan", 14);
			b.CustomMinimumSize = new Vector2(166, 104);
			b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			b.Disabled = alindi || !sirada || !Ilerleme.KuleAcik(t) || Ilerleme.Puan < d.Puan || Ilerleme.Para < d.Para;
			b.AddThemeColorOverride("font_disabled_color", alindi ? new Color(0.5f, 1f, 0.55f) : new Color(0.5f, 0.48f, 0.55f));
			b.Pressed += () =>
			{
				var hata = Ilerleme.DugumAl(t);
				if (hata == null) Ses("altin");
				AgacEkraniniAc(t);
			};
			_agacDugumler.AddChild(b);
		}
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
		int en = Ilerleme.EnUstBolum;
		int bas = Math.Max(1, en - 24);
		var devam = Dugme($"▶  Devam: Bölüm {en}", 24);
		devam.CustomMinimumSize = new Vector2(96, 60);
		devam.Pressed += () => BolumBaslat(en);
		_devamKutu.GetChildren().ToList().ForEach(c => c.QueueFree());
		_devamKutu.AddChild(devam);
		for (int i = bas; i < bas + 30; i++)
		{
			int b = i;
			bool boss = BolumBosslari(i).Count > 0;
			var dg = Dugme(i <= Ilerleme.EnUstBolum ? (boss ? $"{i}☠" : $"{i}") : "🔒", 22);
			dg.CustomMinimumSize = new Vector2(96, 60);
			dg.Disabled = i > Ilerleme.EnUstBolum;
			dg.Pressed += () => BolumBaslat(b);
			_bolumIzgara.AddChild(dg);
		}
		var (ilr, ger) = Ilerleme.SeviyeDurumu();
		_menuOzet.Text = $"Oyuncu Sv {Ilerleme.Seviye}  ({ilr}/{ger} XP)   ·   Para {Ilerleme.Para}   ·   Yetenek puanı {Ilerleme.Puan}   ·   Açık kuleler {AcikKuleler.Count()}/{TdVeri.Kuleler.Count}";
		_agacEkrani.Visible = false;
		MuzikDugmeGuncelle();
		MuzikCal("menu");
	}

	void ArayuzGuncelle()
	{
		_bolumYazi.Text = $"Bölüm {_bolum}";
		_dalgaYazi.Text = $"Dalga {_dalga}/{_dalgaSayisi}";
		_canYazi.Text = $"♥ {_can}";
		_altinYazi.Text = $"◎ {_altin}";
		_tehditYazi.Text = $"Sv {Ilerleme.Seviye}";
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
		_insaPanel.Position = new Vector2(PanelX(_yerler[yer], 390), 66);
		InsaListesiYaz();
		_insaPanel.Visible = true;
	}

	readonly Dictionary<KuleTanim, Button> _insaButon = new();
	readonly Dictionary<KuleTanim, bool> _insaAcikti = new();

	void InsaListesiYaz()
	{
		foreach (var t in TdVeri.Kuleler)
		{
			if (!_insaButon.TryGetValue(t, out var b))
			{
				b = Dugme("", 16);
				b.Alignment = HorizontalAlignment.Left;
				b.CustomMinimumSize = new Vector2(350, 0);
				var tt = t;
				b.Pressed += () => { if (_seciliYer >= 0) KuleKur(_seciliYer, tt); };
				_insaListe.AddChild(b);
				_insaButon[t] = b;
			}
			bool acik = Ilerleme.KuleAcik(t);
			int bedel = Ilerleme.Maliyet(t);
			int kd = Ilerleme.KuleKademe(t.Id);
			if (!_insaAcikti.TryGetValue(t, out var once) || once != acik)
			{
				_insaAcikti[t] = acik;
				if (acik)
				{
					b.AddThemeColorOverride("font_color", TdVeri.HasarRenk[t.HasarTuru].Lightened(0.3f));
					if (t.Doku != null) { b.Icon = t.Doku; b.ExpandIcon = false; b.AddThemeConstantOverride("icon_max_width", 44); }
				}
			}
			string satir1 = acik ? $"{t.Ad}{(kd > 0 ? $" +{kd}" : "")}   ◎{bedel}" : $"🔒 {t.Ad}  (Oyuncu Sv {t.Level})";
			string satir2 = $"{TdVeri.HasarTr[t.HasarTuru]} · {ModAd(t.Mod)}";
			if (acik && t.Etkili.Length > 0) satir2 += $"\nEtkili: {string.Join(", ", t.Etkili)}";
			MetinAyarla(b, satir1 + "\n" + satir2);
			bool kapali = !acik || _altin < bedel;
			if (b.Disabled != kapali) b.Disabled = kapali;
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
		_kulePanel.Position = new Vector2(PanelX(k.P, 410), 66);
		KuleBilgiYaz(k);
		_kulePanel.Visible = true;
	}

	void KuleBilgiYaz(Kule k)
	{
		_kuleBaslik.Text = $"{k.T.Ad}  {new string('★', k.Kademe)}";
		var t = k.T;
		string bilgi = $"Hasar: {k.Hasar:0} {TdVeri.HasarTr[t.HasarTuru]}  ·  {ModAd(t.Mod)}\nMenzil: {k.Menzil:0}   Atış/sn: {k.Hiz:0.0}";
		if (t.Etkili.Length > 0) bilgi += $"\nEtkili: {string.Join(", ", t.Etkili)}";
		if (t.Etkisiz.Length > 0) bilgi += $"\nZayıf kaldığı: {string.Join(", ", t.Etkisiz)}";
		if (k.ZirhDelme > 0) bilgi += "  ·  zırh delici";
		_kuleBilgi.Text = bilgi;
		for (int dal = 0; dal < 2; dal++)
			for (int kd = 0; kd < 3; kd++)
			{
				var n = k.Agac[dal][kd];
				var b = _agacButon[dal, kd];
				bool alindi = k.Dal[dal] > kd;
				bool sirada = k.Dal[dal] == kd;
				bool kilitli = sirada && !YetenekAgaci.Alinabilir(k.Dal, dal);
				string kod = $"{(alindi ? "✓ " : kilitli || !sirada ? "· " : "")}{n.Ad}";
				MetinAyarla(b, alindi ? $"{kod}\n{n.Aciklama}" : kilitli ? $"{kod}\nKapalı (diğer dal)" : $"{kod}  ◎{n.Bedel}\n{n.Aciklama}");
				bool kap = alindi || !sirada || kilitli || _altin < n.Bedel;
				if (b.Disabled != kap) b.Disabled = kap;
				b.AddThemeColorOverride("font_disabled_color", alindi ? new Color(0.5f, 1f, 0.55f) : new Color(0.5f, 0.48f, 0.55f));
				b.AddThemeColorOverride("font_color", kd == 2 ? new Color(1f, 0.85f, 0.4f) : new Color(0.92f, 0.9f, 0.97f));
			}
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
		Ilerleme.EnUstBolum = 46; Ilerleme.Xp = 9000; Ilerleme.Para = 5000;
		foreach (var t in TdVeri.Kuleler.Take(6)) for (int j = 0; j < 3 + t.Level % 5; j++) Ilerleme.DugumAl(t);
		MenuyuGoster();
		await Bekle(0.5f);
		AgacEkraniniAc(TdVeri.Kuleler[1]);
		await Bekle(0.5f);
		await Kare(klasor + "/0_yetenek_agaci.png");
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
			for (int s = 0; s < i % 5; s++) DalAl(k, s < 3 ? i % 2 : 1 - i % 2);
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
		GD.Print($"ONIZLEME fps={Engine.GetFramesPerSecond()} dusman={_dusmanlar.Count} yol={TdHarita.Uzunluk:0} muzik={_muzikler.Count} calan={_calan}");
		Engine.TimeScale = 3;
		for (int i = 0; i < 40 && _dalgaSuruyor; i++) await Bekle(0.5f);
		Engine.TimeScale = 1;
		await Bekle(0.3f);
		await Kare(klasor + "/3b_analiz.png");
		KulePaneliAc(_kuleler[2]);
		await Bekle(0.4f);
		await Kare(klasor + "/4_kule_panel.png");
		Sat(_kuleler[0]);
		InsaPaneliAc(0);
		await Bekle(0.4f);
		await Kare(klasor + "/5_insa.png");
		PanelleriKapat();
		var boss = BolumBosslari(_bolum).FirstOrDefault();
		if (boss != null && _ekran == Ekran.Oyun) { DusmanDogur(boss); _dusmanlar[^1].Mesafe = 700; }
		Engine.TimeScale = 1;
		await Bekle(2.5f);
		await Kare(klasor + "/6_boss.png");
		// geniş telefon ekranı (20:9) denemesi
		DisplayServer.WindowSetSize(new Vector2I(1600, 720));
		await Bekle(0.6f);
		BolumBaslat(3);
		await Bekle(1.2f);
		await Kare(klasor + "/7_genis_ekran.png");
		for (int i = 0; i < 3; i++)
		{
			BolumBaslat(41 + i);
			await Bekle(0.6f);
			await Kare(klasor + $"/8_yol_{i}.png");
		}
		// yürüme yakın çekimi: kulesiz bölümde birkaç düşman, kamera 3x
		var yuruyen = TdVeri.Dusmanlar.Where(t => t.Yuru != null && !t.Ucan && !t.Boss && t.Zirh != "Hayalet").Take(3)
			.Concat(TdVeri.Dusmanlar.Where(t => t.Yuru == null && t.Doku != null && !t.Ucan && !t.Boss && t.Zirh != "Hayalet").Take(1)).ToList();
		var ucan = TdVeri.Dusmanlar.FirstOrDefault(t => t.Doku != null && t.Ucan && !t.Boss);
		if (ucan != null) yuruyen.Add(ucan);
		for (int i = 0; i < yuruyen.Count; i++) { DusmanDogur(yuruyen[i]); _dusmanlar[^1].Mesafe = 260 + i * 45; _dusmanlar[^1].Can = 1e9f; _dusmanlar[^1].CanMax = 1e9f; }
		await Bekle(0.15f);
		var kam = GetViewport().GetCamera2D();
		for (int f = 0; f < 10; f++)
		{
			var hedefD = _dusmanlar.Where(e => !e.Olu).ToList();
			if (hedefD.Count > 0) kam.Position = hedefD.Aggregate(Vector2.Zero, (a, e) => a + e.Kok.Position) / hedefD.Count + new Vector2(0, -25);
			kam.Zoom = Vector2.One * 3f;
			await Kare(klasor + $"/10_yurume_{f}.png");
			await Bekle(0.1f);
		}
		kam.Zoom = Vector2.One; kam.Position = new Vector2(640, 360);
		foreach (var b in new[] { 1, 12, 30, 45 }) { BolumBaslat(b); GD.Print($"ONIZLEME yer bölüm {b}: {_yerler.Count}"); }
		await Bekle(0.4f);
		await Kare(klasor + "/11_yerler_45.png");
		MenuyuGoster();
		await Bekle(0.5f);
		await Kare(klasor + "/9_menu_sonsuz.png");
		GD.Print($"ONIZLEME ekran={GetViewportRect().Size} yer={_yerler.Count}");
		GD.Print($"ONIZLEME bölüm={_bolum} dalga={_dalga}/{_dalgaSayisi} can={_can} düşman={_dusmanlar.Count} kule={_kuleler.Count} doku_kule={TdVeri.Kuleler.Count(k => k.Doku != null)} doku_dusman={TdVeri.Dusmanlar.Count(d => d.Doku != null)} yuru={TdVeri.Dusmanlar.Count(d => d.Yuru != null)}");
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
	float _t, _sayac;
	public override void _Process(double delta)
	{
		_t += (float)delta; _sayac -= (float)delta;
		if (_sayac <= 0) { _sayac = 0.1f; QueueRedraw(); }   // her karede değil, saniyede 10 kez
	}
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
