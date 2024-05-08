Imports Newtonsoft.Json
Imports Rssdp
Imports SatIPTV.Classes
Imports SatIPTV.ViewModels
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Text
Imports System.Xml

Namespace Helper
    Public Class NetworkHelper

        Public Shared Function DiscoverSatIpServers() As List(Of DiscoveredSsdpDevice)
            Dim ipAddresses = New List(Of String)
            For Each netIf In NetworkInterface.GetAllNetworkInterfaces()

                If netIf.NetworkInterfaceType.Equals(NetworkInterfaceType.Ethernet) AndAlso
                                    netIf.OperationalStatus.Equals(OperationalStatus.Up) AndAlso
                                        Not netIf.Name.Contains("vEthernet") Then

                    For Each ip In netIf.GetIPProperties().UnicastAddresses

                        If ip.Address.AddressFamily.Equals(AddressFamily.InterNetwork) Then

                            Dim ipAddress = ip.Address.ToString()
                            Console.WriteLine($"{netIf.Name}: {ipAddress}'")
                            ipAddresses.Add(ipAddress)
                        End If
                    Next
                End If
            Next

            Dim searchTarget = "urn:ses-com:device:SatIPServer:1"
            Dim Devices = New ConcurrentBag(Of DiscoveredSsdpDevice)()
            Parallel.ForEach(ipAddresses, Sub(ipAddress)
                                              Using deviceLocator As New SsdpDeviceLocator(ipAddress)
                                                  Dim foundDevices = deviceLocator.SearchAsync(searchTarget, TimeSpan.FromSeconds(5)).Result
                                                  For Each foundDevice In foundDevices
                                                      Console.WriteLine($"Device: usn={foundDevice.Usn}")
                                                      Devices.Add(foundDevice)
                                                  Next
                                              End Using
                                          End Sub)
            Return Devices.ToList()

        End Function


        Public Shared Function GetSatIpServerDetails(descriptionUrl As String) As SatIpServerDetails
            Try
                Using webClient As New WebClient() With {.Encoding = Encoding.UTF8}
                    Dim xmlDoc As New XmlDocument()
                    xmlDoc.LoadXml(webClient.DownloadString(descriptionUrl))

                    Dim nameSpaceManager As New XmlNamespaceManager(xmlDoc.NameTable)

                    nameSpaceManager.AddNamespace("ns", "urn:schemas-upnp-org:device-1-0")
                    nameSpaceManager.AddNamespace("satip", "urn:ses-com:satip")


                    Dim root As XmlNode = xmlDoc.DocumentElement
                    Dim friendlyName As String = xmlDoc.SelectSingleNode("//ns:friendlyName", nameSpaceManager).InnerText
                    Dim satIpCap As String = xmlDoc.SelectSingleNode("//satip:X_SATIPCAP", nameSpaceManager).InnerText
                    Dim satIpM3u As String = If(xmlDoc.SelectSingleNode("//satip:X_SATIPM3U", nameSpaceManager) IsNot Nothing, xmlDoc.SelectSingleNode("//satip:X_SATIPM3U", nameSpaceManager).InnerText, Nothing)
                    Dim channels As List(Of Channel)

                    If satIpM3u IsNot Nothing Then
                        Dim m3uContent As String = webClient.DownloadString(satIpM3u)
                        If m3uContent.StartsWith("#EXTM3U") Then
                            channels = ParseM3u(m3uContent)
                        End If
                    End If

                    Return New SatIpServerDetails(friendlyName, satIpCap, satIpM3u, channels)
                End Using

            Catch ex As Exception
                MainViewModel.ErrorString = String.Format("SatIpServerDetails: {0}", ex.Message)
            End Try
        End Function

        Private Shared Function ParseM3u(content As String) As List(Of Channel)
            Dim result As New List(Of Channel)
            Using stringReader As New StringReader(content)
                Dim line As String
                Dim channel As Channel
                While True
                    line = stringReader.ReadLine()
                    If line Is Nothing Then Return result
                    If line.StartsWith("#EXTM3U") Then Continue While
                    If line.StartsWith("#EXTVLCOPT") Then Continue While
                    If line.StartsWith("#EXTINF") Then
                        channel = New Channel(line.Split(",")(1))
                        Continue While
                    End If
                    If line.StartsWith("rtsp://") Then
                        channel.StreamUrl = line
                        result.Add(channel)
                    End If

                End While
            End Using
            Return result
        End Function

        Public Shared Function GetPublicChannelList(url As String) As List(Of Channel)
            Try
                Using webClient As New WebClient() With {.Encoding = Encoding.UTF8}

                    Dim m3uContent As String = webClient.DownloadString(url)
                    If m3uContent.StartsWith("#EXTM3U") Then
                        Return ParseM3u(m3uContent)
                    End If

                    Return New List(Of Channel)
                End Using
            Catch ex As Exception
                MainViewModel.ErrorString = String.Format("DVB-S Sender-Download: {0}", ex.Message)
                Return New List(Of Channel)
            End Try
        End Function

        Public Shared Function GetCurrentEpgFromTvHeadend(channelName As String) As EpgInfo
            Dim responseItem As EpgInfo = Nothing
            Task.Run(Sub()
                         Try
                             Dim url As String = String.Format("{0}/api/epg/events/grid?channel={1}&limit=1", My.Settings.TvHeadendServer, channelName)
                             Using webClient As New WebClient() With {.Encoding = Encoding.UTF8}
                                 webClient.Headers.Add(HttpRequestHeader.Authorization, String.Format("Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", My.Settings.TvHeadendUser, EncryptionHelper.ToInsecureString(EncryptionHelper.DecryptString(My.Settings.TvHeadendPassword)))))))
                                 Dim json As String = webClient.DownloadString(url)
                                 Dim response As TVHeadendEPGResponse = JsonConvert.DeserializeObject(json, GetType(TVHeadendEPGResponse))

                                 responseItem = response.entries.FirstOrDefault(Function(x) x.ChannelName.Equals(channelName))

                             End Using
                         Catch ex As Exception
                             MainViewModel.ErrorString = String.Format("TVheadend: {0}", ex.Message)
                         End Try
                     End Sub).Wait()
            Return responseItem
        End Function

        Public Shared Function GetAllEpgFromTvHeadend(channelName As String) As List(Of EpgInfo)
            Dim responseItems As New List(Of EpgInfo)
            Task.Run(Sub()
                         Try
                             Dim url As String = String.Format("{0}/api/epg/events/grid?channel={1}&limit=99", My.Settings.TvHeadendServer, channelName)
                             Using webClient As New WebClient() With {.Encoding = Encoding.UTF8}
                                 webClient.Headers.Add(HttpRequestHeader.Authorization, String.Format("Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", My.Settings.TvHeadendUser, EncryptionHelper.ToInsecureString(EncryptionHelper.DecryptString(My.Settings.TvHeadendPassword)))))))
                                 Dim json As String = webClient.DownloadString(url)
                                 Dim response As TVHeadendEPGResponse = JsonConvert.DeserializeObject(json, GetType(TVHeadendEPGResponse))

                                 responseItems = response.entries.Where(Function(x) x.ChannelName.Equals(channelName)).ToList
                                 'close gaps
                                 Dim prevItem As EpgInfo = Nothing
                                 For Each item In responseItems
                                     If prevItem Is Nothing Then
                                         prevItem = item
                                         Continue For
                                     End If
                                     item.StartTime = prevItem.EndTime

                                     prevItem = item
                                 Next
                                 Console.WriteLine(String.Format("EPG-Abruf für {0}: {1} Elemente", channelName, responseItems.Count()))
                             End Using
                         Catch ex As Exception
                             MainViewModel.ErrorString = String.Format("TVheadend: {0}", ex.Message)
                         End Try
                     End Sub).Wait()
            Return responseItems
        End Function

        'Public Shared Function DownloadChannelIcon(channelName As String) As Boolean
        '    Try
        '        Using webClient As New WebClient() With {.Encoding = Encoding.UTF8}
        '            Dim xmlDoc As New XmlDocument()
        '            Dim url As String = String.Format("https://raw.githubusercontent.com/picons/picons/master/build-source/logos/{0}.png", channelName.Replace(" ", "").ToLower())
        '            webClient.DownloadFile(url, String.Format("{0}.png"))


        '            Return True
        '        End Using
        '    Catch ex As Exception
        '        MainViewModel.ErrorString = String.Format("picons download: {0}", ex.Message)
        '    End Try
        'End Function
    End Class
End Namespace