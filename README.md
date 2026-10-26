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
- `MainMenuWindow.xaml(.cs)`: recibe un `Perfil`. En el inicio muestra el saludo, el nivel, la racha y el progreso dentro del nivel (con las cantidades reales de cada nivel, ver `Niveles.cs`), y los tiles de las secciones. Al entrar a una sección aparece la barra lateral. Teclas: 1-6 desde el inicio, Ctrl+1..6 dentro de una sección, Esc vuelve un paso y F1 abre la guía de uso (también el botón "i", al lado de la racha).
- `Perfil.cs`: Id, Nombre, Usuario, PasswordHash, PasswordSalt, NivelDesbloqueado (1 a 4; `Nivel` es su nombre), Racha, MejorRacha, SenasDominadas, Puntos, AciertosTotales, FallosTotales y FechaRegistro.
- **Persistencia en SQLite** (paquete NuGet `Microsoft.Data.Sqlite`, sin Entity Framework). La base es `handup.db`, junto al ejecutable.
  - `Database.cs` (estática): `Inicializar()` se llama en `App.Application_Startup`. Crea las tablas si no existen. Si el archivo está corrupto, lo renombra a `handup.corrupto-<fecha>.db` y crea uno nuevo. Si la tabla Perfiles está vacía, migra el viejo `perfiles.json` (y lo renombra a `.migrado`) o siembra 3 perfiles de ejemplo (usuarios avril, juan y lucia; sus contraseñas están en `PerfilesDeEjemplo()` de `Database.cs` y cumplen `PoliticaPassword`). También expone `Ejecutar<T>(Func<SqliteConnection, T>)`, que abre la conexión y convierte cada `SqliteException` en `DatabaseException`.
  - `IRepositorio<T>`: interfaz genérica con `ObtenerTodos`, `BuscarPorId`, `Agregar` y `Actualizar`. Los repositorios de los demás módulos (por ejemplo, señas) tienen que implementarla.
  - `RepositorioPerfiles.cs` (se instancia con `new`): implementa `IRepositorio<Perfil>` y suma `BuscarPorUsuario()` (sin distinguir mayúsculas; lanza `UserNotFoundException`), `Registrar()` (valida y lanza `InvalidInputException`; la contraseña se valida con `PoliticaPassword`: 8 caracteres o más, al menos una mayúscula y sin espacios. Esas mismas reglas alimentan el checklist en vivo de `RegisterWindow`) e `IniciarSesion()` (lanza `InvalidInputException("Contraseña incorrecta.")`). `Actualizar()` es el método pensado para la story 4.
  - Tablas del banco de señas (se crean en `CrearTablas()`): `Categorias` (Id TEXT, p. ej. "alimentos"; Nombre, Emoji, Descripcion, Color, Orden) y `Senas` (Id INTEGER = número del código, p. ej. `sign_036` → 36; Codigo, Palabra, CategoriaId, Dificultad 1-4, Descripcion, Instrucciones (un paso por línea, NULL si no hay), RutaImagen, RutaVideo; las rutas son relativas al .exe). También `Metadatos` (Clave/Valor).
  - `ImportadorSenas.cs` (estática): `Sincronizar()` se llama en `App.Application_Startup` después de `Database.Inicializar()`. Lee `signs_database.json` y, si su huella SHA-256 difiere de la guardada en `Metadatos` (o no hay señas), recarga Categorias y Senas en una transacción. Así, cuando se corrige el JSON, se actualiza solo sin tocar los perfiles. Descarta las señas inválidas (sin palabra, con una categoría inexistente, dificultad fuera de 1-4 o sin imagen). Si el JSON falta con la base vacía, está corrupto o no tiene señas válidas, lanza `BancoSenasException`: la app muestra un aviso y sigue funcionando (si ya había señas, se conservan).
  - `PasswordHasher.cs`: hashea las contraseñas con PBKDF2-SHA256 y sal. Nunca se guardan en texto plano.
