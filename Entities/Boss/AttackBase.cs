using Godot;

public enum AttackDistanceBand
{
	LegacyGlobal = 0,
	Near = 1,
	Mid = 2,
	Far = 3
}

public partial class AttackBase : Node2D
{
	[Signal] public delegate void FinishedEventHandler();

	[Export] public bool Enabled = true;
	[Export] public float Weight = 1f;
	[Export] public double CooldownSeconds = 1.0;
	[Export] public bool CanTriggerWhileChasing = false;
	[Export] public bool ChaseOnly = false;

	[ExportGroup("Distance Gate")]
	[Export] public AttackDistanceBand DistanceBand = AttackDistanceBand.LegacyGlobal;

	private double _cooldownUntilSec = 0;

	public bool IsAvailable
	{
		get
		{
			double now = Time.GetTicksMsec() / 1000.0;
			return Enabled && now >= _cooldownUntilSec;
		}
	}

	protected void StartCooldown()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		_cooldownUntilSec = now + CooldownSeconds;
	}

	public virtual bool MatchesDistanceGate(BossController boss, AttackDistanceBand currentBand, float absDx, float absDy)
	{
		if (boss == null || !IsInstanceValid(boss))
			return false;

		if (DistanceBand == AttackDistanceBand.LegacyGlobal)
		{
			return absDx <= Mathf.Max(0f, boss.AttackRangeX) &&
				absDy <= Mathf.Max(0f, boss.AttackRangeY);
		}

		return DistanceBand == currentBand;
	}

	public virtual void Execute(BossController boss)
	{
		StartCooldown();
		Finish();
	}

	protected void Finish()
	{
		EmitSignal(SignalName.Finished);
	}
}
