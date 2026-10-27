using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Persistence.Migrations.SchoolSchema
{
    /// <summary>
    /// Adds the cross-schema foreign key from <c>school_main.class.branch_id</c> to
    /// <c>master.branch.id</c>, required by architecture sections 4.3 and 4.7.
    /// <para>
    /// Written by hand because EF cannot generate it: the two tables belong to different
    /// <c>DbContext</c> types, so no model knows the relationship exists. Same approach as
    /// the partial unique indexes and check constraints.
    /// </para>
    /// <para>
    /// This constraint is the payoff for choosing schema-per-school. Under
    /// database-per-school it would have been impossible and the integrity would have
    /// rested on application code forever. <b>Every branch-scoped table added from now on
    /// needs the same constraint.</b>
    /// </para>
    /// </summary>
    public partial class AddClassBranchForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RESTRICT, not CASCADE: a branch is closed by status, never deleted out from
            // under the classes that reference it. Matches fk_branch_school in master.
            migrationBuilder.Sql("""
                ALTER TABLE school_main.class
                    ADD CONSTRAINT fk_class_branch
                    FOREIGN KEY (branch_id) REFERENCES master.branch (id) ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE school_main.class
                    DROP CONSTRAINT IF EXISTS fk_class_branch;
                """);
        }
    }
}
