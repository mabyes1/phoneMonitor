' Starts the VibeDeck Host inside the signed-in desktop session without a console window.
Option Explicit
Dim shell, fso, appDir, hostExe
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
appDir = fso.GetParentFolderName(WScript.ScriptFullName)
hostExe = fso.BuildPath(appDir, "VibeDeck.Host.exe")
If fso.FileExists(hostExe) Then
  shell.CurrentDirectory = appDir
  shell.Run """" & hostExe & """", 0, False
End If
