using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lod.LlmGateway.Gateway.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "llm_gateway");

            migrationBuilder.CreateTable(
                name: "OpenAIChatCompletionDailyRollup",
                schema: "llm_gateway",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    Client = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    NonStreamRequestCount = table.Column<int>(type: "int", nullable: false),
                    StreamRequestCount = table.Column<int>(type: "int", nullable: false),
                    PrimaryProviderRequestCount = table.Column<int>(type: "int", nullable: false),
                    FailoverRequestCount = table.Column<int>(type: "int", nullable: false),
                    FailedRequestCount = table.Column<int>(type: "int", nullable: false),
                    NonStreamPromptTokens = table.Column<int>(type: "int", nullable: false),
                    NonStreamCompletionTokens = table.Column<int>(type: "int", nullable: false),
                    NonStreamTotalTokens = table.Column<int>(type: "int", nullable: false),
                    StreamPromptTokens = table.Column<int>(type: "int", nullable: false),
                    StreamCompletionTokens = table.Column<int>(type: "int", nullable: false),
                    StreamTotalTokens = table.Column<int>(type: "int", nullable: false),
                    NonStreamPromptCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    NonStreamCompletionCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    NonStreamTotalCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    NonStreamAverageTokensPerSecond = table.Column<double>(type: "float", nullable: true),
                    StreamAverageTokensPerSecond = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAIChatCompletionDailyRollup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenAIChatCompletionRequest",
                schema: "llm_gateway",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GatewayRequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Client = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Api = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Streamed = table.Column<bool>(type: "bit", nullable: false),
                    RequestReceivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestSentUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResponseSentUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResponseModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderIndex = table.Column<int>(type: "int", nullable: true),
                    ProviderAttemptsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailoverUsed = table.Column<bool>(type: "bit", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAIChatCompletionRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenAIChatCompletionNonStream",
                schema: "llm_gateway",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    UpstreamResponseId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    TokensPerSecond = table.Column<double>(type: "float", nullable: true),
                    PromptCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    CompletionCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    RawUsageJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAIChatCompletionNonStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenAIChatCompletionNonStream_OpenAIChatCompletionRequest_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "llm_gateway",
                        principalTable: "OpenAIChatCompletionRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenAIChatCompletionStream",
                schema: "llm_gateway",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    FirstChunkSentUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalChunkSentUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    TimeToFirstChunkSeconds = table.Column<double>(type: "float", nullable: true),
                    TokensPerSecond = table.Column<double>(type: "float", nullable: true),
                    RawUsageJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAIChatCompletionStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenAIChatCompletionStream_OpenAIChatCompletionRequest_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "llm_gateway",
                        principalTable: "OpenAIChatCompletionRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenAIChatCompletionNonStream_RequestId",
                schema: "llm_gateway",
                table: "OpenAIChatCompletionNonStream",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenAIChatCompletionStream_RequestId",
                schema: "llm_gateway",
                table: "OpenAIChatCompletionStream",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenAIChatCompletionDailyRollup",
                schema: "llm_gateway");

            migrationBuilder.DropTable(
                name: "OpenAIChatCompletionNonStream",
                schema: "llm_gateway");

            migrationBuilder.DropTable(
                name: "OpenAIChatCompletionStream",
                schema: "llm_gateway");

            migrationBuilder.DropTable(
                name: "OpenAIChatCompletionRequest",
                schema: "llm_gateway");
        }
    }
}
