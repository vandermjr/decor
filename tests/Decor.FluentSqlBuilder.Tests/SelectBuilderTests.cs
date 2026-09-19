using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Decor.FluentSqlBuilder.Statements;
using Xunit;

namespace Decor.FluentSqlBuilder.Tests
{
    public class SelectTests
    {
        private readonly FluentCommandBuilder _builder;

        public SelectTests()
        {
            _builder = FluentCommandBuilder.Create(new MariaDBDialect());
        }

        private static string NormalizeSqlString(string sql)
        {
            return sql.Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }

        [Fact]
        public void Select_AllColumns_ShouldGenerateAsterisk()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.AllColumns<Product>())
                .From<Product>()
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT p.* FROM products AS p ORDER BY p.ProductID ASC;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_AllColumnsExplicit_ShouldListAllColumnsExcludingNotMapped()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.AllColumns<Product>(true))
                .From<Product>()
                .Build();

            // Assert
            string expectedSql = "SELECT p.ProductID, p.Barcode, p.IsActive, p.Description, p.ManufacturerRef, p.AuxiliaryRef, p.Dimensions, p.Observations, p.StockQuantity, p.MinimumStock, p.BrandID, p.SubgroupID, p.StockUnitID, p.Origin, p.AcquisitionMode, p.ProductType, p.CostPrice, p.SalePrice, p.EmployeeCommissionValue, p.DefaultInstallationServiceID FROM products AS p ORDER BY p.ProductID ASC;";
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString(expectedSql));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_SpecificColumns_ShouldListGivenColumns()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.WithColumns<Product>(p => p.ProductID, p => p.Description))
                .From<Product>()
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT p.ProductID, p.Description FROM products AS p ORDER BY p.ProductID ASC;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_MultiTable_ShouldListColumnsFromAllTables()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s =>
                {
                    s.WithColumns<Product>(p => p.ProductID);
                    s.WithColumns<Brand>(b => b.BrandName);
                })
                .From<Product>()
                .Join(j => j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID))
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT p.ProductID, b.BrandName FROM products AS p INNER JOIN brands AS b ON p.BrandID = b.BrandID ORDER BY p.ProductID ASC;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_ExceptWithExplicitColumns_ShouldExcludeSpecifiedColumns()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.AllColumns<Product>(true).ExceptColumns<Product>(p => p.Description, p => p.Barcode))
                .From<Product>()
                .Build();

            // Assert
            string expectedSql = "SELECT p.ProductID, p.IsActive, p.ManufacturerRef, p.AuxiliaryRef, p.Dimensions, p.Observations, p.StockQuantity, p.MinimumStock, p.BrandID, p.SubgroupID, p.StockUnitID, p.Origin, p.AcquisitionMode, p.ProductType, p.CostPrice, p.SalePrice, p.EmployeeCommissionValue, p.DefaultInstallationServiceID FROM products AS p ORDER BY p.ProductID ASC;";
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString(expectedSql));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_CountNoParameter_ShouldGenerateCountAsterisk()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.Count())
                .From<Product>()
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT COUNT(*) FROM products AS p;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_CountConstantOne_ShouldGenerateCountOne()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.Count(1))
                .From<Product>()
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT COUNT(1) FROM products AS p;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_CountTypedColumn_ShouldGenerateCountOnColumn()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.Count<Product>(p => p.ProductID))
                .From<Product>()
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT COUNT(p.ProductID) FROM products AS p;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_CountDistinct_ShouldGenerateCountDistinct()
        {
            // Act
            var (sql, parameters) = _builder
                .Select(s => s.Count<Product>(p => p.Description, true))
                .From<Product>()
                .Build();

            // Assert
            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT COUNT(DISTINCT p.Description) FROM products AS p;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_SumTypedColumn_ShouldGenerateSumOnColumn()
        {
            var (sql, parameters) = _builder
                .Select(s => s.Sum<Product>(p => p.StockQuantity))
                .From<Product>()
                .Build();

            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT SUM(p.StockQuantity) FROM products AS p;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_SumWithAlias_ShouldGenerateSumWithAlias()
        {
            var (sql, parameters) = _builder
                .Select(s => s.Sum<Product>(p => p.StockQuantity, "TotalStock"))
                .From<Product>()
                .Build();

            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT SUM(p.StockQuantity) AS TotalStock FROM products AS p;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_SumWithWhere_ShouldGenerateSumFilteredQuery()
        {
            var (sql, parameters) = _builder
                .Select(s => s.Sum<Product>(p => p.StockQuantity))
                .From<Product>()
                .Where(w => w.Equals<Product>(p => p.BrandID, 10))
                .Build();

            NormalizeSqlString(sql).Should().Be(NormalizeSqlString("SELECT SUM(p.StockQuantity) FROM products AS p WHERE p.BrandID = @BrandID;"));
            parameters.Should().ContainKey("BrandID");
            parameters["BrandID"].Should().Be(10);
        }

        // --- Testes de Falha ---

        [Fact]
        public void Select_ExceptWithoutExplicitAllColumns_ShouldSelectRemainingColumns()
        {
            var (sql, parameters) = _builder
                .Select(s => s.ExceptColumns<Product>(p => p.ProductID))
                .From<Product>()
                .Build();

            NormalizeSqlString(sql).Should().Contain(
                NormalizeSqlString("SELECT p.Barcode, p.IsActive, p.Description, p.ManufacturerRef, p.AuxiliaryRef, p.Dimensions, p.Observations, p.StockQuantity, p.MinimumStock, p.BrandID, p.SubgroupID, p.StockUnitID, p.Origin, p.AcquisitionMode, p.ProductType, p.CostPrice, p.SalePrice, p.EmployeeCommissionValue, p.DefaultInstallationServiceID FROM products AS p ORDER BY p.ProductID ASC;"));
            parameters.Should().BeEmpty();
        }

        [Fact]
        public void Select_ColumnsAndAllColumns_ShouldThrowException()
        {
            // Act & Assert
            Action act = () => _builder
                .Select(s =>
                {
                    s.WithColumns<Product>(p => p.ProductID);
                    s.AllColumns<Product>();
                })
                .From<Product>()
                .Build();

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Não é possível usar AllColumns com outra seleção de colunas para o mesmo tipo ('Product').");
        }

        [Fact]
        public void Select_CountWithColumns_ShouldThrowException()
        {
            // Act & Assert
            Action act = () => _builder
                .Select(s =>
                {
                    s.WithColumns<Product>(p => p.ProductID);
                    s.Count();
                })
                .From<Product>()
                .Build();

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Não é possível usar Count() com outras colunas selecionadas.");
        }
    }
}