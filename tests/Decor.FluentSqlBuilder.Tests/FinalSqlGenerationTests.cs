using System.ComponentModel.DataAnnotations.Schema;
using Decor.Core.Entities;
using Decor.FluentSqlBuilder;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace Decor.FluentSqlBuilder.Tests
{
    public class FinalSqlGenerationTests
    {
        // Método auxiliar para normalizar strings SQL para comparação
        private static string NormalizeSqlString(string sql)
        {
            return sql.Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final")]
        public void GeneratesCorrectComplexSelectSql()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s =>
                {
                    s.Columns<Product>(p => p.ProductID, p => p.Barcode, p => p.IsActive,
                                       p => p.Description, p => p.ManufacturerRef, p => p.AuxiliaryRef,
                                       p => p.Dimensions, p => p.Observations, p => p.StockQuantity,
                                       p => p.MinimumStock);
                    s.Columns<Brand>(b => b.BrandID, b => b.BrandName);
                    s.Columns<Class>(cl => cl.ClassID, cl => cl.ClassName);
                    s.Columns<Family>(fa => fa.FamilyID, fa => fa.FamilyName);
                    s.Columns<Group>(gr => gr.GroupID, gr => gr.GroupName);
                    s.Columns<Subgroup>(sg => sg.SubgroupID, sg => sg.SubgroupName);
                })
                .From<Product>()
                .Join(j =>
                {
                    j.Inner<Product, Brand>((p, b) => p.BrandID == b.BrandID);
                    j.Inner<Product, Subgroup>((p, sg) => p.SubgroupID == sg.SubgroupID);
                    j.Inner<Subgroup, Group>((sg, gr) => sg.GroupID == gr.GroupID);
                    j.Inner<Group, Family>((gr, fa) => gr.FamilyID == fa.FamilyID);
                    j.Inner<Family, Class>((fa, cl) => fa.ClassID == cl.ClassID);
                })
                .Where(w =>
                {
                    w.Equals((Product p) => p.ProductID, 999);
                    w.Or().Contains((Product p) => p.Description, "test desc");
                })
                .OrderBy("p.Description ASC")
                .Take(10000);

            string expectedSql = @"SELECT
                p.ProductID,
                p.Barcode,
                p.IsActive,
                p.Description,
                p.ManufacturerRef,
                p.AuxiliaryRef,
                p.Dimensions,
                p.Observations,
                p.StockQuantity,
                p.MinimumStock,
                b.BrandID,
                b.BrandName,
                c.ClassID,
                c.ClassName,
                f.FamilyID,
                f.FamilyName,
                g.GroupID,
                g.GroupName,
                s.SubgroupID,
                s.SubgroupName
            FROM
                products AS p
            INNER JOIN
                brands AS b ON p.BrandID = b.BrandID
            INNER JOIN
                subgroups AS s ON p.SubgroupID = s.SubgroupID
            INNER JOIN
                groups AS g ON s.GroupID = g.GroupID
            INNER JOIN
                families AS f ON g.FamilyID = f.FamilyID
            INNER JOIN
                classes AS c ON f.ClassID = c.ClassID
            WHERE p.ProductID = @ProductID OR p.Description LIKE CONCAT('%', @Description, '%')
            ORDER BY
                p.Description ASC
            LIMIT 10000;";

            var (actualSql, actualParameters) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));

            Assert.Contains("ProductID", actualParameters.Keys);
            Assert.Contains("Description", actualParameters.Keys);
            Assert.Equal(2, actualParameters.Count);

            Assert.Equal(999, actualParameters["ProductID"]);
            Assert.Equal("test desc", actualParameters["Description"]);
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - COUNT")]
        public void GeneratesCorrectCountSql_WithWhere()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Count())
                .From<Brand>()
                .Where(w => w.Equals((Brand b) => b.BrandID, 50));

            string expectedSql = @"SELECT
                COUNT(*)
            FROM
                brands AS b
            WHERE b.BrandID = @BrandID;";

            var (actualSql, actualParameters) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));

            Assert.Single(actualParameters.Keys);
            Assert.Contains("BrandID", actualParameters.Keys);

            Assert.Equal(50, actualParameters["BrandID"]);
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - COUNT")]
        public void GeneratesCorrectCountSql_WithoutWhere()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Count(1))
                .From<Product>();

            string expectedSql = @"SELECT
                COUNT(1)
            FROM
                products AS p;";

            var (actualSql, actualParameters) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));

            Assert.Empty(actualParameters.Keys);
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - COUNT")]
        public void CountSql_IgnoresOrderByAndLimit()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Count(1))
                .From<Product>()
                .OrderBy("p.Description ASC")
                .Take(100);

            string expectedSql = @"SELECT
                COUNT(1)
            FROM
                products AS p;";

            var (actualSql, actualParameters) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
            Assert.Empty(actualParameters.Keys);
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - Paginação")]
        public void GeneratesCorrectSqlWithTakeAndSkip()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Columns<Product>(p => p.ProductID))
                .From<Product>()
                .OrderBy("p.ProductID ASC")
                .Take(10)
                .Skip(5);

            string expectedSql = @"SELECT
            p.ProductID
            FROM
                products AS p
            ORDER BY
                p.ProductID ASC
            LIMIT 10 OFFSET 5;"; // MySQL/MariaDB syntax for LIMIT offset, limit

            var (actualSql, _) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - Paginação")]
        public void GeneratesCorrectSqlWithOnlyTake()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Columns<Product>(p => p.ProductID))
                .From<Product>()
                .OrderBy("p.ProductID ASC")
                .Take(10);

            string expectedSql = @"SELECT
                p.ProductID
            FROM
                products AS p
            ORDER BY
                p.ProductID ASC
            LIMIT 10;";

            var (actualSql, _) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - Paginação")]
        public void GeneratesCorrectSqlWithOnlySkip()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Columns<Product>(p => p.ProductID))
                .From<Product>()
                .OrderBy("p.ProductID ASC")
                .Skip(5);

            // Para MySQL/MariaDB, OFFSET sem LIMIT explícito usa um LIMIT muito grande.
            string expectedSql = $@"SELECT
                p.ProductID
            FROM
                products AS p
            ORDER BY
                p.ProductID ASC
            LIMIT {uint.MaxValue} OFFSET 5;";

            var (actualSql, _) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - OrderBy Padrao")]
        public void GeneratesCorrectSqlWithDefaultOrderByPrimaryKey()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Columns<Product>(p => p.ProductID))
                .From<Product>();
            // Sem OrderBy explícito ou dinâmico

            string expectedSql = @"SELECT
                    p.ProductID
                FROM
                    products AS p
                ORDER BY
                    p.ProductID ASC;"; // Deve usar a PK como padrão

            var (actualSql, _) = builder.Build();

            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - OrderBy Padrao")]
        public void ThrowsExceptionIfNoPrimaryKeyForDefaultOrderBy()
        {
            // Entidade sem [Key]
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Columns<NoKeyEntity>(nk => nk.Id))
                .From<NoKeyEntity>();

            var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.Contains("Não foi possível determinar a chave primária para o tipo 'NoKeyEntity'", ex.Message);
        }

        [Table("NoKeyEntities")] public class NoKeyEntity { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - SELECT *")]
        public void GeneratesCorrectSqlForEmptySelect()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.AllColumns<Product>())
                .From<Product>(); // Chama o Select() vazio

            string expectedSql = @"SELECT
                    p.*
                FROM
                    products AS p
                ORDER BY
                    p.ProductID ASC;";

            var (actualSql, _) = builder.Build();
            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - Combinacao AllColumns e Specific Columns")]
        public void GeneratesCorrectSqlForCombinedAllColumnsAndSpecificColumns()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s =>
                {
                    s.AllColumns<Product>(true);
                    s.Columns<Brand>(b => b.BrandID, b => b.BrandName);
                    s.Columns<Subgroup>(sg => sg.SubgroupID);
                })
                .From<Product>();

            // A ordem das colunas pode variar devido à reflexão, então verificamos a presença individualmente
            var (actualSql, _) = builder.Build();
            var normalizedSql = NormalizeSqlString(actualSql);

            Assert.Contains(NormalizeSqlString("SELECT"), normalizedSql);
            Assert.Contains(NormalizeSqlString("FROM products AS p"), normalizedSql);

            // Colunas de Product (todas)
            Assert.Contains(NormalizeSqlString("p.ProductID"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.Barcode"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.IsActive"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.Description"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.ManufacturerRef"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.AuxiliaryRef"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.Dimensions"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.Observations"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.StockQuantity"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.MinimumStock"), normalizedSql);
            Assert.Contains(NormalizeSqlString("p.BrandID"), normalizedSql); // Note: BrandID é uma FK em Product
            Assert.Contains(NormalizeSqlString("p.SubgroupID"), normalizedSql); // Note: SubgroupID é uma FK em Product

            // Colunas de Brand
            Assert.Contains(NormalizeSqlString("b.BrandID"), normalizedSql);
            Assert.Contains(NormalizeSqlString("b.BrandName"), normalizedSql);

            // Colunas de Subgroup
            Assert.Contains(NormalizeSqlString("s.SubgroupID"), normalizedSql);
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - Except")]
        public void GeneratesCorrectSqlForExcept()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.Except<Product>(p => p.BrandID, p => p.Description, p => p.Barcode))
                .From<Product>();

            // Espera-se que BrandID, Description e Barcode sejam excluídos.
            // A ordem das colunas restantes pode variar devido à reflexão.
            string expectedSql = @"SELECT
                    p.ProductID,
                    p.IsActive,
                    p.ManufacturerRef,
                    p.AuxiliaryRef,
                    p.Dimensions,
                    p.Observations,
                    p.StockQuantity,
                    p.MinimumStock,
                    p.SubgroupID,
                    p.Origin,
                    p.AcquisitionMode,
                    p.ProductType
                FROM
                    products AS p
                ORDER BY
                    p.ProductID ASC;"; // Ordenação padrão pela PK

            var (actualSql, _) = builder.Build();
            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }

        [Fact]
        [Trait("Categoria", "Geracao SQL Final - SELECT AllColumns")]
        public void GeneratesCorrectSqlForSelectWithAllColumns()
        {
            var builder = FluentCommandBuilder.Create()
                .Select(s => s.AllColumns<Product>(true))
                .From<Product>();

            // ATUALIZADO: A string esperada agora inclui todas as propriedades mapeáveis da classe Product
            string expectedSql = @"SELECT
                p.ProductID,
                p.Barcode,
                p.IsActive,
                p.Description,
                p.ManufacturerRef,
                p.AuxiliaryRef,
                p.Dimensions,
                p.Observations,
                p.StockQuantity,
                p.MinimumStock,
                p.BrandID,
                p.SubgroupID,
                p.Origin,
                p.AcquisitionMode,
                p.ProductType
            FROM
                products AS p
            ORDER BY
                p.ProductID ASC;"; // Ordenação padrão pela PK

            var (actualSql, _) = builder.Build();
            Assert.Equal(NormalizeSqlString(expectedSql), NormalizeSqlString(actualSql));
        }
    }
}