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
        Public Shared _epgStartTime As DateTime
        Public Shared _epgEndTime As DateTime

        Private _currentProgramUpdateTimer As New System.Threading.Timer(AddressOf CurrentProgramUpdateTimerCallback, Nothing, 20000, 20000)

        Private Sub CurrentProgramUpdateTimerCallback(state As Object)
            Task.Run(Sub()
                         For Each channel In ChannelList
                             If SelectedChannel Is Nothing Then Exit Sub
                             If My.Settings.UseTvHeadend Then channel.CurrentProgram = NetworkHelper.GetCurrentEpgFromTvHeadend(channel.DisplayName)
                         Next
                     End Sub)
        End Sub

        Private _options As String() = {}
        Private _libVLC As New LibVLC(_options)
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
        Private _channelListWidth As GridLength
        Private _epgReady As Boolean

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
                If value IsNot Nothing Then
                    ActivateChannel(value.StreamUrl)
                End If
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
                If value Then
                    ChannelListWidth = New GridLength(My.Settings.ChannelListWidth)
                Else
                    ChannelListWidth = New GridLength(0)
                End If
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

        Public Property ChannelListWidth As GridLength
            Get
                Return _channelListWidth
            End Get
            Set(value As GridLength)
                _channelListWidth = value
                If Not value.Value.Equals(0) Then My.Settings.ChannelListWidth = value.Value
                NotifyPropertyChanged("ChannelListWidth")
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

        Public Property ShowOsd As Boolean
            Get
                Return _showOsd
            End Get
            Set(value As Boolean)
                _showOsd = value
                If value Then _osdTimer.Change(5000, Timeout.Infinite)
                NotifyPropertyChanged("ShowOsd")
            End Set
        End Property

        Public Property EpgReady As Boolean
            Get
                Return _epgReady
            End Get
            Set(value As Boolean)
                _epgReady = value
                NotifyPropertyChanged("EpgReady")
            End Set
        End Property

        Public Property TimelineElements As List(Of TimelineElement)

        Public Property EditChannelListCommand As DelegateCommand
        Public Property MuteCommand As DelegateCommand

        Public Property VolumeSliderDoubleClickCommand As DelegateCommand
        Public Property ExportChannelListCommand As DelegateCommand
        Public Property ImportChannelListCommand As DelegateCommand
        Public Property OpenSettingsCommand As DelegateCommand

        Public Sub New()
            MediaPlayer = New MediaPlayer(_libVLC)
            ChannelList = New ObservableCollection(Of ChannelViewModel)
            TimelineElements = New List(Of TimelineElement)
            CurrentVolume = 100
            Mute = False

            If MediaPlayer.Mute Then MediaPlayer.ToggleMute()

            LoadChannelList().ContinueWith(Sub()
                                               CalculateEpg()
                                           End Sub)

            If Not ChannelList.Count.Equals(0) Then SelectedChannel = ChannelList.First()

            EditChannelListCommand = New DelegateCommand(AddressOf EditChannelListCommandExecute)
            MuteCommand = New DelegateCommand(AddressOf MuteCommandExecute)
            VolumeSliderDoubleClickCommand = New DelegateCommand(AddressOf VolumeSliderDoubleClickCommandExecute)
            ExportChannelListCommand = New DelegateCommand(AddressOf ExportChannelListCommandExecute)
            ImportChannelListCommand = New DelegateCommand(AddressOf ImportChannelListCommandExecute)
            OpenSettingsCommand = New DelegateCommand(AddressOf OpenSettingsCommandExecute)

            ShowChannelListLogos = My.Settings.ShowChannelListLogos
            ShowChannelList = My.Settings.ShowChannelList
            If ShowChannelList Then ChannelListWidth = New GridLength(My.Settings.ChannelListWidth)
        End Sub


