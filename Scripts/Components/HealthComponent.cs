using Godot;
using System;

// Цей клас — чиста логіка. Він не знає про спрайти чи рух.
public partial class HealthComponent : Node
{
	[Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	[Signal] public delegate void DiedEventHandler();

	[Export] public int MaxHealth = 5;
	private int _currentHealth;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
	}

	public void Damage(int amount)
	{
		_currentHealth -= amount;
		
		// Сповіщаємо всіх зацікавлених, що здоров'я змінилося
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		if (_currentHealth <= 0)
		{
			EmitSignal(SignalName.Died);
		}
	}
}