- Excepciones propias: todas heredan de `HandUpException` (abstracta, en `HandUpException.cs`), así las ventanas atrapan cualquier error del dominio con un solo `catch (HandUpException ex)` y los errores inesperados del sistema no se esconden. Cuando hace falta un mensaje distinto para un caso puntual, se atrapa primero la excepción concreta (por ejemplo `InvalidInputException` en Mi perfil) y después `HandUpException`.
- `RegistroErrores.cs` (estática): anota los errores en `errores.log`, junto al ejecutable (al pasar 1 MB, lo anterior queda en `errores.log.old`). Lo usan los `catch` que siguen sin interrumpir al usuario (por ejemplo, si no se pudieron guardar las preferencias) y el controlador global de `App.xaml.cs`: los errores que ninguna ventana atrapó se registran, se avisa con un mensaje entendible y la app sigue abierta. El único `catch` que no registra es el de `Database.cs` que ignora "la columna ya existe" al migrar, porque no es un error.
- Para inspeccionar la base a mano: `C:\sqlite\sqlite3.exe bin\Debug\net8.0-windows10.0.19041.0\handup.db` (también sirve DB Browser for SQLite).
- `Assets/logo.jpg`: el logo, incluido en el csproj como `Resource`.

### Banco multimedia LSA

- **Archivos:** `Assets/signs/<categoria>/` (1.328 archivos: 688 imágenes + 640 videos, ~182 MB) y `signs_database.json` en la raíz del proyecto. En el csproj van como `Content` con `CopyToOutputDirectory=PreserveNewest` (no como `Resource`), así que quedan junto al .exe en `bin\...\Assets\signs\` y `bin\...\signs_database.json`. Las rutas del JSON son relativas al ejecutable (`assets/signs/...`; Windows no distingue mayúsculas).
- **Nombres:** dentro de cada categoría, la imagen (`.png` o `.jpeg`) y el video (`.mp4`) tienen el mismo nombre (minúsculas, sin tildes, guiones bajos). Ej.: `alimentos/dulce_de_leche.png` + `.mp4`.
- **Videos:** tienen que ser MP4 "normales". El 2026-10-16 se encontró que 521 venían como MP4 fragmentados (DASH), que el `MediaElement` de WPF no reproduce (los da por terminados al instante), y se convirtieron sin recodificar con `ffmpeg -i entrada.mp4 -c copy -movflags +faststart salida.mp4`. Si se suman videos nuevos, revisar que no vengan fragmentados.
- **JSON:** `categories` (24 entradas, todas con señas: `id`, `name`, `emoji`, `description`, `color`) y `signs` (640: `id` "sign_001"…, `word`, `category`, `difficulty` 1-4, `description`, `media.image_static`, `media.video`; además traen `instructions` (lista de pasos), `tags`, `handshape`, `movement`, `location`, `related_signs` y `examples`). Las 640 señas tienen video y ninguna ruta está rota (`media.thumbnail` = `media.image_static`).
- **Niveles:** 1 Principiante (145 señas): primeras_frases, abecedario, numeros, saludos, colores, familia. 2 Básico (211): acciones_tiempo, alimentos, ropa, clima, deportes. 3 Intermedio (174): tiempo, escuela, verbos_cotidianos, juegos, emociones, lugares, transporte. 4 Avanzado (110): paises, provincias, profesiones, medios_comunicacion, avanzado, frases_comunes. Cada categoría tiene un único nivel. Las cantidades no están fijas en el código: `Niveles.cs` las cuenta desde la base.
- **Abecedario:** A-Z + CH + Ñ (28 letras justas; no hay LL ni RR, así que para el deletreo hay que partir LL/RR en letras sueltas). La entrada "ABECEDARIO" se movió a `escuela`. Las 48 imágenes `handshape_XX_*.png` del dataset LSA16 se borraron el 2026-10-17 porque la app no las usaba.
- **Descripciones:** las señas tienen una `description` real del movimiento de manos (story 13), todas revisadas por el equipo (el 2026-10-01 se sacaron las 14 marcas "(revisar)" que quedaban).
- **Sistematización LSA:** cada seña tiene además `sistematizacion` con los parámetros que se usan en Argentina para sistematizar una seña: `configuracion` (con el nombre de la configuración según el dactilológico y los números: A, B, B curva, 5, 5 curva o garra, C, O, montoncito, 1, L, V, U, Y, F, pinza...), `ubicacion`, `orientacion`, `movimiento`, `direccion` (no va en las estáticas) y `rasgo_no_manual` (solo si tiene). Se escribió el 2026-10-26 a partir de `description`, así que conviene revisarla contra los videos. `ImportadorSenas` la guarda en la columna `Senas.Sistematizacion` ("Parámetro: valor", uno por línea), `Sena.ParametrosLsa` la separa, y Aprender y el Diccionario la muestran arriba de "Cómo se hace".
- **Detalles a tener en cuenta:**
  - En 375 señas, `handshape`/`movement`/`location` son texto de relleno ("Ver imagen oficial CAS"), así que no conviene mostrarlos. 56 señas no tienen `instructions`: mostrarlas solo si existen.
  - Hay homónimos: NARANJA (alimentos/colores), PAPA/PAPÁ (alimentos/familia), UNO (juegos/numeros), MERIENDA (alimentos/tiempo) y MAÑANA (día siguiente/parte del día). El Traductor muestra primero la que coincide con las tildes escritas ("papá" → familia, "papa" → comida) y, si no, la del nivel más básico; el usuario puede cambiarla.
  - `numeros` incluye una entrada "NUMEROS".
  - Las mayúsculas de `word` son inconsistentes ("MIL", "Cinco"): normalizar al mostrar y al comparar (también las tildes).
  - Cada seña guarda de dónde salió en `source`. Las fuentes están en la sección "Fuentes" de este README y en la guía de uso. Las 640 tienen `source`.

### Qué está hecho

- **Usuarios (stories 1 a 4):** registro, login, carga segura y progreso automático. `ServicioProgreso` guarda cada acierto o fallo (dispara el evento `ProgresoActualizado`) y registra cada seña en la tabla `ProgresoSenas`. El nivel ya no sube por dominar señas: es el `NivelDesbloqueado` del perfil y solo va a subir aprobando la prueba final. La barra de progreso cuenta las señas dominadas del nivel actual desde `ProgresoSenas`.
- **Mi perfil** (`PerfilView`): datos, avance en el nivel, estadísticas y estadísticas por categoría (`EstadisticaCategoria`, con "Practicar" para las más flojas). En "Cuenta": editar el nombre, cambiar la contraseña, cerrar sesión, reiniciar el progreso y eliminar el perfil (las dos últimas con `DialogoConfirmacion`).
- **Aprender** (`AprenderView` → `ElegirNivelView` → `ElegirCategoriaView` → `RecorrerSenasView` → `PracticaView`): stories 5 a 10. Polimorfismo: `Sena` (`SenaEstatica` / `SenaAnimada`) y `Pregunta` (`PreguntaOpcionMultiple` / `PreguntaTipeo`). `RepositorioSenas : IRepositorio<Sena>` lee el banco; `GeneradorPreguntas` arma las preguntas. Volver un paso (Esc o el botón Volver) usa el historial genérico `HistorialNavegacion<T>` (una pila): al avanzar se guarda el paso que se deja, con el texto del botón Volver y cómo restaurarlo, y volver saca el último. La práctica es parte del paso 3, así que desde ahí se vuelve a las categorías; si se llegó desde "Practicar" en Mi perfil, el único paso guardado es Mi perfil. Las teclas de `PracticaView` se despachan con una tabla de eventos (`Dictionary<Key, Func<bool>>`) armada con las teclas que el usuario eligió en Configuración: el `PreviewKeyDown` busca la tecla y llama a su función, que devuelve si la atendió (si no, la tecla sigue su camino, como en `DefWindowProc` de Win32).
- **Visor de señas** (`VisorSena`): imagen o video con Pausar, Repetir y Más lento, y pantalla completa (`VisorPantallaCompletaWindow`).
- **Diccionario** (`DiccionarioView`): stories 11, 12 y 13.
- **Traductor** (`Traductor`, `TraductorView`): stories 14 y 15. Usa un índice genérico `IndiceDeBusqueda<T>`, deletrea con `DispatcherTimer`, tiene pantalla completa (`TraductorPantallaCompletaWindow`) y exporta la traducción como MP4 (`ExportadorVideo`, `PanelVideoTraduccion`, `ExportacionException`). En pantalla se llama **Intérprete** (todos los textos dicen "interpretación", no "traducción") y muestra el aviso "Esta interpretación es aproximada y no es 100% verídica" (también en la pantalla completa y en el video exportado). Las clases conservan el nombre `Traductor*`.
- **Configuración** (`ConfiguracionView`): alto contraste, tamaño del texto (Normal / Grande / Muy grande, `TamanoTexto`) y reasignación de atajos, guardados por perfil (`RepositorioPreferencias`).
- **Barra lateral achicable:** con la flecha de arriba o Ctrl+B queda solo con íconos, y se recuerda por perfil.
- **Guía de uso** (`GuiaView`, `GuiaUso`, `GuiaException`): se abre con el botón "i" del inicio, con "Guía" en la barra lateral o con F1. Tiene un índice de secciones, y cada una muestra cómo se usa, sus atajos y capturas de la app. El contenido está en `ProyectoFinal/Guia/guia.json` y las capturas en `ProyectoFinal/Guia/capturas/`. El botón "Abrir PDF" abre `Presentacion/HandUp - Guia de uso.pdf`, el PDF para la presentación, que se arma desde el mismo JSON con `Presentacion/generar-guia.ps1` (lo imprime Edge). Si se cambia la guía, hay que volver a correr ese script. La última sección, "Fuentes del material", lista de dónde sale el material; usa `tituloPasos` para que la lista se titule "Fuentes" en vez de "Cómo se usa".
- **Banco de frases** (`frases.json`, `BancoFrases`, `Frase`, `BancoFrasesException`): plantillas con huecos (`yo comer {comida}` → "Yo como {comida}.") y listas para completarlos. 96 plantillas (entre 22 y 26 por nivel) arman 1933 frases. Como cada prueba usa 12 plantillas, los tipos de frase cambian de un intento a otro. Al cargarse, cada frase se valida contra el banco de señas: todas sus señas existen (o se deletrean, si llevan `#`), ninguna es de un nivel mayor y al menos una es del nivel de la frase. Las inválidas se descartan y quedan en `BancoFrases.Avisos`.
- **Prueba final con frases:** 12 frases y 8 palabras (`PruebaFinal.ElegirFrases`, repartidas entre plantillas). La frase se reproduce seña por seña con `ReproductorSecuencia` (la misma lógica que el Traductor), con "Repetir frase" (R) y pausa. Se responde con `PreguntaFrase` entre 4 traducciones: las incorrectas son de la misma plantilla y cambian la menor cantidad de palabras posible (`GeneradorPreguntas.CrearFrase`). El resultado muestra palabras y frases por separado. Si el banco de frases no se puede cargar, la prueba sale solo con palabras y lo avisa.
- **Examen final y certificado:** la prueba de Avanzado es el examen final de HandUp (`PruebaFinal.EsExamenFinal`). No desbloquea nada: aprobarlo con 80% o más por primera vez completa HandUp. `ServicioProgreso` dispara el evento `HandUpCompletado`, y el menú abre `FelicitacionesWindow`, con la vista previa del certificado y "Descargar certificado". El certificado (`CertificadoVisual`, `CertificadoHandUp`) es un A4 horizontal con la estética de la app: nombre, porcentaje, fecha en que lo aprobó (columna `FechaAprobada` de `PruebasFinales`) y número (`HU-AAAAMMDD-perfil`). `CertificadoExportador` lo guarda como PDF (escrito a mano, sin librerías) o PNG. Después queda en Mi perfil, con "Ver certificado".
- **Racha por días** (`Perfil.RegistrarDiaDePractica`, `RachaDiasVigente`): además de los aciertos seguidos, cuenta los días seguidos con al menos una respuesta. Se corta si pasa un día sin practicar y guarda el récord (columnas `RachaDias`, `MejorRachaDias` y `UltimoDiaPractica`). Se ve en el inicio y en las estadísticas de Mi perfil.
- **Guía la primera vez:** la guía de uso se abre sola, con un mensaje de bienvenida, la primera vez que entra cada perfil (`Preferencias.GuiaVista`).
- **Ventana principal redimensionable:** se puede agrandar o maximizar. El mínimo es el tamaño base agrandado según el tamaño del texto.
- **Ortografía del banco:** 69 palabras de `signs_database.json` pasaron a tener tilde ("Tío", "Café", "Sábado", "Té"…), y "INTERPETE" ahora es "INTÉRPRETE". Las 18 con género pasaron de "ABUELO A" a "ABUELO/A". Los ids y los archivos no cambiaron.
- **Visor de señas en lugares angostos:** si los botones Pausar, Repetir y Más lento no entran (por ejemplo en la ficha del Diccionario o en el Desafío), se muestran solo con el ícono, con tooltip y nombre accesible.
- **Desafío** (`DesafioView`, `PartidaDesafioView`, `PartidaDesafio`): stories 16 a 19. Se elige nivel (los bloqueados con candado) y categoría; "Empezar partida" está deshabilitado hasta elegir una. Partida de 90 segundos con 3 vidas (`DispatcherTimer`); la barra del tiempo es `BarraTiempo`, un control que se dibuja solo con `OnRender` (el evento Paint de WPF) y se redibuja al cambiar el tiempo o el color, 10 puntos por acierto (+5 con racha de 3 o más), racha en pantalla y resultado final. Repetición espaciada con la cola genérica `ColaRepeticionEspaciada<T>`: las señas falladas vuelven enseguida y las que más se fallaron (según `ProgresoSenas`) aparecen más seguido. Si la categoría no tiene señas, `PartidaDesafio` lanza `MazoVacioException` apenas se crea (se valida antes de empezar). La cola es genérica y no sabe de señas: si se le pide un elemento estando vacía, lanza `InvalidOperationException`, como `Queue<T>`.

