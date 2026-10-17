# Contexto: proyecto HandUp (PGE)

Proyecto final de la materia **Programación Genérica y Eventos (PGE)**. Respondé siempre en español (rioplatense está bien).

## ¿Qué es HandUp?

Una aplicación de escritorio para aprender **Lengua de Señas Argentina (LSA)** de forma interactiva y gamificada. Cada persona la usa por su cuenta, sin profesor ni administrador y sin conexión a internet. Hay un solo tipo de usuario (no hay roles docente/estudiante) y cada uno gestiona únicamente su propio progreso.

**Objetivo general:** una app que enseñe señas paso a paso, permita ponerse a prueba en un modo desafío, traduzca texto a señas y tenga un diccionario visual. Todo tiene que ser 100% accesible y aplicar en serio los temas de PGE: programación genérica, eventos, timers, excepciones y GUI.

**Objetivos específicos:**
- OE1: sistema de usuarios con persistencia (login, perfiles, progreso) y manejo seguro de archivos.
- OE2: aprendizaje progresivo por categorías y niveles, con render de señas estáticas y animadas.
- OE3: evaluación gamificada con timer, vidas, puntaje y repetición espaciada.
- OE4: traductor inverso texto→seña, con deletreo automático si la palabra no existe.
- OE5: diccionario visual con buscador en tiempo real.
- OE6: programación genérica, eventos, excepciones y accesibilidad aplicadas en todos los módulos.

## Los 5 módulos

1. **Usuarios y Persistencia:** registro, login y selección de perfil. El progreso (nivel, señas dominadas, estadísticas, mejor racha) se guarda automáticamente en un archivo. La app no debe crashear si el archivo no existe o está corrupto.
2. **Aprendizaje por Niveles y Categorías:** 22 categorías temáticas y 4 niveles de dificultad (ver "Banco multimedia LSA" más abajo). Cada seña se muestra como imagen (estática) o video `.mp4` (animada). Se responde por opción múltiple (atajos de teclado 1-4) o tipeando la palabra.
3. **Modo Desafío:** partida contrarreloj con vidas y puntaje. Cuenta la racha de aciertos consecutivos. Usa repetición espaciada: las señas que más se fallan aparecen más seguido.
4. **Traductor Inverso:** escribís una palabra o frase. Si existe en el diccionario, se reproduce su seña. Si no existe (por ejemplo, un nombre propio), se deletrea letra por letra con un intervalo temporizado.
5. **Diccionario Visual:** buscador que filtra mientras escribís. Filtros combinables por tipo (estática/animada) y por categoría. Cada seña tiene una descripción textual del movimiento de manos.

**En todos los módulos:** accesibilidad (se puede usar completa con el teclado y no depende del audio) y excepciones propias ante datos inválidos o faltantes.

## Polimorfismo (lo pide la materia)

Como no hay roles de usuario, el polimorfismo va en:
- **Seña**: `SeñaEstatica` y `SeñaAnimada`. Las dos tienen `Reproducir()`, cada una con un comportamiento distinto. **Criterio (decidido):** las categorías `abecedario` y `numeros` son `SeñaEstatica` (se muestra la imagen); todas las demás son `SeñaAnimada` (se reproduce el video `.mp4`). Hoy todas las señas tienen video, pero si a una animada le faltara, cae a su imagen estática.
- **Pregunta**: `PreguntaOpcionMultiple` y `PreguntaTipeo`. Las dos tienen `EvaluarRespuesta()`, cada una con un comportamiento distinto.

## Organización (Scrum simplificado en Trello)

Board: https://trello.com/b/NYLEBkoi/handup-proyecto-pge
Listas: Objetivo del Proyecto, Módulos del Equipo, Reglas del Equipo (DoR/DoD), Product Backlog (19 user stories con criterios de aceptación y checklist técnico), Sprint 0 ("Armar el Backlog", ya hecho) y Hecho.

**Definition of Done** (aplica a todas las stories): compila sin errores ni warnings; se probó a mano contra los criterios de aceptación; maneja los errores con excepciones propias y no crashea; se puede usar 100% con el teclado; otro integrante lo revisó; el progreso se guarda y se recupera bien; el checklist técnico quedó marcado.

## Las 19 user stories (en orden de prioridad, respetando dependencias)

Usuarios:
1. Registrarme y crear mi perfil.
2. Iniciar sesión y seleccionar mi perfil.
3. Que mis datos se carguen de forma segura, sin corromperse.
4. Que mi progreso se guarde automáticamente.

Aprendizaje:
5. Ver la imagen o animación de cada seña. **Es la clave técnica:** acá se resuelve el motor de renderizado que reutilizan los demás módulos.
6. Elegir una categoría temática (22).
7. Elegir un nivel de dificultad (Principiante, Básico, Intermedio, Avanzado).
8. Responder por opción múltiple con atajos de teclado 1-4.
9. Responder tipeando la palabra.
10. Que el botón Confirmar esté deshabilitado hasta elegir una opción.

