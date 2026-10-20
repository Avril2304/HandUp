"""Genera los clips de señas a partir de los videos fuente de assets/signs/_fuentes_raw.
Escribe en assets/signs y en HandUp/ProyectoFinal/Assets/signs (la carpeta = la categoría de la seña).

Cada entrada: (carpeta, nombre, video_fuente, inicio, fin, crop_opcional, segundo_thumbnail_relativo)
Los tiempos se verificaron cuadro por cuadro (4 fps) contra los carteles de cada video.

Tapado de texto (drawbox): cada fuente que muestra el nombre de la seña sobreimpreso tiene su
constante *_BOX que pinta un rectángulo del color del fondo sobre esa zona. Así los clips quedan
limpios para la sección de evaluación sin tener que mantener dos archivos por seña.
Las coordenadas están en píxeles de la resolución original del video fuente.
"""
import os, subprocess, shutil, sys

BASE = os.environ.get("PGE_BASE") or os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
RAW = os.path.join(BASE, "assets/signs/_fuentes_raw")
DESTS = [os.path.join(BASE, "assets/signs"), os.path.join(BASE, "HandUp/ProyectoFinal/Assets/signs")]

PRON = "iPKG67lXuoc_PRONOMBRES EN LSA.mp4"                         # Dirección de Documentación
FAB = "3dtU2H8X_lA_LSA - preguntas y pronombres - Prof. Fab.mp4"   # Prof. Fabián
ULSA_NEG = "W0M9GY_1EgI_preguntas_negacion.mp4"                     # ULSA
SALUDOS = "_muAHDVEItk_Vocabulario de los SALUDOS (LS.mp4"          # Signs of Love
VERBOS_DD = "qo-uIEi3ko8_VERBOS EN LSA.mp4"                         # Dirección de Documentación
VERBOS_SOL = "SHB1Rlg6uvM_Vocabulario de los VERBOS (LSA.mp4"       # Signs of Love
ADJ = "vBSQD1A3OIg_adjetivos.mp4"
ADJ_CROP = "crop=888:544:964:316"  # recuadro de la señante dentro de la diapositiva
LUGARES = "BD7h2yxvKwo_Vocabulario de los LUGARES  (L.mp4"         # Signs of Love
HOGAR = "WIV9V42FeQg_Vocabulario del HOGAR (LSA).mp4"               # Signs of Love
TRANSPORTE = "gPXlc7IaYYM_Vocabulario del TRANSPORTE (LS.mp4"       # Signs of Love
EFFATA_TR = "IiJmufvvX-c_Medios de Transporte en Lengua.mp4"        # Fundación Effata
EFFATA_CROP = "crop=960:720:160:0"  # saca las franjas negras laterales
TERE_ADJ = "RH0ndn0_DMA_Adjetivos en lengua de señas A.mp4"         # LSA con Tere
MAXI_LUG = "W18Q4ZqWBbo_Lengua de Señas Argentina - LU.mp4"         # Maxi Villarruel
CAPAC_FAM = "feV-ra7rvPA_Lengua de señas argentina Clas.mp4"        # Capacitación LSA (chany3h)
CAPAC_CROP = "crop=560:350:90:30"  # saca franjas negras e íconos del celular
FAB_CANT = "xxZXycZMkyk_LSA - ADVERBIOS DE CANTIDAD - .mp4"         # Prof. Fabián
MAXI_PV4 = "9FbH1aIYMoc_Lengua de Señas Argentina - PA.mp4"         # Maxi Villarruel, palabras varias 4
MAXI_PV9 = "SJrDqJA5E_E_Lengua de Señas Argentina - PA.mp4"         # Maxi Villarruel, palabras varias 9
SOL_CAT = "1ICc2IZuy6A_Categorías de VERBOS (LSA).mp4"              # Signs of Love
ULSA_V12 = "1XW6tOX3Y1Y_12⧸ Listado de verbos en LSA. .mp4"          # ULSA

