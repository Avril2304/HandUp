@echo off
rem Doble clic: arma Entrega\HandUp.zip con HandUp listo para pasarle a otra persona (ver publicar.ps1)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publicar.ps1"
pause
