using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BossController : CharacterBody2D
{
	private const string HitFlashShaderCode = @"shader_type canvas_item;

uniform float flash_amount : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	vec4 base = tex * COLOR;
	base.rgb = mix(base.rgb, vec3(1.0), flash_amount);
	COLOR = base;
}";

	[ExportGroup("Components")]
	[Export] public HealthComponent HealthComp;
	[Export] public Hurtbox Hurtbox;

	[Export] public NodePath VisualPath = "Visual";
	[Export] public NodePath AttacksRootPath = "Attacks";
	[Export] public NodePath SpritePath = "AnimatedSprite2D";
	[Export] public float ThinkDelay = 0.6f;
	[Export] public bool AutoAttack = true;
	[Export] public string IdleAnim = "idle";
	[Export] public string ChaseAnim = "run";
	[Export] public float ChaseSpeed = 90f;
	[Export] public float StopDistance = 10f;

	[ExportGroup("Distance Bands")]
	[Export] public float NearMaxX = 80f;
	[Export] public float MidMaxX = 180f;
	[Export] public float FarMaxX = 320f;
	[Export] public float BandMaxY = 56f;

	[ExportGroup("Legacy Range Fallback")]
	[Export] public float AttackRangeX = 96f;
	[Export] public float AttackRangeY = 44f;

	[ExportGroup("Attack Behavior")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float RushChanceWhileChasing = 0.22f;
	[Export(PropertyHint.Range, "0.05,1.0,0.01")] public double RushRollIntervalWhileChasing = 0.28;
	[Export] public NodePath TargetPath;
	[Export] public bool ArtFacesRightByDefault = true;

	[ExportGroup("Contact Setup")]
	[Export] public bool BlocksPlayer = true;
	[Export] public bool DealsContactDamage = true;
	[Export] public int ContactDamage = 1;
	[Export] public NodePath BodyHitboxPath = "BodyHitbox";

	[ExportGroup("Hit Flash")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitFlashStrength = 1.0f;
	[Export(PropertyHint.Range, "0.01,0.3,0.01")] public float HitFlashDuration = 0.2f;

	private Node2D _target;

	private AnimatedSprite2D _sprite;
	private Node _attacksRoot;
	private Node2D _visual;
	private Vector2 _visualBaseScale = Vector2.One;
	private ShaderMaterial _hitFlashMaterial;
	private Tween _hitFlashTween;
	private Hitbox _bodyHitbox;
	private List<AttackBase> _attacks = new();

	private enum BossState { Idle, Chasing, Attacking }
	private BossState _state = BossState.Idle;
	private float _desiredVelocityX = 0f;
	private bool _attackMoveOverride = false;
	private float _attackMoveX = 0f;

	private int _brainToken = 0;
	private bool _isDead = false;
	private const float StopDeceleration = 1800f;
	private double _nextRushRollSec = 0.0;

	public int Facing { get; private set; } = 1;
	public bool IsDead => _isDead;
	public int AttackLocalForwardSign => ArtFacesRightByDefault ? 1 : -1;
	private bool _facingLocked = false;

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>(SpritePath);
		_visual = GetNodeOrNull<Node2D>(VisualPath);
		if (_visual != null)
		{
			_visualBaseScale = _visual.Scale;
			if (Mathf.IsZeroApprox(_visualBaseScale.X))
				_visualBaseScale = new Vector2(1f, _visualBaseScale.Y);
			if (Mathf.IsZeroApprox(_visualBaseScale.Y))
				_visualBaseScale = new Vector2(_visualBaseScale.X, 1f);
			ApplyVisualFacing();
		}

		_target = GetNodeOrNull<Node2D>(TargetPath);
		if (_target == null)
			GD.PrintErr("[BOSS] TargetPath не встановлений або ціль не знайдена (можна поки ігнорувати)");

		_bodyHitbox = GetNodeOrNull<Hitbox>(BodyHitboxPath);
		ApplyContactSetup();

		SetupHitFlashMaterial();

		if (_sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(IdleAnim))
			_sprite.Play(IdleAnim);
		else
			GD.PrintErr($"[BOSS] No sprite/frames/idle anim. IdleAnim='{IdleAnim}'");

		_attacksRoot = GetNode(AttacksRootPath);
		_attacks = _attacksRoot.GetChildren().OfType<AttackBase>().ToList();

		if (AutoAttack) StartBrain();

		if (HealthComp == null)
			HealthComp = GetNodeOrNull<HealthComponent>("HealthComponent");

		if (HealthComp != null)
		{
			HealthComp.HealthChanged += (cur, max) =>
				GD.Print($"[BOSS] HP {cur}/{max}");

			HealthComp.Died += OnBossDied;
		}
		else
		{
			GD.PrintErr("[BOSS] HealthComp is NULL (не прив’язав/не знайдено)");
		}

		if (Hurtbox == null)
			Hurtbox = GetNodeOrNull<Hurtbox>("Hurtbox");

		if (Hurtbox != null)
			Hurtbox.HitReceived += OnBossHitReceived;
	}

	private void StartBrain()
	{
		_brainToken++;
		int token = _brainToken;
		_ = BrainLoop(token);
	}

	private async Task BrainLoop(int token)
	{
		while (IsInsideTree() && AutoAttack && token == _brainToken && !_isDead)
		{
			if (_state == BossState.Attacking)
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				continue;
			}

			if (!TryGetTargetCombatPosition(out Vector2 targetCombatPos))
			{
				_state = BossState.Idle;
				_desiredVelocityX = 0f;
				_nextRushRollSec = 0.0;
				ClearAttackMovement();
				PlayAnim(IdleAnim, restart: false);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				continue;
			}

			Vector2 selfCombatPos = GetCombatPosition(this);
			float targetX = targetCombatPos.X;
			float dx = targetCombatPos.X - selfCombatPos.X;
			float dy = targetCombatPos.Y - selfCombatPos.Y;
			float absDx = Mathf.Abs(dx);
			float absDy = Mathf.Abs(dy);

			if (!TryResolveDistanceBand(absDx, absDy, out AttackDistanceBand currentBand))
			{
				_state = BossState.Chasing;
				FaceTo(targetX);
				_desiredVelocityX = absDx > Mathf.Max(0f, StopDistance)
					? Mathf.Sign(dx) * ChaseSpeed
					: 0f;
				PlayAnim(ChaseAnim, restart: false, fallback: IdleAnim);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				continue;
			}

			if (currentBand == AttackDistanceBand.Near)
			{
				_state = BossState.Idle;
				_desiredVelocityX = 0f;
				var attack = PickAttack(currentBand, absDx, absDy);
				if (attack != null)
				{
					FaceTo(targetX, force: true);
					_state = BossState.Attacking;
					attack.Execute(this);
					await ToSignal(attack, AttackBase.SignalName.Finished);
					if (_isDead) return;
					_state = BossState.Idle;
					_desiredVelocityX = 0f;
					ClearAttackMovement();
				}

				PlayAnim(IdleAnim, restart: false);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				continue;
			}

			_state = BossState.Chasing;
			FaceTo(targetX);
			_desiredVelocityX = absDx > Mathf.Max(0f, StopDistance)
				? Mathf.Sign(dx) * ChaseSpeed
				: 0f;
			PlayAnim(ChaseAnim, restart: false, fallback: IdleAnim);

			if (ShouldRollRushWhileChasing())
			{
				var chaseAttack = PickChaseAttack(currentBand, absDx, absDy);
				if (chaseAttack != null)
				{
					_state = BossState.Attacking;
					_desiredVelocityX = 0f;
					FaceTo(targetX, force: true);
					if (chaseAttack is AttackRush chaseRush)
						chaseRush.TriggerFromChase();
					chaseAttack.Execute(this);
					await ToSignal(chaseAttack, AttackBase.SignalName.Finished);
					if (_isDead) return;
					_state = BossState.Idle;
					_desiredVelocityX = 0f;
					ClearAttackMovement();
					PlayAnim(IdleAnim, restart: false);
				}
			}

			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			continue;
		}
	}

	private AttackBase PickAttack(AttackDistanceBand currentBand, float absDx, float absDy)
	{
		return PickAttack(chaseRoll: false, currentBand, absDx, absDy);
	}

	private AttackBase PickChaseAttack(AttackDistanceBand currentBand, float absDx, float absDy)
	{
		return PickAttack(chaseRoll: true, currentBand, absDx, absDy);
	}

	private AttackBase PickAttack(bool chaseRoll, AttackDistanceBand currentBand, float absDx, float absDy)
	{
		var available = _attacks
			.Where(a => a.IsAvailable)
			.Where(a => chaseRoll ? a.CanTriggerWhileChasing : !a.ChaseOnly)
			.Where(a => a.MatchesDistanceGate(this, currentBand, absDx, absDy))
			.ToList();
		if (available.Count == 0) return null;

		float total = available.Sum(a => a.Weight);
		float r = (float)GD.RandRange(0, total);

		float acc = 0;
		foreach (var a in available)
		{
			acc += a.Weight;
			if (r <= acc) return a;
		}
		return available[0];
	}

	private bool TryResolveDistanceBand(float absDx, float absDy, out AttackDistanceBand band)
	{
		band = AttackDistanceBand.LegacyGlobal;

		if (absDy > Mathf.Max(0f, BandMaxY))
			return false;

		float nearMax = Mathf.Max(0f, NearMaxX);
		float midMax = Mathf.Max(nearMax, MidMaxX);
		float farMax = Mathf.Max(midMax, FarMaxX);

		if (absDx <= nearMax)
		{
			band = AttackDistanceBand.Near;
			return true;
		}

		if (absDx <= midMax)
		{
			band = AttackDistanceBand.Mid;
			return true;
		}

		if (absDx <= farMax)
		{
			band = AttackDistanceBand.Far;
			return true;
		}

		return false;
	}

	private bool ShouldRollRushWhileChasing()
	{
		float chance = Mathf.Clamp(RushChanceWhileChasing, 0f, 1f);
		if (chance <= 0f) return false;

		double now = Time.GetTicksMsec() / 1000.0;
		if (now < _nextRushRollSec) return false;

		double interval = Mathf.Max(0.05f, (float)RushRollIntervalWhileChasing);
		_nextRushRollSec = now + interval;

		return (float)GD.RandRange(0.0, 1.0) <= chance;
	}

	public async Task WaitSeconds(double seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}

	public bool TryGetTargetX(out float targetX)
	{
		if (!TryGetTargetCombatPosition(out Vector2 targetPos))
		{
			targetX = 0f;
			return false;
		}

		targetX = targetPos.X;
		return true;
	}

	private bool TryGetTargetCombatPosition(out Vector2 position)
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			position = Vector2.Zero;
			return false;
		}

		position = GetCombatPosition(_target);
		return true;
	}

	private static Vector2 GetCombatPosition(Node2D node)
	{
		if (node == null || !IsInstanceValid(node))
			return Vector2.Zero;

		var bodyShape = node.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (bodyShape != null && !bodyShape.Disabled)
			return bodyShape.GlobalPosition;

		var hurtbox = node.GetNodeOrNull<Area2D>("Hurtbox");
		if (hurtbox != null)
		{
			var hurtboxShape = hurtbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			if (hurtboxShape != null && !hurtboxShape.Disabled)
				return hurtboxShape.GlobalPosition;
			return hurtbox.GlobalPosition;
		}

		return node.GlobalPosition;
	}

	public void FaceTo(float targetX, bool force = false)
	{
		if (_facingLocked && !force) return;

		Facing = targetX >= GlobalPosition.X ? 1 : -1;
		ApplyVisualFacing();
	}

	public void LockFacing(bool on) => _facingLocked = on;

	public void SetAttackMovement(float velocityX)
	{
		_attackMoveOverride = true;
		_attackMoveX = velocityX;
	}

	public void ClearAttackMovement()
	{
		_attackMoveOverride = false;
		_attackMoveX = 0f;
	}

	private void ApplyContactSetup()
	{
		SetCollisionLayerValue(9, BlocksPlayer);
		SetCollisionMaskValue(2, false);

		if (_bodyHitbox != null)
		{
			_bodyHitbox.Damage = Mathf.Max(0, ContactDamage);
			bool enabled = DealsContactDamage && !_isDead;
			_bodyHitbox.Monitoring = enabled;
			_bodyHitbox.Monitorable = enabled;
		}
	}

	private void SetupHitFlashMaterial()
	{
		if (_sprite == null) return;

		if (_sprite.Material is ShaderMaterial shaderMaterial &&
			shaderMaterial.Shader != null &&
			shaderMaterial.Shader.Code.Contains("flash_amount"))
		{
			_hitFlashMaterial = shaderMaterial;
			return;
		}

		var shader = new Shader();
		shader.Code = HitFlashShaderCode;

		_hitFlashMaterial = new ShaderMaterial();
		_hitFlashMaterial.Shader = shader;
		_hitFlashMaterial.SetShaderParameter("flash_amount", 0.0f);
		_sprite.Material = _hitFlashMaterial;
	}

	private void OnBossHitReceived(int damage, Vector2 sourcePosition)
	{
		PlayHitFlash();
	}

	private void PlayHitFlash()
	{
		if (_hitFlashMaterial == null || !IsInsideTree()) return;

		if (_hitFlashTween != null && _hitFlashTween.IsRunning())
			_hitFlashTween.Kill();

		float startAmount = Mathf.Clamp(HitFlashStrength, 0f, 1f);
		float duration = Mathf.Max(0.01f, HitFlashDuration);

		SetHitFlashAmount(startAmount);
		_hitFlashTween = CreateTween();
		_hitFlashTween.TweenMethod(Callable.From<float>(SetHitFlashAmount), startAmount, 0f, duration)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
	}

	private void SetHitFlashAmount(float amount)
	{
		if (_hitFlashMaterial == null) return;
		_hitFlashMaterial.SetShaderParameter("flash_amount", amount);
	}

	private void ApplyVisualFacing()
	{
		if (_visual == null) return;

		float baseX = Mathf.Abs(_visualBaseScale.X);
		if (Mathf.IsZeroApprox(baseX))
			baseX = 1f;

		int visualFacingSign = Facing * (ArtFacesRightByDefault ? 1 : -1);
		_visual.Scale = new Vector2(baseX * visualFacingSign, _visualBaseScale.Y);
	}

	public async Task Squash(double time = 0.08, float amount = 0.08f)
	{
		if (_sprite == null) return;

		var orig = _sprite.Scale;
		_sprite.Scale = new Vector2(orig.X * (1 + amount), orig.Y * (1 - amount));
		await WaitSeconds(time);
		_sprite.Scale = orig;
	}

	public void PlayAnim(string anim, bool restart = true, string fallback = null)
	{
		if (_sprite == null || _sprite.SpriteFrames == null) return;
		if (_isDead && anim != "death") return;

		string chosen = anim;

		if (!_sprite.SpriteFrames.HasAnimation(chosen))
		{
			if (!string.IsNullOrEmpty(fallback) && _sprite.SpriteFrames.HasAnimation(fallback))
				chosen = fallback;
			else if (_sprite.SpriteFrames.HasAnimation(IdleAnim))
				chosen = IdleAnim;
			else
				return;
		}

		if (restart)
			_sprite.Play(chosen);
		else if (_sprite.Animation != chosen)
			_sprite.Play(chosen);
	}

	public double GetAnimDuration(string anim)
	{
		if (_sprite == null || _sprite.SpriteFrames == null) return 0;
		if (!_sprite.SpriteFrames.HasAnimation(anim)) return 0;

		int frames = _sprite.SpriteFrames.GetFrameCount(anim);
		double fps = _sprite.SpriteFrames.GetAnimationSpeed(anim);
		double scale = _sprite.SpeedScale == 0 ? 1.0 : _sprite.SpeedScale;

		if (frames <= 0 || fps <= 0) return 0;
		return frames / (fps * scale);
	}

	private async Task PlayAnimAndWait(string anim, string fallback = null)
	{
		if (_sprite == null || _sprite.SpriteFrames == null) return;

		string chosen = anim;
		if (!_sprite.SpriteFrames.HasAnimation(chosen))
			chosen = (!string.IsNullOrEmpty(fallback) && _sprite.SpriteFrames.HasAnimation(fallback)) ? fallback : IdleAnim;

		_sprite.Play(chosen);

		if (_sprite.SpriteFrames.GetAnimationLoop(chosen))
			return;

		await ToSignal(_sprite, AnimatedSprite2D.SignalName.AnimationFinished);
	}

	public override void _PhysicsProcess(double delta)
	{
		float g = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");
		float dt = (float)delta;
		float x;
		if (_state == BossState.Chasing && !_isDead)
			x = _desiredVelocityX;
		else if (_state == BossState.Attacking && _attackMoveOverride && !_isDead)
			x = _attackMoveX;
		else
			x = Mathf.MoveToward(Velocity.X, 0f, StopDeceleration * dt);

		if (x != 0f && IsTouchingPlayerHurtbox())
		{
			x = 0f;
			if (_state == BossState.Attacking && _attackMoveOverride)
				ClearAttackMovement();
		}

		Velocity = new Vector2(x, Velocity.Y + g * dt);
		MoveAndSlide();
	}

	private bool IsTouchingPlayerHurtbox()
	{
		if (_bodyHitbox == null || !_bodyHitbox.Monitoring)
			return false;

		var overlaps = _bodyHitbox.GetOverlappingAreas();
		foreach (var area in overlaps)
		{
			if (area == null || !IsInstanceValid(area))
				continue;
			if (area is not Hurtbox hurtbox)
				continue;

			Node owner = hurtbox.GetParent();
			if (owner != null && owner.IsInGroup("Player"))
				return true;

			if (hurtbox.GetCollisionLayerValue(2))
				return true;
		}

		return false;
	}

	private async void OnBossDied()
	{
		if (_isDead) return;
		_isDead = true;

		GD.Print("[BOSS] DIED ✅");
		AutoAttack = false;
		_brainToken++;
		_state = BossState.Idle;
		_desiredVelocityX = 0f;
		ClearAttackMovement();
		SetHitFlashAmount(0f);
		if (_hitFlashTween != null && _hitFlashTween.IsRunning())
			_hitFlashTween.Kill();

		if (Hurtbox != null)
		{
			Hurtbox.Monitoring = false;
			Hurtbox.Monitorable = false;
		}

		foreach (var area in GetAreasRecursive(this))
		{
			area.Monitoring = false;
			area.Monitorable = false;
		}

		var bodyShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (bodyShape != null) bodyShape.Disabled = true;

		await PlayAnimAndWait("death", fallback: IdleAnim);
		QueueFree();
	}

	private static IEnumerable<Area2D> GetAreasRecursive(Node root)
	{
		if (root is Area2D area)
			yield return area;

		foreach (Node child in root.GetChildren())
		{
			foreach (var nested in GetAreasRecursive(child))
				yield return nested;
		}
	}
}
