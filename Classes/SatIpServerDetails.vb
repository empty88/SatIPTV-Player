Namespace Classes
    Public Class SatIpServerDetails
        Public FriendlyName As String
        Public SatIpCap As String
        Public SatIpM3u As String
        Public Channels As List(Of Channel)

        Public Sub New(friendlyName As String, satIpCap As String, satIpM3u As String, channels As List(Of Channel))
            Me.FriendlyName = friendlyName
            Me.SatIpCap = satIpCap
            Me.SatIpM3u = satIpM3u
            Me.Channels = channels
        End Sub
    End Class
End Namespace
