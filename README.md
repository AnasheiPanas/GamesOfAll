# GamesOfAll

GamesOfAll es un lanzador de escritorio para Windows que reúne en una sola biblioteca los juegos instalados de Steam, Epic Games, Riot Games y fuentes locales o emuladores.

## Funciones

- Biblioteca unificada con portadas y filtros por plataforma
- Detección automática de Steam, Epic Games, Riot, juegos locales y emuladores
- Lanzamiento mediante los protocolos oficiales de las tiendas
- Biblioteca personal con favoritos, juegos ocultos, carátulas personalizadas y estadísticas
- Temas oscuro, blanco y rojo/negro
- Fondo ambiental iluminado por la portada del juego seleccionado
- Configuración de emuladores y escaneo de carpetas de ROM/ISO
- Interfaz nativa de Windows con ejecutable único portable

## Uso

1. Ejecuta `GamesOfAll.exe`.
2. Usa **Añadir juego** para registrar un ejecutable local o un juego emulado.
3. Abre **Ajustes > Emuladores** para configurar un emulador, su carpeta de ROM/ISO y sus argumentos.
4. Cambia la apariencia desde **Ajustes > Tema**.

La configuración, la biblioteca y las portadas se guardan dentro de las carpetas `data` y `covers`, junto al ejecutable.

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
