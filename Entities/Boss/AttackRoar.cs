using Godot;
using System;
using System.Threading.Tasks;

public partial class AttackRoar : AttackBase
{
	[Export] public PackedScene GroundFlameFxScene;
	[Export] public string TelegraphAnim = "slash_telegraph";
	[Export] public string AttackAnim = "angry";
	[Export] public double TelegraphTime = 0.28;
	[Export] public double RecoveryTime = 0.22;

	[ExportGroup("Flame Wave")]
	[Export] public int WaveCount = 7;
	[Export] public float StartForwardOffset = 18f;
	[Export] public float Spacing = 26f;
	[Export] public double SpawnDelay = 0.04;
	[Export] public float HorizontalJitter = 6f;
	[Export] public float GroundYOffset = -12f;
	[Export] public float GroundRayLength = 240f;

	[ExportGroup("Wall Spread")]
	[Export(PropertyHint.Layers2DPhysics)] public uint WallCollisionMask = 1;
	[Export] public float MaxWallSearchDistance = 1400f;
	[Export] public float WallInset = 8f;
	[Export] public float WallProbeHeight = 6f;

	public override async void Execute(BossController boss)
	{
		StartCooldown();

		if (boss == null || !IsInstanceValid(boss))
		{
			Finish();
			return;
		}

		try
		{
			if (boss.TryGetTargetX(out float targetXAtStart))
				boss.FaceTo(targetXAtStart, force: true);

			boss.PlayAnim(TelegraphAnim, restart: true, fallback: boss.IdleAnim);
			double teleDur = boss.GetAnimDuration(TelegraphAnim);
			await boss.WaitSeconds(Math.Max(TelegraphTime, teleDur));
			if (boss.IsDead) return;

			if (boss.TryGetTargetX(out float targetXBeforeHit))
				boss.FaceTo(targetXBeforeHit, force: true);
			boss.LockFacing(true);

			boss.PlayAnim(AttackAnim, restart: true, fallback: boss.IdleAnim);
			double attackDur = boss.GetAnimDuration(AttackAnim);
			if (attackDur > 0)
				await boss.WaitSeconds(attackDur);
			if (boss.IsDead) return;

			await SpawnGroundFlamesToWalls(boss);
			if (boss.IsDead) return;

			if (RecoveryTime > 0)
				await boss.WaitSeconds(RecoveryTime);
			if (boss.IsDead) return;
			boss.PlayAnim(boss.IdleAnim, restart: true);
		}
		finally
		{
			boss.LockFacing(false);
			Finish();
		}
	}

	private async Task SpawnGroundFlamesToWalls(BossController boss)
	{
		if (GroundFlameFxScene == null || boss == null || !IsInstanceValid(boss))
			return;

		Node parent = boss.GetTree()?.CurrentScene ?? boss.GetParent();
		if (parent == null) return;

		float originX = boss.GlobalPosition.X;
		float spacing = Mathf.Max(1f, Mathf.Abs(Spacing));
		float startOffset = Mathf.Max(0f, Mathf.Abs(StartForwardOffset));
		float fallbackReach = GetFallbackReach();

		float leftLimitX = originX - fallbackReach;
		if (TryFindWallX(boss, originX, -1, out float leftWallX))
			leftLimitX = leftWallX + Mathf.Abs(WallInset);

		float rightLimitX = originX + fallbackReach;
		if (TryFindWallX(boss, originX, 1, out float rightWallX))
			rightLimitX = rightWallX - Mathf.Abs(WallInset);

		int step = 0;
		const int maxSteps = 4096;
		while (step < maxSteps)
		{
			float dist = startOffset + step * spacing;
			bool spawnedAny = false;

			if (dist <= 0.001f)
			{
				if (originX >= leftLimitX && originX <= rightLimitX)
				{
					SpawnGroundFlame(parent, boss, originX, 1);
					spawnedAny = true;
				}
			}
			else
			{
				float leftX = originX - dist;
				if (leftX >= leftLimitX)
				{
					SpawnGroundFlame(parent, boss, leftX, -1);
					spawnedAny = true;
				}

				float rightX = originX + dist;
				if (rightX <= rightLimitX)
				{
					SpawnGroundFlame(parent, boss, rightX, 1);
					spawnedAny = true;
				}
			}

			if (!spawnedAny)
				break;

			step++;
			if (SpawnDelay > 0)
				await boss.WaitSeconds(SpawnDelay);
		}
	}

	private void SpawnGroundFlame(Node parent, BossController boss, float x, int dir)
	{
		if (parent == null || boss == null || !IsInstanceValid(boss))
			return;

		var fx = GroundFlameFxScene.Instantiate<Node2D>();
		parent.AddChild(fx);

		float jitter = HorizontalJitter > 0f
			? (float)GD.RandRange(-HorizontalJitter, HorizontalJitter)
			: 0f;
		float spawnX = x + jitter;
		float groundY = GetGroundYAtX(boss, spawnX);
		fx.GlobalPosition = new Vector2(spawnX, groundY + GroundYOffset);

		var anim = fx.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (anim != null)
			anim.FlipH = dir < 0;
	}

	private bool TryFindWallX(BossController boss, float originX, int dir, out float wallX)
	{
		wallX = originX;
		if (boss == null || !IsInstanceValid(boss))
			return false;

		var space = boss.GetWorld2D()?.DirectSpaceState;
		if (space == null)
			return false;

		float maxDistance = Mathf.Max(64f, Mathf.Abs(MaxWallSearchDistance));
		float probeY = GetGroundYAtX(boss, originX) - Mathf.Max(1f, Mathf.Abs(WallProbeHeight));

		Vector2 from = new Vector2(originX, probeY);
		Vector2 to = from + new Vector2(Mathf.Sign(dir) * maxDistance, 0f);
		var query = PhysicsRayQueryParameters2D.Create(from, to);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.CollisionMask = WallCollisionMask == 0 ? 1u : WallCollisionMask;
		query.Exclude = new Godot.Collections.Array<Rid> { boss.GetRid() };

		var hit = space.IntersectRay(query);
		if (hit.Count == 0 || !hit.ContainsKey("position"))
			return false;

		wallX = hit["position"].AsVector2().X;
		return true;
	}

	private float GetFallbackReach()
	{
		int count = Mathf.Max(1, WaveCount);
		float spacing = Mathf.Max(1f, Mathf.Abs(Spacing));
		float start = Mathf.Abs(StartForwardOffset);
		return start + (count - 1) * spacing;
	}

	private float GetGroundYAtX(BossController boss, float x)
	{
		if (boss == null || !IsInstanceValid(boss))
			return 0f;

		Vector2 from = new Vector2(x, boss.GlobalPosition.Y - 8f);
		Vector2 to = from + new Vector2(0, Mathf.Max(32f, GroundRayLength));
		var query = PhysicsRayQueryParameters2D.Create(from, to);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.CollisionMask = 1;
		query.Exclude = new Godot.Collections.Array<Rid> { boss.GetRid() };

		var space = boss.GetWorld2D()?.DirectSpaceState;
		if (space != null)
		{
			var hit = space.IntersectRay(query);
			if (hit.Count > 0 && hit.ContainsKey("position"))
			{
				return hit["position"].AsVector2().Y;
			}
		}

		var bodyShape = boss.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (bodyShape != null && !bodyShape.Disabled)
		{
			float y = bodyShape.GlobalPosition.Y;
			if (bodyShape.Shape is RectangleShape2D rect)
				y += rect.Size.Y * 0.5f;
			return y;
		}

		return boss.GlobalPosition.Y;
	}
}
