# Cyberpunk · EDGERUNNER Nightfall

Una aplicación portable que convierte el escritorio de Windows 11 en una ciudad cyberpunk con lluvia animada, reloj y paneles con datos reales del equipo.

![Vista previa de la interfaz](docs/vista-previa.png)

## Características

- Fondo panorámico integrado y lluvia animada.
- Reloj con segundos y fecha local.
- CPU global, RAM usada y total, disco del sistema, batería y estado de red.
- Tres colores de interfaz: Edgerunners, Netrunner y Arasaka.
- Controles para ocultar el reloj y los datos, detener la lluvia y pausar la animación al usar batería.
- Pausa de la lluvia cuando una aplicación ocupa toda la pantalla principal.
- Inicio automático opcional al entrar a la sesión de Windows.
- Menú en la bandeja del sistema con ajustes y accesos al Explorador, navegador y configuración.

## Cómo usarlo

1. Descarga este repositorio desde **Code → Download ZIP** y descomprímelo.
2. Abre **`portable/EDGERUNNER.exe`**.
3. Revisa la vista previa y pulsa **Activar en mi escritorio**.
4. Para personalizarlo, haz doble clic en el icono **E** de la bandeja, junto al reloj de Windows. Puede estar dentro de los iconos ocultos.
5. Si quieres que aparezca al encender la laptop, activa **Iniciar con Windows** en los ajustes.

Conserva el ejecutable en la misma ubicación si activas el inicio automático. Si lo mueves, desactiva y vuelve a activar esa opción desde su nueva ubicación.

Para cerrar la aplicación, usa **Salir y restaurar escritorio** en el menú del icono E. El fondo anterior vuelve a verse. Para impedir futuros inicios, desmarca también **Iniciar con Windows**.

### Requisitos

- Windows 11 de 64 bits.
- .NET Framework 4.8 y WPF, componentes presentes en el equipo donde se desarrolló y probó.

No requiere Rainmeter, Lively Wallpaper, Node.js, Python ni un servidor. El ejecutable contiene la imagen, el icono y el diseño. No necesita permisos de administrador.

## Tecnologías y funcionamiento

| Componente | Función |
| --- | --- |
| C# | Lógica de la aplicación, ajustes y métricas |
| WPF / XAML | Composición de la interfaz y vista previa |
| Windows Forms / GDI | Superficie de dibujo del escritorio y bandeja del sistema |
| Win32 | Integración con Explorer y lectura de CPU y memoria |
| XML | Preferencias locales |

WPF compone la interfaz. Para mostrarla en el escritorio, una superficie nativa GDI se integra con las ventanas de Explorer, debajo de los iconos. La lluvia se dibuja por separado del contenido que se actualiza cada segundo.

La integración contempla la estructura de capas de Windows 11 24H2 y posteriores. La primera implementación podía crear una ventana que existía, pero quedaba oculta; la versión incluida conecta explícitamente la superficie al escritorio después de que Windows Forms termina de abrirla.

## Estructura

```text
Cyberpunk/
├── portable/
│   └── EDGERUNNER.exe      # Aplicación lista para ejecutar
├── source/
│   ├── Edgerunner.cs       # Aplicación e integración nativa
│   ├── Interface.xaml     # Diseño de la interfaz
│   ├── night-city.png     # Fondo integrado
│   ├── edgerunner.ico     # Icono de la aplicación
│   ├── app.manifest       # Compatibilidad, DPI y permisos
│   └── Build.ps1          # Compilación local
└── docs/
    └── vista-previa.png
```

## Compilar

Desde la carpeta del repositorio, ejecuta en PowerShell:

```powershell
& .\source\Build.ps1
```

El script usa el compilador de .NET Framework de Windows y genera **`EDGERUNNER.exe`** en la raíz. No modifica la copia de `portable/` automáticamente.

Para comprobar el renderizado, las métricas y el guardado de preferencias:

```powershell
Start-Process -FilePath .\EDGERUNNER.exe -ArgumentList '--self-test' -Wait
```

La prueba escribe una imagen y un informe en `%LOCALAPPDATA%\EdgerunnerOS`. Ese render permite revisar la interfaz, pero la visibilidad final debe comprobarse en el escritorio de Windows.

## Datos y configuración

Las preferencias y el registro de errores se guardan localmente en `%LOCALAPPDATA%\EdgerunnerOS`. El inicio automático opcional crea una entrada de inicio para el usuario actual. La aplicación no transmite las métricas a un servidor.

**Red conectada** indica que Windows detecta una interfaz de red disponible; no es una prueba de acceso a Internet. El fondo es una ilustración con lluvia superpuesta, no una escena 3D ni un vídeo.

## Estado y límites

Probado en una laptop con Windows 11, monitor de 1920 × 1080 y escala del 125%. Se comprobaron el ejecutable aislado, las métricas, los ajustes, el cierre y la visibilidad real del fondo tras corregir la integración con Explorer.

- Se aplica al monitor principal.
- Conserva los iconos y la barra de tareas de Windows.
- El inicio automático ocurre después de iniciar sesión; no cambia la pantalla de bloqueo.
- Una actualización de Explorer puede requerir ajustes en la integración nativa.
- El ejecutable no tiene firma comercial.

Proyecto personal inspirado en la estética cyberpunk, sin afiliación oficial con Cyberpunk 2077 o Cyberpunk: Edgerunners.
