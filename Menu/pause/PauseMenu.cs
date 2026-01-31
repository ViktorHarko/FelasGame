using Godot;
using System;

public partial class PauseMenu : Control
{
	[Export] private string menuPath = "res://Menu/MainMenu.tscn";
	
	public override void _Ready()
	{
		Visible = false; // приховуємо меню за замовчуванням

		// Підключаємо кнопки
		var resumeButton = GetNode<Button>("PanelContainer/VBoxContainer/Button");
		resumeButton.Pressed += OnResumePressed;

		var restartButton = GetNode<Button>("PanelContainer/VBoxContainer/Button2");
		restartButton.Pressed += OnRestartPressed;

		var quitButton = GetNode<Button>("PanelContainer/VBoxContainer/Button3");
		quitButton.Pressed += OnQuitPressed;
	}

	private void OnResumePressed()
	{
		TogglePause(false);
	}

	private void OnRestartPressed()
	{
		TogglePause(false);
		GetTree().ReloadCurrentScene();
		
	}

	private void OnQuitPressed()
	{
		TogglePause(false);
		if (ResourceLoader.Exists(menuPath))
		{
			GetTree().ChangeSceneToFile(menuPath);
		}
		else
		{
			GD.PrintErr("Сцена гри не знайдена за шляхом: " + menuPath);
		}
	}

	/// <summary>
	/// Метод для показу/сховання паузи
	/// </summary>
	public void TogglePause(bool pause)
	{
		Visible = pause;
		GetTree().Paused = pause;
	}
}
