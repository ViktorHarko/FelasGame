using Godot;
using System;

public partial class AttackRush : AttackBase
{
	[Export] public string TelegraphAnim = "slash_telegraph";
	[Export] public double TelegraphTime = 0.22;
	[Export] public string RushAnim = "run";
	[Export] public double WindupTime = 0.08;
	[Export] public double RushTime = 0.55;
	[Export] public double RushTimeFromChase = 0.90;
	[Export] public float RushSpeed = 260f;
	[Export] public float RushSpeedFromChase = 360f;
	[Export] public double RecoveryTime = 0.20;
	[Export] public bool StopOnWall = true;
	[Export] public float StopDistanceToTarget = 12f;
	private bool _useChaseProfileNextExecution = false;

	public void TriggerFromChase()
	{
		_useChaseProfileNextExecution = true;
	}

	public override async void Execute(BossController boss)
	{
		StartCooldown();
		bool useChaseProfile = _useChaseProfileNextExecution;
		_useChaseProfileNextExecution = false;

		float rushSpeed = useChaseProfile ? RushSpeedFromChase : RushSpeed;
		double rushTime = useChaseProfile ? RushTimeFromChase : RushTime;

		if (boss == null || !IsInstanceValid(boss))
		{
			Finish();
			return;
		}

		try
		{
			if (boss.TryGetTargetX(out float targetX))
				boss.FaceTo(targetX, force: true);

			boss.PlayAnim(TelegraphAnim, restart: true, fallback: boss.IdleAnim);
			double teleDur = boss.GetAnimDuration(TelegraphAnim);
			await boss.WaitSeconds(Math.Max(TelegraphTime, teleDur));
			if (boss.IsDead) return;

			if (boss.TryGetTargetX(out float targetXBeforeRush))
				boss.FaceTo(targetXBeforeRush, force: true);

			boss.LockFacing(true);

			if (WindupTime > 0)
			{
				await boss.WaitSeconds(WindupTime);
				if (boss.IsDead) return;
			}

			int dir = boss.Facing;
			if (dir == 0) dir = 1;

			boss.PlayAnim(RushAnim, restart: true, fallback: boss.IdleAnim);
			boss.SetAttackMovement(dir * Mathf.Abs(rushSpeed));

			double endAt = Time.GetTicksMsec() / 1000.0 + Math.Max(0.0, rushTime);
			while (!boss.IsDead && IsInsideTree())
			{
				double now = Time.GetTicksMsec() / 1000.0;
				if (now >= endAt)
					break;

				if (StopOnWall && boss.IsOnWall())
					break;

				if (boss.TryGetTargetX(out float targetXNow))
				{
					float dx = targetXNow - boss.GlobalPosition.X;
					if (Mathf.Abs(dx) <= Mathf.Max(0f, StopDistanceToTarget))
						break;
				}

				await ToSignal(boss.GetTree(), SceneTree.SignalName.PhysicsFrame);
			}

			boss.ClearAttackMovement();
			if (boss.IsDead) return;

			if (RecoveryTime > 0)
			{
				boss.PlayAnim(boss.IdleAnim, restart: true);
				await boss.WaitSeconds(RecoveryTime);
			}
		}
		finally
		{
			boss.ClearAttackMovement();
			boss.LockFacing(false);
			boss.PlayAnim(boss.IdleAnim, restart: false);
			Finish();
		}
	}
}
