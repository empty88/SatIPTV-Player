Imports SatIPTV.ViewModels

Public Class TimelineElement
    Inherits ViewModelBase

    Public Property Text As String
    Public Property Margin As Thickness

    Public Sub New()

    End Sub

    Public Sub New(text As String)
        Me.Text = text
    End Sub
End Class
