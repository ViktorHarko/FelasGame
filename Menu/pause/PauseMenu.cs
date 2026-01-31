using Godot;
using System;

public partial class PauseMenu : Control
{
	[Export] private string menuPath = "res://Menu/MainMenu.tscn";
	[Export] private Button resumeButton;
	[Export] private Button restartButton;
	[Export] private Button quitButton;
	public override void _Ready()
	{
		Visible = false; // приховуємо меню за замовчуванням

		// Підключаємо кнопки
		resumeButton = GetNode<Button>("PanelContainer/VBoxContainer/Button");
		resumeButton.Pressed += OnResumePressed;

		restartButton = GetNode<Button>("PanelContainer/VBoxContainer/Button2");
		restartButton.Pressed += OnRestartPressed;

		quitButton = GetNode<Button>("PanelContainer/VBoxContainer/Button3");
		quitButton.Pressed += OnQuitPressed;
		
		resumeButton.GrabFocus();
	}
	
	public override void _Process(double delta)
	{
		// Якщо зараз жодна кнопка не у фокусі, ставимо на першу
		if (!resumeButton.HasFocus() && !restartButton.HasFocus() && !quitButton.HasFocus())
		{
			resumeButton.GrabFocus();
		}
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
