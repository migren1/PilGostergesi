@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize /codepage:65001 /win32icon:"%~dp0pil.ico" /out:"%~dp0PilGostergesi.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0PilGostergesi.cs"
if errorlevel 1 (echo HATA & pause) else (echo Tamam: PilGostergesi.exe)
