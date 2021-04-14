Imports Flazzy
Imports Flazzy.ABC
Imports Flazzy.IO
Imports Flazzy.Records
Imports Flazzy.Tags

Public Class FlashFile
        Inherits ShockwaveFlash

        Public Sub New(ByVal path As String)
            MyBase.New(path)
            AbcFiles = New List(Of ABCFile)()
            AbcTagFiles = New Dictionary(Of DoABCTag, ABCFile)()
        End Sub

        Public ReadOnly Property AbcFiles As List(Of ABCFile)
        Public ReadOnly Property AbcTagFiles As Dictionary(Of DoABCTag, ABCFile)

        Protected Overrides Sub WriteTag(ByVal tag As TagItem, ByVal output As FlashWriter)
            If tag.Kind = TagKind.DoABC Then
                Dim abcTag = CType(tag, DoABCTag)
                abcTag.ABCData = AbcTagFiles(abcTag).ToArray()
            End If

            MyBase.WriteTag(tag, output)
        End Sub

        Protected Overrides Function ReadTag(ByVal header As HeaderRecord, ByVal input As FlashReader) As TagItem
            Dim tag = MyBase.ReadTag(header, input)

            If tag.Kind = TagKind.DoABC Then
                Dim abcTag = CType(tag, DoABCTag)
                Dim abcFile = New ABCFile(abcTag.ABCData)
                AbcTagFiles.Add(abcTag, abcFile)
                AbcFiles.Add(abcFile)
            End If

            Return tag
        End Function
End Class