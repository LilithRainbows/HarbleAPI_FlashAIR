Public Class NamespaceCache
    Public OldNamespaceName As String
    Public NewNamespaceName As String

    Public Sub New(OldNamespaceName As String, NewNamespaceName As String)
        Me.OldNamespaceName = OldNamespaceName
        Me.NewNamespaceName = NewNamespaceName
    End Sub
End Class
