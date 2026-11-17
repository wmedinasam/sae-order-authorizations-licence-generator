// Puente JS mínimo para Blazor: descarga del .lic y tema del kit de marca (brand/web/theme-init.js).
window.WISLicense = {
  descargar: function (nombre, base64) {
    var bytes = Uint8Array.from(atob(base64), function (c) { return c.charCodeAt(0); });
    var url = URL.createObjectURL(new Blob([bytes], { type: "application/octet-stream" }));
    var enlace = document.createElement("a");
    enlace.href = url;
    enlace.download = nombre;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 0);
  },
  tema: function () {
    return window.WISBrandTheme ? window.WISBrandTheme.get() : "auto";
  },
  fijarTema: function (valor) {
    if (window.WISBrandTheme) window.WISBrandTheme.set(valor);
  }
};