# ---- Filtros drawbox para tapar el nombre de la seña sobreimpreso en cada fuente ----
# VERBOS_DD (1280×720): palabras a la derecha o a la izquierda según la seña
VERBOS_DD_R_BOX = "drawbox=x=760:y=95:w=510:h=155:color=white:t=fill"
VERBOS_DD_L_BOX = "drawbox=x=30:y=95:w=450:h=155:color=white:t=fill"
# Signs of Love 1280×720 (LUGARES, VERBOS_SOL, SOL_CAT):
#   etiqueta turquesa con el nombre, esquina derecha, aprox y=310..450
SOL_BOX         = "drawbox=x=920:y=300:w=360:h=175:color=white:t=fill"
# Lugares (Signs of Love): tapa ilustración arriba a la derecha y etiqueta de texto
LUGARES_BOX     = "drawbox=x=710:y=0:w=570:h=480:color=white:t=fill"
# Signs of Love SALUDOS:
#   Para bien, mal, mas_o_menos, no_entiendo (con escala/emojis a la derecha, banner empieza en y=350):
SALUDOS_ESCALA_BOX  = "drawbox=x=680:y=350:w=600:h=370:color=white:t=fill"
#   Para como_estas, perdon, por_favor (con personaje ilustrado, banner bajo el personaje empieza en y=490):
SALUDOS_PERSONA_BOX = "drawbox=x=580:y=490:w=700:h=230:color=white:t=fill"
# Transporte (Signs of Love): tapa tanto la ilustración del vehículo como la etiqueta de texto
TRANSPORTE_BOX  = "drawbox=x=710:y=50:w=570:h=430:color=white:t=fill"
# Subte (Effata): rectángulo desde la esquina izquierda que tapa la zona del texto
SUBTE_BOX       = "drawbox=x=0:y=200:w=220:h=350:color=0x9aa8bc:t=fill"
# HOGAR (640×360): tapa ilustración de cocina y etiqueta con nombre
HOGAR_BOX       = "drawbox=x=455:y=15:w=185:h=215:color=white:t=fill"
# FAB (854×480): texto amarillo en negrita, esquina superior-derecha
FAB_BOX         = "drawbox=x=490:y=10:w=364:h=135:color=gray:t=fill"
# ULSA_NEG / ULSA_V12 (1280×720): texto gris oscuro, lado izquierdo, altura media
ULSA_BOX        = "drawbox=x=100:y=215:w=530:h=220:color=white:t=fill"
# TERE_ADJ (1280×720): texto en negrita arriba al centro (franja completa)
TERE_BOX        = "drawbox=x=80:y=0:w=1110:h=85:color=black:t=fill"
# MAXI_LUG (960×720): subtítulo blanco centrado abajo
MAXI_LUG_BOX    = "drawbox=x=0:y=600:w=960:h=120:color=black:t=fill"
# MAXI_PV9 (960×720): subtítulo blanco centrado abajo (algo más arriba)
MAXI_PV9_BOX    = "drawbox=x=0:y=500:w=960:h=100:color=black:t=fill"
# MAXI_PV4 (960×720): subtítulo blanco centrado abajo sobre remera/fondo (necesitar)
MAXI_PV4_BOX    = "drawbox=x=0:y=540:w=960:h=180:color=black:t=fill"
# FAB_CANT (1280×720): subtítulo negro con texto blanco abajo al centro (mucho, poco)
FAB_CANT_BOX    = "drawbox=x=350:y=610:w=580:h=90:color=black:t=fill"
# PRON (1280×720): palabra grande arriba a la izquierda (cada_uno, vos, nosotros, etc.)
PRON_L_BOX      = "drawbox=x=0:y=50:w=480:h=175:color=white:t=fill"
PRON_MIO_BOX    = "drawbox=x=0:y=40:w=440:h=265:color=white:t=fill"

# AMIGO (Capacitación LSA, tras crop 560x350): tapa el texto "AMIGO" con el color de la pared
AMIGO_BOX       = "drawbox=x=30:y=60:w=220:h=75:color=0xd1cbbf:t=fill"

