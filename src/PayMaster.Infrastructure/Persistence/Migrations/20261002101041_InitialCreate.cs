using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace PayMaster.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    OldValues = table.Column<string>(type: "longtext", nullable: true),
                    NewValues = table.Column<string>(type: "longtext", nullable: true),
                    Details = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CompanyName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    KraPin = table.Column<string>(type: "varchar(11)", maxLength: 11, nullable: true),
                    NssfEmployerNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    ShaEmployerNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    PostalAddress = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Website = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    LogoPath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    PaymentDay = table.Column<int>(type: "int", nullable: false),
                    RequireSeparatePayrollApprover = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MinimumNetPayRatio = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    EmployeeNumberPrefix = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    PayslipFooter = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Link = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RatingScaleLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Value = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    MinScore = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Color = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingScaleLevels", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReviewCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ReviewType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCycles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalaryComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    TaxTreatment = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IsPensionable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsRecurring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryComponents", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalaryGrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    MinimumSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryGrades", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StatutoryConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    LegalReference = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    IsLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ActivatedById = table.Column<int>(type: "int", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryConfigurations", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FirstName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StatutoryRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StatutoryConfigurationId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryRates_StatutoryConfigurations_StatutoryConfiguratio~",
                        column: x => x.StatutoryConfigurationId,
                        principalTable: "StatutoryConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TaxBrackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StatutoryConfigurationId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxBrackets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxBrackets_StatutoryConfigurations_StatutoryConfigurationId",
                        column: x => x.StatutoryConfigurationId,
                        principalTable: "StatutoryConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false),
                    Value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HeadEmployeeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Reference = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    RunType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    OriginalRunId = table.Column<int>(type: "int", nullable: true),
                    Scope = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    StatutoryConfigurationId = table.Column<int>(type: "int", nullable: true),
                    StatutorySnapshotJson = table.Column<string>(type: "longtext", nullable: true),
                    EmployeeCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    TotalGross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTaxableIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPaye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSha = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalHousingLevy = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNssf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalOtherDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNetPay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEmployerContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CalculatedById = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SubmittedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalizedById = table.Column<int>(type: "int", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PaidById = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancelledById = table.Column<int>(type: "int", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReversedById = table.Column<int>(type: "int", nullable: true),
                    StatusReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_PayrollRuns_OriginalRunId",
                        column: x => x.OriginalRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_StatutoryConfigurations_StatutoryConfigurationId",
                        column: x => x.StatutoryConfigurationId,
                        principalTable: "StatutoryConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    SalaryGradeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_SalaryGrades_SalaryGradeId",
                        column: x => x.SalaryGradeId,
                        principalTable: "SalaryGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    NationalId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    KraPin = table.Column<string>(type: "varchar(11)", maxLength: 11, nullable: true),
                    NssfNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ShaNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    PersonalEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    WorkEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    EmergencyContactRelationship = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: true),
                    EmploymentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    EmploymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ManagerId = table.Column<int>(type: "int", nullable: true),
                    WorkLocation = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PayFrequency = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    PaymentMethod = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    BankName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BankBranch = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: true),
                    MobileMoneyNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    HasDisabilityExemption = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisabilityExemptionCertificateNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Kpis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    MeasurementUnit = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ScoringMethod = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    PositionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kpis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kpis_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kpis_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalaryStructures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SalaryGradeId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    PositionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryStructures_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryStructures_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryStructures_SalaryGrades_SalaryGradeId",
                        column: x => x.SalaryGradeId,
                        principalTable: "SalaryGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeDeductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountRecovered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmploymentHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromDepartment = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ToDepartment = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    FromPosition = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ToPosition = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    FromStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    ToStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmploymentHistories_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollEmployees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PeriodLock = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    EmployeeNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    EmployeeName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    DepartmentName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    JobTitle = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    KraPin = table.Column<string>(type: "varchar(11)", maxLength: 11, nullable: true),
                    NssfNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ShaNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    NationalId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    PaymentMethod = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    BankName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BankBranch = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: true),
                    MobileMoneyNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    DaysInPeriod = table.Column<int>(type: "int", nullable: false),
                    DaysPayable = table.Column<int>(type: "int", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableEarnings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NonTaxableEarnings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PensionablePay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NssfTier1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NssfTier2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Nssf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sha = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HousingLevy = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AllowableDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxBeforeReliefs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalRelief = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceRelief = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HousingLevyRelief = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Paye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerNssf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerHousingLevy = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEmployerContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Errors = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Warnings = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    CalculationBreakdownJson = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployees_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployees_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollInputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollInputs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollInputs_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollInputs_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PerformanceGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    ManagerId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceGoals_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceGoals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceGoals_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PerformanceReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ReviewCycleId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ReviewerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Strengths = table.Column<string>(type: "text", nullable: true),
                    AreasForImprovement = table.Column<string>(type: "text", nullable: true),
                    ManagerComments = table.Column<string>(type: "text", nullable: true),
                    SelfAssessment = table.Column<string>(type: "text", nullable: true),
                    EmployeeComments = table.Column<string>(type: "text", nullable: true),
                    DevelopmentRecommendations = table.Column<string>(type: "text", nullable: true),
                    HrComments = table.Column<string>(type: "text", nullable: true),
                    OverallScore = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    OverallRating = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    ManagerEvaluatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SelfAssessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FeedbackGivenAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalizedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalTable: "ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeSalaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SalaryStructureId = table.Column<int>(type: "int", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    ChangeReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectionReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSalaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaries_SalaryStructures_SalaryStructureId",
                        column: x => x.SalaryStructureId,
                        principalTable: "SalaryStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalaryStructureComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SalaryStructureId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: false),
                    AmountType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryStructureComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryStructureComponents_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryStructureComponents_SalaryStructures_SalaryStructureId",
                        column: x => x.SalaryStructureId,
                        principalTable: "SalaryStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollDeductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: true),
                    EmployeeDeductionId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    TaxTreatment = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollEarnings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    TaxTreatment = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IsPensionable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEarnings_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollEmployerContributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEmployerContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployerContributions_PayrollEmployees_PayrollEmploye~",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Payslips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PayslipNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EmailStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    EmailedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EmailError = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GoalProgressUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PerformanceGoalId = table.Column<int>(type: "int", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    EvidenceStoragePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalProgressUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalProgressUpdates_PerformanceGoals_PerformanceGoalId",
                        column: x => x.PerformanceGoalId,
                        principalTable: "PerformanceGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DevelopmentPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PerformanceReviewId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Skills = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevelopmentPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevelopmentPlans_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PerformanceFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PerformanceReviewId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceFeedback_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceFeedback_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PerformanceReviewScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PerformanceReviewId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    KpiId = table.Column<int>(type: "int", nullable: true),
                    PerformanceGoalId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    WeightedScore = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Comments = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceReviewScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceReviewScores_Kpis_KpiId",
                        column: x => x.KpiId,
                        principalTable: "Kpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviewScores_PerformanceGoals_PerformanceGoalId",
                        column: x => x.PerformanceGoalId,
                        principalTable: "PerformanceGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviewScores_PerformanceReviews_PerformanceReview~",
                        column: x => x.PerformanceReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeSalaryComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeSalaryId = table.Column<int>(type: "int", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSalaryComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryComponents_EmployeeSalaries_EmployeeSalaryId",
                        column: x => x.EmployeeSalaryId,
                        principalTable: "EmployeeSalaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryComponents_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "SalaryComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_HeadEmployeeId",
                table: "Departments",
                column: "HeadEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentPlans_EmployeeId_Status",
                table: "DevelopmentPlans",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentPlans_PerformanceReviewId",
                table: "DevelopmentPlans",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_EmployeeId_IsActive",
                table: "EmployeeDeductions",
                columns: new[] { "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_SalaryComponentId",
                table: "EmployeeDeductions",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                table: "EmployeeDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_KraPin",
                table: "Employees",
                column: "KraPin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LastName_FirstName",
                table: "Employees",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_NationalId",
                table: "Employees",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PositionId",
                table: "Employees",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Status",
                table: "Employees",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaries_EmployeeId_Status_EffectiveFrom",
                table: "EmployeeSalaries",
                columns: new[] { "EmployeeId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaries_SalaryStructureId",
                table: "EmployeeSalaries",
                column: "SalaryStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryComponents_EmployeeSalaryId_SalaryComponentId",
                table: "EmployeeSalaryComponents",
                columns: new[] { "EmployeeSalaryId", "SalaryComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryComponents_SalaryComponentId",
                table: "EmployeeSalaryComponents",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentHistories_EmployeeId_EffectiveDate",
                table: "EmploymentHistories",
                columns: new[] { "EmployeeId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressUpdates_PerformanceGoalId",
                table: "GoalProgressUpdates",
                column: "PerformanceGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_DepartmentId",
                table: "Kpis",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_PositionId",
                table: "Kpis",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_EmployeeDeductionId",
                table: "PayrollDeductions",
                column: "EmployeeDeductionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_PayrollEmployeeId",
                table: "PayrollDeductions",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEarnings_PayrollEmployeeId",
                table: "PayrollEarnings",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployees_EmployeeId",
                table: "PayrollEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployees_PayrollPeriodId_EmployeeId_PeriodLock",
                table: "PayrollEmployees",
                columns: new[] { "PayrollPeriodId", "EmployeeId", "PeriodLock" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployees_PayrollRunId_EmployeeId",
                table: "PayrollEmployees",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployerContributions_PayrollEmployeeId",
                table: "PayrollEmployerContributions",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollInputs_EmployeeId",
                table: "PayrollInputs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollInputs_PayrollPeriodId_EmployeeId",
                table: "PayrollInputs",
                columns: new[] { "PayrollPeriodId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollInputs_SalaryComponentId",
                table: "PayrollInputs",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_Year_Month",
                table: "PayrollPeriods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_DepartmentId",
                table: "PayrollRuns",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_OriginalRunId",
                table: "PayrollRuns",
                column: "OriginalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PayrollPeriodId_Status",
                table: "PayrollRuns",
                columns: new[] { "PayrollPeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_Reference",
                table: "PayrollRuns",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_StatutoryConfigurationId",
                table: "PayrollRuns",
                column: "StatutoryConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_EmployeeId",
                table: "Payslips",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollEmployeeId",
                table: "Payslips",
                column: "PayrollEmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayslipNumber",
                table: "Payslips",
                column: "PayslipNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceFeedback_EmployeeId",
                table: "PerformanceFeedback",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceFeedback_PerformanceReviewId",
                table: "PerformanceFeedback",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceGoals_DepartmentId",
                table: "PerformanceGoals",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceGoals_EmployeeId_Status",
                table: "PerformanceGoals",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceGoals_ManagerId",
                table: "PerformanceGoals",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceGoals_TargetDate",
                table: "PerformanceGoals",
                column: "TargetDate");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_EmployeeId",
                table: "PerformanceReviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewCycleId_EmployeeId",
                table: "PerformanceReviews",
                columns: new[] { "ReviewCycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewerId",
                table: "PerformanceReviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_Status",
                table: "PerformanceReviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviewScores_KpiId",
                table: "PerformanceReviewScores",
                column: "KpiId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviewScores_PerformanceGoalId",
                table: "PerformanceReviewScores",
                column: "PerformanceGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviewScores_PerformanceReviewId",
                table: "PerformanceReviewScores",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Code",
                table: "Positions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_DepartmentId",
                table: "Positions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_SalaryGradeId",
                table: "Positions",
                column: "SalaryGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingScaleLevels_Value",
                table: "RatingScaleLevels",
                column: "Value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_Status",
                table: "ReviewCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryComponents_Code",
                table: "SalaryComponents",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_Code",
                table: "SalaryGrades",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructureComponents_SalaryComponentId",
                table: "SalaryStructureComponents",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructureComponents_SalaryStructureId_SalaryComponentId",
                table: "SalaryStructureComponents",
                columns: new[] { "SalaryStructureId", "SalaryComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_Code",
                table: "SalaryStructures",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_DepartmentId",
                table: "SalaryStructures",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_PositionId",
                table: "SalaryStructures",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_SalaryGradeId",
                table: "SalaryStructures",
                column: "SalaryGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryConfigurations_Status_EffectiveFrom",
                table: "StatutoryConfigurations",
                columns: new[] { "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryConfigurations_Version",
                table: "StatutoryConfigurations",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryRates_StatutoryConfigurationId_Key",
                table: "StatutoryRates",
                columns: new[] { "StatutoryConfigurationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxBrackets_StatutoryConfigurationId_Order",
                table: "TaxBrackets",
                columns: new[] { "StatutoryConfigurationId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedAt",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_HeadEmployeeId",
                table: "Departments",
                column: "HeadEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_HeadEmployeeId",
                table: "Departments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropTable(
                name: "DevelopmentPlans");

            migrationBuilder.DropTable(
                name: "EmployeeDeductions");

            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "EmployeeSalaryComponents");

            migrationBuilder.DropTable(
                name: "EmploymentHistories");

            migrationBuilder.DropTable(
                name: "GoalProgressUpdates");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PayrollDeductions");

            migrationBuilder.DropTable(
                name: "PayrollEarnings");

            migrationBuilder.DropTable(
                name: "PayrollEmployerContributions");

            migrationBuilder.DropTable(
                name: "PayrollInputs");

            migrationBuilder.DropTable(
                name: "Payslips");

            migrationBuilder.DropTable(
                name: "PerformanceFeedback");

            migrationBuilder.DropTable(
                name: "PerformanceReviewScores");

            migrationBuilder.DropTable(
                name: "RatingScaleLevels");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "SalaryStructureComponents");

            migrationBuilder.DropTable(
                name: "StatutoryRates");

            migrationBuilder.DropTable(
                name: "TaxBrackets");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "EmployeeSalaries");

            migrationBuilder.DropTable(
                name: "PayrollEmployees");

            migrationBuilder.DropTable(
                name: "Kpis");

            migrationBuilder.DropTable(
                name: "PerformanceGoals");

            migrationBuilder.DropTable(
                name: "PerformanceReviews");

            migrationBuilder.DropTable(
                name: "SalaryComponents");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "SalaryStructures");

            migrationBuilder.DropTable(
                name: "PayrollRuns");

            migrationBuilder.DropTable(
                name: "ReviewCycles");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.DropTable(
                name: "StatutoryConfigurations");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "SalaryGrades");
        }
    }
}
