using Godot;
using System;

public partial class Room1 : Node2D
{
	[Export] public CharacterBody2D PlayerNode;
	[Export] public Sprite2D RoomSprite;
[ExportGroup("Audio")]
	[Export] public AudioStream BackgroundMusic; // Сюди перетягуємо файл .mp3

	public override void _Ready()
	{
		// Запускаємо музику цієї локації через менеджер
		if (SceneManager.Instance != null)
		{
			SceneManager.Instance.PlayMusic(BackgroundMusic);
		}

		CallDeferred(MethodName.SetupLevel);
	}
	// Змінна для малювання рамки (DEBUG)
	private Rect2 _debugLimitRect;

	

	private void SetupLevel()
	{
		// --- 1. ПЕРЕВІРКИ ---
		if (PlayerNode == null)
		{
			GD.PrintErr($"[CRITICAL] У сцені '{Name}' не призначено PlayerNode!");
			return;
		}
		if (SceneManager.Instance == null)
		{
			 GD.PrintErr("[CRITICAL] SceneManager не працює! Перевірте Autoload.");
			 return;
		}

		// --- 2. КАМЕРА ---
		SetupCameraLimits();

		// --- 3. СПАВН ГРАВЦЯ ---
		string targetPortalID = SceneManager.Instance.TargetPortalID;

		if (!string.IsNullOrEmpty(targetPortalID))
		{
			var portals = GetTree().GetNodesInGroup("Portals");

			foreach (Node node in portals)
			{
				if (node is Portal portal && portal.PortalName == targetPortalID)
				{
					GD.Print($"[WORLD] Гравець прибув до: {portal.PortalName}");
					
					// 1. Тимчасово вимикаємо портал, щоб не засмоктало назад
					portal.StartCooldown(); 

					// 2. Ставимо гравця трохи збоку від дверей
					// Якщо SpawnOnLeft (зліва), то зміщення -20, інакше +20.
					// (Ставимо близько, бо він зараз сам відійде)
					float offset = portal.SpawnOnLeft ? -20 : 20; 
					PlayerNode.GlobalPosition = new Vector2(portal.GlobalPosition.X + offset, portal.GlobalPosition.Y);

					// 3. ЗАПУСКАЄМО АНІМАЦІЮ ВИХОДУ
					// Ми перевіряємо, чи PlayerNode це справді наш скрипт Player
					if (PlayerNode is Player playerScript)
					{
						// Визначаємо куди йти: 
						// Якщо з'явилися зліва (-1), то йдемо вліво. Якщо справа (1), то вправо.
						float walkDir = portal.SpawnOnLeft ? -1 : 1;
						
						// Викликаємо метод, який змусить його йти
						RunExitCutscene(playerScript, walkDir);
					}
					
					break; 
				}
			}
			
			// Очищаємо ID
			SceneManager.Instance.TargetPortalID = "";
		}
	}

	// --- НОВИЙ МЕТОД: Змушує гравця відійти від дверей ---
	private async void RunExitCutscene(Player player, float direction)
	{
		// 1. Забираємо керування
		player.ToggleCutscene(true);
		
		// 2. Змушуємо йти у потрібний бік (це також розверне спрайт!)
		player.ForceWalk(direction);
		
		// 3. Чекаємо 0.5 секунди (поки він відійде від порталу)
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
		
		// 4. Повертаємо керування
		player.ToggleCutscene(false);
	}

	private void SetupCameraLimits()
	{
		if (RoomSprite == null) return;
		var camera = PlayerNode.GetNodeOrNull<Camera2D>("Camera2D");
		if (camera == null) return;

		// Вмикаємо рамку
		camera.EditorDrawLimits = true;

		Rect2 rect = RoomSprite.GetRect();
		Transform2D trans = RoomSprite.GlobalTransform;
		Vector2 topLeft = trans.BasisXform(rect.Position) + trans.Origin;
		Vector2 bottomRight = trans.BasisXform(rect.End) + trans.Origin;

		int left = (int)Mathf.Min(topLeft.X, bottomRight.X);
		int right = (int)Mathf.Max(topLeft.X, bottomRight.X);
		int top = (int)Mathf.Min(topLeft.Y, bottomRight.Y);
		int bottom = (int)Mathf.Max(topLeft.Y, bottomRight.Y);

		camera.LimitLeft = left;
		camera.LimitTop = top;
		camera.LimitRight = right;
		camera.LimitBottom = bottom;
		
		_debugLimitRect = new Rect2(left, top, right - left, bottom - top);
		QueueRedraw();
	}

	//public override void _Draw()
	//{
		//if (_debugLimitRect.Size != Vector2.Zero)
			//DrawRect(_debugLimitRect, Colors.Red, false, 5.0f);
	//}
}
