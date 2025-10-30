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


            Return (DateTime.Now - startTime).TotalSeconds
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

    Public Sub New(eventId As Long, nextEventId As Long, startTime As Long, endTime As Long, channelName As String, title As String, subTitle As String, description As String)
        Me.EventId = eventId
        Me.NextEventId = nextEventId
        Me.StartTime = startTime
        Me.EndTime = endTime
        Me.ChannelName = channelName
        Me.Title = title
        Me.SubTitle = subTitle
        Me.Description = description
    End Sub

    Public Overrides Function ToString() As String
        Dim startTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        startTime = startTime.AddSeconds(Me.StartTime).ToLocalTime()

        Dim endTime As New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        endTime = endTime.AddSeconds(Me.EndTime).ToLocalTime()
        Dim time As String = String.Format("{0} - {1}", startTime.ToString("dd.MM. HH:mm"), endTime.ToString("HH:mm"))
        Return String.Format("{0}: {1} - {2}", EventId, time, Title)
    End Function

End Class
