namespace TonexAdvisor.App.Services;

/// <summary>Where the browser keeps its preferences between two runs.</summary>
public interface IUserStateStore
{
    UserState Load();

    void Save(UserState state);
}

/// <summary>
/// The real store: one small JSON file next to the API configuration, outside the repository.
/// </summary>
public sealed class UserStateStore : IUserStateStore
{
    private readonly string? _path;

    public UserStateStore(string? path = null)
        => _path = path;

    public UserState Load() => UserState.Load(_path);

    public void Save(UserState state) => state.Save(_path);
}

/// <summary>
/// A store that reads and writes nothing: tests must not see the preferences of the person
/// running them, nor overwrite them.
/// </summary>
public sealed class InMemoryUserStateStore : IUserStateStore
{
    private UserState _state = new();

    public UserState Load() => _state;

    public void Save(UserState state) => _state = state;
}
