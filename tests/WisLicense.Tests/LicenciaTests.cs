using System.Security.Cryptography;
using WisLicense.Core;

namespace WisLicense.Tests;

public class LicenciaTests
{
    private static readonly DateTime Dentro = Ayudas.Desde.AddDays(10);

    [Fact]
    public void Roundtrip_devuelve_el_mismo_payload()
    {
        using var h = new Ayudas();
        var original = h.Mensual();
        var lic = h.Emitir(original);

        using var pub = h.ClavePublica();
        var abierto = LectorLicencia.Abrir(lic, h.AppSecret, pub, h.InstallationId);

        Assert.Equal(original, abierto);
    }

    [Fact]
    public void Lifetime_roundtrip_con_validTo_nulo()
    {
        using var h = new Ayudas();
        using var pub = h.ClavePublica();
        var abierto = LectorLicencia.Abrir(h.Emitir(h.Lifetime()), h.AppSecret, pub, h.InstallationId);
        Assert.Null(abierto.ValidTo);
        Assert.Equal(1, abierto.MaxVersionMayor);
    }

    [Fact]
    public void Dos_emisiones_del_mismo_payload_difieren_por_el_nonce()
    {
        using var h = new Ayudas();
        var p = h.Mensual();
        Assert.NotEqual(h.Emitir(p), h.Emitir(p));
    }

    [Fact]
    public void Otra_instalacion_da_InstalacionDistinta()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        using var pub = h.ClavePublica();

        var ex = Assert.Throws<LicenciaException>(() =>
            LectorLicencia.Abrir(lic, h.AppSecret, pub, Guid.NewGuid()));
        Assert.Equal(CodigoError.InstalacionDistinta, ex.Codigo);
    }

    [Fact]
    public void Otra_clave_publica_da_FirmaInvalida()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        using var otra = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var ex = Assert.Throws<LicenciaException>(() =>
            LectorLicencia.Abrir(lic, h.AppSecret, otra, h.InstallationId));
        Assert.Equal(CodigoError.FirmaInvalida, ex.Codigo);
    }

    // Plan: alterar un byte en cualquier región debe fallar siempre.
    [Theory]
    [InlineData("magic")]
    [InlineData("nonce")]
    [InlineData("longitud")]
    [InlineData("ciphertext")]
    [InlineData("tag")]
    [InlineData("firma_primer_byte")]
    [InlineData("firma_ultimo_byte")]
    public void Alterar_un_byte_en_cualquier_region_falla(string region)
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        using var pub = h.ClavePublica();

        var idx = region switch
        {
            "magic" => 0,
            "nonce" => Formato.MagicLen + 1,
            "longitud" => Formato.OffsetLongitud,
            "ciphertext" => Formato.OffsetCiphertext,
            "tag" => lic.Length - Formato.FirmaLen - 1,
            "firma_primer_byte" => lic.Length - Formato.FirmaLen,
            _ => lic.Length - 1,
        };

        lic[idx] ^= 0x01;

        Assert.ThrowsAny<LicenciaException>(() =>
            LectorLicencia.Abrir(lic, h.AppSecret, pub, h.InstallationId));
    }

    [Fact]
    public void Version_de_formato_distinta_da_FormatoInvalido()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        lic[Formato.MagicLen] = 0x02;
        using var pub = h.ClavePublica();

        var ex = Assert.Throws<LicenciaException>(() =>
            LectorLicencia.Abrir(lic, h.AppSecret, pub, h.InstallationId));
        Assert.Equal(CodigoError.FormatoInvalido, ex.Codigo);
    }

    [Fact]
    public void Archivo_truncado_da_FormatoInvalido()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual())[..50];
        using var pub = h.ClavePublica();

        var ex = Assert.Throws<LicenciaException>(() =>
            LectorLicencia.Abrir(lic, h.AppSecret, pub, h.InstallationId));
        Assert.Equal(CodigoError.FormatoInvalido, ex.Codigo);
    }

    [Fact]
    public void Verificar_acepta_licencia_vigente()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        using var pub = h.ClavePublica();

        var p = LectorLicencia.Verificar(lic, h.AppSecret, pub, h.InstallationId, Dentro);
        Assert.Equal(Planes.Mensual, p.Plan);
    }

    [Fact]
    public void Verificar_rechaza_licencia_expirada_fuera_de_gracia()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        using var pub = h.ClavePublica();

        var ex = Assert.Throws<LicenciaException>(() =>
            LectorLicencia.Verificar(lic, h.AppSecret, pub, h.InstallationId, Ayudas.Desde.AddMonths(1).AddDays(8)));
        Assert.Equal(CodigoError.NoVigente, ex.Codigo);
    }

    [Fact]
    public void Gracia_sigue_permitiendo_la_licencia()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Mensual());
        using var pub = h.ClavePublica();

        var p = LectorLicencia.Verificar(lic, h.AppSecret, pub, h.InstallationId, Ayudas.Desde.AddMonths(1).AddDays(3));
        Assert.Equal(EstadoVigencia.EnGracia, Vigencia.Evaluar(p, Ayudas.Desde.AddMonths(1).AddDays(3)));
    }

    [Fact]
    public void Lifetime_nunca_expira()
    {
        using var h = new Ayudas();
        var lic = h.Emitir(h.Lifetime());
        using var pub = h.ClavePublica();

        var p = LectorLicencia.Verificar(lic, h.AppSecret, pub, h.InstallationId, DateTime.UtcNow.AddYears(50));
        Assert.Equal(Planes.Lifetime, p.Plan);
    }

    [Fact]
    public void Derivacion_usa_el_guid_en_orden_RFC4122()
    {
        // Pin del orden de bytes: Guid.ToByteArray() daría otro salt en los 3 primeros campos.
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var salt = Convert.FromHexString("00112233445566778899aabbccddeeff");
        var secreto = Convert.FromHexString(new string('a', 64));

        var esperada = HKDF.DeriveKey(HashAlgorithmName.SHA256, secreto, 32, salt, Formato.InfoHkdf);
        var real = ClaveAes.Derivar(secreto, id);

        Assert.Equal(esperada, real);
    }
}
