using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A key is not enough to be consulted: a voice the user switched off is neither called nor
/// shown. The model list comes from the API — free and paid — and a ticked voice that disappeared
/// is named, never silently replaced.
/// </summary>
public class VoiceAndModelTests
{
    private sealed class FakeHost : IDatabaseHost
    {
        public ToneXDatabase? CurrentDatabase => null;

        public Task LoadDatabaseAsync(string path) => Task.CompletedTask;
    }

    private static string EmptyFolder()
        => Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "tonex-vm-" + Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>
    /// A settings screen that writes nothing real: a test must never touch the configuration of
    /// the person running it, nor the file it lives in.
    /// </summary>
    private static SettingsViewModel NewViewModel(
        IUserStateStore store,
        string folder,
        Func<string, Task<IReadOnlyList<string>>>? fetcher = null,
        AppConfig? config = null)
        => new(new FakeHost(), store, folder, fetcher, config ?? new AppConfig(), _ => { });

    [Fact]
    public void A_disabled_free_voice_is_not_consulted_even_with_its_key()
    {
        var config = new AppConfig
        {
            ApiKey = "oc_sk_test",
            Providers =
            {
                ["gemini"] = new ProviderCredential { ApiKey = "AIza-test" },
                ["groq"] = new ProviderCredential { ApiKey = "gsk-test" },
            },
        };

        var state = new UserState { DisabledVoices = new List<string> { "gemini" } };
        var labels = AiConsultation.BuildVoices(config, state)
            .Select(voice => voice.Label)
            .ToList();

        Assert.Equal(new[] { "Groq" }, labels.ToArray());
    }

    [Fact]
    public void NothingTickedLeavesTheArbitreAlone()
    {
        var config = new AppConfig { ApiKey = "oc_sk_test", Model = "longcat-2.5-preview-free" };

        Assert.Empty(AiConsultation.BuildVoices(config));
        Assert.NotNull(AiConsultation.BuildArbitre(config));
    }

    [Fact]
    public void SwitchingAFreeVoiceOffIsRemembered()
    {
        var folder = EmptyFolder();

        try
        {
            var store = new InMemoryUserStateStore();
            var viewModel = NewViewModel(store, folder);

            viewModel.GeminiActive = false;
            viewModel.MistralActive = false;

            var state = store.Load();
            Assert.Contains("gemini", state.DisabledVoices);
            Assert.Contains("mistral", state.DisabledVoices);
            Assert.DoesNotContain("groq", state.DisabledVoices);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_model_that_disappeared_falls_back_on_a_free_one()
    {
        var folder = EmptyFolder();

        try
        {
            var viewModel = NewViewModel(
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(
                    new[] { "glm-5.3-flash", "kimi-k3", "space-bunny-free" }));

            viewModel.ApiKey = "oc_sk_test";
            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            // LongCat absent : le premier gratuit, et on le dit.
            Assert.Equal("space-bunny-free", viewModel.SelectedModel!.Id);
            Assert.Contains("n'est plus disponible", viewModel.ModelStatus, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task LongCat_is_preferred_when_it_is_still_there()
    {
        var folder = EmptyFolder();

        try
        {
            var viewModel = NewViewModel(
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(
                    new[] { "glm-5.3-flash", "longcat-2.5-preview-free" }));

            viewModel.ApiKey = "oc_sk_test";
            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            Assert.Equal("longcat-2.5-preview-free", viewModel.SelectedModel!.Id);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Paid_models_are_offered_and_say_so()
    {
        var folder = EmptyFolder();

        try
        {
            var viewModel = NewViewModel(
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(new[] { "glm-5.3-flash", "space-bunny-free" }));

            viewModel.ApiKey = "oc_sk_test";
            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            Assert.Equal(2, viewModel.AvailableModels.Count);
            Assert.False(viewModel.AvailableModels[0].IsFree);
            Assert.Contains("payant", viewModel.AvailableModels[0].Label, StringComparison.Ordinal);
            Assert.Contains("gratuit", viewModel.AvailableModels[1].Label, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_ticked_voice_that_disappeared_is_named_and_never_replaced()
    {
        var folder = EmptyFolder();

        try
        {
            // L'utilisateur avait coché deux voix, dont une qui n'existe plus.
            var config = new AppConfig
            {
                ApiKey = "oc_sk_test",
                VoiceModels = { "kimi-k2.6", "space-bunny-free" },
            };

            var viewModel = NewViewModel(
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(
                    new[] { "longcat-2.5-preview-free", "space-bunny-free" }),
                config);

            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            Assert.Contains("kimi-k2.6", viewModel.VoiceModelsStatus, StringComparison.Ordinal);
            Assert.Contains("n'est plus disponible", viewModel.VoiceModelsStatus, StringComparison.Ordinal);

            // Rien n'a été coché à sa place : seule la voix encore présente reste active.
            var ticked = viewModel.VoiceChoices.Where(choice => choice.IsChecked).ToList();
            Assert.Single(ticked);
            Assert.Equal("space-bunny-free", ticked[0].Model.Id);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Ticking_a_voice_is_remembered()
    {
        var folder = EmptyFolder();

        try
        {
            var config = new AppConfig { ApiKey = "oc_sk_test" };
            var viewModel = NewViewModel(
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(
                    new[] { "longcat-2.5-preview-free", "space-bunny-free" }),
                config);

            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            viewModel.VoiceChoices.First(choice => choice.Model.Id == "space-bunny-free").IsChecked = true;

            Assert.Equal(new[] { "space-bunny-free" }, config.VoiceModels.ToArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
