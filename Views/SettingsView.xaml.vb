Imports SatIPTV.Helper
Imports SatIPTV.ViewModels

Namespace Views
    Public Class SettingsView

        Public Sub New()

            ' Dieser Aufruf ist für den Designer erforderlich.
            InitializeComponent()

            ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
            TvHeadendPassword.Password = EncryptionHelper.ToInsecureString(EncryptionHelper.DecryptString(My.Settings.TvHeadendPassword))
        End Sub
        Private Sub TvHeadendPassword_PasswordChanged(sender As Object, e As RoutedEventArgs)
            DirectCast(Me.DataContext, SettingsViewModel).TvHeadendPassword = TvHeadendPassword.SecurePassword
        End Sub
    End Class
End Namespace