
Imports System.ComponentModel
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports System.Windows.Input
Imports LibVLCSharp.Shared
Imports SatIPTV.Classes
Imports SatIPTV.ViewModels

Namespace Views
    Class MainWindow
        Private _scrollMousePoint As Windows.Point
        Private _scrollChannelMousePoint As Windows.Point
        Private _hOff As Double = 1
        Private _vOff As Double = 1
        Private _osdTimer As New System.Threading.Timer(AddressOf OsdTimerCallback, Nothing, Timeout.Infinite, Timeout.Infinite)
        Private _clockTimer As New System.Threading.Timer(AddressOf ClockTimerCallBack, Nothing, 1000, 100)
        Private _previousWindowTop As Double
        Private _previousWindowLeft As Double
        Private _previousWindowWidth As Double
        Private _previousWindowHeight As Double
        Private _epgScrolledToNow As Boolean

        Public Sub New()

            ' Dieser Aufruf ist für den Designer erforderlich.
            InitializeComponent()

            ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.

            AddHandler EpgView.LayoutUpdated, AddressOf EpgViewScrollToNow
            AddHandler KeyboardListener.KeyDown, AddressOf GlobalKeyDown
        End Sub

        Private Sub ToggleFullscreen()
            Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(Me.Left, Me.Top))

            If Me.WindowStyle.Equals(WindowStyle.None) Then
                ' disable fullscreen
                Me.Top = _previousWindowTop
                Me.Left = _previousWindowLeft
                Me.Width = _previousWindowWidth
                Me.Height = _previousWindowHeight
                Me.ResizeMode = ResizeMode.CanResize
                Me.WindowStyle = WindowStyle.ThreeDBorderWindow
            Else
                ' enable fullscreen
                _previousWindowTop = Me.Top
                _previousWindowLeft = Me.Left
                _previousWindowHeight = Me.Height
                _previousWindowWidth = Me.Width
                Me.Top = currentScreen.WorkingArea.Top
                Me.Left = currentScreen.WorkingArea.Left
                Me.Width = currentScreen.Bounds.Width
                Me.Height = currentScreen.Bounds.Height

                Me.WindowStartupLocation = WindowStartupLocation.Manual
                Me.ResizeMode = ResizeMode.NoResize
                Me.WindowStyle = WindowStyle.None
            End If
        End Sub

#Region "timer callbacks"
        Private Sub OsdTimerCallback(state As Object)
            Application.Current.Dispatcher.Invoke(Sub() EpgBorder.Visibility = Visibility.Collapsed)
        End Sub



        Private Sub ClockTimerCallBack(state As Object)
            Application.Current.Dispatcher.Invoke(Sub()
                                                      Me.CurrentTime.Header = DateTime.Now.ToString("HH:mm:ss")
                                                      CurrentTimeBar.Margin = New Thickness((Date.Now - MainViewModel._epgStartTime).TotalSeconds / 10, 0, 0, 0)
                                                  End Sub)
        End Sub
#End Region

