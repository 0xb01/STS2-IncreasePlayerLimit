using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;

namespace IncreasePlayerLimit.Configuration;

/// <summary>
/// Central configuration management for IncreasePlayerLimit.
/// Manages player limits, protocol bit allocations, and runtime difficulty settings.
/// </summary>
public static class ModConfig
{
	// --- Player Count Limits ---

	public const int DefaultPlayerLimit = 8;
	public const int MinPlayerLimit = 4;
	public const int MaxPlayerLimit = 16;
	public const int VanillaMultiplayerHolderCount = 4;

	// --- Upstream Vanilla Bit Widths ---

	public const int VanillaSlotIdBits = 2;
	public const int VanillaLobbyListLengthBits = 3;

	// --- Extended Protocol Bit Widths ---

	/// <summary>SlotId: 4 bits -> supports slot IDs 0-15 (up to 16 players).</summary>
	public const int SlotIdBits = 4;

	/// <summary>Lobby list length: 5 bits -> supports up to 31 players.</summary>
	public const int LobbyListLengthBits = 5;

	// --- File Paths ---

	public const string ModFolderName = "IncreasePlayerLimit";
	private const string ConfigFileName = "config.ini";
	private const string LegacyConfigFileName = "config.json";

	// --- Runtime Configuration Properties ---

	public static int TargetPlayerLimit { get; private set; } = DefaultPlayerLimit;
	public static bool DifficultyScalingEnabled { get; private set; } = true;
	public static bool MacOsTlsWorkaroundEnabled { get; set; } = true;

	public static string? ConfigFilePath { get; private set; }

	public static void SetTargetPlayerLimit(int value)
	{
		TargetPlayerLimit = Math.Clamp(value, MinPlayerLimit, MaxPlayerLimit);
	}

	public static void SetDifficultyScalingEnabled(bool enabled)
	{
		DifficultyScalingEnabled = enabled;
	}

	public static void SetMacOsTlsWorkaround(bool enabled)
	{
		MacOsTlsWorkaroundEnabled = enabled;
	}

	// --- Config File Management ---

	public static void LoadOrCreateConfig()
	{
		string modDirectory = ResolveModDirectory();
		Directory.CreateDirectory(modDirectory);
		ConfigFilePath = Path.Combine(modDirectory, ConfigFileName);

		string legacyPath = Path.Combine(modDirectory, LegacyConfigFileName);
		if (File.Exists(legacyPath) && !File.Exists(ConfigFilePath))
		{
			MigrateLegacyJsonConfig(legacyPath);
		}

		if (File.Exists(ConfigFilePath))
		{
			try
			{
				ParseIniConfig(ConfigFilePath);
				return;
			}
			catch (Exception ex)
			{
				Log.Warn($"Failed to parse config at {ConfigFilePath}: {ex.Message}");
				BackupCorruptedConfig(ConfigFilePath);
			}
		}

		Save();
	}

	public static void Save()
	{
		if (string.IsNullOrEmpty(ConfigFilePath))
		{
			return;
		}

		try
		{
			using var writer = new StreamWriter(ConfigFilePath, false);
			writer.WriteLine("[macos]");
			writer.WriteLine($"tls_workaround={MacOsTlsWorkaroundEnabled.ToString().ToLowerInvariant()}");
			writer.WriteLine();
			writer.WriteLine("[multiplayer]");
			writer.WriteLine($"max_player_limit={TargetPlayerLimit}");
			writer.WriteLine($"difficulty_scaling={DifficultyScalingEnabled.ToString().ToLowerInvariant()}");
		}
		catch (Exception ex)
		{
			Log.Warn($"Failed to save config: {ex.Message}");
		}
	}

	private static void ParseIniConfig(string path)
	{
		string currentSection = "";
		foreach (string rawLine in File.ReadAllLines(path))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line[0] == ';' || line[0] == '#')
			{
				continue;
			}
			if (line[0] == '[' && line[^1] == ']')
			{
				currentSection = line[1..^1].Trim();
				continue;
			}
			int eq = line.IndexOf('=');
			if (eq < 0)
			{
				continue;
			}
			string key = line[..eq].Trim();
			string value = line[(eq + 1)..].Trim();

			switch (currentSection.ToLowerInvariant())
			{
				case "macos" when key.Equals("tls_workaround", StringComparison.OrdinalIgnoreCase):
					MacOsTlsWorkaroundEnabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
					break;
				case "multiplayer" when key.Equals("max_player_limit", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int rawLimit):
					SetTargetPlayerLimit(rawLimit);
					break;
				case "multiplayer" when key.Equals("difficulty_scaling", StringComparison.OrdinalIgnoreCase):
					SetDifficultyScalingEnabled(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
					break;
			}
		}
	}

	private static void MigrateLegacyJsonConfig(string jsonPath)
	{
		try
		{
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
			if (doc.RootElement.TryGetProperty("max_player_limit", out JsonElement limitEl) && limitEl.TryGetInt32(out int raw))
			{
				SetTargetPlayerLimit(raw);
			}
			if (doc.RootElement.TryGetProperty("macos_tls_workaround", out JsonElement tlsEl))
			{
				MacOsTlsWorkaroundEnabled = tlsEl.ValueKind == JsonValueKind.True;
			}
			Save();
			File.Delete(jsonPath);
			Log.Info("Migrated legacy config.json to config.ini");
		}
		catch (Exception ex)
		{
			Log.Warn($"Failed to migrate legacy config: {ex.Message}");
		}
	}

	private static string ResolveModDirectory()
	{
		string? assemblyLocation = Assembly.GetExecutingAssembly().Location;
		string? assemblyDirectory = string.IsNullOrWhiteSpace(assemblyLocation) ? null : Path.GetDirectoryName(assemblyLocation);
		if (!string.IsNullOrWhiteSpace(assemblyDirectory) && Directory.Exists(assemblyDirectory))
		{
			return assemblyDirectory;
		}

		string fallbackModDirectory = Path.Combine(AppContext.BaseDirectory, "mods", ModFolderName);
		if (Directory.Exists(fallbackModDirectory))
		{
			return fallbackModDirectory;
		}

		string appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		return Path.Combine(appDataRoot, "StS2Mods", ModFolderName);
	}

	private static void BackupCorruptedConfig(string configPath)
	{
		if (!File.Exists(configPath))
		{
			return;
		}

		string backupPath = $"{configPath}.bak";
		if (File.Exists(backupPath))
		{
			backupPath = $"{configPath}.{DateTime.Now:yyyyMMddHHmmss}.bak";
		}
		File.Move(configPath, backupPath);
	}
}
