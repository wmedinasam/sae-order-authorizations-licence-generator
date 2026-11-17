namespace WisLicense.Core;

public enum EstadoVigencia
{
    Vigente,
    EnGracia,
    Expirada,
}

public static class Vigencia
{
    /// <summary>Lifetime nunca expira. Mensual/anual: vigente hasta validTo, luego gracia, luego expirada.</summary>
    public static EstadoVigencia Evaluar(LicenciaPayload p, DateTime ahoraUtc)
    {
        if (p.ValidTo is null) return EstadoVigencia.Vigente;
        if (ahoraUtc <= p.ValidTo) return EstadoVigencia.Vigente;
        if (ahoraUtc <= p.ValidTo.Value.AddDays(p.GraceDays)) return EstadoVigencia.EnGracia;
        return EstadoVigencia.Expirada;
    }
}
