# Arma "HandUp - Guia de uso.pdf" a partir de ProyectoFinal/Guia/guia.json (el mismo contenido que muestra la app)
# y de las capturas de ProyectoFinal/Guia/capturas. Usa la estética de la app: colores, Georgia, íconos de Windows y el logo.
# El PDF lo imprime Microsoft Edge (viene con Windows), así que no hace falta instalar nada.
#
# Uso (desde la raíz del repo):  powershell -ExecutionPolicy Bypass -File Presentacion\generar-guia.ps1

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$carpetaGuia = Join-Path $raiz 'ProyectoFinal\Guia'
$salidaPdf = Join-Path $PSScriptRoot 'HandUp - Guia de uso.pdf'
$html = Join-Path $env:TEMP 'handup-guia.html'

$edge = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'No se encontró Microsoft Edge para imprimir el PDF.' }

$guia = Get-Content -Raw -Encoding UTF8 (Join-Path $carpetaGuia 'guia.json') | ConvertFrom-Json

function Html([string]$texto) { [System.Net.WebUtility]::HtmlEncode($texto) }
function Icono([string]$codigo) { if ($codigo) { "&#x$codigo;" } else { '&#xE946;' } }
function Uri([string]$ruta) { ([System.Uri](Resolve-Path $ruta).Path).AbsoluteUri }

$logo = Uri (Join-Path $raiz 'ProyectoFinal\Assets\logo.jpg')

# ===== Índice =====
$indice = ''
$n = 0
foreach ($s in $guia.secciones) {
    $n++
    $indice += "<li><span class='ico'>$(Icono $s.glifo)</span><span class='num'>$n</span>$(Html $s.titulo)</li>`n"
}

# ===== Secciones =====
$secciones = ''
$n = 0
foreach ($s in $guia.secciones) {
    $n++
    $cuerpo = "<p class='resumen'>$(Html $s.resumen)</p>"

    if ($s.pasos) {
        $cuerpo += "<h3><span class='ico'>&#xE8FD;</span>Cómo se usa</h3><ol class='pasos'>"
        foreach ($p in $s.pasos) { $cuerpo += "<li>$(Html $p)</li>" }
        $cuerpo += '</ol>'
    }
    if ($s.consejo) {
        $cuerpo += "<div class='consejo'><span class='ico'>&#xE82F;</span><p>$(Html $s.consejo)</p></div>"
    }
    if ($s.atajos) {
        $cuerpo += "<h3><span class='ico'>&#xE765;</span>Atajos de teclado</h3><table class='atajos'>"
        foreach ($a in $s.atajos) { $cuerpo += "<tr><td><span class='tecla'>$(Html $a.teclas)</span></td><td>$(Html $a.accion)</td></tr>" }
        $cuerpo += '</table>'
    }
    foreach ($c in $s.capturas) {
        $ruta = Join-Path $carpetaGuia "capturas\$($c.archivo)"
        if (Test-Path $ruta) {
            $cuerpo += "<figure><img src='$(Uri $ruta)' alt='$(Html $c.pie)'><figcaption>$(Html $c.pie)</figcaption></figure>"
        }
    }

    $secciones += @"
<section class='seccion'>
  <header><span class='badge'>$(Icono $s.glifo)</span><div><div class='kicker'>Sección $n</div><h2>$(Html $s.titulo)</h2></div></header>
  $cuerpo
</section>
"@
}

$fecha = (Get-Date).ToString('MMMM yyyy', [System.Globalization.CultureInfo]'es-AR')

