Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports Prism.Commands


Namespace ViewModels

    Public Class ViewModelBase
        Implements INotifyPropertyChanged

        Public Event PropertyChanged As PropertyChangedEventHandler _
            Implements INotifyPropertyChanged.PropertyChanged

        Public Sub NotifyPropertyChanged(ByVal info As String)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(info))
        End Sub

        Private _closeCommand As ICommand
        Private _closing As Boolean

        Public ReadOnly Property CloseCommand As ICommand
            Get
                If _closeCommand Is Nothing Then
                    _closeCommand = New DelegateCommand(AddressOf CloseCommandExecute)
                End If
                Return _closeCommand
            End Get
        End Property

        Public Sub CloseCommandExecute()
            If _closing Then Exit Sub
            Dim window As Window = GetWindow()
            If window IsNot Nothing Then
                _closing = True
                window.Close()
            End If
            _closing = False
        End Sub

        Public Function GetWindow() As Window
            For Each window As Window In System.Windows.Application.Current.Windows
                Try
                    If window.DataContext Is Me Then
                        Return window
                    End If
                Catch ex As Exception
                End Try
            Next
            Return Nothing
        End Function


        Public Sub CloseWindow()
            CloseCommandExecute()
        End Sub

    End Class
End Namespace
