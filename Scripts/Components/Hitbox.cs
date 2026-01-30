using Godot;

public partial class Hitbox : Area2D
{
	[Export] public int Damage = 1;

	public override void _Ready()
	{
		AreaEntered += OnAreaEntered;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is Hurtbox hurtbox)
		{
			// Передаємо (Урон, МОЯ ПОЗИЦІЯ)
			// GlobalPosition - це позиція меча/кулі
			hurtbox.TakeHit(Damage, GlobalPosition);
		}
	}
}
