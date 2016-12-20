@echo off
set /p pw="Passwort: "
%~dp0\vsixsigntool.exe sign /f CodeSign.pfx /p %pw% "%~dp0..\..\Ifp.Analyzers\Ifp.Analyzers.Vsix\bin\Release\Ifp.Analyzers.Vsix.vsix"
Pause