using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace AgenteActivos;

public record ReportePayload(
    string codigo,
    string? usuario_activo,
    string? ip_local,
    string? ip_publica,
    string? red_wifi,
    string? bssid_conectado,
    int? bateria,
    string? version_agente,
    string? nombre_equipo,
    string? serial,
    string? marca,
    string? modelo,
    string? sistema_op,
    string? version_so,
    string? procesador,
    string? memoria_ram,
    string? almacenamiento,
    string? direccion_mac
);

public class ReporteSender
{
    private readonly HttpClient _http;
    private readonly AgenteOptions _options;
    private readonly ILogger<ReporteSender> _logger;

    public ReporteSender(HttpClient http, IOptions<AgenteOptions> options, ILogger<ReporteSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnviarReporteAsync(CancellationToken ct)
    {
        var wifi = RecolectorDatos.ObtenerInfoWifi();
        var ipPublica = await RecolectorDatos.ObtenerIpPublicaAsync(_http, ct);
        var (marca, modelo) = RecolectorDatos.ObtenerMarcaModelo();
        var (sistemaOp, versionSo) = RecolectorDatos.ObtenerSistemaOperativo();

        var payload = new ReportePayload(
            codigo: _options.Codigo,
            usuario_activo: RecolectorDatos.ObtenerUsuarioActivo(),
            ip_local: RecolectorDatos.ObtenerIpLocal(),
            ip_publica: ipPublica,
            red_wifi: wifi.Ssid,
            bssid_conectado: wifi.Bssid,
            bateria: RecolectorDatos.ObtenerBateria(),
            version_agente: _options.VersionAgente,
            nombre_equipo: RecolectorDatos.ObtenerNombreEquipo(),
            serial: RecolectorDatos.ObtenerSerial(),
            marca: marca,
            modelo: modelo,
            sistema_op: sistemaOp,
            version_so: versionSo,
            procesador: RecolectorDatos.ObtenerProcesador(),
            memoria_ram: RecolectorDatos.ObtenerMemoriaRam(),
            almacenamiento: RecolectorDatos.ObtenerAlmacenamiento(),
            direccion_mac: RecolectorDatos.ObtenerDireccionMac()
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.EdgeFunctionUrl)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("x-agent-key", _options.ApiKey);

        var response = await _http.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Reporte enviado correctamente para el activo {Codigo}.", _options.Codigo);
        }
        else
        {
            var cuerpo = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "El servidor rechazó el reporte del activo {Codigo}. Status: {Status}. Respuesta: {Cuerpo}",
                _options.Codigo, response.StatusCode, cuerpo);
        }
    }
}
