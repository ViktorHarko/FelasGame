using Godot;
using System;
using System.Threading.Tasks;

public partial class SceneManager : CanvasLayer
{
	public static SceneManager Instance { get; private set; }
	public string TargetPortalID { get; set; } = "";

	private AnimationPlayer _animPlayer;
	private ColorRect _blackScreen;
	
	// Посилання на плеєр музики
	private AudioStreamPlayer _musicPlayer;

	public override void _Ready()
	{
		Instance = this;
		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		_blackScreen = GetNode<ColorRect>("ColorRect");
		_musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer"); // Переконайся, що вузол так називається!
		
		_blackScreen.Color = new Color(0, 0, 0, 0);
	}

	// --- НОВИЙ МЕТОД ДЛЯ МУЗИКИ ---
	public void PlayMusic(AudioStream newSong)
	{
		// 1. Якщо пісні немає (тиша) — вимикаємо
		if (newSong == null)
		{
			_musicPlayer.Stop();
			return;
		}

		// 2. Якщо ця пісня ВЖЕ грає — нічого не робимо (щоб не починалася спочатку)
		if (_musicPlayer.Stream == newSong && _musicPlayer.Playing)
		{
			return; 
		}

		// 3. Ставимо нову пісню і граємо
		_musicPlayer.Stream = newSong;
		_musicPlayer.Play();
	}

	public async void ChangeScene(string scenePath, string portalID)
	{
		TargetPortalID = portalID;

		// 1. Граємо анімацію затемнення
		_animPlayer.Play("fade_out");
		await ToSignal(_animPlayer, "animation_finished");

		// 2. Змінюємо сцену
		GetTree().ChangeSceneToFile(scenePath);

		// 3. Чекаємо, поки нова сцена завантажиться і виставить гравця
		// (Анімацію "fade_in" ми викличемо вже з рівня або автоматично тут)
		_animPlayer.Play("fade_in");
	}
}
