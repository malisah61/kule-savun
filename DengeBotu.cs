using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Denge botu: oyunu sıfır kayıttan başlayıp bulutta kendi kendine oynar (hızlı ileri sararak).
/// Makul bir oyuncu gibi davranır: bölüm düşman zırhlarına göre verimli kuleyi seçer, iyi yerlere
/// (yolun en çok geçtiği yerler önce) kurar, artan parayla yükseltir; kaybederse kalıcı ağaçtan
/// güçlenip tekrar dener. Her oyun bir satır olarak CSV'ye yazılır.
/// Kullanım: godot ... -- --denge=/yol/denge.csv
/// </summary>
public partial class KuleSavunmasi
{
	bool _botModu;
	readonly Dictionary<string, int> _botKullanim = new();

	async System.Threading.Tasks.Task DengeBotu(string yol)
	{
		_botModu = true;
		await Bekle(0.5f);
		// sıfırdan başla
		Ilerleme.EnUstBolum = 1; Ilerleme.Xp = 0; Ilerleme.Para = 0; Ilerleme.HarcananPuan = 0; Ilerleme.Kademe.Clear();
		Ilerleme.MuzikAcik = false;
		var csv = new StringBuilder("oyun,bolum,deneme,sonuc,dalga,dalgaSayisi,can,seviye,kalici,kule,simSure,uyum\n");
		var ilkGecis = new SortedDictionary<int, (int deneme, int seviye, int can)>();
		int oyun = 0, deneme = 0, sonBolum = 0;
		var bas = Time.GetTicksMsec();
		const float dt = 1f / 30f;
		int hedefBolum = 60;
		foreach (var a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("--denge-son=")) hedefBolum = int.Parse(a.Substring(12));

		while (Ilerleme.EnUstBolum <= hedefBolum && oyun < 400 && Time.GetTicksMsec() - bas < 40 * 60 * 1000)
		{
			int b = Ilerleme.EnUstBolum;
			if (b != sonBolum) { deneme = 0; sonBolum = b; }
			deneme++; oyun++;
			BolumBaslat(b);
			float sim = 0, harcamaSayac = 0;
			bool bitti = false;
			while (!bitti)
			{
				// her karede 20 sn'lik oyun
				for (int i = 0; i < 600; i++)
				{
					if (_ekran != Ekran.Oyun) { bitti = true; break; }
					if (!_dalgaSuruyor)
					{
						BotHarca();
						DalgaBaslat();
					}
					harcamaSayac -= dt;
					if (harcamaSayac <= 0) { BotHarca(); harcamaSayac = 3f; }
					Adim(dt);
					sim += dt;
					if (sim > 3600) { bitti = true; break; }   // takılma koruması
				}
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			bool kazandi = Ilerleme.EnUstBolum > b;
			BotKaliciHarca();
			int kalici = Ilerleme.Kademe.Values.Sum();
			csv.Append($"{oyun},{b},{deneme},{(kazandi ? "K" : "Y")},{_dalga},{_dalgaSayisi},{Math.Max(0, _can)},{Ilerleme.Seviye},{kalici},{_kuleler.Count},{sim:0},{_uyum:0.00}\n");
			GD.Print($"DENGE oyun={oyun} bölüm={b} deneme={deneme} {(kazandi ? "KAZANDI" : "kaybetti")} dalga={_dalga}/{_dalgaSayisi} can={Math.Max(0, _can)} sv={Ilerleme.Seviye} kalıcı={kalici} uyum={_uyum:0.00}");
			if (kazandi && !ilkGecis.ContainsKey(b)) ilkGecis[b] = (deneme, Ilerleme.Seviye, Math.Max(0, _can));
			_sonucPanel.Visible = false;
			if (deneme >= 25) { GD.Print($"DENGE DUVAR: bölüm {b} 25 denemede geçilemedi"); break; }
		}
		using (var f = FileAccess.Open(yol, FileAccess.ModeFlags.Write)) f?.StoreString(csv.ToString());
		var oz = new StringBuilder("bölüm  deneme  seviye  kalan_can\n");
		foreach (var (k, v) in ilkGecis) oz.Append($"{k,5}  {v.deneme,6}  {v.seviye,6}  {v.can,9}\n");
		oz.Append($"\nToplam oyun: {oyun}, ulaşılan bölüm: {Ilerleme.EnUstBolum}, oyuncu seviyesi: {Ilerleme.Seviye}, gerçek süre: {(Time.GetTicksMsec() - bas) / 1000} sn\n");
		using (var f = FileAccess.Open(yol.Replace(".csv", "_ozet.txt"), FileAccess.ModeFlags.Write)) f?.StoreString(oz.ToString());
		GD.Print(oz.ToString());
		GD.Print("DENGE bitti");
		GetTree().Quit();
	}

	/// <summary>Kulenin bu bölümün düşman karışımına karşı altın başına gücü.</summary>
	float BotDeger(KuleTanim t)
	{
		var havuz = TdVeri.Dusmanlar.Where(d => !d.Boss && d.Level <= _bolum).ToList();
		float etki = havuz.Count == 0 ? 1f : havuz.Average(d => TdVeri.HasarCarpani(d.Zirh, t.HasarTuru) * (d.Ucan && t.HasarTuru == "Kusatma" ? 0f : 1f));
		var k = new Kule { T = t, Agac = null };
		var bon = Ilerleme.Bonus(t);
		k.HasarC += bon.hasar; k.HizC += bon.hiz;
		float cesit = Mathf.Pow(0.8f, _kuleler.Count(x => x.T == t));   // aynı kuleden çok kurma
		return k.Dps * etki * cesit / Math.Max(1, Ilerleme.Maliyet(t));
	}

	/// <summary>Elindeki altını harcar: boş iyi yer varsa en verimli kule, yoksa en ucuz yükseltme.</summary>
	void BotHarca()
	{
		for (int guvenlik = 0; guvenlik < 30; guvenlik++)
		{
			var bos = Enumerable.Range(0, _yerler.Count).Where(i => !_kuleler.Any(k => k.P == _yerler[i])).ToList();
			var acik = AcikKuleler.Where(t => Ilerleme.Maliyet(t) <= _altin).ToList();
			// yükseltme adayı: en ucuz alınabilir dal (önce Güç, sonra Ustalık)
			(Kule k, int dal, int bedel)? yuk = null;
			foreach (var k in _kuleler)
				for (int dal = 0; dal < 2; dal++)
					if (YetenekAgaci.Alinabilir(k.Dal, dal) && k.Dal[dal] < k.Agac[dal].Count)
					{
						int bedel = k.Agac[dal][k.Dal[dal]].Bedel;
						if (yuk == null || bedel < yuk.Value.bedel) yuk = (k, dal, bedel);
					}
			bool kurabilir = bos.Count > 0 && acik.Count > 0;
			// yerlerin yarısı dolana kadar kur; sonra yükseltme ucuzsa onu tercih et
			bool yukTercih = yuk != null && yuk.Value.bedel <= _altin && (!kurabilir || (_kuleler.Count >= _yerler.Count / 2 && yuk.Value.bedel < acik.Min(Ilerleme.Maliyet) * 0.9f));
			if (yukTercih) { DalAl(yuk.Value.k, yuk.Value.dal); continue; }
			if (kurabilir)
			{
				var t = acik.OrderByDescending(BotDeger).First();
				KuleKur(bos[0], t);
				_botKullanim[t.Id] = _botKullanim.GetValueOrDefault(t.Id) + 1;
				continue;
			}
			if (yuk != null && yuk.Value.bedel <= _altin) { DalAl(yuk.Value.k, yuk.Value.dal); continue; }
			break;
		}
		PanelleriKapat();
	}

	/// <summary>Bölüm sonunda kalıcı ağaç: en çok kullandığı kulelere puan/para harcar.</summary>
	void BotKaliciHarca()
	{
		foreach (var (id, _) in _botKullanim.OrderByDescending(x => x.Value).ToList())
		{
			var t = TdVeri.Kuleler.FirstOrDefault(x => x.Id == id);
			if (t == null) continue;
			while (Ilerleme.DugumAl(t) == null) { }
		}
	}
}
