namespace Decor.Core.Common;

public static class CommercialItemReference
{
    public static bool IsValid(int? productId, int? serviceId)
        => (productId is > 0 && serviceId is null)
            || (serviceId is > 0 && productId is null);

    public const string ValidationMessage = "O item deve identificar exatamente um produto ou servico com codigo positivo.";
}