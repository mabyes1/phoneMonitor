' Launches VibeDeck web UI without a visible console window.
Option Explicit
Dim shell, fso, cmd, rc
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
cmd = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "Open-VibeDeck.cmd")
If fso.FileExists(cmd) Then
  rc = shell.Run("""" & cmd & """", 0, False)
Else
  shell.Run "http://127.0.0.1:5000", 1, False
End If
