# Deploy de la Edge Function `reportar-agente`

## 1. Requisitos
- Supabase CLI instalado (`npm install -g supabase`)
- Estar logueado: `supabase login`
- Tener el `project ref` de tu proyecto (Settings > General en el dashboard)

## 2. Copiar la carpeta a tu proyecto
Copia la carpeta `reportar-agente/` dentro de `supabase/functions/` de tu proyecto local
(donde ya deberías tener corriendo `supabase init` alguna vez, si no, córrelo primero).

```
supabase/
  functions/
    reportar-agente/
      index.ts
```

## 3. Configurar el secreto (la clave que van a usar los agentes)
```bash
supabase secrets set AGENT_API_KEY=una-clave-larga-y-aleatoria-que-inventes
```
Guarda esa misma clave para pegarla luego en el `appsettings.json` de cada agente Windows.

`SUPABASE_URL` y `SUPABASE_SERVICE_ROLE_KEY` **no hay que configurarlas manualmente** — Supabase
las inyecta automáticamente a toda Edge Function.

## 4. Deploy
```bash
supabase functions deploy reportar-agente --project-ref TU_PROJECT_REF
```

## 5. Probar que funciona (antes de instalar el agente)
```bash
curl -X POST "https://TU_PROJECT_REF.supabase.co/functions/v1/reportar-agente" \
  -H "Content-Type: application/json" \
  -H "x-agent-key: la-clave-que-configuraste" \
  -d '{
    "codigo": "LAP-001",
    "usuario_activo": "prueba.manual",
    "ip_local": "192.168.1.50",
    "red_wifi": "Oficina-Bogota",
    "bssid_conectado": "AA:BB:CC:DD:EE:FF",
    "bateria": 87,
    "version_agente": "test-manual"
  }'
```
Cambia `LAP-001` por un código que sí exista en tu tabla `activos`. Si responde
`{"ok":true,...}`, revisa la tabla `reportes` — debería aparecer la fila nueva con el
`estado` ya calculado por el trigger.

## Nota de seguridad
`AGENT_API_KEY` es una clave **compartida** por todos los agentes (no una por equipo). Está
bien para el piloto de 2-3 máquinas, pero si más adelante das de baja un equipo robado/perdido,
no hay forma de revocarle el acceso solo a ese equipo sin rotar la clave de todos. Si eso te
importa antes de escalar a los 300 equipos, dímelo y lo cambiamos a una clave por dispositivo.
