
namespace Raven.Server.Documents.Queries.AST
{
    public abstract class QueryExpression
    {
        public ExpressionType Type;

        public abstract override string ToString();

        public abstract string GetText();
    }
}
