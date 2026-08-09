using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Lore.Data.Models;

namespace Lore.Data;

public partial class LoreDbContext : DbContext
{
    public DbSet<FileSource> FileSources => Set<FileSource>();
    public DbSet<FileEntry> Files => Set<FileEntry>();
    public DbSet<FileEntryChunk> FileChunks => Set<FileEntryChunk>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<PrimaryCategory> PrimaryCategories => Set<PrimaryCategory>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

    public LoreDbContext(DbContextOptions<LoreDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FileEntry>(entity =>
        {
            entity.HasIndex(f => f.Path).IsUnique();
        });

        modelBuilder
            .Entity<PrimaryCategory>()
            .HasData(
                new PrimaryCategory
                {
                    Id = 1,
                    Name = "Financial",
                    Keywords =
                        "accounting, banking, payments, billing, invoices, receipts, taxation, payroll, financial statements, balance sheet, general ledger, income, expenses, monetary transactions, wire transfer, credit card statement",
                },
                new PrimaryCategory
                {
                    Id = 2,
                    Name = "Legal",
                    Keywords =
                        "legal agreements, litigation, compliance, intellectual property, contracts, court orders, power of attorney, statutes, regulations, non-disclosure, terms of service, liabilities, governance, legal counsel",
                },
                new PrimaryCategory
                {
                    Id = 3,
                    Name = "Medical",
                    Keywords =
                        "healthcare, medical treatment, clinical records, prescriptions, medical diagnosis, lab results, patient charts, physician notes, health insurance claims, pharmacy, clinical trials, pathology",
                },
                new PrimaryCategory
                {
                    Id = 4,
                    Name = "Government",
                    Keywords =
                        "government agencies, public records, official identification, immigration, passports, visas, permits, licenses, municipal records, regulatory filings, citizenship, civil documents, social security",
                },
                new PrimaryCategory
                {
                    Id = 5,
                    Name = "Technical",
                    Keywords =
                        "software development, engineering, IT infrastructure, source code, API reference, system architecture, database schema, DevOps, hardware specifications, networking, technical manuals, algorithms",
                },
                new PrimaryCategory
                {
                    Id = 6,
                    Name = "Business",
                    Keywords =
                        "general business operations, strategic planning, management, corporate policies, meeting minutes, company vision, human resources, organizational hierarchy, project management, business proposals",
                },
                new PrimaryCategory
                {
                    Id = 7,
                    Name = "Educational",
                    Keywords =
                        "teaching, learning, courses, academic research, curricula, textbooks, training materials, lecture notes, certifications, diplomas, school assignments, student records, syllabus",
                },
                new PrimaryCategory
                {
                    Id = 8,
                    Name = "Marketing",
                    Keywords =
                        "advertising, brand strategy, sales materials, promotional campaigns, press releases, market research, customer outreach, target demographics, social media plans, pitch decks, product launches",
                },
                new PrimaryCategory
                {
                    Id = 9,
                    Name = "Correspondence",
                    Keywords =
                        "personal or professional communication, letters, email threads, internal memos, formal notices, inter-office messaging, newsletters, standard mail, written communications",
                },
                new PrimaryCategory
                {
                    Id = 10,
                    Name = "Creative",
                    Keywords =
                        "creative writing, literary work, poetry, stories, screenplays, song lyrics, personal essays, fiction, artwork descriptions, journal entries, manuscripts, creative prose",
                },
                new PrimaryCategory
                {
                    Id = 11,
                    Name = "Other",
                    Keywords =
                        "uncategorized content, miscellaneous files, general text, unidentified material, generic documents",
                }
            );

        modelBuilder
            .Entity<DocumentType>()
            .HasData(
                new DocumentType
                {
                    Id = 1,
                    Name = "Invoice",
                    Keywords =
                        "invoice, bill, amount due, payment due date, invoice number, line items, billing address, remit payment to, total balance, vendor invoice, tax rate",
                },
                new DocumentType
                {
                    Id = 2,
                    Name = "Receipt",
                    Keywords =
                        "receipt, proof of payment, transaction confirmation, payment received, sales receipt, cash register, total paid, payment method, change due, subtotal",
                },
                new DocumentType
                {
                    Id = 3,
                    Name = "Statement",
                    Keywords =
                        "financial statement, account statement, monthly summary, credit card statement, bank statement, beginning balance, ending balance, ledger statement",
                },
                new DocumentType
                {
                    Id = 4,
                    Name = "Contract",
                    Keywords =
                        "contract, agreement, non-disclosure agreement, terms and conditions, legally binding, signatures, execution date, effective date, obligation, breach, clauses, parties hereto",
                },
                new DocumentType
                {
                    Id = 5,
                    Name = "Form",
                    Keywords =
                        "application form, registration form, questionnaire, fillable fields, survey, intake form, official application, checkboxes, applicant signature",
                },
                new DocumentType
                {
                    Id = 6,
                    Name = "Certificate",
                    Keywords =
                        "certificate of completion, diploma, official certification, accredited, awarded to, certification of compliance, achievement, credential, birth certificate",
                },
                new DocumentType
                {
                    Id = 7,
                    Name = "Identification",
                    Keywords =
                        "driver license, passport, national ID card, government identification, date of birth, identity verification, photo ID, SSN, social security card",
                },
                new DocumentType
                {
                    Id = 8,
                    Name = "Correspondence",
                    Keywords =
                        "letter, email message, internal memo, memorandum, subject line, dear sir or madam, warm regards, notification letter, formal notice",
                },
                new DocumentType
                {
                    Id = 9,
                    Name = "Report",
                    Keywords =
                        "audit report, business report, analytical report, whitepaper, executive summary, findings, assessment, study results, quarterly report, research analysis",
                },
                new DocumentType
                {
                    Id = 10,
                    Name = "Manual",
                    Keywords =
                        "user manual, technical guide, API reference, instruction manual, operation manual, developer documentation, installation guide, troubleshooting",
                },
                new DocumentType
                {
                    Id = 11,
                    Name = "Presentation",
                    Keywords =
                        "slide deck, powerpoint presentation, keynote, pitch deck, slide summary, presentation agenda, overview presentation",
                },
                new DocumentType
                {
                    Id = 12,
                    Name = "Spreadsheet",
                    Keywords =
                        "spreadsheet data, tabular format, financial model, excel data, csv export, columns and rows, calculated totals, tabular analysis",
                },
                new DocumentType
                {
                    Id = 13,
                    Name = "Resume",
                    Keywords =
                        "resume, curriculum vitae, work experience, employment history, technical skills, education background, professional summary, references",
                },
                new DocumentType
                {
                    Id = 14,
                    Name = "ResearchPaper",
                    Keywords =
                        "academic research paper, abstract, methodology, literature review, bibliography, citations, peer-reviewed, doi, journal article",
                },
                new DocumentType
                {
                    Id = 15,
                    Name = "Note",
                    Keywords =
                        "handwritten notes, meeting notes, personal memo, quick thoughts, scratchpad, bulleted agenda, informal summary",
                },
                new DocumentType
                {
                    Id = 16,
                    Name = "TaxDocument",
                    Keywords =
                        "tax return, Form W-2, Form 1099, IRS tax filing, tax deduction, gross income, tax year, withholding, revenue service",
                },
                new DocumentType
                {
                    Id = 17,
                    Name = "InsuranceDocument",
                    Keywords =
                        "insurance policy, claim form, explanation of benefits, coverage details, policy number, deductible, insured party, premium, claim number",
                },
                new DocumentType
                {
                    Id = 18,
                    Name = "MedicalRecord",
                    Keywords =
                        "medical chart, clinical report, prescription, physician notes, diagnostic results, patient history, medical examination, lab report",
                },
                new DocumentType
                {
                    Id = 19,
                    Name = "BankStatement",
                    Keywords =
                        "bank account statement, checking account, savings statement, deposits, withdrawals, routing number, daily balance summary",
                },
                new DocumentType
                {
                    Id = 20,
                    Name = "CreativeWriting",
                    Keywords =
                        "short story, poem, poetry stanza, novel excerpt, fictional writing, prose, essay, song lyric, creative manuscript",
                },
                new DocumentType
                {
                    Id = 21,
                    Name = "Other",
                    Keywords =
                        "uncategorized format, standard text document, general file, unspecified document format",
                }
            );
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}

public class LoreDbContextFactory : IDesignTimeDbContextFactory<LoreDbContext>
{
    public LoreDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoreDbContext>();
        optionsBuilder.UseSqlite($"Data Source=../lore.db").UseSnakeCaseNamingConvention();
        return new LoreDbContext(optionsBuilder.Options);
    }
}
