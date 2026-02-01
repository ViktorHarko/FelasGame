using Godot;
using System;

public partial class Frog : CharacterBody2D
{
	[ExportGroup("Components")]
	[Export] public HealthComponent HealthComp;

	[ExportGroup("Stats")]
	[Export] public int Damage = 1;
	[Export] public float JumpSpeed = 450.0f;
	[Export] public float JumpForce = -350.0f;
	[Export] public float ChaseJumpSpeed = 520.0f;
	[Export] public float Gravity = 980.0f;

	[ExportGroup("Combat")]
	[Export] public float KnockbackForce = 200.0f;
	[Export] public float StunTime = 0.4f;
	[Export] public float AttackCooldown = 1.0f;
	[Export] public float TongueReach = 60.0f;

	[ExportGroup("Timing")]
	[Export] public float IdleTime = 1.5f;
	[Export] public float ChaseIdleTime = 0.5f;

	enum FrogState { Idle, Jump, Chase, Attack, Hurt, Dead }
	private FrogState _currentState = FrogState.Idle;

	private AnimatedSprite2D _sprite;
	private RayCast2D _floorDetector;
	private Area2D _detectionArea;
	private Area2D _tongueHitbox;
	private Area2D _tongueHitboxExtended;
	private Hurtbox _hurtbox;

	private int _direction = 1;
	private float _stateTimer = 0.0f;
	private Node2D _target = null;
	private bool _justJumped = false;

	public override void _Ready()
	{
		string myID = GetUniqueID();

		if (GameManager.Instance != null && GameManager.Instance.IsEnemyDead(myID))
		{
			QueueFree();
			return;
		}
		
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_floorDetector = GetNode<RayCast2D>("RayCast2D");
		_detectionArea = GetNode<Area2D>("DetectionArea");
		_tongueHitbox = GetNode<Area2D>("TongueHitbox");
		_tongueHitboxExtended = GetNode<Area2D>("TongueHitboxExtended");
		_hurtbox = GetNode<Hurtbox>("Hurtbox");

		_detectionArea.BodyEntered += OnBodyDetected;
		_detectionArea.BodyExited += OnBodyLost;   
		_hurtbox.HitReceived += TakeHitLogic;

		if (HealthComp != null)
		{
			HealthComp.Died += OnDeath;
		}

		_currentState = FrogState.Idle;
		_stateTimer = IdleTime;
		_sprite.Play("idle");

		GD.Print("ДЖябаа туйки!");
	}

	public override void _PhysicsProcess(double delta)
	{   
		if (_currentState == FrogState.Dead) return;

		float fDelta = (float)delta;
		Vector2 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y += Gravity * fDelta;
		}

