Imports SatIPTV.ViewModels

Public Class EpgInfoViewModelBase
    Inherits ViewModelBase

    Public Property StartTime As String

    Public Property EndTime As String

    Public Property Title As String

    Public ReadOnly Property Duration As Integer
        Get
            Return (GetLocalEndTime() - GetLocalStartTime()).TotalSeconds
        End Get
    End Property

    Public ReadOnly Property Width As Integer
        Get
            If Duration.Equals(0) Then Return 0
            Return Duration / 10
        End Get
    End Property

    Public Function GetLocalStartTime() As Date
        Dim startTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        Return startTime.AddSeconds(Me.StartTime).ToLocalTime()
    End Function

    Public Function GetLocalEndTime() As Date
        Dim endtime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        Return endtime.AddSeconds(Me.EndTime).ToLocalTime()
    End Function

    Public Sub New()

    End Sub

    Public Sub New(duration As Long)
        Me.New()
        Me.StartTime = 0
        Me.EndTime = duration
        Me.Title = "Keine EPG-Daten vorhanden"
    End Sub
End Class
