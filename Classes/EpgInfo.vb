Imports Newtonsoft.Json

Public Class EpgInfo
    <JsonProperty("eventId")>
    Public Property EventId As Long
    <JsonProperty("nextEventId")>
    Public Property NextEventId As Long
    <JsonProperty("start")>
    Public Property StartTime As Long
    <JsonProperty("stop")>
    Public Property EndTime As Long

    <JsonProperty("channelName")>
    Public Property ChannelName As String

    <JsonProperty("title")>
    Public Property Title As String
    <JsonProperty("subtitle")>
    Public Property SubTitle As String
    <JsonProperty("description")>
    Public Property Description As String

    Public ReadOnly Property Progress As Integer
        Get
            Dim startTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
            startTime = startTime.AddSeconds(Me.StartTime).ToLocalTime()

            Dim endTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
            endTime = endTime.AddSeconds(Me.EndTime).ToLocalTime()


            Return (DateTime.UtcNow - startTime).TotalSeconds / (endTime - startTime).TotalSeconds
        End Get
    End Property

    Public ReadOnly Property TimeSpanString As String
        Get
            Dim startTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
            startTime = startTime.AddSeconds(Me.StartTime).ToLocalTime()

            Dim endTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
            endTime = endTime.AddSeconds(Me.EndTime).ToLocalTime()

            Return String.Format("{0} - {1}", startTime.ToString("HH:mm"), endTime.ToString("HH:mm"))
        End Get
    End Property

    Public Sub New()

    End Sub

End Class
