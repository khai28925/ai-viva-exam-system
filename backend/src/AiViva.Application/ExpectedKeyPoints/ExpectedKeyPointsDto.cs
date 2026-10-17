using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using AiViva.Domain.Entities;

namespace AiViva.Application.ExpectedKeyPoints
{
    public record ExpectedKeyPointsDto(
    Guid Id,
    Guid QuestionId,
    String Content,
    int DisplayOrder,
    DateTimeOffset CreatedAt
    );

    public record CreateExpectedKeyPointsRequest
    {
        public CreateExpectedKeyPointsRequest(String content, Guid questionId)
        {
            QuestionId = questionId;
            Content = content.Trim() ?? string.Empty;
        }
        [Required(ErrorMessage = "The Expected Key Points must have a question GUID.")]
        Guid QuestionId { get; }
        [Required(ErrorMessage = "Content is required.")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 2000 characters.")]
        String Content { get; }
    }

    public sealed record UpdateExpectedKeyPointsRequest
    {
        public UpdateExpectedKeyPointsRequest(String content, Guid questionId)
        {
            QuestionId = questionId;
            Content = content.Trim() ?? string.Empty;
        }
        [Required(ErrorMessage = "The Expected Key Points must have a question GUID.")]
        Guid QuestionId { get; }
        [Required(ErrorMessage = "Content is required.")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 2000 characters.")]
        String Content { get; }
    }
}

