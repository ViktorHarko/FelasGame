using Godot;
using System;
using System.Threading.Tasks;

public partial class Portal : Area2D
{
	[Export(PropertyHint.File, "*.tscn")] public string NextScenePath;
	
	[ExportGroup("Portal Settings")]
	[Export] public string PortalName;
	[Export] public string DestinationPortalName;
	[Export] public bool SpawnOnLeft = false;

	// --- ДОДАЄМО ЗМІННУ БЛОКУВАННЯ ---
	private bool _isLocked = false; 

	public override void _Ready()
	{
		AddToGroup("Portals");
		BodyEntered += OnBodyEntered;
	}

	// Метод для тимчасового відключення
	public async void StartCooldown()
	{
		// 1. Блокуємо МИТТЄВО (для коду)
		_isLocked = true;
		
		// 2. Вимикаємо фізику (для оптимізації)
		SetDeferred("monitoring", false);
		
		// Чекаємо 1 секунду
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		
		// Вмикаємо назад
		_isLocked = false;
		SetDeferred("monitoring", true);
	}

	private async void OnBodyEntered(Node2D body)
	{
		// --- ПЕРЕВІРКА ---
		// Якщо портал заблокований, ми ігноруємо все, навіть якщо фізика спрацювала
		if (_isLocked) return;

		if (body is Player player)
		{
			if (string.IsNullOrEmpty(NextScenePath))
			{
				GD.PrintErr($"Портал {Name} не має NextScenePath!");
				return;
			}

			// Блокуємо цей портал, щоб він не спрацював двічі, поки ми ще не вийшли
			_isLocked = true; 

			player.ToggleCutscene(true);

			float dir = Mathf.Sign(player.Velocity.X);
			if (Mathf.IsZeroApprox(player.Velocity.X))
			{
				 dir = (GlobalPosition.X - player.GlobalPosition.X) > 0 ? 1 : -1;
			}

			player.ForceWalk(dir);

			await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
			SceneManager.Instance.ChangeScene(NextScenePath, DestinationPortalName);
		}
	}
}
