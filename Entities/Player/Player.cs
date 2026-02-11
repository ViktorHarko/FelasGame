using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public PauseMenu PauseMenu;
	[ExportGroup("Components")]
	[Export] public HealthComponent HealthComp; // Посилання на компонент здоров'я
	
	[ExportGroup("Stats")]
	[Export] public float Speed = 250.0f;      // Швидкість ходьби
	[Export] public float RunSpeed = 450.0f;   // Швидкість бігу (після ривка)
	[Export] public float JumpVelocity = -550.0f;
	[Export] public float Acceleration = 1500.0f;
	[Export] public float Friction = 1200.0f;
	[Export] public int MaxJumps = 2;
	[Export] public float JumpCutoff = 0.6f; 

	[ExportGroup("Combat")]
	[Export] public float KnockbackForce = 300.0f;
	[Export] public float KnockbackUpForce = 160.0f;
	[Export] public float KnockbackDamping = 2200.0f;
	[Export] public float StunDuration = 0.18f;
	[Export] public float HurtInvincibilityDuration = 0.8f;
	[Export] public float EnemyTopDropDuration = 0.12f;
	[Export] public float EnemyTopDropPush = 120.0f;

	[Export] public float DownAttackSpeed = 600.0f;
	[Export] public float DownAttackBounce = -350.0f;

	[ExportGroup("Invincibility Visual")]
	[Export] public float BlinkInterval = 0.1f; 

	// Шлях до початкової сцени гри
	[Export] private string gameStartPath = "res://Levels/Room1/Room1.tscn";
	
	[ExportGroup("Dash")]
	[Export] public float DashSpeed = 650f;
	[Export] public float DashDuration = 0.12f;
	[Export] public float DashCooldown = 0.15f; 

	[ExportGroup("Wall Slide")]
	[Export] public float WallSlideMaxFallSpeed = 120f;
	[Export] public float WallSlideStickFriction = 2000f;

	[ExportGroup("Wall Jump")]
	[Export] public float WallJumpVertical = -450.0f;
	[Export] public float WallJumpHorizontal = 350.0f;
	[Export] public float WallJumpLockTime = 0.12f;

	public float Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	// Стан
	private bool _isAttacking = false;
	private bool _isDownAttacking = false;
	private bool _isDead = false;
	private bool _isInCutscene = false;
	private bool _isStunned = false;
	private bool _isDashing = false;
	private bool _dashAvailableInAir = true;
	private bool _isWallSliding = false;

	private int _jumpCount = 0;
	private float _stunTimer = 0.0f;

	private float _dashTimer = 0f;
	private float _dashCooldownTimer = 0f;

	// Невразливість та мерехтіння
	private bool _isBlinking = false;
	private float _blinkTimer = 0.0f;
	private float _enemyTopDropTimer = 0.0f;

	private float _wallJumpLockTimer = 0.0f;

	// Вузли
	private AnimatedSprite2D _animatedSprite;
	private Area2D _swordHitbox; 
	private Area2D _swordHitboxDown;
	private Hurtbox _hurtbox;

	// Аудіо
	private AudioStreamPlayer2D _audioWalk;
	private AudioStreamPlayer2D _audioJump;
	private AudioStreamPlayer2D _audioAttack;
	private AudioStreamPlayer2D _audioHurt;

	// Ray
	private RayCast2D _wallRayLeft;
	private RayCast2D _wallRayRight;


	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_swordHitbox = GetNode<Area2D>("SwordArea"); 
		_swordHitboxDown = GetNode<Area2D>("SwordAreaDown");
		_swordHitboxDown.AreaEntered += OnDownAttackHit;
		
		// Аудіо
		_audioWalk = GetNode<AudioStreamPlayer2D>("Audio_Walk");
		_audioJump = GetNode<AudioStreamPlayer2D>("Audio_Jump");
		_audioAttack = GetNode<AudioStreamPlayer2D>("Audio_Attack");
		_audioHurt = GetNode<AudioStreamPlayer2D>("Audio_Hurt");

		_animatedSprite.AnimationFinished += OnAnimationFinished;

		// Підписка на сигнали
		if (HealthComp != null)
			HealthComp.Died += OnDeath;

		_hurtbox = GetNode<Hurtbox>("Hurtbox");
		_hurtbox.HitReceived += TakeHitLogic;
		_hurtbox.InvincibilityStarted += OnInvincibilityStarted;
		_hurtbox.InvincibilityEnded += OnInvincibilityEnded;
		_hurtbox.EnableInvincibility = true;
		_hurtbox.InvincibilityDuration = HurtInvincibilityDuration;
		
		_wallRayLeft = GetNode<RayCast2D>("WallRayLeft");
		_wallRayRight = GetNode<RayCast2D>("WallRayRight");
		
		PauseMenu = GetNode<PauseMenu>("CanvasLayer2/PauseMenu");
	}


	public override void _PhysicsProcess(double delta)
	{
		if (_isDead) return;

		Vector2 velocity = Velocity;
		float fDelta = (float)delta;

		_wallJumpLockTimer -= fDelta;
		_dashCooldownTimer -= fDelta;
		
		if (_enemyTopDropTimer > 0f)
		{
			_enemyTopDropTimer -= fDelta;
			if (_enemyTopDropTimer <= 0f)
				SetCollisionMaskValue(9, true);
		}

		if (_swordHitbox != null) _swordHitbox.Monitoring = _isAttacking;
		if (_swordHitboxDown != null) _swordHitboxDown.Monitoring = _isDownAttacking;

		// 1. Катсцена
		if (_isInCutscene)
		{
			if (!IsOnFloor()) velocity.Y += Gravity * fDelta;
			UpdateAnimation(velocity.X, velocity);
			Velocity = velocity;
			MoveAndSlideWithContactDamage();
			return;
		}

		// 2. Оглушення (Stun)
		if (_stunTimer > 0)
		{
			_isStunned = true;
			_stunTimer -= fDelta;
			velocity.Y += Gravity * fDelta;
			velocity.X = Mathf.MoveToward(velocity.X, 0, KnockbackDamping * fDelta);
			Velocity = velocity;
			MoveAndSlideWithContactDamage();
			if (_stunTimer <= 0f)
				_isStunned = false;
			return;
		}
		_isStunned = false;

		// Скидання атаки
		if ((_isAttacking && _animatedSprite.Animation != "attack") ||
			(_isDownAttacking && _animatedSprite.Animation != "down"))
		{
			ResetAttackState();
		}

		// --- DASH ---
		if (_isDashing)
		{
			if (_animatedSprite.Animation != "dash")
				_animatedSprite.Play("dash");
				
			_dashTimer -= fDelta;
			velocity.Y = 0;
			Velocity = velocity;
			MoveAndSlideWithContactDamage();

			if (_dashTimer <= 0)
			{
				_isDashing = false;
				_dashCooldownTimer = DashCooldown;
				// Тут не перемикаємо анімацію вручну, UpdateAnimation зробить це в наступному кадрі
			}
			return;
		}

		// 3. Атака
		if (_isAttacking)
		{
			if (!IsOnFloor()) velocity.Y += Gravity * fDelta;
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Friction * fDelta);
			Velocity = velocity;
			MoveAndSlideWithContactDamage();
			return;
		}

		if (_isDownAttacking)
		{
			velocity.Y = DownAttackSpeed;
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Friction * 0.5f * fDelta);
			Velocity = velocity;
			MoveAndSlideWithContactDamage();

			if (IsOnFloor() || Velocity.Y < DownAttackSpeed * 0.5f)
			{
				EndDownAttack();
			}
			return;
		}

		// 4. Рух і Стрибки
		_isWallSliding = false;

		float direction = Input.GetAxis("ui_left", "ui_right");

		if (!IsOnFloor())
		{
			velocity.Y += Gravity * fDelta;
		}
		else
		{
			_jumpCount = 0;
			_dashAvailableInAir = true;
		}

		if (Input.IsActionJustReleased("ui_accept") && velocity.Y < 0)
		{
			velocity.Y *= JumpCutoff; 
		}

		int wallSide = GetWallSide();

		bool touchingWall = !IsOnFloor() && wallSide != 0 && _wallJumpLockTimer <= 0f;
		bool pressingAway = (wallSide == -1 && direction > 0) || (wallSide == 1 && direction < 0);
		bool canWallSlide = touchingWall && velocity.Y > 0 && !pressingAway;
		bool didWallJump = false;

		// Стрибок
		if (Input.IsActionJustPressed("ui_accept"))
		{
			if (touchingWall && velocity.Y > 0)
			{
				int pushDir = -wallSide;
				velocity.Y = WallJumpVertical;
				velocity.X = pushDir * WallJumpHorizontal;

				_wallJumpLockTimer = WallJumpLockTime;
				_isWallSliding = false;
				_jumpCount = 1;
				_dashAvailableInAir = true;
				_animatedSprite.FlipH = pushDir < 0;
				_audioJump.PitchScale = (float)GD.RandRange(0.9, 1.1);
				_audioJump.Play();
				didWallJump = true;
			}
			else if (IsOnFloor() || _jumpCount < MaxJumps)
			{
				velocity.Y = JumpVelocity;
				_jumpCount++;
				_audioJump.PitchScale = (float)GD.RandRange(0.9, 1.1);
				_audioJump.Play();
			}
		}

		// --- ЛОГІКА РУХУ (SILKSONG STYLE) ---
		if (!didWallJump)
		{
			// За замовчуванням швидкість - звичайна ходьба
			float currentMoveSpeed = Speed;

			// Якщо кнопка Dash затиснута, ми біжимо швидко.
			if (Input.IsActionPressed("dash"))
			{
				currentMoveSpeed = RunSpeed;
			}

			if (direction != 0)
			{
				velocity.X = Mathf.MoveToward(velocity.X, direction * currentMoveSpeed, Acceleration * fDelta);
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * fDelta);
			}
		}

		if (canWallSlide)
		{
			_isWallSliding = true;
			velocity.Y = Mathf.Min(velocity.Y, WallSlideMaxFallSpeed);
			velocity.X = Mathf.MoveToward(velocity.X, 0, WallSlideStickFriction * fDelta);
		}

		if (Input.IsActionJustPressed("attack"))
		{
			if (!IsOnFloor() && Input.IsActionPressed("ui_down"))
				StartDownAttack();
			else
				StartAttack();
		}

		if (Input.IsActionJustPressed("dash"))
		{
			if (TryStartDash(direction, ref velocity))
			{
				_audioJump.Play();
				Velocity = velocity;
				MoveAndSlideWithContactDamage();
				return;
			}
		}

		// Аудіо ходьби
		if (IsOnFloor() && Mathf.Abs(velocity.X) > 10)
		{
			if (!_audioWalk.Playing) _audioWalk.Play();
		}
		else _audioWalk.Stop();

		UpdateAnimation(direction, velocity);
		Velocity = velocity;
		MoveAndSlideWithContactDamage();
	}

	public override void _Process(double delta)
	{
		if (_isBlinking)
		{
			_blinkTimer -= (float)delta;
			if (_blinkTimer <= 0)
			{
				_animatedSprite.Visible = !_animatedSprite.Visible;
				_blinkTimer = BlinkInterval;
			}
		}
	}

	// --- МЕТОДИ ЛОГІКИ ---

	private void MoveAndSlideWithContactDamage()
	{
		MoveAndSlide();
		TryApplyBossContactDamageFromSlide();
	}

	private void TryApplyBossContactDamageFromSlide()
	{
		if (_isDead || _hurtbox == null) return;
		bool canTakeHit = !(_hurtbox.EnableInvincibility && _hurtbox.IsInvincible());

		int slideCount = GetSlideCollisionCount();
		for (int i = 0; i < slideCount; i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);
			if (collision == null) continue;
			if (collision.GetCollider() is not BossController boss) continue;
			if (boss.IsDead || !boss.DealsContactDamage) continue;

			if (collision.GetNormal().Y < -0.7f)
				StartEnemyTopDrop();

			int damage = Mathf.Max(0, boss.ContactDamage);
			if (!canTakeHit || damage <= 0) continue;

			_hurtbox.TakeHit(damage, boss.GlobalPosition);
			return;
		}
	}

	private void StartEnemyTopDrop()
	{
		_enemyTopDropTimer = Mathf.Max(_enemyTopDropTimer, EnemyTopDropDuration);
		SetCollisionMaskValue(9, false);
		Velocity = new Vector2(Velocity.X, Mathf.Max(Velocity.Y, EnemyTopDropPush));
	}

	public void TakeHitLogic(int damage, Vector2 sourcePos)
	{
		if (_isDead) return;

		_audioHurt.Play();
		_stunTimer = StunDuration;
		_isStunned = true;
		ResetAttackState();
		_isDashing = false;

		float dir = Mathf.Sign(GlobalPosition.X - sourcePos.X);
		if (Mathf.IsZeroApprox(dir))
			dir = _animatedSprite.FlipH ? 1f : -1f;

		Velocity = new Vector2(dir * KnockbackForce, -KnockbackUpForce);
	}

	private void OnInvincibilityStarted()
	{
		_isBlinking = true;
		_blinkTimer = BlinkInterval;
		_animatedSprite.Visible = true;
	}

	private void OnInvincibilityEnded()
	{
		_isBlinking = false;
		_animatedSprite.Visible = true;
		_animatedSprite.Modulate = Colors.White;
	}

	private void OnDeath()
	{
		if (_isDead) return;
		_isDead = true;
		_isBlinking = false;
		_isStunned = false;
		_stunTimer = 0f;
		_animatedSprite.Visible = true;
		_animatedSprite.Modulate = Colors.White;
		ResetAttackState();
		_animatedSprite.Play("death");
		GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
	}

	private void StartAttack()
	{
		_isAttacking = true;
		_animatedSprite.Play("attack");
		_audioAttack.PitchScale = (float)GD.RandRange(0.9, 1.2);
		_audioAttack.Play();
		_swordHitbox.Monitoring = true;
	}

	private void StartDownAttack()
	{
		_isDownAttacking = true;
		_animatedSprite.Play("down");
		_audioAttack.PitchScale = (float)GD.RandRange(0.8, 1.0);
		_audioAttack.Play();
		_swordHitbox.Monitoring = false;
		_swordHitboxDown.Monitoring = true;
		Velocity = new Vector2(Velocity.X, 0);
	}

	private void EndDownAttack()
	{
		ResetAttackState();
		_animatedSprite.Play("idle");
	}

	private void OnDownAttackHit(Area2D area)
	{
		if (area is Hurtbox)
		{
			Velocity = new Vector2(Velocity.X, DownAttackBounce);
			EndDownAttack();
			_jumpCount = 1;
			_dashAvailableInAir = true;
		}
	}

	public void ToggleCutscene(bool active)
	{
		ResetAttackState();
		_isInCutscene = active;
		if (active)
		{
			Velocity = Vector2.Zero;
			_audioWalk.Stop();
		}
	}

	public void ForceWalk(float directionSign)
	{
		Velocity = new Vector2(directionSign * Speed, Velocity.Y);
		if (directionSign != 0) 
		{ 
			_animatedSprite.FlipH = directionSign < 0; 
			_animatedSprite.Play("run"); 
		}
	}

	private void OnAnimationFinished()
	{
		if (_animatedSprite.Animation == "attack")
		{
			ResetAttackState();
			_animatedSprite.Play("idle");
		}
		
		if (_animatedSprite.Animation == "wall_slide_start")
		{
			if (_isWallSliding)
			{
				_animatedSprite.Play("wall_slide_loop");
			}
		}
		
		if (_animatedSprite.Animation == "death")
		{
			if (GameManager.Instance != null)
			{
				GameManager.Instance.ResetGame();
				string respawnScene = GameManager.Instance.GetRespawnScenePath();
				GetTree().ChangeSceneToFile(respawnScene);
			}
			else
			{
				GetTree().ChangeSceneToFile(gameStartPath);
			}
		}
	}

	// --- ОНОВЛЕНИЙ МЕТОД UPDATE ANIMATION (З ПАДІННЯМ) ---
	private void UpdateAnimation(float direction, Vector2 velocity)
	{
		if (_isAttacking || _isDownAttacking || _isDashing || _isDead || _isStunned) return;

		if (_isWallSliding)
		{
			if (_animatedSprite.Animation == "wall_slide_loop") return;
			if (_animatedSprite.Animation == "wall_slide_start") return;
			_animatedSprite.Play("wall_slide_start");
			return;
		}
		
		if (direction > 0) 
		{ 
			_animatedSprite.FlipH = false; 
			_swordHitbox.Scale = new Vector2(1, 1); 
			_swordHitboxDown.Scale = new Vector2(1, 1); 
		}
		else if (direction < 0) 
		{ 
			_animatedSprite.FlipH = true; 
			_swordHitbox.Scale = new Vector2(-1, 1); 
			_swordHitboxDown.Scale = new Vector2(-1, 1); 
		}

		if (IsOnFloor()) 
		{ 
			if (Mathf.IsZeroApprox(velocity.X)) 
			{
				_animatedSprite.Play("idle"); 
			}
			else 
			{
				// Логіка перемикання бігу/ходьби
				if (Mathf.Abs(velocity.X) > Speed + 10.0f)
				{
					_animatedSprite.Play("run"); 
				}
				else
				{
					_animatedSprite.Play("walk"); 
				}
			}
		}
		else 
		{ 
			// ЛОГІКА СТРИБКА І ПАДІННЯ
			if (velocity.Y < 0) 
			{
				_animatedSprite.Play("jump"); // Рух вгору
			}
			else if (velocity.Y > 0)
			{
				_animatedSprite.Play("fall"); // Рух вниз
			}
		}
	}

	private void ResetAttackState()
	{
		_isAttacking = false;
		_isDownAttacking = false;
		_swordHitbox.Monitoring = false;
		_swordHitboxDown.Monitoring = false;
	}

	private bool TryStartDash(float inputDir, ref Vector2 velocity)
	{
		if (_isDead) return false;
		if (_dashCooldownTimer > 0f) return false;
		if (_isAttacking || _isDownAttacking) return false;
		if (_stunTimer > 0f) return false;

		if (!IsOnFloor() && !_dashAvailableInAir) return false;

		float dir;
		if (!Mathf.IsZeroApprox(inputDir)) dir = Mathf.Sign(inputDir);
		else dir = _animatedSprite.FlipH ? -1f : 1f;

		if (_isWallSliding)
		{
			int wallSide = GetWallSide();
			if (wallSide != 0 && Mathf.Sign(dir) == wallSide)
				return false;
		}

		_isDashing = true;
		_dashTimer = DashDuration;

		if (!IsOnFloor())
			_dashAvailableInAir = false;

		velocity.X = dir * DashSpeed;
		velocity.Y = 0;

		return true;
	}

	private bool IsFacingWall()
	{
		RayCast2D ray = _animatedSprite.FlipH ? _wallRayLeft : _wallRayRight;
		ray.ForceRaycastUpdate();
		return ray.IsColliding();
	}	

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.IsPressed() && !keyEvent.IsEcho())
		{
			if (keyEvent.Keycode == Key.Escape && PauseMenu != null)
			{
				PauseMenu.TogglePause(!PauseMenu.Visible);
				if (PauseMenu.Visible)
					_audioWalk.Stop();
			}
		}
	}
	
	private void TogglePauseMenu()
	{
		if (PauseMenu == null) return;
		PauseMenu.Visible = !PauseMenu.Visible;
		GetTree().Paused = PauseMenu.Visible;

		if (PauseMenu.Visible)
		{
			_audioWalk.Stop();
		}
	}

	private int GetWallSide()
	{
		_wallRayLeft.ForceRaycastUpdate();
		_wallRayRight.ForceRaycastUpdate();

		if (_wallRayLeft.IsColliding()) return -1;
		if (_wallRayRight.IsColliding()) return 1;

		return 0;
	}
}
