using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Microsoft.Win32;

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

    // Nota: esta función solo consulta Win32_DiskDrive.Size (capacidad TOTAL del disco físico).
    // A propósito NO se consulta Win32_LogicalDisk.FreeSpace (espacio disponible), que es un
    // dato distinto y no se quiere enviar al backend. No requiere cambios: ya cumple con
    // "enviar solo el total, no el disponible".
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

    public static string? ObtenerAlmacenamientoDisponible()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3"); // 3 = disco fijo
            double totalLibre = 0;
            foreach (ManagementObject item in searcher.Get())
            {
                var libre = item["FreeSpace"];
                if (libre is not null) totalLibre += Convert.ToDouble(libre);
            }
            if (totalLibre == 0) return null;
            double gb = totalLibre / (1024 * 1024 * 1024);
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


    public static void AsegurarServicioUbicacionActivo()
    {
        try
        {
            // 1. Verificar/activar el switch maestro de ubicación (HKLM, requiere SYSTEM/Admin)
            using var claveConfig = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", writable: true);

            if (claveConfig is not null)
            {
                var estadoActual = claveConfig.GetValue("Status");
                if (estadoActual is null || Convert.ToInt32(estadoActual) != 1)
                {
                    claveConfig.SetValue("Status", 1, RegistryValueKind.DWord);
                }
            }

            // 2. Asegurar que el servicio lfsvc esté corriendo (puede estar detenido aunque el switch esté en 1)
            using var sc = new ServiceController("lfsvc");
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }
        catch
        {
            // Si falla (permisos, servicio no existe, etc.), no interrumpe el resto del agente
        }

    }

    // Fabricantes "placeholder" que en realidad son el nombre del driver genérico de
    // Windows, no la marca real del periférico. Si se dejan tal cual, confunden en el
    // inventario (ej: aparece "Microsoft" como fabricante de un mouse Logitech
    // conectado por dongle genérico). Se limpian a null para no reportar un dato falso.
    private static readonly string[] FabricantesGenericos =
    {
        "(standard keyboards)",
        "(standard mouse types)",
        "(standard system devices)",
        "microsoft"
    };

    public static List<PerifericoInfo> ObtenerPerifericos()
    {
        var resultado = new List<PerifericoInfo>();

        void AgregarDesdeConsulta(string query, string tipoDefault)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject item in searcher.Get())
                {
                    var nombre = item["Name"]?.ToString()?.Trim();
                    var deviceId = item["DeviceID"]?.ToString()?.Trim();
                    var fabricante = item["Manufacturer"]?.ToString()?.Trim();

                    if (string.IsNullOrWhiteSpace(nombre)) continue;
                    if (deviceId is not null && deviceId.Contains("ACPI", StringComparison.OrdinalIgnoreCase)) continue;
                    if (nombre.Contains("Integrated", StringComparison.OrdinalIgnoreCase)) continue;
                    if (nombre.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;

                    if (tipoDefault == "audio")
                    {
                        bool esUsbOBluetooth = deviceId is not null &&
                            (deviceId.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
                             deviceId.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase));

                        if (!esUsbOBluetooth) continue;
                    }

                    // El fabricante que reporta WMI para dispositivos HID genéricos suele ser
                    // el publisher del driver (p. ej. "Microsoft"), no la marca real. Se limpia
                    // para no guardar en la base de datos un fabricante incorrecto.
                    if (fabricante is not null && FabricantesGenericos.Contains(fabricante.ToLowerInvariant()))
                    {
                        fabricante = null;
                    }

                    resultado.Add(new PerifericoInfo(tipoDefault, nombre, fabricante, deviceId));
                }
            }
            catch
            {
                // Si falla una categoría puntual, seguimos con las demás
            }
        }

        AgregarDesdeConsulta(
            "SELECT Name, DeviceID, Manufacturer FROM Win32_SoundDevice", "audio");
        AgregarDesdeConsulta(
            "SELECT Name, DeviceID, Manufacturer FROM Win32_PnPEntity WHERE PNPClass = 'Keyboard'", "teclado");
        AgregarDesdeConsulta(
            "SELECT Name, DeviceID, Manufacturer FROM Win32_PnPEntity WHERE PNPClass = 'Mouse'", "mouse");

        try
        {
            var panelesInternos = new HashSet<string>();
            using var searcherConexion = new ManagementObjectSearcher(
                "root\\wmi", "SELECT InstanceName, VideoOutputTechnology FROM WmiMonitorConnectionParams");
            foreach (ManagementObject item in searcherConexion.Get())
            {
                var tech = item["VideoOutputTechnology"];
                if (tech is not null && Convert.ToUInt32(tech) == 0x80000000)
                {
                    var instancia = item["InstanceName"]?.ToString()?.Trim();
                    if (instancia is not null) panelesInternos.Add(instancia);
                }
            }

            using var searcherMonitor = new ManagementObjectSearcher(
                "root\\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
            foreach (ManagementObject item in searcherMonitor.Get())
            {
                var instancia = item["InstanceName"]?.ToString()?.Trim();
                if (instancia is not null && panelesInternos.Contains(instancia)) continue;

                var nombreBytes = item["UserFriendlyName"] as ushort[];
                var nombre = nombreBytes is not null
                    ? new string(nombreBytes.Where(b => b != 0).Select(b => (char)b).ToArray())
                    : null;

                if (!string.IsNullOrWhiteSpace(nombre))
                {
                    resultado.Add(new PerifericoInfo("monitor", nombre, null, instancia));
                }
            }
        }
        catch
        {
            // Si falla, seguimos sin monitor externo detectado
        }

        return FiltrarGenericos(resultado);
    }

    private static string? ExtraerVid(string? deviceId)
    {
        if (deviceId is null) return null;
        var m = Regex.Match(deviceId, @"VID_([0-9A-Fa-f]{4})");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static List<PerifericoInfo> FiltrarGenericos(List<PerifericoInfo> items)
    {
        bool esGenerico(PerifericoInfo p) =>
            p.nombre.Contains("compatible con HID", StringComparison.OrdinalIgnoreCase) ||
            p.nombre.Contains("HID-compliant", StringComparison.OrdinalIgnoreCase) ||
            p.nombre.Contains("Dispositivo de teclado HID", StringComparison.OrdinalIgnoreCase) ||
            p.nombre.Contains("HID Keyboard Device", StringComparison.OrdinalIgnoreCase) ||
            p.nombre.StartsWith("Varios ", StringComparison.OrdinalIgnoreCase);

        var resultado = new List<PerifericoInfo>();

        foreach (var grupo in items.GroupBy(p => p.tipo))
        {
            var especificos = grupo.Where(p => !esGenerico(p)).ToList();
            var vidsEspecificos = especificos
                .Select(p => ExtraerVid(p.device_id))
                .Where(v => v is not null)
                .ToHashSet();

            // Un genérico solo se descarta si comparte VID con un específico ya
            // detectado (misma pieza de hardware contada dos veces). Si su VID
            // es distinto (o no tiene específico con quien comparar), es un
            // dispositivo real aparte y se conserva.
            var genericosValidos = grupo
                .Where(esGenerico)
                .Where(p =>
                {
                    var vid = ExtraerVid(p.device_id);
                    return vid is null || !vidsEspecificos.Contains(vid);
                });

            resultado.AddRange(especificos);
            resultado.AddRange(genericosValidos);
        }

        return resultado;
    }
}

public record PerifericoInfo(string tipo, string nombre, string? fabricante, string? device_id);