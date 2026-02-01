using Godot;
using System.Collections.Generic; // Потрібно для HashSet

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// Шлях до файлу збереження
	private const string SAVE_PATH = "user://savegame.cfg";
	private const string DEFAULT_START_SCENE = "res://Levels/Room1/Room1.tscn";

	// Тут ми зберігаємо унікальні ID всіх убитих ворогів
	// HashSet працює швидше за List для пошуку
	public HashSet<string> DeadEnemyIDs = new HashSet<string>();

	// Збереження здоров'я гравця між сценами
	public int? SavedPlayerHealth { get; private set; } = null;
	public int SavedPlayerMaxHealth { get; private set; } = 5;

	// Збережена сцена (checkpoint)
	public string SavedScenePath { get; private set; } = null;

	public void SavePlayerHealth(int currentHealth, int maxHealth)
	{
		SavedPlayerHealth = currentHealth;
		SavedPlayerMaxHealth = maxHealth;
		GD.Print($"[GAME MANAGER] Здоров'я збережено: {currentHealth}/{maxHealth}");
	}

	public int GetSavedHealth(int defaultMax)
	{
		return SavedPlayerHealth ?? defaultMax;
	}

	public override void _Ready()
	{
		Instance = this;
		LoadGame(); // Завантажуємо збереження при старті
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

	// Скидання стану гри (для нової гри)
	public void ResetGame()
	{
		DeadEnemyIDs.Clear();
		SavedPlayerHealth = null; // Скидаємо здоров'я до максимуму
		GD.Print("[GAME MANAGER] Стан гри скинуто!");
	}

	// --- СИСТЕМА ЗБЕРЕЖЕННЯ ---

	/// <summary>
	/// Зберігає гру у файл (поточна сцена + здоров'я)
	/// </summary>
	public void SaveGame(string currentScenePath)
	{
		var config = new ConfigFile();
		
		config.SetValue("game", "scene_path", currentScenePath);
		config.SetValue("game", "player_health", SavedPlayerHealth ?? SavedPlayerMaxHealth);
		config.SetValue("game", "player_max_health", SavedPlayerMaxHealth);
		
		var error = config.Save(SAVE_PATH);
		if (error == Error.Ok)
		{
			SavedScenePath = currentScenePath;
			GD.Print($"[GAME MANAGER] Гру збережено! Сцена: {currentScenePath}");
		}
		else
		{
			GD.PrintErr($"[GAME MANAGER] Помилка збереження: {error}");
		}
	}

	/// <summary>
	/// Завантажує збереження з файлу
	/// </summary>
	public void LoadGame()
	{
		var config = new ConfigFile();
		var error = config.Load(SAVE_PATH);
		
		if (error == Error.Ok)
		{
			SavedScenePath = config.GetValue("game", "scene_path", DEFAULT_START_SCENE).AsString();
			SavedPlayerHealth = config.GetValue("game", "player_health", 5).AsInt32();
			SavedPlayerMaxHealth = config.GetValue("game", "player_max_health", 5).AsInt32();
			GD.Print($"[GAME MANAGER] Збереження завантажено! Сцена: {SavedScenePath}");
		}
		else
		{
			GD.Print("[GAME MANAGER] Файл збереження не знайдено, використовуємо значення за замовчуванням");
			SavedScenePath = null;
		}
	}

	/// <summary>
	/// Перевіряє чи існує збереження
	/// </summary>
	public bool HasSaveGame()
	{
		return SavedScenePath != null && FileAccess.FileExists(SAVE_PATH);
	}

	/// <summary>
	/// Повертає шлях до сцени для респауну (збережена або стандартна)
	/// </summary>
	public string GetRespawnScenePath()
	{
		return SavedScenePath ?? DEFAULT_START_SCENE;
	}

	/// <summary>
	/// Видаляє файл збереження (для нової гри)
	/// </summary>
	public void DeleteSaveGame()
	{
		if (FileAccess.FileExists(SAVE_PATH))
		{
			DirAccess.RemoveAbsolute(SAVE_PATH);
			GD.Print("[GAME MANAGER] Збереження видалено!");
		}
		SavedScenePath = null;
	}
}