#Region "event handler"

        Private Sub GlobalKeyDown(Key As Keys)
            If Key.Equals(Keys.F2) Then
                If EpgView.IsVisible Then
                    EpgView.Visibility = Visibility.Hidden
                Else
                    _epgScrolledToNow = False
                    EpgView.Visibility = Visibility.Visible
                End If
            ElseIf Key.Equals(Keys.F1) Then
                DirectCast(DataContext, MainViewModel).ShowChannelList = Not DirectCast(DataContext, MainViewModel).ShowChannelList
            ElseIf Key.Equals(Keys.F12) Then
                ToggleFullscreen()
            End If
        End Sub

        Private Sub EpgViewScrollToNow(sender As Object, e As EventArgs)
            If Not _epgScrolledToNow AndAlso Not EpgProgramScrollView.ActualWidth.Equals(0) Then
                EpgProgramScrollView.ScrollToHorizontalOffset(((Date.Now - MainViewModel._epgStartTime).TotalSeconds / 10) - EpgProgramScrollView.ActualWidth / 2)
                _epgScrolledToNow = True
            End If
        End Sub

        Private Sub OverlayGrid_MouseMove(sender As Object, e As Input.MouseEventArgs)
            Application.Current.Dispatcher.Invoke(Sub() If My.Settings.UseTvHeadend AndAlso Not EpgView.IsVisible Then EpgBorder.Visibility = Visibility.Visible)
            _osdTimer.Change(5000, Timeout.Infinite)
        End Sub

        Private Sub MenuItemAlwaysForeground_Checked(sender As Object, e As RoutedEventArgs)
            Me.Topmost = True
        End Sub

        Private Sub MenuItemAlwaysForeground_Unchecked(sender As Object, e As RoutedEventArgs)
            Me.Topmost = False
        End Sub

        Private Sub VolumeSlider_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
            DirectCast(sender, Slider).Value = 100
        End Sub

        Private Sub OverlayGrid_MouseDown(sender As Object, e As MouseButtonEventArgs)
            If e.ClickCount.Equals(2) Then
                ToggleFullscreen()
            End If
        End Sub

        Private Sub MenuItemFullscreen_Checked(sender As Object, e As RoutedEventArgs)
            Dim currentScreen As Screen = Screen.FromPoint(New System.Drawing.Point(Me.Left, Me.Top))
            _previousWindowTop = Me.Top
            _previousWindowLeft = Me.Left
            _previousWindowHeight = Me.Height
            _previousWindowWidth = Me.Width
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

            Me.Top = _previousWindowTop
            Me.Left = _previousWindowLeft
            Me.Width = _previousWindowWidth
            Me.Height = _previousWindowHeight
        End Sub

        Private Sub ErrorDisplay_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
            MainViewModel.ErrorString = Nothing
        End Sub
        Private Sub EpgBorder_MouseEnter(sender As Object, e As Input.MouseEventArgs)
            If EpgDescriptionBlock.RenderSize.Height + EpgTitle.RenderSize.Height + EpgSubTitle.RenderSize.Height > EpgBorder.RenderSize.Height Then
                EpgBorder.Height = EpgTitle.RenderSize.Height + EpgSubTitle.RenderSize.Height + EpgDescriptionBlock.RenderSize.Height + 40
            End If
        End Sub

        Private Sub EpgBorder_MouseLeave(sender As Object, e As Input.MouseEventArgs) Handles EpgBorder.MouseLeave
            ' restore default height
            EpgBorder.Height = 150
        End Sub
        Private Sub MainWindow_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
            My.Settings.Save()
            _clockTimer.Dispose()
            DirectCast(Me.DataContext, MainViewModel).Unload()
        End Sub

        Private Sub EpgProgramScrollView_ScrollChanged(sender As Object, e As ScrollChangedEventArgs) Handles EpgProgramScrollView.ScrollChanged
            TimelineScrollView.ScrollToHorizontalOffset(e.HorizontalOffset)
            EpgDayTitle.Text = MainViewModel._epgStartTime.AddSeconds(e.HorizontalOffset * 10).AddSeconds(EpgProgramScrollView.ActualWidth / 2 * 10).ToString("dddd, dd.MM.yy")
        End Sub

        Private Sub MenuItemEpg_Click(sender As Object, e As RoutedEventArgs)
            If EpgView.IsVisible Then
                EpgView.Visibility = Visibility.Hidden
            Else
                _epgScrolledToNow = False
                EpgView.Visibility = Visibility.Visible
                EpgBorder.Visibility = Visibility.Hidden
            End If
        End Sub

        Private Sub EpgItem_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
            Debug.WriteLine("EpgItem down")
            _scrollMousePoint = e.GetPosition(EpgProgramScrollView)
            _scrollChannelMousePoint = e.GetPosition(EpgChannelScrollView)
            _hOff = EpgProgramScrollView.HorizontalOffset
            _vOff = EpgChannelScrollView.VerticalOffset
            sender.CaptureMouse()
        End Sub

        Private Sub EpgItem_PreviewMouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
            Debug.WriteLine("EpgItem up")
            If Math.Abs(_scrollMousePoint.X - e.GetPosition(EpgProgramScrollView).X) <= 2 AndAlso Math.Abs(_scrollMousePoint.Y - e.GetPosition(EpgProgramScrollView).Y) <= 2 Then
                If sender.dataContext.Channel IsNot Nothing Then
                    Debug.WriteLine("EpgItem clicked")
                    DirectCast(Me.DataContext, MainViewModel).SelectedChannel = sender.dataContext.Channel
                    EpgView.Visibility = Visibility.Hidden
                End If
            End If
            sender.ReleaseMouseCapture()
        End Sub

        Private Sub EpgItem_PreviewMouseMove(sender As Object, e As Input.MouseEventArgs)
            If sender.IsMouseCaptured Then
                Debug.WriteLine("EpgItem Drag Move")
                EpgProgramScrollView.ScrollToHorizontalOffset(_hOff + (_scrollMousePoint.X - e.GetPosition(EpgProgramScrollView).X))
                EpgChannelScrollView.ScrollToVerticalOffset(_vOff + (_scrollChannelMousePoint.Y - e.GetPosition(EpgChannelScrollView).Y))
            End If
            MouseTimeBar.Margin = New Thickness(EpgProgramScrollView.HorizontalOffset + e.GetPosition(EpgProgramScrollView).X, 0, 0, 0)
        End Sub

        Private Sub ChannelListVideoGridSplitter_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
            DirectCast(Me.DataContext, MainViewModel).ChannelListWidth = New GridLength(240)
        End Sub

        Private Sub EpgNowButton_Click(sender As Object, e As RoutedEventArgs)
            EpgProgramScrollView.ScrollToHorizontalOffset(((Date.Now - MainViewModel._epgStartTime).TotalSeconds / 10) - EpgProgramScrollView.ActualWidth / 2)
        End Sub

        Private Sub EpgPrev24hButton_Click(sender As Object, e As RoutedEventArgs)
            EpgProgramScrollView.ScrollToHorizontalOffset(EpgProgramScrollView.HorizontalOffset - ((24 * 60 * 60) / 10))
        End Sub

        Private Sub EpgNext24hButton_Click(sender As Object, e As RoutedEventArgs)
            EpgProgramScrollView.ScrollToHorizontalOffset(EpgProgramScrollView.HorizontalOffset + ((24 * 60 * 60) / 10))
        End Sub

        Private Sub EpgPrimeButton_Click(sender As Object, e As RoutedEventArgs)
            Dim prime As New DateTime(Date.Now.Year, Date.Now.Month, Date.Now.Day, 20, 0, 0)
            EpgProgramScrollView.ScrollToHorizontalOffset(((prime - MainViewModel._epgStartTime).TotalSeconds / 10))
        End Sub

        Private Sub MenuItemInfo_Click(sender As Object, e As RoutedEventArgs)
            Dim window As New AboutView
            window.Owner = Me
            window.ShowDialog()
        End Sub

        Private Sub MenuItemSettings_Click(sender As Object, e As RoutedEventArgs)
            Dim window As New SettingsView()
            window.Owner = Me
            window.ShowDialog()
        End Sub
#End Region
    End Class
End Namespace