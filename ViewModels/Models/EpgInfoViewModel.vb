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
                Dim startTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                startTime = startTime.AddSeconds(Me.StartTime).ToLocalTime()

                Dim endTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                endTime = endTime.AddSeconds(Me.EndTime).ToLocalTime()

                Return (endTime - startTime).TotalSeconds / 5
            End Get
        End Property

        Public ReadOnly Property Width As Integer
            Get
                Return Duration
            End Get
        End Property


        Public Property Title As String

        Public Property SubTitle As String

        Public Property Description As String

        Public ReadOnly Property Tooltip As String
            Get
                Return Title & vbNewLine & SubTitle
            End Get
        End Property


        Public Sub New()

        End Sub

        Public Sub New(epgInfo As EpgInfo)
            Me.New()
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
            Me.EndTime = duration / 5
        End Sub
    End Class
End Namespace
