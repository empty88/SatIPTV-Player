Imports Rssdp
Imports SatIPTV.Classes

Namespace ViewModels.Models
    Public Class SatIpServerViewModel

        Public ReadOnly Property DisplayName As String
            Get
                Return String.Format("{0} ({1})", IpAddress, Details.FriendlyName)
            End Get
        End Property
        Public Property Usn As String
        Public Property DescriptionLocation As String
        Public Property IpAddress As String

        Public Property Details As SatIpServerDetails

        Public Sub New(discoveredDevice As DiscoveredSsdpDevice)
            Me.Usn = discoveredDevice.Usn
            Me.DescriptionLocation = discoveredDevice.DescriptionLocation.AbsoluteUri
            Me.IpAddress = discoveredDevice.DescriptionLocation.Host
        End Sub
    End Class
End Namespace