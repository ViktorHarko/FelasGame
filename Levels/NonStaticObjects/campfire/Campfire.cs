using Godot;
using System;

public partial class Campfire : Node2D
{
	private Marker2D _sitPosition;
	private Area2D _interactArea;
	private Player _playerInArea; // Змінна для гравця, який зараз у зоні

	public override void _Ready()
	{
		_sitPosition = GetNode<Marker2D>("SitPosition");
		_interactArea = GetNode<Area2D>("InteractArea");

		// Перевірка на помилки
		if (_sitPosition == null) GD.PrintErr("ПОМИЛКА: Не знайдено SitPosition у Campfire!");
		if (_interactArea == null) GD.PrintErr("ПОМИЛКА: Не знайдено InteractArea у Campfire!");

		_interactArea.BodyEntered += OnBodyEntered;
		_interactArea.BodyExited += OnBodyExited;
	}

	public override void _Process(double delta)
	{
		// Перевіряємо щокадру:
		// 1. Чи є гравець у зоні?
		// 2. Чи натиснута кнопка? (тут "ui_down" або ваша "interact")
		if (_playerInArea != null && Input.IsActionJustPressed("ui_down"))
		{
			SitPlayerDown();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		// Перевіряємо, чи це гравець
		if (body is Player p)
		{
			GD.Print("Гравець підійшов до вогнища!"); // ДЕБАГ
			_playerInArea = p;
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Player p && _playerInArea == p)
		{
			GD.Print("Гравець відійшов."); // ДЕБАГ
			_playerInArea = null;
		}
	}

	private void SitPlayerDown()
	{
		GD.Print("Сідаємо...");
		
		// Рахуємо сторону
		float dirToFire = GlobalPosition.X - _sitPosition.GlobalPosition.X;
		int facingDirection = dirToFire > 0 ? 1 : -1;

		_playerInArea.StartSitting(_sitPosition.GlobalPosition, facingDirection);
	}
}