### Qué falta

- **Sistema de niveles:** la fase 0 está hecha (nivel desbloqueado, tabla `PruebasFinales` y migración que conserva el nivel de cada perfil). La fase 1 también: en Elegir nivel, los niveles de arriba se ven con candado y al elegirlos se avisa qué hay que aprobar. La fase 2 también: desde Elegir nivel se rinde la prueba final del nivel actual (tecla P), con 20 señas repartidas entre sus categorías y sin homónimos. Con 80% o más se desbloquea el siguiente (`ServicioProgreso.RegistrarPruebaFinal`, evento `NivelDesbloqueado`), y se guarda el mejor resultado en `PruebasFinales`. La fase 3 también: quien se registra eligiendo un nivel mayor arranca en Principiante con la nivelación pendiente (`Perfil.NivelacionPendiente`). Al aprobar la prueba final del nivel anterior pasa directo al elegido; se puede rendir al registrarse ("Rendir ahora"), desde Elegir nivel o desde Mi perfil, o dejarla de lado ("Empezar como Principiante"). La fase 4 también: la prueba final tiene 12 frases y 8 palabras (ver "Banco de frases" más arriba). El plan está en `PLAN_NIVELES.md`.

> Nota: esta sección refleja el estado al 2026-10-17. Actualizala a medida que se avance.

