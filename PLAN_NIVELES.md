# Plan: sistema de niveles (nivel verificado, niveles bloqueados, prueba final y nivelación)

> Propuesto el 2026-10-16. **Queda para después del Traductor** (stories 14 y 15).

## Situación de partida

Las tarjetas de Trello van en cadena:

banco de frases → **prueba final** (80% o más desbloquea) → **niveles bloqueados** → **nivelación** al registrarse → **conservar el nivel** de los usuarios que ya existen

Estado al 2026-10-16:
- **Ampliación del banco de señas: hecha.** Hay 640 señas en 24 categorías, incluidas "Primeras frases" y "Acciones y tiempo", con YO, VOS, TENER, QUERER, SÍ, NO, etc. Por nivel: 145 / 211 / 174 / 110.
- **Banco de frases: no existe** todavía (falta `frases.json`).
- Según la tarjeta, la prueba final es 60% frases, y las frases se muestran con la secuencia de señas del **Traductor** (todavía no hecho).
- Hoy el nivel se calcula según la cantidad de señas dominadas. El parche `Niveles.Actualizar` evita que baje, pero queda reemplazado en la fase 0.

## Fases

| Fase | Qué se hace | Tarjetas que cierra |
|---|---|---|
| **0. Base de datos** ✅ *(hecha el 2026-10-17)* | Se agrega a cada perfil el **nivel desbloqueado** (1 a 4) y una tabla `PruebasFinales` (PerfilId, Nivel, MejorPorcentaje). Una migración, que corre una sola vez (marca en `Metadatos`), deja a cada perfil con el nivel que ya tenía. El nivel **deja de subir por señas dominadas**: solo sube con la prueba final. La barra de progreso cuenta las señas dominadas *del nivel actual* (desde `ProgresoSenas`), lo que corrige el "0 / 174". | Conservar el nivel |
| **1. Niveles bloqueados** ✅ *(hecha el 2026-10-17, también en Desafío)* | En Elegir nivel, los niveles de arriba se ven atenuados y con candado. Al elegir uno aparece el mensaje "Aprobá la prueba final de {anterior} con 80% o más para desbloquear {nivel}". Siguen accesibles con el teclado, el lector de pantalla los anuncia como "bloqueado" y el bloqueo también aplica en Desafío. | Niveles bloqueados |
| **2. Prueba final (v1: solo palabras)** ✅ *(hecha el 2026-10-17; como ningún nivel tiene 10 categorías, las 20 señas se reparten parejo entre las que hay, en vez de "máximo 2 por categoría")* | 20 palabras del nivel, como máximo 2 por categoría, sin homónimos (NARANJA, PAPÁ, UNO, MERIENDA). Muestra el resultado; con 80% o más desbloquea el nivel siguiente y aparece una pantalla de logro. Guarda el mejor puntaje sin pisarlo con uno peor y dispara el evento `NivelDesbloqueado`, que escucha el menú. Reutiliza `GeneradorPreguntas` y la UI de Práctica. | Prueba final + desbloqueo al 80% (salvo la parte de frases) |
| **3. Nivelación** ✅ *(hecha el 2026-10-17)* | Al registrarse con un nivel mayor a Principiante aparecen "Rendir ahora" y "Empezar como Principiante", y se rinde la prueba final del nivel anterior. Si se cierra la app a la mitad, queda pendiente y se puede rendir desde Mi perfil. | Nivelación |
| **4. Frases** *(después del Traductor)* | Se crea `frases.json` con plantillas y validación al importar. A la prueba final se le suman las preguntas de frases (60%), usando el reproductor de secuencias del Traductor. | Banco de frases, y completa la prueba final |

Las fases 0 a 3 no dependen de contenido nuevo. Al final de la fase 2 ya funciona todo el ciclo: rendir, aprobar y desbloquear.

## Usuarios ya registrados

No hace falta resetearlos. `handup.db` está en `.gitignore`: cada integrante tiene su propia base local, solo con perfiles de prueba. La migración de la fase 0 conserva su nivel. Para arrancar de cero alcanza con borrar `handup.db` (se regenera con los 3 perfiles de ejemplo).

## Para acordar con el equipo

1. La prueba final va a ser **solo de palabras por un tiempo**, aunque la tarjeta pide 60% de frases.
2. El orden: primero el Traductor (lo necesita la fase 4) y después las fases 0 a 4.
