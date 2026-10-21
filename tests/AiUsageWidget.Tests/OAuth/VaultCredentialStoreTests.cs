using AiUsageWidget.Models;
using AiUsageWidget.Services.OAuth;
using AiUsageWidget.Tests.Fakes;

namespace AiUsageWidget.Tests.OAuth;

public class VaultCredentialStoreTests
{
    private const string Key = "AiUsageWidget/teste";

    private readonly FakeSecretVault _vault = new();

    private VaultCredentialStore<CodexCredentials> Create() => new(_vault, Key);

    [Fact]
    public void SemNadaNoCofre_DevolveNulo() =>
        Assert.Null(Create().Load());

    [Fact]
    public void GravaELe()
    {
        var store = Create();
        var login = new CodexCredentials("a", "r", new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero), "conta-ficticia", "plus");

        store.Save(login);

        Assert.Equal(login, store.Load());
        Assert.True(_vault.Secrets.ContainsKey(Key));
    }

    [Fact]
    public void CadaProvedorUsaASuaChave()
    {
        new VaultCredentialStore<ClaudeCredentials>(_vault, "AiUsageWidget/claude").Save(new ClaudeCredentials("c", null, null));
        Create().Save(new CodexCredentials("x", null, null, null, null));

        Assert.Equal(2, _vault.Secrets.Count);
    }

    [Fact]
    public void ConteudoEstragado_DescartaEPedeLogin()
    {
        _vault.Secrets[Key] = "{ quebrado";

        Assert.Null(Create().Load());
        Assert.Empty(_vault.Secrets);
    }

    [Fact]
    public void Clear_ApagaDoCofre()
    {
        var store = Create();
        store.Save(new CodexCredentials("a", null, null, null, null));

        store.Clear();

        Assert.Empty(_vault.Secrets);
    }
}
