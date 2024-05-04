Imports System.Collections.Concurrent
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports Prism.Commands
Imports Prism.Modularity
Imports Rssdp
Imports SatIPTV.Helper
Imports SatIPTV.ViewModels
Imports SatIPTV.ViewModels.Models

Public Class EditChannelListViewModel
    Inherits ViewModelBase

    Private _selectedChannel As ChannelViewModel
    Private _serverList As List(Of SatIpServerViewModel)
    Private _selectedServer As SatIpServerViewModel
    Private _selectedServerChannel As ChannelViewModel
    Private _loading As Boolean
    Public Property ChannelList As ObservableCollection(Of ChannelViewModel)

    Public Property SelectedChannel As ChannelViewModel
        Get
            Return _selectedChannel
        End Get
        Set(value As ChannelViewModel)
            _selectedChannel = value
            NotifyPropertyChanged("SelectedChannel")
            RemoveChannelCommand.RaiseCanExecuteChanged()
            MoveChannelUpCommand.RaiseCanExecuteChanged()
            MoveChannelDownCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Public Property SelectedServerChannel As ChannelViewModel
        Get
            Return _selectedServerChannel
        End Get
        Set(value As ChannelViewModel)
            _selectedServerChannel = value
            NotifyPropertyChanged("SelectedServerChannel")
        End Set
    End Property

    Public Property ServerList As List(Of SatIpServerViewModel)
        Get
            Return _serverList
        End Get
        Set(value As List(Of SatIpServerViewModel))
            _serverList = value
            NotifyPropertyChanged("ServerList")
        End Set
    End Property

    Public Property Loading As Boolean
        Get
            Return _loading
        End Get
        Set(value As Boolean)
            _loading = value
            NotifyPropertyChanged("Loading")
            RefreshServerListCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Public Property ServerChannelList As ObservableCollection(Of ChannelViewModel)

    Public Property SelectedServer As SatIpServerViewModel
        Get
            Return _selectedServer
        End Get
        Set(value As SatIpServerViewModel)
            _selectedServer = value
            ServerChannelList.Clear()
            If _selectedServer.Details.Channels IsNot Nothing Then
                ServerChannelList.AddRange(_selectedServer.Details.Channels.Select(Function(x) New ChannelViewModel(x.DisplayName, x.StreamUrl)).ToList())
            ElseIf _selectedServer.Details.SatIpCap.StartsWith("DVBS") Then
                ServerChannelList.AddRange(NetworkHelper.GetPublicChannelList(My.Settings.DVBSChannelListUrl).Select(Function(x) New ChannelViewModel(x.DisplayName, x.StreamUrl)).ToList())
            End If
            NotifyPropertyChanged("SelectedServer")
        End Set
    End Property

    Public Property AddChannelCommand As DelegateCommand
    Public Property AddChannelFromServerCommand As DelegateCommand(Of Object)
    Public Property RemoveChannelCommand As DelegateCommand

    Public Property MoveChannelUpCommand As DelegateCommand
    Public Property MoveChannelDownCommand As DelegateCommand
    Public Property SaveCommand As DelegateCommand

    Public Property RefreshServerListCommand As DelegateCommand

    Public Sub New()
        ChannelList = New ObservableCollection(Of ChannelViewModel)
        ServerChannelList = New ObservableCollection(Of ChannelViewModel)

        ChannelList.Clear()
        For Each channel In My.Settings.ChannelList
            Dim chArr As String() = channel.Split("""")
            ChannelList.Add(New ChannelViewModel(chArr(1), chArr(3)))
        Next

        AddChannelCommand = New DelegateCommand(AddressOf AddChannelCommandExecute)
        AddChannelFromServerCommand = New DelegateCommand(Of Object)(AddressOf AddChannelFromServerCommandExecute)
        RemoveChannelCommand = New DelegateCommand(AddressOf RemoveChannelCommandExecute, AddressOf RemoveChannelCommandCanExecute)
        MoveChannelUpCommand = New DelegateCommand(AddressOf MoveChannelUpCommandExecute, AddressOf MoveChannelUpCommandCanExecute)
        MoveChannelDownCommand = New DelegateCommand(AddressOf MoveChannelDownCommandExecute, AddressOf MoveChannelDownCommandCanExecute)
        SaveCommand = New DelegateCommand(AddressOf SaveCommandExecute)
        RefreshServerListCommand = New DelegateCommand(AddressOf RefreshServerListCommandExecute, AddressOf RefreshServerListCommandCanExecute)

        RefreshServerListCommand.Execute()
    End Sub

    Private Sub RefreshServerListCommandExecute()
        Loading = True
        Task.Run(Sub()
                     ServerList = NetworkHelper.DiscoverSatIpServers().Select(Function(x) New SatIpServerViewModel(x)).ToList()
                 End Sub).ContinueWith(Sub()
                                           For Each server In ServerList
                                               server.Details = NetworkHelper.GetSatIpServerDetails(server.DescriptionLocation)
                                           Next
                                           Loading = False
                                       End Sub)
    End Sub

    Private Function RefreshServerListCommandCanExecute() As Boolean
        Return Not Loading
    End Function

    Private Sub MoveChannelDownCommandExecute()
        ChannelList.Move(ChannelList.IndexOf(SelectedChannel), ChannelList.IndexOf(SelectedChannel) + 1)
        MoveChannelUpCommand.RaiseCanExecuteChanged()
        MoveChannelDownCommand.RaiseCanExecuteChanged()
    End Sub

    Private Function MoveChannelDownCommandCanExecute() As Boolean
        Return SelectedChannel IsNot Nothing AndAlso Not ChannelList.IndexOf(SelectedChannel).Equals(ChannelList.Count - 1)
    End Function

    Private Function MoveChannelUpCommandCanExecute() As Boolean
        Return SelectedChannel IsNot Nothing AndAlso Not ChannelList.IndexOf(SelectedChannel).Equals(0)
    End Function

    Private Sub MoveChannelUpCommandExecute()
        ChannelList.Move(ChannelList.IndexOf(SelectedChannel), ChannelList.IndexOf(SelectedChannel) - 1)
        MoveChannelUpCommand.RaiseCanExecuteChanged()
        MoveChannelDownCommand.RaiseCanExecuteChanged()
    End Sub

    Private Sub AddChannelFromServerCommandExecute()
        Dim displayName As String = SelectedServerChannel.DisplayName
        Dim streamUrl As String = SelectedServerChannel.StreamUrl.Replace("sat.ip", SelectedServer.IpAddress)
        ChannelList.Add(New ChannelViewModel(displayName, streamUrl))
    End Sub

    Private Sub SaveCommandExecute()
        My.Settings.ChannelList.Clear()
        For Each channel In ChannelList
            If channel.DisplayName.Equals("Sendername eingeben") OrElse channel.StreamUrl.Equals("Stream-Url eingeben") Then Continue For
            My.Settings.ChannelList.Add(String.Format("""{0}"",""{1}""", channel.DisplayName, channel.StreamUrl))
        Next

        My.Settings.Save()
        CloseWindow()
    End Sub

    Private Function RemoveChannelCommandCanExecute() As Boolean
        Return SelectedChannel IsNot Nothing
    End Function

    Private Sub RemoveChannelCommandExecute()
        ChannelList.Remove(SelectedChannel)
    End Sub

    Private Sub AddChannelCommandExecute()
        ChannelList.Add(New ChannelViewModel("Sendername eingeben", "Stream-Url eingeben"))
    End Sub
End Class