CLIPS = [
    # --- pronombres (mismo señante para todos) ---
    # "yo" usa sign_476 (saludos/yo, CAS); este corte quedó sin uso
    # ("primeras_frases", "yo",       PRON, 34.3, 37.9, None, 1.2, None),
    ("primeras_frases", "vos",        PRON, 38.1, 41.4, None, 1.2, PRON_L_BOX),
    ("primeras_frases", "nosotros",   PRON, 56.8, 60.0, None, 1.2, PRON_L_BOX),
    ("primeras_frases", "ellos",      PRON, 52.7, 56.0, None, 0.8, PRON_L_BOX),
    ("primeras_frases", "suyo",       PRON, 24.3, 27.5, None, 0.8, PRON_L_BOX),
    # --- preguntas ---
    ("primeras_frases", "como",       FAB, 26.7, 29.1, None, 1.0, FAB_BOX),
    ("primeras_frases", "que",        FAB, 22.3, 24.6, None, 1.0, FAB_BOX),
    ("primeras_frases", "quien",      FAB, 34.3, 36.6, None, 1.0, FAB_BOX),
    # --- sí / no (variante básica, sin las enfáticas) ---
    ("primeras_frases", "si",         ULSA_NEG, 54.85, 57.2, None, 0.6, ULSA_BOX),
    ("primeras_frases", "no",         ULSA_NEG, 70.4, 72.6, None, 0.8, ULSA_BOX),
    # --- saludos / estados (mismo señante, banner abajo a la derecha) ---
    ("primeras_frases", "como_estas", SALUDOS, 35.2, 40.3, None, 1.2, SALUDOS_PERSONA_BOX),
    ("primeras_frases", "bien",       SALUDOS, 40.8, 45.0, None, 1.6, SALUDOS_ESCALA_BOX),
    ("primeras_frases", "mal",        SALUDOS, 45.6, 50.4, None, 1.6, SALUDOS_ESCALA_BOX),
    ("primeras_frases", "mas_o_menos", SALUDOS, 53.4, 57.9, None, 1.6, SALUDOS_ESCALA_BOX),
    ("primeras_frases", "perdon",     SALUDOS, 59.0, 63.5, None, 1.6, SALUDOS_PERSONA_BOX),
    # --- verbos (Dirección de Documentación, mismo señante) ---
    ("primeras_frases", "tener",    VERBOS_DD, 11.6, 14.7, None, 1.0, VERBOS_DD_R_BOX),
    ("acciones_tiempo", "gustar",   VERBOS_DD, 81.7, 83.7, None, 0.8, VERBOS_DD_L_BOX),
    ("acciones_tiempo", "tomar",    VERBOS_DD, 99.2, 102.1, None, 1.0, VERBOS_DD_R_BOX),
    ("acciones_tiempo", "trabajar", VERBOS_DD, 109.9, 112.6, None, 0.8, VERBOS_DD_R_BOX),
    ("acciones_tiempo", "jugar",    VERBOS_DD, 115.6, 118.4, None, 0.8, VERBOS_DD_L_BOX),
    ("primeras_frases", "vivir",    VERBOS_DD, 118.5, 120.6, None, 0.8, VERBOS_DD_L_BOX),
    ("primeras_frases", "conocer",  VERBOS_DD, 123.4, 126.3, None, 0.8, VERBOS_DD_R_BOX),
    ("acciones_tiempo", "ir",       VERBOS_DD, 128.9, 130.8, None, 0.6, VERBOS_DD_L_BOX),
    ("acciones_tiempo", "querer",   VERBOS_SOL, 125.4, 128.4, None, 1.2, SOL_BOX),
    # --- adjetivos (recortado al recuadro de la señante; el crop ya elimina el texto) ---
    ("acciones_tiempo", "lindo",            ADJ, 35.3, 40.5, ADJ_CROP, 4.0, None),
    # --- lugares (Signs of Love) ---
    ("lugares", "casa",               LUGARES, 10.4, 13.4, None, 1.2, LUGARES_BOX),
    ("lugares", "baño",               LUGARES, 13.4, 16.4, None, 0.8, LUGARES_BOX),
    ("lugares", "plaza",              LUGARES, 50.0, 52.2, None, 1.4, LUGARES_BOX),
    ("lugares", "cocina",             HOGAR, 22.6, 26.9, None, 1.6, HOGAR_BOX),
    # --- transporte (Signs of Love: tapa vehículo + texto; subte: tapa cartel izquierda) ---
    ("transporte", "auto",            TRANSPORTE, 5.4, 9.4, None, 1.2, TRANSPORTE_BOX),
    ("transporte", "colectivo",       TRANSPORTE, 9.5, 15.1, None, 2.0, TRANSPORTE_BOX),
    ("transporte", "bicicleta",       TRANSPORTE, 19.9, 23.4, None, 1.2, TRANSPORTE_BOX),
    ("transporte", "moto",            TRANSPORTE, 23.5, 26.4, None, 1.2, TRANSPORTE_BOX),
    ("transporte", "avion",           TRANSPORTE, 26.5, 29.2, None, 1.0, TRANSPORTE_BOX),
    ("transporte", "tren",            TRANSPORTE, 29.3, 33.6, None, 1.6, TRANSPORTE_BOX),
    ("transporte", "taxi",            TRANSPORTE, 39.6, 42.6, None, 1.4, TRANSPORTE_BOX),
    ("transporte", "subte",           EFFATA_TR, 41.8, 44.4, EFFATA_CROP, 0.6, SUBTE_BOX),
    # --- avanzado ---
    ("avanzado", "facil",             TERE_ADJ, 111.3, 114.3, None, 0.8, TERE_BOX),
    ("avanzado", "dificil",           TERE_ADJ, 124.6, 128.8, None, 1.6, TERE_BOX),
    # --- faltantes de la épica ---
    ("primeras_frases", "cuando",     FAB, 24.4, 26.4, None, 1.0, FAB_BOX),
    ("primeras_frases", "por_favor",  SALUDOS, 97.6, 101.0, None, 1.6, SALUDOS_PERSONA_BOX),
    ("lugares", "trabajo",            VERBOS_DD, 109.9, 112.6, None, 0.8, VERBOS_DD_R_BOX),  # misma seña que trabajar
    ("lugares", "supermercado",       MAXI_LUG, 38.4, 39.9, None, 0.8, MAXI_LUG_BOX),
    ("emociones", "amigo",            CAPAC_FAM, 84.3, 87.6, CAPAC_CROP, 1.6, AMIGO_BOX),
    # --- épica, nivel 2 (acciones_tiempo) ---
    ("acciones_tiempo", "grande",     ADJ, 25.5, 27.9, ADJ_CROP, 1.2, None),
    ("acciones_tiempo", "chico",      ADJ, 28.4, 31.0, ADJ_CROP, 1.2, None),
    ("acciones_tiempo", "feo",        ADJ, 44.3, 46.2, ADJ_CROP, 0.6, None),
    ("acciones_tiempo", "caliente",   ADJ, 96.1, 98.2, ADJ_CROP, 0.8, None),
    ("acciones_tiempo", "mucho",      FAB_CANT, 14.6, 17.6, None, 1.2, FAB_CANT_BOX),
    ("acciones_tiempo", "poco",       FAB_CANT, 23.9, 27.4, None, 1.2, FAB_CANT_BOX),
    ("acciones_tiempo", "cocinar",    VERBOS_DD, 26.9, 29.4, None, 1.0, VERBOS_DD_R_BOX),
    ("acciones_tiempo", "necesitar",  MAXI_PV4, 27.0, 28.6, None, 0.6, MAXI_PV4_BOX),
    # --- épica, nivel 3 (verbos_cotidianos) ---
    ("verbos_cotidianos", "venir",    VERBOS_DD, 135.8, 138.4, None, 0.8, VERBOS_DD_R_BOX),
    ("verbos_cotidianos", "llegar",   VERBOS_SOL, 75.8, 78.4, None, 1.2, SOL_BOX),
    ("verbos_cotidianos", "salir",    MAXI_PV9, 11.5, 12.9, None, 0.6, MAXI_PV9_BOX),
    # --- reemplazos de señas de la épica sin video LSA ---
    ("primeras_frases", "por_que",    FAB, 29.2, 31.2, None, 0.8, FAB_BOX),         # en vez de Porque
    ("primeras_frases", "para_que",   ULSA_NEG, 40.9, 43.0, None, 0.8, ULSA_BOX),    # en vez de Para
    ("acciones_tiempo", "cada_uno",   PRON, 62.4, 64.8, None, 0.6, PRON_L_BOX),      # en vez de Todos
    ("acciones_tiempo", "probar",     ULSA_V12, 8.0, 10.9, None, 1.0, ULSA_BOX),     # en vez de Rico
    ("verbos_cotidianos", "acompanar", SOL_CAT, 46.8, 50.0, None, 1.6, SOL_BOX),    # en vez de Con
    # --- frases hechas, nivel 4 (frases_comunes) ---
    ("frases_comunes", "no_entiendo", SALUDOS, 86.0, 90.4, None, 2.0, SALUDOS_ESCALA_BOX),
    ("frases_comunes", "repetir",     ULSA_V12, 123.6, 127.0, None, 1.2, ULSA_BOX),
]


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"ffmpeg falló: {' '.join(cmd)}\n{r.stderr[-800:]}")


