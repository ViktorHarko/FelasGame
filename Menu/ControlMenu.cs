using Godot;
using System;

public partial class ControlMenu : Control
{
	[Export] private Button newGameButton;
	[Export] private Button exitButton;

	// Шлях до сцени гри
	[Export] private string gameScenePath = "res://Levels/Room1/Room1.tscn";

	public override void _Ready()
	{
		newGameButton ??= GetNode<Button>("VBoxContainer/newGameButton");
		exitButton ??= GetNode<Button>("VBoxContainer/exitButton");

		// Додаємо слухачів на події
		newGameButton.Pressed += OnNewGamePressed;
		exitButton.Pressed += OnExitPressed;

		// Початковий фокус
		newGameButton.GrabFocus();
	}

	public override void _Process(double delta)
	{
		// Якщо зараз жодна кнопка не у фокусі, ставимо на першу
		if (!newGameButton.HasFocus() && !exitButton.HasFocus())
		{
			newGameButton.GrabFocus();
		}
	}

	private void OnNewGamePressed()
	{
		GD.Print("Нова гра стартує!");
		// Завантажуємо сцену гри
		if (ResourceLoader.Exists(gameScenePath))
		{
			GetTree().ChangeSceneToFile(gameScenePath);
		}
		else
		{
			GD.PrintErr("Сцена гри не знайдена за шляхом: " + gameScenePath);
		}
	}

	private void OnExitPressed()
	{
		GD.Print("Вихід з гри");
		GetTree().Quit();
	}
}
