Imports System.Runtime.InteropServices

Namespace Helper
    Public Class ApplicationHelper


        <DllImport("User32.dll", CharSet:=CharSet.Auto, CallingConvention:=CallingConvention.StdCall)>
        Private Overloads Shared Function GetForegroundWindow() As IntPtr
        End Function

        <DllImport("User32.dll", CharSet:=CharSet.Auto, CallingConvention:=CallingConvention.StdCall)>
        Private Overloads Shared Function GetWindowThreadProcessId(handle As IntPtr, ByRef processId As Integer) As Int32
        End Function

        Public Shared Function ApplicationIsActivated()
            Dim activatedHandle = GetForegroundWindow()
            If activatedHandle.Equals(IntPtr.Zero) Then
                Return False
            End If

            Dim processId As Integer = Process.GetCurrentProcess().Id
            Dim activeProcessId As Integer
            GetWindowThreadProcessId(activatedHandle, activeProcessId)

            Return activeProcessId.Equals(processId)
        End Function

    End Class
End Namespace
