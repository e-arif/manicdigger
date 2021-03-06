; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{119E2FCB-5CDD-4C24-BCB2-56A824E2BF0A}
AppName=Manic Digger
AppVerName=Manic Digger
AppPublisherURL=http://www.manicdigger.sourceforge.net/
AppSupportURL=http://www.manicdigger.sourceforge.net/
AppUpdatesURL=http://www.manicdigger.sourceforge.net/
DefaultDirName={sd}\Manic Digger
DefaultGroupName=Manic Digger
AllowNoIcons=yes
OutputBaseFilename=setup
Compression=lzma
SolidCompression=yes
OutputDir=output2
WizardImageFile=setup_WizardImage.bmp
WizardSmallImageFile=setup_WizardSmallImage.bmp

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "output\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{cm:UninstallProgram,Manic Digger}"; Filename: "{uninstallexe}"
Name: "{group}\Manic Digger"; Filename: "{app}\ManicDigger.exe"
Name: "{group}\Configuration"; Filename: "{app}\UserData"

[Registry]
Root: HKCR; Subkey: ".mdlink"; ValueType: string; ValueName: ""; ValueData: "ManicDigger"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "ManicDigger"; ValueType: string; ValueName: ""; ValueData: "Manic Digger multiplayer link"; Flags: uninsdeletekey
Root: HKCR; Subkey: "ManicDigger\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ManicDigger.exe,0"
Root: HKCR; Subkey: "ManicDigger\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ManicDigger.exe"" ""%1"""




Root: HKCR; Subkey: "md"; ValueType: string; ValueName: ""; ValueData: "URL:Manic Digger"; Flags: uninsdeletekey
Root: HKCR; Subkey: "md"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKCR; Subkey: "md\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ManicDigger.exe,0"
Root: HKCR; Subkey: "md\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ManicDigger.exe"" ""%1"""