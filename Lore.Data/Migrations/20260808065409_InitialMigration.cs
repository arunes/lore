using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lore.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    keywords = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "file_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    exclude_pattern = table.Column<string>(type: "TEXT", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "primary_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    keywords = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_primary_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    directory = table.Column<string>(type: "TEXT", nullable: false),
                    extension = table.Column<string>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: true),
                    primary_category_id = table.Column<int>(type: "INTEGER", nullable: true),
                    document_type_id = table.Column<int>(type: "INTEGER", nullable: true),
                    file_created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    file_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    size = table.Column<long>(type: "INTEGER", nullable: false),
                    hash = table.Column<string>(type: "TEXT", nullable: false),
                    process_status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_files_document_types_document_type_id",
                        column: x => x.document_type_id,
                        principalTable: "document_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_files_primary_categories_primary_category_id",
                        column: x => x.primary_category_id,
                        principalTable: "primary_categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "file_chunks",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    file_entry_id = table.Column<int>(type: "INTEGER", nullable: false),
                    chunk_index = table.Column<int>(type: "INTEGER", nullable: false),
                    chunk_text = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_chunks_files_file_entry_id",
                        column: x => x.file_entry_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "id", "created_at", "keywords", "modified_at", "name" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "invoice, bill, amount due, payment due date, invoice number, line items, billing address, remit payment to, total balance, vendor invoice, tax rate", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Invoice" },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "receipt, proof of payment, transaction confirmation, payment received, sales receipt, cash register, total paid, payment method, change due, subtotal", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Receipt" },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "financial statement, account statement, monthly summary, credit card statement, bank statement, beginning balance, ending balance, ledger statement", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Statement" },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "contract, agreement, non-disclosure agreement, terms and conditions, legally binding, signatures, execution date, effective date, obligation, breach, clauses, parties hereto", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Contract" },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "application form, registration form, questionnaire, fillable fields, survey, intake form, official application, checkboxes, applicant signature", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Form" },
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "certificate of completion, diploma, official certification, accredited, awarded to, certification of compliance, achievement, credential, birth certificate", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Certificate" },
                    { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "driver license, passport, national ID card, government identification, date of birth, identity verification, photo ID, SSN, social security card", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Identification" },
                    { 8, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "letter, email message, internal memo, memorandum, subject line, dear sir or madam, warm regards, notification letter, formal notice", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Correspondence" },
                    { 9, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "audit report, business report, analytical report, whitepaper, executive summary, findings, assessment, study results, quarterly report, research analysis", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Report" },
                    { 10, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "user manual, technical guide, API reference, instruction manual, operation manual, developer documentation, installation guide, troubleshooting", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Manual" },
                    { 11, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "slide deck, powerpoint presentation, keynote, pitch deck, slide summary, presentation agenda, overview presentation", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Presentation" },
                    { 12, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "spreadsheet data, tabular format, financial model, excel data, csv export, columns and rows, calculated totals, tabular analysis", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Spreadsheet" },
                    { 13, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "resume, curriculum vitae, work experience, employment history, technical skills, education background, professional summary, references", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Resume" },
                    { 14, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "academic research paper, abstract, methodology, literature review, bibliography, citations, peer-reviewed, doi, journal article", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "ResearchPaper" },
                    { 15, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "handwritten notes, meeting notes, personal memo, quick thoughts, scratchpad, bulleted agenda, informal summary", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Note" },
                    { 16, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "tax return, Form W-2, Form 1099, IRS tax filing, tax deduction, gross income, tax year, withholding, revenue service", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "TaxDocument" },
                    { 17, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "insurance policy, claim form, explanation of benefits, coverage details, policy number, deductible, insured party, premium, claim number", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "InsuranceDocument" },
                    { 18, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "medical chart, clinical report, prescription, physician notes, diagnostic results, patient history, medical examination, lab report", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "MedicalRecord" },
                    { 19, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "bank account statement, checking account, savings statement, deposits, withdrawals, routing number, daily balance summary", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "BankStatement" },
                    { 20, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "short story, poem, poetry stanza, novel excerpt, fictional writing, prose, essay, song lyric, creative manuscript", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "CreativeWriting" },
                    { 21, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "uncategorized format, standard text document, general file, unspecified document format", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Other" }
                });

            migrationBuilder.InsertData(
                table: "primary_categories",
                columns: new[] { "id", "created_at", "keywords", "modified_at", "name" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "accounting, banking, payments, billing, invoices, receipts, taxation, payroll, financial statements, balance sheet, general ledger, income, expenses, monetary transactions, wire transfer, credit card statement", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Financial" },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "legal agreements, litigation, compliance, intellectual property, contracts, court orders, power of attorney, statutes, regulations, non-disclosure, terms of service, liabilities, governance, legal counsel", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Legal" },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "healthcare, medical treatment, clinical records, prescriptions, medical diagnosis, lab results, patient charts, physician notes, health insurance claims, pharmacy, clinical trials, pathology", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Medical" },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "government agencies, public records, official identification, immigration, passports, visas, permits, licenses, municipal records, regulatory filings, citizenship, civil documents, social security", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Government" },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "software development, engineering, IT infrastructure, source code, API reference, system architecture, database schema, DevOps, hardware specifications, networking, technical manuals, algorithms", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Technical" },
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "general business operations, strategic planning, management, corporate policies, meeting minutes, company vision, human resources, organizational hierarchy, project management, business proposals", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Business" },
                    { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "teaching, learning, courses, academic research, curricula, textbooks, training materials, lecture notes, certifications, diplomas, school assignments, student records, syllabus", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Educational" },
                    { 8, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "advertising, brand strategy, sales materials, promotional campaigns, press releases, market research, customer outreach, target demographics, social media plans, pitch decks, product launches", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Marketing" },
                    { 9, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "personal or professional communication, letters, email threads, internal memos, formal notices, inter-office messaging, newsletters, standard mail, written communications", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Correspondence" },
                    { 10, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "creative writing, literary work, poetry, stories, screenplays, song lyrics, personal essays, fiction, artwork descriptions, journal entries, manuscripts, creative prose", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Creative" },
                    { 11, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "uncategorized content, miscellaneous files, general text, unidentified material, generic documents", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_chunks_file_entry_id",
                table: "file_chunks",
                column: "file_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_files_document_type_id",
                table: "files",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_files_path",
                table: "files",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_files_primary_category_id",
                table: "files",
                column: "primary_category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_chunks");

            migrationBuilder.DropTable(
                name: "file_sources");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropTable(
                name: "document_types");

            migrationBuilder.DropTable(
                name: "primary_categories");
        }
    }
}
