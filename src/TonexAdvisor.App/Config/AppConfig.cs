using System.Text.Json;
using System.Text.Json.Serialization;

namespace TonexAdvisor.App.Config;

/// <summary>
/// Configuration locale de l'application : clé API OpenCode et modèle choisi.
/// </summary>
/// <remarks>
/// Ce fichier vit <b>hors du dépôt</b> : <c>%APPDATA%\TonexAdvisor\config.json</c>. Il contient un
/// secret et est donc exclu de Git (voir le <c>.gitignore</c>, qui l'exclut aussi par mesure de
/// sécurité si quelqu'un recrée le fichier à la racine du projet).
/// </remarks>
public sealed class AppConfig
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Clé API OpenCode Go. Jamais versionnée.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Identifiant du modèle utilisé, voir <see cref="OpenCodeModels.Free"/>.</summary>
    public string Model { get; set; } = OpenCodeModels.Default;

    /// <summary>Base URL de l'API, sans le suffixe <c>/chat/completions</c>.</summary>
    public string Endpoint { get; set; } = OpenCodeModels.EndPoint;

    /// <summary>True quand une clé a été enregistrée.</summary>
    [JsonIgnore]
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Emplacement par défaut du fichier, en dehors de tout dépôt Git.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TonexAdvisor",
        "config.json");

    /// <summary>
    /// Lit la configuration. Un fichier absent ou illisible rend une configuration vide plutôt que
    /// de faire échouer le démarrage.
    /// </summary>
    public static AppConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
                return new AppConfig();

            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(file), Options);
            if (config is null)
                return new AppConfig();

            // Ne jamais retenir un modèle payant écrit à la main dans le fichier.
            config.Model = OpenCodeModels.Resolve(config.Model).Id;
            return config;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppConfig();
        }
    }

    /// <summary>Écrit la configuration en place (création du dossier si nécessaire).</summary>
    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(file);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(file, JsonSerializer.Serialize(this, Options));
    }
}
