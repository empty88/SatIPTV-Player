Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text

Namespace Helper
    Public Class RtspHelper

        Private Shared _cSeq As Integer = 0
        Private Shared _session As String
        Private Shared _keepAlive As Boolean
        Private Shared _streamUri As Uri
        Private Shared _networkStream As NetworkStream
        Private Shared _streamId As Integer

        Public Shared Function RequestMulticastStream(streamUrl As String) As StreamResult
            If _networkStream IsNot Nothing Then
                RtspHelper.TeardownCommand(_streamUri)
                '_networkStream.Dispose()
            End If
            Dim streamUri As New Uri(streamUrl)
            streamUri = New Uri(String.Format("{0}://{1}:{2}{3}", streamUri.Scheme, streamUri.Host, If(streamUri.Port.Equals(-1), 554, streamUri.Port), streamUri.PathAndQuery))
            Dim tcpClient As New TcpClient(streamUri.Host, If(streamUri.Port.Equals(-1), 554, streamUri.Port))
            tcpClient.ReceiveBufferSize = 64 * 1024
            tcpClient.NoDelay = True

            _networkStream = tcpClient.GetStream()
            Dim socket As New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            socket.Bind(New IPEndPoint(IPAddress.Any, 0))

            Dim clientPort = DirectCast(socket.LocalEndPoint, IPEndPoint).Port
            socket.Dispose()
            Dim setupResult As RtspHelper.SetupResult
            setupResult = RtspHelper.SetupCommand(streamUri, clientPort)
            _streamId = setupResult.StreamId
            If Not setupResult.Success OrElse String.IsNullOrWhiteSpace(setupResult.ServerIp) Then
                Debug.WriteLine("Setup not succeeded")
                Return Nothing
            End If
            RtspHelper.DescribeCommand(streamUri)

            If Not RtspHelper.PlayCommand(New Uri(String.Format("rtsp://{0}:{1}", streamUri.Host, If(streamUri.Port.Equals(-1), 554, streamUri.Port))), setupResult.StreamId) Then Return Nothing
            Dim result As StreamResult = New StreamResult() With {
                    .StreamId = setupResult.StreamId,
                    .ServerIp = IPAddress.Parse(setupResult.ServerIp),
                    .ServerPort = setupResult.ServerPort
                }
            _streamUri = streamUri
            Return result
        End Function

        Public Shared Function Shutdown() As Boolean
            _keepAlive = False
            Return TeardownCommand(_streamUri)
        End Function

        Private Shared Function SetupCommand(streamUri As Uri, clientPort As Integer) As SetupResult
            Dim readbuffer(16 * 1024) As Byte
            Dim writebuffer(8 * 1024) As Byte
            Dim session As String = String.Empty
            Dim streamId As Integer = 0
            Dim serverPort As Integer
            Dim serverIp As String = String.Empty
            Dim result As New SetupResult()
            Dim response As New StringBuilder()
            _streamUri = streamUri
            _keepAlive = False

            Dim queryBuilder = New StringBuilder(512)
            queryBuilder.AppendFormat("{0} {1} RTSP/1.0" & vbCrLf, "SETUP", streamUri.ToString)
            queryBuilder.AppendFormat("CSeq: {0}" & vbCrLf, _cSeq)
            queryBuilder.AppendFormat("Transport: {0}{1}-{2}" & vbCrLf, "RTP/AVP;multicast;client_port=", clientPort, clientPort + 1)
            queryBuilder.AppendFormat("User-Agent: {0}" & vbCrLf, "SatIPTV")
            queryBuilder.AppendLine()

            writebuffer = Encoding.ASCII.GetBytes(queryBuilder.ToString)

            Debug.WriteLine(queryBuilder.ToString)
            _networkStream.Write(writebuffer, 0, writebuffer.Length)
            _networkStream.ReadAsync(readbuffer, 0, readbuffer.Length).ContinueWith(Sub(x As Task(Of Integer))
                                                                                        Dim read = x.Result
                                                                                        Dim ms = New MemoryStream(readbuffer, 0, read)
                                                                                        Dim streamReader = New StreamReader(ms, Encoding.ASCII)
                                                                                        While Not streamReader.EndOfStream
                                                                                            Dim responseLine = streamReader.ReadLine()
                                                                                            If responseLine.StartsWith("Session:") Then
                                                                                                session = responseLine.Split(":")(1).Trim
                                                                                                _session = session.Split(";")(0)
                                                                                            ElseIf responseLine.StartsWith("com.ses.streamID") Then
                                                                                                streamId = responseLine.Split(":")(1).Trim
                                                                                            ElseIf responseLine.StartsWith("Transport:") Then
                                                                                                Dim parts As String() = responseLine.Split(";")
                                                                                                For Each part In parts
                                                                                                    If part.StartsWith("destination=") Then
                                                                                                        serverIp = part.Split("=")(1)
                                                                                                    ElseIf part.StartsWith("port=") Then
                                                                                                        serverPort = part.Split("=")(1).Split("-")(0)
                                                                                                    End If
                                                                                                Next
                                                                                            Else
                                                                                                response.AppendLine(responseLine)
                                                                                            End If
                                                                                            Debug.WriteLine("Response: " & responseLine)
                                                                                        End While
                                                                                    End Sub).Wait()
            _cSeq += 1
            If response.ToString.Contains("200 OK") Then
                result.Success = True
                result.Session = _session
                result.StreamId = streamId
                result.ServerIp = serverIp
                result.ServerPort = serverPort
            Else
                result.Success = False
            End If
            Return result
        End Function

        Private Shared Function PlayCommand(streamUri As Uri, streamId As Integer)
            Dim readbuffer(16 * 1024) As Byte
            Dim writebuffer(8 * 1024) As Byte
            Dim response As New StringBuilder()
            _streamUri = streamUri

            Dim queryBuilder As New StringBuilder(1024)
            queryBuilder.AppendFormat("{0} {1} RTSP/1.0" & vbCrLf, "PLAY", streamUri.ToString & "stream=" & streamId)
            queryBuilder.AppendFormat("CSeq: {0}" & vbCrLf, _cSeq)
            queryBuilder.AppendFormat("User-Agent: {0}" & vbCrLf, "SatIPTV")
            queryBuilder.AppendFormat("Session: {0}" & vbCrLf, _session)
            queryBuilder.AppendLine()

            writebuffer = Encoding.ASCII.GetBytes(queryBuilder.ToString)

            Debug.WriteLine(queryBuilder.ToString)
            _networkStream.Write(writebuffer, 0, writebuffer.Length)
            _networkStream.ReadAsync(readbuffer, 0, readbuffer.Length).ContinueWith(Sub(x As Task(Of Integer))
                                                                                        Dim read = x.Result
                                                                                        Dim ms = New MemoryStream(readbuffer, 0, read)
                                                                                        Dim streamReader = New StreamReader(ms, Encoding.ASCII)
                                                                                        While Not streamReader.EndOfStream
                                                                                            Dim responseLine As String = streamReader.ReadLine()
                                                                                            response.AppendLine(responseLine)
                                                                                            Debug.WriteLine("Antwort: " & responseLine)
                                                                                        End While
                                                                                    End Sub).Wait()
            _cSeq += 1
            If response.ToString.Contains("200 OK") Then
                _keepAlive = True
                KeepAlive()
                Return True
            Else
                Return False
            End If
        End Function

        Private Shared Function DescribeCommand(streamUri As Uri)
            Dim readbuffer(16 * 1024) As Byte
            Dim writebuffer(8 * 1024) As Byte
            Dim response As New StringBuilder()
            _streamUri = streamUri

            Dim queryBuilder As New StringBuilder(1024)
            queryBuilder.AppendFormat("{0} {1} RTSP/1.0" & vbCrLf, "DESCRIBE", streamUri.ToString())
            queryBuilder.AppendFormat("CSeq: {0}" & vbCrLf, _cSeq)
            queryBuilder.AppendFormat("User-Agent: {0}" & vbCrLf, "SatIPTV")
            queryBuilder.AppendFormat("Accept: {0}" & vbCrLf, "application/sdp")
            queryBuilder.AppendLine()

            writebuffer = Encoding.ASCII.GetBytes(queryBuilder.ToString)

            Debug.WriteLine(queryBuilder.ToString)
            _networkStream.Write(writebuffer, 0, writebuffer.Length)
            Dim read = _networkStream.Read(readbuffer, 0, readbuffer.Length)
            Using ms = New MemoryStream(readbuffer, 0, read)
                Using streamReader = New StreamReader(ms, Encoding.ASCII)
                    While Not streamReader.EndOfStream
                        Dim responseLine As String = streamReader.ReadLine()
                        response.AppendLine(responseLine)
                        Debug.WriteLine("Antwort: " & responseLine)
                    End While
                End Using
            End Using

            _cSeq += 1
            If response.ToString.Contains("200 OK") Then
                Return True
            Else
                Return False
            End If
        End Function

        Private Shared Function TeardownCommand(streamUri As Uri) As Boolean
            Dim readbuffer(16 * 1024) As Byte
            Dim writebuffer(8 * 1024) As Byte
            Dim response As New StringBuilder()
            _keepAlive = False
            Try


                Dim queryBuilder = New StringBuilder(1024)
                queryBuilder.AppendFormat("{0} {1} RTSP/1.0" & vbCrLf, "TEARDOWN", String.Format("{0}://{1}:{2}/stream={3}", streamUri.Scheme, streamUri.Host, streamUri.Port, _streamId))
                queryBuilder.AppendFormat("CSeq: {0}" & vbCrLf, _cSeq)
                queryBuilder.AppendFormat("Session: {0}" & vbCrLf, _session)
                queryBuilder.AppendFormat("User-Agent: {0}" & vbCrLf, "SatIPTV")
                queryBuilder.AppendLine()

                writebuffer = Encoding.ASCII.GetBytes(queryBuilder.ToString)

                Debug.WriteLine(queryBuilder.ToString)
                _networkStream.Write(writebuffer, 0, writebuffer.Length)
                Task.Delay(100).Wait()
                Dim read = _networkStream.Read(readbuffer, 0, readbuffer.Length)

                Dim ms = New MemoryStream(readbuffer, 0, read)
                Dim streamReader = New StreamReader(ms, Encoding.ASCII)
                While Not streamReader.EndOfStream
                    Dim responseLine = streamReader.ReadLine()
                    response.AppendLine(responseLine)
                    Debug.WriteLine("Antwort: " & responseLine)
                End While

                _cSeq += 1
            Catch ex As Exception

            End Try
            If response.ToString.Contains("200 OK") Then
                Return True
            Else
                Return False
            End If
        End Function

        Private Shared Sub OptionsCommand(streamUri As Uri)
            Dim readbuffer(16 * 1024) As Byte
            Dim writebuffer(8 * 1024) As Byte
            Dim response As New StringBuilder()

            Dim queryBuilder = New StringBuilder(1024)
            queryBuilder.AppendFormat("{0} {1} RTSP/1.0" & vbCrLf, "OPTIONS", _streamUri.Scheme & "://" & _streamUri.Authority)
            queryBuilder.AppendFormat("CSeq: {0}" & vbCrLf, _cSeq)
            queryBuilder.AppendFormat("User-Agent: {0}" & vbCrLf, "SatIPTV")
            queryBuilder.AppendFormat("Session: {0};timeout=60" & vbCrLf, _session)
            queryBuilder.AppendFormat("Accept: {0}", "application/sdp, application/rtsl, application/mheg")
            queryBuilder.Append(vbCrLf & vbCrLf)

            writebuffer = Encoding.ASCII.GetBytes(queryBuilder.ToString)
            Debug.WriteLine(queryBuilder.ToString)

            _networkStream.Write(writebuffer, 0, writebuffer.Length)

            _networkStream.ReadAsync(readbuffer, 0, readbuffer.Length).ContinueWith(Sub(x As Task(Of Integer))
                                                                                        Dim read = x.Result
                                                                                        Dim ms = New MemoryStream(readbuffer, 0, read)
                                                                                        Dim streamReader = New StreamReader(ms, Encoding.ASCII)
                                                                                        While Not streamReader.EndOfStream
                                                                                            Dim responseLine = streamReader.ReadLine()
                                                                                            Debug.WriteLine("Antwort: " & responseLine)
                                                                                            Exit While
                                                                                        End While
                                                                                    End Sub)
            _cSeq += 1
        End Sub
        Private Shared Sub KeepAlive()
            Dim readbuffer(16 * 1024) As Byte
            Dim writebuffer(8 * 1024) As Byte

            Dim tsk = Task.Factory.StartNew(Sub()
                                                While True
                                                    Try
                                                        If Not _keepAlive Then Exit Sub
                                                        If Not _networkStream.Socket.Connected Then
                                                            Debug.WriteLine("KeepAlive stopped, Socket disconnected")
                                                            _keepAlive = False
                                                            Continue While
                                                        End If

                                                        OptionsCommand(_streamUri)
                                                        For i = 0 To 50
                                                            If Not _keepAlive Then Exit Sub
                                                            Task.Delay(500).Wait()
                                                        Next
                                                    Catch ex As Exception

                                                    End Try
                                                End While
                                            End Sub, TaskCreationOptions.LongRunning)
        End Sub

        Public Class SetupResult
            Public Property Session As String
            Public Property StreamId As Integer
            Public Property ServerIp As String
            Public Property ServerPort As Integer

            Public Property Success As Boolean

        End Class

        Public Class StreamResult
            Public Property StreamId As Integer
            Public Property ServerIp As IPAddress
            Public Property ServerPort As Integer
        End Class
    End Class
End Namespace