using TonexAdvisor.App.Config;

namespace TonexAdvisor.Core.Tests;

public class AppConfigTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "tonex-advisor-tests", Guid.NewGuid().ToString("N"), "config.json");

    [Fact]
    public void Save_then_load_round_trips_the_key_and_model()
    {
        var file = TempFile();

        try
        {
            new AppConfig
            {
                ApiKey = "oc_sk_test",
                Model = "space-bunny-free",
            }.Save(file);

            var loaded = AppConfig.Load(file);

            Assert.Equal("oc_sk_test", loaded.ApiKey);
            Assert.Equal("space-bunny-free", loaded.Model);
            Assert.True(loaded.HasApiKey);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void Missing_file_yields_an_empty_configuration()
    {
        var loaded = AppConfig.Load(TempFile());

        Assert.Equal("", loaded.ApiKey);
        Assert.False(loaded.HasApiKey);
    }

    [Fact]
    public void Garbage_file_yields_an_empty_configuration_instead_of_throwing()
    {
        var file = TempFile();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{ not json !!");

            var loaded = AppConfig.Load(file);

            Assert.False(loaded.HasApiKey);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void A_paid_model_written_by_hand_is_replaced_by_a_free_one()
    {
        var file = TempFile();

        try
        {
            new AppConfig { ApiKey = "oc_sk_test", Model = "glm-5.3-flash" }.Save(file);

            var loaded = AppConfig.Load(file);

            // La liste des modèles vient de l'API : un modèle payant est un choix légitime et
            // n'est plus réécrit en silence.
            Assert.Equal("glm-5.3-flash", loaded.Model);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void Free_models_are_told_apart_by_their_identifier()
    {
        Assert.True(OpenCodeModels.IsFree("longcat-2.5-preview-free"));
        Assert.True(OpenCodeModels.IsFree("space-bunny-free"));
        Assert.False(OpenCodeModels.IsFree("glm-5.3-flash"));

        Assert.All(OpenCodeModels.Defaults, model => Assert.True(model.IsFree));
        Assert.All(OpenCodeModels.Defaults, model => Assert.False(string.IsNullOrWhiteSpace(model.Label)));
    }

    [Fact]
    public void The_fallback_prefers_longcat_then_any_free_model()
    {
        Assert.Equal(OpenCodeModels.PreferredId, OpenCodeModels.Fallback(OpenCodeModels.Defaults).Id);

        // LongCat parti : le premier gratuit.
        var withoutLongCat = new[]
        {
            OpenCodeModels.FromId("kimi-k3"),
            OpenCodeModels.FromId("space-bunny-free"),
        };
        Assert.Equal("space-bunny-free", OpenCodeModels.Fallback(withoutLongCat).Id);

        // Aucun gratuit : on prend ce qu'il y a plutôt que de rester sans modèle.
        var paidOnly = new[] { OpenCodeModels.FromId("kimi-k3") };
        Assert.Equal("kimi-k3", OpenCodeModels.Fallback(paidOnly).Id);
    }

    [Fact]
    public void Resolve_normalises_an_identifier_without_forcing_the_free_tier()
    {
        Assert.Equal(OpenCodeModels.PreferredId, OpenCodeModels.Resolve(null).Id);
        Assert.Equal("space-bunny-free", OpenCodeModels.Resolve("Space-BUNNY-free").Id);
        Assert.Equal("kimi-k3", OpenCodeModels.Resolve("kimi-k3").Id);
    }

    [Fact]
    public void Default_path_sits_outside_any_repository()
    {
        // Le fichier contient un secret : il doit vivre dans %APPDATA%, jamais dans le dépôt.
        Assert.Contains("TonexAdvisor", AppConfig.DefaultPath);
        Assert.EndsWith("config.json", AppConfig.DefaultPath);
        Assert.DoesNotContain(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, AppConfig.DefaultPath);
    }
}
