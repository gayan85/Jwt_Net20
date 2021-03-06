
Namespace XXXX.PetaJson


    Public Interface IJsonReader
        Function Parse(type As System.Type) As Object

        Function Parse(Of T)() As T

        Sub ParseInto(into As Object)

        Function ReadLiteral(converter As ReadCallback_t(Of Object, Object)) As Object

        Sub ParseDictionary(callback As WriteCallback_t(Of String))

        Sub ParseArray(callback As WriteCallback_t)

        Function GetLiteralKind() As LiteralKind

        Function GetLiteralString() As String

        Sub NextToken()
    End Interface


End Namespace
