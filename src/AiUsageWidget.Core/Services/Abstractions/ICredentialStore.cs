namespace AiUsageWidget.Services.Abstractions;

/// <summary>Onde o widget guarda o próprio login de um provedor.</summary>
public interface ICredentialStore<T>
    where T : class
{
    /// <summary>O login atual, ou nulo se o usuário ainda não entrou.</summary>
    T? Load();

    void Save(T credentials);

    void Clear();
}
