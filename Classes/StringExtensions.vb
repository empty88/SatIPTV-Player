Imports System.Runtime.CompilerServices

Namespace Classes
    Public Module StringExtensions
        <Extension()>
        Public Function ReplaceAny(ByVal input As String, ByVal needle As String(), replace As String)
            For Each value In needle
                input = input.Replace(value, replace)
            Next
            Return input
        End Function
    End Module
End Namespace
