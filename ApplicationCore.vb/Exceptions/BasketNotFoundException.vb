Option Explicit On
Option Infer On
Option Strict On

Namespace Exceptions
    Public Class BasketNotFoundException
        Inherits Exception
        Public Sub New(basketId As Integer)
            MyBase.New($"No basket found with id {basketId}")
        End Sub

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        Public Sub New(message As String, innerException As Exception)
            MyBase.New(message, innerException)
        End Sub
    End Class
End Namespace
