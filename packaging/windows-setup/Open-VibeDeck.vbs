' Launches the VibeDeck web UI without ever creating a Command Prompt window.
Option Explicit

Const Url = "http://127.0.0.1:5000"
Const HealthUrl = "http://127.0.0.1:5000/health"

If WScript.Arguments.Named.Exists("check") Then
  WScript.Quit 0
End If

Dim shell, fso, appDir, hostExe, attempt
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
appDir = fso.GetParentFolderName(WScript.ScriptFullName)
hostExe = fso.BuildPath(appDir, "VibeDeck.Host.exe")

If Not fso.FileExists(hostExe) Then
  shell.Popup "VibeDeck Host is missing. Re-run VibeDeck Setup.", 0, "VibeDeck", 16
  WScript.Quit 1
End If

If Not IsHostReady() Then
  shell.CurrentDirectory = appDir
  shell.Run """" & hostExe & """", 0, False
End If

For attempt = 1 To 40
  If IsHostReady() Then
    shell.Run Url, 1, False
    WScript.Quit 0
  End If
  WScript.Sleep 500
Next

shell.Popup "VibeDeck Host did not become ready. Check that nothing else owns port 5000, then try again.", 0, "VibeDeck", 48
shell.Run Url, 1, False
WScript.Quit 2

Function IsHostReady()
  On Error Resume Next
  Dim request
  Set request = CreateObject("WinHttp.WinHttpRequest.5.1")
  request.SetTimeouts 500, 500, 500, 1000
  request.Open "GET", HealthUrl, False
  request.Send
  IsHostReady = (Err.Number = 0 And request.Status = 200)
  Err.Clear
  On Error GoTo 0
End Function
