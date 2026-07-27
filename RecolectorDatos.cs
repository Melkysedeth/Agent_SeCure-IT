using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace AgenteActivos;

public record InfoWifi(string? Ssid, string? Bssid);

public static class RecolectorDatos
{
    public static string ObtenerUsuarioActivo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                var valor = item["UserName"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(valor)) return valor;
            }
            // Nadie con sesión interactiva iniciada (pantalla de login, o sesión cerrada)
            return "Sin sesión activa";
        }
        catch
        {
            return Environment.UserName; // fallback si falla WMI
        }
    }

    public static string? ObtenerIpLocal()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // No manda tráfico real, solo hace que el SO elija la interfaz de salida.
            socket.Connect("8.8.8.8", 65530);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> ObtenerIpPublicaAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var ip = await http.GetStringAsync("https://api.ipify.org", ct);
            return ip.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ejecuta `netsh wlan show interfaces` y extrae SSID/BSSID de la conexión activa.
    /// Si el equipo está conectado por cable (sin adaptador WiFi activo), devuelve
    /// (null, null) — ver aviso sobre el trigger de geofencing antes de instalar
    /// esto en desktops sin WiFi.
    /// </summary>
    public static InfoWifi ObtenerInfoWifi()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proceso = Process.Start(psi);
            if (proceso is null) return new InfoWifi(null, null);

            string salida = proceso.StandardOutput.ReadToEnd();
            proceso.WaitForExit(5000);

            string? ssid = Regex.Match(salida, @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline)
                .Groups[1].Value.Trim();
            string? bssid = Regex.Match(salida, @"BSSID\s*:\s*([0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5})")
                .Groups[1].Value.Trim();

            return new InfoWifi(
                string.IsNullOrWhiteSpace(ssid) ? null : ssid,
                string.IsNullOrWhiteSpace(bssid) ? null : bssid
            );
        }
        catch
        {
            return new InfoWifi(null, null);
        }
    }

    public static string ObtenerNombreEquipo()
    {
        return Environment.MachineName;
    }

    public static string? ObtenerSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (ManagementObject item in searcher.Get())
            {
                var valor = item["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(valor)) return valor;
            }
            return null;
        }
        catch { return null; }
    }

    public static (string? Marca, string? Modelo) ObtenerMarcaModelo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                return (
                    item["Manufacturer"]?.ToString()?.Trim(),
                    item["Model"]?.ToString()?.Trim()
                );
            }
            return (null, null);
        }
        catch { return (null, null); }
    }

    public static (string? Sistema, string? Version) ObtenerSistemaOperativo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                return (
                    item["Caption"]?.ToString()?.Trim(),
                    item["Version"]?.ToString()?.Trim()
                );
            }
            return (null, null);
        }
        catch { return (null, null); }
    }

    public static string? ObtenerProcesador()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject item in searcher.Get())
            {
                var valor = item["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(valor)) return valor;
            }
            return null;
        }
        catch { return null; }
    }

    public static string? ObtenerMemoriaRam()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                var bytes = item["TotalPhysicalMemory"];
                if (bytes is not null)
                {
                    double gb = Convert.ToDouble(bytes) / (1024 * 1024 * 1024);
                    return $"{Math.Round(gb)} GB";
                }
            }
            return null;
        }
        catch { return null; }
    }

    public static string? ObtenerAlmacenamiento()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Size FROM Win32_DiskDrive");
            double totalBytes = 0;
            foreach (ManagementObject item in searcher.Get())
            {
                var size = item["Size"];
                if (size is not null) totalBytes += Convert.ToDouble(size);
            }
            if (totalBytes == 0) return null;
            double gb = totalBytes / (1024 * 1024 * 1024);
            return $"{Math.Round(gb)} GB";
        }
        catch { return null; }
    }

    public static string? ObtenerDireccionMac()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT MACAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (ManagementObject item in searcher.Get())
            {
                var mac = item["MACAddress"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(mac)) return mac;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Porcentaje de batería vía WMI. Devuelve null en desktops sin batería
    /// (no es un error, simplemente no aplica).
    /// </summary>
    public static int? ObtenerBateria()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining FROM Win32_Battery");
            foreach (ManagementObject bateria in searcher.Get())
            {
                var valor = bateria["EstimatedChargeRemaining"];
                if (valor is not null) return Convert.ToInt32(valor);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
