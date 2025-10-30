Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.ComponentModel.DataAnnotations.Schema
Imports System.IO
Imports System.Threading
Imports System.Threading.Channels
Imports System.Transactions
Imports System.Windows.Forms
Imports LibVLCSharp.Shared
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
        Public Shared _epgStartTime As DateTime
        Public Shared _epgEndTime As DateTime

        Private _currentProgramUpdateTimer As New System.Threading.Timer(AddressOf CurrentProgramUpdateTimerCallback, Nothing, 2000, 10000)
        Private _epgFromEpgGrabberUpdateTimer As New System.Threading.Timer(AddressOf UpdateEpgGrabberEpgInfos, Nothing, 10000, 20000)

        Private _options As String() = {"--satip-multicast"}
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
        Private _epgVisibility As Visibility = Visibility.Hidden
        Private _currentProgramUpdateCancellationTokenSource As New CancellationTokenSource()

        Public Property ChannelList As ObservableCollection(Of ChannelViewModel)

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

        Public Property EpgReady As Boolean
            Get
                Return _epgReady
            End Get
            Set(value As Boolean)
                _epgReady = value
                NotifyPropertyChanged("EpgReady")
            End Set
        End Property

        Public Property EpgVisibility As Visibility
            Get
                Return _epgVisibility
            End Get
            Set(value As Visibility)
                _epgVisibility = value

            End Set
        End Property

        Public Property TimelineElements As ObservableCollection(Of TimelineElement)

        Public Property EditChannelListCommand As DelegateCommand
        Public Property MuteCommand As DelegateCommand

        Public Property VolumeSliderDoubleClickCommand As DelegateCommand
        Public Property ExportChannelListCommand As DelegateCommand
        Public Property ImportChannelListCommand As DelegateCommand
        Public Property OpenSettingsCommand As DelegateCommand


        Private Sub CurrentProgramUpdateTimerCallback(state As Object)
            If ChannelList Is Nothing Then Exit Sub
            Debug.WriteLine("Current Program Update")
            Task.Run(Sub()
                         For Each channel In ChannelList
                             If SelectedChannel Is Nothing OrElse _currentProgramUpdateCancellationTokenSource.Token.IsCancellationRequested Then Exit Sub

                             If My.Settings.UseTvHeadend Then
                                 Dim epg As EpgInfo = NetworkHelper.GetCurrentEpgFromTvHeadend(channel.DisplayName)
                                 If epg IsNot Nothing Then channel.CurrentProgram = epg
                             Else
                                 Dim epg As EpgInfo = DatabaseHelper.SelectCurrentEpgInfo(channel.DisplayName)
                                 If epg IsNot Nothing Then channel.CurrentProgram = epg
                             End If
                             If _currentProgramUpdateCancellationTokenSource.Token.IsCancellationRequested Then Exit Sub
                         Next
                     End Sub, _currentProgramUpdateCancellationTokenSource.Token)
        End Sub

        Private Sub UpdateEpgGrabberEpgInfos(state As Object)
            If Not My.Settings.UseTvHeadend Then
                For Each channel In ChannelList.ToList
                    Dim epgs As List(Of EpgInfo) = DatabaseHelper.SelectEpgInfos(channel.DisplayName)
                    If epgs Is Nothing Then Continue For
                    Dim channelEpgs As List(Of EpgInfoViewModelBase) = channel.EpgInfos.ToList
                    For Each epg In epgs.ToList
                        Dim foundEpg As EpgInfoViewModel = channelEpgs.FirstOrDefault(Function(x As EpgInfoViewModelBase)
                                                                                          If TypeOf x Is EpgInfoViewModel Then
                                                                                              Return DirectCast(x, EpgInfoViewModel).EventId.Equals(epg.EventId)
                                                                                          End If
                                                                                          Return False
                                                                                      End Function)
                        If foundEpg Is Nothing Then
                            Application.Current.Dispatcher.Invoke(Sub()
                                                                      For i = 0 To channel.EpgInfos.Count - 1
                                                                          If channel.EpgInfos(i).StartTime > epg.StartTime Then
                                                                              channel.EpgInfos.Insert(i, New EpgInfoViewModel(epg, channel))
                                                                              Exit Sub
                                                                          End If
                                                                      Next
                                                                      channel.EpgInfos.Add(New EpgInfoViewModel(epg, channel))
                                                                  End Sub)
                        Else
                            'foundEpg.StartTime = epg.StartTime
                            'foundEpg.EndTime = epg.EndTime
                            'foundEpg.Title = epg.Title
                            'foundEpg.SubTitle = epg.SubTitle
                            'foundEpg.Description = epg.Description
                        End If
                    Next
                Next
                CalculateEpg()
            End If
            'CalculateEpg()
        End Sub


        Public Sub New()
            MediaPlayer = New MediaPlayer(_libVLC)
            ChannelList = New ObservableCollection(Of ChannelViewModel)
            TimelineElements = New ObservableCollection(Of TimelineElement)
            CurrentVolume = 100
            Mute = False

            SQLitePCL.raw.SetProvider(New SQLitePCL.SQLite3Provider_e_sqlite3())
            DatabaseHelper.PurgeOldEpgInfos()

            If MediaPlayer.Mute Then MediaPlayer.ToggleMute()

            LoadChannelList()

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
            _currentProgramUpdateCancellationTokenSource.Cancel()
            Dim Window As New SettingsView()
            Window.Owner = GetWindow()
            Window.DataContext = New SettingsViewModel()
            If Window.ShowDialog() Then
                _currentProgramUpdateCancellationTokenSource = New CancellationTokenSource()
                LoadChannelList()
                _currentProgramUpdateTimer.Change(20000, 20000)

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
                Dim channelList As New List(Of Classes.Channel)

                Using fs As New FileStream(filename, FileMode.Open)
                    Using sw As New StreamReader(fs)
                        If Not sw.ReadLine().Equals("#EXTM3U") Then
                            MessageBox.Show("Ungültiges Dateiformat!", "Fehler")
                            Exit Sub
                        End If
                        Dim channel As Classes.Channel = Nothing
                        While Not sw.EndOfStream
                            Dim line As String = sw.ReadLine()
                            If line.StartsWith("#EXTM3U") Then Continue While
                            If line.StartsWith("#EXTVLCOPT") Then Continue While
                            If line.StartsWith("#EXTINF") Then
                                channel = New Classes.Channel(line.Replace("#EXTINF:0,", String.Empty))
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
            _currentProgramUpdateCancellationTokenSource.Cancel()
            Dim previousSelectedChannelName As String = String.Empty
            If SelectedChannel IsNot Nothing Then previousSelectedChannelName = SelectedChannel.DisplayName
            Dim vm As New EditChannelListViewModel()
            Dim window As New EditChannelListView()
            window.DataContext = vm
            window.Owner = GetWindow()
            If window.ShowDialog() Then
                _currentProgramUpdateCancellationTokenSource = New CancellationTokenSource()
                LoadChannelList()

                Application.Current.Dispatcher.Invoke(Sub()
                                                          Dim newChannel As ChannelViewModel = ChannelList.FirstOrDefault(Function(x) x.DisplayName.Equals(previousSelectedChannelName))
                                                          If Not ChannelList.Count.Equals(0) AndAlso newChannel Is Nothing Then
                                                              SelectedChannel = ChannelList.First()
                                                          ElseIf newChannel IsNot Nothing Then
                                                              SelectedChannel = newChannel
                                                          End If
                                                      End Sub)
                _currentProgramUpdateTimer.Change(20000, 20000)
            End If
        End Sub

        Private Sub MuteCommandExecute()
            MediaPlayer.ToggleMute()
            Mute = Not Mute
        End Sub

