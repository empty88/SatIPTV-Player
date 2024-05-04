
Imports System.Threading
Imports System.Windows.Forms
Imports System.Windows.Input
Imports SatIPTV.ViewModels

Namespace Views
    Class MainWindow
        Private scrollMousePoint = New Point()
        Private hOff As Double = 1
        Private _osdTimer As New System.Threading.Timer(AddressOf OsdTimerCallback, Nothing, Timeout.Infinite, Timeout.Infinite)
        Private _clockTimer As New System.Threading.Timer(AddressOf ClockTimerCallBack, Nothing, 1000, 100)
        Private previousTop As Double
        Private previousLeft As Double
        Private previousWidth As Double
        Private previousHeight As Double

        Public Sub New()

            ' Dieser Aufruf ist für den Designer erforderlich.
            InitializeComponent()

            ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.


        End Sub

        Private Sub VolumeSlider_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
            DirectCast(sender, Slider).Value = 100
        End Sub

        Private Sub EpgScrollView_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles EpgScrollView.PreviewMouseLeftButtonDown
            scrollMousePoint = e.GetPosition(EpgScrollView)
            hOff = EpgScrollView.HorizontalOffset
            EpgScrollView.CaptureMouse()
        End Sub

        Private Sub EpgScrollView_PreviewMouseMove(sender As Object, e As Input.MouseEventArgs) Handles EpgScrollView.PreviewMouseMove
            If EpgScrollView.IsMouseCaptured Then EpgScrollView.ScrollToHorizontalOffset(hOff + (scrollMousePoint.x - e.GetPosition(EpgScrollView).X))
        End Sub

        Private Sub EpgScrollView_PreviewMouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles EpgScrollView.PreviewMouseLeftButtonUp
            EpgScrollView.ReleaseMouseCapture()
        End Sub

        Private Sub MainWindow_KeyDown(sender As Object, e As Input.KeyEventArgs) Handles Me.KeyDown
            If e.Key.Equals(Key.Escape) Then
                DirectCast(Me.DataContext, MainViewModel).ShowEpg = False
            ElseIf e.Key.Equals(Key.F1) Then
                Me.WindowStyle = WindowStyle.ThreeDBorderWindow
                Me.WindowState = WindowState.Normal
                Me.ResizeMode = ResizeMode.CanResize
                Me.Topmost = False
            End If
        End Sub

        Private Sub OverlayGrid_MouseDown(sender As Object, e As MouseButtonEventArgs)
            Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(Me.Left, Me.Top))

            If e.ClickCount.Equals(2) Then
                If Me.WindowStyle.Equals(WindowStyle.None) Then
                    ' Vollbild aus
                    Me.Top = previousTop
                    Me.Left = previousLeft
                    Me.Width = previousWidth
                    Me.Height = previousHeight
                    Me.ResizeMode = ResizeMode.CanResize
                    Me.WindowStyle = WindowStyle.ThreeDBorderWindow
                Else
                    ' Vollbild an
                    previousTop = Me.Top
                    previousLeft = Me.Left
                    previousHeight = Me.Height
                    previousWidth = Me.Width
                    Me.Top = currentScreen.WorkingArea.Top
                    Me.Left = currentScreen.WorkingArea.Left
                    Me.Width = currentScreen.Bounds.Width
                    Me.Height = currentScreen.Bounds.Height

                    Me.WindowStartupLocation = WindowStartupLocation.Manual
                    Me.ResizeMode = ResizeMode.NoResize
                    Me.WindowStyle = WindowStyle.None
                End If

            End If

        End Sub

        Private Sub OsdTimerCallback(state As Object)
            Application.Current.Dispatcher.Invoke(Sub() EpgBorder.Visibility = Visibility.Collapsed)
        End Sub

        Private Sub OverlayGrid_MouseMove(sender As Object, e As Input.MouseEventArgs)
            Application.Current.Dispatcher.Invoke(Sub() If My.Settings.UseTvHeadend AndAlso Not DirectCast(Me.DataContext, MainViewModel).ShowEpg Then EpgBorder.Visibility = Visibility.Visible)
            _osdTimer.Change(5000, Timeout.Infinite)
        End Sub

        Private Sub MenuItemAlwaysForeground_Checked(sender As Object, e As RoutedEventArgs)
            Me.Topmost = True
        End Sub

        Private Sub MenuItemAlwaysForeground_Unchecked(sender As Object, e As RoutedEventArgs)
            Me.Topmost = False
        End Sub

        Private Sub ErrorDisplay_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
            MainViewModel.ErrorString = String.Empty
        End Sub

        Private Sub MenuItemFullscreen_Checked(sender As Object, e As RoutedEventArgs)
            Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(Me.Left, Me.Top))
            previousTop = Me.Top
            previousLeft = Me.Left
            previousHeight = Me.Height
            previousWidth = Me.Width
            Me.Top = currentScreen.WorkingArea.Top
            Me.Left = currentScreen.WorkingArea.Left
            Me.Width = currentScreen.Bounds.Width
            Me.Height = currentScreen.Bounds.Height

            Me.WindowStartupLocation = WindowStartupLocation.Manual
            Me.ResizeMode = ResizeMode.NoResize
            Me.WindowStyle = WindowStyle.None
        End Sub

        Private Sub MenuItemFullscreen_Unchecked(sender As Object, e As RoutedEventArgs)
            Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(Me.Left, Me.Top))
            Me.ResizeMode = ResizeMode.CanResize
            Me.WindowStyle = WindowStyle.ThreeDBorderWindow

            Me.Top = previousTop
            Me.Left = previousLeft
            Me.Width = previousWidth
            Me.Height = previousHeight

        End Sub

        Private Sub EpgDescriptionBlock_SizeChanged(sender As Object, e As SizeChangedEventArgs)
            If DirectCast(sender, TextBlock).RenderSize.Height > 200 Then

            End If
        End Sub

        Private Sub EpgBorder_MouseEnter(sender As Object, e As Input.MouseEventArgs)
            If EpgDescriptionBlock.RenderSize.Height > 90 Then
                EpgBorder.Height = 120 + EpgDescriptionBlock.RenderSize.Height
            End If
        End Sub

        Private Sub EpgBorder_MouseLeave(sender As Object, e As Input.MouseEventArgs) Handles EpgBorder.MouseLeave
            EpgBorder.Height = 150
        End Sub

        Private Sub ClockTimerCallBack(state As Object)
            Application.Current.Dispatcher.Invoke(Sub() Me.CurrentTime.Header = DateTime.Now.ToString("HH:mm:ss"))
        End Sub
    End Class
End Namespace