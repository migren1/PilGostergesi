; Pil Gostergesi - Inno Setup kurulum script'i
; Derleme: ISCC.exe installer.iss

#define AppName "Pil Gostergesi"
#define AppVersion "1.0.0"
#define AppPublisher "Pil Gostergesi"
#define AppExe "PilGostergesi.exe"
#define SrcDir ".."

[Setup]
AppId={{E7A1C9D4-2B6F-4A3E-8C5D-PILGOSTERGE01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\PilGostergesi
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=.\out
OutputBaseFilename=PilGostergesi-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne kısayol"; GroupDescription: "Ek simgeler:"
; Varsayılan İŞARETLİ (unchecked YOK): Windows açılışında başlasın
Name: "startup"; Description: "Windows açılışında başlat"; GroupDescription: "Seçenekler:"

[Files]
Source: "{#SrcDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\OKU.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SrcDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
; Baslangic: uygulamanin kendi kullandigi ad ("Pil Yüzdesi.lnk") -> cift kayit olmaz
Name: "{userstartup}\Pil Yüzdesi"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Pil Göstergesi'ni şimdi başlat"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kaldirmadan once uygulamayi durdur
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden; RunOnceId: "StopPil"

[UninstallDelete]
; Uygulamanin kendi olusturdugu baslangic kisayolunu ve loglari temizle
Type: files; Name: "{userstartup}\Pil Yüzdesi.lnk"
Type: files; Name: "{app}\widget.log"
Type: files; Name: "{app}\boot.log"
Type: files; Name: "{app}\test.txt"
