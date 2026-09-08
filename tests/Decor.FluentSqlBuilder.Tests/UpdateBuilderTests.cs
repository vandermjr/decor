using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;

namespace Decor.FluentSqlBuilder.Tests
{
    public class UpdateBuilderTests
    {

        private static string NormalizeSqlString(string sql)
        {
            return sql.Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }
        private readonly FluentCommandBuilder _builder;

        public UpdateBuilderTests()
        {
            _builder = FluentCommandBuilder.Create(new MariaDBDialect());
        }

        [Fact]
        public void Build_ShouldCreateUpdateQueryWithSetAndWhereClause()
        {
            // Act
            var (sql, parameters) = _builder
                .Update().Table<Product>()
                .Set((Product prod) => prod.Description, "Teclado Mecânico")
                .Where(w => w.Equals((Product item) => item.ProductID, 10))
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("UPDATE products AS p SET p.Description = @Description WHERE p.ProductID = @ProductID;"));
            parameters.Should().ContainKeys("Description", "ProductID");
            parameters["Description"].Should().Be("Teclado Mecânico");
            parameters["ProductID"].Should().Be(10);
        }
    }
}