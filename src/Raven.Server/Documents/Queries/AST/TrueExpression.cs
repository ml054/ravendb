namespace Raven.Server.Documents.Queries.AST
{
    public class TrueExpression : QueryExpression
    {
        public TrueExpression()
        {
            Type = ExpressionType.True;
        }

        public override string ToString()
        {
            return "true";
        }

        public override string GetText()
        {
            return ToString();
        }
    }
}
