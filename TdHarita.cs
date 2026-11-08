using Godot;
using System;
using System.Collections.Generic;

/// <summary>Mezarlık haritası: kodla çizilen zemin, kıvrımlı yol, kule yerleri ve süsler.</summary>
public partial class TdHarita : Node2D
{
	public static readonly Vector2[] Yol =
	{
		new(-60, 150), new(300, 150), new(300, 420), new(620, 420), new(620, 190),
		new(950, 190), new(950, 520), new(1340, 520)
	};
	public const float YolGenislik = 54f;

	public static readonly Vector2[] AdayYerler =
	{
		new(150, 250), new(400, 260), new(200, 370), new(400, 520), new(520, 320), new(530, 525),
		new(720, 300), new(720, 430), new(785, 100), new(860, 300), new(1050, 300), new(1050, 410),
		new(860, 430), new(1150, 620), new(990, 630), new(800, 600), new(150, 560), new(1180, 400),
	};

	public static float Uzunluk { get; private set; }
	static readonly List<float> Birikim = new();

	static TdHarita()
	{
		Birikim.Add(0);
		for (int i = 1; i < Yol.Length; i++) Birikim.Add(Birikim[i - 1] + Yol[i - 1].DistanceTo(Yol[i]));
		Uzunluk = Birikim[^1];
	}

	/// <summary>Yolun başından 'm' piksel ilerideki nokta ve gidiş yönü.</summary>
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

	public static List<Vector2> KuleYerleri()
	{
		var l = new List<Vector2>();
		foreach (var p in AdayYerler)
			if (YolaUzaklik(p) > YolGenislik * 0.5f + 34f) l.Add(p);
		return l;
	}

	readonly List<(Vector2 p, float r, Color c)> _benekler = new();
	readonly List<(Vector2 p, int tur)> _susler = new();
	float _t;

	public override void _Ready()
	{
		var rng = new RandomNumberGenerator { Seed = 61 };
		for (int i = 0; i < 260; i++)
		{
			var p = new Vector2(rng.RandfRange(0, 1280), rng.RandfRange(56, 720));
			if (YolaUzaklik(p) < YolGenislik * 0.6f) continue;
			_benekler.Add((p, rng.RandfRange(2, 7), rng.Randf() < 0.5f ? new Color(0.13f, 0.17f, 0.11f) : new Color(0.09f, 0.12f, 0.09f)));
		}
		var yerler = KuleYerleri();
		for (int i = 0; i < 40; i++)
		{
			var p = new Vector2(rng.RandfRange(20, 1260), rng.RandfRange(80, 700));
			if (YolaUzaklik(p) < YolGenislik * 0.5f + 20f) continue;
			bool yakin = false;
			foreach (var y in yerler) if (y.DistanceTo(p) < 52) yakin = true;
			if (!yakin) _susler.Add((p, rng.RandiRange(0, 3)));
		}
	}

	public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

	public override void _Draw()
	{
		DrawRect(new Rect2(0, 0, 1280, 720), new Color(0.11f, 0.13f, 0.1f));
		foreach (var (p, r, c) in _benekler) DrawCircle(p, r, c);
		// yol: kenar + toprak + iz
		for (int i = 1; i < Yol.Length; i++)
		{
			DrawLine(Yol[i - 1], Yol[i], new Color(0.12f, 0.09f, 0.07f), YolGenislik + 10);
			DrawCircle(Yol[i - 1], (YolGenislik + 10) / 2, new Color(0.12f, 0.09f, 0.07f));
		}
		for (int i = 1; i < Yol.Length; i++)
		{
			DrawLine(Yol[i - 1], Yol[i], new Color(0.3f, 0.23f, 0.17f), YolGenislik);
			DrawCircle(Yol[i - 1], YolGenislik / 2, new Color(0.3f, 0.23f, 0.17f));
		}
		for (int i = 1; i < Yol.Length; i++)
			DrawDashedLine(Yol[i - 1], Yol[i], new Color(0.24f, 0.18f, 0.13f), 3, 14);
		// süsler: mezar taşı, haç, ölü ağaç, kemik
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
		// giriş portalı ve çıkış kapısı
		float n = 0.5f + 0.5f * Mathf.Sin(_t * 3);
		DrawCircle(new Vector2(18, 150), 34, new Color(0.25f, 0.9f, 0.3f, 0.12f + 0.08f * n));
		DrawCircle(new Vector2(18, 150), 22, new Color(0.15f, 0.6f, 0.2f, 0.25f));
		DrawRect(new Rect2(1228, 470, 52, 100), new Color(0.2f, 0.16f, 0.12f));
		DrawRect(new Rect2(1236, 482, 44, 88), new Color(0.07f, 0.05f, 0.05f));
		DrawCircle(new Vector2(1240, 470), 8, new Color(1f, 0.6f, 0.2f, 0.5f + 0.3f * n));
		// sis
		for (int i = 0; i < 6; i++)
		{
			float x = Mathf.PosMod(_t * (12 + i * 5) + i * 230, 1500) - 110;
			DrawRect(new Rect2(x, 120 + i * 105, 160, 10), new Color(0.7f, 0.9f, 0.75f, 0.04f));
		}
	}
}
