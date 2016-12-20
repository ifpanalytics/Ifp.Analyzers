@echo off
set /p pw="Passwort: "
vsixsigntool.exe sign /f CodeSign.pfx /p %pw% E:\Ifp.Analyzers.Vsix.vsix
Pause