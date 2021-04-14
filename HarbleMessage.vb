Public Class HarbleMessage
    Public Name As String
    Public ID As String
    Public Hash As String
    Public IsOutgoing As Boolean
    Public ClassName As String
    Public ClassNamespace As String
    Public Sub New(Name As String, ID As String, Hash As String, IsOutgoing As Boolean, ClassName As String, ClassNamespace As String)
        Me.Name = Name
        Me.ID = ID
        Me.Hash = Hash
        Me.IsOutgoing = IsOutgoing
        Me.ClassName = ClassName
        Me.ClassNamespace = ClassNamespace
    End Sub
End Class