#Region "Commands"

        Private Sub OpenSettingsCommandExecute()
            _currentProgramUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite)
            Dim window As New SettingsView()
            window.Owner = GetWindow()
            window.DataContext = New SettingsViewModel()
            If window.ShowDialog() Then
                LoadChannelList().ContinueWith(Sub()
                                                   CalculateEpg()
                                                   _currentProgramUpdateTimer.Change(20000, 20000)
                                               End Sub)
            End If
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
                        LoadChannelList().ContinueWith(Sub()
                                                           CalculateEpg()
                                                       End Sub)

                    End Using
                End Using
            End If
        End Sub

        Private Sub ExportChannelListCommandExecute()
            Dim Dialog = New Microsoft.Win32.SaveFileDialog()
            Dialog.FileName = String.Format("{0:yyyy-MM-dd} Senderliste", Date.Now)
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

        Private Sub EditChannelListCommandExecute()
            _currentProgramUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite)
            Dim previousSelectedChannelName As String = String.Empty
            If SelectedChannel IsNot Nothing Then previousSelectedChannelName = SelectedChannel.DisplayName
            Dim vm As New EditChannelListViewModel()
            Dim window As New EditChannelListView()
            window.DataContext = vm
            window.Owner = GetWindow()
            If window.ShowDialog() Then
                LoadChannelList().ContinueWith(Sub()
                                                   CalculateEpg()

                                                   Application.Current.Dispatcher.Invoke(Sub()
                                                                                             Dim newChannel As ChannelViewModel = ChannelList.FirstOrDefault(Function(x) x.DisplayName.Equals(previousSelectedChannelName))
                                                                                             If Not ChannelList.Count.Equals(0) AndAlso newChannel Is Nothing Then
                                                                                                 SelectedChannel = ChannelList.First()
                                                                                             ElseIf newChannel IsNot Nothing Then
                                                                                                 SelectedChannel = newChannel
                                                                                             End If
                                                                                         End Sub)
                                                   _currentProgramUpdateTimer.Change(20000, 20000)
                                               End Sub)
            End If
        End Sub

        Private Sub MuteCommandExecute()
            MediaPlayer.ToggleMute()
            Mute = Not Mute
        End Sub

