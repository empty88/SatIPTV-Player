
Imports System.Windows.Threading
Imports SatIPTV.Classes
Imports SatIPTV.Helper

Class Application

    ' Ereignisse auf Anwendungsebene wie Startup, Exit und DispatcherUnhandledException
    ' können in dieser Datei verarbeitet werden.

    Private KListener As New KeyboardListener()

    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        KListener.SetHook()

        DatabaseHelper.Initialize()
    End Sub

    Private Sub Application_Exit(sender As Object, e As ExitEventArgs) Handles Me.[Exit]
        EpgGrabber.Shutdown()
    End Sub

    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        EpgGrabber.Shutdown()
    End Sub
End Class
