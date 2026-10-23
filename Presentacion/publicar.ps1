# Arma la carpeta "HandUp" lista para pasarle a alguien que no programa, y la comprime en HandUp.zip.
# El programa sale como un solo ejecutable ("Abrir HandUp.exe") que trae .NET adentro, así que la otra
# persona no tiene que instalar nada. Los videos, la guía y los .json quedan al lado del .exe.
#
# Uso (desde la raíz del repo):  powershell -ExecutionPolicy Bypass -File Presentacion\publicar.ps1
# o doble clic en Presentacion\publicar.bat
# Requiere el SDK de .NET 8: https://dotnet.microsoft.com/download/dotnet/8.0

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz 'ProyectoFinal\ProyectoFinal.csproj'
$entrega = Join-Path $raiz 'Entrega'
$carpeta = Join-Path $entrega 'HandUp'
$zip = Join-Path $entrega 'HandUp.zip'
$nombreExe = 'Abrir HandUp.exe'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'No se encontró dotnet. Instalá el SDK de .NET 8 desde https://dotnet.microsoft.com/download/dotnet/8.0 y volvé a correr el script.'
}

# ===== Limpiar la entrega anterior =====
if (Test-Path $entrega) { Remove-Item $entrega -Recurse -Force }
New-Item -ItemType Directory -Path $carpeta | Out-Null

# ===== Publicar =====
Write-Host 'Publicando HandUp (puede tardar un par de minutos)...' -ForegroundColor Cyan
dotnet publish $proyecto -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $carpeta
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló (código $LASTEXITCODE)." }

# Los .pdb son para depurar y la base se crea sola al abrir la app: no van en la entrega
Get-ChildItem $carpeta -Filter *.pdb | Remove-Item -Force
Get-ChildItem $carpeta -Filter 'handup.db*' | Remove-Item -Force

# ===== Renombrar el ejecutable para que quede primero en la lista =====
Rename-Item (Join-Path $carpeta 'ProyectoFinal.exe') $nombreExe

# ===== Instrucciones =====
# Sin tilde en el nombre: el descompresor de Windows a veces rompe los nombres con acentos dentro de un .zip
$instrucciones = @"
CÓMO ABRIR HANDUP
=================

1. Si te llegó como HandUp.zip, primero descomprimilo:
   clic derecho sobre HandUp.zip -> "Extraer todo" -> "Extraer".
   (Si se abre desde adentro del .zip, no se ven los videos.)

2. Dejá la carpeta HandUp en el Escritorio o en Descargas.

3. Hacé doble clic en "Abrir HandUp" (el que tiene el logo de HandUp).

4. La primera vez, Windows puede mostrar "Windows protegió su PC".
   Es porque el programa es nuevo, no porque tenga algo malo.
   Tocá "Más información" y después "Ejecutar de todas formas".

5. Creá tu perfil con un nombre de usuario y una contraseña, y listo.
   Dentro de la app, el botón "i" (o la tecla F1) abre una guía de uso.

No hace falta instalar nada. No borres ni muevas las otras carpetas y archivos:
ahí están los videos de las señas.

Funciona en Windows 10 (versión 2004 o más nueva) y Windows 11.
"@
[System.IO.File]::WriteAllText((Join-Path $carpeta 'Como abrir HandUp.txt'), $instrucciones, (New-Object System.Text.UTF8Encoding $true))

# ===== Comprimir =====
Write-Host 'Comprimiendo HandUp.zip...' -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($carpeta, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)

$mb = [math]::Round((Get-Item $zip).Length / 1MB)
Write-Host ''
Write-Host "Listo: $zip ($mb MB)" -ForegroundColor Green
Write-Host 'Antes de mandarlo, probalo: descomprimilo en otra carpeta y abrí "Abrir HandUp.exe".'
Invoke-Item $entrega
