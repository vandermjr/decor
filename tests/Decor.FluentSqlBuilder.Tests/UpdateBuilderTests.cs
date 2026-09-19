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
                .Update(u => u.Entity<Product>(prod => prod.Description, "Teclado Mecânico"))
                .Where(w => w.Equals<Product>(item => item.ProductID, 10))
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("UPDATE products AS p SET p.Description = @Description WHERE p.ProductID = @ProductID;"));
            parameters.Should().ContainKeys("Description", "ProductID");
            parameters["Description"].Should().Be("Teclado Mecânico");
            parameters["ProductID"].Should().Be(10);
        }

        [Fact]
        public void Build_WithEntityConfiguration_ShouldCreateUpdateQuery()
        {
            var entity = new Brand { BrandID = 10, BrandName = "Marca Atualizada" };

            var (sql, parameters) = _builder
                .Update(u => u.Entity(entity))
                .Where(w => w.Equals<Brand>(brand => brand.BrandID, entity.BrandID))
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("UPDATE brands AS b SET b.BrandName = @BrandName WHERE b.BrandID = @BrandID;"));
            parameters["BrandName"].Should().Be("Marca Atualizada");
            parameters["BrandID"].Should().Be(10);
        }

        [Fact]
        public void Build_WithPropertyEntityConfiguration_ShouldCreatePartialUpdateQuery()
        {
            var (sql, parameters) = _builder
                .Update(u => u.Entity<Product>(product => product.SalePrice, 100))
                .Where(w => w.Equals<Product>(product => product.ProductID, 10))
                .Build();

            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("UPDATE products AS p SET p.SalePrice = @SalePrice WHERE p.ProductID = @ProductID;"));
            parameters.Should().ContainKeys("SalePrice", "ProductID");
            parameters["SalePrice"].Should().Be(100);
            parameters["ProductID"].Should().Be(10);
        }
    }
}