#End Region

        Public Function CalculateEpg() As Boolean
            EpgReady = False
            Dim earliestEpg As EpgInfoViewModel = Nothing
            Dim latestEpg As EpgInfoViewModel = Nothing
            For Each channel In ChannelList
                Dim firstEpg = channel.EpgInfos.FirstOrDefault()
                If earliestEpg Is Nothing OrElse (firstEpg IsNot Nothing AndAlso firstEpg.StartTime < earliestEpg.StartTime) Then earliestEpg = firstEpg
                Dim lastEpg = channel.EpgInfos.LastOrDefault()
                If latestEpg Is Nothing OrElse (lastEpg IsNot Nothing AndAlso lastEpg.EndTime > latestEpg.EndTime) Then latestEpg = lastEpg
            Next

            For Each channel In ChannelList
                Dim firstEpg = channel.EpgInfos.FirstOrDefault()
                If firstEpg Is Nothing OrElse firstEpg.StartTime.Equals("0") Then Return False

                Dim difference As Long = firstEpg.StartTime - earliestEpg.StartTime
                Application.Current.Dispatcher.Invoke(Sub() channel.EpgInfos.Insert(0, New EpgInfoViewModel(difference)))
            Next
            If earliestEpg Is Nothing Then Return False
            _epgStartTime = earliestEpg.GetLocalStartTime()
            _epgEndTime = latestEpg.GetLocalEndTime()
            If TimelineElements.Count.Equals(0) Then
                Dim firstTime As DateTime
                If _epgStartTime.Minute >= 30 Then
                    firstTime = New Date(_epgStartTime.Year, _epgStartTime.Month, _epgStartTime.Day, _epgStartTime.Hour, 30, 0)
                    Application.Current.Dispatcher.Invoke(Sub() TimelineElements.Add(New TimelineElement(String.Format("{0:d2}:{1:d2}", _epgStartTime.Hour, 30)) With {.Margin = New Thickness(((_epgStartTime.Minute - 30) * 60 + _epgStartTime.Second) / -10, 0, 0, 0)}))
                Else
                    firstTime = New Date(_epgStartTime.Year, _epgStartTime.Month, _epgStartTime.Day, _epgStartTime.Hour, 0, 0)
                    Application.Current.Dispatcher.Invoke(Sub() TimelineElements.Add(New TimelineElement(String.Format("{0:d2}:{1:d2}", _epgStartTime.Hour, 0)) With {.Margin = New Thickness((_epgStartTime.Minute * 60 + _epgStartTime.Second) / -10, 0, 0, 0)}))
                End If
                Dim elementTime As Date = firstTime
                For i = 0 To (_epgEndTime - _epgStartTime).TotalMinutes / 30
                    elementTime = elementTime.AddMinutes(30)
                    Application.Current.Dispatcher.Invoke(Sub() TimelineElements.Add(New TimelineElement(elementTime.ToString("HH:mm"))))
                Next
            End If
            EpgReady = True
            Return True
        End Function

        Public Function LoadChannelList() As Task
            Dim channelVm As ChannelViewModel = Nothing
            Dim epgs As List(Of EpgInfo)
            SelectedChannel = Nothing
            My.Settings.Reload()
            ChannelList.Clear()
            Return Task.Run(Sub()
                                For Each channel In My.Settings.ChannelList
                                    Dim chArr As String() = channel.Split("""")
                                    Application.Current.Dispatcher.Invoke(Sub() channelVm = New ChannelViewModel(chArr(1), chArr(3)))

                                    If My.Settings.UseTvHeadend Then
                                        epgs = NetworkHelper.GetAllEpgFromTvHeadend(chArr(1))
                                        Application.Current.Dispatcher.Invoke(Sub()
                                                                                  channelVm.EpgInfos.AddRange(epgs.Select(Function(x) New EpgInfoViewModel(x, channelVm)))
                                                                                  If Not channelVm.EpgInfos.Count.Equals(0) Then channelVm.CurrentProgram = channelVm.EpgInfos.FirstOrDefault().EpgInfo
                                                                              End Sub)
                                    End If

                                    Application.Current.Dispatcher.Invoke(Sub() ChannelList.Add(channelVm))
                                    Console.WriteLine(chArr(1) & " geladen")
                                Next

                                SelectedChannel = ChannelList.FirstOrDefault()
                            End Sub)
        End Function

        Private Sub UpdateVolume(sender As Object, e As MediaPlayerVolumeChangedEventArgs)
            Application.Current.Dispatcher.Invoke(Sub()
                                                      CurrentVolume = e.Volume * 100
                                                  End Sub)
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
            Using media = New Media(_libVLC, New Uri(streamUrl))
                MediaPlayer.Play(media)

            End Using
            AddHandler MediaPlayer.Media.MetaChanged, AddressOf UpdateMeta
            AddHandler MediaPlayer.VolumeChanged, AddressOf UpdateVolume
            AddHandler MediaPlayer.EncounteredError, AddressOf ErrorOccured
            If MediaPlayer.Volume > 100 Then MediaPlayer.Volume = 100

            Task.Run(Sub()
                         If SelectedChannel Is Nothing Then Exit Sub
                         If My.Settings.UseTvHeadend Then
                             SelectedChannel.CurrentProgram = NetworkHelper.GetCurrentEpgFromTvHeadend(SelectedChannel.DisplayName)
                             ShowOsd = True
                         End If
                     End Sub)
        End Sub

        Private Sub ErrorOccured(sender As Object, e As EventArgs)
            MainViewModel.ErrorString = "Wiedergabe fehlgeschlagen"
        End Sub

        Private Sub OsdTimerCallback(state As Object)
            ShowOsd = False
        End Sub

        Public Sub Unload()
            MediaPlayer.Stop()
            MediaPlayer.Dispose()
            _libVLC.Dispose()
        End Sub

    End Class
End Namespace