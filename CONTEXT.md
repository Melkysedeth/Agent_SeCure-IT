# Agente Windows — Monitoreo de Activos

## Qué hace
Cada `IntervaloMinutos` (15 min para las pruebas iniciales) recolecta:
usuario de Windows con sesión activa, IP local, IP pública, SSID/BSSID de WiFi
(si el equipo está conectado por WiFi), y porcentaje de batería (si aplica).
Lo envía por HTTPS a la Edge Function `reportar-agente`. **No calcula el
`estado`** ("en línea"/"fuera de sede") — eso lo decide el trigger de la base
de datos según el BSSID que reciba.

## 1. Requisitos
- .NET 8 SDK: https://dotnet.microsoft.com/download
- Windows 10/11 o Windows Server (para correr como servicio)

## 2. Configurar antes de compilar
Edita `appsettings.json`:
```json
{
  "Agente": {
    "Codigo": "LAP-001",              // <- código real del activo en tu inventario
    "EdgeFunctionUrl": "https://TU_PROJECT_REF.supabase.co/functions/v1/reportar-agente",
    "ApiKey": "la-misma-clave-de-AGENT_API_KEY-en-supabase",
    "IntervaloMinutos": 15,
    "VersionAgente": "1.0.0-windows"
  }
}
```
**Cada equipo necesita su propio `Codigo`** — es como el agente sabe de qué activo está
reportando. Todo lo demás puede quedar igual en todos los equipos de prueba.

## Lo que logramos:

- Desplegamos la Edge Function reportar-agente en Supabase (código completo, ya en producción).
- Creamos el secreto AGENT_API_KEY en Supabase (guárdala en un lugar seguro, ya la tienes).
- Desactivamos "Verify JWT" en esa función para que solo dependa de tu x-agent-key.
- Corregimos el insert por un upsert en la función, porque reportes guarda solo el último estado por activo (no historial) — confirmado por la restricción UNIQUE en activo_id.
- Probamos la función con curl desde PowerShell hasta que respondió {"ok":true,...}.
- Instalamos el SDK de .NET 8 en tu computador.
- Instalamos el paquete NuGet Microsoft.Extensions.Http que faltaba (el .csproj ya quedó actualizado con esto).
- Corrimos el agente con dotnet run — funcionó, mandó el reporte, y confirmamos que la hora en la base de datos está en UTC (normal, hay que convertirla en el dashboard después).

**Importante: el agente NO quedó instalado permanentemente. Solo corrió en modo prueba (dotnet run), en una terminal abierta. Si cerraste esa terminal, el agente ya se detuvo.**

## Qué falta (para mañana o desde otra cuenta)
- Empaquetar el agente como .exe:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -o ./publicado
```
- Instalarlo como servicio de Windows (para que corra solo, sin terminal abierta, incluso tras reiniciar):
```bash
   sc.exe create "AgenteMonitoreoActivos" binPath= "C:\AgenteActivos\AgenteActivos.exe" start= auto
   sc.exe start "AgenteMonitoreoActivos"
```
- Repetir la instalación en 2-3 equipos de prueba, cambiando el Codigo en el appsettings.json de cada uno.
- Verificar con sc.exe query "AgenteMonitoreoActivos" que quedó RUNNING.
- Pendiente aparte: resolver el geofencing para equipos conectados por cable (sin WiFi/BSSID) — tenías la idea de usar rangos de IP fija por sede.
- Pendiente aparte: convertir la hora UTC a hora de Colombia en el Dashboard Web cuando lo construyan.