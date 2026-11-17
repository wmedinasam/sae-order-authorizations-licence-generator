namespace WisLicense.Core;

/// <summary>Reglas de negocio del payload (plan §5). Devuelve errores legibles; lista vacía = válido.</summary>
public static class ReglasLicencia
{
    public static IReadOnlyList<string> Validar(LicenciaPayload p)
    {
        var errores = new List<string>();

        if (p.Formato != Formato.VersionFormato) errores.Add($"formato debe ser {Formato.VersionFormato}.");
        if (p.LicenseId == Guid.Empty) errores.Add("licenseId es obligatorio.");
        if (p.Serial < 1) errores.Add("serial debe ser >= 1.");
        if (string.IsNullOrWhiteSpace(p.Cliente)) errores.Add("cliente es obligatorio.");
        if (p.InstallationId == Guid.Empty) errores.Add("installationId es obligatorio.");
        if (!Planes.Todos.Contains(p.Plan)) errores.Add($"plan debe ser {string.Join(" | ", Planes.Todos)}.");
        if (!Ediciones.Todas.Contains(p.Edicion)) errores.Add($"edicion debe ser {string.Join(" | ", Ediciones.Todas)}.");
        if (p.MaxEmpresas < 1) errores.Add("maxEmpresas debe ser >= 1.");
        if (p.MaxUsuarios < 1) errores.Add("maxUsuarios debe ser >= 1.");
        if (p.GraceDays < 0) errores.Add("graceDays no puede ser negativo.");
        if (p.MaxVersionMayor is < 1) errores.Add("maxVersionMayor debe ser >= 1 si se indica.");

        foreach (var (nombre, fecha) in new[] { ("emitida", p.Emitida), ("validFrom", p.ValidFrom) })
            if (fecha.Kind != DateTimeKind.Utc && fecha != default) errores.Add($"{nombre} debe ser UTC.");

        if (p.Plan == Planes.Lifetime)
        {
            if (p.ValidTo is not null) errores.Add("lifetime no lleva validTo.");
            if (p.GraceDays != 0) errores.Add("lifetime exige graceDays = 0.");
            if (p.MaxVersionMayor is null) errores.Add("lifetime exige maxVersionMayor.");
        }
        else if (Planes.Todos.Contains(p.Plan))
        {
            if (p.ValidTo is null) errores.Add($"{p.Plan} exige validTo.");
            else if (p.ValidTo.Value.Kind != DateTimeKind.Utc) errores.Add("validTo debe ser UTC.");
            else if (p.ValidTo <= p.ValidFrom) errores.Add("validTo debe ser posterior a validFrom.");
        }

        return errores;
    }
}
