namespace WisLicense.Core;

public enum CodigoError
{
    Validacion,
    FormatoInvalido,
    FirmaInvalida,
    InstalacionDistinta,
    NoVigente,
}

/// <summary>Error tipado de emisión o verificación. Lo mapea el CLI a códigos de salida (1 entrada, 2 cripto/archivo).</summary>
public sealed class LicenciaException(CodigoError codigo, string mensaje) : Exception(mensaje)
{
    public CodigoError Codigo { get; } = codigo;
}
