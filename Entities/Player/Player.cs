using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public PauseMenu PauseMenu;
	[ExportGroup("Components")]
	[Export] public HealthComponent HealthComp; // Посилання на компонент здоров'я
	
	[ExportGroup("Stats")]
	[Export] public float Speed = 250.0f;
	[Export] public float JumpVelocity = -450.0f;
	[Export] public float Acceleration = 1500.0f;
	[Export] public float Friction = 1200.0f;
	[Export] public int MaxJumps = 2;

	[ExportGroup("Combat")]
	[Export] public float KnockbackForce = 300.0f;
	[Export] public float StunDuration = 0.3f;

	[Export] public float DownAttackSpeed = 600.0f;
	[Export] public float DownAttackBounce = -350.0f;

	[ExportGroup("Invincibility Visual")]
	[Export] public float BlinkInterval = 0.1f; // Швидкість блимання

	// Шлях до початкової сцени гри
	[Export] private string gameStartPath = "res://Levels/Room1/Room1.tscn";

	public float Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	// Стан
	private bool _isAttacking = false;
	private bool _isDownAttacking = false;
	private bool _isDead = false;
	private bool _isInCutscene = false;
	private bool _isStunned = false;
	private int _jumpCount = 0;
	private float _stunTimer = 0.0f;

	// Невразливість та мерехтіння
	private bool _isBlinking = false;
	private float _blinkTimer = 0.0f;

	// Вузли
	private AnimatedSprite2D _animatedSprite;
	private Area2D _swordHitbox; // Це тепер Hitbox (Area2D)
	private Area2D _swordHitboxDown;
	private Hurtbox _hurtbox;

	// Аудіо
	private AudioStreamPlayer2D _audioWalk;
	private AudioStreamPlayer2D _audioJump;
	private AudioStreamPlayer2D _audioAttack;
	private AudioStreamPlayer2D _audioHurt;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_swordHitbox = GetNode<Area2D>("SwordArea"); // Переконайся, що на ньому висить скрипт Hitbox.cs
		_swordHitboxDown = GetNode<Area2D>("SwordAreaDown");
		_swordHitboxDown.AreaEntered += OnDownAttackHit;
		
		// Аудіо
		_audioWalk = GetNode<AudioStreamPlayer2D>("Audio_Walk");
		_audioJump = GetNode<AudioStreamPlayer2D>("Audio_Jump");
		_audioAttack = GetNode<AudioStreamPlayer2D>("Audio_Attack");
		_audioHurt = GetNode<AudioStreamPlayer2D>("Audio_Hurt");

		_animatedSprite.AnimationFinished += OnAnimationFinished;

		// --- ПІДПИСКА НА СИГНАЛИ КОМПОНЕНТІВ ---
		// Ми кажемо: "Коли HealthComponent кричить Died, запусти мій метод OnDeath"
		HealthComp.Died += OnDeath;

		// Отримуємо Hurtbox і підписуємось на сигнали невразливості
		_hurtbox = GetNode<Hurtbox>("Hurtbox");
		_hurtbox.HitReceived += TakeHitLogic;
		_hurtbox.InvincibilityStarted += OnInvincibilityStarted;
		_hurtbox.InvincibilityEnded += OnInvincibilityEnded;
		
		
	}


	public override void _PhysicsProcess(double delta)
	{
		if (_isDead) return;

		Vector2 velocity = Velocity;
		float fDelta = (float)delta;

		if (_swordHitbox != null) _swordHitbox.Monitoring = _isAttacking;
		if (_swordHitboxDown != null) _swordHitboxDown.Monitoring = _isDownAttacking;

		// 1. Катсцена
		if (_isInCutscene)
		{
			if (!IsOnFloor()) velocity.Y += Gravity * fDelta;
			UpdateAnimation(velocity.X, velocity);
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		// 2. Оглушення (Stun)
		if (_stunTimer > 0)
		{
			_stunTimer -= fDelta;
			velocity.Y += Gravity * fDelta;
			velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * fDelta);
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if ((_isAttacking && _animatedSprite.Animation != "attack") ||
			(_isDownAttacking && _animatedSprite.Animation != "down"))
		{
			ResetAttackState();
		}


		// 3. Атака
		if (_isAttacking)
		{
			if (!IsOnFloor()) velocity.Y += Gravity * fDelta;
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Friction * fDelta);
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (_isDownAttacking)
		{
			velocity.Y = DownAttackSpeed;
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Friction * 0.5f * fDelta);

			Velocity = velocity;
			MoveAndSlide();

			if (IsOnFloor() || Velocity.Y < DownAttackSpeed * 0.5f)
			{
				EndDownAttack();
			}

			return;
		}


		// 4. Рух
		if (!IsOnFloor()) velocity.Y += Gravity * fDelta;
		else _jumpCount = 0;

		if (Input.IsActionJustPressed("ui_accept") && (IsOnFloor() || _jumpCount < MaxJumps))
		{
			velocity.Y = JumpVelocity;
			_jumpCount++;
			_audioJump.PitchScale = (float)GD.RandRange(0.9, 1.1);
			_audioJump.Play();
		}

		float direction = Input.GetAxis("ui_left", "ui_right");
		if (direction != 0)
			 velocity.X = Mathf.MoveToward(Velocity.X, direction * Speed, Acceleration * fDelta);
		else
			 velocity.X = Mathf.MoveToward(Velocity.X, 0, Friction * fDelta);

		if (Input.IsActionJustPressed("attack"))
		{
			if (!IsOnFloor() && Input.IsActionPressed("ui_down"))
			{
				StartDownAttack();
			}
			else
			{
				StartAttack();
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
		MoveAndSlide();
	}

	public override void _Process(double delta)
	{
		if (_isBlinking)
		{
			_blinkTimer -= (float)delta;
			if (_blinkTimer <= 0)
			{
				// Перемикаємо між червоним напівпрозорим і нормальним
				if (_animatedSprite.Modulate.A < 1.0f)
				{
					_animatedSprite.Modulate = Colors.White; // Повністю видимий
				}
				else
				{
					_animatedSprite.Modulate = new Color(1, 0, 0, 0.5f); // Червоний напівпрозорий
				}
				_blinkTimer = BlinkInterval;
			}
		}
	}

	// Цей метод викликає Hurtbox, коли хтось нас вдарив
	// Зверни увагу: нам більше не треба рахувати HP тут!
	public void TakeHitLogic(int damage, Vector2 sourcePos)
	{
		if (_isDead) return;

		// Звук (тільки якщо урон реально пройшов)
		_audioHurt.Play();

		// Логіка відкидання
		_stunTimer = StunDuration;
		ResetAttackState();

		Vector2 knockbackDir = (GlobalPosition - sourcePos).Normalized();
		Velocity = new Vector2(knockbackDir.X * KnockbackForce, -200);
	}

	// Методи для обробки невразливості
	private void OnInvincibilityStarted()
	{
		_isBlinking = true;
		_blinkTimer = BlinkInterval;
	}

	private void OnInvincibilityEnded()
	{
		_isBlinking = false;
		_animatedSprite.Modulate = Colors.White; // Відновлюємо нормальний колір
	}

	// Цей метод викликається автоматично через Сигнал від HealthComponent
	private void OnDeath()
	{
		if (_isDead) return;

		_isDead = true;

		// Зупиняємо блимання і відновлюємо нормальний колір
		_isBlinking = false;
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
		// Area2D (Hitbox) сам зробить свою справу, коли торкнеться ворога
		_swordHitbox.Monitoring = true;
	}

	private void StartDownAttack()
	{
		_isDownAttacking = true;
		_animatedSprite.Play("down");
		_audioAttack.PitchScale = (float)GD.RandRange(0.8, 1.0);
		_audioAttack.Play();

		// Вимикаємо звичайний хітбокс, вмикаємо нижній
		_swordHitbox.Monitoring = false;
		_swordHitboxDown.Monitoring = true;

		// Скидаємо вертикальну швидкість для початку пікірування
		Velocity = new Vector2(Velocity.X, 0);
	}

	private void EndDownAttack()
	{
		ResetAttackState();
		_animatedSprite.Play("idle");
	}

	// Викликається при влучанні атаки вниз у щось
	private void OnDownAttackHit(Area2D area)
	{
		// Перевіряємо, що це Hurtbox (ворог)
		if (area is Hurtbox)
		{
			// Відскакуємо вгору!
			Velocity = new Vector2(Velocity.X, DownAttackBounce);

			// Завершуємо атаку вниз
			EndDownAttack();

			// Скидаємо лічильник стрибків, щоб можна було стрибати знову
			_jumpCount = 1;
		}
	}


	// --- Методи для катсцен ---
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
		if (directionSign != 0) { _animatedSprite.FlipH = directionSign < 0; _animatedSprite.Play("run"); }
	}

	// Анімації та OnAnimationFinished залишаються такими ж...
	private void OnAnimationFinished()
	{
		if (_animatedSprite.Animation == "attack")
		{
			ResetAttackState();
			_animatedSprite.Play("idle");
		}
		if (_animatedSprite.Animation == "death")
		{
			// Повний рестарт гри (як кнопка Restart)
			if (GameManager.Instance != null)
			{
				GameManager.Instance.ResetGame();
				// Завантажуємо збережену сцену (або стартову якщо немає збереження)
				string respawnScene = GameManager.Instance.GetRespawnScenePath();
				GetTree().ChangeSceneToFile(respawnScene);
			}
			else
			{
				GetTree().ChangeSceneToFile(gameStartPath);
			}
		}
	}

	private void UpdateAnimation(float direction, Vector2 velocity)
	{
		if (_isAttacking || _isDownAttacking || _isDead || _isStunned) return;
		
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

		if (IsOnFloor()) { if (Mathf.IsZeroApprox(velocity.X)) _animatedSprite.Play("idle"); else _animatedSprite.Play("run"); }
		else { if (velocity.Y < 0) _animatedSprite.Play("jump"); }
	}

	private void ResetAttackState()
	{
		_isAttacking = false;
		_isDownAttacking = false;
		_swordHitbox.Monitoring = false;
		_swordHitboxDown.Monitoring = false;
	}
	
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.IsPressed() && !keyEvent.IsEcho())
		{
			if (keyEvent.Keycode == Key.Escape && PauseMenu != null)
			{
				PauseMenu.TogglePause(!PauseMenu.Visible);

				// Зупиняємо звук ходьби під час паузи
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

		// При паузі зупиняємо аудіо ходьби
		if (PauseMenu.Visible)
		{
			_audioWalk.Stop();
		}
	}
}
