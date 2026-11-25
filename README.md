# Games

Games es un lanzador de escritorio para Windows que reúne en una sola biblioteca los juegos instalados de Steam, Epic Games, Riot Games y fuentes locales o emuladores.

## Funciones

- Biblioteca unificada con portadas y filtros por plataforma
- Detección automática de Steam, Epic Games, Riot, juegos locales y emuladores
- Lanzamiento mediante los protocolos oficiales de las tiendas
- Biblioteca personal con favoritos, juegos ocultos, carátulas personalizadas y estadísticas
- Ficha integrada en la biblioteca con tiempo jugado y descripción de Steam/Wikipedia en solo lectura
- Horas de Steam sincronizadas desde la caché local del cliente y mostradas en tarjetas, ficha y estadísticas
- Horas separadas por cada perfil de Steam guardado en el equipo, con el nombre de cuenta que Steam informa
- Para League of Legends, Games guarda tiempo por cuenta y actualiza nivel/rango mediante Riot API; detecta cuentas al iniciar sesión con Riot Client abierto. Configura la clave en Ajustes → Configurar datos online de Riot. Solo se consultan Riot Games API oficiales y la clave se cifra con DPAPI
- Administra u oculta cuentas detectadas desde Ajustes → Cuentas de Riot; las cuentas ocultas se pueden restaurar
- Animación de carga acorde al tema y transiciones suaves en las tarjetas
- Temas oscuro, blanco y rojo/negro
- Fondo ambiental iluminado por la portada del juego seleccionado
- Configuración de emuladores y escaneo de carpetas de ROM/ISO
- Interfaz nativa de Windows con ejecutable único portable

## Uso

1. Ejecuta `Games.exe`.
2. Haz clic en una tarjeta para abrir los detalles; usa **Jugar** en esa ficha para iniciar el juego. El clic derecho conserva las acciones rápidas.
3. Usa **Añadir juego** para registrar un ejecutable local o una ROM asociada a un emulador configurado.
4. Abre **Ajustes > Emuladores** para configurar el ejecutable, la carpeta de ROM/ISO, las extensiones y los argumentos. Usa `{rom}` donde debe insertarse la ruta de cada juego.
5. Cambia la apariencia desde **Ajustes > Tema**.

Las descripciones consultadas se almacenan en la biblioteca local para futuras consultas. La configuración, la biblioteca y las portadas se guardan dentro de las carpetas `data` y `covers`, junto al ejecutable.

## Compilar

Requisitos:

- Windows
- .NET Framework 4.0 o superior
- `csc.exe` disponible en el Framework de Windows

Desde PowerShell:

```powershell
.\build.ps1
```

Para compilar y abrir la aplicación:

```powershell
.\build.ps1 -Run
```

Para crear también un acceso directo en el escritorio:

```powershell
.\build.ps1 -Shortcut
```
