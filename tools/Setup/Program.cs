using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace TonexAdvisor.Setup;

/// <summary>
/// Installateur de Tonex Song Advisor, écrit dans le projet plutôt qu'avec un outil tiers.
/// </summary>
/// <remarks>
/// <para>
/// Pourquoi le nôtre : WiX est sous MS-RL (copyleft réciproque), NSIS et Inno Setup sont sous des
/// licences hors de la liste approuvée par le projet. Ce programme est du code du projet (MIT),
/// exécuté sur le runtime .NET (MIT) : aucune licence à couvrir.
/// </para>
/// <para>
/// Installation par utilisateur, sans droits administrateur : l'application va dans
/// <c>%LOCALAPPDATA%\Programs\TonexSongAdvisor</c>, avec des raccourcis, une entrée « Ajout ou
/// suppression de programmes » et un désinstalleur. La configuration utilisateur
/// (<c>%APPDATA%\TonexAdvisor</c>) n'est jamais touchée.
/// </para>
/// </remarks>
internal static class Program
{
    private const string AppName = "Tonex Song Advisor";
    private const string ExeName = "TonexAdvisor.App.exe";
    private const string UninstallId = "TonexSongAdvisor";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TonexSongAdvisor";

    private static int Main(string[] args)
    {
        // Copié en `uninstall.exe`, le programme se comporte comme désinstalleur même lancé
        // sans argument : un double-clic ne peut pas réinstaller par erreur.
        var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
        var uninstall = Has(args, "--uninstall")
                        || string.Equals(exeName, "uninstall", StringComparison.OrdinalIgnoreCase);
        var silent = Has(args, "--silent");

        try
        {
            if (uninstall)
                Uninstall(silent);
            else
                Install(silent);

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Échec : {exception.Message}");
            return 1;
        }
    }

    private static void Install(bool silent)
    {
        var installDir = InstallDir();
        Console.WriteLine($"Installation de {AppName} dans {installDir}…");

        if (Directory.Exists(installDir))
            Directory.Delete(installDir, recursive: true);

        Directory.CreateDirectory(installDir);
        ExtractPayload(installDir);

        var exe = Path.Combine(installDir, ExeName);
        if (!File.Exists(exe))
            throw new InvalidOperationException($"{ExeName} est absent de la charge utile.");

        var uninstaller = Path.Combine(installDir, "uninstall.exe");
        File.Copy(Environment.ProcessPath ?? throw new InvalidOperationException("Chemin de l'installateur inconnu."), uninstaller, overwrite: true);

        CreateShortcut(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), $"{AppName}.lnk"),
            exe,
            installDir,
            "Conseille le meilleur preset TONEX pour une chanson.");

        CreateShortcut(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{AppName}.lnk"),
            exe,
            installDir,
            "Conseille le meilleur preset TONEX pour une chanson.");

        RegisterUninstall(installDir, uninstaller);

        Console.WriteLine("Installation terminée.");
        if (!silent)
            Console.WriteLine("Raccourcis créés : menu Démarrer et bureau.");
    }

    private static void Uninstall(bool silent)
    {
        var installDir = InstallDir();
        Console.WriteLine($"Désinstallation de {AppName}…");

        DeleteIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), $"{AppName}.lnk"));
        DeleteIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{AppName}.lnk"));

        using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true))
        {
            if (key is not null)
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        }

        if (Directory.Exists(installDir))
        {
            try
            {
                Directory.Delete(installDir, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Le désinstalleur tourne depuis ce dossier : on le fait supprimer juste après.
                Process.Start(new ProcessStartInfo(
                    "cmd.exe",
                    $"/c ping -n 3 127.0.0.1 > nul & rd /s /q \"{installDir}\"")
                {
                    CreateNoWindow = true,
                });
            }
        }

        Console.WriteLine("Désinstallation terminée. Vos réglages (%APPDATA%\\TonexAdvisor) sont conservés.");
        if (!silent)
            Console.WriteLine("Appuyez sur une touche pour fermer.");
        if (!silent)
            Console.ReadKey(intercept: true);
    }

    /// <summary>L'application installée, par utilisateur : aucun privilège requis.</summary>
    private static string InstallDir()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "TonexSongAdvisor");

    /// <summary>Décompresse la charge utile incorporée à cet exécutable.</summary>
    private static void ExtractPayload(string installDir)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException(
                "Charge utile absente : construisez l'installateur avec tools\\build-release.ps1.");

        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        archive.ExtractToDirectory(installDir);
        Console.WriteLine($"{archive.Entries.Count} fichiers extraits.");
    }

    /// <summary>Raccourci .lnk, créé par le shell Windows (COM WScript.Shell).</summary>
    private static void CreateShortcut(string linkPath, string target, string workingDirectory, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell indisponible.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("WScript.Shell indisponible.");

        dynamic shortcut = shell.CreateShortcut(linkPath);
        shortcut.TargetPath = target;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = description;
        shortcut.Save();
    }

    private static void RegisterUninstall(string installDir, string uninstaller)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey)
            ?? throw new InvalidOperationException("Impossible d'écrire la clé de désinstallation.");

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", VersionLabel());
        key.SetValue("Publisher", "tonex-song-advisor");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", Path.Combine(installDir, ExeName));
        key.SetValue("UninstallString", $"\"{uninstaller}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static string VersionLabel()
    {
        // La version des assemblys n'est pas fiable en publication single-file : on lit celle
        // des informations du fichier, qui est celle annoncée à la release.
        if (Environment.ProcessPath is { } path)
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var product = info.ProductVersion;
            if (!string.IsNullOrWhiteSpace(product))
                return product.Split('+')[0].Trim();

            if (!string.IsNullOrWhiteSpace(info.FileVersion))
                return info.FileVersion;
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static bool Has(string[] args, string flag)
        => args.Any(argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
}
