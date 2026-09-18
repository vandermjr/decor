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
                .Insert(i => i.Entity(newProduct))
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Contain(
                NormalizeSqlString("INSERT INTO products (Barcode, IsActive, Description, ManufacturerRef, AuxiliaryRef, Dimensions, Observations, StockQuantity, MinimumStock, BrandID, SubgroupID, StockUnitID, Origin, AcquisitionMode, ProductType, CostPrice, SalePrice, EmployeeCommissionValue, DefaultInstallationServiceID) VALUES (@Barcode, @IsActive, @Description, @ManufacturerRef, @AuxiliaryRef, @Dimensions, @Observations, @StockQuantity, @MinimumStock, @BrandID, @SubgroupID, @StockUnitID, @Origin, @AcquisitionMode, @ProductType, @CostPrice, @SalePrice, @EmployeeCommissionValue, @DefaultInstallationServiceID);"));
            parameters.Should().ContainKeys("Barcode", "IsActive", "Description", "ManufacturerRef", "AuxiliaryRef", "Dimensions", "Observations", "StockQuantity", "MinimumStock", "BrandID", "SubgroupID", "StockUnitID", "Origin", "AcquisitionMode", "ProductType", "CostPrice", "SalePrice", "EmployeeCommissionValue", "DefaultInstallationServiceID");
            parameters["Barcode"].Should().Be("12345");
            parameters["IsActive"].Should().Be(true);
            parameters["Description"].Should().Be("Mouse sem fio");
            parameters["StockQuantity"].Should().Be(50);
            parameters["BrandID"].Should().Be(1);
            parameters["SubgroupID"].Should().Be(1);
        }

        [Fact]
        public void Build_WithEntityConfiguration_ShouldCreateInsertQuery()
        {
            var entity = new Brand { BrandName = "Marca Nova" };

            var (sql, parameters) = _builder
                .Insert(i => i.Entity(entity))
                .Build();

            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("INSERT INTO brands (BrandName) VALUES (@BrandName);"));
            parameters.Should().ContainKey("BrandName");
            parameters["BrandName"].Should().Be("Marca Nova");
        }

        [Fact]
        public void Build_WithReturningGeneratedId_ShouldAppendLastInsertIdSelect()
        {
            var (sql, _) = _builder
                .Insert(i => i.Entity(new Brand { BrandName = "Marca X" }))
                .ReturningGeneratedId()
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("INSERT INTO brands (BrandName) VALUES (@BrandName);"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("SELECT LAST_INSERT_ID();"));
        }

        [Fact]
        public void BuildBatch_WithEntitiesConfiguration_ShouldBuildBatch()
        {
            List<Brand> rows = [new Brand { BrandName = "Marca A" }, new Brand { BrandName = "Marca B" }];

            var (sql, parameters) = _builder
                .Insert(i => i.Entities(rows))
                .BuildBatch();

            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("INSERT INTO brands (BrandName) VALUES (@BrandName);"));
            parameters.Should().BeSameAs(rows);
            parameters.Cast<Brand>().Should().HaveCount(2);
        }

        [Fact]
        public void Build_WithEntityConfigurationAndReturningGeneratedId_ShouldAppendLastInsertIdSelect()
        {
            var (sql, parameters) = _builder
                .Insert(i => i.Entity(new Brand { BrandName = "Marca X" }))
                .ReturningGeneratedId()
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("INSERT INTO brands (BrandName) VALUES (@BrandName);"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("SELECT LAST_INSERT_ID();"));
            parameters["BrandName"].Should().Be("Marca X");
        }
    }
}