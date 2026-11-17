using WisLicense.Core;

namespace WisLicense.Tests;

public class ReglasTests
{
    private static readonly DateTime Desde = Ayudas.Desde;

    [Fact]
    public void Lifetime_valido_sin_validTo()
    {
        using var h = new Ayudas();
        Assert.Empty(ReglasLicencia.Validar(h.Lifetime()));
    }

    [Fact]
    public void Lifetime_con_validTo_es_invalido()
    {
        using var h = new Ayudas();
        var p = h.Lifetime() with { ValidTo = Desde.AddYears(1) };
        Assert.Contains(ReglasLicencia.Validar(p), e => e.Contains("no lleva validTo"));
    }

    [Fact]
    public void Lifetime_exige_maxVersionMayor_y_graceDays_cero()
    {
        using var h = new Ayudas();
        var p = h.Lifetime() with { MaxVersionMayor = null, GraceDays = 3 };
        var errores = ReglasLicencia.Validar(p);
        Assert.Contains(errores, e => e.Contains("maxVersionMayor"));
        Assert.Contains(errores, e => e.Contains("graceDays = 0"));
    }

    [Fact]
    public void Mensual_con_validTo_anterior_a_validFrom_es_invalido()
    {
        using var h = new Ayudas();
        var p = h.Mensual(validTo: Desde.AddDays(-1));
        Assert.Contains(ReglasLicencia.Validar(p), e => e.Contains("posterior a validFrom"));
    }

    [Fact]
    public void Mensual_sin_validTo_es_invalido()
    {
        using var h = new Ayudas();
        var p = h.Mensual() with { ValidTo = null };
        Assert.Contains(ReglasLicencia.Validar(p), e => e.Contains("exige validTo"));
    }

    [Fact]
    public void Fechas_no_UTC_son_invalidas()
    {
        using var h = new Ayudas();
        var p = h.Mensual() with { ValidFrom = DateTime.SpecifyKind(Desde, DateTimeKind.Local) };
        Assert.Contains(ReglasLicencia.Validar(p), e => e.Contains("validFrom debe ser UTC"));
    }

    [Fact]
    public void Empaquetar_rechaza_payload_invalido()
    {
        using var h = new Ayudas();
        var ex = Assert.Throws<LicenciaException>(() => h.Emitir(h.Mensual() with { Cliente = "" }));
        Assert.Equal(CodigoError.Validacion, ex.Codigo);
    }
}
