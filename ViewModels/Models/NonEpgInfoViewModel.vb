Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports SatIPTV.Helper
Imports SatIPTV.ViewModels

Namespace ViewModels.Models
    Public Class NonEpgInfoViewModel
        Inherits ViewModelBase

        Public Property StartTime As String

        Public Property EndTime As String

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

        Public Property Title As String

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
