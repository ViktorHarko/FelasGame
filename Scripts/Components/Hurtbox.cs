using Godot;

public partial class Hurtbox : Area2D
{
	[Export] public HealthComponent HealthComponent;

	// Створюємо сигнал: "Мене вдарили! (На скільки, Звідки)"
	[Signal] public delegate void HitReceivedEventHandler(int damage, Vector2 sourcePosition);

	[Signal] public delegate void InvincibilityStartedEventHandler();
	[Signal] public delegate void InvincibilityEndedEventHandler();

	[ExportGroup("Invincibility")]
	[Export] public float InvincibilityDuration = 1.2f;
	[Export] public bool EnableInvincibility = true;

	private bool _isInvincible = false;
	private float _invincibilityTimer = 0f;

	public override void _Process(double delta)
	{
		// Оновлюємо таймер невразливості
		if (_isInvincible)
		{
			_invincibilityTimer -= (float)delta;
			if (_invincibilityTimer <= 0)
			{
				EndInvincibility();
			}
		}
	}

	public void TakeHit(int damage, Vector2 sourcePosition)
	{
		if (_isInvincible && EnableInvincibility)
		{
			return;
		}

		// 1. Віднімаємо здоров'я (якщо є компонент)
		if (HealthComponent != null)
		{
			HealthComponent.Damage(damage);
		}

		GD.Print($"[HURTBOX] TakeHit dmg={damage} healthIsNull={HealthComponent == null}");


		// 2. Кричимо "Мене вдарили!", щоб Enemy.cs це почув і увімкнув анімацію
		EmitSignal(SignalName.HitReceived, damage, sourcePosition);

		if (EnableInvincibility)
		{
			StartInvincibility();
		}
	}

	private void StartInvincibility()
	{
		_isInvincible = true;
		_invincibilityTimer = InvincibilityDuration;
		EmitSignal(SignalName.InvincibilityStarted);
	}

	private void EndInvincibility()
	{
		_isInvincible = false;
		_invincibilityTimer = 0f;
		EmitSignal(SignalName.InvincibilityEnded);
	}

	public bool IsInvincible() => _isInvincible;

	public void ForceInvincibility(float duration)
	{
		_invincibilityTimer = duration;
		if (!_isInvincible)
		{
			StartInvincibility();
		}
	}
}
