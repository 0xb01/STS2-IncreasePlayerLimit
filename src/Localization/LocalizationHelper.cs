using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using IncreasePlayerLimit.Configuration;

namespace IncreasePlayerLimit.Localization;

/// <summary>
/// Handles mod text localization lookups from packaged English Godot json resources.
/// </summary>
public static class LocalizationHelper
{
	private const string DefaultLanguageCode = "en_us";

	private static Dictionary<string, string>? _localizationTable;

	public static string GetText(string key, string fallbackText)
	{
		Dictionary<string, string> table = EnsureLocalizationTable();
		if (table.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		return fallbackText;
	}

	private static Dictionary<string, string> EnsureLocalizationTable()
	{
		if (_localizationTable != null)
		{
			return _localizationTable;
		}

		string filePath = $"res://{ModConfig.ModFolderName}/localization/{DefaultLanguageCode}.json";
		var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
			if (file != null)
			{
				var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(file.GetAsText());
				if (parsed != null)
				{
					table = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"Failed to load localization file: {filePath}. {ex.Message}");
		}

		_localizationTable = table;
		return table;
	}
}