		switch (_currentState)
		{
			case FrogState.Idle:
				velocity.X = 0;
				ProcessIdle(fDelta);
				break;
				
			case FrogState.Jump:
				if (_justJumped)
				{
					velocity.Y = JumpForce;
					_justJumped = false;
				}

				if (IsOnWall())
				{
					_direction *= -1;
					_currentState = FrogState.Idle;
					_stateTimer = IdleTime;
					velocity.X = 0;
				}
				else
				{
					velocity.X = ProcessJump(velocity);
				}
				break;
				
			case FrogState.Chase:
				velocity.X = ProcessChase();
				break;
				
			case FrogState.Attack:
				velocity.X = 0;
				ProcessAttack(fDelta);
				break;
				
			case FrogState.Hurt:
				velocity.X = Mathf.MoveToward(velocity.X, 0, 800.0f * fDelta);
				ProcessHurt(fDelta);
				break;
		}

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation();
		UpdateFacingDirection();
	}

	private string GetUniqueID()
	{
		string sceneName = GetTree().CurrentScene != null ? GetTree().CurrentScene.Name : "UnknownScene";
		return sceneName + "_" + GetPath().ToString();
	}

	private void OnBodyDetected(Node2D body)
	{
		if (!body.IsInGroup("Player")) return;

		_target = body;

		if (_currentState == FrogState.Jump && !IsOnFloor())
			return;

		_currentState = FrogState.Chase;
		_stateTimer = ChaseIdleTime;
	}


	private void OnBodyLost(Node2D body)
	{
		if (body == _target)
		{
			_target = null;
			GD.Print("Жаба вгубилась");
		}
	}

	private void TakeHitLogic(int damage, Vector2 sourcePosition)
	{
		if (_currentState == FrogState.Dead) return;
		
		_currentState = FrogState.Hurt;
		_stateTimer = StunTime;
		
		_sprite.Modulate = Colors.Red;
		GetTree().CreateTimer(0.25f).Timeout += () => 
		{
			if (IsInstanceValid(this)) _sprite.Modulate = Colors.White;
		};
		
		Vector2 knockbackDir = (GlobalPosition - sourcePosition).Normalized();
		Velocity = new Vector2(knockbackDir.X * KnockbackForce, -150);
		
		_tongueHitbox.Monitoring = false;
		_tongueHitboxExtended.Monitoring = false;
		
		GD.Print("Жаба -хп");
	}

	private void OnDeath()
	{
		if (_currentState == FrogState.Dead) return;
		
		if (GameManager.Instance != null)
		{
			GameManager.Instance.RegisterDeath(GetUniqueID());
		}
		
		_currentState = FrogState.Dead;
		
		GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
		_detectionArea.SetDeferred("monitoring", false);
		_tongueHitbox.SetDeferred("monitoring", false);
		_tongueHitboxExtended.SetDeferred("monitoring", false);
		
		GD.Print("Жаба мертва ааааа шкода");
		
		GetTree().CreateTimer(2.0f).Timeout += () => 
		{
			if (IsInstanceValid(this)) QueueFree();
		};
	}

	private void InitiateJump()
	{
		bool isCliffAhead = IsOnFloor() && !_floorDetector.IsColliding();

		if (isCliffAhead || IsOnWall())
		{
			_direction *= -1;
			_currentState = FrogState.Idle;
			_stateTimer = IdleTime;
			return;
		}

		_currentState = FrogState.Jump;
		_justJumped = true;

		GD.Print("Джяябааа стрибнула, ух ти яка");
	}


	private void ProcessIdle(float delta)
	{
		_stateTimer -= delta;
		
		if (_stateTimer <= 0)
		{
			InitiateJump();
		}
	}


	private float ProcessJump(Vector2 currentVelocity)
	{
		// Приземлятись тільки якщо падаємо вниз (не летимо вгору)
		if (IsOnFloor() && currentVelocity.Y >= 0)
		{
			if (_target != null)
			{
				_currentState = FrogState.Chase;
				_stateTimer = ChaseIdleTime;
			}
			else
			{
				_currentState = FrogState.Idle;
				_stateTimer = IdleTime;
			}
		}

		return (_target != null ? ChaseJumpSpeed : JumpSpeed) * _direction;
	}

	private float ProcessChase()
	{
		if (_target == null)
		{
			_currentState = FrogState.Idle;
			_stateTimer = IdleTime;
			return 0;
		}
		
		float distanceToPlayer = Mathf.Abs(_target.GlobalPosition.X - GlobalPosition.X);
		
		if (distanceToPlayer <= TongueReach && IsOnFloor())
		{
			SwitchToAttack();
			return 0;
		}
		
		_direction = (_target.GlobalPosition.X > GlobalPosition.X) ? 1 : -1;
		
		_stateTimer -= (float)GetPhysicsProcessDeltaTime();
		if (_stateTimer <= 0 && IsOnFloor())
		{
			InitiateJump();
		}
		
		return 0;
	}

	private void ProcessAttack(float delta)
	{
		_stateTimer -= delta;

		if (_stateTimer >= 0.7f)
		{
			_tongueHitbox.Monitoring = true;
			_tongueHitboxExtended.Monitoring = false;
			GD.Print($"Attack phase 1: tongue close only. Timer: {_stateTimer:F2}");
		}
		else if (_stateTimer >= 0.4f)
		{
			_tongueHitbox.Monitoring = true;
			_tongueHitboxExtended.Monitoring = true;
			GD.Print($"Attack phase 2: tongue extended! Timer: {_stateTimer:F2}");
		}
		else
		{
			_tongueHitbox.Monitoring = false;
			_tongueHitboxExtended.Monitoring = false;
		}
		
		if (_stateTimer <= 0)
		{
			_tongueHitbox.Monitoring = false;
			_tongueHitboxExtended.Monitoring = false;
			
			if (_target != null)
			{
				float distanceToPlayer = Mathf.Abs(_target.GlobalPosition.X - GlobalPosition.X);
				if (distanceToPlayer <= TongueReach)
				{
					SwitchToAttack();
				}
				else
				{
					_currentState = FrogState.Chase;
					_stateTimer = ChaseIdleTime;
				}
			}
			else
			{
				_currentState = FrogState.Idle;
				_stateTimer = IdleTime;
			}
		}
	}

	private void ProcessHurt(float delta)
	{
		_stateTimer -= delta;
		
		if (_stateTimer <= 0)
		{
			if (_target != null)
			{
				_currentState = FrogState.Chase;
				_stateTimer = ChaseIdleTime;
			}
			else
			{
				_currentState = FrogState.Idle;
				_stateTimer = IdleTime;
			}
		}
	}

	private void UpdateAnimation()
	{
		if (_currentState == FrogState.Dead)
		{
			if (_sprite.Animation != "explosion")
			{
				_sprite.Play("explosion");
			}
			return;
		}
		
		if (_currentState == FrogState.Hurt)
		{
			string hurtAnim = _sprite.SpriteFrames.HasAnimation("hurt") ? "hurt" : "idle";
			if (_sprite.Animation != hurtAnim)
			{
				_sprite.Play(hurtAnim);
			}
			return;
		}
		
		if (_currentState == FrogState.Attack)
		{
			if (_sprite.Animation != "attack")
			{
				_sprite.Play("attack");
			}
			return;
		}

		if (_currentState == FrogState.Jump)
		{
			if (_sprite.Animation != "hop")
			{
				_sprite.Play("hop");
			}
			return;
		}
		
		if (_sprite.Animation != "idle")
		{
			_sprite.Play("idle");
		}
	}

	private void UpdateFacingDirection()
	{
		if (_currentState == FrogState.Dead || _currentState == FrogState.Hurt) 
			return;
		
		_sprite.FlipH = (_direction < 0);
	
		_tongueHitbox.Scale = new Vector2(_direction, 1);
		_tongueHitboxExtended.Scale = new Vector2(_direction, 1);
		_detectionArea.Scale = new Vector2(_direction, 1);
		
		Vector2 rayPos = _floorDetector.Position;
		rayPos.X = Mathf.Abs(rayPos.X) * _direction;
		_floorDetector.Position = rayPos;
	}

	private void SwitchToAttack()
	{
		_currentState = FrogState.Attack;
		_stateTimer = AttackCooldown;
		
		GD.Print("Джяябааа атакує язикомб ухтишка");
	}
}
