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

            Assert.Contains(loaded.Model, OpenCodeModels.Free.Select(model => model.Id));
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void Only_free_tier_models_are_proposed()
    {
        Assert.NotEmpty(OpenCodeModels.Free);
        Assert.All(OpenCodeModels.Free, model => Assert.EndsWith("-free", model.Id));
        Assert.All(OpenCodeModels.Free, model => Assert.False(string.IsNullOrWhiteSpace(model.Label)));
    }

    [Fact]
    public void Default_model_belongs_to_the_free_list()
    {
        Assert.Contains(OpenCodeModels.Default, OpenCodeModels.Free.Select(model => model.Id));
        Assert.Equal(OpenCodeModels.Free[0].Id, OpenCodeModels.Resolve(null).Id);
        Assert.Equal("space-bunny-free", OpenCodeModels.Resolve("Space-BUNNY-free").Id);
        Assert.Equal(OpenCodeModels.Free[0].Id, OpenCodeModels.Resolve("kimi-k3").Id);
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
