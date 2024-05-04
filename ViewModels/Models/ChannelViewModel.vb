
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.Net
Imports System.Threading
Imports SatIPTV.Classes
Imports SatIPTV.Helper

Namespace ViewModels.Models
    Public Class ChannelViewModel
        Inherits ViewModelBase

        Private _currentProgram As EpgInfo

        Private progressUpdateTimer As New Timer(AddressOf ProgressUpdate, Nothing, 1000, 1000)

        Private _currentProgramProgress As Integer
        Public Property DisplayName As String

        Public Property Logo As BitmapImage

        Public Property CurrentProgram As EpgInfo
            Get
                Return _currentProgram
            End Get
            Set(value As EpgInfo)
                _currentProgram = value
                NotifyPropertyChanged("CurrentProgram")
            End Set
        End Property

        Public Property CurrentProgramProgress As Integer
            Get
                Return _currentProgramProgress
            End Get
            Set(value As Integer)
                _currentProgramProgress = value
                NotifyPropertyChanged("CurrentProgramProgress")
            End Set
        End Property

        Public Property EpgInfos As ObservableCollection(Of EpgInfoViewModel)

        Public Property StreamUrl As String

        Public Property ServerIp As IPAddress

        Public Sub New()
            Me.EpgInfos = New ObservableCollection(Of EpgInfoViewModel)
        End Sub

        Public Sub New(displayName As String, streamUrl As String)
            Me.New()
            Me.DisplayName = displayName
            Me.StreamUrl = streamUrl
            Dim removeStrings As String() = {" ", ".", "-", "_", "ä", "Ä", "ö", "Ö", "ü", "Ü", "ß"}
            Try
                Me.Logo = New BitmapImage(New Uri(String.Format("pack://application:,,,/picons/{0}.png", displayName _
                .Replace("+", "plus") _
                                                                                                                    .ReplaceAny(removeStrings, String.Empty) _
                                                                                                                    .ToLower)))
            Catch ex As Exception
            End Try
            Dim epg As New List(Of EpgInfo)
            Task.Run(Sub()
                         If My.Settings.UseTvHeadend Then epg = NetworkHelper.GetAllEpgFromTvHeadend(displayName)
                     End Sub).ContinueWith(Sub()
                                               Application.Current.Dispatcher.Invoke(Sub()
                                                                                         EpgInfos.AddRange(epg.Select(Function(x) New EpgInfoViewModel(x)))
                                                                                         CurrentProgram = epg.FirstOrDefault()
                                                                                     End Sub)

                                           End Sub)
        End Sub
        Public Sub New(displayName As String, streamUrl As String, currentProgram As EpgInfo)
            Me.New(displayName, streamUrl)
            Me.CurrentProgram = currentProgram
        End Sub

        Private Sub ProgressUpdate(state As Object)
            If CurrentProgram Is Nothing Then Exit Sub
            CurrentProgramProgress = CurrentProgram.Progress
        End Sub
    End Class
End Namespace
