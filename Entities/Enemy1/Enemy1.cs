using Godot;
using System;

public partial class Enemy1 : CharacterBody2D
{
	// --- КОМПОНЕНТИ ---
	[ExportGroup("Components")]
	[Export] public HealthComponent HealthComp;

	[ExportGroup("Stats")]
	[Export] public int Damage = 1;
	[Export] public float PatrolSpeed = 60.0f;
	[Export] public float ChaseSpeed = 120.0f;
	[Export] public float Gravity = 980.0f;
	
	[ExportGroup("Combat")]
	[Export] public float KnockbackForce = 200.0f;
	[Export] public float StunTime = 0.4f;
	[Export] public float AttackCooldown = 1.0f;

	enum EnemyState { Patrol, Idle, Chase, Attack, Hurt, Dead }
	private EnemyState _currentState = EnemyState.Patrol;
	
	private AnimatedSprite2D _sprite;
	private RayCast2D _floorDetector;
	private Area2D _detectionArea;
	private Area2D _attackArea;
	
	private int _direction = 1; 
	private float _stateTimer = 0.0f;
	private Node2D _target = null;

	public override void _Ready()
	{
		// 1. ПЕРЕВІРКА НА СТАРТІ
		string myID = GetUniqueID();
		if (GameManager.Instance != null && GameManager.Instance.IsEnemyDead(myID))
		{
			QueueFree();
			return;
		}

		// Отримуємо вузли
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_floorDetector = GetNode<RayCast2D>("RayCast2D");
		_detectionArea = GetNode<Area2D>("DetectionArea");
		_attackArea = GetNode<Area2D>("AttackArea");

		_detectionArea.BodyEntered += OnBodyDetected;
		_detectionArea.BodyExited += OnBodyLost;
		_sprite.AnimationFinished += OnAnimationFinished;
		
		var myHurtbox = GetNode<Hurtbox>("Hurtbox");
		myHurtbox.HitReceived += TakeHitLogic;
		// ПІДПИСКА НА ПОДІЇ КОМПОНЕНТА
		if (HealthComp != null)
		{
			HealthComp.Died += OnDeath;
		}
		else
		{
			GD.PrintErr("HealthComponent не прив'язано до ворога в Інспекторі!");
		}

		_sprite.Play("run");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_currentState == EnemyState.Dead) return;

		float fDelta = (float)delta;
		Vector2 velocity = Velocity;

		if (!IsOnFloor()) velocity.Y += Gravity * fDelta;

