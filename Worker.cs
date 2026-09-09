using Microsoft.Extensions.Options;

namespace AgenteActivos;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ReporteSender _sender;
    private readonly AgenteOptions _options;

    public Worker(ILogger<Worker> logger, ReporteSender sender, IOptions<AgenteOptions> options)
    {
        _logger = logger;
        _sender = sender;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _options.IntervaloMinutos));
        _logger.LogInformation(
            "Agente iniciado para el activo {Codigo}. Intervalo de reporte: {Intervalo} min.",
            _options.Codigo, intervalo.TotalMinutes);

        RecolectorDatos.AsegurarServicioUbicacionActivo();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _sender.EnviarReporteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Nunca dejamos que un error de un ciclo tumbe el servicio completo:
                // simplemente se reintenta en el siguiente ciclo.
                _logger.LogError(ex, "Error inesperado enviando el reporte. Se reintentará en el próximo ciclo.");
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // El servicio se está deteniendo, salimos del loop normalmente.
            }
        }
    }
}
