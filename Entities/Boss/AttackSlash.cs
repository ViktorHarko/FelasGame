using Godot;
using System;
using System.Collections.Generic;

public partial class AttackSlash : AttackBase
{
	[Export] public NodePath HitboxPath = "Hitbox";
	[Export] public string TelegraphAnim = "slash_telegraph";
	[Export] public string AttackAnim = "slash";
	[Export] public double TelegraphTime = 0.25;
	[Export] public double ActiveTime = 0.12;
	[Export] public double EndPadding = 0.02;
	[Export] public double RecoveryTime = 0.25;
	[Export(PropertyHint.Range, "0,1,0.01")] public double HitMomentNormalized = 0.55;
	[Export] public Vector2 HitboxOffset = new Vector2(40, 0); // піджени під спрайт

	private Area2D _hitbox;
	private readonly HashSet<ulong> _hitThisSwing = new();

	public override void _Ready()
	{
		_hitbox = GetNodeOrNull<Area2D>(HitboxPath);
		if (_hitbox == null)
		{
			GD.PrintErr("[AttackSlash] Hitbox not found. Перевір HitboxPath.");
			return;
		}

		SetHitbox(false);
		_hitbox.AreaEntered += OnAreaEntered;
	}

	public override async void Execute(BossController boss)
	{
		StartCooldown();
		_hitThisSwing.Clear();

		try
		{
			if (boss.TryGetTargetX(out float targetXAtTelegraph))
				boss.FaceTo(targetXAtTelegraph, force: true);

			boss.PlayAnim(TelegraphAnim, restart: true, fallback: boss.IdleAnim);
			await boss.Squash(0.08, 0.10f);

			double teleDur = boss.GetAnimDuration(TelegraphAnim);
			await boss.WaitSeconds(Math.Max(TelegraphTime, teleDur));
			if (boss.IsDead) return;

			if (boss.TryGetTargetX(out float targetXBeforeSlash))
				boss.FaceTo(targetXBeforeSlash, force: true);
			boss.LockFacing(true);

			boss.PlayAnim(AttackAnim, restart: true, fallback: boss.IdleAnim);

			double slashDur = boss.GetAnimDuration(AttackAnim);
			if (slashDur <= 0) slashDur = ActiveTime + 0.1;

			double hitCenterTime = Mathf.Clamp((float)HitMomentNormalized, 0f, 1f) * slashDur;
			double startDelay = Math.Max(0, hitCenterTime - ActiveTime * 0.5);
			double latestStart = Math.Max(0, slashDur - ActiveTime - EndPadding);
			startDelay = Math.Min(startDelay, latestStart);
			await boss.WaitSeconds(startDelay);
			if (boss.IsDead) return;

			PlaceHitbox(boss);
			SetHitbox(true);
			await boss.WaitSeconds(ActiveTime);
			SetHitbox(false);

			double rest = Math.Max(0, slashDur - startDelay - ActiveTime);
			await boss.WaitSeconds(rest);
			if (boss.IsDead) return;


			await boss.WaitSeconds(RecoveryTime);
			if (boss.IsDead) return;
			boss.PlayAnim(boss.IdleAnim, restart: true);
		}
		finally
		{
			boss.LockFacing(false);
			SetHitbox(false);
			Finish();
		}
	}

	private void SetHitbox(bool on)
	{
		GD.Print($"[SLASH] hitbox {(on ? "ON" : "OFF")} | gp={_hitbox?.GlobalPosition}");
		if (_hitbox == null) return;
		_hitbox.Monitoring = on;
		_hitbox.Monitorable = on;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area == null) return;
		if (!_hitThisSwing.Add(area.GetInstanceId())) return;

		GD.Print($"[SLASH] HIT -> {area.Name}");
	}

	private void PlaceHitbox(BossController boss)
	{
		if (_hitbox == null || boss == null) return;
		float x = Mathf.Abs((float)HitboxOffset.X) * boss.AttackLocalForwardSign;
		_hitbox.Position = new Vector2(x, HitboxOffset.Y);
	}
}
