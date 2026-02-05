using Godot;

public partial class CameraLimiter : Area2D
{
	private CollisionShape2D _shape;

	public override void _Ready()
	{
		_shape = GetNode<CollisionShape2D>("CollisionShape2D");
		// Вмикаємо моніторинг примусово
		Monitoring = true; 
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		// ДІАГНОСТИКА 1: Чи взагалі хтось зайшов?
		GD.Print($"Хтось зайшов у зону! Це: {body.Name}");

		if (body is Player player)
		{
			// ДІАГНОСТИКА 2: Це гравець. Шукаємо камеру...
			Camera2D cam = player.GetNodeOrNull<Camera2D>("Camera2D");
			
			if (cam != null)
			{
				GD.Print("Камеру знайдено! Оновлюю ліміти.");
				UpdateCameraLimits(cam);
			}
			else
			{
				GD.PrintErr("ПОМИЛКА: У гравця немає вузла з назвою 'Camera2D'!");
			}
		}
	}

	private void UpdateCameraLimits(Camera2D cam)
	{
		// Отримуємо форму прямокутника
		RectangleShape2D rectShape = (RectangleShape2D)_shape.Shape;
		
		// Отримуємо розмір і позицію відносно центру
		Vector2 size = rectShape.Size * _shape.GlobalScale; // <--- ВРАХОВУЄМО МАСШТАБ!
		Vector2 globalPos = _shape.GlobalPosition;

		// Оскільки RectangleShape2D центрується посередині (0,0), 
		// то лівий верхній кут - це (Position - Size/2)
		// Але ми вже маємо глобальну позицію центру шейпа
		
		float halfWidth = size.X / 2;
		float halfHeight = size.Y / 2;

		// Встановлюємо межі
		cam.LimitLeft = (int)(globalPos.X - halfWidth);
		cam.LimitRight = (int)(globalPos.X + halfWidth);
		cam.LimitTop = (int)(globalPos.Y - halfHeight);
		cam.LimitBottom = (int)(globalPos.Y + halfHeight);
		
		GD.Print($"Межі оновлено: L={cam.LimitLeft} R={cam.LimitRight} T={cam.LimitTop} B={cam.LimitBottom}");
	}
}
