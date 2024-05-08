Imports System.Diagnostics.Contracts
Imports System.Security
Imports System.Security.Cryptography
Imports System.Security.Cryptography.Xml
Imports System.Text
Imports System.Windows.Forms
Imports Prism.Commands
Imports SatIPTV.Helper

Namespace ViewModels
    Public Class SettingsViewModel
        Inherits ViewModelBase


        Public Property UseTvHeadend As Boolean

        Public Property TvHeadendServer As String

        Public Property TvHeadendUser As String
        Public Property TvHeadendPassword As SecureString
        Public Property DVBSChannelListUrl As String

        Public Property SaveCommand As DelegateCommand


        Public Sub New()
            SaveCommand = New DelegateCommand(AddressOf SaveCommandExecute)

            UseTvHeadend = My.Settings.UseTvHeadend
            TvHeadendServer = My.Settings.TvHeadendServer
            TvHeadendUser = My.Settings.TvHeadendUser
            TvHeadendPassword = EncryptionHelper.DecryptString(My.Settings.TvHeadendPassword)
            DVBSChannelListUrl = My.Settings.DVBSChannelListUrl
        End Sub

        Private Sub SaveCommandExecute()
            My.Settings.UseTvHeadend = UseTvHeadend
            My.Settings.TvHeadendServer = TvHeadendServer
            My.Settings.TvHeadendUser = TvHeadendUser
            My.Settings.TvHeadendPassword = EncryptionHelper.EncryptString(TvHeadendPassword)
            My.Settings.DVBSChannelListUrl = DVBSChannelListUrl
            My.Settings.Save()
            GetWindow().DialogResult = DialogResult.OK
            CloseWindow()
        End Sub


    End Class
End Namespace