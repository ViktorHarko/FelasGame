using Godot;

public partial class Hurtbox : Area2D
{
	[Export] public HealthComponent HealthComponent;

	// Створюємо сигнал: "Мене вдарили! (На скільки, Звідки)"
	[Signal] public delegate void HitReceivedEventHandler(int damage, Vector2 sourcePosition);

	public void TakeHit(int damage, Vector2 sourcePosition)
	{
		// 1. Віднімаємо здоров'я (якщо є компонент)
		if (HealthComponent != null)
		{
			HealthComponent.Damage(damage);
		}

		// 2. Кричимо "Мене вдарили!", щоб Enemy.cs це почув і увімкнув анімацію
		EmitSignal(SignalName.HitReceived, damage, sourcePosition);
	}
}
