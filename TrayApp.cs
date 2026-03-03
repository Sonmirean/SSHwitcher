namespace SSHwitcher;

using System.Reflection;
using System.Xml.Linq;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly SshConfigFile _config = new();

    public TrayApplicationContext()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "SSHwitcher",
            Visible = true,
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowMenu();
        };

        RebuildMenu();
    }

    private void ShowMenu()
    {
        var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        mi?.Invoke(_trayIcon, null);
    }

    private void RebuildMenu()
    {
        _config.Load();

        var menu = new ContextMenuStrip();
        menu.Renderer = new ToolStripProfessionalRenderer(
            new SubtleColorTable());

        if (!_config.Exists)
        {
            var noConfig = new ToolStripMenuItem("No SSH config found")
            {
                Enabled = false
            };
            menu.Items.Add(noConfig);
            menu.Items.Add(new ToolStripSeparator());

            var createItem = new ToolStripMenuItem("Create ~/.ssh/config...");
            createItem.Click += OnCreateConfig;
            menu.Items.Add(createItem);
        }
        else if (_config.Entries.Count == 0)
        {
            var empty = new ToolStripMenuItem("No hosts defined in SSH config")
            {
                Enabled = false
            };
            menu.Items.Add(empty);
        }
        else
        {
            var availableKeys = _config.GetAvailableKeys();

            foreach (var entry in _config.Entries)
            {
                var hostItem = new ToolStripMenuItem(entry.DisplayName);

                // Show current identity info as tooltip
                var info = new List<string>();
                if (entry.HostName != null) info.Add($"Host: {entry.HostName}");
                if (entry.User != null) info.Add($"User: {entry.User}");
                if (entry.IdentityFile != null) info.Add($"Key: {entry.IdentityFile}");
                hostItem.ToolTipText = info.Count > 0
                    ? string.Join("\n", info)
                    : "No details configured";

                if (availableKeys.Count == 0)
                {
                    var noKeys = new ToolStripMenuItem("No private keys found in ~/.ssh/")
                    {
                        Enabled = false
                    };
                    hostItem.DropDownItems.Add(noKeys);
                }
                else
                {
                    foreach (var key in availableKeys)
                    {
                        var keyName = Path.GetFileName(
                            SshConfigFile.NormalizeKeyPath(key));
                        var keyItem = new ToolStripMenuItem(keyName);

                        // Check if this is the currently assigned key
                        bool isCurrent = entry.IdentityFile != null &&
                            NormalizePath(entry.IdentityFile) == NormalizePath(key);
                        keyItem.Checked = isCurrent;

                        var capturedEntry = entry;
                        var capturedKey = key;
                        keyItem.Click += (_, _) =>
                        {
                            _config.SetIdentityFile(capturedEntry, capturedKey);
                            _config.Save();
                            RebuildMenu();

                            _trayIcon.BalloonTipTitle = "SSHwitcher";
                            _trayIcon.BalloonTipText =
                                $"{capturedEntry.Host} → {Path.GetFileName(SshConfigFile.NormalizeKeyPath(capturedKey))}";
                            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                            _trayIcon.ShowBalloonTip(2000);
                        };

                        hostItem.DropDownItems.Add(keyItem);
                    }
                }

                menu.Items.Add(hostItem);
            }
        }

        menu.Items.Add(new ToolStripSeparator());

        var reloadItem = new ToolStripMenuItem("Reload config");
        reloadItem.Click += (_, _) => RebuildMenu();
        menu.Items.Add(reloadItem);

        var openConfigItem = new ToolStripMenuItem("Open config in editor");
        openConfigItem.Click += OnOpenConfig;
        menu.Items.Add(openConfigItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        };
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
    }

    private void OnOpenConfig(object? sender, EventArgs e)
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh", "config");

        if (File.Exists(configPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
        }
    }

    private void OnCreateConfig(object? sender, EventArgs e)
    {
        var sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var configPath = Path.Combine(sshDir, "config");

        if (!Directory.Exists(sshDir))
            Directory.CreateDirectory(sshDir);

        if (!File.Exists(configPath))
        {
            File.WriteAllText(configPath,
                """
                # SSH Config - managed by SSHwitcher
                # Add your hosts below, for example:
                #
                # Host github-personal
                #     HostName github.com
                #     User git
                #     IdentityFile ~/.ssh/id_ed25519_personal

                """);
        }

        RebuildMenu();

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = configPath,
            UseShellExecute = true
        });
    }

    private static string NormalizePath(string path)
    {
        return SshConfigFile.NormalizeKeyPath(path)
            .Replace('\\', '/')
            .TrimEnd('/')
            .ToLowerInvariant();
    }

    private static Icon CreateTrayIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("SSHwitcher.sshwitcher.ico");
        return new Icon(stream);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Subtle color scheme for the context menu.
/// </summary>
internal class SubtleColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(250, 250, 250);
    public override Color MenuItemSelected => Color.FromArgb(229, 241, 251);
    public override Color MenuItemBorder => Color.FromArgb(204, 232, 255);
}