# uso: python3 fix_clips.py [nombre ...]  (sin argumentos regenera todos)
SOLO = set(sys.argv[1:])

for cat, name, src, start, end, crop, thumb_at, box in CLIPS:
    if SOLO and name not in SOLO:
        continue
    src_path = os.path.join(RAW, src)
    assert os.path.exists(src_path), src_path
    # El orden importa: primero crop (si hay), luego box (tapado de texto), luego scale
    vf_clip  = ",".join(f for f in [crop, box, "scale=-2:'min(720,ih)'"] if f)
    vf_thumb = ",".join(f for f in [crop, box, "scale=-2:'min(720,ih)'"] if f)

    out_dir = os.path.join(DESTS[0], cat)
    os.makedirs(out_dir, exist_ok=True)
    mp4 = os.path.join(out_dir, f"{name}.mp4")
    png = os.path.join(out_dir, f"{name}.png")

    # -ss después de -i: corte exacto al cuadro (no salta al keyframe)
    run(["ffmpeg", "-v", "error", "-y", "-i", src_path, "-ss", str(start), "-to", str(end),
         "-vf", vf_clip, "-c:v", "libx264", "-crf", "22", "-preset", "medium",
         "-pix_fmt", "yuv420p", "-an", "-movflags", "+faststart", mp4])
    run(["ffmpeg", "-v", "error", "-y", "-ss", str(start + thumb_at), "-i", src_path,
         "-frames:v", "1", "-vf", vf_thumb, png])

    for d in DESTS[1:]:
        os.makedirs(os.path.join(d, cat), exist_ok=True)
        shutil.copyfile(mp4, os.path.join(d, cat, f"{name}.mp4"))
        shutil.copyfile(png, os.path.join(d, cat, f"{name}.png"))
    print(f"✅ {cat}/{name}  {end - start:.1f}s")