		switch (_currentState)
		{
			case EnemyState.Patrol:
				velocity.X = ProcessPatrol();
				break;
			case EnemyState.Idle:
				velocity.X = 0;
				ProcessIdle(fDelta);
				break;
			case EnemyState.Chase:
				velocity.X = ProcessChase();
				break;
			case EnemyState.Attack:
				velocity.X = 0; 
				ProcessAttack(fDelta);
				break;
			case EnemyState.Hurt:
				velocity.X = Mathf.MoveToward(velocity.X, 0, 800.0f * fDelta);
				ProcessHurt(fDelta); // Ось цей метод тепер існує знизу!
				break;
		}

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation(velocity);
		UpdateFacingDirection();
	}

	// --- ЛОГІКА ОТРИМАННЯ УРОНУ ---
	public void TakeHitLogic(int amount, Vector2 sourcePosition)
{
	if (_currentState == EnemyState.Dead) return;

	// 1. МИТТЄВО змінюємо стан (це вимикає логіку атаки у _PhysicsProcess)
	_currentState = EnemyState.Hurt;
	_stateTimer = StunTime;

	// 2. ПРИМУСОВО перебиваємо анімацію
	// Якщо у тебе є анімація "hurt" - встав її сюди. 
	// Якщо немає - використовуй "idle", щоб ворог виглядав розгубленим.
	if (_sprite.SpriteFrames.HasAnimation("hurt"))
	{
		_sprite.Play("hurt");
	}
	else
	{
		_sprite.Play("idle");
	}

	// 3. Візуальний ефект (блимання червоним)
	_sprite.Modulate = Colors.Red;
	
	// Створюємо таймер для повернення кольору
	GetTree().CreateTimer(0.25f).Timeout += () => 
	{
		// Перевірка IsInstanceValid потрібна, щоб гра не впала, якщо ворог помре за цей час
		if (IsInstanceValid(this)) _sprite.Modulate = Colors.White;
	};

	// 4. Фізичне відкидання
	Vector2 knockbackDir = (GlobalPosition - sourcePosition).Normalized();
	Velocity = new Vector2(knockbackDir.X * KnockbackForce, -150);
}

	// --- ОБРОБКА БОЛЮ (ЯКИЙ Я ЗАБУВ У МИНУЛОМУ КОДІ) ---
	private void ProcessHurt(float delta)
	{
		_stateTimer -= delta;
		if (_stateTimer <= 0)
		{
			// Якщо є гравець поруч - біжимо за ним, якщо ні - патрулюємо
			if (_target != null) _currentState = EnemyState.Chase;
			else _currentState = EnemyState.Patrol;
		}
	}

	// --- ЛОГІКА СМЕРТІ ---
	private void OnDeath()
	{
		if (_currentState == EnemyState.Dead) return;

		if (GameManager.Instance != null)
		{
			GameManager.Instance.RegisterDeath(GetUniqueID());
		}

		_currentState = EnemyState.Dead;
		_sprite.Play("death");
		
		GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
		_detectionArea.SetDeferred("monitoring", false);
		_attackArea.SetDeferred("monitoring", false);
	}

	private string GetUniqueID()
	{
		string sceneName = GetTree().CurrentScene != null ? GetTree().CurrentScene.Name : "UnknownScene";
		return sceneName + "_" + GetPath().ToString();
	}

	// --- AI ТА РУХ ---
	private float ProcessPatrol()
	{
		bool isCliff = IsOnFloor() && !_floorDetector.IsColliding();
		if (IsOnWall() || isCliff) { SwitchToIdle(); return 0; }
		return PatrolSpeed * _direction;
	}

	private void ProcessIdle(float delta) 
	{ 
		_stateTimer -= delta; 
		if (_stateTimer <= 0) { _direction *= -1; _currentState = EnemyState.Patrol; } 
	}

	private float ProcessChase()
	{
		if (_target == null) { _currentState = EnemyState.Patrol; return 0; }
		
		if (_attackArea.OverlapsBody(_target)) { SwitchToAttack(); return 0; }
		
		float dist = _target.Position.X - Position.X;
		if (Mathf.Abs(dist) < 10.0f) return 0;
		
		_direction = (dist > 0) ? 1 : -1;
		
		if (IsOnFloor() && !_floorDetector.IsColliding()) return 0;
		
		return ChaseSpeed * _direction;
	}

	private void SwitchToAttack()
	{
		_currentState = EnemyState.Attack;
		_stateTimer = AttackCooldown;
		_sprite.Play("attack");
		
		if (_target != null)
		{
			// Шукаємо Hurtbox у гравця
			var hurtbox = _target.GetNodeOrNull<Hurtbox>("Hurtbox");
			if (hurtbox != null)
			{
				hurtbox.TakeHit(Damage, GlobalPosition);
				if (_target.HasMethod("TakeHitLogic"))
				{
					_target.Call("TakeHitLogic", Damage, GlobalPosition);
				}
			}
			else if (_target.HasMethod("TakeDamage"))
			{
				_target.Call("TakeDamage", Damage, GlobalPosition);
			}
		}
	}

	private void ProcessAttack(float delta)
	{
		_stateTimer -= delta;
		if (_stateTimer <= 0 && !_sprite.IsPlaying()) 
		{
			if (_target == null) { _currentState = EnemyState.Patrol; return; }
			if (_attackArea.OverlapsBody(_target)) SwitchToAttack();
			else _currentState = EnemyState.Chase;
		}
	}

	private void SwitchToIdle() { _currentState = EnemyState.Idle; _stateTimer = 2.0f; _sprite.Play("idle"); }
	private void OnBodyDetected(Node2D body) { if (body.IsInGroup("Player")) { _target = body; _currentState = EnemyState.Chase; } }
	private void OnBodyLost(Node2D body) { if (body == _target) { _target = null; } }
	private void OnAnimationFinished() { if (_sprite.Animation == "death") QueueFree(); }

	private void UpdateAnimation(Vector2 velocity)
{
	// Мертві не танцюють
	if (_currentState == EnemyState.Dead) return;

	// ПРІОРИТЕТ №1: БІЛЬ
	// Якщо ворогу боляче - він не може бігти чи атакувати
	if (_currentState == EnemyState.Hurt)
	{
		// Перевіряємо, чи є анімація "hurt", інакше граємо "idle"
		string hurtAnim = _sprite.SpriteFrames.HasAnimation("hurt") ? "hurt" : "idle";
		
		// Граємо, тільки якщо вона ВЖЕ не грає (щоб не запускати спочатку кожен кадр)
		if (_sprite.Animation != hurtAnim)
		{
			_sprite.Play(hurtAnim);
		}
		return; // Виходимо, щоб інші анімації не спрацювали
	}

	// ПРІОРИТЕТ №2: АТАКА
	// Якщо ми атакуємо - не перебивати анімацію бігом
	if (_currentState == EnemyState.Attack) return;

	// ПРІОРИТЕТ №3: РУХ
	if (Mathf.Abs(velocity.X) > 0)
	{
		_sprite.Play("run");
	}
	else
	{
		_sprite.Play("idle");
	}
}

	private void UpdateFacingDirection()
	{
		if (_currentState == EnemyState.Dead || _currentState == EnemyState.Hurt) return;

		_sprite.FlipH = (_direction < 0);
		// Повертаємо зону атаки
	_attackArea.Scale = new Vector2(_direction, 1);
	
	// Повертаємо зону, якою ворог бачить гравця (очі)
	_detectionArea.Scale = new Vector2(_direction, 1);

	// 3. Повертаємо детектор підлоги (RayCast)
	// RayCast краще переміщати позицію, а не скейлити, бо це просто промінь
	Vector2 rayPos = _floorDetector.Position;
	rayPos.X = Mathf.Abs(rayPos.X) * _direction;
	_floorDetector.Position = rayPos;
	}
}