#End Region

        Public Function CalculateEpg() As Boolean
            Debug.WriteLine("Calculate EPG, {0} Channels loaded", Me.ChannelList.ToList.Count)
            EpgReady = False
            Dim earliestEpg As EpgInfoViewModelBase = Nothing
            Dim latestEpg As EpgInfoViewModelBase = Nothing
            Dim channelList As List(Of ChannelViewModel) = Me.ChannelList.ToList
            For Each channel In channelList
                Application.Current.Dispatcher.Invoke(Sub()
                                                          If TypeOf channel.EpgInfos.FirstOrDefault() Is NonEpgInfoViewModel Then channel.EpgInfos.Remove(channel.EpgInfos.FirstOrDefault())
                                                          If TypeOf channel.EpgInfos.LastOrDefault() Is NonEpgInfoViewModel Then channel.EpgInfos.Remove(channel.EpgInfos.LastOrDefault())
                                                      End Sub)

                Dim firstEpg = channel.EpgInfos.FirstOrDefault()
                If earliestEpg Is Nothing OrElse (firstEpg IsNot Nothing AndAlso firstEpg.StartTime < earliestEpg.StartTime) Then earliestEpg = firstEpg
                Dim lastEpg = channel.EpgInfos.LastOrDefault()
                If latestEpg Is Nothing OrElse (lastEpg IsNot Nothing AndAlso lastEpg.EndTime > latestEpg.EndTime) Then latestEpg = lastEpg
            Next

            For Each channel In channelList
                Dim firstEpg = channel.EpgInfos.FirstOrDefault()
                Dim lastEpg = channel.EpgInfos.LastOrDefault()
                Dim differenceStart As Long
                Dim differenceEnd As Long = 0
                If latestEpg Is Nothing Then Continue For
                If firstEpg Is Nothing Then
                    differenceStart = latestEpg.EndTime - earliestEpg.StartTime
                Else
                    differenceStart = firstEpg.StartTime - earliestEpg.StartTime
                End If
                If lastEpg IsNot Nothing Then differenceEnd = latestEpg.EndTime - lastEpg.EndTime

                Application.Current.Dispatcher.Invoke(Sub()
                                                          If Not differenceStart.Equals(0) Then channel.EpgInfos.Insert(0, New NonEpgInfoViewModel(differenceStart))
                                                          If Not differenceEnd.Equals(0) Then channel.EpgInfos.Add(New NonEpgInfoViewModel(differenceEnd))
                                                      End Sub)
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
                    Application.Current.Dispatcher.Invoke(Sub()
                                                              TimelineElements.Add(New TimelineElement(elementTime.ToString("HH:mm")))
                                                              Debug.WriteLine("Timeline element added: " & elementTime.ToString("HH:mm"))
                                                          End Sub)
                Next
            End If
            EpgReady = True
            Return True
        End Function

        Public Sub LoadChannelList()
            Dim channelVm As ChannelViewModel = Nothing
            Dim epgs As List(Of EpgInfo)
            SelectedChannel = Nothing
            My.Settings.Reload()
            ChannelList.Clear()
            Task.Run(Sub()
                         For Each channel In My.Settings.ChannelList
                             Dim chArr As String() = channel.Split("""")
                             'TODO: bestehende Einträge aktualiseren
                             Application.Current.Dispatcher.Invoke(Sub()
                                                                       channelVm = New ChannelViewModel(chArr(1), chArr(3))
                                                                       ChannelList.Add(channelVm)
                                                                   End Sub)
                         Next

                         For Each channelVm In ChannelList
                             If _currentProgramUpdateCancellationTokenSource.Token.IsCancellationRequested Then Exit Sub
                             If My.Settings.UseTvHeadend Then
                                 epgs = NetworkHelper.GetAllEpgFromTvHeadend(channelVm.DisplayName)
                             Else
                                 epgs = DatabaseHelper.SelectEpgInfos(channelVm.DisplayName)
                             End If
                             'TODO: bestehende Einträge aktualiseren
                             Application.Current.Dispatcher.Invoke(Sub()
                                                                       channelVm.EpgInfos.AddRange(epgs?.Select(Function(x) New EpgInfoViewModel(x, channelVm)))
                                                                       If Not channelVm.EpgInfos.Count.Equals(0) Then channelVm.CurrentProgram = DirectCast(channelVm.EpgInfos.FirstOrDefault(), EpgInfoViewModel).EpgInfo
                                                                   End Sub)
                             Debug.WriteLine("EPG for " & channelVm.DisplayName & " loaded")
                         Next

                         SelectedChannel = ChannelList.FirstOrDefault()
                     End Sub).ContinueWith(Sub()
                                               CalculateEpg()
                                           End Sub)
        End Sub

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
                                     Debug.WriteLine("Aktuelles EPG von TVHeadend abgerufen")
                                 Else
                                     epgInfo = DatabaseHelper.SelectCurrentEpgInfo(SelectedChannel.DisplayName)
                                     Debug.WriteLine("Aktuelles EPG von Grabber-DB abgerufen")
                                 End If

                                 Application.Current.Dispatcher.BeginInvoke(Sub()
                                                                                If e.MetadataType.Equals(MetadataType.NowPlaying) Then
                                                                                    Debug.WriteLine("Metadaten aktualisiert (NowPlaying)")

                                                                                    If Not String.IsNullOrWhiteSpace(nowPl) Then NowPlaying = nowPl

                                                                                    If SelectedChannel IsNot Nothing Then SelectedChannel.CurrentProgram = epgInfo
                                                                                End If
                                                                            End Sub)
                             End If
                         End If
                     End Sub)
        End Sub

        Private Sub ActivateChannel(streamUrl As String)
            If SelectedChannel.CurrentProgram IsNot Nothing Then NowPlaying = SelectedChannel.CurrentProgram.Title & " "

            If MediaPlayer.IsPlaying Then
                EpgGrabber.Shutdown()
            Else

            End If
            Dim streamResult As RtspHelper.StreamResult = RtspHelper.RequestMulticastStream(streamUrl)
            If streamResult Is Nothing Then
                MainViewModel.ErrorString = "Wiedergabe fehlgeschlagen"
                Exit Sub
            End If
            Using Media = New Media(_libVLC, New Uri(String.Format("rtp://{0}:{1}/stream={2}", streamResult.ServerIp, streamResult.ServerPort, streamResult.StreamId)))
                MediaPlayer.Play(Media)
            End Using
            If MediaPlayer.Media IsNot Nothing Then
                AddHandler MediaPlayer.Media.MetaChanged, AddressOf UpdateMeta
            End If

            AddHandler MediaPlayer.VolumeChanged, AddressOf UpdateVolume
            AddHandler MediaPlayer.EncounteredError, AddressOf ErrorOccured
            AddHandler MediaPlayer.Stopped, AddressOf ErrorOccured
            If MediaPlayer.Volume > 100 Then MediaPlayer.Volume = 100

            Task.Run(Sub()
                         If SelectedChannel Is Nothing Then Exit Sub
                         If My.Settings.UseTvHeadend Then
                             SelectedChannel.CurrentProgram = NetworkHelper.GetCurrentEpgFromTvHeadend(SelectedChannel.DisplayName)
                         Else
                             EpgGrabber.GrabUdp(streamResult.ServerIp, streamResult.ServerPort)
                         End If
                     End Sub)
        End Sub

        Private Sub ErrorOccured(sender As Object, e As EventArgs)
            MainViewModel.ErrorString = "Wiedergabe fehlgeschlagen"
        End Sub

        Public Sub Unload()
            EpgGrabber.Shutdown()
            MediaPlayer.Stop()
            MediaPlayer.Dispose()
            _libVLC.Dispose()
        End Sub

    End Class
End Namespace