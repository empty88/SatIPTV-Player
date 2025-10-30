Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports SatIPTV.Helper
Imports SatIPTV.ViewModels

Namespace ViewModels.Models
    Public Class NonEpgInfoViewModel
        Inherits EpgInfoViewModelBase


        Public Sub New(duration As Long)
            MyBase.New(duration)
        End Sub

    End Class
End Namespace
