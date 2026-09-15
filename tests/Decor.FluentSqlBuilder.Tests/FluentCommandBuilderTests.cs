using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;

namespace Decor.FluentSqlBuilder.Tests
{
    public class FluentCommandBuilderTests
    {
        [Table("profiles")]
        private class Profile
        {
            [Key]
            public int ProfileID { get; set; }
            public string? Name { get; set; }
        }

        private class ExplicitColumnEntity
        {
            [Key]
            public int ID { get; set; }

            [Column("attribute_column")]
            public string Value { get; set; } = string.Empty;
        }

        [Fact]
        public void AutoAlias_ShouldGenerateAliasAutomatically_WhenNotExplicitlyRegistered()
        {
            // Arrange
            var builder = FluentCommandBuilder.Create(new MariaDBDialect());

            // Act
            var alias = builder.GetAliasForType(typeof(Product));

            // Assert
            alias.Should().Be("p");
            builder.GetTableNameForAlias("p").Should().Be("products");
        }

        [Fact]
        public void AutoAlias_ShouldHandleCollisionsWithNumberedSuffix()
        {
            // Arrange
            var builder = FluentCommandBuilder.Create(new MariaDBDialect());

            // Act
            var productAlias = builder.GetAliasForType(typeof(Product));
            var profileAlias = builder.GetAliasForType(typeof(Profile));

            // Assert
            productAlias.Should().Be("p");
            profileAlias.Should().Be("p1");
            builder.GetTableNameForAlias("p").Should().Be("products");
            builder.GetTableNameForAlias("p1").Should().Be("profiles");
        }

        [Fact]
        public void AutoAlias_ShouldSuffixReservedSqlKeyword()
        {
            var builder = FluentCommandBuilder.Create(new MariaDBDialect());

            var alias = builder.GetAliasForType(typeof(OccurrenceReason));

            alias.Should().Be("or1");
            builder.GetTableNameForAlias("or1").Should().Be("occurrence_reasons");
        }

        [Fact]
        public void LambdaParameter_ShouldBeIndependentFromAlias()
        {
            // Arrange & Act
            var (sql, parameters) = FluentCommandBuilder.Create(new MariaDBDialect())
                .Select(s => s.Columns<Product>(prod => prod.ProductID, item => item.Description))
                .From<Product>()
                .Where(w => w.Equals((Product anyVarName) => anyVarName.ProductID, 123))
                .Build();

            // Assert
            sql.Should().Contain("p.ProductID");
            sql.Should().Contain("p.Description");
            sql.Should().Contain("FROM\n    products AS p");
            sql.Should().Contain("WHERE p.ProductID = @ProductID");
            parameters["ProductID"].Should().Be(123);
        }

        [Fact]
#pragma warning disable CS0618 // Type or member is obsolete
        public void RegisterAlias_ShouldStoreAliasAndTableName_ForBackwardCompatibility()
        {
            // Arrange
            var builder = FluentCommandBuilder.Create(new MariaDBDialect());

            // Act
            builder.RegisterAlias<Product>("prod_alias");

            // Assert
            var alias = builder.GetAliasForType(typeof(Product));
            alias.Should().Be("prod_alias");
            builder.GetTableNameForAlias("prod_alias").Should().Be("products");
        }

        [Fact]
        public void RegisterAlias_ShouldThrowExceptionForDuplicateType_ForBackwardCompatibility()
        {
            // Arrange
            var builder = FluentCommandBuilder.Create(new MariaDBDialect())
                .RegisterAlias<Product>("p");

            // Act & Assert
            Action act = () => builder.RegisterAlias<Product>("product");
            act.Should().Throw<ArgumentException>()
                .WithMessage("O tipo 'Product' já foi registrado com o alias 'p'.");
        }
#pragma warning restore CS0618

        [Fact]
        public void RegisterColumn_ShouldUseExplicitColumnName()
        {
            var (sql, _) = FluentCommandBuilder.Create(new MariaDBDialect())
                .RegisterColumn<ExplicitColumnEntity>(nameof(ExplicitColumnEntity.Value), "registered_column")
                .Select(s => s.Columns<ExplicitColumnEntity>(e => e.Value))
                .From<ExplicitColumnEntity>()
                .Build();

            sql.Should().Contain("e.registered_column");
        }

        [Fact]
        public void RegisterColumn_ShouldTakePriorityOverAttributeAndPropertyName()
        {
            var (sql, _) = FluentCommandBuilder.Create(new MariaDBDialect())
                .RegisterColumn<ExplicitColumnEntity>(nameof(ExplicitColumnEntity.Value), "registered_column")
                .Select(s => s.AllColumns<ExplicitColumnEntity>(explicitColumns: true))
                .From<ExplicitColumnEntity>()
                .Build();

            sql.Should().Contain("e.registered_column AS Value");
            sql.Should().NotContain("attribute_column");
        }

        [Fact]
        public void ColumnAttribute_ShouldRemainTheFallbackWithoutExplicitRegistration()
        {
            var (sql, _) = FluentCommandBuilder.Create(new MariaDBDialect())
                .Select(s => s.Columns<ExplicitColumnEntity>(e => e.Value))
                .From<ExplicitColumnEntity>()
                .Build();

            sql.Should().Contain("e.attribute_column AS Value");
        }
    }
}