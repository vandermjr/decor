using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;

namespace Decor.FluentSqlBuilder.Tests
{
    public class DeleteBuilderTests
    {

        private static string NormalizeSqlString(string sql)
        {
            return sql.Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }
        private readonly FluentCommandBuilder _builder;

        public DeleteBuilderTests()
        {
            _builder = FluentCommandBuilder.Create(new MariaDBDialect());
        }

        [Fact]
        public void Build_ShouldCreateDeleteQueryWithWhereClause()
        {
            // Act
            var (sql, parameters) = _builder
                .Delete<Product>()
                .Where(w => w.Equals((Product p) => p.ProductID, 5))
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("DELETE p FROM products AS p WHERE p.ProductID = @ProductID;"));
            parameters.Should().ContainKey("ProductID").And.ContainValue(5);
        }
    }
}