using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemplateDormApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "DORM_OPER");

            migrationBuilder.CreateTable(
                name: "D_ADMIN",
                columns: table => new
                {
                    ADMIN_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ADMIN_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PHONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    ROLE_LEVEL = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    BUILDING_ID = table.Column<long>(type: "NUMBER(19)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_ADMIN", x => x.ADMIN_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_BED_ALLOCATION",
                columns: table => new
                {
                    ALLOCATION_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    ROOM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    BED_NO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CHECKIN_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CHECKOUT_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_BED_ALLOCATION", x => x.ALLOCATION_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_BUILDING",
                columns: table => new
                {
                    BUILDING_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BUILDING_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    BUILDING_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    TOTAL_FLOORS = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_BUILDING", x => x.BUILDING_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_CREDIT_ACCOUNT",
                columns: table => new
                {
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    CURRENT_SCORE = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 100),
                    UPDATED_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_CREDIT_ACCOUNT", x => x.STUDENT_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_FACILITY_BOOKING",
                columns: table => new
                {
                    BOOKING_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    FACILITY_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    CREATE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    START_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    END_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_FACILITY_BOOKING", x => x.BOOKING_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_Fee_Detail",
                schema: "DORM_OPER",
                columns: table => new
                {
                    Detail_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Fee_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Student_ID = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Room_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Water_Share = table.Column<decimal>(type: "DECIMAL(18, 2)", nullable: false),
                    Power_Share = table.Column<decimal>(type: "DECIMAL(18, 2)", nullable: false),
                    Stay_Days = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Total_Days = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Bill_Type = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Is_Paid = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Create_Time = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_Fee_Detail", x => x.Detail_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_HYGIENE_RECORD",
                columns: table => new
                {
                    RECORD_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROOM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CHECK_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    SCORE = table.Column<decimal>(type: "DECIMAL(4,1)", precision: 4, scale: 1, nullable: false),
                    INSPECTOR_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_HYGIENE_RECORD", x => x.RECORD_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_ITEM_LOAN",
                columns: table => new
                {
                    LOAN_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    BORROW_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DUE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    RETURN_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IDEMPOTENCY_KEY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_ITEM_LOAN", x => x.LOAN_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_LATE_ENTRY",
                columns: table => new
                {
                    RECORD_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    RETURN_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    REASON = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_LATE_ENTRY", x => x.RECORD_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_NOTICE",
                columns: table => new
                {
                    NOTICE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ADMIN_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    TITLE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CONTENT = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    PUBLISH_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_NOTICE", x => x.NOTICE_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_REPAIR_MATERIAL",
                columns: table => new
                {
                    MATERIAL_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    MATERIAL_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    UNIT = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    STOCK_QTY = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_REPAIR_MATERIAL", x => x.MATERIAL_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_REPAIR_MATERIAL_USAGE",
                columns: table => new
                {
                    USAGE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TICKET_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MATERIAL_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    USE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    IDEMPOTENCY_KEY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_REPAIR_MATERIAL_USAGE", x => x.USAGE_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_REPAIR_TICKET",
                columns: table => new
                {
                    TICKET_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    ROOM_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ISSUE_DESC = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    SUBMIT_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    SLA_LEVEL = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    DEADLINE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ASSIGNED_TO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_REPAIR_TICKET", x => x.TICKET_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_SHARED_ITEM",
                columns: table => new
                {
                    ITEM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ITEM_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    BUILDING_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TOTAL_QTY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    AVAILABLE_QTY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_SHARED_ITEM", x => x.ITEM_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_STUDENT",
                columns: table => new
                {
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    GENDER = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    MAJOR_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    PHONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_STUDENT", x => x.STUDENT_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_USER_ACCOUNT",
                columns: table => new
                {
                    ACCOUNT_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    LOGIN_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    PASSWORD_HASH = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    ACCOUNT_STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    ADMIN_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_USER_ACCOUNT", x => x.ACCOUNT_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_UTILITY_FEE",
                columns: table => new
                {
                    FEE_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROOM_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    YEAR_MONTH = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    WATER_FEE = table.Column<decimal>(type: "DECIMAL(8,2)", precision: 8, scale: 2, nullable: true),
                    POWER_FEE = table.Column<decimal>(type: "DECIMAL(8,2)", precision: 8, scale: 2, nullable: true),
                    IS_PAID = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    PUBLISH_STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_UTILITY_FEE", x => x.FEE_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_Wallet_Account",
                schema: "DORM_OPER",
                columns: table => new
                {
                    Student_ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Balance = table.Column<decimal>(type: "DECIMAL(18, 2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_Wallet_Account", x => x.Student_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_Wallet_Log",
                schema: "DORM_OPER",
                columns: table => new
                {
                    Log_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Student_ID = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Amount = table.Column<decimal>(type: "DECIMAL(18, 2)", nullable: false),
                    Transaction_Type = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Before_Balance = table.Column<decimal>(type: "DECIMAL(18, 2)", nullable: false),
                    After_Balance = table.Column<decimal>(type: "DECIMAL(18, 2)", nullable: false),
                    Detail_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Idempotency_Key = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Create_Time = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_Wallet_Log", x => x.Log_ID);
                });

            migrationBuilder.CreateTable(
                name: "D_FACILITY",
                columns: table => new
                {
                    FACILITY_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BUILDING_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FACILITY_CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    FACILITY_TYPE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_FACILITY", x => x.FACILITY_ID);
                    table.ForeignKey(
                        name: "FK_D_FACILITY_BUILDING",
                        column: x => x.BUILDING_ID,
                        principalTable: "D_BUILDING",
                        principalColumn: "BUILDING_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "D_ROOM",
                columns: table => new
                {
                    ROOM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    BUILDING_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ROOM_NUMBER = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    CAPACITY = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    OCCUPANCY = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    FLOOR = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    POWER_STATUS = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_ROOM", x => x.ROOM_ID);
                    table.ForeignKey(
                        name: "FK_D_ROOM_D_BUILDING_BUILDING_ID",
                        column: x => x.BUILDING_ID,
                        principalTable: "D_BUILDING",
                        principalColumn: "BUILDING_ID");
                });

            migrationBuilder.CreateTable(
                name: "D_CREDIT_LOG",
                columns: table => new
                {
                    LOG_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    STUDENT_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    SCORE_CHANGE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    REASON = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    EVENT_KEY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_CREDIT_LOG", x => x.LOG_ID);
                    table.ForeignKey(
                        name: "FK_D_CREDIT_LOG_ACCOUNT",
                        column: x => x.STUDENT_ID,
                        principalTable: "D_CREDIT_ACCOUNT",
                        principalColumn: "STUDENT_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "D_HYGIENE_COMMENT",
                columns: table => new
                {
                    RECORD_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    COMMENT = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_HYGIENE_COMMENT", x => x.RECORD_ID);
                    table.ForeignKey(
                        name: "FK_D_HYGIENE_COMMENT_D_HYGIENE_RECORD_RECORD_ID",
                        column: x => x.RECORD_ID,
                        principalTable: "D_HYGIENE_RECORD",
                        principalColumn: "RECORD_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "D_NOTICE_DISPLAY",
                columns: table => new
                {
                    NOTICE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_PINNED = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    PIN_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_NOTICE_DISPLAY", x => x.NOTICE_ID);
                    table.ForeignKey(
                        name: "FK_D_NOTICE_DISPLAY_D_NOTICE_NOTICE_ID",
                        column: x => x.NOTICE_ID,
                        principalTable: "D_NOTICE",
                        principalColumn: "NOTICE_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "D_REPAIR_ATTACHMENT",
                columns: table => new
                {
                    ATTACHMENT_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TICKET_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    STORAGE_REF = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ORIGINAL_NAME = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: true),
                    CONTENT_TYPE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    FILE_SIZE = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CREATE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_REPAIR_ATTACHMENT", x => x.ATTACHMENT_ID);
                    table.ForeignKey(
                        name: "FK_D_REPAIR_ATTACHMENT_D_REPAIR_TICKET_TICKET_ID",
                        column: x => x.TICKET_ID,
                        principalTable: "D_REPAIR_TICKET",
                        principalColumn: "TICKET_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "D_REPAIR_LOG",
                columns: table => new
                {
                    LOG_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TICKET_ID = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ADMIN_ID = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    PROCESS_DESC = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    RESOLVE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_REPAIR_LOG", x => x.LOG_ID);
                    table.ForeignKey(
                        name: "FK_D_REPAIR_LOG_D_REPAIR_TICKET_TICKET_ID",
                        column: x => x.TICKET_ID,
                        principalTable: "D_REPAIR_TICKET",
                        principalColumn: "TICKET_ID");
                });

            migrationBuilder.CreateTable(
                name: "D_NOTIFICATION",
                columns: table => new
                {
                    NOTIFICATION_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    RECIPIENT_ACCOUNT_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TITLE = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CONTENT = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    NOTIFICATION_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    READ_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATE_TIME = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_NOTIFICATION", x => x.NOTIFICATION_ID);
                    table.ForeignKey(
                        name: "FK_D_NOTIFICATION_ACCOUNT",
                        column: x => x.RECIPIENT_ACCOUNT_ID,
                        principalTable: "D_USER_ACCOUNT",
                        principalColumn: "ACCOUNT_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "D_ASSET",
                columns: table => new
                {
                    ASSET_ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROOM_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ASSET_NAME = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D_ASSET", x => x.ASSET_ID);
                    table.ForeignKey(
                        name: "FK_D_ASSET_D_ROOM_ROOM_ID",
                        column: x => x.ROOM_ID,
                        principalTable: "D_ROOM",
                        principalColumn: "ROOM_ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_D_ASSET_ROOM_ID",
                table: "D_ASSET",
                column: "ROOM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_D_CREDIT_LOG_STUDENT_ID",
                table: "D_CREDIT_LOG",
                column: "STUDENT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_D_FACILITY_BUILDING_ID",
                table: "D_FACILITY",
                column: "BUILDING_ID");

            migrationBuilder.CreateIndex(
                name: "IX_D_NOTIFICATION_RECIPIENT_ACCOUNT_ID",
                table: "D_NOTIFICATION",
                column: "RECIPIENT_ACCOUNT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_D_REPAIR_ATTACHMENT_TICKET_ID",
                table: "D_REPAIR_ATTACHMENT",
                column: "TICKET_ID");

            migrationBuilder.CreateIndex(
                name: "IX_D_REPAIR_LOG_TICKET_ID",
                table: "D_REPAIR_LOG",
                column: "TICKET_ID",
                unique: true,
                filter: "\"TICKET_ID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_D_ROOM_BUILDING_ID",
                table: "D_ROOM",
                column: "BUILDING_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "D_ADMIN");

            migrationBuilder.DropTable(
                name: "D_ASSET");

            migrationBuilder.DropTable(
                name: "D_BED_ALLOCATION");

            migrationBuilder.DropTable(
                name: "D_CREDIT_LOG");

            migrationBuilder.DropTable(
                name: "D_FACILITY");

            migrationBuilder.DropTable(
                name: "D_FACILITY_BOOKING");

            migrationBuilder.DropTable(
                name: "D_Fee_Detail",
                schema: "DORM_OPER");

            migrationBuilder.DropTable(
                name: "D_HYGIENE_COMMENT");

            migrationBuilder.DropTable(
                name: "D_ITEM_LOAN");

            migrationBuilder.DropTable(
                name: "D_LATE_ENTRY");

            migrationBuilder.DropTable(
                name: "D_NOTICE_DISPLAY");

            migrationBuilder.DropTable(
                name: "D_NOTIFICATION");

            migrationBuilder.DropTable(
                name: "D_REPAIR_ATTACHMENT");

            migrationBuilder.DropTable(
                name: "D_REPAIR_LOG");

            migrationBuilder.DropTable(
                name: "D_REPAIR_MATERIAL");

            migrationBuilder.DropTable(
                name: "D_REPAIR_MATERIAL_USAGE");

            migrationBuilder.DropTable(
                name: "D_SHARED_ITEM");

            migrationBuilder.DropTable(
                name: "D_STUDENT");

            migrationBuilder.DropTable(
                name: "D_UTILITY_FEE");

            migrationBuilder.DropTable(
                name: "D_Wallet_Account",
                schema: "DORM_OPER");

            migrationBuilder.DropTable(
                name: "D_Wallet_Log",
                schema: "DORM_OPER");

            migrationBuilder.DropTable(
                name: "D_ROOM");

            migrationBuilder.DropTable(
                name: "D_CREDIT_ACCOUNT");

            migrationBuilder.DropTable(
                name: "D_HYGIENE_RECORD");

            migrationBuilder.DropTable(
                name: "D_NOTICE");

            migrationBuilder.DropTable(
                name: "D_USER_ACCOUNT");

            migrationBuilder.DropTable(
                name: "D_REPAIR_TICKET");

            migrationBuilder.DropTable(
                name: "D_BUILDING");
        }
    }
}
