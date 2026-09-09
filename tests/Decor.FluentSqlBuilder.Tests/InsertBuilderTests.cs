using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;

namespace Decor.FluentSqlBuilder.Tests
{
    public class InsertBuilderTests
    {

        private static string NormalizeSqlString(string sql)
        {
            return sql.Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }
        private readonly FluentCommandBuilder _builder;

        public InsertBuilderTests()
        {
            _builder = FluentCommandBuilder.Create(new MariaDBDialect());
        }

        [Fact]
        public void Build_ShouldCreateInsertQuery_FromEntity()
        {
            // Arrange
            var newProduct = new Product
            {
                Barcode = "12345",
                Description = "Mouse sem fio",
                IsActive = true,
                StockQuantity = 50,
                BrandID = 1,
                SubgroupID = 1
            };

            // Act
            var (sql, parameters) = _builder
                .Insert().Into<Product>()
                .Values(newProduct)
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Contain(
                NormalizeSqlString("INSERT INTO products (Barcode, IsActive, Description, ManufacturerRef, AuxiliaryRef, Dimensions, Observations, StockQuantity, MinimumStock, BrandID, SubgroupID, Origin, AcquisitionMode) VALUES (@Barcode, @IsActive, @Description, @ManufacturerRef, @AuxiliaryRef, @Dimensions, @Observations, @StockQuantity, @MinimumStock, @BrandID, @SubgroupID, @Origin, @AcquisitionMode);"));
            parameters.Should().ContainKeys("Barcode", "IsActive", "Description", "ManufacturerRef", "AuxiliaryRef", "Dimensions", "Observations", "StockQuantity", "MinimumStock", "BrandID", "SubgroupID", "Origin", "AcquisitionMode");
            parameters["Barcode"].Should().Be("12345");
            parameters["IsActive"].Should().Be(true);
            parameters["Description"].Should().Be("Mouse sem fio");
            parameters["StockQuantity"].Should().Be(50);
            parameters["BrandID"].Should().Be(1);
            parameters["SubgroupID"].Should().Be(1);
        }

        [Fact]
        public void Build_WithReturningGeneratedId_ShouldAppendLastInsertIdSelect()
        {
            var (sql, _) = _builder
                .Insert().Into<Brand>()
                .Values(new Brand { BrandName = "Marca X" })
                .ReturningGeneratedId()
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("INSERT INTO brands (BrandName) VALUES (@BrandName);"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("SELECT LAST_INSERT_ID();"));
        }

        [Fact]
        public void ValuesBatch_WithConcreteList_ShouldBuildBatchAgainstDeclaredEntityType()
        {
            // Regressão: uma List<Brand> concreta também satisfaz Values<TEntity>(TEntity) com
            // TEntity=List<Brand>, então o método em lote precisa de um nome distinto (ValuesBatch)
            // para não ser escolhido incorretamente pela resolução de sobrecarga do C#.
            List<Brand> rows = [new Brand { BrandName = "Marca A" }, new Brand { BrandName = "Marca B" }];

            var (sql, parameters) = _builder
                .Insert().Into<Brand>()
                .ValuesBatch(rows)
                .BuildBatch();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("INSERT INTO brands (BrandName) VALUES (@BrandName);"));
            parameters.Should().BeSameAs(rows);
        }
    }
}