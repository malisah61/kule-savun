using Godot;
using System;

/// <summary>Ateş, büyü, patlama, toz gibi 2D parçacık efektleri ve kodla üretilen sesler.</summary>
public static class Efekt
{
	static Texture2D _parilti;
	static Texture2D _yumusak;
	static CanvasItemMaterial _toplama;
	static readonly RandomNumberGenerator Rng = new RandomNumberGenerator();

	public static Texture2D Parilti
	{
		get
		{
			if (_parilti == null)
			{
				var g = new Gradient();
				g.Offsets = new float[] { 0f, 0.35f, 1f };
				g.Colors = new Color[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0) };
				_parilti = new GradientTexture2D
				{
					Gradient = g, Width = 64, Height = 64,
					Fill = GradientTexture2D.FillEnum.Radial,
					FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
				};
			}
			return _parilti;
		}
	}

	/// <summary>Duman ve toz için yumuşak kenarlı daire.</summary>
	public static Texture2D Yumusak
	{
		get
		{
			if (_yumusak == null)
			{
				var g = new Gradient();
				g.Offsets = new float[] { 0f, 0.6f, 1f };
				g.Colors = new Color[] { new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0) };
				_yumusak = new GradientTexture2D
				{
					Gradient = g, Width = 64, Height = 64,
					Fill = GradientTexture2D.FillEnum.Radial,
					FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
				};
			}
			return _yumusak;
		}
	}

	public static CanvasItemMaterial Toplama =>
		_toplama ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public static Gradient Renkler(params Color[] renkler)
	{
		var g = new Gradient();
		var ofs = new float[renkler.Length];
		for (int i = 0; i < renkler.Length; i++)
			ofs[i] = renkler.Length == 1 ? 0f : (float)i / (renkler.Length - 1);
		g.Offsets = ofs;
		g.Colors = renkler;
		return g;
	}

	public static readonly Color[] AtesRenk =
	{
		new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.6f, 0.1f, 0.95f),
		new Color(0.9f, 0.2f, 0.05f, 0.7f), new Color(0.25f, 0.1f, 0.08f, 0f)
	};

	public static readonly Color[] MorRenk =
	{
		new Color(1f, 0.85f, 1f, 1f), new Color(0.75f, 0.3f, 1f, 0.9f),
		new Color(0.35f, 0.05f, 0.6f, 0.6f), new Color(0.1f, 0f, 0.2f, 0f)
	};

	public static readonly Color[] BuzRenk =
	{
		new Color(1f, 1f, 1f, 1f), new Color(0.55f, 0.85f, 1f, 0.9f), new Color(0.2f, 0.45f, 1f, 0f)
	};

	/// <summary>Genel parçacık yayıcı.</summary>
	public static CpuParticles2D Yayici(Node ebeveyn, Vector2 konum, Color[] renk, int adet, float omur,
		float hizMin, float hizMax, float boyMin, float boyMax, bool tekSefer, bool toplamali = true,
		float yercekimi = 0f, float yayilma = 180f, float yaricap = 0f)
	{
		var p = new CpuParticles2D
		{
			Position = konum,
			Amount = Math.Max(1, adet),
			Lifetime = omur,
			OneShot = tekSefer,
			Explosiveness = tekSefer ? 0.92f : 0f,
			Texture = toplamali ? Parilti : Yumusak,
			Direction = Vector2.Up,
			Spread = yayilma,
			Gravity = new Vector2(0, yercekimi),
			InitialVelocityMin = hizMin,
			InitialVelocityMax = hizMax,
			ScaleAmountMin = boyMin,
			ScaleAmountMax = boyMax,
			ColorRamp = Renkler(renk),
			LocalCoords = false,
			DampingMin = 20f,
			DampingMax = 60f,
		};
		var egri = new Curve();
		egri.AddPoint(new Vector2(0f, 0.6f));
		egri.AddPoint(new Vector2(0.25f, 1f));
		egri.AddPoint(new Vector2(1f, 0.15f));
		p.ScaleAmountCurve = egri;
		if (yaricap > 0f)
		{
			p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
			p.EmissionSphereRadius = yaricap;
		}
		if (toplamali)
			p.Material = Toplama;
		ebeveyn.AddChild(p);
		p.Emitting = true;
		if (tekSefer)
			KendiniSil(p, omur + 0.4f);
		return p;
	}

	public static void KendiniSil(Node n, float sn)
	{
		var t = n.GetTree().CreateTimer(sn);
		t.Timeout += () => { if (GodotObject.IsInstanceValid(n)) n.QueueFree(); };
	}

	/// <summary>Sürekli yanan ateş (meşale, yanan düşman, ateş topu izi).</summary>
	public static CpuParticles2D Ates(Node ebeveyn, Vector2 konum, float boy = 1f, int adet = 24)
	{
		var p = Yayici(ebeveyn, konum, AtesRenk, adet, 0.55f, 25f * boy, 70f * boy, 0.35f * boy, 0.7f * boy,
			false, true, -90f * boy, 25f, 6f * boy);
		return p;
	}

	/// <summary>Bir izi durdurur, son parçacıklar sönünce siler.</summary>
	public static void Sondur(CpuParticles2D p)
	{
		if (p == null || !GodotObject.IsInstanceValid(p)) return;
		p.Emitting = false;
		var dunya = p.GlobalPosition;
		var oyun = p.GetTree().CurrentScene;
		if (p.GetParent() != oyun)
		{
			p.Reparent(oyun);
			p.GlobalPosition = dunya;
		}
		KendiniSil(p, p.Lifetime + 0.2f);
	}

	/// <summary>Patlama: alev topu + kıvılcım + duman + parlama.</summary>
	public static void Patlama(Node ebeveyn, Vector2 konum, float boy = 1f)
	{
		Yayici(ebeveyn, konum, AtesRenk, (int)(40 * boy), 0.7f, 120f * boy, 320f * boy, 0.8f * boy, 1.8f * boy,
			true, true, -60f, 180f, 10f * boy);
		Yayici(ebeveyn, konum, new[] { new Color(1, 1, 0.8f, 1), new Color(1, 0.7f, 0.2f, 1), new Color(1, 0.3f, 0, 0) },
			(int)(24 * boy), 0.6f, 250f * boy, 520f * boy, 0.12f, 0.25f, true, true, 500f);
		Yayici(ebeveyn, konum + new Vector2(0, -10), new[] { new Color(0.3f, 0.27f, 0.25f, 0.7f), new Color(0.2f, 0.2f, 0.2f, 0f) },
			(int)(14 * boy), 1.3f, 30f * boy, 90f * boy, 1.2f * boy, 2.4f * boy, true, false, -50f, 180f, 18f * boy);
		Parlama(ebeveyn, konum, new Color(1f, 0.7f, 0.3f), 3.5f * boy, 0.35f);
	}

	public static void Parlama(Node ebeveyn, Vector2 konum, Color renk, float boy, float sure)
	{
		var s = new Sprite2D { Texture = Parilti, Position = konum, Modulate = renk, Scale = Vector2.One * boy * 0.4f, Material = Toplama };
		ebeveyn.AddChild(s);
		var tw = s.CreateTween().SetParallel(true);
		tw.TweenProperty(s, "scale", Vector2.One * boy, sure).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(s, "modulate:a", 0f, sure);
		tw.Chain().TweenCallback(Callable.From(s.QueueFree));
	}

	public static void Kivilcim(Node ebeveyn, Vector2 konum, Color renk)
	{
		Yayici(ebeveyn, konum, new[] { Colors.White, renk, new Color(renk, 0f) }, 10, 0.35f, 120f, 260f, 0.12f, 0.25f, true, true, 300f);
	}

	public static void MorPatlama(Node ebeveyn, Vector2 konum, float boy = 1f)
	{
		Yayici(ebeveyn, konum, MorRenk, (int)(26 * boy), 0.55f, 90f * boy, 220f * boy, 0.6f * boy, 1.3f * boy, true, true, -40f);
		Parlama(ebeveyn, konum, new Color(0.7f, 0.3f, 1f), 2.4f * boy, 0.3f);
	}

	public static void Toz(Node ebeveyn, Vector2 konum, float boy = 1f)
	{
		Yayici(ebeveyn, konum, new[] { new Color(0.62f, 0.5f, 0.36f, 0.8f), new Color(0.5f, 0.42f, 0.32f, 0f) },
			(int)(14 * boy), 0.9f, 40f * boy, 120f * boy, 0.7f * boy, 1.6f * boy, true, false, -30f, 70f, 12f * boy);
	}

	public static void Kemik(Node ebeveyn, Vector2 konum)
	{
		Yayici(ebeveyn, konum, new[] { new Color(0.95f, 0.93f, 0.85f, 1f), new Color(0.8f, 0.78f, 0.7f, 0f) },
			12, 0.8f, 120f, 240f, 0.15f, 0.3f, true, false, 600f, 70f);
	}

	/// <summary>Uçuşan yazı: hasar sayısı, altın.</summary>
	public static void Yazi(Node ebeveyn, Vector2 konum, string metin, Color renk, int boyut = 26)
	{
		var l = new Label { Text = metin, Position = konum + new Vector2(-40, -20), Size = new Vector2(80, 30), ZIndex = 50 };
		l.HorizontalAlignment = HorizontalAlignment.Center;
		l.AddThemeFontSizeOverride("font_size", boyut);
		l.AddThemeColorOverride("font_color", renk);
		l.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.05f, 0.05f));
		l.AddThemeConstantOverride("outline_size", 7);
		ebeveyn.AddChild(l);
		var tw = l.CreateTween().SetParallel(true);
		tw.TweenProperty(l, "position:y", l.Position.Y - 55, 0.9f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tw.TweenProperty(l, "modulate:a", 0f, 0.5f).SetDelay(0.45f);
		tw.Chain().TweenCallback(Callable.From(l.QueueFree));
	}

	public static float Rastgele(float a, float b) => Rng.RandfRange(a, b);

	// ======================================================= SESLER (kodla üretilir)

	public static AudioStreamWav SesUret(string tur)
	{
		const int oran = 22050;
		float sure = tur switch
		{
			"ok" => 0.18f, "ates" => 0.45f, "patlama" => 0.9f, "vurus" => 0.15f, "altin" => 0.25f,
			"kale" => 0.3f, "buyu" => 0.4f, "deprem" => 1.1f, "boru" => 1.2f, "kalkan" => 0.35f, _ => 0.2f
		};
		int n = (int)(oran * sure);
		var veri = new byte[n * 2];
		var r = new Random(tur.GetHashCode());
		float faz = 0f, dusuk = 0f;
		for (int i = 0; i < n; i++)
		{
			float t = (float)i / oran;
			float u = t / sure;
			float gurultu = (float)(r.NextDouble() * 2 - 1);
			float s;
			switch (tur)
			{
				case "ok":
					dusuk += (gurultu - dusuk) * (0.5f - 0.4f * u);
					s = dusuk * (1 - u) * 0.8f;
					break;
				case "ates":
					dusuk += (gurultu - dusuk) * 0.12f;
					s = dusuk * MathF.Sin(MathF.PI * u) * 1.4f;
					break;
				case "patlama":
				case "deprem":
					dusuk += (gurultu - dusuk) * (tur == "deprem" ? 0.03f : 0.06f + 0.1f * (1 - u));
					faz += 2 * MathF.PI * (60f - 30f * u) / oran;
					s = (dusuk * 2.2f + MathF.Sin(faz) * 0.5f) * MathF.Pow(1 - u, 1.6f);
					break;
				case "vurus":
					faz += 2 * MathF.PI * (180f - 120f * u) / oran;
					s = (MathF.Sin(faz) * 0.7f + gurultu * 0.3f) * MathF.Pow(1 - u, 2);
					break;
				case "kale":
					faz += 2 * MathF.PI * (90f - 40f * u) / oran;
					dusuk += (gurultu - dusuk) * 0.2f;
					s = (MathF.Sin(faz) * 0.7f + dusuk * 0.5f) * MathF.Pow(1 - u, 2.5f);
					break;
				case "altin":
					faz += 2 * MathF.PI * (u < 0.35f ? 1320f : 1760f) / oran;
					s = MathF.Sin(faz) * 0.35f * (1 - u);
					break;
				case "buyu":
					faz += 2 * MathF.PI * (400f + 900f * u + 60f * MathF.Sin(t * 90f)) / oran;
					s = MathF.Sin(faz) * 0.35f * MathF.Sin(MathF.PI * u);
					break;
				case "kalkan":
					faz += 2 * MathF.PI * 520f / oran;
					s = (MathF.Sin(faz) * 0.4f + MathF.Sin(faz * 2.7f) * 0.25f) * MathF.Pow(1 - u, 3);
					break;
				case "boru":
					faz += 2 * MathF.PI * (u < 0.3f ? 220f : 294f) / oran;
					s = (MathF.Sin(faz) + 0.4f * MathF.Sin(faz * 2) + 0.2f * MathF.Sin(faz * 3)) * 0.25f
						* MathF.Min(1, u * 12) * MathF.Min(1, (1 - u) * 5);
					break;
				default:
					s = gurultu * (1 - u) * 0.3f;
					break;
			}
			short v = (short)(Math.Clamp(s, -1f, 1f) * 30000);
			veri[i * 2] = (byte)(v & 0xff);
			veri[i * 2 + 1] = (byte)((v >> 8) & 0xff);
		}
		return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = oran, Stereo = false, Data = veri };
	}
}
