using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Connections;
using Sofa.Contracts.Intake;
using Sofa.Intake.Data;
using Sofa.Intake.GitHub;
using Sofa.Intake.Requests;
using Wolverine;

namespace Sofa.Intake.Questions;

public static class AnswersOnTheIssue
{
    public static async Task<OutgoingMessages> ReadAsync(
        IssueCommented commented,
        Guid workspaceId,
        Guid connectionId,
        IntakeDb db,
        IdentityGitHubAccounts identity,
        TimeProvider time,
        ILogger<GitHubEventReceived> logger,
        CancellationToken cancellation)
    {
        var messages = new OutgoingMessages();
        if (commented.By.IsBot)
        {
            return messages;
        }

        var requests = await db.Requests.AsNoTracking()
            .Where(r => r.WorkspaceId == workspaceId && r.ConnectionId == connectionId
                && r.Repository.ToLower() == commented.Repository.ToLower() && r.Number == commented.Number)
            .ToListAsync(cancellation);
        foreach (var request in requests)
        {
            var questions = await db.QuestionComments.Where(q => q.RequestId == request.Id).ToListAsync(cancellation);
            if (AnswerRules.WhatTheCommentAnswers(questions, commented.CommentId) is not { } answering)
            {
                continue;
            }

            var question = answering.Question;
            if (!answering.AlreadyAnsweredByThisComment)
            {
                if (AnswerText.Of(commented.Body) is not { } answer)
                {
                    logger.LogInformation(
                        "Took {Login}'s comment on {Repository}#{Number} for no answer: it only quotes or mentions",
                        commented.By.Login, request.Repository, request.Number);
                    continue;
                }

                var answerer = await identity.HolderWithAccessToTheWorkspacesProjectsAsync(workspaceId, commented.By.Id, cancellation);
                if (answerer is null)
                {
                    logger.LogInformation(
                        "Ignored {Login}'s reply on {Repository}#{Number}: their GitHub account is linked to no User with access to Project {Key}",
                        commented.By.Login, request.Repository, request.Number, request.ProjectKey);
                    continue;
                }

                question.AnsweredByCommentId = commented.CommentId;
                question.AnsweredByUserId = answerer.UserId;
                question.Answer = answer;
                question.AnsweredAt = time.GetUtcNow();
                await db.SaveChangesAsync(cancellation);
                logger.LogInformation("{Login} answered Question {QuestionId} on {Key} in reply on the issue",
                    commented.By.Login, question.QuestionId, question.WorkItemKey);
            }

            messages.Add(new QuestionAnsweredOnRequest(
                question.QuestionId, request.Id, request.WorkspaceId, question.AnsweredByUserId!.Value, question.Answer!, question.AnsweredAt!.Value));
        }

        return messages;
    }
}
