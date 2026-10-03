namespace AiViva.Domain.Entities;

public sealed class Question
{
    private Question() { }

    public Question(Guid questionBankId, string content)
    {
        if (questionBankId == Guid.Empty)
        {
            throw new ArgumentException("Question bank ID must not be empty.", nameof(questionBankId));
        }

        Id = Guid.NewGuid();
        QuestionBankId = questionBankId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdateContent(content);
    }

    public Guid Id { get; private set; }

    public Guid QuestionBankId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateContent(string content)
    {
        var normalizedContent = content?.Trim();
        if (string.IsNullOrEmpty(normalizedContent) || normalizedContent.Length > 2000)
        {
            throw new ArgumentException("Content must contain 1 to 2000 characters.", nameof(content));
        }

        Content = normalizedContent;
    }
}
