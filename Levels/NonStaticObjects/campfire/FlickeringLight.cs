using Godot;
using System;

public partial class FlickeringLight : PointLight2D
{
	[Export] public float MinEnergy = 0.8f;
	[Export] public float MaxEnergy = 1.5f;
	
	[Export] public float MinZoom = 0.9f;
	[Export] public float MaxZoom = 1.1f;

	// Як швидко змінюється світло (чим менше число, тим швидше мерехтіння)
	[Export] public float FlickerSpeed = 0.1f; 

	public override void _Ready()
	{
		StartFlicker();
	}

	private void StartFlicker()
	{
		// 1. Створюємо Tween (анімацію через код)
		Tween tween = CreateTween();
		
		// 2. Генеруємо випадкові значення
		float targetEnergy = (float)GD.RandRange(MinEnergy, MaxEnergy);
		float targetZoom = (float)GD.RandRange(MinZoom, MaxZoom);
		
		// Додаємо трохи випадковості у час, щоб вогонь не був як метроном
		float duration = (float)GD.RandRange(FlickerSpeed * 0.5f, FlickerSpeed * 1.5f);

		// 3. Плавно змінюємо Енергію (Яскравість)
		tween.Parallel().TweenProperty(this, "energy", targetEnergy, duration);
		
		// 4. Плавно змінюємо Розмір (щоб світло "дихало")
		tween.Parallel().TweenProperty(this, "texture_scale", targetZoom, duration);

		// 5. Коли анімація закінчиться -> запускаємо її знову (рекурсія)
		tween.Finished += StartFlicker;
	}
}
