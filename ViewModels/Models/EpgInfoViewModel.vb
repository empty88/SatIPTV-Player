Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports SatIPTV.Helper
Imports SatIPTV.ViewModels

Namespace ViewModels.Models
    Public Class EpgInfoViewModel
        Inherits EpgInfoViewModelBase

        Public Property EventId As Long

        Public Property NextEventId As Long

        Public Property TimeSpanString As String


        Public ReadOnly Property IsLive As Boolean
            Get
                Return Date.Now > GetLocalStartTime() AndAlso Date.Now < GetLocalEndTime()
            End Get
        End Property
        Public ReadOnly Property IsPrime As Boolean
            Get
                Return GetLocalStartTime().Hour.Equals(20) AndAlso GetLocalStartTime().Minute > 10 AndAlso GetLocalStartTime().Minute < 20
            End Get
        End Property


        Public Property SubTitle As String

        Public Property Description As String

        Public Property Channel As ChannelViewModel

        Public ReadOnly Property Tooltip As String
            Get
                Return String.Format("{0} {1}" & vbCrLf & "{2}" & vbCrLf & "{3}", TimeSpanString, Title, SubTitle, Description)
            End Get
        End Property

        Public Property EpgInfo As EpgInfo

        Public Sub New(epgInfo As EpgInfo, channel As ChannelViewModel)
            MyBase.New()
            Me.Channel = channel
            Me.EpgInfo = epgInfo
            Me.EventId = epgInfo.EventId
            Me.NextEventId = epgInfo.NextEventId
            Me.StartTime = epgInfo.StartTime
            Me.EndTime = epgInfo.EndTime
            Me.Title = epgInfo.Title
            Me.SubTitle = epgInfo.SubTitle
            Me.Description = epgInfo.Description
            Me.TimeSpanString = epgInfo.TimeSpanString
        End Sub





        Public Sub Update()
            NotifyPropertyChanged("IsLive")
        End Sub
    End Class
End Namespace
