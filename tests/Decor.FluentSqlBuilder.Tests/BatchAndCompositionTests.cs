using Decor.Core.Entities;
using FluentAssertions;
using Decor.FluentSqlBuilder;
using Decor.FluentSqlBuilder.Dialects.MariaDB;
using Xunit;

namespace Decor.FluentSqlBuilder.Tests
{
    public class BatchAndCompositionTests
    {
        private static string NormalizeSqlString(string sql)
        {
            return sql.Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }

        private static FluentCommandBuilder CreateBuilder() => FluentCommandBuilder.Create(new MariaDBDialect());

        [Fact]
        public void Batch_WithTwoSelects_ShouldConcatenateStatementsAndMergeParameters()
        {
            var (sql, parameters) = CreateBuilder()
                .Batch()
                .Select(s => s
                    .Select(sc => sc.Columns<Role>(r => r.RoleID, r => r.RoleName))
                    .From<Role>()
                    .Where(w => w.Equals((Role r) => r.RoleID, 1))
                    .OrderBy("r.RoleName ASC"))
                .Select(s => s
                    .Select(sc => sc.Columns<Permission>(p => p.PermissionID, p => p.PermissionCode))
                    .From<Permission>()
                    .Where(w => w.Equals((Permission p) => p.PermissionID, 2))
                    .OrderBy("p.PermissionCode ASC"))
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("SELECT\n    r.RoleID,\n    r.RoleName\nFROM\n    roles AS r\nWHERE r.RoleID = @RoleID\nORDER BY\n    r.RoleName ASC;"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("SELECT\n    p.PermissionID,\n    p.PermissionCode\nFROM\n    permissions AS p\nWHERE p.PermissionID = @PermissionID\nORDER BY\n    p.PermissionCode ASC;"));
            parameters.Should().Contain("RoleID", 1);
            parameters.Should().Contain("PermissionID", 2);
        }

        [Fact]
        public void Batch_WithoutAnyStatement_ShouldThrow()
        {
            var act = () => CreateBuilder().Batch().Build();
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Union_WithTwoSelects_ShouldCombineWithUnionKeywordAndMergeParameters()
        {
            var first = CreateBuilder()
                .Select(s => s.Columns<Permission>(p => p.PermissionCode))
                .From<Permission>()
                .Where(w => w.Equals((Permission p) => p.PermissionID, 5));

            var second = CreateBuilder()
                .Select(s => s.Columns<Permission>(p => p.PermissionCode))
                .From<Permission>()
                .Where(w => w.Equals((Permission p) => p.PermissionID, 5));

            var (sql, parameters) = first.Union(second).BuildInline();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("UNION"));
            NormalizeSqlString(sql).Should().NotContain(";");
            parameters.Should().ContainSingle().Which.Value.Should().Be(5);
        }

        [Fact]
        public void FromSubquery_WithUnionAndDistinctColumn_ShouldWrapAsDerivedTable()
        {
            var roleBased = CreateBuilder()
                .Select(s => s.Columns<Permission>(p => p.PermissionCode))
                .From<Permission>()
                .Where(w => w.Equals((Permission p) => p.PermissionID, 7));

            var overrideBased = CreateBuilder()
                .Select(s => s.Columns<Permission>(p => p.PermissionCode))
                .From<Permission>()
                .Where(w => w.Equals((Permission p) => p.PermissionID, 7));

            var union = roleBased.Union(overrideBased);

            var (sql, parameters) = CreateBuilder()
                .Select(s =>
                {
                    s.Distinct();
                    s.Column("effective.PermissionCode");
                })
                .FromSubquery(union, "effective")
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("SELECT DISTINCT"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("effective.PermissionCode"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("FROM ("));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString(") AS effective"));
            parameters.Should().ContainSingle().Which.Value.Should().Be(7);
        }

        [Fact]
        public void Join_Left_WithCompoundAndCondition_ShouldGenerateLeftJoinWithParameterizedConstant()
        {
            var (sql, parameters) = CreateBuilder()
                .Select(s => s.AllColumns<Permission>())
                .From<Permission>()
                .Join(j => j.Left<Permission, RolePermission>((p, rp) => p.PermissionID == rp.PermissionID && rp.RoleID == 5))
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("LEFT OUTER JOIN"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("role_permissions AS rp ON p.PermissionID = rp.PermissionID AND rp.RoleID = @JoinParam"));
            parameters.Should().Contain("JoinParam", 5);
        }

        [Fact]
        public void Where_Group_ShouldWrapConditionsInParentheses()
        {
            var (sql, parameters) = CreateBuilder()
                .Select(s => s.Columns<Permission>(p => p.PermissionID))
                .From<Permission>()
                .Where(w =>
                {
                    w.Equals((Permission p) => p.PermissionID, 1);
                    w.Group(g =>
                    {
                        g.IsNull((Permission p) => p.Description);
                        g.Or();
                        g.Equals((Permission p) => p.Description, "x");
                    });
                })
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("(p.Description IS NULL OR p.Description = @Description)"));
            parameters.Should().Contain("Description", "x");
        }

        [Fact]
        public void Join_Left_WithThreeTableOnCondition_ShouldReferenceBothPreviouslyJoinedEntities()
        {
            // Reproduz o caso do item 11: um LEFT JOIN cujo ON referencia duas entidades
            // já presentes na consulta (Permission e UserRole), além da entidade sendo unida (UserPermissionOverride).
            var (sql, parameters) = CreateBuilder()
                .Select(s => s.AllColumns<Permission>())
                .From<Permission>()
                .Join(j =>
                {
                    j.Inner<Permission, RolePermission>((p, rp) => p.PermissionID == rp.PermissionID);
                    j.Inner<RolePermission, UserRole>((rp, ur) => ur.RoleID == rp.RoleID);
                    j.Left<Permission, UserRole, UserPermissionOverride>((p, ur, o) => o.PermissionID == p.PermissionID && o.UserID == ur.UserID);
                })
                .Build();

            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("LEFT OUTER JOIN"));
            NormalizeSqlString(sql).Should().Contain(NormalizeSqlString("user_permission_overrides AS upo ON upo.PermissionID = p.PermissionID AND upo.UserID = ur.UserID"));
            parameters.Should().BeEmpty();
        }
    }
}