## Fuentes

Sale del campo `source` de `signs_database.json` (contado el 2026-10-20). Las 640 señas tienen fuente. Es la misma lista que la sección "Fuentes del material" de la guía; acá van además los ids de YouTube de los clips, para el informe.

- **Señario, diccionario de LSA:** 399 señas (249 de la versión 2 y 150 de la versión 1). Es la base del banco: videos, imágenes y descripciones.
- **LSA Database:** 164 señas, grabadas en Full HD frente a una pared blanca. Parte son del dataset LSA64: Ronchetti, F., Quiroga, F., Estrebou, C., Lanzarini, L. y Rosete, A. (2016). *LSA64: An Argentinian Sign Language Dataset*. III-LIDI, Facultad de Informática, UNLP. LSA64 tiene 64 señas, así que no todas las de LSA Database pueden venir de ahí.
- **LSA Oficial (YouTube):** 40 señas.
- **Clips de YouTube** (recortados y con el texto sobreimpreso tapado para las preguntas):
  - Signs of Love: Vocabulario de los saludos (`_muAHDVEItk`), de los verbos (`SHB1Rlg6uvM`), Categorías de verbos (`1ICc2IZuy6A`), de los lugares (`BD7h2yxvKwo`), del hogar (`WIV9V42FeQg`) y del transporte (`gPXlc7IaYYM`). 15 señas.
  - Dirección de Documentación: Pronombres en LSA (`iPKG67lXuoc`) y Verbos en LSA (`qo-uIEi3ko8`). 4 señas.
  - Prof. Fabián Gervasoni: LSA preguntas y pronombres (`3dtU2H8X_lA`) y Adverbios de cantidad (`xxZXycZMkyk`). 4 señas.
  - Adjetivos en LSA Parte I (`vBSQD1A3OIg`). 4 señas.
  - ULSA: Listado de verbos en LSA (12) (`1XW6tOX3Y1Y`) y Preguntas en LSA (`W0M9GY_1EgI`). 3 señas.
  - Maxi Villarruel: LSA Lugares (`W18Q4ZqWBbo`), Palabras varias 4 (`9FbH1aIYMoc`) y Palabras varias 9 (`SJrDqJA5E_E`). 3 señas.
  - LSA con Tere: Adjetivos en LSA (`RH0ndn0_DMA`). 2 señas.
  - Fundación Effata: Medios de transporte en LSA (`IiJmufvvX-c`). 1 seña.
  - Capacitación en LSA: Clase 4, Familia (`feV-ra7rvPA`). 1 seña.

Los ids de YouTube salen de `fix_clips.py`, el script que recortó y tapó los clips (lo tiene Lorenzo; falta subirlo al repo).

## Pendientes del equipo

- Definir la fecha límite real (Sprint 0 sigue sin fecha).
- Repartir los 5 módulos entre los 2 o 3 integrantes.
- Citar las fuentes en el informe (la lista está en "Fuentes").
- Subir `fix_clips.py` al repo.
- Confirmar qué señas de LSA Database son del dataset LSA64.

## Cómo trabajar

- Código C#/WPF claro, con comentarios en español y del nivel de la materia (sin frameworks externos ni MVVM complejo, salvo que se pida).
- Mantener el estilo visual existente y reutilizar los recursos de `App.xaml`.
- Todo tiene que poder usarse con el teclado y manejar los errores con excepciones propias.
- Aprovechar la programación genérica (por ejemplo, un `Repositorio<T>` o colecciones genéricas) y los eventos, porque son temas centrales de la materia.
- Verificar que compile sin errores ni warnings.
