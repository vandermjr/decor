namespace Decor.Core.Validation;
public interface IRepositoryValidator<T>
{
    /// <summary>
    /// Valida a entidade de domínio no contexto do repositório e retorna mensagens de erro, se houver.
    /// </summary>
    IEnumerable<string> Validate(T entity);
}
