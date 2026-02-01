using Godot;
using System;

// UI компонент для відображення здоров'я гравця
public partial class HealthUI : Control
{
	[Export] public HealthComponent HealthComponent; // Посилання на HealthComponent гравця

	private Label _healthLabel;
	private ProgressBar _healthBar;

	public override void _Ready()
	{
		_healthLabel = GetNode<Label>("HealthLabel");
		_healthBar = GetNode<ProgressBar>("HealthBar");

		// Підписуємось на зміни здоров'я
		if (HealthComponent != null)
		{
			HealthComponent.HealthChanged += OnHealthChanged;

			// Встановлюємо початкове значення з реальним здоров'ям
			OnHealthChanged(HealthComponent.CurrentHealth, HealthComponent.MaxHealth);
		}
		else
		{
			GD.PrintErr("HealthUI: HealthComponent не підключений!");
		}
	}

	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		// Оновлюємо текст
		if (_healthLabel != null)
		{
			_healthLabel.Text = $"HP: {currentHealth}/{maxHealth}";
		}

		// Оновлюємо прогрес-бар
		if (_healthBar != null)
		{
			_healthBar.MaxValue = maxHealth;
			_healthBar.Value = currentHealth;
		}
	}

	public override void _ExitTree()
	{
		if (HealthComponent != null)
		{
			HealthComponent.HealthChanged -= OnHealthChanged;
		}
	}
}
