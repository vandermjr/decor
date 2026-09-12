using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Decor.FluentSqlBuilder.Helpers;

/// <summary>
/// Gerencia o mapeamento entre tipos de entidade, aliases de tabela e nomes de tabelas no banco de dados.
/// Suporta geração automática e determinação por convenção ou registro explícito.
/// </summary>
public class TableAliasRegistry
{
    private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "as", "by", "in", "is", "join", "not", "on", "or"
    };

    private readonly Dictionary<Type, string> _typeAliases = [];
    private readonly Dictionary<string, string> _aliasTableNames = [];
    private readonly Dictionary<Type, string> _typeTableNames = [];
    private readonly HashSet<string> _usedAliases = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<Type, string> TypeAliases => _typeAliases;
    public IReadOnlyDictionary<string, string> AliasTableNames => _aliasTableNames;

    /// <summary>
    /// Registra explicitamente um alias e nome de tabela para uma entidade.
    /// Mantido para compatibilidade e casos específicos de customização.
    /// </summary>
    public void RegisterAlias(Type entityType, string alias, string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        if (_typeAliases.TryGetValue(entityType, out var existingAlias))
        {
            if (existingAlias != alias)
            {
                throw new ArgumentException($"O tipo '{entityType.Name}' já foi registrado com o alias '{existingAlias}'.");
            }
            return;
        }

        if (_aliasTableNames.TryGetValue(alias, out var existingTable))
        {
            throw new ArgumentException($"O alias '{alias}' já foi registrado para a tabela '{existingTable}'.");
        }

        string finalTableName = tableName ?? ResolveTableName(entityType);

        _typeAliases[entityType] = alias;
        _aliasTableNames[alias] = finalTableName;
        _typeTableNames[entityType] = finalTableName;
        _usedAliases.Add(alias);
    }

    /// <summary>
    /// Obtém o alias para o tipo de entidade fornecido ou gera um automaticamente se ainda não registrado.
    /// </summary>
    public string GetOrAddAlias(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_typeAliases.TryGetValue(entityType, out var alias))
        {
            return alias;
        }

        alias = GenerateAlias(entityType);
        string tableName = ResolveTableName(entityType);

        _typeAliases[entityType] = alias;
        _aliasTableNames[alias] = tableName;
        _typeTableNames[entityType] = tableName;
        _usedAliases.Add(alias);

        return alias;
    }

    /// <summary>
    /// Obtém o alias para o tipo genérico fornecido ou gera um automaticamente.
    /// </summary>
    public string GetOrAddAlias<TEntity>() => GetOrAddAlias(typeof(TEntity));

    /// <summary>
    /// Obtém o nome da tabela para a entidade fornecida.
    /// </summary>
    public string GetTableName(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (!_typeTableNames.TryGetValue(entityType, out var tableName))
        {
            GetOrAddAlias(entityType);
            tableName = _typeTableNames[entityType];
        }

        return tableName;
    }

    /// <summary>
    /// Obtém o nome da tabela para o tipo genérico fornecido.
    /// </summary>
    public string GetTableName<TEntity>() => GetTableName(typeof(TEntity));

    /// <summary>
    /// Obtém o nome da tabela associado ao alias fornecido.
    /// </summary>
    public string GetTableNameForAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        if (_aliasTableNames.TryGetValue(alias, out var tableName))
        {
            return tableName;
        }

        throw new ArgumentException($"Nome da tabela não encontrado para o alias: {alias}.");
    }

    /// <summary>
    /// Tenta obter o alias já registrado para o tipo.
    /// </summary>
    public bool TryGetAlias(Type entityType, out string? alias)
    {
        return _typeAliases.TryGetValue(entityType, out alias);
    }

    /// <summary>
    /// Tenta obter o nome da tabela para um alias.
    /// </summary>
    public bool TryGetTableName(string alias, out string? tableName)
    {
        return _aliasTableNames.TryGetValue(alias, out tableName);
    }

    private string GenerateAlias(Type entityType)
    {
        var upperLetters = entityType.Name.Where(char.IsUpper).Select(char.ToLowerInvariant).ToArray();
        string baseCandidate = upperLetters.Length > 0
            ? new string(upperLetters)
            : entityType.Name[..1].ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(baseCandidate))
        {
            baseCandidate = "t";
        }

        if (!_usedAliases.Contains(baseCandidate) && !ReservedAliases.Contains(baseCandidate))
        {
            return baseCandidate;
        }

        int suffix = 1;
        while (_usedAliases.Contains($"{baseCandidate}{suffix}") || ReservedAliases.Contains($"{baseCandidate}{suffix}"))
        {
            suffix++;
        }

        return $"{baseCandidate}{suffix}";
    }

    private static string ResolveTableName(Type entityType)
    {
        var tableAttribute = entityType.GetCustomAttribute<TableAttribute>();
        return tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name)
            ? tableAttribute.Name
            : entityType.Name + "s";
    }
}
