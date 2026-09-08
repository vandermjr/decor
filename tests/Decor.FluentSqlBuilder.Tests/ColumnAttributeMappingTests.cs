using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;

namespace Decor.FluentSqlBuilder.Tests
{
    public class ColumnAttributeMappingTests
    {
        [Table("tb_produtos")]
        private class CustomMappedProduct
        {
            [Key]
            [Column("id_produto")]
            public int ProductID { get; set; }

            [Column("desc_produto")]
            public string? Description { get; set; }

            [Column("id_marca")]
            public int BrandID { get; set; }

            [NotMapped]
            public string? IgnoredProp { get; set; }
        }

        [Table("tb_marcas")]
        private class CustomMappedBrand
        {
            [Key]
            [Column("id_marca")]
            public int BrandID { get; set; }

            [Column("nome_marca")]
            public string? BrandName { get; set; }
        }

        private static FluentCommandBuilder CreateCommandBuilder()
        {
            return FluentCommandBuilder.Create(new MariaDBDialect());
        }

        [Fact]
        public void Select_ExplicitColumnsWithCustomColumnAttribute_ShouldUseColumnNamesWithAlias()
        {
            var (sql, _) = CreateCommandBuilder()
                .Select(s => s.Columns<CustomMappedProduct>(p => p.ProductID, p => p.Description))
                .From<CustomMappedProduct>()
                .Build();

            sql.Should().Contain("cmp.id_produto AS ProductID");
            sql.Should().Contain("cmp.desc_produto AS Description");
            sql.Should().Contain("FROM\n    tb_produtos AS cmp");
            sql.Should().Contain("ORDER BY\n    cmp.id_produto ASC");
        }

        [Fact]
        public void Select_AllExplicitColumnsWithCustomColumnAttribute_ShouldUseColumnNames()
        {
            var (sql, _) = CreateCommandBuilder()
                .Select(s => s.AllColumns<CustomMappedProduct>(explicitColumns: true))
                .From<CustomMappedProduct>()
                .Build();

            sql.Should().Contain("cmp.id_produto AS ProductID");
            sql.Should().Contain("cmp.desc_produto AS Description");
            sql.Should().Contain("cmp.id_marca AS BrandID");
            sql.Should().NotContain("IgnoredProp");
        }

        [Fact]
        public void Where_WithCustomColumnAttribute_ShouldUseColumnNameInPredicate()
        {
            var (sql, parameters) = CreateCommandBuilder()
                .Select(s => s.AllColumns<CustomMappedProduct>())
                .From<CustomMappedProduct>()
                .Where(w => w.Equals((CustomMappedProduct p) => p.Description, "Detergente"))
                .Build();

            sql.Should().Contain("WHERE cmp.desc_produto = @Description");
            parameters["Description"].Should().Be("Detergente");
        }

        [Fact]
        public void Join_WithCustomColumnAttribute_ShouldUseColumnNameInOnClause()
        {
            var (sql, _) = CreateCommandBuilder()
                .Select(s => s.Columns<CustomMappedProduct>(p => p.Description))
                .From<CustomMappedProduct>()
                .Join(j => j.Inner<CustomMappedProduct, CustomMappedBrand>((p, b) => p.BrandID == b.BrandID))
                .Build();

            sql.Should().Contain("INNER JOIN\n    tb_marcas AS cmb ON cmp.id_marca = cmb.id_marca");
        }

        [Fact]
        public void Insert_WithCustomColumnAttribute_ShouldUseColumnNamesInInsertAndPropertyNamesInParameters()
        {
            var entity = new CustomMappedProduct
            {
                ProductID = 10,
                Description = "SABÃO",
                BrandID = 5,
                IgnoredProp = "Ignore"
            };

            var (sql, parameters) = CreateCommandBuilder()
                .Insert()
                .Into<CustomMappedProduct>()
                .Values(entity)
                .Build();

            sql.Should().Contain("INSERT INTO tb_produtos (desc_produto, id_marca)");
            sql.Should().Contain("VALUES (@Description, @BrandID);");
            parameters["Description"].Should().Be("SABÃO");
            parameters["BrandID"].Should().Be(5);
            parameters.Should().NotContainKey("ProductID"); // Key ignored
            parameters.Should().NotContainKey("IgnoredProp");
        }

        [Fact]
        public void Update_WithCustomColumnAttribute_ShouldUseColumnNamesInSetAndWhere()
        {
            var (sql, parameters) = CreateCommandBuilder()
                .Update()
                .Table<CustomMappedProduct>()
                .Set((CustomMappedProduct p) => p.Description, "Novo Nome")
                .Where(w => w.Equals((CustomMappedProduct p) => p.ProductID, 1))
                .Build();

            sql.Should().Contain("UPDATE tb_produtos AS cmp");
            sql.Should().Contain("SET cmp.desc_produto = @Description");
            sql.Should().Contain("WHERE cmp.id_produto = @ProductID;");
            parameters["Description"].Should().Be("Novo Nome");
            parameters["ProductID"].Should().Be(1);
        }
    }
}