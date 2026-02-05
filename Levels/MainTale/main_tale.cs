using Godot;
using System;

public partial class main_tale : Node2D
{
	[ExportGroup("References")]
	[Export] public CharacterBody2D PlayerNode; // Перетягни сюди вузол гравця
	[Export] public Sprite2D RoomSprite;        // Перетягни сюди картинку заднього фону (для меж камери)

	[ExportGroup("Audio")]
	[Export] public AudioStream BackgroundMusic; // Сюди перетягуємо файл .mp3 для цієї локації

	// Змінна для малювання рамки (DEBUG), якщо потрібно буде перевірити
	private Rect2 _debugLimitRect;

	public override void _Ready()
	{
		// 1. Запускаємо музику
		if (SceneManager.Instance != null)
		{
			// Якщо музика призначена - граємо її, якщо ні - SceneManager продовжить грати попередню
			if (BackgroundMusic != null)
			{
				SceneManager.Instance.PlayMusic(BackgroundMusic);
			}
		}
		else
		{
			GD.PrintErr("[MAIN_TALE] SceneManager не знайдено! Музика не гратиме.");
		}

		// 2. Налаштовуємо рівень (камера, позиція гравця)
		// Викликаємо CallDeferred, щоб дати час фізиці та іншим вузлам ініціалізуватися
		CallDeferred(MethodName.SetupLevel);
	}

	private void SetupLevel()
	{
		// --- ПЕРЕВІРКИ ---
		if (PlayerNode == null)
		{
			GD.PrintErr($"[CRITICAL] У сцені '{Name}' не призначено PlayerNode в Інспекторі!");
			return;
		}

		// --- НАЛАШТУВАННЯ КАМЕРИ ---
		SetupCameraLimits();

		// --- СПАВН ГРАВЦЯ (ПОРТАЛИ) ---
		HandlePortalSpawn();
	}

	private void SetupCameraLimits()
	{
		// Якщо ми не призначили спрайт кімнати, камера не буде обмежена
		if (RoomSprite == null) 
		{
			GD.Print("[MAIN_TALE] RoomSprite не призначено. Ліміти камери не встановлені.");
			return;
		}

		var camera = PlayerNode.GetNodeOrNull<Camera2D>("Camera2D");
		if (camera == null) return;

		// Отримуємо реальні розміри картинки фону
		Rect2 rect = RoomSprite.GetRect();
		Transform2D trans = RoomSprite.GlobalTransform;
		
		// Конвертуємо локальні координати кутів спрайта в глобальні координати світу
		Vector2 topLeft = trans.BasisXform(rect.Position) + trans.Origin;
		Vector2 bottomRight = trans.BasisXform(rect.End) + trans.Origin;

		// Знаходимо крайні точки
		int left = (int)Mathf.Min(topLeft.X, bottomRight.X);
		int right = (int)Mathf.Max(topLeft.X, bottomRight.X);
		int top = (int)Mathf.Min(topLeft.Y, bottomRight.Y);
		int bottom = (int)Mathf.Max(topLeft.Y, bottomRight.Y);

		// Задаємо ліміти камері
		camera.LimitLeft = left;
		camera.LimitTop = top;
		camera.LimitRight = right;
		camera.LimitBottom = bottom;
		
		// (Опціонально) Малюємо жовту рамку в редакторі, щоб бачити межі
		camera.EditorDrawLimits = true;

		// Зберігаємо для дебагу (якщо розкоментуєш _Draw)
		_debugLimitRect = new Rect2(left, top, right - left, bottom - top);
	}

	private void HandlePortalSpawn()
	{
		if (SceneManager.Instance == null) return;

		string targetPortalID = SceneManager.Instance.TargetPortalID;

		// Якщо ми прийшли сюди не через портал (просто запустили сцену), нічого не робимо
		if (string.IsNullOrEmpty(targetPortalID)) return;

		var portals = GetTree().GetNodesInGroup("Portals");

		foreach (Node node in portals)
		{
			if (node is Portal portal && portal.PortalName == targetPortalID)
			{
				GD.Print($"[MAIN_TALE] Гравець прибув до порталу: {portal.PortalName}");
				
				// Вимикаємо портал на секунду, щоб не телепортувало назад
				portal.StartCooldown(); 

				// Ставимо гравця
				float offset = portal.SpawnOnLeft ? -20 : 20; 
				PlayerNode.GlobalPosition = new Vector2(portal.GlobalPosition.X + offset, portal.GlobalPosition.Y);

				// Запускаємо анімацію виходу (катсцену)
				if (PlayerNode is Player playerScript)
				{
					float walkDir = portal.SpawnOnLeft ? -1 : 1;
					RunExitCutscene(playerScript, walkDir);
				}
				
				break; 
			}
		}
		
		// Очищаємо ID цілі, щоб наступні завантаження не збивалися
		SceneManager.Instance.TargetPortalID = "";
	}

	// Асинхронний метод для автоматичного ходіння від дверей
	private async void RunExitCutscene(Player player, float direction)
	{
		// Забираємо керування у гравця
		player.ToggleCutscene(true);
		
		// Змушуємо йти
		player.ForceWalk(direction);
		
		// Чекаємо 0.5 секунди
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
		
		// Повертаємо керування
		player.ToggleCutscene(false);
	}
}
