Imports System.Windows.Forms
Imports SatIPTV.Classes

Class Application

    ' Ereignisse auf Anwendungsebene wie Startup, Exit und DispatcherUnhandledException
    ' können in dieser Datei verarbeitet werden.

    Private KListener As New KeyboardListener()

    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        KListener.SetHook()

    End Sub
End Class
