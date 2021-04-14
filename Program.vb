Imports System.IO
Imports Flazzy.ABC
Imports Flazzy.ABC.AVM2.Instructions
Imports Newtonsoft.Json.Linq

Module Program
    Private Const TargetSwf As String = "Revisions\HabboAir.swf"
    Dim HarbleNamespaceCache As New List(Of NamespaceCache)

    Sub Main(ByVal args As String())
        IO.Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location))
        Console.Title = "HarbleAPI for FlashAIR"
        Try
            If IO.File.Exists(TargetSwf) = False Then
                Console.WriteLine("HabboAir.swf not found.")
                Console.WriteLine("")
                Exit Try
            End If
            Console.WriteLine("Dumping messages ...")
            Dim Flash = New FlashFile(TargetSwf)
            Flash.Disassemble()
            Dim HarbleAPIRevision As String = "UNKNOWN"
            Dim HarbleOutgoingMessages As New List(Of HarbleMessage)
            Dim HarbleIncomingMessages As New List(Of HarbleMessage)
            Dim HarbleUnknownMessages As New List(Of HarbleMessage)
            For Each TempClass In flash.AbcFiles(2).Classes
                Dim TempClassName As String = GetNewClassName(TempClass)
                If TempClassName = "HabboMessages" Then
                    Dim TempClassCode = TempClass.Constructor.Body.ParseCode()
                    For i As Integer = 0 To TempClassCode.Count - 1
                        Dim TempClassCodeInstruction As ASInstruction = TempClassCode(i)
                        Dim MessageDetected As Boolean = False
                        Dim MessageOutgoing As Boolean = False
                        Dim MessageID As Integer = 0
                        Dim MessageOldClassName As String = ""
                        Dim MessageNewClassName As String = ""
                        Dim MessageHashCode As String = ""
                        Dim MessageNamespace As String = ""
                        If TempClassCodeInstruction.OP = OPCode.PushByte Then
                            MessageID = CType(TempClassCodeInstruction, PushByteIns).Value
                            MessageDetected = True
                        End If
                        If TempClassCodeInstruction.OP = OPCode.PushShort Then
                            MessageID = CType(TempClassCodeInstruction, PushShortIns).Value
                            MessageDetected = True
                        End If
                        If MessageDetected = True Then
                            TempClassCodeInstruction = TempClassCode(i - 1)
                            If TempClassCodeInstruction.OP = OPCode.GetLex Then
                                If CType(TempClassCodeInstruction, GetLexIns).TypeName.Name = "_composers" Then
                                    MessageOutgoing = True
                                End If
                            End If
                            TempClassCodeInstruction = TempClassCode(i + 1)
                            If TempClassCodeInstruction.OP = OPCode.GetLex Then
                                MessageOldClassName = CType(TempClassCodeInstruction, GetLexIns).TypeName.Name
                                MessageNamespace = CType(TempClassCodeInstruction, GetLexIns).TypeName.[Namespace].Name
                                MessageHashCode = CType(TempClassCodeInstruction, GetLexIns).TypeName.GetHashCode
                                MessageNewClassName = MessageOldClassName
                                If MessageOldClassName.StartsWith("_-") Then 'Try to parse name as Class
                                    MessageNewClassName = GetNewClassName(GetClassByName(flash.AbcFiles, MessageOldClassName))
                                    If String.IsNullOrWhiteSpace(MessageNewClassName) Then
                                        MessageNewClassName = MessageOldClassName
                                    End If
                                End If
                                If MessageNewClassName.StartsWith("_-") Then 'Try to parse name as Instance
                                    MessageNewClassName = GetNewInstanceName(GetInstanceByName(flash.AbcFiles, MessageOldClassName))
                                    If String.IsNullOrWhiteSpace(MessageNewClassName) Then
                                        HarbleUnknownMessages.Add(New HarbleMessage(CleanNewClassName(MessageOldClassName), MessageID, MessageHashCode, MessageOutgoing, MessageOldClassName, MessageNamespace))
                                        Continue For
                                    End If
                                End If
                            End If
                            If MessageOutgoing = True Then
                                HarbleOutgoingMessages.Add(New HarbleMessage(CleanNewClassName(MessageNewClassName), MessageID, MessageHashCode, MessageOutgoing, MessageOldClassName, MessageNamespace))
                                If MessageID = 4000 And MessageOutgoing = True Then 'Outgoing 4000=ClientHello
                                    HarbleAPIRevision = GetRevisionByClassName(flash.AbcFiles, MessageOldClassName)
                                End If
                            Else
                                HarbleIncomingMessages.Add(New HarbleMessage(CleanNewClassName(MessageNewClassName), MessageID, MessageHashCode, MessageOutgoing, MessageOldClassName, MessageNamespace))
                            End If
                        End If
                    Next
                End If
            Next
            If HarbleIncomingMessages.Count + HarbleOutgoingMessages.Count + HarbleUnknownMessages.Count = 0 Then
                Console.WriteLine("No messages found.")
            Else
                Dim FixedHarbleUnknownMessages As New List(Of HarbleMessage)
                For Each UnknownMessage In HarbleUnknownMessages
                    Dim UnknownMessageName As String = "Unknown" & GetMinifiedNamespaceFromCache(UnknownMessage.ClassNamespace) & "Message"
                    Dim UnknownMessageNameCount As Integer = FixedHarbleUnknownMessages.FindAll(Function(x) x.Name.StartsWith(UnknownMessageName)).Count
                    UnknownMessageName = UnknownMessageName & "_" & UnknownMessageNameCount
                    Dim NewUnknownMessage = New HarbleMessage(UnknownMessageName, UnknownMessage.ID, UnknownMessage.Hash, UnknownMessage.IsOutgoing, UnknownMessage.ClassName, UnknownMessage.ClassNamespace)
                    FixedHarbleUnknownMessages.Add(NewUnknownMessage)
                    If NewUnknownMessage.IsOutgoing Then
                        HarbleOutgoingMessages.Add(NewUnknownMessage)
                    Else
                        HarbleIncomingMessages.Add(NewUnknownMessage)
                    End If
                Next
                Dim HarbleJSON As JObject = New JObject()
                HarbleJSON.Add(New JProperty("Revision", HarbleAPIRevision))
                HarbleJSON.Add(New JProperty("Outgoing", GetHarbleMessagesJArray(HarbleOutgoingMessages)))
                HarbleJSON.Add(New JProperty("Incoming", GetHarbleMessagesJArray(HarbleIncomingMessages)))
                File.WriteAllText("HARBLE_API-" & HarbleAPIRevision, HarbleJSON.ToString)
                Console.WriteLine("Destination file: " & "HARBLE_API-" & HarbleAPIRevision)
                Console.WriteLine("Saved " & HarbleOutgoingMessages.Count & " outgoing messages and " & HarbleIncomingMessages.Count & " incoming messages.")
                Console.WriteLine("Unknown messages: " & HarbleUnknownMessages.Count)
            End If
            Console.WriteLine("")
        Catch ex As Exception
            Console.WriteLine("")
            Console.WriteLine("An error occurred.")
        End Try
        Console.WriteLine("Press ENTER to exit ...")
        Do While Console.ReadKey(True).Key = ConsoleKey.Enter = False
            'Wait until user press ENTER
        Loop
        Environment.Exit(0)
    End Sub

    Function GetRevisionByClassName(ByVal ABCFiles As List(Of ABCFile), ByVal OldClassName As String) As String
        Try
            Dim TempClassCode = GetInstanceByName(ABCFiles, OldClassName).GetMethod("getMessageArray").Body.ParseCode()
            For i As Integer = 0 To TempClassCode.Count - 1
                Dim TempClassCodeInstruction As ASInstruction = TempClassCode(i)
                If TempClassCodeInstruction.OP = OPCode.PushString Then
                    Return CType(TempClassCodeInstruction, PushStringIns).Value
                End If
            Next
        Catch
            'Revision not found
        End Try
        Return "UNKNOWN"
    End Function

    Function GetHarbleMessagesJArray(ByVal HarbleMessages As List(Of HarbleMessage)) As JArray
        Dim HarbleMessagesJArray As New JArray()
        For Each HarbleMessage In HarbleMessages
            Dim NewHarbleJMessage As New JObject()
            NewHarbleJMessage.Add(New JProperty("Name", HarbleMessage.Name))
            NewHarbleJMessage.Add(New JProperty("Id", HarbleMessage.ID))
            NewHarbleJMessage.Add(New JProperty("Hash", HarbleMessage.Hash))
            NewHarbleJMessage.Add(New JProperty("IsOutgoing", HarbleMessage.IsOutgoing))
            NewHarbleJMessage.Add(New JProperty("ClassName", HarbleMessage.ClassName))
            HarbleMessagesJArray.Add(NewHarbleJMessage)
        Next
        Return HarbleMessagesJArray
    End Function

    Function GetNewInstanceName(ByVal RequestedInstance As ASInstance) As String
        Try
            Return RequestedInstance.Constructor.Name
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Function GetNewClassName(ByVal RequestedClass As ASClass) As String
        Try
            Dim ClassTrait = RequestedClass.Traits.Concat(RequestedClass.Instance.Traits).FirstOrDefault(Function(x) x.QName.[Namespace].Kind = NamespaceKind.[Private])
            Dim ClassNames = ClassTrait.QName.[Namespace].Name.Split({":"c}, 2, StringSplitOptions.None)
            Dim OldClassNamespace = RequestedClass.QName.[Namespace].Name
            Dim NewClassNamespace = If(ClassNames.Length = 2, ClassNames(0), String.Empty)
            Dim NewClassName = If(ClassNames.Length = 2, ClassNames(1), ClassNames(0))
            If String.IsNullOrWhiteSpace(NewClassNamespace) = False Then
                If NewClassNamespace = OldClassNamespace = False Then
                    If HarbleNamespaceCache.Exists(Function(x) x.OldNamespaceName = OldClassNamespace) = False Then
                        HarbleNamespaceCache.Add(New NamespaceCache(OldClassNamespace, NewClassNamespace))
                    End If
                End If
            End If
            Return NewClassName
        Catch
            Return ""
        End Try
    End Function

    Function GetMinifiedNamespaceFromCache(ByVal OldNamespace As String) As String
        Dim NewNamespace As String
        Try
            NewNamespace = HarbleNamespaceCache.First(Function(x) x.OldNamespaceName = OldNamespace).NewNamespaceName
        Catch
            NewNamespace = OldNamespace
        End Try
        If NewNamespace.Contains(".") Then
            NewNamespace = NewNamespace.Remove(0, NewNamespace.LastIndexOf(".") + 1)
        End If
        Return Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(NewNamespace.ToLower)
    End Function

    Function GetClassByName(ByVal ABCFiles As List(Of ABCFile), ByVal RequestedClass As String) As ASClass
        For Each ABCfile In ABCFiles
            Try
                Return ABCfile.Classes.First(Function(x) x.QName.Name = RequestedClass)
            Catch
                'Class not found in ABCfile
            End Try
        Next
        Return Nothing
    End Function

    Function GetInstanceByName(ByVal ABCFiles As List(Of ABCFile), ByVal RequestedInstance As String) As ASInstance
        For Each ABCfile In ABCFiles
            Try
                Return ABCfile.Instances.First(Function(x) x.QName.Name = RequestedInstance)
            Catch
                'Instance not found in ABCfile
            End Try
        Next
        Return Nothing
    End Function

    Function CleanNewClassName(ByVal NewClassName As String) As String
        Dim CleanedClassName = NewClassName & "]"
        CleanedClassName = CleanedClassName.Replace("MessageEvent]", "")
        CleanedClassName = CleanedClassName.Replace("MessageComposer]", "")
        CleanedClassName = CleanedClassName.Replace("MessageParser]", "")
        CleanedClassName = CleanedClassName.Replace("Composer]", "")
        CleanedClassName = CleanedClassName.Replace("Event]", "")
        CleanedClassName = CleanedClassName.Replace("]", "")
        Return CleanedClassName
    End Function

End Module
