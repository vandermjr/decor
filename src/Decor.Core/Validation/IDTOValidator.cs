namespace Decor.Core.Validation;
public interface IDTOValidator<T>
{
    /// <summary>
    /// Valida o dto e retorna uma lista de mensagens de erro, ou uma lista vazia se estiver tudo OK.
    /// </summary>
    IEnumerable<string> Validate(T dto);
}


