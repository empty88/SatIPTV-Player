Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Configuration
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms
Imports LibVLCSharp.Shared
Imports Prism
Imports Prism.Commands
Imports SatIPTV.Classes
Imports SatIPTV.Helper
Imports SatIPTV.ViewModels.Models
Imports SatIPTV.Views

Namespace ViewModels
    Public Class MainViewModel
        Inherits ViewModelBase

        Private _currentVolume As Integer
        Private _selectedChannel As ChannelViewModel
        Private _osdTimer As New System.Threading.Timer(AddressOf OsdTimerCallback, Nothing, Timeout.Infinite, Timeout.Infinite)

        Private _currentProgramUpdateTimer As New System.Threading.Timer(AddressOf CurrentProgramUpdateTimerCallback, Nothing, 20000, 20000)

        Private Sub CurrentProgramUpdateTimerCallback(state As Object)
            Task.Run(Sub()
                         For Each channel In ChannelList
                             If SelectedChannel Is Nothing Then Exit Sub
                             If My.Settings.UseTvHeadend Then channel.CurrentProgram = NetworkHelper.GetCurrentEpgFromTvHeadend(channel.DisplayName)
                         Next
                     End Sub)
        End Sub

        Private options As String() = {}
        Private libVLC As New LibVLC(options)
        Private _nowPlaying As String
        Private _showChannelListLogos As Boolean
        Private _showOsd As Boolean
        Private _mute As Boolean
        Private _showChannelList As Boolean
        Private _windowStyle As WindowStyle
        Private _windowState As WindowState
        Private Shared _errorString As String
        Private _fullscreen As Boolean
        Private _showEpg As Boolean
        Private _currentTime As String

        Public Shared Property ErrorString As String
            Get
                Return _errorString
            End Get
            Set(value As String)
                _errorString = value
                RaiseEvent StaticPropertyChanged(Nothing, New PropertyChangedEventArgs(NameOf(ErrorString)))
            End Set
        End Property

        Public Shared Event StaticPropertyChanged As PropertyChangedEventHandler

        Public Property MediaPlayer As MediaPlayer

        Public Property CurrentTime As String
            Get
                Return _currentTime
            End Get
            Set(value As String)
                _currentTime = value
                NotifyPropertyChanged("CurrentTime")
            End Set
        End Property

        Public Property NowPlaying As String
            Get
                Return _nowPlaying
            End Get
            Set(value As String)
                _nowPlaying = value
                NotifyPropertyChanged("NowPlaying")
            End Set
        End Property

        Public Property SelectedChannel As ChannelViewModel
            Get
                Return _selectedChannel
            End Get
            Set(value As ChannelViewModel)
                _selectedChannel = value
                If value IsNot Nothing Then ActivateChannel(value.StreamUrl)
                NotifyPropertyChanged("SelectedChannel")
            End Set
        End Property

        Public Property ShowChannelList As Boolean
            Get
                Return _showChannelList
            End Get
            Set(value As Boolean)
                _showChannelList = value
                My.Settings.ShowChannelList = value
                My.Settings.Save()
                NotifyPropertyChanged("ShowChannelList")
            End Set
        End Property

        Public Property ShowChannelListLogos As Boolean
            Get
                Return _showChannelListLogos
            End Get
            Set(value As Boolean)
                _showChannelListLogos = value
                My.Settings.ShowChannelListLogos = value
                My.Settings.Save()
                NotifyPropertyChanged("ShowChannelListLogos")
            End Set
        End Property

        Public Property ChannelList As ObservableCollection(Of ChannelViewModel)

        Public Property CurrentVolume As Integer
            Get
                Return _currentVolume
            End Get
            Set(value As Integer)
                If Not MediaPlayer.Volume.Equals(value) Then MediaPlayer.Volume = value
                _currentVolume = value
                NotifyPropertyChanged("CurrentVolume")
            End Set
        End Property

        Public Property Mute As Boolean
            Get
                Return _mute
            End Get
            Set(value As Boolean)
                _mute = value
                NotifyPropertyChanged("Mute")
            End Set
        End Property

        Public Property Fullscreen As Boolean
            Get
                Return _fullscreen
            End Get
            Set(value As Boolean)
                _fullscreen = value
                If value Then
                    For Each window As Window In System.Windows.Application.Current.Windows
                        Try
                            If window.DataContext Is Me Then
                                Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(window.Left, window.Top))
                                window.Top = currentScreen.WorkingArea.Top
                                window.Left = currentScreen.WorkingArea.Left
                                window.Width = currentScreen.Bounds.Width
                                window.Height = currentScreen.Bounds.Height
                                window.WindowStartupLocation = WindowStartupLocation.Manual
                                window.ResizeMode = ResizeMode.NoResize
                                window.WindowStyle = WindowStyle.None
                                window.WindowState = WindowState.Maximized
                            End If
                        Catch ex As Exception
                        End Try
                    Next
                Else
                    For Each window As Window In System.Windows.Application.Current.Windows
                        Try
                            If window.DataContext Is Me Then
                                Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(window.Left, window.Top))
                                window.Top = currentScreen.WorkingArea.Top
                                window.Left = currentScreen.WorkingArea.Left
                                window.Width = currentScreen.Bounds.Width
                                window.Height = currentScreen.Bounds.Height
                                window.ResizeMode = ResizeMode.CanResize
                                window.WindowStyle = WindowStyle.ThreeDBorderWindow
                            End If
                        Catch ex As Exception
                        End Try
                    Next
                End If
                NotifyPropertyChanged(Fullscreen)
            End Set
        End Property

        Public Property ShowOsd As Boolean
            Get
                Return _showOsd
            End Get
            Set(value As Boolean)
                _showOsd = value
                NotifyPropertyChanged("ShowOsd")
            End Set
        End Property

        Public Property ShowEpg As Boolean
            Get
                Return _showEpg
            End Get
            Set(value As Boolean)
                _showEpg = value
                If value Then CalculateEpg()
                NotifyPropertyChanged("ShowEpg")
            End Set
        End Property

        Private Sub CalculateEpg()
            Dim earliestEpg As EpgInfoViewModel = Nothing
            For Each channel In ChannelList
                Dim firstEpg = channel.EpgInfos.FirstOrDefault()
                If earliestEpg Is Nothing OrElse (firstEpg IsNot Nothing AndAlso firstEpg.StartTime < earliestEpg.StartTime) Then earliestEpg = firstEpg
            Next
            For Each channel In ChannelList
                Dim firstEpg = channel.EpgInfos.FirstOrDefault()
                If firstEpg.Title Is Nothing Then Continue For

                Dim difference As Long = firstEpg.StartTime - earliestEpg.StartTime
                channel.EpgInfos.Insert(0, New EpgInfoViewModel(difference))
            Next
        End Sub

        Public Property EditChannelListCommand As DelegateCommand
        Public Property MuteCommand As DelegateCommand

        Public Property VolumeSliderDoubleClickCommand As DelegateCommand
        Public Property ExportChannelListCommand As DelegateCommand
        Public Property ImportChannelListCommand As DelegateCommand
        Public Property MakeFullscreenCommand As DelegateCommand
        Public Property OpenSettingsCommand As DelegateCommand

        Public Property ShowEpgCommand As DelegateCommand

        Public Sub New()
            MediaPlayer = New MediaPlayer(libVLC)
            ChannelList = New ObservableCollection(Of ChannelViewModel)
            CurrentVolume = 100

            LoadChannelList()

            If Not ChannelList.Count.Equals(0) Then SelectedChannel = ChannelList.First()

            EditChannelListCommand = New DelegateCommand(AddressOf EditChannelListCommandExecute)
            MuteCommand = New DelegateCommand(AddressOf MuteCommandExecute)
            VolumeSliderDoubleClickCommand = New DelegateCommand(AddressOf VolumeSliderDoubleClickCommandExecute)
            ExportChannelListCommand = New DelegateCommand(AddressOf ExportChannelListCommandExecute)
            ImportChannelListCommand = New DelegateCommand(AddressOf ImportChannelListCommandExecute)
            MakeFullscreenCommand = New DelegateCommand(AddressOf MakeFullscreenCommandExecute)
            OpenSettingsCommand = New DelegateCommand(AddressOf OpenSettingsCommandExecute)
            ShowEpgCommand = New DelegateCommand(AddressOf ShowEpgCommandExecute)

            ShowChannelListLogos = My.Settings.ShowChannelListLogos
            ShowChannelList = My.Settings.ShowChannelList
        End Sub

        Private Sub ShowEpgCommandExecute()
            ShowEpg = True
        End Sub

        Private Sub OpenSettingsCommandExecute()
            Dim window As New SettingsView()
            window.ShowDialog()
        End Sub

        Private Sub MakeFullscreenCommandExecute()
            Dim secondaryScreen As Screen = Screen.AllScreens(1)
            For Each window As Window In System.Windows.Application.Current.Windows
                Try
                    If window.DataContext Is Me Then
                        window.Top = secondaryScreen.WorkingArea.Top
                        window.Left = secondaryScreen.WorkingArea.Left
                        window.Width = secondaryScreen.Bounds.Width
                        window.Height = secondaryScreen.Bounds.Height
                        window.WindowStartupLocation = WindowStartupLocation.Manual
                        window.ResizeMode = ResizeMode.NoResize
                        window.WindowStyle = WindowStyle.None
                        window.WindowState = WindowState.Maximized
                    End If
                Catch ex As Exception
                End Try
            Next
        End Sub

        Private Sub ImportChannelListCommandExecute()
            Dim oldSelectedChannelName As String = Nothing
            If SelectedChannel IsNot Nothing Then oldSelectedChannelName = SelectedChannel.DisplayName
            Dim Dialog = New Microsoft.Win32.OpenFileDialog()
            'Dialog.FileName = String.Format("{0:yyyy-mm-dd} Senderliste", Date.Now)
            Dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)
            Dialog.DefaultExt = ".m3u"
            Dialog.Filter = "Senderliste (.m3u)|*.m3u"

            Dim result As Boolean = Dialog.ShowDialog()


            If (result = True) Then
                Dim filename As String = Dialog.FileName
                Dim channelList As New List(Of Channel)

                Using fs As New FileStream(filename, FileMode.Open)
                    Using sw As New StreamReader(fs)
                        If Not sw.ReadLine().Equals("#EXTM3U") Then
                            MessageBox.Show("Ungültiges Dateiformat!", "Fehler")
                            Exit Sub
                        End If
                        Dim channel As Channel = Nothing
                        While Not sw.EndOfStream
                            Dim line As String = sw.ReadLine()
                            If line.StartsWith("#EXTM3U") Then Continue While
                            If line.StartsWith("#EXTVLCOPT") Then Continue While
                            If line.StartsWith("#EXTINF") Then
                                channel = New Channel(line.Replace("#EXTINF:0,", String.Empty))
                            End If
                            If line.StartsWith("rtsp://") Then
                                channel.StreamUrl = line
                                channelList.Add(channel)
                            End If
                        End While
                        If System.Windows.MessageBox.Show("Sollen die Sender der derzeitigen Senderliste behalten werden?", "Sender behalten?", MessageBoxButton.YesNo, MessageBoxImage.Question).Equals(MessageBoxResult.No) Then
                            My.Settings.ChannelList.Clear()
                        End If

                        For Each channel In channelList
                            My.Settings.ChannelList.Add(String.Format("""{0}"",""{1}""", channel.DisplayName, channel.StreamUrl))
                        Next
                        My.Settings.Save()
                        LoadChannelList()

                    End Using
                End Using
            End If
        End Sub

        Private Sub ExportChannelListCommandExecute()
            Dim Dialog = New Microsoft.Win32.SaveFileDialog()
            Dialog.FileName = String.Format("{0:yyyy-mm-dd} Senderliste", Date.Now)
            Dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)
            Dialog.DefaultExt = ".m3u"
            Dialog.Filter = "Senderliste (.m3u)|*.m3u"

            Dim result As Boolean = Dialog.ShowDialog()

            Dim filename As String
            If (result = True) Then
                filename = Dialog.FileName

                Using fs As New FileStream(filename, FileMode.CreateNew)
                    Using sw As New StreamWriter(fs)
                        sw.WriteLine("#EXTM3U")
                        For Each channel In ChannelList
                            sw.WriteLine(String.Format("#EXTINF:0,{0}", channel.DisplayName))
                            sw.WriteLine(channel.StreamUrl)
                        Next
                        sw.Flush()

                    End Using
                End Using
            End If
        End Sub

        Private Sub VolumeSliderDoubleClickCommandExecute()
            CurrentVolume = 100
        End Sub

        Private Sub LoadChannelList()
            '_currentProgramUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite)
            SelectedChannel = Nothing
            My.Settings.Reload()
            ChannelList.Clear()
            For Each channel In My.Settings.ChannelList
                Dim chArr As String() = channel.Split("""")
                Dim currentEpg As EpgInfo
                If My.Settings.UseTvHeadend Then currentEpg = NetworkHelper.GetCurrentEpgFromTvHeadend(chArr(1))
                ChannelList.Add(New ChannelViewModel(chArr(1), chArr(3), currentEpg))
            Next
            '_currentProgramUpdateTimer.Change(20000, 20000)
            SelectedChannel = ChannelList.FirstOrDefault()
        End Sub

        Private Sub UpdateVolume(sender As Object, e As MediaPlayerVolumeChangedEventArgs)
            Application.Current.Dispatcher.Invoke(Sub()
                                                      CurrentVolume = e.Volume * 100
                                                  End Sub)
        End Sub

        Private Sub UpdateState(sender As Object, e As MediaStateChangedEventArgs)

        End Sub

        Private Sub EditChannelListCommandExecute()
            Dim previousSelectedChannelName As String
            If SelectedChannel IsNot Nothing Then previousSelectedChannelName = SelectedChannel.DisplayName
            Dim vm As New EditChannelListViewModel()
            Dim window As New EditChannelListView()
            window.DataContext = vm
            window.ShowDialog()
            LoadChannelList()
            Dim newChannel As ChannelViewModel = ChannelList.FirstOrDefault(Function(x) x.DisplayName.Equals(previousSelectedChannelName))
            If Not ChannelList.Count.Equals(0) AndAlso newChannel Is Nothing Then
                SelectedChannel = ChannelList.First()
            ElseIf newChannel IsNot Nothing Then
                SelectedChannel = newChannel
            End If
        End Sub

        Private Sub MuteCommandExecute()
            MediaPlayer.ToggleMute()
            Mute = Not Mute
        End Sub

        Private Sub UpdateMeta(sender As Object, e As MediaMetaChangedEventArgs)
            Task.Run(Sub()
                         If SelectedChannel Is Nothing Then Exit Sub
                         Dim epgInfo As EpgInfo = Nothing

                         If e.MetadataType.Equals(MetadataType.NowPlaying) Then
                             Dim nowPl As String = MediaPlayer.Media.Meta(MetadataType.NowPlaying)
                             If nowPl IsNot Nothing AndAlso Not nowPl.Equals(NowPlaying) Then
                                 If My.Settings.UseTvHeadend Then
                                     epgInfo = NetworkHelper.GetCurrentEpgFromTvHeadend(SelectedChannel.DisplayName)
                                     Console.WriteLine("EPG von TVHeadend abgerufen")
                                 End If

                                 Application.Current.Dispatcher.BeginInvoke(Sub()
                                                                                If e.MetadataType.Equals(MetadataType.NowPlaying) Then
                                                                                    Console.WriteLine("Metadaten aktualisiert (NowPlaying)")

                                                                                    If Not String.IsNullOrWhiteSpace(nowPl) Then NowPlaying = nowPl

                                                                                    If My.Settings.UseTvHeadend Then
                                                                                        If SelectedChannel IsNot Nothing Then SelectedChannel.CurrentProgram = epgInfo
                                                                                        ShowOsd = True
                                                                                        _osdTimer.Change(5000, Timeout.Infinite)
                                                                                    End If
                                                                                End If
                                                                            End Sub)
                             End If
                         End If
                     End Sub)
        End Sub

        Private Sub ActivateChannel(streamUrl As String)
            streamUrl = streamUrl.Replace("rtsp://", "satip://")
            If SelectedChannel.CurrentProgram IsNot Nothing Then NowPlaying = SelectedChannel.CurrentProgram.Title & " "

            MediaPlayer.Play(New Media(libVLC, New Uri(streamUrl)))
            AddHandler MediaPlayer.Media.MetaChanged, AddressOf UpdateMeta
            AddHandler MediaPlayer.Media.StateChanged, AddressOf UpdateState
            AddHandler MediaPlayer.VolumeChanged, AddressOf UpdateVolume
            If MediaPlayer.Volume > 100 Then MediaPlayer.Volume = 100

            Task.Run(Sub()
                         If SelectedChannel Is Nothing Then Exit Sub
                         If My.Settings.UseTvHeadend Then
                             SelectedChannel.CurrentProgram = NetworkHelper.GetCurrentEpgFromTvHeadend(SelectedChannel.DisplayName)
                             ShowOsd = True
                         End If
                     End Sub)
        End Sub

        Private Sub OsdTimerCallback(state As Object)
            ShowOsd = False
        End Sub




    End Class
End Namespace