Diccionario:
11. Buscar una seña en tiempo real mientras escribo.
12. Filtrar los resultados por tipo de seña y por categoría.
13. Ver la descripción del movimiento de manos de cada seña.

Traductor:
14. Escribir una palabra o frase y ver su traducción a señas.
15. Que si la palabra no está en el diccionario se deletree letra por letra.

Desafío:
16. Jugar una partida contrarreloj con vidas.
17. Que el botón Empezar esté deshabilitado hasta elegir una categoría.
18. Acumular una racha de aciertos consecutivos.
19. Que las señas que fallo aparezcan más seguido (repetición espaciada).

## Stack y estado actual del código

**Stack actual: WPF con .NET 8 (C#).** El proyecto se pensó primero en Win32/C++ y después se habló de WinForms, pero la base de código que existe hoy es **WPF**. Para timers usar `DispatcherTimer`; para mostrar señas estáticas, `Image`/`BitmapImage`, y para las animadas, `MediaElement` (video `.mp4`, sin audio necesario). Ya no se usan animaciones cuadro a cuadro. Para exportar la traducción como video se usa el editor de video que trae Windows (`Windows.Media.Editing`), sin programas externos.

**Ubicación:** `ProyectoFinal\` (dentro de esta carpeta).
Solución: `ProyectoFinal.sln`, proyecto `ProyectoFinal.csproj` (`net8.0-windows10.0.19041.0`, UseWPF, Nullable enable; requiere Windows 10 2004 o superior). Namespace: `ProyectoFinal`. La app compila en `bin\Debug\net8.0-windows10.0.19041.0\` (ahí quedan el `.exe`, `handup.db` y los assets).

**Archivos existentes:**
- `App.xaml`: recursos globales de estilo. Fuente `FontHeading`. Brushes `BrushBackground`, `BrushCard`, `BrushBorder`, `BrushBorderAccent`, `BrushInk`, `BrushMuted`, `BrushAccent`, `BrushAccentDark`, `BrushAccentSoft`, `BrushError`, `BrushErrorBg`, `BrushSuccess` y `BrushSuccessBg`. Efecto `CardShadow`. Estilos `FieldLabel`, `RoundedTextBox`, `RoundedPasswordBox`, `PrimaryButton`, `SecondaryButton`, `LinkButton` y `ProfileChipItem`. La paleta es verde azulado sobre fondo claro.
- `LoginWindow.xaml(.cs)`: ventana de 940x600 con dos columnas. A la izquierda, un panel de marca con fondo `#14332B`, el logo (`Assets/logo.jpg`, una mano verde) y el título "HandUp". A la derecha, el formulario: perfiles existentes como chips en un ListBox, usuario, contraseña, un botón Iniciar sesión y el link "Creala acá" hacia el registro.
- `RegisterWindow.xaml(.cs)`: el mismo panel de marca a la izquierda y el formulario de registro a la derecha.
- `MainMenuWindow.xaml(.cs)`: recibe un `Perfil`. En el inicio muestra el saludo, el nivel, la racha y el progreso dentro del nivel (con las cantidades reales de cada nivel, ver `Niveles.cs`), y los tiles de las secciones. Al entrar a una sección aparece la barra lateral. Teclas: 1-6 desde el inicio, Ctrl+1..6 dentro de una sección, Esc vuelve un paso. Desafío todavía muestra "Próximamente".
- `Perfil.cs`: Id, Nombre, Usuario, PasswordHash, PasswordSalt, Nivel, Racha, MejorRacha, SenasDominadas, Puntos, AciertosTotales, FallosTotales y FechaRegistro.
- **Persistencia en SQLite** (paquete NuGet `Microsoft.Data.Sqlite`, sin Entity Framework). La base es `handup.db`, junto al ejecutable.
  - `Database.cs` (estática): `Inicializar()` se llama en `App.Application_Startup`. Crea las tablas si no existen. Si el archivo está corrupto, lo renombra a `handup.corrupto-<fecha>.db` y crea uno nuevo. Si la tabla Perfiles está vacía, migra el viejo `perfiles.json` (y lo renombra a `.migrado`) o siembra 3 perfiles de ejemplo (usuarios avril, juan y lucia; sus contraseñas están en `PerfilesDeEjemplo()` de `Database.cs` y cumplen `PoliticaPassword`). También expone `Ejecutar<T>(Func<SqliteConnection, T>)`, que abre la conexión y convierte cada `SqliteException` en `DatabaseException`.
  - `IRepositorio<T>`: interfaz genérica con `ObtenerTodos`, `BuscarPorId`, `Agregar` y `Actualizar`. Los repositorios de los demás módulos (por ejemplo, señas) tienen que implementarla.
  - `RepositorioPerfiles.cs` (se instancia con `new`): implementa `IRepositorio<Perfil>` y suma `BuscarPorUsuario()` (sin distinguir mayúsculas; lanza `UserNotFoundException`), `Registrar()` (valida y lanza `InvalidInputException`; la contraseña se valida con `PoliticaPassword`: 8 caracteres o más, al menos una mayúscula y sin espacios. Esas mismas reglas alimentan el checklist en vivo de `RegisterWindow`) e `IniciarSesion()` (lanza `InvalidInputException("Contraseña incorrecta.")`). `Actualizar()` es el método pensado para la story 4.
  - Tablas del banco de señas (se crean en `CrearTablas()`): `Categorias` (Id TEXT, p. ej. "alimentos"; Nombre, Emoji, Descripcion, Color, Orden) y `Senas` (Id INTEGER = número del código, p. ej. `sign_036` → 36; Codigo, Palabra, CategoriaId, Dificultad 1-4, Descripcion, Instrucciones (un paso por línea, NULL si no hay), RutaImagen, RutaVideo; las rutas son relativas al .exe). También `Metadatos` (Clave/Valor).
  - `ImportadorSenas.cs` (estática): `Sincronizar()` se llama en `App.Application_Startup` después de `Database.Inicializar()`. Lee `signs_database.json` y, si su huella SHA-256 difiere de la guardada en `Metadatos` (o no hay señas), recarga Categorias y Senas en una transacción. Así, cuando se corrige el JSON, se actualiza solo sin tocar los perfiles. Descarta las señas inválidas (sin palabra, con una categoría inexistente, dificultad fuera de 1-4 o sin imagen). Si el JSON falta con la base vacía, está corrupto o no tiene señas válidas, lanza `BancoSenasException`: la app muestra un aviso y sigue funcionando (si ya había señas, se conservan).
  - `PasswordHasher.cs`: hashea las contraseñas con PBKDF2-SHA256 y sal. Nunca se guardan en texto plano.
- `InvalidInputException.cs`, `UserNotFoundException.cs`, `DatabaseException.cs` y `BancoSenasException.cs`: excepciones propias.
- Para inspeccionar la base a mano: `C:\sqlite\sqlite3.exe bin\Debug\net8.0-windows10.0.19041.0\handup.db` (también sirve DB Browser for SQLite).
- `Assets/logo.jpg`: el logo, incluido en el csproj como `Resource`.

### Banco multimedia LSA

- **Archivos:** `Assets/signs/<categoria>/` (1.328 archivos: 688 imágenes + 640 videos, ~182 MB) y `signs_database.json` en la raíz del proyecto. En el csproj van como `Content` con `CopyToOutputDirectory=PreserveNewest` (no como `Resource`), así que quedan junto al .exe en `bin\...\Assets\signs\` y `bin\...\signs_database.json`. Las rutas del JSON son relativas al ejecutable (`assets/signs/...`; Windows no distingue mayúsculas).
- **Nombres:** dentro de cada categoría, la imagen (`.png` o `.jpeg`) y el video (`.mp4`) tienen el mismo nombre (minúsculas, sin tildes, guiones bajos). Ej.: `alimentos/dulce_de_leche.png` + `.mp4`.
- **Videos:** tienen que ser MP4 "normales". El 2026-10-16 se encontró que 521 venían como MP4 fragmentados (DASH), que el `MediaElement` de WPF no reproduce (los da por terminados al instante), y se convirtieron sin recodificar con `ffmpeg -i entrada.mp4 -c copy -movflags +faststart salida.mp4`. Si se suman videos nuevos, revisar que no vengan fragmentados.
- **JSON:** `categories` (24 entradas, todas con señas: `id`, `name`, `emoji`, `description`, `color`) y `signs` (640: `id` "sign_001"…, `word`, `category`, `difficulty` 1-4, `description`, `media.image_static`, `media.video`; además traen `instructions` (lista de pasos), `tags`, `handshape`, `movement`, `location`, `related_signs` y `examples`). Las 640 señas tienen video y ninguna ruta está rota (`media.thumbnail` = `media.image_static`).
- **Niveles:** 1 Principiante (145 señas): primeras_frases, abecedario, numeros, saludos, colores, familia. 2 Básico (211): acciones_tiempo, alimentos, ropa, clima, deportes. 3 Intermedio (174): tiempo, escuela, verbos_cotidianos, juegos, emociones, lugares, transporte. 4 Avanzado (110): paises, provincias, profesiones, medios_comunicacion, avanzado, frases_comunes. Cada categoría tiene un único nivel. Las cantidades no están fijas en el código: `Niveles.cs` las cuenta desde la base.
- **Abecedario:** A-Z + CH + Ñ (28 letras justas; no hay LL ni RR, así que para el deletreo hay que partir LL/RR en letras sueltas). La entrada "ABECEDARIO" se movió a `escuela`. Hay además 48 imágenes `handshape_XX_*.png` (dataset LSA16) que no están en el JSON.
- **Descripciones:** las señas tienen una `description` real del movimiento de manos (story 13), todas revisadas por el equipo (el 2026-10-01 se sacaron las 14 marcas "(revisar)" que quedaban).
- **Detalles a tener en cuenta:**
  - En 375 señas, `handshape`/`movement`/`location` son texto de relleno ("Ver imagen oficial CAS"), así que no conviene mostrarlos. 56 señas no tienen `instructions`: mostrarlas solo si existen.
  - Hay homónimos: NARANJA (alimentos/colores), PAPA/PAPÁ (alimentos/familia), UNO (juegos/numeros), MERIENDA (alimentos/tiempo) y MAÑANA (día siguiente/parte del día). El Traductor muestra primero la que coincide con las tildes escritas ("papá" → familia, "papa" → comida) y, si no, la del nivel más básico; el usuario puede cambiarla.
  - `numeros` incluye una entrada "NUMEROS".
  - Las mayúsculas de `word` son inconsistentes ("MIL", "Cinco"): normalizar al mostrar y al comparar (también las tildes).
  - Las fuentes (Señario V2, LSA16) se citan más adelante en el informe.

### Qué está hecho

- **Usuarios (stories 1 a 4):** registro, login, carga segura y progreso automático. `ServicioProgreso` guarda cada acierto o fallo (dispara el evento `ProgresoActualizado`) y registra cada seña en la tabla `ProgresoSenas`. El nivel puede subir pero nunca bajar (`Niveles.Actualizar`).
- **Mi perfil** (`PerfilView`): datos, avance en el nivel y estadísticas. En "Cuenta" se puede editar el nombre (vacío → `InvalidInputException`) y cerrar sesión (vuelve al login sin cerrar la app).
- **Aprender** (`AprenderView` → `ElegirNivelView` → `ElegirCategoriaView` → `RecorrerSenasView` → `PracticaView`): stories 5 a 10. Polimorfismo: `Sena` (`SenaEstatica` / `SenaAnimada`) y `Pregunta` (`PreguntaOpcionMultiple` / `PreguntaTipeo`). `RepositorioSenas : IRepositorio<Sena>` lee el banco; `GeneradorPreguntas` arma las preguntas.
- **Visor de señas** (`VisorSena`): imagen o video con Pausar, Repetir y Más lento, y pantalla completa (`VisorPantallaCompletaWindow`).
- **Diccionario** (`DiccionarioView`): stories 11, 12 y 13.
- **Traductor** (`Traductor`, `TraductorView`): stories 14 y 15. Usa un índice genérico `IndiceDeBusqueda<T>`, deletrea con `DispatcherTimer`, tiene pantalla completa (`TraductorPantallaCompletaWindow`) y exporta la traducción como MP4 (`ExportadorVideo`, `PanelVideoTraduccion`, `ExportacionException`).
- **Configuración** (`ConfiguracionView`): alto contraste y reasignación de atajos, guardados por perfil (`RepositorioPreferencias`).

### Qué falta

- **Desafío** (stories 16 a 19): contrarreloj con vidas, botón Empezar deshabilitado, racha en pantalla y repetición espaciada.
- **Sistema de niveles:** niveles bloqueados, prueba final con 80% para desbloquear, nivelación al registrarse y banco de frases. El plan está en `PLAN_NIVELES.md`.
- **Mi perfil:** cambiar contraseña, reiniciar progreso, eliminar perfil y estadísticas por categoría.
- **Accesibilidad:** agrandar el texto (la preferencia `TamanoFuente` ya existe en la base, falta el control).

> Nota: esta sección refleja el estado al 2026-10-17. Actualizala a medida que se avance.

## Pendientes del equipo

- Definir la fecha límite real (Sprint 0 sigue sin fecha).
- Repartir los 5 módulos entre los 2 o 3 integrantes.
- Citar las fuentes del material (Señario V2, LSA16) en el informe.

## Cómo trabajar

- Código C#/WPF claro, con comentarios en español y del nivel de la materia (sin frameworks externos ni MVVM complejo, salvo que se pida).
- Mantener el estilo visual existente y reutilizar los recursos de `App.xaml`.
- Todo tiene que poder usarse con el teclado y manejar los errores con excepciones propias.
- Aprovechar la programación genérica (por ejemplo, un `Repositorio<T>` o colecciones genéricas) y los eventos, porque son temas centrales de la materia.
- Verificar que compile sin errores ni warnings.
