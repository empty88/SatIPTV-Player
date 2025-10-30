Namespace Classes
    Public Class EpgService
        Public Property ServiceId As Long
        Public Property ServiceName As String
        Public Property ProviderName As String

        Public Sub New(serviceId As Long, serviceName As String, providerName As String)
            Me.ServiceId = serviceId
            Me.ServiceName = serviceName
            Me.ProviderName = providerName
        End Sub
    End Class
End Namespace
