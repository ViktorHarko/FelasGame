using Godot;
using System;

// Цей клас — чиста логіка. Він не знає про спрайти чи рух.
public partial class HealthComponent : Node
{
	[Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	[Signal] public delegate void DiedEventHandler();

	[Export] public int MaxHealth = 5;
	[Export] public bool IsPlayer = false; // Чи це здоров'я гравця
	private int _currentHealth;

	public int CurrentHealth => _currentHealth;

	public override void _Ready()
	{
		// Відновлюємо збережене здоров'я тільки для гравця
		if (IsPlayer && GameManager.Instance != null && GameManager.Instance.SavedPlayerHealth.HasValue)
		{
			_currentHealth = GameManager.Instance.GetSavedHealth(MaxHealth);
			GD.Print($"[HEALTH] Відновлено здоров'я: {_currentHealth}/{MaxHealth}");
		}
		else
		{
			_currentHealth = MaxHealth;
		}
		
		// Сповіщаємо UI про поточне здоров'я
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	public void Damage(int amount)
	{
		_currentHealth -= amount;
		
		// Обмежуємо мінімум 0
		if (_currentHealth < 0) _currentHealth = 0;
		
		// Сповіщаємо всіх зацікавлених, що здоров'я змінилося
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		// Зберігаємо здоров'я в GameManager тільки для гравця і якщо живий
		if (IsPlayer && _currentHealth > 0 && GameManager.Instance != null)
		{
			GameManager.Instance.SavePlayerHealth(_currentHealth, MaxHealth);
		}

		if (_currentHealth <= 0)
		{
			EmitSignal(SignalName.Died);
		}
	}
}
