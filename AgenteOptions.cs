namespace AgenteActivos;

public class AgenteOptions
{
    public string Codigo { get; set; } = "";
    public string EdgeFunctionUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int IntervaloMinutos { get; set; } = 120;
    public string VersionAgente { get; set; } = "1.0.0-windows";
}