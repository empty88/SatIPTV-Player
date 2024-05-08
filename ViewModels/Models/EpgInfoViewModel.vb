Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports SatIPTV.Helper
Imports SatIPTV.ViewModels

Namespace ViewModels.Models
    Public Class EpgInfoViewModel
        Inherits ViewModelBase

        Public Property EventId As Long

        Public Property NextEventId As Long
        Public Property StartTime As String

        Public Property EndTime As String

        Public Property TimeSpanString As String

        Public ReadOnly Property Duration As Integer
            Get
                Return (GetLocalEndTime() - GetLocalStartTime()).TotalSeconds
            End Get
        End Property

        Public ReadOnly Property Width As Integer
            Get
                Return Duration / 10
            End Get
        End Property

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


        Public Property Title As String

        Public Property SubTitle As String

        Public Property Description As String

        Public Property Channel As ChannelViewModel

        Public ReadOnly Property Tooltip As String
            Get
                Return String.Format("{0} {1}" & vbNewLine & "{2}" & vbNewLine & "{3}", TimeSpanString, Title, SubTitle, Description)
            End Get
        End Property

        Public Property EpgInfo As EpgInfo


        Public Sub New()

        End Sub

        Public Sub New(epgInfo As EpgInfo, channel As ChannelViewModel)
            Me.New()
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

        Public Sub New(duration As Long)
            Me.StartTime = 0
            Me.EndTime = duration
            Me.Title = "Keine EPG-Daten vorhanden"
        End Sub

        Public Function GetLocalStartTime() As Date
            Dim startTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
            Return startTime.AddSeconds(Me.StartTime).ToLocalTime()
        End Function

        Public Function GetLocalEndTime() As Date
            Dim endtime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
            Return endtime.AddSeconds(Me.EndTime).ToLocalTime()
        End Function
    End Class
End Namespace
