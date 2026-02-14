
namespace SSHwitcher;

public class SshHostEntry
{
	public string Host { get; set; } = "";
	public string? HostName { get; set; }
	public string? User { get; set; }
	public string? IdentityFile { get; set; }
	public int HostLineIndex { get; set; } = -1;
	public int IdentityFileLineIndex { get; set; } = -1;

	public string DisplayName => Host == "*" ? "Default (*)" : Host;
}

public class SshConfigFile
{
	private static readonly string SshDir = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

	private static readonly string ConfigPath = Path.Combine(SshDir, "config");

	private List<string> _lines = [];
	private List<SshHostEntry> _entries = [];

	public IReadOnlyList<SshHostEntry> Entries => _entries;

	public bool Exists => File.Exists(ConfigPath);

	public void Load()
	{
		_entries.Clear();

		if (!File.Exists(ConfigPath))
		{
			_lines = [];
			return;
		}

		_lines = [.. File.ReadAllLines(ConfigPath)];

		SshHostEntry? current = null;

		for (int i = 0; i < _lines.Count; i++)
		{
			var line = _lines[i];
			var trimmed = line.Trim();

			if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
				continue;

			var (key, value) = SplitOption(trimmed);
			if (key == null) continue;

			if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
			{
				current = new SshHostEntry
				{
					Host = value ?? "",
					HostLineIndex = i
				};
				_entries.Add(current);
			}
			else if (current != null)
			{
				switch (key.ToLowerInvariant())
				{
					case "hostname":
						current.HostName = value;
						break;
					case "user":
						current.User = value;
						break;
					case "identityfile":
						current.IdentityFile = value;
						current.IdentityFileLineIndex = i;
						break;
				}
			}
		}
	}

	public void SetIdentityFile(SshHostEntry entry, string identityFile)
	{
		if (entry.IdentityFileLineIndex >= 0)
		{
			var existingLine = _lines[entry.IdentityFileLineIndex];
			var indent = existingLine[..^existingLine.TrimStart().Length];
			if (string.IsNullOrEmpty(indent)) indent = "    ";
			_lines[entry.IdentityFileLineIndex] = $"{indent}IdentityFile {identityFile}";
		}
		else
		{
			var insertAt = entry.HostLineIndex + 1;
			_lines.Insert(insertAt, $"    IdentityFile {identityFile}");
			foreach (var e in _entries)
			{
				if (e.HostLineIndex >= insertAt)
					e.HostLineIndex++;
				if (e.IdentityFileLineIndex >= insertAt)
					e.IdentityFileLineIndex++;
			}
			entry.IdentityFileLineIndex = insertAt;
		}

		entry.IdentityFile = identityFile;
	}

	public void Save()
	{
		if (File.Exists(ConfigPath))
		{
			var backupPath = ConfigPath + ".bak";
			File.Copy(ConfigPath, backupPath, overwrite: true);
		}

		File.WriteAllLines(ConfigPath, _lines);
	}

	public List<string> GetAvailableKeys()
	{
		if (!Directory.Exists(SshDir))
			return [];

		var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"config", "known_hosts", "known_hosts.old", "authorized_keys",
			"environment", "rc"
		};

		var keys = new List<string>();

		foreach (var file in Directory.GetFiles(SshDir))
		{
			var name = Path.GetFileName(file);

			if (name.StartsWith('.'))
				continue;
			if (name.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
				continue;
			if (skipNames.Contains(name))
				continue;

			try
			{
				using var reader = new StreamReader(file);
				var firstLine = reader.ReadLine();
				if (firstLine != null && firstLine.Contains("PRIVATE KEY"))
				{
					keys.Add($"~/.ssh/{name}");
				}
			}
			catch
			{}
		}

		keys.Sort(StringComparer.OrdinalIgnoreCase);
		return keys;
	}

	public static string NormalizeKeyPath(string path)
	{
		if (path.StartsWith("~/") || path.StartsWith("~\\"))
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				path[2..]);
		}
		return path;
	}

	public static string ToTildePath(string absolutePath)
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (absolutePath.StartsWith(home, StringComparison.OrdinalIgnoreCase))
		{
			return "~" + absolutePath[home.Length..].Replace('\\', '/');
		}
		return absolutePath;
	}

	private static (string? Key, string? Value) SplitOption(string line)
	{
		int sep = -1;
		for (int i = 0; i < line.Length; i++)
		{
			if (line[i] == '=' || char.IsWhiteSpace(line[i]))
			{
				sep = i;
				break;
			}
		}

		if (sep < 0) return (line, null);

		var key = line[..sep];
		var value = line[(sep + 1)..].Trim();
		if (sep < line.Length && line[sep] == '=')
			value = line[(sep + 1)..].Trim();

		return (key, value);
	}
}
