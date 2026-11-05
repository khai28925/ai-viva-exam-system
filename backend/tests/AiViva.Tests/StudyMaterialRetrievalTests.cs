using AiViva.Application.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using FluentAssertions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AiViva.Tests;

public sealed class StudyMaterialRetrievalTests
{
    [Fact]
    public void Chunker_merges_short_slide_pages_and_skips_blank_ones()
    {
        var chunks = StudyMaterialChunker.Chunk([
            "Slide 1: Kiến trúc phân lớp gồm presentation, business và data.",
            "   ",
            "Slide 3: Mỗi lớp chỉ phụ thuộc vào lớp ngay bên dưới nó."
        ]);

        chunks.Should().ContainSingle();
        chunks[0].PageStart.Should().Be(1);
        chunks[0].PageEnd.Should().Be(3);
        chunks[0].Text.Should().Contain("Slide 1").And.Contain("Slide 3");
    }

    [Fact]
    public void Chunker_splits_long_pages_with_overlap_and_keeps_page_numbers()
    {
        var sentence = "Normalization removes update anomalies in relational schemas. ";
        var page = string.Concat(Enumerable.Repeat(sentence, 60));

        var chunks = StudyMaterialChunker.Chunk(["Intro page with a short overview of databases.", page]);

        chunks[0].PageStart.Should().Be(1);
        chunks.Skip(1).Should().OnlyContain(chunk => chunk.PageStart == 2 && chunk.PageEnd == 2);
        chunks.Skip(1).Should().OnlyContain(chunk => chunk.Text.Length <= StudyMaterialChunker.ChunkSize);
        chunks.Count.Should().BeGreaterThan(3);
        // Consecutive windows overlap so a sentence cut at a boundary is still retrievable whole.
        chunks[1].Text.Should().Contain(chunks[2].Text[..30]);
    }

    [Fact]
    public void Retriever_ranks_keyword_matches_and_folds_vietnamese_diacritics()
    {
        var chunks = Enumerable.Range(0, 10).Select(index => Chunk(index, index == 6
            ? "Phân tích yêu cầu phi chức năng: độ trễ, bảo mật."
            : $"Chương {index}: giới thiệu lập trình hướng đối tượng.")).ToArray();

        var result = StudyMaterialRetriever.Retrieve(chunks, "phan tich yeu cau", null, 3);

        result.Should().HaveCount(3);
        result.Should().Contain(chunk => chunk.ChunkIndex == 6);
    }

    [Fact]
    public void Retriever_prefers_semantically_similar_passages_when_embeddings_exist()
    {
        var chunks = new[]
        {
            Chunk(0, "Alpha", [1, 0]),
            Chunk(1, "Beta", [0, 1]),
            Chunk(2, "Gamma", [0.9f, 0.1f])
        };

        var result = StudyMaterialRetriever.Retrieve(chunks, "unrelated words", [1, 0], 2);

        result.Select(chunk => chunk.ChunkIndex).Should().Equal(0, 2);
    }

    [Fact]
    public void Retriever_without_topic_samples_the_whole_syllabus_evenly()
    {
        var chunks = Enumerable.Range(0, 20).Select(index => Chunk(index, $"Section {index}")).ToArray();

        var result = StudyMaterialRetriever.Retrieve(chunks, null, null, 4);

        result.Select(chunk => chunk.ChunkIndex).Should().Equal(0, 5, 10, 15);
    }

    [Fact]
    public void PdfPig_extractor_reads_each_page_of_a_real_pdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("Dependency injection reduces coupling.", 12, new PdfPoint(40, 700), font);
        builder.AddPage(PageSize.A4).AddText("Layered architecture separates concerns.", 12, new PdfPoint(40, 700), font);

        var pages = new PdfPigTextExtractor().ExtractPages(builder.Build());

        pages.Should().HaveCount(2);
        pages[0].Should().Contain("Dependency injection reduces coupling.");
        pages[1].Should().Contain("Layered architecture");
        StudyMaterialChunker.Chunk(pages).Should().ContainSingle()
            .Which.PageEnd.Should().Be(2);
    }

    private static readonly Guid Document = Guid.NewGuid();

    private static RetrievableChunk Chunk(int index, string text, float[]? embedding = null) =>
        new(Guid.NewGuid(), Document, index, index + 1, index + 1, text, embedding);
}
