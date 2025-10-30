


Imports Microsoft.Data
Imports Microsoft.Data.Sqlite
Imports SatIPTV.Classes

Namespace Helper
    Public Class DatabaseHelper
        Private Shared _connectionString As String = "Data Source=epg.db"

        Public Shared Sub Initialize()
            Using conn As New Sqlite.SqliteConnection(_connectionString)
                conn.Open()
                Using cmd As New Sqlite.SqliteCommand("PRAGMA journal_mode=WAL;", conn)
                    cmd.ExecuteNonQuery()
                End Using
                Using cmd As New Sqlite.SqliteCommand("PRAGMA synchronous = NORMAL;", conn)
                    cmd.ExecuteNonQuery()
                End Using
                Using cmd As New Sqlite.SqliteCommand("PRAGMA temp_store = MEMORY;", conn)
                    cmd.ExecuteNonQuery()
                End Using
            End Using
        End Sub


        Public Shared Function InsertEpgInfo(epgInfo As EpgInfo) As Integer
            Debug.WriteLine("Insert EPG {0} {1}", epgInfo.ChannelName, epgInfo.Title)
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("INSERT INTO epgs (eventId, nextEventId, starttime, endtime, channelName, title, subtitle, description) VALUES (@eventId, @nextEventId, @starttime, @endtime, @channelName, @title, @subtitle, @description)", connection)
                    command.Parameters.AddWithValue("@eventId", epgInfo.EventId)
                    command.Parameters.AddWithValue("@nextEventId", epgInfo.NextEventId)
                    command.Parameters.AddWithValue("@starttime", epgInfo.StartTime)
                    command.Parameters.AddWithValue("@endtime", epgInfo.EndTime)
                    command.Parameters.AddWithValue("@channelName", epgInfo.ChannelName)
                    command.Parameters.AddWithValue("@title", epgInfo.Title)
                    command.Parameters.AddWithValue("@subtitle", epgInfo.SubTitle)
                    command.Parameters.AddWithValue("@description", epgInfo.Description)
                    Try
                        Return command.ExecuteNonQuery()
                    Catch ex As SqliteException
                        Debug.WriteLine("SQL Error")
                        If ex.SqliteErrorCode.Equals(5) Then
                            Debug.WriteLine("insert: database locked, retry")
                            Task.Delay(100)
                            connection.Dispose()
                            InsertEpgInfo(epgInfo)
                        End If
                    End Try
                End Using
            End Using
            Return 0
        End Function

        Public Shared Function UpdateEpgInfo(epgInfo As EpgInfo) As Integer
            Debug.WriteLine("Update EPG {0} {1}", epgInfo.ChannelName, epgInfo.Title)
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("UPDATE epgs SET nextEventId = @nextEventId, starttime = @starttime, endtime = @endtime, channelName = @channelName, title = @title, subtitle = @subtitle, description = @description WHERE eventId = @eventId AND channelname = @channelName COLLATE NOCASE", connection)
                    command.Parameters.AddWithValue("@eventId", epgInfo.EventId)
                    command.Parameters.AddWithValue("@nextEventId", epgInfo.NextEventId)
                    command.Parameters.AddWithValue("@starttime", epgInfo.StartTime)
                    command.Parameters.AddWithValue("@endtime", epgInfo.EndTime)
                    command.Parameters.AddWithValue("@channelName", epgInfo.ChannelName)
                    command.Parameters.AddWithValue("@title", epgInfo.Title)
                    command.Parameters.AddWithValue("@subtitle", epgInfo.SubTitle)
                    command.Parameters.AddWithValue("@description", epgInfo.Description)
                    Try
                        Return command.ExecuteNonQuery()
                    Catch ex As SqliteException
                        If ex.SqliteErrorCode.Equals(5) Then
                            Debug.WriteLine("update: database locked, retry")
                            Task.Delay(100)
                            UpdateEpgInfo(epgInfo)
                        End If
                    End Try
                End Using
            End Using
            Return 0
        End Function

        Public Shared Function SelectEpgInfo(channelName As String, eventId As Long) As EpgInfo
            'Debug.WriteLine("Select EPG")
            Dim result As EpgInfo = Nothing
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("SELECT eventId, nextEventId, starttime, endtime, channelName, title, subtitle, description FROM epgs WHERE eventId = @eventId AND channelName = @channelName COLLATE NOCASE", connection)
                    command.Parameters.AddWithValue("@eventId", eventId)
                    command.Parameters.AddWithValue("@channelName", channelName)
                    Using reader As SqliteDataReader = command.ExecuteReader()
                        If reader.Read() Then
                            result = New EpgInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7))
                        End If
                        reader.Close()
                    End Using
                End Using
            End Using
            Return result
        End Function

        Public Shared Function SelectConcurrentEpgInfos(eventId As Long, channelName As String, startTime As Long, endTime As Long) As List(Of EpgInfo)
            'Debug.WriteLine("Select EPG")
            Dim result As New List(Of EpgInfo)
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("SELECT eventId, nextEventId, starttime, endtime, channelName, title, subtitle, description FROM epgs
                                                    WHERE ChannelName = @channelName COLLATE NOCASE AND
                                                    ((StartTime <= @startTime AND EndTime >= @endTime) OR
                                                    (StartTime <= @startTime AND EndTime > @startTime AND EndTime <= @endTime) OR
                                                    (StartTime >= @startTime AND StartTime < @endTime AND EndTime >= @endTime) OR
                                                    (StartTime >= @startTime AND EndTime <= @endTime)) AND
                                                    eventId <> @eventId
                                                    ORDER BY EventId", connection)
                    command.Parameters.AddWithValue("@eventId", eventId)
                    command.Parameters.AddWithValue("@channelName", channelName)
                    command.Parameters.AddWithValue("@startTime", startTime)
                    command.Parameters.AddWithValue("@endTime", endTime)
                    Using reader As SqliteDataReader = command.ExecuteReader()
                        While reader.Read()
                            result.Add(New EpgInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7)))
                        End While
                        reader.Close()
                    End Using
                End Using
            End Using
            Return result
        End Function

        Public Shared Function SelectCurrentEpgInfo(channelName) As EpgInfo
            Dim result As EpgInfo = Nothing
            Dim now As New DateTimeOffset(Date.UtcNow)
            now.ToUnixTimeSeconds()
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("SELECT eventId, nextEventId, starttime, endtime, channelName, title, subtitle, description FROM epgs WHERE channelName = @channelName COLLATE NOCASE AND starttime < @currentTime AND endtime > @currentTime", connection)
                    command.Parameters.AddWithValue("@channelName", channelName)
                    command.Parameters.AddWithValue("@currentTime", now.ToUnixTimeSeconds())
                    Using reader As SqliteDataReader = command.ExecuteReader()
                        If reader.Read() Then
                            result = New EpgInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7))
                        End If
                        reader.Close()
                    End Using
                End Using
            End Using
            Return result
        End Function

        Public Shared Function SelectEpgInfos(channelName) As List(Of EpgInfo)
            Dim epgInfos As New List(Of EpgInfo)
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("SELECT eventId, nextEventId, starttime, endtime, channelName, title, subtitle, description FROM epgs WHERE channelName = @channelName COLLATE NOCASE ORDER BY starttime", connection)
                    command.Parameters.AddWithValue("@channelName", channelName)
                    Using reader As SqliteDataReader = command.ExecuteReader()
                        While reader.Read()
                            Dim epgInfo As New EpgInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7))
                            epgInfos.Add(epgInfo)
                        End While
                        reader.Close()
                    End Using
                End Using
            End Using
            Return epgInfos
        End Function

        Public Shared Function DeleteEpgInfo(eventId As Long) As Boolean
            Debug.WriteLine(String.Format("EPG with Id {0} deleted", eventId))
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("DELETE FROM epgs WHERE eventId = @eventId", connection)
                    command.Parameters.AddWithValue("eventId", eventId)
                    Return command.ExecuteNonQuery() > 0
                End Using
            End Using
        End Function

        Public Shared Function PurgeOldEpgInfos() As Integer
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("DELETE FROM epgs WHERE endtime <= strftime('%s', datetime('now','-2 day'))", connection)
                    Return command.ExecuteNonQuery()
                End Using
            End Using
        End Function

        Public Shared Function SelectEpgService(serviceId As Long) As EpgService
            'Debug.WriteLine("Select EPG Service")
            Dim result As EpgService = Nothing
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("SELECT serviceId, Name, ProviderName FROM epg_services WHERE serviceId = @serviceId", connection)
                    command.Parameters.AddWithValue("@serviceId", serviceId)
                    Using reader As SqliteDataReader = command.ExecuteReader()
                        If reader.Read() Then
                            result = New EpgService(reader.GetInt64(0), reader.GetString(1), reader.GetString(2))
                        End If
                        reader.Close()
                    End Using
                End Using
            End Using
            Return result
        End Function

        Public Shared Function InsertEpgService(service As EpgService) As Integer
            'Debug.WriteLine("Insert EPG Service")
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("INSERT INTO epg_services (serviceId, Name, ProviderName) VALUES (@serviceId, @serviceName, @providerName)", connection)
                    command.Parameters.AddWithValue("@serviceId", service.ServiceId)
                    command.Parameters.AddWithValue("@serviceName", service.ServiceName)
                    command.Parameters.AddWithValue("@providerName", service.ProviderName)
                    command.ExecuteNonQuery()
                End Using
            End Using
            Return 0
        End Function

        Public Shared Function UpdateEpgService(service As EpgService) As Integer
            Debug.WriteLine("Update EPG Service")
            Using connection As New SqliteConnection(_connectionString)
                connection.Open()
                Using command As New SqliteCommand("UPDATE epg_services SET Name = @serviceName, ProviderName = @providerName WHERE serviceId = @serviceId", connection)
                    command.Parameters.AddWithValue("@serviceId", service.ServiceId)
                    command.Parameters.AddWithValue("@serviceName", service.ServiceName)
                    command.Parameters.AddWithValue("@providerName", service.ProviderName)
                    Try
                        Return command.ExecuteNonQuery()
                    Catch ex As SqliteException
                        Debug.WriteLine("SQL Error")
                        If ex.SqliteErrorCode.Equals(5) Then
                            Debug.WriteLine("update service: database locked, retry")
                            Task.Delay(100)
                            UpdateEpgService(service)
                        End If
                    End Try
                End Using
            End Using
            Return 0
        End Function
    End Class

End Namespace