using Godot;

/// <summary>Genişleyen halka (kalkan darbesi, deprem dalgası).</summary>
public partial class Halka : Node2D
{
	public Color Renk = new Color(0.6f, 0.85f, 1f);
	public float Yaricap = 10f;
	public float Kalinlik = 10f;
	public float Basik = 0.35f;

	public override void _Draw()
	{
		DrawSetTransform(Vector2.Zero, 0, new Vector2(1, Basik));
		DrawArc(Vector2.Zero, Yaricap, 0, Mathf.Tau, 48, Renk, Kalinlik, true);
		DrawArc(Vector2.Zero, Yaricap * 0.85f, 0, Mathf.Tau, 48, new Color(Renk, Renk.A * 0.4f), Kalinlik * 0.6f, true);
	}

	public static void Olustur(Node ebeveyn, Vector2 konum, Color renk, float sonYaricap, float sure, float basik = 0.35f)
	{
		var h = new Halka { Position = konum, Renk = renk, Basik = basik, Material = Efekt.Toplama };
		ebeveyn.AddChild(h);
		var tw = h.CreateTween().SetParallel(true);
		tw.TweenMethod(Callable.From<float>(r => { h.Yaricap = r; h.Kalinlik = 4f + 14f * (1f - r / sonYaricap); h.QueueRedraw(); }),
			10f, sonYaricap, sure).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tw.TweenProperty(h, "modulate:a", 0f, sure);
		tw.Chain().TweenCallback(Callable.From(h.QueueFree));
	}
}
