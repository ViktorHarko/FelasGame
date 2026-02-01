using Godot;
using System;

public partial class ControlMenu : Control
{
	[Export] private Button newGameButton;
	[Export] private Button exitButton;
	[Export] private Button continueButton;

	// Шлях до сцени гри
	[Export] private string gameScenePath = "res://Levels/Room1/Room1.tscn";

	public override void _Ready()
	{
		newGameButton ??= GetNode<Button>("VBoxContainer/newGameButton");
		exitButton ??= GetNode<Button>("VBoxContainer/exitButton");
		continueButton ??= GetNode<Button>("VBoxContainer/Continue");
		// Додаємо слухачів на події
		newGameButton.Pressed += OnNewGamePressed;
		exitButton.Pressed += OnExitPressed;
		continueButton.Pressed += OnContinuePressed;

		// Показуємо/ховаємо кнопку Continue залежно від наявності збереження
		UpdateContinueButtonVisibility();

		// Початковий фокус
		newGameButton.GrabFocus();
	}

	private void UpdateContinueButtonVisibility()
	{
		if (continueButton != null && GameManager.Instance != null)
		{
			continueButton.Visible = GameManager.Instance.HasSaveGame();
		}
	}

	public override void _Process(double delta)
	{
		// Якщо зараз жодна кнопка не у фокусі, ставимо на першу
		if (!newGameButton.HasFocus() && !exitButton.HasFocus() && !continueButton.HasFocus())
		{
			newGameButton.GrabFocus();
		}
	}

	private void OnNewGamePressed()
	{
		GD.Print("Нова гра стартує!");
		
		// Скидаємо стан гри і видаляємо збереження
		if (GameManager.Instance != null)
		{
			GameManager.Instance.ResetGame();
			GameManager.Instance.DeleteSaveGame();
		}
		
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

	private void OnContinuePressed()
	{
		GD.Print("Продовжуємо гру!");
		
		if (GameManager.Instance != null && GameManager.Instance.HasSaveGame())
		{
			string savedScene = GameManager.Instance.GetRespawnScenePath();
			if (ResourceLoader.Exists(savedScene))
			{
				GetTree().ChangeSceneToFile(savedScene);
			}
			else
			{
				GD.PrintErr("Збережена сцена не знайдена: " + savedScene);
			}
		}
	}

	private void OnExitPressed()
	{
		GD.Print("Вихід з гри");
		GetTree().Quit();
	}
}
