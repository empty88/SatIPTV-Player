Imports System.Buffers
Imports System.IO
Imports System.IO.Pipelines
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports SatIPTV.Helper

Namespace Classes
    Public Class EpgGrabber
        Private Shared _enc As Encoding = CodePagesEncodingProvider.Instance.GetEncoding(28599)
        'Private _instance As EpgGrabber
        Private Shared _eitStreamUri As Uri
        Private Shared _session As String
        Private Shared _streamId As Integer
        Private Shared _rtspPort As Integer = 554
        Private Shared _cancellationTokenSource As New CancellationTokenSource
        Private Shared _cancellationToken As CancellationToken = _cancellationTokenSource.Token

        Public Shared _memStream As MemoryStream
        Private Shared _runGuid As Guid = Guid.NewGuid
        Private Shared _bufferDir As DirectoryInfo

        Public Shared Sub GrabUdp(multicastIp As IPAddress, multicastPort As Integer)
            _bufferDir = New DirectoryInfo(_runGuid.ToString)
            _bufferDir.Create()
            _cancellationTokenSource = New CancellationTokenSource
            _cancellationToken = _cancellationTokenSource.Token

            Dim socket As New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, New MulticastOption(multicastIp))
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, True)
            'socket.ReceiveBufferSize = 64 * 64 * 1024
            socket.Bind(New IPEndPoint(IPAddress.Any, multicastPort))


            Task.Run(Sub()
                         Dim pipe = New Pipe()
                         Dim writing As Task = FillPipeAsync(socket, pipe.Writer)
                         Dim reading As Task = ReadPipeAsync(pipe.Reader)

                         Task.WhenAll(reading, writing)
                     End Sub)
        End Sub

        Public Shared Sub Shutdown()
            If _eitStreamUri Is Nothing Then Exit Sub
            _cancellationTokenSource.Cancel()
        End Sub

        Private Shared Async Function FillPipeAsync(ByVal socket As Socket, ByVal writer As PipeWriter) As Task
            Const minimumBufferSize As Integer = 4 * 262144
            Try
                While True
                    Dim memory As Memory(Of Byte) = writer.GetMemory(minimumBufferSize)

                    Try
                        Dim bytesRead As Integer = Await socket.ReceiveAsync(memory, SocketFlags.None, _cancellationToken)

                        If bytesRead = 0 Then
                            Exit While
                        End If

                        writer.Advance(bytesRead)
                    Catch ex As Exception
                        Debug.WriteLine("Pipeline Error: {0}", ex)
                        Exit While
                    End Try

                    Dim result As FlushResult = Await writer.FlushAsync()

                    If result.IsCompleted Then
                        Debug.WriteLine("Pipeline Completed: {0}")
                        Exit While
                    End If
                End While
            Catch ex As Exception
                Shutdown()
                Debug.WriteLine("FillPipe")
            End Try

            Await writer.CompleteAsync()
        End Function

        Private Shared Async Function ReadPipeAsync(ByVal reader As PipeReader) As Task
            Dim line As ReadOnlySequence(Of Byte) = Nothing
            Dim rtspStart As Long = -2
            Dim rtspEnd As Long = -1
            Dim eitTableLength As Integer = -1
            Dim sdtTableLength As Integer = -1
            Dim eitBufferArr As Byte() = {}
            Dim sdtBufferArr As Byte() = {}
            Dim pos = 0
            Dim databytes As Byte() = {}
            Dim databytesLength As Integer
            Dim headerOffset As Integer
            Dim payloadOffset As Integer
            Dim lastSequenceNumber As Long = -1
            Try
                Dim first = True
                While True
                    If _cancellationToken.IsCancellationRequested Then
                        Debug.WriteLine("Canceled")
                        Exit While
                    End If
                    'Debug.WriteLine("ReadPipeAsync")
                    Dim result As ReadResult = Await reader.ReadAsync(_cancellationToken)
                    Dim buffer As ReadOnlySequence(Of Byte) = result.Buffer

                    'Debug.WriteLine("Data received")
                    For Each segment In buffer
                        If _cancellationToken.IsCancellationRequested Then
                            Exit While
                        End If
                        Dim seqNumber As Long = BitConverter.ToUInt16(segment.ToArray.Skip(2).Take(2).Reverse.ToArray, 0)
                        If first Then
                            first = False
                            lastSequenceNumber = seqNumber
                            Continue For
                        End If
                        Dim segmentHeader = segment.Slice(0, 12).ToArray
                        'Debug.WriteLine("Buffer Length: {0}", result.Buffer.Length)

                        'Debug.WriteLine("Sequence Number: {0}", seqNumber)
                        If Not seqNumber.Equals(lastSequenceNumber + 1) AndAlso Not seqNumber.Equals(0) Then
                            eitTableLength = -1
                            sdtTableLength = -1
                            eitBufferArr = {}
                            sdtBufferArr = {}
                            'Debug.WriteLine("###############   Sequence Number Error: {0}", seqNumber)
                            lastSequenceNumber = seqNumber
                            Continue For
                        End If
                        lastSequenceNumber = seqNumber
                        segment = segment.Slice(12)
                        pos = 0

                        While pos + 188 <= segment.Length
                            If _cancellationToken.IsCancellationRequested Then
                                Debug.WriteLine("Canceled")
                                Exit While
                            End If
                            'Debug.WriteLine(String.Format("pos: {0}", pos))
                            If segment.Slice(pos, 1).ToArray(0) = &H47 AndAlso
                                segment.Slice(pos + 2, 1).ToArray(0) = &H12 AndAlso
                                segment.Slice(pos + 3, 1).ToArray(0) >= &H10 AndAlso
                                segment.Slice(pos + 3, 1).ToArray(0) <= &H1F Then                 'PID 0x12 gefunden
                                'Debug.WriteLine(String.Format("Counter (EIT): {0:x2}", segment.Slice(pos + 3, 1).ToArray(0)))
                                payloadOffset = 0
                                headerOffset = 0
                                If segment.Slice(pos + 1, 1).ToArray(0) = &H40 Then
                                    headerOffset = 5                                            ' PayloadOffset Pointer vorhanden
                                ElseIf segment.Slice(pos + 1, 1).ToArray(0) = &H0 Then
                                    headerOffset = 4                                            ' kein PayloadOffset Pointer
                                End If
                                If eitTableLength.Equals(-1) Then
                                    If segment.Slice(pos + 1, 1).ToArray(0) = &H40 Then
                                        payloadOffset = segment.Slice(pos + 4, 1).ToArray(0)
                                        'Debug.WriteLine(String.Format("Payload Offset found: {0}", payloadOffset))
                                    End If
                                    If headerOffset.Equals(4) Then
                                        pos += 188
                                        Continue While
                                    Else
                                        eitTableLength = GetEitTableLength(segment.Slice(pos + headerOffset + payloadOffset, 3).ToArray)
                                    End If

                                End If

                                If Not eitTableLength.Equals(-1) AndAlso Not eitTableLength.Equals(15) Then

                                    If eitBufferArr.Length.Equals(0) Then     'erstes Paket vom EIT Table
                                        'Console.WriteLine(_enc.GetString(databytes))
                                        If eitTableLength <= 180 Then
                                            databytesLength = (eitTableLength + 3 - eitBufferArr.Length)
                                            databytes = segment.Slice(pos + headerOffset + payloadOffset, 188 - headerOffset - payloadOffset).ToArray
                                            databytesLength = Math.Min(databytesLength, databytes.Length)
                                            Array.Resize(eitBufferArr, eitBufferArr.Length + databytesLength)
                                            Array.Copy(databytes, 0, eitBufferArr, eitBufferArr.Length - databytesLength, databytesLength)
                                            pos += headerOffset + payloadOffset + databytesLength
                                            'Debug.WriteLine("EIT Buffer Size calculated (small table)")
                                        Else
                                            databytes = segment.Slice(pos + headerOffset + payloadOffset, 188 - headerOffset - payloadOffset).ToArray
                                            Array.Resize(eitBufferArr, eitBufferArr.Length + databytes.Length)
                                            Array.Copy(databytes, 0, eitBufferArr, eitBufferArr.Length - databytes.Length, databytes.Length)
                                            pos += headerOffset + payloadOffset + databytes.Length
                                        End If
                                    Else
                                        databytes = segment.Slice(pos + headerOffset + payloadOffset, 188 - headerOffset - payloadOffset).ToArray
                                        If eitBufferArr.Length + databytes.Length <= eitTableLength + 3 Then
                                            Array.Resize(eitBufferArr, eitBufferArr.Length + databytes.Length)
                                            Array.Copy(databytes, 0, eitBufferArr, eitBufferArr.Length - databytes.Length, databytes.Length)
                                            pos += headerOffset + payloadOffset + databytes.Length
                                        Else
                                            databytesLength = (eitTableLength + 3 - eitBufferArr.Length)
                                            Array.Resize(eitBufferArr, eitBufferArr.Length + databytesLength)
                                            Array.Copy(databytes, 0, eitBufferArr, eitBufferArr.Length - databytesLength, databytesLength)
                                            'Debug.WriteLine("EIT Buffer Size calculated")
                                        End If

                                    End If
                                Else
                                    pos += 188
                                End If

                                'Debug.WriteLine("Bytes left: {0}", {eitTableLength - eitBufferArr.Length + 3})
                                'Debug.WriteLine("EIT Buffer Length: {0}, EIT Table Length: {1}", {eitBufferArr.Length, eitTableLength})
                                If Not eitTableLength.Equals(-1) AndAlso eitBufferArr.Length.Equals(eitTableLength + 3) Then
                                    ProcessEitTableArray(eitBufferArr.Clone)
                                    Array.Clear(eitBufferArr)
                                    Array.Resize(eitBufferArr, 0)
                                    eitTableLength = -1
                                End If
                            ElseIf segment.Slice(pos, 1).ToArray(0) = &H47 AndAlso
                                segment.Slice(pos + 2, 1).ToArray(0) = &H11 AndAlso
                                segment.Slice(pos + 3, 1).ToArray(0) >= &H10 AndAlso
                                segment.Slice(pos + 3, 1).ToArray(0) <= &H1F Then                      ' PID 0x11 gefunden
                                'Debug.WriteLine(String.Format("Counter (SDT): {0:x2}", segment.Slice(pos + 3, 1).ToArray(0)))
                                payloadOffset = 0
                                headerOffset = 0
                                If segment.Slice(pos + 1, 1).ToArray(0) = &H40 Then
                                    headerOffset = 5                                            ' PayloadOffset Pointer vorhanden
                                ElseIf segment.Slice(pos + 1, 1).ToArray(0) = &H0 Then
                                    headerOffset = 4                                            ' kein PayloadOffset Pointer
                                End If
                                If sdtTableLength.Equals(-1) Then
                                    If segment.Slice(pos + 1, 1).ToArray(0) = &H40 Then
                                        payloadOffset = segment.Slice(pos + 4, 1).ToArray(0)
                                    End If
                                    If headerOffset.Equals(4) Then
                                        pos += 188
                                        Continue While
                                    Else
                                        sdtTableLength = GetSdtTableLength(segment.Slice(pos + headerOffset + payloadOffset, 3).ToArray)
                                    End If

                                End If

                                If Not sdtTableLength.Equals(-1) AndAlso Not sdtTableLength.Equals(15) Then

                                    If sdtBufferArr.Length.Equals(0) Then     'erstes Paket vom EIT Table
                                        If sdtTableLength <= 180 Then
                                            databytesLength = (sdtTableLength + 3 - sdtBufferArr.Length)
                                            databytes = segment.Slice(pos + headerOffset + payloadOffset, 188 - headerOffset - payloadOffset).ToArray
                                            databytesLength = Math.Min(databytesLength, databytes.Length)
                                            Array.Resize(sdtBufferArr, sdtBufferArr.Length + databytesLength)
                                            Array.Copy(databytes, 0, sdtBufferArr, sdtBufferArr.Length - databytesLength, databytesLength)
                                            pos += headerOffset + payloadOffset + databytesLength
                                            'Debug.WriteLine("SDT Buffer Size calculated (small table)")
                                        Else
                                            databytes = segment.Slice(pos + headerOffset + payloadOffset, 188 - headerOffset - payloadOffset).ToArray
                                            Array.Resize(sdtBufferArr, sdtBufferArr.Length + databytes.Length)
                                            Array.Copy(databytes, 0, sdtBufferArr, sdtBufferArr.Length - databytes.Length, databytes.Length)
                                            pos += headerOffset + payloadOffset + databytes.Length
                                        End If
                                    Else
                                        databytes = segment.Slice(pos + headerOffset + payloadOffset, 188 - headerOffset - payloadOffset).ToArray
                                        If sdtBufferArr.Length + databytes.Length <= sdtTableLength + 3 Then
                                            Array.Resize(sdtBufferArr, sdtBufferArr.Length + databytes.Length)
                                            Array.Copy(databytes, 0, sdtBufferArr, sdtBufferArr.Length - databytes.Length, databytes.Length)
                                            pos += headerOffset + payloadOffset + databytes.Length
                                        Else
                                            databytesLength = (sdtTableLength + 3 - sdtBufferArr.Length)
                                            Array.Resize(sdtBufferArr, sdtBufferArr.Length + databytesLength)
                                            Array.Copy(databytes, 0, sdtBufferArr, sdtBufferArr.Length - databytesLength, databytesLength)
                                            'Debug.WriteLine("SDT Buffer Size calculated")
                                        End If

                                    End If
                                Else
                                    pos += 188
                                End If

                                'Debug.WriteLine("SDT Buffer Length: {0}, SDT Table Length: {1}", {sdtBufferArr.Length, sdtTableLength})
                                If Not sdtTableLength.Equals(-1) AndAlso sdtBufferArr.Length.Equals(sdtTableLength + 3) Then
                                    ProcessSdtTableArray(sdtBufferArr.Clone)
                                    Array.Clear(sdtBufferArr)
                                    Array.Resize(sdtBufferArr, 0)
                                    sdtTableLength = -1
                                End If
                            Else
                                'Debug.Write(".")
                                If pos Mod 188 <> 0 Then
                                    pos += 1
                                Else
                                    pos += 188
                                End If
                            End If

                        End While


                        If result.IsCompleted Then
                            Debug.WriteLine("result completed")
                            Exit While
                        End If
                    Next
                    If result.IsCompleted Then
                        Debug.WriteLine("result completed")
                        Exit While
                    End If

                    reader.AdvanceTo(result.Buffer.End)
                End While
            Catch ex As Exception
                Debug.WriteLine("ReadPipe: {0}", ex)
            End Try
            Await reader.CompleteAsync()
        End Function

        Private Shared Sub ProcessEitTableArray(table_buf As Byte())
            Task.Run(Sub()
                         Dim eventNameLen As Integer
                         Dim eventName As String
                         Dim eventTextLength As Integer
                         Dim eventText As String
                         Dim eventDescriptionLength As Integer
                         Dim eventDescription As String
                         Dim event_header_length As Integer = 12
                         Dim desc_header_length As Integer = 2
                         Dim epgEvents As New List(Of EpgInfo)

                         Try
                             Dim serviceIdBytes As Byte() = table_buf.Skip(3).Take(2).ToArray
                             Dim serviceId As Long = BitConverter.ToUInt16(serviceIdBytes.Reverse.ToArray, 0)
                             Dim epgService As EpgService = DatabaseHelper.SelectEpgService(serviceId)
                             If epgService Is Nothing Then
                                 Debug.WriteLine("ServiceId missing in DB")
                                 Return
                             End If
                             'Debug.WriteLine("Service ID: {0:x} {1}", {ByteArrayToString(serviceIdBytes), _serviceList.FirstOrDefault(Function(x) x.ServiceId.Equals(serviceId)).ServiceName})
                             'WriteEitToFile(table_buf, "eit-" & Guid.NewGuid.ToString)
                             'Console.WriteLine("Start Time: {0}", start_time)
                             'Console.WriteLine(Encoding.UTF8.GetString(table_buf))
                             Dim pos As Integer = 14
                             While pos < table_buf.Length - 4 ' Event Loop, 4 = CRC-Length
                                 eventName = String.Empty
                                 eventText = String.Empty
                                 eventDescription = String.Empty

                                 Dim event_start_pos As Integer = pos
                                 Dim event_id_bytes() As Byte = table_buf.Skip(pos).Take(2).ToArray
                                 Dim eventId As Integer = BitConverter.ToUInt16(event_id_bytes.Reverse.ToArray, 0)
                                 'Debug.WriteLine("Event ID: {0} {1}", {ByteArrayToString(event_id_bytes), eventId})
                                 Dim start_time_bytes() As Byte = table_buf.Skip(pos + 2).Take(5).ToArray
                                 Dim startTime As Long = New DateTimeOffset(GetDateFromMJD(start_time_bytes)).ToUnixTimeSeconds
                                 Dim endTime As Long = startTime + GetDurationFromHex(table_buf.Skip(pos + 7).Take(3).ToArray).TotalSeconds
                                 'Debug.WriteLine("Start Time: {0}", GetDateFromMJD(start_time_bytes).ToString("dd.MM.yy HH:mm"))
                                 'Debug.WriteLine("Duration: {0}", GetDurationFromHex(table_buf.Skip(pos + 7).Take(3).ToArray))

                                 Dim event_loop_length As Integer = GetTableOrEventLength(table_buf.Skip(pos + 10).Take(2).ToArray)
                                 'Console.WriteLine("Event Loop Length: {0}", event_loop_length)

                                 Dim event_length As Integer = event_header_length + event_loop_length
                                 pos += event_header_length
                                 While pos < event_start_pos + event_length AndAlso pos < table_buf.Length   ' Descriptor Loop
                                     Dim type As Integer = table_buf(pos)
                                     Dim desc_loop_length As Integer = table_buf(pos + 1)
                                     'Console.WriteLine("Descriptor Loop Length: {0}", desc_loop_length)
                                     Dim desc_length As Integer = desc_header_length + desc_loop_length
                                     If desc_loop_length.Equals(0) Then Exit While
                                     If type = &H4D Then                     ' Short Event Descriptor
                                         eventNameLen = table_buf(pos + 5)
                                         eventName &= _enc.GetString(table_buf.Skip(pos + 7).Take(eventNameLen - 1).ToArray)

                                         eventTextLength = table_buf(pos + 5 + 1 + eventNameLen)
                                         eventText &= _enc.GetString(table_buf.Skip(pos + 7 + eventNameLen + 1).Take(eventTextLength - 1).ToArray)
                                     ElseIf type = &H4E Then                 ' Exetended Event Descriptor
                                         eventDescriptionLength = table_buf(pos + 7)
                                         eventDescription &= _enc.GetString(table_buf.Skip(pos + 9).Take(eventDescriptionLength - 1).ToArray)
                                     End If
                                     pos += desc_length
                                 End While
                                 'Debug.WriteLine("Event Name: {0}", eventName)
                                 'Debug.WriteLine("Event Text: {0}", eventText)
                                 'Debug.WriteLine("Event Description: {0}", eventDescription)
                                 If startTime < 0 OrElse endTime < 0 OrElse String.IsNullOrWhiteSpace(eventName) Then Continue While
                                 Dim serviceName As String = epgService.ServiceName
                                 If String.IsNullOrWhiteSpace(serviceName) Then
                                     Continue While
                                 End If
                                 Dim epgInfo As New EpgInfo(eventId, Nothing, startTime, endTime, serviceName, eventName, eventText, eventDescription)

                                 AddOrUpdateEpg(epgInfo)
                             End While
                             If pos.Equals(table_buf.Length - 4) Then
                                 'Debug.WriteLine("EIT vollständig gelesen")
                             Else
                                 Debug.WriteLine("EIT Error")
                             End If
                         Catch ex As Exception
                             Debug.WriteLine("EIT Error: " & ex.ToString)
                         End Try
                     End Sub)
        End Sub

        Private Shared Sub ProcessSdtTableArray(table_buf As Byte())
            Task.Run(Sub()
                         Dim providerName As String
                         Dim providerNameLength As Integer
                         Dim serviceName As String
                         Dim serviceNameLength As Integer
                         Dim pos As Integer = 11
                         'WriteSdtToFile(table_buf, "sdt-" & Guid.NewGuid.ToString)
                         While pos < table_buf.Length - 4
                             providerName = String.Empty
                             serviceName = String.Empty
                             Dim serviceStartPos As Integer = pos
                             Dim sdtServiceIdBytes() As Byte = table_buf.Skip(pos).Take(2).ToArray
                             Dim sdtServiceId As Long = BitConverter.ToUInt16(sdtServiceIdBytes.Reverse.ToArray, 0)

                             Dim serviceLoopLength As Integer = GetTableOrEventLength(table_buf.Skip(pos + 3).Take(2).ToArray)
                             Dim serviceHeaderLength As Integer = 5
                             Dim serviceLength As Integer = serviceHeaderLength + serviceLoopLength
                             pos += serviceHeaderLength
                             While pos < serviceStartPos + serviceLength AndAlso pos < table_buf.Length
                                 Dim type As Integer = table_buf(pos)
                                 Dim descHeaderLength As Integer = 2
                                 Dim descriptorLoopLength As Integer = table_buf(pos + 1)
                                 Dim descriptorLength As Integer = descHeaderLength + descriptorLoopLength
                                 If descriptorLoopLength.Equals(0) Then Exit While
                                 If type = &H48 Then

                                     If table_buf(pos + 4) <= &H1F Then
                                         providerNameLength = table_buf(pos + 3) - 1
                                         providerName = _enc.GetString(table_buf.Skip(pos + 5).Take(providerNameLength).ToArray)
                                         serviceNameLength = table_buf(pos + 3 + providerNameLength + 2) - 1
                                         serviceName = _enc.GetString(table_buf.Skip(pos + 5 + providerNameLength + 2).Take(serviceNameLength).ToArray)
                                     Else
                                         providerNameLength = table_buf(pos + 3)
                                         providerName = _enc.GetString(table_buf.Skip(pos + 4).Take(providerNameLength).ToArray)
                                         serviceNameLength = table_buf(pos + 3 + providerNameLength + 1)
                                         serviceName = _enc.GetString(table_buf.Skip(pos + 4 + providerNameLength + 1).Take(serviceNameLength).ToArray)
                                     End If
                                     serviceName = New String(serviceName.Where(Function(x As Char) x < ChrW(&H80) OrElse x > ChrW(&H9F)).ToArray)
                                     If serviceName.StartsWith(ChrW(&H5)) Then serviceName = serviceName.Substring(1)
                                     providerName = New String(providerName.Where(Function(x As Char) x < ChrW(&H80) OrElse x > ChrW(&H9F)).ToArray)
                                     If providerName.StartsWith(ChrW(&H5)) Then providerName = providerName.Substring(1)
                                     'Debug.WriteLine("Service Name: {0} (0x{1:x})", serviceName, ByteArrayToString(sdtServiceIdBytes))
                                 End If
                                 pos += descriptorLength
                             End While
                             AddOrUpdateServices(New EpgService(sdtServiceId, serviceName, providerName))
                         End While
                         If pos.Equals(table_buf.Length - 4) Then
                             'Debug.WriteLine("SDT parsing finished")
                         Else
                             Debug.WriteLine("SDT Error")
                         End If
                     End Sub)
        End Sub

        Private Shared Sub AddOrUpdateEpg(epgInfo As EpgInfo)
            Dim currentEpgInfo As EpgInfo = DatabaseHelper.SelectEpgInfo(epgInfo.ChannelName, epgInfo.EventId)
            'Dim concurrentEpgs As List(Of EpgInfo) = DatabaseHelper.SelectConcurrentEpgInfos(epgInfo.EventId, epgInfo.ChannelName, epgInfo.StartTime, epgInfo.EndTime)
            'If Not concurrentEpgs.Count.Equals(0) Then concurrentEpgs.ForEach(Function(x) DatabaseHelper.DeleteEpgInfo(x.EventId))
            If currentEpgInfo IsNot Nothing Then
                'Debug.WriteLine("EPG already in db, check for changes")
                If Not epgInfo.Description.Equals(currentEpgInfo.Description) OrElse
                    Not epgInfo.Title.Equals(currentEpgInfo.Title) OrElse
                    Not epgInfo.SubTitle.Equals(currentEpgInfo.SubTitle) OrElse
                    Not epgInfo.StartTime.Equals(currentEpgInfo.StartTime) OrElse
                    Not epgInfo.EndTime.Equals(currentEpgInfo.EndTime) OrElse
                    Not epgInfo.NextEventId.Equals(currentEpgInfo.NextEventId) Then
                    DatabaseHelper.UpdateEpgInfo(epgInfo)
                End If
            Else
                Dim concurrentEpgs As List(Of EpgInfo) = DatabaseHelper.SelectConcurrentEpgInfos(epgInfo.EventId, epgInfo.ChannelName, epgInfo.StartTime, epgInfo.EndTime)
                If Not concurrentEpgs.Count.Equals(0) Then concurrentEpgs.ForEach(Function(x) DatabaseHelper.DeleteEpgInfo(x.EventId))
                DatabaseHelper.InsertEpgInfo(epgInfo)
            End If
        End Sub

        Private Shared Sub AddOrUpdateServices(epgService As EpgService)
            Dim currentEpgService As EpgService = DatabaseHelper.SelectEpgService(epgService.ServiceId)
            If currentEpgService IsNot Nothing Then
                If Not epgService.ProviderName.Equals(currentEpgService.ProviderName) OrElse Not epgService.ServiceName.Equals(currentEpgService.ServiceName) Then
                    DatabaseHelper.UpdateEpgService(epgService)
                End If
            Else
                DatabaseHelper.InsertEpgService(epgService)
            End If
        End Sub

        Private Shared Function GetDurationFromHex(bytes As Byte()) As TimeSpan
            Return TimeSpan.Parse(String.Format("{0:x2}:{1:x2}:{2:x2}", bytes(0), bytes(1), bytes(2)))
        End Function

        Private Shared Function GetDateFromMJD(start_time_bytes() As Byte) As Date
            Try
                Dim mjd As Integer = BitConverter.ToUInt16(start_time_bytes.Take(2).Reverse.ToArray, 0)
                Dim hour As Integer = String.Format("{0:x2}", start_time_bytes.Skip(2).Take(1).ToArray(0))
                Dim minute As Integer = String.Format("{0:x2}", start_time_bytes.Skip(3).Take(1).ToArray(0))
                Dim second As Integer = String.Format("{0:x2}", start_time_bytes.Skip(4).Take(1).ToArray(0))

                Return New Date(1858, 11, 17).AddDays(mjd).AddHours(hour).AddMinutes(minute).AddSeconds(second).ToLocalTime()
            Catch ex As Exception
                Return Nothing
            End Try
        End Function

        Private Shared Function GetTableOrEventLength(two_bytes As Byte()) As Integer
            Dim bits = New BitArray(two_bytes.Take(1).ToArray)
            bits(4) = False
            bits(5) = False
            bits(6) = False
            bits(7) = False

            bits.CopyTo(two_bytes, 0)
            Return BitConverter.ToUInt16(two_bytes.Reverse.ToArray, 0)
        End Function

        Private Shared Function GetEitTableLength(rtsppacket As Byte()) As Integer
            If rtsppacket(0) >= &H50 AndAlso rtsppacket(0) <= &H5F AndAlso String.Format("{0:x2}", rtsppacket(1)).StartsWith("f") Then
                'Debug.WriteLine("######### EIT found: {0:x}", rtsppacket(0))
                Dim table_length_bytes(1) As Byte
                rtsppacket.Skip(1).Take(2).ToArray.CopyTo(table_length_bytes, 0)
                Dim bits As New BitArray(rtsppacket.Skip(1).Take(1).ToArray)
                bits(4) = False
                bits(5) = False
                bits(6) = False
                bits(7) = False

                bits.CopyTo(table_length_bytes, 0)
                'Debug.WriteLine("EIT Length: {0}", BitConverter.ToInt16(table_length_bytes.Reverse.ToArray, 0))
                Dim length As Integer = BitConverter.ToUInt16(table_length_bytes.Reverse.ToArray, 0)
                If length.Equals(15) Then Return -1
                Return length
            Else
                Return -1
            End If
        End Function
        Private Shared Function GetSdtTableLength(rtsppacket As Byte()) As Integer
            If (rtsppacket(0) = &H42 OrElse rtsppacket(0) = &H42) AndAlso String.Format("{0:x2}", rtsppacket(1)).StartsWith("f") Then
                'Debug.WriteLine("######### SDT found: {0:x}", rtsppacket(0))
                Dim table_length_bytes(1) As Byte
                rtsppacket.Skip(1).Take(2).ToArray.CopyTo(table_length_bytes, 0)
                Dim bits As New BitArray(rtsppacket.Skip(1).Take(1).ToArray)
                bits(4) = False
                bits(5) = False
                bits(6) = False
                bits(7) = False

                bits.CopyTo(table_length_bytes, 0)
                'Debug.WriteLine("SDT Length: {0}", BitConverter.ToUInt16(table_length_bytes.Reverse.ToArray, 0))
                Dim length As Integer = BitConverter.ToUInt16(table_length_bytes.Reverse.ToArray, 0)
                If length.Equals(15) Then Return -1
                Return length
            Else
                Return -1
            End If
        End Function

        Private Shared Function ByteArrayToString(ba As Byte()) As String
            Dim hex As New StringBuilder(ba.Length * 2)
            For Each b In ba
                hex.AppendFormat("{0:x2}", b)
            Next

            Return hex.ToString()
        End Function

        Private Shared Sub WriteEitToFile(bytes As Byte(), name As String)
            Dim pos As Integer = 0
            Using sw As New StreamWriter(Path.Combine(_bufferDir.FullName, "buffer-" & name & ".txt"))
                For Each bt In bytes
                    If pos.Equals(13) OrElse pos.Equals(25) Then
                        sw.WriteLine("{0:x2}", bt)
                    Else
                        sw.Write("{0:x2}", bt)
                    End If
                    pos += 1
                Next
                sw.Flush()
            End Using
        End Sub
    End Class
End Namespace