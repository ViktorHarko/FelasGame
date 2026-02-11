using Godot;
using System;

public partial class Grass : Area2D
{
	[Export] public Sprite2D Sprite;
	[Export] public float BendStrength = 0.5f; // Сила вигину (для шейдера краще менші числа)

	private Tween _tween;
	private ShaderMaterial _material; // Посилання на матеріал

	public override void _Ready()
	{
		if (Sprite == null) Sprite = GetNode<Sprite2D>("Sprite2D");
		
		// --- ВИПРАВЛЕННЯ ТУТ ---
		// Ми створюємо дублікат матеріалу, щоб цей кущ мав свій власний шейдер
		if (Sprite.Material is ShaderMaterial mat)
		{
			_material = (ShaderMaterial)mat.Duplicate();
			Sprite.Material = _material; // Призначаємо дублікат назад спрайту
		}
		else
		{
			GD.PrintErr("УВАГА: ShaderMaterial не знайдено!");
		}

		BodyEntered += OnBodyEntered;

		// --- ВАРІАТИВНІСТЬ ---
		float randomScale = (float)GD.RandRange(0.8, 1.2);
		Sprite.Scale = new Vector2(randomScale, randomScale);

		float randomColor = (float)GD.RandRange(0.8, 1.0);
		Sprite.Modulate = new Color(randomColor, randomColor, randomColor);
		
		if (GD.Randf() > 0.5f) Sprite.FlipH = true;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is CharacterBody2D && _material != null)
		{
			BendGrass(body.GlobalPosition);
		}
	}

	private void BendGrass(Vector2 bodyPos)
	{
		// Визначаємо напрямок: 1 (вправо) або -1 (вліво)
		float direction = bodyPos.X < GlobalPosition.X ? 1 : -1;
		
		if (_tween != null && _tween.IsRunning())
		{
			_tween.Kill();
		}
		_tween = CreateTween();

		// Розраховуємо цільове значення вигину
		float targetBend = BendStrength * direction;

		// --- АНІМАЦІЯ ШЕЙДЕРА ---
		// Ми використовуємо TweenMethod, щоб змінювати параметр "bend" у шейдері
		
		// 1. Різкий нахил в бік руху
		_tween.TweenMethod(Callable.From<float>(SetShaderBend), 0.0f, targetBend, 0.2f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		// 2. Пружинистий відскок назад (переліт через центр)
		_tween.TweenMethod(Callable.From<float>(SetShaderBend), targetBend, -targetBend * 0.5f, 0.3f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			
		// 3. Повернення в спокій (0)
		_tween.TweenMethod(Callable.From<float>(SetShaderBend), -targetBend * 0.5f, 0.0f, 0.5f)
			.SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	// Допоміжний метод для встановлення значення в шейдер
	private void SetShaderBend(float value)
	{
		_material.SetShaderParameter("bend", value);
	}
}
