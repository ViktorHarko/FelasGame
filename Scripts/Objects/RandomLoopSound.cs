using Godot;
using System;

// 1. ПЕРЕВІРТЕ ЦЕЙ РЯДОК!
// Назва класу (RandomLoopSound) має бути такою ж, як назва файлу (RandomLoopSound.cs)
public partial class RandomLoopSound : AudioStreamPlayer
{
	[Export] public float MinWaitTime = 5.0f;
	[Export] public float MaxWaitTime = 15.0f;
	[Export] public bool PlayOnStart = false;

	public override void _Ready()
	{
		Finished += OnFinished;

		if (PlayOnStart) Play();
		else ScheduleNextPlay();
	}

	private void OnFinished()
	{
		ScheduleNextPlay();
	}

	private async void ScheduleNextPlay()
	{
		float waitTime = (float)GD.RandRange(MinWaitTime, MaxWaitTime);
		await ToSignal(GetTree().CreateTimer(waitTime), "timeout");

		if (!IsInstanceValid(this)) return;

		PitchScale = (float)GD.RandRange(0.9, 1.1);
		Play();
	}
}