$contenido = @"
<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='utf-8'>
<title>$(Html $guia.titulo)</title>
<style>
  @page { size: A4; margin: 18mm 16mm 20mm 16mm; }
  :root {
    --fondo: #FAF8F4; --tarjeta: #FFFFFF; --borde: #E7E2D6; --borde-acento: #CFE8E1;
    --tinta: #14312B; --apagado: #6B7B76; --acento: #1F7A6C; --acento-osc: #175C52;
    --acento-suave: #EAF4F1; --barra: #14332B; --destacado: #5DCAA5;
  }
  * { box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  body { margin: 0; font-family: 'Segoe UI', sans-serif; color: var(--tinta); font-size: 10.5pt; line-height: 1.5; }
  h1, h2 { font-family: Georgia, serif; font-weight: bold; margin: 0; }
  .ico, .badge { font-family: 'Segoe Fluent Icons', 'Segoe MDL2 Assets'; font-weight: normal; }

  /* Tapa: el panel de marca del login (fondo verde oscuro, logo y HandUp en Georgia) */
  .tapa { height: 257mm; background: var(--barra); color: #fff; border-radius: 6mm; display: flex; flex-direction: column;
          align-items: center; justify-content: center; text-align: center; break-after: page; }
  .tapa img { width: 56mm; height: 56mm; border-radius: 12mm; }
  .tapa h1 { font-size: 46pt; margin-top: 12mm; }
  .tapa .sub { font-size: 17pt; color: var(--borde-acento); margin-top: 3mm; }
  .tapa .linea { width: 30mm; height: 1.2mm; background: var(--destacado); border-radius: 1mm; margin: 9mm auto; }
  .tapa .desc { font-size: 11.5pt; color: #CFE8E1; max-width: 120mm; }
  .tapa .pie { margin-top: 26mm; font-size: 9.5pt; color: #9DBDB4; }

  /* Índice */
  .indice { break-after: page; }
  .indice h1 { font-size: 26pt; margin-bottom: 2mm; }
  .indice .intro { color: var(--apagado); margin: 0 0 8mm; }
  .indice ol { list-style: none; padding: 0; margin: 0; columns: 1; }
  .indice li { display: flex; align-items: center; gap: 4mm; padding: 3.2mm 4mm; margin-bottom: 2.5mm; background: var(--tarjeta);
               border: 0.35mm solid var(--borde); border-radius: 3mm; font-weight: 600; font-size: 11.5pt; }
  .indice .ico { color: var(--acento); font-size: 13pt; width: 6mm; text-align: center; }
  .indice .num { color: var(--apagado); font-weight: 600; font-size: 9.5pt; border: 0.35mm solid var(--borde-acento);
                 border-radius: 1.3mm; padding: 0 1.8mm; }

  /* Secciones: como las tarjetas de la app */
  .seccion { break-before: page; }
  .seccion header { display: flex; align-items: center; gap: 5mm; padding-bottom: 4mm; margin-bottom: 4mm; border-bottom: 0.5mm solid var(--borde); }
  .badge { width: 15mm; height: 15mm; border-radius: 4mm; background: var(--acento); color: #fff; font-size: 19pt;
           display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
  .kicker { font-size: 8.5pt; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; color: var(--apagado); }
  .seccion h2 { font-size: 22pt; }
  .resumen { font-size: 11.5pt; margin: 0 0 2mm; }
  h3 { font-size: 9pt; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; color: var(--apagado);
       margin: 6mm 0 3mm; display: flex; align-items: center; gap: 2.5mm; }
  h3 .ico { color: var(--acento); font-size: 11pt; }

  ol.pasos { list-style: none; counter-reset: paso; padding: 0; margin: 0; }
  ol.pasos li { counter-increment: paso; position: relative; padding-left: 10mm; margin-bottom: 2.6mm; }
  ol.pasos li::before { content: counter(paso); position: absolute; left: 0; top: 0.2mm; width: 6.2mm; height: 6.2mm; border-radius: 50%;
                        background: var(--acento-suave); color: var(--acento-osc); font-weight: 700; font-size: 8.5pt;
                        display: flex; align-items: center; justify-content: center; }

  .consejo { display: flex; gap: 3mm; background: var(--acento-suave); border: 0.4mm solid var(--borde-acento); border-radius: 3mm;
             padding: 3mm 4mm; margin-top: 4mm; }
  .consejo .ico { color: var(--acento-osc); font-size: 12pt; }
  .consejo p { margin: 0; }

  table.atajos { border-collapse: collapse; width: 100%; }
  table.atajos td { padding: 1.3mm 0; vertical-align: top; }
  table.atajos td:first-child { width: 38mm; padding-right: 4mm; }
  .tecla { display: inline-block; border: 0.4mm solid var(--borde-acento); border-radius: 1.6mm; padding: 0.3mm 2.2mm;
           font-weight: 700; color: var(--acento-osc); font-size: 9.5pt; background: #fff; }

  figure { margin: 6mm auto 0; break-inside: avoid; max-width: 150mm; }
  figure img { width: 100%; border: 0.35mm solid var(--borde); border-radius: 3mm; display: block; }
  figcaption { font-size: 9pt; color: var(--apagado); margin-top: 1.8mm; }

</style>
</head>
<body>
  <div class='tapa'>
    <img src='$logo' alt='Logo de HandUp'>
    <h1>HandUp</h1>
    <div class='sub'>Guía de uso</div>
    <div class='linea'></div>
    <div class='desc'>$(Html $guia.subtitulo)</div>
    <div class='pie'>Proyecto final · $fecha</div>
  </div>

  <div class='indice'>
    <h1>Contenido</h1>
    <p class='intro'>Esta guía es la misma que se abre dentro de la app con el botón "i" (al lado de la racha) o con la tecla F1.</p>
    <ol>
$indice
    </ol>
  </div>

$secciones
</body>
</html>
"@

[System.IO.File]::WriteAllText($html, $contenido, (New-Object System.Text.UTF8Encoding($false)))

# Edge imprime el HTML a PDF (sin encabezado ni pie del navegador)
$perfilTemporal = Join-Path $env:TEMP 'handup-edge-perfil'
& $edge --headless=new --disable-gpu --no-first-run "--user-data-dir=$perfilTemporal" --no-pdf-header-footer `
        "--print-to-pdf=$salidaPdf" ([System.Uri]$html).AbsoluteUri | Out-Null
Start-Sleep -Milliseconds 500

if (-not (Test-Path $salidaPdf)) { throw 'Edge no generó el PDF.' }
Remove-Item $html -ErrorAction SilentlyContinue
Write-Host "Listo: $salidaPdf"
