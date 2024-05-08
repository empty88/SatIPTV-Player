Imports System.Runtime.CompilerServices
Imports System
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Windows.Input
Imports System.Windows.Forms


Namespace Classes


    Public Class KeyboardListener

        <DllImport("User32.dll", CharSet:=CharSet.Auto, CallingConvention:=CallingConvention.StdCall)>
        Private Overloads Shared Function SetWindowsHookEx(ByVal idHook As Integer, ByVal HookProc As KBDLLHookProc, ByVal hInstance As IntPtr, ByVal wParam As Integer) As IntPtr
        End Function
        <DllImport("User32.dll", CharSet:=CharSet.Auto, CallingConvention:=CallingConvention.StdCall)>
        Friend Overloads Shared Function CallNextHookEx(ByVal idHook As Integer, ByVal nCode As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As Integer
        End Function
        <DllImport("User32.dll", CharSet:=CharSet.Auto, CallingConvention:=CallingConvention.StdCall)>
        Friend Overloads Shared Function UnhookWindowsHookEx(ByVal idHook As Integer) As Boolean
        End Function
        'Public WH_KEYBOARD_LL As Integer = 13
        'Public WM_KEYDOWN As Integer = 256
        'Public WM_KEYUP As Integer = 257
        Private Const WH_KEYBOARD_LL As Integer = 13
        Private Const HC_ACTION As Integer = 0
        Private Const WM_KEYDOWN = &H100
        Private Const WM_KEYUP = &H101
        Private Const WM_SYSKEYDOWN = &H104
        Private Const WM_SYSKEYUP = &H105
        Private Const VK_NUMPAD0 = &H60
        Private Const VK_NUMPAD1 = &H61
        Private Const VK_NUMPAD2 = &H62
        Private Const VK_NUMPAD3 = &H63
        Private Const VK_NUMPAD4 = &H64
        Private Const VK_NUMPAD5 = &H65
        Private Const VK_NUMPAD6 = &H66
        Private Const VK_NUMPAD7 = &H67
        Private Const VK_NUMPAD8 = &H68
        Private Const VK_NUMPAD9 = &H69

        <StructLayout(LayoutKind.Sequential)>
        Private Structure KBDLLHOOKSTRUCT
            Public vkCode As UInt32
            Public scanCode As UInt32
            Public flags As KBDLLHOOKSTRUCTFlags
            Public time As UInt32
            Public dwExtraInfo As UIntPtr
        End Structure

        <Flags()>
        Private Enum KBDLLHOOKSTRUCTFlags As UInt32
            LLKHF_EXTENDED = &H1
            LLKHF_INJECTED = &H10
            LLKHF_ALTDOWN = &H20
            LLKHF_UP = &H80
        End Enum

        Public Shared Event KeyDown(ByVal Key As Keys)
        Public Shared Event KeyUp(ByVal Key As Keys)


        Private Delegate Function KBDLLHookProc(ByVal nCode As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As Integer

        Private KBDLLHookProcDelegate As KBDLLHookProc = New KBDLLHookProc(AddressOf KeyboardProc)
        Private HHookID As IntPtr = IntPtr.Zero


        Private Function KeyboardProc(ByVal nCode As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As Integer
            If (nCode = HC_ACTION) Then
                Dim struct As KBDLLHOOKSTRUCT
                Select Case wParam
                    Case WM_KEYDOWN, WM_SYSKEYDOWN
                        RaiseEvent KeyDown(CType(CType(Marshal.PtrToStructure(lParam, struct.GetType()), KBDLLHOOKSTRUCT).vkCode, Keys))
                    Case WM_KEYUP, WM_SYSKEYUP
                        RaiseEvent KeyUp(CType(CType(Marshal.PtrToStructure(lParam, struct.GetType()), KBDLLHOOKSTRUCT).vkCode, Keys))
                End Select
            End If
            Return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam)
        End Function

        Public Sub SetHook()
            Try
                HHookID = SetWindowsHookEx(WH_KEYBOARD_LL, KBDLLHookProcDelegate, Marshal.GetHINSTANCE(System.Reflection.Assembly.GetExecutingAssembly.GetModules()(0)), 0)
                If HHookID = IntPtr.Zero Then
                    Throw New Exception("Could not set keyboard hook")
                End If
            Catch ex As Exception
                MsgBox("keyboard hook Error: " & ex.Message)
            End Try
        End Sub

        Public Sub UnHook()
            If Not HHookID = IntPtr.Zero Then
                UnhookWindowsHookEx(HHookID)
            End If
        End Sub

    End Class

End Namespace