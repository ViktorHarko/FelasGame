using Godot;
using System.Collections.Generic; // Потрібно для HashSet

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// Тут ми зберігаємо унікальні ID всіх убитих ворогів
	// HashSet працює швидше за List для пошуку
	public HashSet<string> DeadEnemyIDs = new HashSet<string>();

	public override void _Ready()
	{
		Instance = this;
	}

	// Метод, щоб додати ворога в список мертвих
	public void RegisterDeath(string enemyID)
	{
		if (!DeadEnemyIDs.Contains(enemyID))
		{
			DeadEnemyIDs.Add(enemyID);
			GD.Print($"[GAME MANAGER] Ворог записаний у книгу мертвих: {enemyID}");
		}
	}

	// Перевірка, чи ворог мертвий
	public bool IsEnemyDead(string enemyID)
	{
		return DeadEnemyIDs.Contains(enemyID);
	}
}
