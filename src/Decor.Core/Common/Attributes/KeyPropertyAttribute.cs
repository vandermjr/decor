namespace Decor.Core.Common.Attributes;

/// <summary>
/// Marca uma propriedade de um DTO como sendo a propriedade de chave/identificação principal,
/// permitindo que a UI aplique um estilo visual diferenciado a ela.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeyPropertyAttribute : Attribute
{
}