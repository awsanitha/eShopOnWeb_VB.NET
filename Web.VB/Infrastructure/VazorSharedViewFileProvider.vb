Imports System.Collections.Concurrent
Imports System.IO
Imports System.Reflection
Imports Microsoft.Extensions.FileProviders
Imports Microsoft.Extensions.Primitives
Imports Vazor

Namespace Infrastructure

    ''' <summary>
    ''' Serves the Vazor shared views (_ViewImports, _ViewStart, _Layout and the shared
    ''' partials) to Razor runtime compilation in a path-separator independent way.
    ''' </summary>
    ''' <remarks>
    ''' Vazor's own <c>VazorViewMapper.AddStatic</c> stores each shared view under a key
    ''' built with <c>IO.Path.Combine</c>, while every lookup is normalised to Windows
    ''' backslashes by <c>ViewInfo.FixPath</c>. On Windows both sides produce
    ''' "Pages\_ViewImports.cshtml" and match. On Linux the stored key becomes
    ''' "Pages/_ViewImports.cshtml" while the lookup still asks for
    ''' "Pages\_ViewImports.cshtml", so no shared view is ever found and every view that
    ''' depends on _ViewImports/_ViewStart fails to compile at runtime.
    '''
    ''' This provider resolves the shared views itself using separator-independent keys,
    ''' so the Vazor/ZML engine behaves identically on Windows and Linux. Dynamic
    ''' per-request pages created by <c>VazorPage.CreateNew</c> are keyed by name only
    ''' (no directory separator) and are unaffected, so they continue to be served by
    ''' <c>Vazor.VazorViewProvider</c>, which stays registered alongside this provider.
    ''' </remarks>
    Public Class VazorSharedViewFileProvider
        Implements IFileProvider

        Private ReadOnly _viewAssembly As Assembly
        Private ReadOnly _views As Lazy(Of ConcurrentDictionary(Of String, Byte()))

        Public Sub New(viewAssembly As Assembly)
            _viewAssembly = viewAssembly
            ' Resolved lazily so that generation happens after Startup.Configure has run
            ' ZML.ZmlPages.Compile() and Vazor.VazorSharedView.CreateAll().
            _views = New Lazy(Of ConcurrentDictionary(Of String, Byte()))(AddressOf BuildViewMap)
        End Sub

        Private Function BuildViewMap() As ConcurrentDictionary(Of String, Byte())
            Dim map As New ConcurrentDictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)

            Dim sharedViewTypes =
                From candidate In _viewAssembly.GetTypes()
                Where candidate.IsClass AndAlso
                      Not candidate.IsAbstract AndAlso
                      candidate.IsSubclassOf(GetType(VazorSharedView)) AndAlso
                      candidate.GetConstructor(Type.EmptyTypes) IsNot Nothing

            For Each viewType As Type In sharedViewTypes
                Dim view = TryCast(Activator.CreateInstance(viewType), VazorSharedView)
                If view Is Nothing Then Continue For
                map(NormalizeKey(view.Path & "/" & view.Name & ".cshtml")) = view.Content
            Next

            Return map
        End Function

        ''' <summary>Normalises a view path to '/' separators with no leading/trailing separator.</summary>
        Private Shared Function NormalizeKey(path As String) As String
            If String.IsNullOrEmpty(path) Then Return String.Empty
            Return path.Replace("\"c, "/"c).Trim("/"c)
        End Function

        Public Function GetFileInfo(subpath As String) As IFileInfo Implements IFileProvider.GetFileInfo
            Dim content As Byte() = Nothing
            If Not String.IsNullOrEmpty(subpath) AndAlso
               _views.Value.TryGetValue(NormalizeKey(subpath), content) Then
                Return New VazorSharedViewFileInfo(subpath, content)
            End If

            Return New NotFoundFileInfo(If(subpath, String.Empty))
        End Function

        Public Function GetDirectoryContents(subpath As String) As IDirectoryContents Implements IFileProvider.GetDirectoryContents
            Return NotFoundDirectoryContents.Singleton
        End Function

        Public Function Watch(filter As String) As IChangeToken Implements IFileProvider.Watch
            ' Shared views are generated once from compiled VB, so they never change on disk.
            Return NullChangeToken.Singleton
        End Function

    End Class

    ''' <summary>An in-memory <see cref="IFileInfo"/> over a generated Vazor shared view.</summary>
    Friend NotInheritable Class VazorSharedViewFileInfo
        Implements IFileInfo

        Private ReadOnly _name As String
        Private ReadOnly _content As Byte()

        Public Sub New(subpath As String, content As Byte())
            _name = Path.GetFileName(subpath.Replace("\"c, "/"c))
            _content = content
        End Sub

        Public ReadOnly Property Exists As Boolean Implements IFileInfo.Exists
            Get
                Return True
            End Get
        End Property

        Public ReadOnly Property IsDirectory As Boolean Implements IFileInfo.IsDirectory
            Get
                Return False
            End Get
        End Property

        Public ReadOnly Property LastModified As DateTimeOffset Implements IFileInfo.LastModified
            Get
                Return DateTimeOffset.MinValue
            End Get
        End Property

        Public ReadOnly Property Length As Long Implements IFileInfo.Length
            Get
                Return _content.LongLength
            End Get
        End Property

        Public ReadOnly Property Name As String Implements IFileInfo.Name
            Get
                Return _name
            End Get
        End Property

        Public ReadOnly Property PhysicalPath As String Implements IFileInfo.PhysicalPath
            Get
                Return Nothing
            End Get
        End Property

        Public Function CreateReadStream() As Stream Implements IFileInfo.CreateReadStream
            ' A fresh stream per call: Razor may read the same shared view many times.
            Return New MemoryStream(_content, writable:=False)
        End Function

    End Class

End Namespace
