using AgenteActivos;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Permite que el mismo ejecutable corra como consola (para probar) o instalado
// como Windows Service (sc.exe create / New-Service). Cuando corre como
// servicio, los logs van al Visor de eventos de Windows automáticamente.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Agente Monitoreo Activos";
});

builder.Services.Configure<AgenteOptions>(builder.Configuration.GetSection("Agente"));
builder.Services.AddHttpClient<ReporteSender>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
