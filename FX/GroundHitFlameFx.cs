using Godot;
using System;
using System.Threading.Tasks;

public partial class GroundHitFlameFx : Node2D
{
	[Export] public AnimatedSprite2D Anim;
	[Export] public string PreferredAnimation = "burst";
	[Export] public bool ForceNonLoop = true;
	[Export] public double MaxLifetime = 1.4;
	[Export] public bool FreeAtAnimEnd = true;

	[ExportGroup("Damage")]
	[Export] public Hitbox DamageHitbox;
	[Export] public bool EnableDamage = true;
	[Export] public int Damage = 1;
	[Export] public double DamageActiveTime = 0.20;

	private bool _queuedForFree = false;

	public override void _Ready()
	{
		if (Anim == null)
			Anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") ?? GetNodeOrNull<AnimatedSprite2D>("Anim");
		if (Anim == null)
		{
			QueueFree();
			return;
		}

		Anim.Centered = true;
		Anim.FrameChanged += OnFrameChanged;
		Anim.AnimationFinished += () => SafeQueueFree();

		string animName = ResolveAnimationName();
		if (ForceNonLoop && Anim.SpriteFrames != null && Anim.SpriteFrames.HasAnimation(animName))
			Anim.SpriteFrames.SetAnimationLoop(animName, false);
		Anim.Play(animName);
		OnFrameChanged();

		SetupDamageHitbox();

		if (FreeAtAnimEnd)
			_ = AutoFreeByAnimDuration(animName);

		if (MaxLifetime > 0)
		{
			_ = AutoFreeByLifetime(MaxLifetime);
		}
	}

	private void OnFrameChanged()
	{
		if (Anim == null || Anim.SpriteFrames == null) return;
		var tex = Anim.SpriteFrames.GetFrameTexture(Anim.Animation, Anim.Frame);
		if (tex == null) return;
		float h = tex.GetHeight();
		Anim.Offset = new Vector2(0, -h * 0.5f);
	}

	private string ResolveAnimationName()
	{
		if (Anim?.SpriteFrames == null)
			return string.Empty;

		if (!string.IsNullOrEmpty(PreferredAnimation) && Anim.SpriteFrames.HasAnimation(PreferredAnimation))
			return PreferredAnimation;
		if (Anim.SpriteFrames.HasAnimation("burst"))
			return "burst";
		if (Anim.SpriteFrames.HasAnimation("default"))
			return "default";
		return Anim.SpriteFrames.GetAnimationNames()[0];
	}

	private async Task AutoFreeByLifetime(double seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		SafeQueueFree();
	}

	private async Task AutoFreeByAnimDuration(string animName)
	{
		if (Anim?.SpriteFrames == null || string.IsNullOrEmpty(animName) || !Anim.SpriteFrames.HasAnimation(animName))
			return;

		int frames = Anim.SpriteFrames.GetFrameCount(animName);
		double fps = Anim.SpriteFrames.GetAnimationSpeed(animName);
		double speedScale = Math.Abs(Anim.SpeedScale);
		if (speedScale <= 0.001) speedScale = 1.0;

		if (frames <= 0 || fps <= 0) return;

		double duration = frames / (fps * speedScale);
		await ToSignal(GetTree().CreateTimer(Math.Max(0.05, duration + 0.02)), SceneTreeTimer.SignalName.Timeout);
		SafeQueueFree();
	}

	private void SetupDamageHitbox()
	{
		if (DamageHitbox == null)
			DamageHitbox = GetNodeOrNull<Hitbox>("DamageHitbox");
		if (DamageHitbox == null)
			return;

		DamageHitbox.Damage = Mathf.Max(0, Damage);
		bool enabled = EnableDamage && DamageHitbox.Damage > 0;
		DamageHitbox.Monitoring = enabled;
		DamageHitbox.Monitorable = enabled;

		if (enabled && DamageActiveTime > 0)
			_ = DisableDamageAfter(DamageActiveTime);
	}

	private async Task DisableDamageAfter(double seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		if (DamageHitbox == null || !IsInstanceValid(DamageHitbox))
			return;
		DamageHitbox.Monitoring = false;
		DamageHitbox.Monitorable = false;
	}

	private void SafeQueueFree()
	{
		if (_queuedForFree) return;
		_queuedForFree = true;
		if (IsInsideTree())
			QueueFree();
	}
}
