namespace Decor.FluentSqlBuilder.Dialects
{
    /// <summary>
    /// Fornece palavras-chave e operadores SQL comuns, baseados no padrão ANSI SQL.
    /// Esta classe é abstrata para permitir que dialetos de banco de dados a herdem.
    /// </summary>
    public class SqlKeywords
    {
        // Palavras-chave de DML (Data Manipulation Language)
        public virtual string SELECT { get; } = "SELECT";
        public virtual string INSERT { get; } = "INSERT";
        public virtual string INTO { get; } = "INTO";
        public virtual string UPDATE { get; } = "UPDATE";
        public virtual string DELETE { get; } = "DELETE";
        public virtual string FROM { get; } = "FROM";
        public virtual string VALUES { get; } = "VALUES";
        public virtual string SET { get; } = "SET";

        // Palavras-chave de DDL (Data Definition Language)
        public virtual string CREATE => "CREATE";
        public virtual string ALTER => "ALTER";
        public virtual string DROP => "DROP";
        public virtual string TABLE => "TABLE";
        public virtual string DATABASE => "DATABASE";
        public virtual string INDEX => "INDEX";
        public virtual string CONSTRAINT => "CONSTRAINT";
        public virtual string PRIMARY_KEY => "PRIMARY KEY";
        public virtual string FOREIGN_KEY => "FOREIGN KEY";
        public virtual string REFERENCES => "REFERENCES";
        public virtual string UNIQUE => "UNIQUE";
        public virtual string CHECK => "CHECK";
        public virtual string DEFAULT => "DEFAULT";
        public virtual string NOT_NULL => "NOT NULL";

        // Palavras-chave de DQL (Data Query Language) e Operadores Lógicos
        public virtual string WHERE { get; } = "WHERE";
        public virtual string AND { get; } = "AND";
        public virtual string OR { get; } = "OR";
        public virtual string NOT => "NOT";
        public virtual string BETWEEN => "BETWEEN";
        public virtual string IN => "IN";
        public virtual string LIKE { get; } = "LIKE";
        public virtual string IS => "IS";
        public virtual string NULL => "NULL";
        public virtual string HAVING => "HAVING";
        public virtual string GROUP_BY => "GROUP BY";

        // Operadores de Comparação
        public virtual string EQUALS { get; } = "=";
        public virtual string NOT_EQUALS => "<>"; // Padrão ANSI SQL
        public virtual string GREATER_THAN { get; } = ">";
        public virtual string LESS_THAN { get; } = "<";
        public virtual string GREATER_THAN_OR_EQUALS { get; } = ">=";
        public virtual string LESS_THAN_OR_EQUALS { get; } = "<=";

        // Junções (JOINs)
        public virtual string INNER_JOIN { get; } = "INNER JOIN";
        public virtual string LEFT_OUTER_JOIN { get; } = "LEFT OUTER JOIN";
        public virtual string RIGHT_OUTER_JOIN { get; } = "RIGHT OUTER JOIN";
        public virtual string FULL_OUTER_JOIN { get; } = "FULL OUTER JOIN";
        public virtual string ON { get; } = "ON";

        // Ordenação
        public virtual string ORDER_BY { get; } = "ORDER BY";
        public virtual string ASC => "ASC";
        public virtual string DESC => "DESC";

        // Paginação e Limites (Padrão ANSI SQL:2008)
        public virtual string OFFSET => "OFFSET";
        public virtual string ROWS => "ROWS";
        public virtual string FETCH => "FETCH";
        public virtual string NEXT => "NEXT";
        public virtual string ONLY => "ONLY";

        // Funções de Agregação
        public virtual string COUNT => "COUNT";
        public virtual string AVG => "AVG";
        public virtual string SUM => "SUM";
        public virtual string MIN => "MIN";
        public virtual string MAX => "MAX";


        public virtual string CONCAT => "CONCAT";

        // Outras palavras-chave
        public virtual string AS { get; } = "AS";
        public virtual string ALL => "ALL";
        public virtual string DISTINCT => "DISTINCT";
        public virtual string UNION => "UNION";
        public virtual string EXCEPT => "EXCEPT";
        public virtual string INTERSECT => "INTERSECT";
        public virtual string ASTERISK => "*";
        public virtual string COMMA => ", ";
        public virtual string SEMICOLON => ";";
        public virtual string LEFT_PARENTHESIS => "(";
        public virtual string RIGHT_PARENTHESIS => ")";
        public virtual string DOT { get; } = ".";
        public virtual string SINGLE_QUOTE { get; } = "'";

        // Wildcards (Caracteres curinga)        
        public virtual string ANY_STRING_WILDCARD { get; } = "%";
        public virtual string ANY_SINGLE_CHARACTER_WILDCARD { get; } = "_";

        // Caracteres de escape de identificadores
        public virtual string PARAMETER_PREFIX { get; } = "@";
        public virtual string IDENTIFIER_PREFIX => "[";
        public virtual string IDENTIFIER_SUFFIX => "]";
    }
}