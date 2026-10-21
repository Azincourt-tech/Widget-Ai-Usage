using System.Net;
using System.Net.Http;
using AiUsageWidget.Models;
using AiUsageWidget.Services.Abstractions;

namespace AiUsageWidget.Tests.Fakes;

/// <summary>Relógio parado num instante conhecido, em UTC-3 (horário de Brasília).</summary>
internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly TimeZoneInfo Brasilia = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");

    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => Brasilia;
}

internal sealed class FakeUiTimer : IUiTimer
{
    public TimeSpan Interval { get; set; }

    public bool IsEnabled { get; private set; }

    public event EventHandler? Tick;

    public void Start() => IsEnabled = true;

    public void Stop() => IsEnabled = false;

    public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeThemeService : IThemeService
{
    public string? Applied { get; private set; }

    public void Apply(string theme) => Applied = theme;
}

/// <summary>Guarda as notificações mostradas (título e texto).</summary>
internal sealed class FakeNotificationService : INotificationService
{
    public List<(string Title, string Message)> Shown { get; } = [];

    public void Show(string title, string message) => Shown.Add((title, message));
}

/// <summary>Faz o papel da tela de configurações: o teste diz o que o "usuário" muda e se salvou.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public Func<AppSettings, IReadOnlyList<IUsageProvider>, bool> OnShow { get; set; } = (_, _) => false;

    public Task<bool> ShowSettingsAsync(AppSettings settings, IReadOnlyList<IUsageProvider> providers) =>
        Task.FromResult(OnShow(settings, providers));
}

internal sealed class FakeStartupRegistration(bool enabled = false) : IStartupRegistration
{
    public bool IsEnabled { get; private set; } = enabled;

    public int SetCount { get; private set; }

    public void Set(bool enabled)
    {
        IsEnabled = enabled;
        SetCount++;
    }
}

internal sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings? Saved { get; private set; }

    public int SaveCount { get; private set; }

    public AppSettings Load() => Saved?.Clone() ?? new AppSettings();

    public void Save(AppSettings settings)
    {
        Saved = settings.Clone();
        SaveCount++;
    }
}

internal sealed class FakeCredentialStore<T>(T? credentials = null) : ICredentialStore<T>
    where T : class
{
    public T? Current { get; set; } = credentials;

    public int SaveCount { get; private set; }

    public T? Load() => Current;

    public void Save(T credentials)
    {
        Current = credentials;
        SaveCount++;
    }

    public void Clear() => Current = null;
}

internal sealed class FakeSecretVault : ISecretVault
{
    public Dictionary<string, string> Secrets { get; } = [];

    public string? Read(string key) => Secrets.GetValueOrDefault(key);

    public void Write(string key, string secret) => Secrets[key] = secret;

    public void Delete(string key) => Secrets.Remove(key);
}

/// <summary>Faz o papel do navegador: guarda o endereço aberto e executa o que o teste mandar.</summary>
internal sealed class FakeBrowserLauncher(Action<Uri>? onOpen = null) : IBrowserLauncher
{
    public Uri? Opened { get; private set; }

    public void Open(Uri url)
    {
        Opened = url;
        onOpen?.Invoke(url);
    }
}

/// <summary>
/// Responde cada requisição HTTP com o que <paramref name="respond"/> devolver e guarda todas as
/// requisições (com o corpo já lido, porque o HttpClient descarta o conteúdo depois do envio).
/// </summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public FakeHttpHandler(HttpStatusCode status, string body)
        : this(_ => new HttpResponseMessage(status) { Content = new StringContent(body) })
    {
    }

    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1].Request : null;

    public int Calls => Requests.Count;

    public Exception? Throw { get; init; }

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        if (Throw is not null)
            throw Throw;

        return respond(request);
    }
}

/// <summary>Conta com login que o teste controla.</summary>
internal sealed class FakeAccount : IProviderAccount
{
    public bool IsSignedIn { get; set; }

    /// <summary>O que o "login no navegador" faz; por padrão, entra na hora.</summary>
    public Func<CancellationToken, Task>? OnSignIn { get; init; }

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        if (OnSignIn is not null)
            await OnSignIn(cancellationToken);
        IsSignedIn = true;
    }

    public void SignOut() => IsSignedIn = false;
}

/// <summary>Provedor que responde o que o teste mandar (leitura ou exceção).</summary>
internal sealed class FakeUsageProvider(string id, Func<ProviderUsage> read) : IUsageProvider
{
    public int Calls { get; private set; }

    public string Id => id;

    public string DisplayName => id;

    public IProviderAccount? Account { get; init; }

    public Task<ProviderUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(read());
    }
}
