using Godot;
using System.Collections.Generic;

/// <summary>Gökyüzü, dağlar, tepeler, ağaçlar, bulutlar ve yol: hepsi kodla çizilir.</summary>
public partial class Arkaplan : Node2D
{
	public float Genislik = 1280f;
	public float Yer = 630f;
	readonly List<Vector3> _bulutlar = new(); // x, y, boy
	readonly List<Vector3> _agaclar = new();
	readonly List<Vector2> _cimen = new();

	public override void _Ready()
	{
		var r = new RandomNumberGenerator { Seed = 7 };
		for (int i = 0; i < 6; i++)
			_bulutlar.Add(new Vector3(r.RandfRange(0, 2200), r.RandfRange(50, 230), r.RandfRange(0.7f, 1.4f)));
		for (int i = 0; i < 22; i++)
			_agaclar.Add(new Vector3(r.RandfRange(-50, 2600), r.RandfRange(0, 40), r.RandfRange(0.7f, 1.3f)));
		_agaclar.Sort((a, b) => a.Y.CompareTo(b.Y));
		for (int i = 0; i < 160; i++)
			_cimen.Add(new Vector2(r.RandfRange(0, 2600), r.RandfRange(4, 90)));
	}

	public override void _Process(double delta)
	{
		for (int i = 0; i < _bulutlar.Count; i++)
		{
			var b = _bulutlar[i];
			b.X -= (float)delta * 9f * b.Z;
			if (b.X < -250) b.X = Genislik + 250;
			_bulutlar[i] = b;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		float g = Mathf.Max(Genislik, 1280f) + 100f;
		// Gökyüzü (yukarıdan aşağı açılan mavi)
		var ust = new Color(0.33f, 0.58f, 0.92f);
		var alt = new Color(0.78f, 0.9f, 1f);
		DrawPolygon(new[] { new Vector2(-50, 0), new Vector2(g, 0), new Vector2(g, Yer), new Vector2(-50, Yer) },
			new[] { ust, ust, alt, alt });
		// Güneş
		DrawCircle(new Vector2(g * 0.72f, 120), 70, new Color(1f, 0.95f, 0.7f, 0.25f));
		DrawCircle(new Vector2(g * 0.72f, 120), 46, new Color(1f, 0.96f, 0.75f));
		// Bulutlar
		foreach (var b in _bulutlar)
		{
			var c = new Color(1, 1, 1, 0.92f);
			float s = b.Z;
			DrawCircle(new Vector2(b.X, b.Y), 34 * s, c);
			DrawCircle(new Vector2(b.X + 38 * s, b.Y + 8 * s), 28 * s, c);
			DrawCircle(new Vector2(b.X - 38 * s, b.Y + 10 * s), 24 * s, c);
			DrawCircle(new Vector2(b.X + 10 * s, b.Y + 16 * s), 30 * s, c);
		}
		// Uzak dağlar
		Dag(g, Yer - 250, 260, new Color(0.55f, 0.63f, 0.78f), 3);
		Dag(g, Yer - 170, 200, new Color(0.45f, 0.6f, 0.62f), 11);
		// Tepeler
		Tepe(g, Yer - 70, 70, new Color(0.38f, 0.62f, 0.32f), 0.004f);
		// Ağaçlar
		foreach (var a in _agaclar)
		{
			if (a.X > g) continue;
			float s = a.Z;
			var taban = new Vector2(a.X, Yer - 40 + a.Y);
			DrawRect(new Rect2(taban.X - 5 * s, taban.Y - 30 * s, 10 * s, 32 * s), new Color(0.38f, 0.25f, 0.15f));
			var koyu = new Color(0.17f, 0.42f, 0.2f);
			var acik = new Color(0.25f, 0.55f, 0.25f);
			for (int k = 0; k < 3; k++)
			{
				float y = taban.Y - 28 * s - k * 22 * s;
				float w = (34 - k * 8) * s;
				DrawColoredPolygon(new[] { new Vector2(taban.X - w, y), new Vector2(taban.X + w, y), new Vector2(taban.X, y - 40 * s) },
					k % 2 == 0 ? koyu : acik);
			}
		}
		// Çimen zemin
		var cim1 = new Color(0.36f, 0.6f, 0.26f);
		var cim2 = new Color(0.26f, 0.48f, 0.2f);
		DrawPolygon(new[] { new Vector2(-50, Yer - 30), new Vector2(g, Yer - 30), new Vector2(g, 800), new Vector2(-50, 800) },
			new[] { cim1, cim1, cim2, cim2 });
		// Toprak yol
		var yol = new Color(0.62f, 0.5f, 0.34f);
		DrawRect(new Rect2(-50, Yer - 12, g + 50, 34), yol);
		DrawRect(new Rect2(-50, Yer - 12, g + 50, 4), new Color(0.5f, 0.4f, 0.27f));
		foreach (var c in _cimen)
		{
			if (c.X > g) continue;
			float y = Yer + c.Y - 30;
			if (y > Yer - 14 && y < Yer + 24) continue;
			DrawLine(new Vector2(c.X, y), new Vector2(c.X - 3, y - 9), cim2.Darkened(0.1f), 2);
			DrawLine(new Vector2(c.X + 4, y), new Vector2(c.X + 6, y - 7), cim1.Lightened(0.15f), 2);
		}
	}

	void Dag(float g, float taban, float yukseklik, Color renk, int tohum)
	{
		var r = new RandomNumberGenerator { Seed = (ulong)tohum };
		var noktalar = new List<Vector2> { new Vector2(-50, taban + yukseklik) };
		float x = -50;
		while (x < g + 200)
		{
			noktalar.Add(new Vector2(x, taban + r.RandfRange(0, yukseklik * 0.55f)));
			x += r.RandfRange(120, 260);
			noktalar.Add(new Vector2(x, taban + yukseklik * r.RandfRange(0.55f, 0.8f)));
			x += r.RandfRange(80, 160);
		}
		noktalar.Add(new Vector2(g + 200, taban + yukseklik));
		DrawColoredPolygon(noktalar.ToArray(), renk);
	}

	void Tepe(float g, float taban, float yukseklik, Color renk, float frekans)
	{
		var noktalar = new List<Vector2> { new Vector2(-50, Yer) };
		for (float x = -50; x <= g + 50; x += 30)
			noktalar.Add(new Vector2(x, taban + Mathf.Sin(x * frekans * Mathf.Tau) * yukseklik * 0.4f - Mathf.Sin(x * 0.011f) * 18f));
		noktalar.Add(new Vector2(g + 50, Yer));
		DrawColoredPolygon(noktalar.ToArray(), renk);
	}
}
