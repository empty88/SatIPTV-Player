Imports System.Security
Imports System.Security.Cryptography
Imports System.Text

Namespace Helper
    Public Class EncryptionHelper

        Private Shared entropy As Byte() = Encoding.Unicode.GetBytes("SaLtY tVHeadeNd daTa")
        Public Shared Function EncryptString(input As SecureString) As String

            Dim encryptedData As Byte() = ProtectedData.Protect(Encoding.Unicode.GetBytes(ToInsecureString(input)), entropy, DataProtectionScope.CurrentUser)
            Return Convert.ToBase64String(encryptedData)

        End Function
        Public Shared Function DecryptString(encryptedData As String) As SecureString
            Try
                Dim decryptedData As Byte() = ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), entropy, DataProtectionScope.CurrentUser)
                Return ToSecureString(Encoding.Unicode.GetString(decryptedData))
            Catch ex As Exception

            End Try
            Return New SecureString()
        End Function

        Public Shared Function ToSecureString(input As String) As SecureString

            Dim secure As New SecureString()
            For Each c In input
                secure.AppendChar(c)
            Next
            secure.MakeReadOnly()
            Return secure
        End Function

        Public Shared Function ToInsecureString(Input As SecureString) As String
            Dim returnValue As String = String.Empty
            Dim ptr As IntPtr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(Input)

            Try
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr)
            Catch ex As Exception

            Finally
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr)
            End Try
            Return returnValue
        End Function
    End Class
End Namespace